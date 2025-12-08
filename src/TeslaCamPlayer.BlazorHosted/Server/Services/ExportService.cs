using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public class ExportService : IExportService
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly IClipsService _clipsService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _exportDirectory;

    public ExportService(ISettingsProvider settingsProvider, IClipsService clipsService, IServiceScopeFactory scopeFactory)
    {
        _settingsProvider = settingsProvider;
        _clipsService = clipsService;
        _scopeFactory = scopeFactory;
        _exportDirectory = Path.Combine(_settingsProvider.Settings.ClipsRootPath, "ExportedClips");
        if (!Directory.Exists(_exportDirectory))
        {
            Directory.CreateDirectory(_exportDirectory);
        }
    }

    public IEnumerable<ExportJob> GetJobs()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
        return dbContext.ExportJobs.OrderByDescending(j => j.CreatedAt).ToList();
    }

    public async Task<ExportJob> GetJobAsync(Guid id)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
        return await dbContext.ExportJobs.FindAsync(id);
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

        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
            dbContext.ExportJobs.Add(job);
            await dbContext.SaveChangesAsync();
        }

        // Run in background
        _ = Task.Run(() => ProcessExportAsync(jobId, request));

        return job;
    }

    private async Task ProcessExportAsync(Guid jobId, ExportRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
        var job = await dbContext.ExportJobs.FindAsync(jobId);

        if (job == null)
        {
            Log.Error($"Export job {jobId} not found in database during processing start.");
            return;
        }

        try
        {
            job.Status = ExportStatus.Processing;
            await dbContext.SaveChangesAsync();

            // 1. Get the clip details to find the files
            var clips = await _clipsService.GetClipsAsync(SyncMode.None);
            var clip = clips.FirstOrDefault(c =>
                c.StartDate == request.ClipStartDate &&
                ((c.Event?.Timestamp == null && request.EventFolderName == null) || // Recent clip
                 (c.Event != null && c.Segments.Any(s => s.CameraFront?.EventFolderName == request.EventFolderName))));

            if (clip == null)
            {
                 clip = clips.FirstOrDefault(c => c.StartDate == request.ClipStartDate);
            }

            if (clip == null)
            {
                throw new Exception("Clip not found.");
            }

            // 2. Identify relevant segments and files
            var segments = clip.Segments.Where(s => s.EndDate > request.StartTime && s.StartDate < request.EndTime).OrderBy(s => s.StartDate).ToList();

            if (!segments.Any())
            {
                throw new Exception("No video segments found for the selected time range.");
            }

            // 3. Build FFmpeg inputs
            var activeCameras = request.SelectedCameras.Where(c => c != Cameras.Unknown).ToList();
            if (!activeCameras.Contains(request.MainCamera) && request.MainCamera != Cameras.Unknown) activeCameras.Add(request.MainCamera);

            var tempFiles = new List<string>();
            var validCameras = new List<Cameras>();
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
                }

                if (sb.Length > 0)
                {
                    await File.WriteAllTextAsync(concatListPath, sb.ToString());
                    tempFiles.Add(concatListPath);
                    cameraInputMap[cam] = inputIndex++;
                    validCameras.Add(cam);
                }
            }

            if (!validCameras.Any())
            {
                throw new Exception("No video files found for the selected cameras in this time range.");
            }

            if (!validCameras.Contains(request.MainCamera))
            {
                request.MainCamera = validCameras.First();
            }

            activeCameras = validCameras;

            var filterComplex = new StringBuilder();

            var startOffset = (request.StartTime - segments.First().StartDate).TotalSeconds;
            if (startOffset < 0) startOffset = 0;

            var duration = (request.EndTime - request.StartTime).TotalSeconds;

            var otherCameras = activeCameras.Where(c => c != request.MainCamera).ToList();
            var mainCamIndex = cameraInputMap[request.MainCamera];
            var otherCamIndices = otherCameras.Select(c => cameraInputMap[c]).ToList();

            string mainLabel = $"[{mainCamIndex}:v]";
            string mainScaled = $"[main_s]";

            if (activeCameras.Count <= 4)
            {
                // Layout A: Main Left (1440x1080), Column Right.
                filterComplex.Append($"{mainLabel}scale=1440:1080:force_original_aspect_ratio=decrease,pad=1440:1080:(ow-iw)/2:(oh-ih)/2[main_s];");

                int yPos = 0;
                for (int i = 0; i < otherCamIndices.Count; i++)
                {
                    filterComplex.Append($"[{otherCamIndices[i]}:v]scale=480:360:force_original_aspect_ratio=decrease,pad=480:360:(ow-iw)/2:(oh-ih)/2[s{i}];");
                }

                // Base canvas
                filterComplex.Append($"nullsrc=size=1920x1080[base];");
                filterComplex.Append($"[base][main_s]overlay=0:0[tmp1];");

                string lastTmp = "tmp1";
                for (int i = 0; i < otherCamIndices.Count; i++)
                {
                    string nextTmp = $"tmp{i + 2}";
                    if (i == otherCamIndices.Count - 1) nextTmp = "outv";

                    filterComplex.Append($"[{lastTmp}][s{i}]overlay=1440:{yPos}[{nextTmp}]"); // No semicolon here
                    if (i < otherCamIndices.Count - 1)
                    {
                        filterComplex.Append(";");
                    }
                    yPos += 360;
                    lastTmp = nextTmp;
                }

                if (otherCamIndices.Count == 0)
                {
                    // Just main
                     filterComplex.Replace("[tmp1]", "[outv]");
                     // Remove the trailing semicolon from replacement if any?
                     // Wait, if 0 other cams, we appended "[base][main_s]overlay=0:0[tmp1];"
                     // Replacing [tmp1] with [outv] leaves the semicolon.
                     // And it's the last command.
                     // So we need to remove the last semicolon.
                     if (filterComplex[filterComplex.Length - 1] == ';')
                     {
                         filterComplex.Length--;
                     }
                }
            }
            else
            {
                // Layout B: Main (960x720) Top-Left. Flow others.
                filterComplex.Append($"{mainLabel}scale=960:720:force_original_aspect_ratio=decrease,pad=960:720:(ow-iw)/2:(oh-ih)/2[main_s];");

                filterComplex.Append($"nullsrc=size=1920x1080[base];");
                filterComplex.Append($"[base][main_s]overlay=0:0[tmp1];");

                var slots = new[]
                {
                    (960, 0), (1440, 0),
                    (960, 360), (1440, 360),
                    (0, 720), (480, 720), (960, 720), (1440, 720)
                };

                string lastTmp = "tmp1";
                var limit = Math.Min(otherCamIndices.Count, slots.Length);
                for (int i = 0; i < limit; i++)
                {
                    filterComplex.Append($"[{otherCamIndices[i]}:v]scale=480:360:force_original_aspect_ratio=decrease,pad=480:360:(ow-iw)/2:(oh-ih)/2[s{i}];");

                    string nextTmp = $"tmp{i + 2}";
                    if (i == limit - 1) nextTmp = "outv";

                    filterComplex.Append($"[{lastTmp}][s{i}]overlay={slots[i].Item1}:{slots[i].Item2}[{nextTmp}]");
                     if (i < limit - 1)
                    {
                        filterComplex.Append(";");
                    }
                    lastTmp = nextTmp;
                }
            }

            var inputArgs = new StringBuilder();
            foreach (var cam in activeCameras)
            {
                 var concatPath = tempFiles[cameraInputMap[cam]];
                 inputArgs.Append($"-f concat -safe 0 -i \"{concatPath}\" ");
            }

            var outputFilePath = GetExportFilePath(job.FileName);

            var ffmpegArgs = $"{inputArgs} -filter_complex \"{filterComplex}\" -map \"[outv]\" -ss {startOffset} -t {duration} -c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p \"{outputFilePath}\"";

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
            await dbContext.SaveChangesAsync();

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
            await dbContext.SaveChangesAsync();
        }
    }
}
