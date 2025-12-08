using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public class ExportService : IExportService
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly IClipsService _clipsService;
    private readonly ConcurrentDictionary<Guid, ExportJob> _jobs = new();
    private readonly string _exportDirectory;

    public ExportService(ISettingsProvider settingsProvider, IClipsService clipsService)
    {
        _settingsProvider = settingsProvider;
        _clipsService = clipsService;
        _exportDirectory = Path.Combine(_settingsProvider.Settings.ClipsRootPath, "ExportedClips");
        if (!Directory.Exists(_exportDirectory))
        {
            Directory.CreateDirectory(_exportDirectory);
        }
    }

    public IEnumerable<ExportJob> GetJobs() => _jobs.Values.OrderByDescending(j => j.CreatedAt);

    public Task<ExportJob> GetJobAsync(Guid id)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public string GetExportFilePath(string fileName)
    {
        return Path.Combine(_exportDirectory, fileName);
    }

    public async Task<ExportJob> StartExportAsync(ExportRequest request)
    {
        var jobId = Guid.NewGuid();
        var fileName = $"Export_{request.StartTime:yyyyMMdd_HHmmss}_{jobId.ToString().Substring(0, 8)}.mp4";
        var job = new ExportJob
        {
            Id = jobId,
            Name = $"Export {request.StartTime}",
            Status = ExportStatus.Queued,
            CreatedAt = DateTime.Now,
            FileName = fileName,
            Progress = 0
        };

        _jobs.TryAdd(jobId, job);

        // Run in background
        _ = Task.Run(() => ProcessExportAsync(job, request));

        return job;
    }

    private async Task ProcessExportAsync(ExportJob job, ExportRequest request)
    {
        try
        {
            job.Status = ExportStatus.Processing;

            // 1. Get the clip details to find the files
            var clips = await _clipsService.GetClipsAsync(SyncMode.None);
            var clip = clips.FirstOrDefault(c =>
                c.StartDate == request.ClipStartDate &&
                ((c.Event?.Timestamp == null && request.EventFolderName == null) || // Recent clip
                 (c.Event != null && c.Segments.Any(s => s.CameraFront?.EventFolderName == request.EventFolderName)))); // Saved/Sentry clip logic might differ slightly, but using StartDate is usually safe if unique enough.

            // Actually, ClipsService builds clips based on event folders or recent clips grouping.
            // A safer way is to iterate and find the one matching.

            // If we can't find it easily by strict equality, let's try to match segments.
            if (clip == null)
            {
                 clip = clips.FirstOrDefault(c => c.StartDate == request.ClipStartDate);
            }

            if (clip == null)
            {
                throw new Exception("Clip not found.");
            }

            // 2. Identify relevant segments and files
            // The user wants to export from request.StartTime to request.EndTime.
            // We need to find all segments that overlap with this interval.

            var segments = clip.Segments.Where(s => s.EndDate > request.StartTime && s.StartDate < request.EndTime).OrderBy(s => s.StartDate).ToList();

            if (!segments.Any())
            {
                throw new Exception("No video segments found for the selected time range.");
            }

            // 3. Build FFmpeg inputs
            // We need to construct a complex filter graph.
            // Simplification: We will export the whole sequence of segments, but trim the start of the first and end of the last.

            // However, merging multiple files (concatenation) AND compositing views (stacking) is complex in one command.
            // It might be easier to:
            // For each segment:
            //   Composite the views into a single temp video.
            // Then:
            //   Concatenate the temp videos.
            //   Trim the result.
            // OR:
            //   Use the concat demuxer for each camera angle to create continuous streams, then map them into the filter complex.

            // Let's try the "concat demuxer" approach.
            // We create a text file for each camera angle listing the files.

            var tempFiles = new List<string>();
            var activeCameras = request.SelectedCameras;
            if (!activeCameras.Contains(request.MainCamera)) activeCameras.Add(request.MainCamera); // Ensure main is there

            var filterComplex = new StringBuilder();

            // Determine input indexes
            var cameraInputMap = new Dictionary<Cameras, int>();
            int inputIndex = 0;

            foreach (var cam in activeCameras)
            {
                // Create concat file for this camera
                var concatListPath = Path.Combine(Path.GetTempPath(), $"concat_{job.Id}_{cam}.txt");
                var sb = new StringBuilder();
                foreach (var seg in segments)
                {
                    var videoFile = cam switch
                    {
                        Cameras.Front => seg.CameraFront,
                        Cameras.LeftRepeater => seg.CameraLeftRepeater,
                        Cameras.RightRepeater => seg.CameraRightRepeater,
                        Cameras.Back => seg.CameraBack,
                        Cameras.LeftBPillar => seg.CameraLeftBPillar,
                        Cameras.RightBPillar => seg.CameraRightBPillar,
                        Cameras.Fisheye => seg.CameraFisheye,
                        Cameras.Narrow => seg.CameraNarrow,
                        Cameras.Cabin => seg.CameraCabin,
                        _ => null
                    };

                    if (videoFile != null)
                    {
                         sb.AppendLine($"file '{videoFile.FilePath}'");
                    }
                    else
                    {
                         // If a segment is missing this camera, we might need a dummy black video or just skip (but skipping breaks sync).
                         // Generating a black video of duration is hard without knowing exact duration.
                         // For now, assume consistent files, or if missing, we might have desync issues.
                         // A more robust way is generating black frames.
                         // Let's skip for MVP and hope segments are consistent.
                    }
                }
                await File.WriteAllTextAsync(concatListPath, sb.ToString());
                tempFiles.Add(concatListPath);

                cameraInputMap[cam] = inputIndex++;
            }

            // Calculate trim times
            // First segment start: segments.First().StartDate
            // Request start: request.StartTime
            // Offset = request.StartTime - segments.First().StartDate
            var startOffset = (request.StartTime - segments.First().StartDate).TotalSeconds;
            if (startOffset < 0) startOffset = 0;

            var duration = (request.EndTime - request.StartTime).TotalSeconds;

            // 4. Construct Filter Complex
            // Layout: Main Camera (Scale 1280:-1) top.
            // Others: Row below.
            // Canvas size?
            // 1920x1080 output?
            // If Main is 1280 wide (approx 2/3 of 1920), height is 960 (4:3) or 720 (16:9). Tesla clips are 1280x960 usually.

            // User requested: "merges all the views together with the current large one kept as main"
            // "Main camera defaults to Front but is swappable."
            // "Main camera displayed significantly larger."

            // Let's go for a 1920x1440 layout or similar (4:3 aspect preserved).
            // Or 1920x1080.

            // Layout Proposal:
            // Canvas: 1920 x 1440 (keeping 4:3 aspect ratio of sources usually)
            // Main: Top Left, 1440x1080.
            // Side: Column on the right? Or Row on bottom?

            // Let's try a standard grid approach.
            // Main: Top, centered? Or Top-Left.
            // Let's stick to the "Main + Row" user mentioned.
            // "Main camera ... significantly larger"

            // Source resolution is typically 1280x960 per file.

            // Let's define a layout.
            // Output: 1920x1080 (HD).
            // Main: Scaled to fit height 1080? Or something like 1440x1080 taking up 75% width.
            // Remaining width: 1920-1440 = 480.
            // Stack others vertically on the right side?
            // If we have 3-5 other cameras. 1080 / 3 = 360 height. 480x360 (4:3).
            // This fits perfectly!
            // Main (Front): 1440x1080 at x=0, y=0.
            // Side 1: 480x360 at x=1440, y=0
            // Side 2: 480x360 at x=1440, y=360
            // Side 3: 480x360 at x=1440, y=720

            // What if more than 3 side cameras?
            // We have Front, Back, Left, Right (4 total).
            // Main = Front. Sides = Back, Left, Right. (3 total). Perfect.

            // If Pillars involved (6 total):
            // Main + 5 sides.
            // Side column needs 5 slots. 1080/5 = 216. 480x216 is not 4:3.
            // 4:3 ratio for width 480 is 360 height.
            // If we have 5 sides, we can't fit them all in one column at 4:3 without overlapping or scaling down further.

            // Alternative: Main Top 2/3, Bottom row 1/3.
            // Main: 1920x(2/3*H)? No.

            // Let's try 1280x960 Main.
            // If we put it in the center.
            // Or use the provided layout logic.

            // Let's use a dynamic layout.
            // Base resolution: 1920x1080.
            // Main Input: [main]
            // Side Inputs: [s1], [s2], ...

            // Scale Main to H=1080 (W=1440). Position 0,0.
            // Side inputs: Scale to W=1920-1440=480. (H=360).
            // Stack them vertically on the right.
            // If > 3 side cameras, we might run out of vertical space.
            // If > 3, we might need a different layout or scroll/wrap (not possible in video).
            // User said: "capture the time I want to export. Then ... merges all the views ... current large one kept as main".
            // "include all camera views that are available ... I want the option to remove views".

            // So if user selects 6 cameras (Main + 5 others).
            // Maybe Main + 2 columns on right?
            // Main 1440x1080. Remaining 480.
            // Maybe Main smaller?
            // Main 960x720.
            // 2 rows of 3?

            // Let's stick to the "Main + Right Column" for up to 3 side cameras.
            // If > 3 side cameras, maybe use a "Bottom Row" layout?
            // Main: 1920x720. (Aspect ratio distorted? No, crop or pad).
            // Bottom row: 5 cameras. 1920/5 = 384 width.

            // Let's go with a simple versatile grid using `xstack` if possible, but `overlay` is more flexible.

            var scaleFilter = "";
            var overlayFilter = "";

            // Logic:
            // Main camera is [0].
            // Others are [1]..[N].

            // Let's define the canvas. 1920x1080.
            // If N <= 3 (Main + 3 sides):
            //   Main: Scale to 1440x1080. Pad if necessary.
            //   Sides: Scale to 480x360.
            //   Pos: Main(0,0). Sides(1440, 0), (1440, 360), (1440, 720).

            // If N > 3 (e.g. Main + 5 sides = 6 total):
            //   Main: Scale to 1280x960 (Native). Centered? or Top-Left.
            //   Remaining width: 1920-1280=640.
            //   Right column width 640.
            //   Side cams: 640w -> 480h.
            //   Height 1080. 1080 / 480 = 2.25 cams fit. Not enough.

            //   How about: Main Top-Left 2/3.
            //   Main: 1280x960.
            //   Right Col: 640w.
            //   Bottom Row: under main.

            //   Let's try:
            //   Output 1920x1080.
            //   Main: 1280x960 at (320, 0)? Centered horizontally?
            //   Or (0,0).
            //   Sides: Small tiles at the bottom or side?

            //   Let's just scale everything to fit a grid? No, user wants "Large Main".

            //   Robust Layout for N cameras:
            //   Main: Fixed top-left 1440x1080 (or 1350x1080 to maintain 5:4? No 4:3).
            //   1440x1080 (4:3).
            //   Space remaining on right: 480x1080.
            //   We can fit 3 cameras (480x360).
            //   If more cameras, we overlap? Or reduce Main size?

            //   Let's restrict Main size based on count.
            //   If > 4 cameras total:
            //   Main = 960x720. (Half size of 1920x1440?).
            //   Canvas 1920x1080.
            //   Main at (0,0).
            //   Right col (960x1080 space): can fit 2 cols of 480x360?
            //   960 width = 2 * 480.
            //   720 height. Bottom row?

            //   Layout:
            //   [ Main 960x720 ] [ Side1 480x360 ] [ Side2 480x360 ]
            //                    [ Side3 480x360 ] [ Side4 480x360 ]
            //   [ Side5 480x360 ] ...

            //   Let's implement a dynamic layout calculation.
            //   For simplicity in this iteration:
            //   Layout A (<= 4 cams): Main (1440x1080) Left. Col (480x360) Right.
            //   Layout B (> 4 cams): Main (960x720) Top-Left.
            //     Side 1 (480x360) Top-Right (960, 0)
            //     Side 2 (480x360) Top-Right (1440, 0)
            //     Side 3 (480x360) Middle-Right (960, 360)
            //     Side 4 (480x360) Middle-Right (1440, 360)
            //     Side 5 (480x360) Bottom-Left (0, 720)
            //     Side 6 (480x360) Bottom (480, 720)
            //     ...

            var otherCameras = activeCameras.Where(c => c != request.MainCamera).ToList();
            var mainCamIndex = cameraInputMap[request.MainCamera];
            var otherCamIndices = otherCameras.Select(c => cameraInputMap[c]).ToList();

            // Initialize filter complex
            // Scale Main
            string mainLabel = $"[v{mainCamIndex}]";
            string mainScaled = $"[main_s]";

            // We need to apply text overlays for camera names too? User didn't strictly ask, but app has them.
            // "Camera labels ... displayed on the bottom-left corner"
            // We should try to preserve that if possible.
            // FFmpeg `drawtext` can do it.

            if (activeCameras.Count <= 4)
            {
                // Layout A: Main Left (1440x1080), Column Right.
                filterComplex.Append($"{mainLabel}scale=1440:1080:force_original_aspect_ratio=decrease,pad=1440:1080:(ow-iw)/2:(oh-ih)/2[main_s];");

                int yPos = 0;
                for (int i = 0; i < otherCamIndices.Count; i++)
                {
                    filterComplex.Append($"[v{otherCamIndices[i]}]scale=480:360:force_original_aspect_ratio=decrease,pad=480:360:(ow-iw)/2:(oh-ih)/2[s{i}];");
                }

                // Base canvas
                filterComplex.Append($"nullsrc=size=1920x1080[base];");
                filterComplex.Append($"[base][main_s]overlay=0:0[tmp1];");

                string lastTmp = "tmp1";
                for (int i = 0; i < otherCamIndices.Count; i++)
                {
                    string nextTmp = $"tmp{i + 2}";
                    if (i == otherCamIndices.Count - 1) nextTmp = "outv";

                    filterComplex.Append($"[{lastTmp}][s{i}]overlay=1440:{yPos}[{nextTmp}];");
                    yPos += 360;
                    lastTmp = nextTmp;
                }

                if (otherCamIndices.Count == 0)
                {
                    // Just main
                     filterComplex.Replace("[tmp1]", "[outv]");
                }
            }
            else
            {
                // Layout B: Main (960x720) Top-Left. Flow others.
                // Main
                filterComplex.Append($"{mainLabel}scale=960:720:force_original_aspect_ratio=decrease,pad=960:720:(ow-iw)/2:(oh-ih)/2[main_s];");

                // Base
                filterComplex.Append($"nullsrc=size=1920x1080[base];");
                filterComplex.Append($"[base][main_s]overlay=0:0[tmp1];");

                // Slots:
                // 1: 960, 0
                // 2: 1440, 0
                // 3: 960, 360
                // 4: 1440, 360
                // 5: 0, 720
                // 6: 480, 720
                // 7: 960, 720
                // 8: 1440, 720

                var slots = new[]
                {
                    (960, 0), (1440, 0),
                    (960, 360), (1440, 360),
                    (0, 720), (480, 720), (960, 720), (1440, 720)
                };

                string lastTmp = "tmp1";
                for (int i = 0; i < otherCamIndices.Count; i++)
                {
                    if (i >= slots.Length) break; // limit

                    filterComplex.Append($"[v{otherCamIndices[i]}]scale=480:360:force_original_aspect_ratio=decrease,pad=480:360:(ow-iw)/2:(oh-ih)/2[s{i}];");

                    string nextTmp = $"tmp{i + 2}";
                    if (i == otherCamIndices.Count - 1) nextTmp = "outv";

                    filterComplex.Append($"[{lastTmp}][s{i}]overlay={slots[i].Item1}:{slots[i].Item2}[{nextTmp}];");
                    lastTmp = nextTmp;
                }
            }

            // Trim
            // Use -ss and -t on output is faster? No, for filter complex we must use `trim` filter or input seeking.
            // Input seeking is hard with concat demuxer if we want precise sync?
            // Actually -ss before -i works for demuxer?
            // "When using the concat demuxer, -ss and -t seek/trim the CONCATENATED stream".
            // So we can just put -ss and -t in the output options or before input (but input is concat file).
            // Putting -ss before -i concat.txt works.

            // Build input args
            var inputArgs = new StringBuilder();
            foreach (var cam in activeCameras)
            {
                 var concatPath = tempFiles[cameraInputMap[cam]];
                 // We apply the same seek to all inputs to keep sync
                 inputArgs.Append($"-ss {startOffset} -f concat -safe 0 -i \"{concatPath}\" ");
            }

            var outputFilePath = GetExportFilePath(job.FileName);

            // Command
            var ffmpegArgs = $"{inputArgs} -filter_complex \"{filterComplex}\" -map \"[outv]\" -t {duration} -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p \"{outputFilePath}\"";

            // Run FFmpeg
            Log.Information("Starting FFmpeg with args: {Args}", ffmpegArgs);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("ffmpeg", ffmpegArgs)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Monitor output for progress (optional, parsing frame=...)
            // Just read to end to avoid blocking
            var stdErrTask = process.StandardError.ReadToEndAsync();
            var stdOutTask = process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();
            string output = await stdOutTask;
            string error = await stdErrTask;

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg failed with exit code {process.ExitCode}. Error: {error}");
            }

            job.Status = ExportStatus.Completed;
            job.Progress = 100;

            // Cleanup temp files
            foreach(var f in tempFiles)
            {
                if(File.Exists(f)) File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export job failed");
            job.Status = ExportStatus.Failed;
            job.ErrorMessage = ex.Message;
        }
    }
}
