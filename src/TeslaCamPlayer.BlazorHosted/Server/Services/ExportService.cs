using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    private static readonly SemaphoreSlim _exportSemaphore = new(1);
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

            // SECURITY: Prevent DoS by limiting the number of queued jobs
            var queuedCount = await dbContext.ExportJobs.CountAsync(j => j.Status == ExportStatus.Queued || j.Status == ExportStatus.Processing);
            if (queuedCount >= 5)
            {
                throw new InvalidOperationException("Export queue is full (max 5 jobs). Please wait for current jobs to finish.");
            }

            dbContext.ExportJobs.Add(job);
            await dbContext.SaveChangesAsync();
        }

        // Run in background
        _ = Task.Run(async () =>
        {
            await _exportSemaphore.WaitAsync();
            try
            {
                await ProcessExportAsync(jobId, request);
            }
            finally
            {
                _exportSemaphore.Release();
            }
        });

        return job;
    }

    private async Task ProcessExportAsync(Guid jobId, ExportRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
        var julesService = scope.ServiceProvider.GetRequiredService<IJulesApiService>();
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
            if (!activeCameras.Any())
            {
                activeCameras = Enum.GetValues(typeof(Cameras))
                    .Cast<Cameras>()
                    .Where(c => c != Cameras.Unknown)
                    .ToList();
            }

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
                         // SECURITY: Escape single quotes in filename to prevent breaking out of the quoted string in FFmpeg concat file
                         // e.g. "file 'foo'bar.mp4'" -> "file 'foo'\''bar.mp4'"
                         var escapedPath = videoFile.FilePath.Replace("'", "'\\''");
                         sb.AppendLine($"file '{escapedPath}'");
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

            // Base canvas - Black background to avoid green bars
            filterComplex.Append($"color=s=1920x1080:c=black[base];");

            // Main Camera processing (Top Center)
            string mainCamName = System.Text.RegularExpressions.Regex.Replace(request.MainCamera.ToString(), "([a-z])([A-Z])", "$1 $2");
            filterComplex.Append($"[{mainCamIndex}:v]scale=960:720:force_original_aspect_ratio=decrease,pad=960:720:(ow-iw)/2:(oh-ih)/2:color=black,");
            filterComplex.Append($"drawtext=text='{mainCamName}':fontcolor=white:fontsize=24:x=10:y=h-th-10:box=1:boxcolor=black@0.5:fontfile='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'[main_s];");

            // Overlay Main on Base
            filterComplex.Append($"[base][main_s]overlay=480:0"); // (1920-960)/2 = 480

            if (otherCameras.Count == 0)
            {
                filterComplex.Append($"[outv]");
            }
            else
            {
                filterComplex.Append($"[tmp_main];");

                string lastTmp = "tmp_main";
                int totalSideWidth = otherCameras.Count * 480;
                int startX = (1920 - totalSideWidth) / 2;

                for (int i = 0; i < otherCameras.Count; i++)
                {
                    var cam = otherCameras[i];
                    var camIndex = cameraInputMap[cam];
                    string camName = System.Text.RegularExpressions.Regex.Replace(cam.ToString(), "([a-z])([A-Z])", "$1 $2");

                    int xPos = startX + (i * 480);
                    int yPos = 720;

                    // Side Camera processing (Bottom Row)
                    filterComplex.Append($"[{camIndex}:v]scale=480:360:force_original_aspect_ratio=decrease,pad=480:360:(ow-iw)/2:(oh-ih)/2:color=black,");
                    filterComplex.Append($"drawtext=text='{camName}':fontcolor=white:fontsize=24:x=10:y=h-th-10:box=1:boxcolor=black@0.5:fontfile='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'[s{i}];");

                    string nextTmp = (i == otherCameras.Count - 1) ? "outv" : $"tmp_{i}";
                    filterComplex.Append($"[{lastTmp}][s{i}]overlay={xPos}:{yPos}[{nextTmp}]");

                    if (i < otherCameras.Count - 1)
                    {
                         filterComplex.Append(";");
                    }
                    lastTmp = nextTmp;
                }
            }

            var outputFilePath = GetExportFilePath(job.FileName);

            // SECURITY: Use ArgumentList to prevent command injection
            var processStartInfo = new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var cam in activeCameras)
            {
                var concatPath = tempFiles[cameraInputMap[cam]];
                processStartInfo.ArgumentList.Add("-f");
                processStartInfo.ArgumentList.Add("concat");
                processStartInfo.ArgumentList.Add("-safe");
                processStartInfo.ArgumentList.Add("0");
                processStartInfo.ArgumentList.Add("-i");
                processStartInfo.ArgumentList.Add(concatPath);
            }

            processStartInfo.ArgumentList.Add("-filter_complex");
            processStartInfo.ArgumentList.Add(filterComplex.ToString());

            processStartInfo.ArgumentList.Add("-map");
            processStartInfo.ArgumentList.Add("[outv]");

            processStartInfo.ArgumentList.Add("-ss");
            processStartInfo.ArgumentList.Add(startOffset.ToString(CultureInfo.InvariantCulture));

            processStartInfo.ArgumentList.Add("-t");
            processStartInfo.ArgumentList.Add(duration.ToString(CultureInfo.InvariantCulture));

            processStartInfo.ArgumentList.Add("-c:v");
            processStartInfo.ArgumentList.Add("libx264");

            processStartInfo.ArgumentList.Add("-preset");
            processStartInfo.ArgumentList.Add("veryfast");

            processStartInfo.ArgumentList.Add("-crf");
            processStartInfo.ArgumentList.Add("23");

            processStartInfo.ArgumentList.Add("-pix_fmt");
            processStartInfo.ArgumentList.Add("yuv420p");

            processStartInfo.ArgumentList.Add(outputFilePath);

            // Reconstruct args string for logging only
            var argsLog = string.Join(" ", processStartInfo.ArgumentList.Select(a => a.Contains(" ") ? $"\"{a}\"" : a));
            Log.Information("Starting FFmpeg with args: {Args}", argsLog);

            var process = new Process
            {
                StartInfo = processStartInfo
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
            await julesService.ReportErrorAsync(ex, $"Export Job {jobId} Failed");
        }
    }
}
