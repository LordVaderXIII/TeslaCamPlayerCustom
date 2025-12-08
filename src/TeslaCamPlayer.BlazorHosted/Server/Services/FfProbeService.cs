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

			var process = new Process
			{
				StartInfo = new ProcessStartInfo(ExePath)
				{
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
					UseShellExecute = false,
					Arguments = videoFilePath
				}
			};

			process.Start();

			// To avoid deadlocks, always read the output stream first.
			var outputTask = process.StandardError.ReadToEndAsync();

			// We also need to consume StandardOutput to prevent deadlock if the buffer fills up,
			// even though we don't use it.
			var stdOutTask = process.StandardOutput.ReadToEndAsync();

			await process.WaitForExitAsync();

			var output = await outputTask;
			await stdOutTask;

			return Helpers.ParseFfProbeOutputHelper.GetDuration(output);
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