using System.Diagnostics;
using Serilog;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public abstract class FfProbeService : IFfProbeService
{
	protected abstract string ExePath { get; }


	public async Task<TimeSpan?> GetVideoFileDurationAsync(string videoFilePath)
	{
		try
		{
			Log.Information("Get video duration for video {Path}", videoFilePath);

			// Optimization: Use specific ffprobe flags to output only the duration in seconds.
			// This reduces process output parsing overhead and avoids reading the entire file metadata.
			// -v error: Suppress logging
			// -show_entries format=duration: Show only duration
			// -of default=noprint_wrappers=1:nokey=1: specific format (value only)
			var process = new Process
			{
				StartInfo = new ProcessStartInfo(ExePath)
				{
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
					UseShellExecute = false
				}
			};

			process.StartInfo.ArgumentList.Add("-v");
			process.StartInfo.ArgumentList.Add("error");
			process.StartInfo.ArgumentList.Add("-show_entries");
			process.StartInfo.ArgumentList.Add("format=duration");
			process.StartInfo.ArgumentList.Add("-of");
			process.StartInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
			process.StartInfo.ArgumentList.Add(videoFilePath);

			process.Start();

			// To avoid deadlocks, always read the output stream first.
			var stdErrTask = process.StandardError.ReadToEndAsync();
			var stdOutTask = process.StandardOutput.ReadToEndAsync();

			await process.WaitForExitAsync();

			var stdErr = await stdErrTask;
			var stdOut = await stdOutTask;

			// Try to parse from stdout (optimized path)
			var duration = Helpers.ParseFfProbeOutputHelper.GetDuration(stdOut);
			if (duration.HasValue)
			{
				return duration;
			}

			// Fallback to stderr parsing (legacy behavior or if optimized args failed silently but stderr has info)
			return Helpers.ParseFfProbeOutputHelper.GetDuration(stdErr);
		}
		catch (Exception e)
		{
			Log.Error(e, "Failed to get video file duration for {Path}", videoFilePath);
			return null;
		}
	}
}

public class FfProbeServiceWindows : FfProbeService
{
	protected override string ExePath { get; } = Path.Combine(AppContext.BaseDirectory, "lib", "ffprobe.exe");
}

public class FfProbeServiceDocker : FfProbeService
{
	protected override string ExePath { get; } = "ffprobe";
}
