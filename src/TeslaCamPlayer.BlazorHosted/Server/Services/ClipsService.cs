using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using TeslaCamPlayer.BlazorHosted.Server.Data;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Services;

public partial class ClipsService : IClipsService
{
	private const string NoThumbnailImageUrl = "/img/no-thumbnail.png";
    private const string ClipsCacheKey = "Clips_All";

	private static readonly Regex FileNameRegex = FileNameRegexGenerated();
	private static readonly SemaphoreSlim FfProbeSemaphore = new(10);
	private static readonly SemaphoreSlim _syncSemaphore = new(1);
	
	private readonly ISettingsProvider _settingsProvider;
	private readonly IFfProbeService _ffProbeService;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IMemoryCache _memoryCache;

	public ClipsService(
		ISettingsProvider settingsProvider,
		IFfProbeService ffProbeService,
		IServiceScopeFactory scopeFactory,
		IMemoryCache memoryCache)
	{
		_settingsProvider = settingsProvider;
		_ffProbeService = ffProbeService;
		_scopeFactory = scopeFactory;
		_memoryCache = memoryCache;
	}

	public async Task<Clip[]> GetClipsAsync(SyncMode syncMode = SyncMode.None)
	{
		using var scope = _scopeFactory.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TeslaCamDbContext>();
        var julesService = scope.ServiceProvider.GetRequiredService<IJulesApiService>();

        try
        {
            if (syncMode == SyncMode.None)
            {
                if (_memoryCache.TryGetValue(ClipsCacheKey, out Clip[] cachedClips))
                {
                    return cachedClips;
                }
            }

            if (syncMode == SyncMode.Incremental || syncMode == SyncMode.Full || !dbContext.VideoFiles.Any())
            {
                // SECURITY: Prevent DoS by serializing sync operations
                await _syncSemaphore.WaitAsync();
                try
                {
                    if (syncMode == SyncMode.Full)
                    {
                        // Clear all video files from DB
                        await dbContext.VideoFiles.ExecuteDeleteAsync();
                    }

                    // Double-check if we still need to sync (in case another thread just finished populating DB)
                    // This prevents redundant scans when multiple threads hit SyncMode.None simultaneously
                    if (syncMode != SyncMode.None || !await dbContext.VideoFiles.AnyAsync())
                    {
                        await SyncClipsAsync(dbContext);
                    }
                }
                finally
                {
                    _syncSemaphore.Release();
                }
            }

            var videoFiles = await dbContext.VideoFiles.ToListAsync();
            var clips = await BuildClipsAsync(videoFiles);

            _memoryCache.Set(ClipsCacheKey, clips, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(1)
            });

            return clips;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting clips.");
            await julesService.ReportErrorAsync(ex, $"Error getting clips with SyncMode {syncMode}");
            throw;
        }
	}

	private async Task SyncClipsAsync(TeslaCamDbContext dbContext)
	{
        _memoryCache.Remove(ClipsCacheKey);

		var knownVideoFiles = await dbContext.VideoFiles
			.ToDictionaryAsync(v => v.FilePath, v => v);

		// Optimization: Use EnumerateFiles to reduce memory allocation compared to GetFiles
		var filePaths = Directory
			.EnumerateFiles(_settingsProvider.Settings.ClipsRootPath, "*.mp4", SearchOption.AllDirectories)
			.ToHashSet();

		// Remove files that no longer exist
		var filesToRemove = knownVideoFiles.Keys.Where(k => !filePaths.Contains(k)).ToList();
		if (filesToRemove.Any())
		{
			foreach (var fileToRemove in filesToRemove)
			{
				var entity = knownVideoFiles[fileToRemove];
				dbContext.VideoFiles.Remove(entity);
				knownVideoFiles.Remove(fileToRemove);
			}
		}

		// Find new files
		var newFiles = filePaths
			.Where(path => !knownVideoFiles.ContainsKey(path))
			.Select(path => new { Path = path, RegexMatch = FileNameRegex.Match(path) })
			.Where(f => f.RegexMatch.Success)
			.ToList();

		if (newFiles.Any())
		{
			var newVideoFiles = (await Task.WhenAll(newFiles
				.AsParallel()
				.Select(async f =>
				{
					await FfProbeSemaphore.WaitAsync();
					try
					{
						return await TryParseVideoFileAsync(f.Path, f.RegexMatch);
					}
					finally
					{
						FfProbeSemaphore.Release();
					}
				})))
				.Where(v => v != null)
				.ToList();

			await dbContext.VideoFiles.AddRangeAsync(newVideoFiles);
		}

		await dbContext.SaveChangesAsync();
	}

	private async Task<Clip[]> BuildClipsAsync(List<VideoFile> videoFiles)
	{
		var recentFiles = new List<VideoFile>();
		var eventFiles = new List<VideoFile>();

		// Optimization: Iterate the list once to separate files, avoiding multiple LINQ passes (O(N) vs O(2N))
		foreach (var videoFile in videoFiles)
		{
			if (videoFile.ClipType == ClipType.Recent)
			{
				recentFiles.Add(videoFile);
			}
			else if (!string.IsNullOrWhiteSpace(videoFile.EventFolderName))
			{
				eventFiles.Add(videoFile);
			}
		}

		var recentClips = GetRecentClips(recentFiles);

		// Optimization: Process event clips in parallel using Task.WhenAll to support async I/O
		var eventTasks = eventFiles
			.GroupBy(v => v.EventFolderName)
			.Select(g => ParseClipAsync(g.Key, g.ToList()));

		var eventClips = await Task.WhenAll(eventTasks);

		var clips = eventClips
			.AsParallel()
			.Concat(recentClips.AsParallel())
			.OrderByDescending(c => c.StartDate)
			.ToArray();

		return clips;
	}

	private static IEnumerable<Clip> GetRecentClips(List<VideoFile> recentVideoFiles)
	{
		// Optimize: Group by StartDate first to avoid O(N^2) scan in the loop
		var groupedSegments = recentVideoFiles
			.GroupBy(f => f.StartDate)
			.OrderByDescending(g => g.Key)
			.ToList();

		var currentClipSegments = new List<ClipVideoSegment>();
		for (var i = 0; i < groupedSegments.Count; i++)
		{
			var segmentVideos = groupedSegments[i].ToList();
			// Use the first video in the group for reference properties (Start, Duration)
			var currentVideoFile = segmentVideos[0];

			var segment = new ClipVideoSegment
			{
				StartDate = currentVideoFile.StartDate,
				EndDate = currentVideoFile.StartDate.Add(currentVideoFile.Duration),
				CameraFront = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.Front),
				CameraLeftRepeater = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.LeftRepeater),
				CameraRightRepeater = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.RightRepeater),
				CameraBack = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.Back),
				CameraLeftBPillar = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.LeftBPillar),
				CameraRightBPillar = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.RightBPillar),
				CameraFisheye = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.Fisheye),
				CameraNarrow = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.Narrow),
				CameraCabin = segmentVideos.FirstOrDefault(v => v.Camera == Cameras.Cabin)
			};
			
			currentClipSegments.Add(segment);

			// No more groups
			if (i + 1 >= groupedSegments.Count)
			{
				yield return new Clip(ClipType.Recent, currentClipSegments.ToArray())
				{
					ThumbnailUrl = NoThumbnailImageUrl
				};
				currentClipSegments.Clear();
				yield break;
			}

			const int segmentVideoGapToleranceInSeconds = 5;
			var nextSegmentFirstVideo = groupedSegments[i + 1].First();
			// Next video is within X seconds of last video of current segment, continue building clip segments
			if (nextSegmentFirstVideo.StartDate <= segment.EndDate.AddSeconds(segmentVideoGapToleranceInSeconds))
				continue;
			
			// Next video is more than X seconds, assume it's a new recent video clip
			yield return new Clip(ClipType.Recent, currentClipSegments.ToArray())
			{
				ThumbnailUrl = NoThumbnailImageUrl
			};
			currentClipSegments.Clear();
		}
	}

	private async Task<VideoFile> TryParseVideoFileAsync(string path, Match regexMatch)
	{
		try
		{
			return await ParseVideoFileAsync(path, regexMatch);
		}
		catch (Exception e)
		{
			Log.Error(e, "Failed to parse info for video file from path: {Path}", path);
            using var scope = _scopeFactory.CreateScope();
            var julesService = scope.ServiceProvider.GetRequiredService<IJulesApiService>();
            await julesService.ReportErrorAsync(e, $"Failed to parse video file: {path}");
			return null;
		}
	}

	private async Task<VideoFile> ParseVideoFileAsync(string path, Match regexMatch)
	{
		var clipType = regexMatch.Groups["type"].Value switch
		{
			"RecentClips" => ClipType.Recent,
			"SavedClips" => ClipType.Saved,
			"SentryClips" => ClipType.Sentry,
			_ => ClipType.Unknown
		};

		var camera = regexMatch.Groups["camera"].Value switch
		{
			"back" => Cameras.Back,
			"front" => Cameras.Front,
			"left_repeater" => Cameras.LeftRepeater,
			"right_repeater" => Cameras.RightRepeater,
			"left_pillar" => Cameras.LeftBPillar,
			"right_pillar" => Cameras.RightBPillar,
			"fisheye" => Cameras.Fisheye,
			"narrow" => Cameras.Narrow,
			_ => Cameras.Unknown
		};

		var date = new DateTime(
			int.Parse(regexMatch.Groups["vyear"].Value),
			int.Parse(regexMatch.Groups["vmonth"].Value),
			int.Parse(regexMatch.Groups["vday"].Value),
			int.Parse(regexMatch.Groups["vhour"].Value),
			int.Parse(regexMatch.Groups["vminute"].Value),
			int.Parse(regexMatch.Groups["vsecond"].Value));

		var duration = await _ffProbeService.GetVideoFileDurationAsync(path);
		if (!duration.HasValue)
		{
			Log.Error("Failed to get duration for video file {Path}", path);
			return null;
		}

		var eventFolderName = clipType != ClipType.Recent
			? regexMatch.Groups["event"].Value
			: null;
		
		var relativePath = Path.GetRelativePath(_settingsProvider.Settings.ClipsRootPath, path);

		return new VideoFile
		{
			FilePath = path,
			Url = $"/Api/Video/{Uri.EscapeDataString(relativePath)}",
			EventFolderName = eventFolderName,
			ClipType = clipType,
			StartDate = date,
			Camera = camera,
			Duration = duration.Value
		};
	}

	private async Task<Clip> ParseClipAsync(string eventFolderName, List<VideoFile> eventVideoFiles)
	{
		var segments = eventVideoFiles
			.GroupBy(v => v.StartDate)
			// Optimize: Remove AsParallel() as this is already called within a parallel loop (BuildClips)
			// and the collection size (files per event) is small.
			.Select(g => new ClipVideoSegment
			{
				StartDate = g.Key,
				EndDate = g.Key.Add(g.First().Duration),
				CameraFront = g.FirstOrDefault(v => v.Camera == Cameras.Front),
				CameraLeftRepeater = g.FirstOrDefault(v => v.Camera == Cameras.LeftRepeater),
				CameraRightRepeater = g.FirstOrDefault(v => v.Camera == Cameras.RightRepeater),
				CameraBack = g.FirstOrDefault(v => v.Camera == Cameras.Back),
				CameraLeftBPillar = g.FirstOrDefault(v => v.Camera == Cameras.LeftBPillar),
				CameraRightBPillar = g.FirstOrDefault(v => v.Camera == Cameras.RightBPillar),
				CameraFisheye = g.FirstOrDefault(v => v.Camera == Cameras.Fisheye),
				CameraNarrow = g.FirstOrDefault(v => v.Camera == Cameras.Narrow),
				CameraCabin = g.FirstOrDefault(v => v.Camera == Cameras.Cabin)
			})
			.ToArray();

		var eventFolderPath = Path.GetDirectoryName(eventVideoFiles.First().FilePath)!;
		var expectedEventJsonPath = Path.Combine(eventFolderPath, "event.json");
		var eventInfo = await TryReadEventAsync(expectedEventJsonPath);

		var expectedEventThumbnailPath = Path.Combine(eventFolderPath, "thumb.png");
		// File.Exists is fast (metadata only), so synchronous call is acceptable here.
		var thumbnailUrl = NoThumbnailImageUrl;
		if (File.Exists(expectedEventThumbnailPath))
		{
			var relativeThumbPath = Path.GetRelativePath(_settingsProvider.Settings.ClipsRootPath, expectedEventThumbnailPath);
			thumbnailUrl = $"/Api/Thumbnail/{Uri.EscapeDataString(relativeThumbPath)}";
		}

		return new Clip(eventVideoFiles.First().ClipType, segments)
		{
			Event = eventInfo,
			ThumbnailUrl = thumbnailUrl
		};
	}

	private async Task<Event> TryReadEventAsync(string path)
	{
		return await _memoryCache.GetOrCreateAsync(path, async entry =>
		{
			// Cache for 1 hour as event files are static
			entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

			try
			{
				if (!File.Exists(path))
					return null;

				var json = await File.ReadAllTextAsync(path);
				return JsonConvert.DeserializeObject<Event>(json);
			}
			catch (Exception e)
			{
				Log.Error(e, "Failed to read {EventJsonPath}", path);
				return null;
			}
		});
	}

	/*
	 * \SavedClips\2023-06-16_17-18-06\2023-06-16_17-12-49-front.mp4"
	 * type = SavedClips
	 * event = 2023-06-16_17-18-06
	 * year = 2023
	 * month = 06
	 * day = 17
	 * hour = 18
	 * minute = 06
	 * vyear = 2023
	 * vmonth = 06
	 * vhour = 17
	 * vminute = 12
	 * vsecond = 49
	 * camera = front
	 */
	[GeneratedRegex(@"(?:[\\/]|^)(?<type>(?:Recent|Saved|Sentry)Clips)(?:[\\/](?<event>(?<year>20\d{2})\-(?<month>[0-1][0-9])\-(?<day>[0-3][0-9])_(?<hour>[0-2][0-9])\-(?<minute>[0-5][0-9])\-(?<second>[0-5][0-9])))?[\\/](?<vyear>20\d{2})\-(?<vmonth>[0-1][0-9])\-(?<vday>[0-3][0-9])_(?<vhour>[0-2][0-9])\-(?<vminute>[0-5][0-9])\-(?<vsecond>[0-5][0-9])\-(?<camera>back|front|left_repeater|right_repeater|left_pillar|right_pillar|fisheye|narrow)\.mp4")]
	private static partial Regex FileNameRegexGenerated();
}
