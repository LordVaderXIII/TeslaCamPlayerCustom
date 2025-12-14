using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Client.Components;

public partial class ClipViewer : ComponentBase, IDisposable
{
	private static readonly TimeSpan TimelineScrubTimeout = TimeSpan.FromSeconds(2);
	
	[Inject]
	public IJSRuntime JsRuntime { get; set; }
	
	[Parameter]
	public EventCallback PreviousButtonClicked { get; set; }
	
	[Parameter]
	public EventCallback NextButtonClicked { get; set; }

	[Parameter]
	public bool IsExportMode { get; set; }

	[Parameter]
	public EventCallback<ExportRequest> OnExportRequested { get; set; }

	private double TimelineValue
	{
		get => _timelineValue;
		set
		{
			_timelineValue = value;
			if (_isScrubbing)
				_setVideoTimeDebounceTimer.Enabled = true;
		}
	}

	private Clip _clip;
	private VideoPlayer _videoPlayerFront;
	private VideoPlayer _videoPlayerLeftRepeater;
	private VideoPlayer _videoPlayerRightRepeater;
	private VideoPlayer _videoPlayerBack;
	private VideoPlayer _videoPlayerLeftBPillar;
	private VideoPlayer _videoPlayerRightBPillar;
	private VideoPlayer _videoPlayerFisheye;
	private VideoPlayer _videoPlayerNarrow;
	private VideoPlayer _videoPlayerCabin;
    private readonly HashSet<Cameras> _loadedCameras = new();
	private bool _isPlaying;
	private ClipVideoSegment _currentSegment;
	private MudSlider<double> _timelineSlider;
	private double _timelineMaxSeconds;
	private double _ignoreTimelineValue;
	private bool _wasPlayingBeforeScrub;
	private bool _isScrubbing;
	private double _timelineValue;
	private System.Timers.Timer _setVideoTimeDebounceTimer;
    private System.Timers.Timer _syncTimer;
	private CancellationTokenSource _loadSegmentCts = new();
	private Cameras _mainCamera = Cameras.Front;
    private bool _showCameraOverlay; // Mobile camera switch overlay
    private bool _is360Mode = false;
	private double _playbackRate = 1.0;
	private double PlaybackRate
	{
		get => _playbackRate;
		set
		{
			_playbackRate = value;
			InvokeAsync(StateHasChanged);
		}
	}

	// Export logic
	private double _exportStart;
	private double _exportEnd;
	private bool _isDraggingExportHandle;
	private bool _draggingStartHandle;
	private Dictionary<Cameras, SelectionState> _cameraSelection = new();

	private class SelectionState
	{
		public bool IsSelected { get; set; }
	}

	protected override void OnInitialized()
	{
		_setVideoTimeDebounceTimer = new(500);
		_setVideoTimeDebounceTimer.Elapsed += ScrubVideoDebounceTick;

        _syncTimer = new(1000);
        _syncTimer.Elapsed += SyncVideosTick;
        _syncTimer.Enabled = true;

		foreach (Cameras cam in Enum.GetValues(typeof(Cameras)))
		{
			_cameraSelection[cam] = new SelectionState { IsSelected = true };
		}
	}

	protected override void OnParametersSet()
	{
		if (IsExportMode && _exportEnd == 0 && _timelineMaxSeconds > 0)
		{
			_exportStart = 0;
			_exportEnd = _timelineMaxSeconds;
		}
	}

	protected override void OnAfterRender(bool firstRender)
	{
		if (!firstRender)
			return;

		if (_videoPlayerFront != null) _videoPlayerFront.Loaded += () => { Console.WriteLine("Loaded: Front"); _loadedCameras.Add(Cameras.Front); };
		if (_videoPlayerLeftRepeater != null) _videoPlayerLeftRepeater.Loaded += () => { Console.WriteLine("Loaded: Left"); _loadedCameras.Add(Cameras.LeftRepeater); };
		if (_videoPlayerRightRepeater != null) _videoPlayerRightRepeater.Loaded += () => { Console.WriteLine("Loaded: Right"); _loadedCameras.Add(Cameras.RightRepeater); };
		if (_videoPlayerBack != null) _videoPlayerBack.Loaded += () => { Console.WriteLine("Loaded: Back"); _loadedCameras.Add(Cameras.Back); };
		if (_videoPlayerLeftBPillar != null) _videoPlayerLeftBPillar.Loaded += () => { Console.WriteLine("Loaded: LeftBPillar"); _loadedCameras.Add(Cameras.LeftBPillar); };
		if (_videoPlayerRightBPillar != null) _videoPlayerRightBPillar.Loaded += () => { Console.WriteLine("Loaded: RightBPillar"); _loadedCameras.Add(Cameras.RightBPillar); };
		if (_videoPlayerFisheye != null) _videoPlayerFisheye.Loaded += () => { Console.WriteLine("Loaded: Fisheye"); _loadedCameras.Add(Cameras.Fisheye); };
		if (_videoPlayerNarrow != null) _videoPlayerNarrow.Loaded += () => { Console.WriteLine("Loaded: Narrow"); _loadedCameras.Add(Cameras.Narrow); };
		if (_videoPlayerCabin != null) _videoPlayerCabin.Loaded += () => { Console.WriteLine("Loaded: Cabin"); _loadedCameras.Add(Cameras.Cabin); };
	}

	private static Task AwaitUiUpdate()
		=> Task.Delay(100);

	public async Task SetClipAsync(Clip clip)
	{
		_clip = clip;
		TimelineValue = 0;
		_timelineMaxSeconds = (clip.EndDate - clip.StartDate).TotalSeconds;

		_exportStart = 0;
		_exportEnd = _timelineMaxSeconds;

		_currentSegment = _clip.Segments.First();
		await SetCurrentSegmentVideosAsync();
	}

	private async Task<bool> SetCurrentSegmentVideosAsync()
	{
		if (_currentSegment == null)
			return false;

		await _loadSegmentCts.CancelAsync();
		_loadSegmentCts = new();
		
		_loadedCameras.Clear();
		var cameraCount = _currentSegment.CameraAnglesCount();

		var wasPlaying = _isPlaying;
		if (wasPlaying)
			await TogglePlayingAsync(false);
		
		if (_loadSegmentCts.IsCancellationRequested)
			return false;
		
		await InvokeAsync(StateHasChanged);

		var timeout = Task.Delay(10000);
		var completedTask = await Task.WhenAny(Task.Run(async () =>
		{
            // Optimize: Wait only for Main Camera to load
			while (!_loadedCameras.Contains(_mainCamera) && !_loadSegmentCts.IsCancellationRequested)
				await Task.Delay(10, _loadSegmentCts.Token);
			
			Console.WriteLine("Main camera loaded, playing...");
		}, _loadSegmentCts.Token), timeout);

		if (completedTask == timeout)
		{
			Console.WriteLine("Loading timed out");
            // Proceed anyway, maybe others loaded?
		}

		if (wasPlaying)
			await TogglePlayingAsync(true);

		await ExecuteOnPlayers(async p => await p.SetPlaybackRateAsync(PlaybackRate));

		if (_is360Mode)
		{
			await Update360ModeAsync();
		}

		return !_loadSegmentCts.IsCancellationRequested;
	}

	private async Task CyclePlaybackRateAsync()
	{
		var newRate = PlaybackRate switch
		{
			0.5 => 1.0,
			1.0 => 1.5,
			1.5 => 2.0,
			2.0 => 0.5,
			_ => 1.0
		};
		await SetPlaybackRateAsync(newRate);
	}

	private async Task SetPlaybackRateAsync(double rate)
	{
		PlaybackRate = rate;
		await ExecuteOnPlayers(async p => await p.SetPlaybackRateAsync(rate));
	}

	private async Task ExecuteOnPlayers(Func<VideoPlayer, Task> action)
	{
		try
		{
            if (_videoPlayerFront != null) await action(_videoPlayerFront);
			if (_videoPlayerLeftRepeater != null) await action(_videoPlayerLeftRepeater);
			if (_videoPlayerRightRepeater != null) await action(_videoPlayerRightRepeater);
			if (_videoPlayerBack != null) await action(_videoPlayerBack);
			if (_videoPlayerLeftBPillar != null) await action(_videoPlayerLeftBPillar);
			if (_videoPlayerRightBPillar != null) await action(_videoPlayerRightBPillar);
			if (_videoPlayerFisheye != null) await action(_videoPlayerFisheye);
			if (_videoPlayerNarrow != null) await action(_videoPlayerNarrow);
			if (_videoPlayerCabin != null) await action(_videoPlayerCabin);
		}
		catch
		{
			// ignore
		}
	}

	private Task TogglePlayingAsync(bool? play = null)
	{
		play ??= !_isPlaying;
		_isPlaying = play.Value;
		return ExecuteOnPlayers(async p => await (play.Value ? p.PlayAsync() : p.PauseAsync()));
	}

	private Task PlayPauseClicked()
		=> TogglePlayingAsync();

	private async Task VideoEnded()
	{
		if (_currentSegment == _clip.Segments.Last())
			return;

		await TogglePlayingAsync(false);

		var nextSegment = _clip.Segments
			.OrderBy(s => s.StartDate)
			.SkipWhile(s => s != _currentSegment)
			.Skip(1)
			.FirstOrDefault()
			?? _clip.Segments.FirstOrDefault();

		if (nextSegment == null)
		{
			await TogglePlayingAsync(false);
			return;
		}

		_currentSegment = nextSegment;
		await SetCurrentSegmentVideosAsync();
		await AwaitUiUpdate();
		await TogglePlayingAsync(true);
	}

	private async Task FrontVideoTimeUpdate()
	{
		if (_currentSegment == null)
			return;

		if (_isScrubbing || _isDraggingExportHandle)
			return;
		
        // Ensure player is valid
        if (_videoPlayerFront == null) return;

		var seconds = await _videoPlayerFront.GetTimeAsync();
		var currentTime = _currentSegment.StartDate.AddSeconds(seconds);
		var secondsSinceClipStart = (currentTime - _clip.StartDate).TotalSeconds;
		
		_ignoreTimelineValue = secondsSinceClipStart;
		TimelineValue = secondsSinceClipStart;
	}

	private async Task TimelineSliderPointerDown()
	{
		if (IsExportMode) return;

		_isScrubbing = true;
		_wasPlayingBeforeScrub = _isPlaying;
		await TogglePlayingAsync(false);
		
		await AwaitUiUpdate();
		await ScrubToSliderTime();
	}

	private async Task TimelineSliderPointerUp()
	{
		Console.WriteLine("Pointer up");
		await ScrubToSliderTime();
		_isScrubbing = false;
			
		if (!_isPlaying && _wasPlayingBeforeScrub)
			await TogglePlayingAsync(true);
	}

    private async void SyncVideosTick(object _, ElapsedEventArgs __)
    {
        if (!_isPlaying || _isScrubbing || _currentSegment == null)
            return;

        await InvokeAsync(async () =>
        {
            try
            {
                // Get Main Camera Time
                var mainPlayer = GetPlayerForCamera(_mainCamera);
                if (mainPlayer == null) return;

                var mainTime = await mainPlayer.GetTimeAsync();

                // Check and Sync others in parallel
                var tasks = new List<Task>
                {
                    CheckAndSyncPlayer(_videoPlayerFront, Cameras.Front, mainTime),
                    CheckAndSyncPlayer(_videoPlayerLeftRepeater, Cameras.LeftRepeater, mainTime),
                    CheckAndSyncPlayer(_videoPlayerRightRepeater, Cameras.RightRepeater, mainTime),
                    CheckAndSyncPlayer(_videoPlayerBack, Cameras.Back, mainTime),
                    CheckAndSyncPlayer(_videoPlayerLeftBPillar, Cameras.LeftBPillar, mainTime),
                    CheckAndSyncPlayer(_videoPlayerRightBPillar, Cameras.RightBPillar, mainTime),
                    CheckAndSyncPlayer(_videoPlayerFisheye, Cameras.Fisheye, mainTime),
                    CheckAndSyncPlayer(_videoPlayerNarrow, Cameras.Narrow, mainTime),
                    CheckAndSyncPlayer(_videoPlayerCabin, Cameras.Cabin, mainTime)
                };

                await Task.WhenAll(tasks);
            }
            catch
            {
                // Ignore sync errors
            }
        });
    }

    private VideoPlayer GetPlayerForCamera(Cameras camera)
    {
        return camera switch
        {
            Cameras.Front => _videoPlayerFront,
            Cameras.LeftRepeater => _videoPlayerLeftRepeater,
            Cameras.RightRepeater => _videoPlayerRightRepeater,
            Cameras.Back => _videoPlayerBack,
            Cameras.LeftBPillar => _videoPlayerLeftBPillar,
            Cameras.RightBPillar => _videoPlayerRightBPillar,
            Cameras.Fisheye => _videoPlayerFisheye,
            Cameras.Narrow => _videoPlayerNarrow,
            Cameras.Cabin => _videoPlayerCabin,
            _ => null
        };
    }

    private async Task CheckAndSyncPlayer(VideoPlayer player, Cameras camera, double mainTime)
    {
        if (player == null || camera == _mainCamera) return;

        try
        {
            var time = await player.GetTimeAsync();
            if (Math.Abs(time - mainTime) > 0.4) // 400ms drift tolerance
            {
                Console.WriteLine($"Syncing {camera} (Diff: {time - mainTime:F3}s)");
                await player.SetTimeAsync(mainTime);
            }
        }
        catch { /* Player might not be ready */ }
    }

	private async void ScrubVideoDebounceTick(object _, ElapsedEventArgs __)
		=> await ScrubToSliderTime();

	private async Task ScrubToSliderTime()
	{
		_setVideoTimeDebounceTimer.Enabled = false;

		if (!_isScrubbing)
			return;

		await SeekToTimestampAsync();
	}

	private async Task SeekToTimestampAsync()
	{
		try
		{
			var scrubToDate = _clip.StartDate.AddSeconds(TimelineValue);
			var segment = _clip.SegmentAtDate(scrubToDate)
			              ?? _clip.Segments.Where(s => s.StartDate > scrubToDate).MinBy(s => s.StartDate);

			if (segment == null)
				return;

			if (segment != _currentSegment)
			{
				_currentSegment = segment;
				if (!await SetCurrentSegmentVideosAsync())
					return;
			}

			var secondsIntoSegment = (scrubToDate - segment.StartDate).TotalSeconds;
			await ExecuteOnPlayers(async p => await p.SetTimeAsync(secondsIntoSegment));
		}
		catch
		{
			// ignore
		}
	}

	private async Task SkipBackwardClicked()
	{
		TimelineValue = Math.Max(0, TimelineValue - 5);
		await SeekToTimestampAsync();
	}

	private async Task SkipForwardClicked()
	{
		TimelineValue = Math.Min(_timelineMaxSeconds, TimelineValue + 5);
		await SeekToTimestampAsync();
	}

	private async Task SwapCamera(Cameras camera)
	{
        if (_mainCamera == camera)
        {
            // If already selected, maybe just close overlay?
            _showCameraOverlay = false;
            return;
        }

		var wasPlaying = _isPlaying;
		if (wasPlaying)
			await TogglePlayingAsync(false);

		_mainCamera = camera;
        _showCameraOverlay = false; // Close overlay on swap
		await InvokeAsync(StateHasChanged);

		if (_currentSegment != null)
		{
			var scrubToDate = _clip.StartDate.AddSeconds(TimelineValue);
			var secondsIntoSegment = (scrubToDate - _currentSegment.StartDate).TotalSeconds;

			await ExecuteOnPlayers(async p =>
			{
				await p.SetTimeAsync(secondsIntoSegment);
				await p.SetPlaybackRateAsync(PlaybackRate);
			});
		}

		if (wasPlaying)
			await TogglePlayingAsync(true);
	}

    private void ToggleCameraOverlay()
    {
        _showCameraOverlay = !_showCameraOverlay;
    }

	private double DateTimeToTimelinePercentage(DateTime dateTime)
	{
		var percentage = Math.Round(dateTime.Subtract(_clip.StartDate).TotalSeconds / _clip.TotalSeconds * 100, 2);
		return Math.Clamp(percentage, 0, 100);
	}

	private string SegmentStartMargerStyle(ClipVideoSegment segment)
	{
		var percentage = DateTimeToTimelinePercentage(segment.StartDate);
		return $"left: {percentage}%";
	}

	private string EventMarkerStyle()
	{
		if (_clip?.Event?.Timestamp == null)
			return "display: none";

		var percentage = DateTimeToTimelinePercentage(_clip.Event.Timestamp);
		return $"left: {percentage}%";
	}

	// Export Methods

	private SelectionState GetCameraSelectionState(Cameras camera)
	{
		if (!_cameraSelection.ContainsKey(camera))
		{
			_cameraSelection[camera] = new SelectionState { IsSelected = true };
		}
		return _cameraSelection[camera];
	}

	private string ExportHandleStyle(bool start)
	{
		var val = start ? _exportStart : _exportEnd;
		var pct = (val / _timelineMaxSeconds) * 100;
		return $"left: {pct}%";
	}

	private string ExportHighlightStyle()
	{
		var startPct = (_exportStart / _timelineMaxSeconds) * 100;
		var endPct = (_exportEnd / _timelineMaxSeconds) * 100;
		var width = endPct - startPct;
		return $"left: {startPct}%; width: {width}%";
	}

	private void StartDrag(PointerEventArgs e, bool isStart)
	{
		if (!IsExportMode) return;
		_isDraggingExportHandle = true;
		_draggingStartHandle = isStart;
	}

	private void EndDrag(PointerEventArgs e)
	{
		_isDraggingExportHandle = false;
	}

	private void OnTimelinePointerMove(PointerEventArgs e)
	{
		if (_isDraggingExportHandle)
		{
			_ = UpdateHandlePosition(e);
		}
	}

	private async Task UpdateHandlePosition(PointerEventArgs e)
	{
		var result = await JsRuntime.InvokeAsync<double>("getSliderPercentage", e.ClientX, _timelineSliderElement);

		var seconds = result * _timelineMaxSeconds;
		seconds = Math.Max(0, Math.Min(_timelineMaxSeconds, seconds));

		if (_draggingStartHandle)
		{
			_exportStart = Math.Min(seconds, _exportEnd - 1); // Ensure at least 1 sec diff
		}
		else
		{
			_exportEnd = Math.Max(seconds, _exportStart + 1);
		}

		StateHasChanged();
	}

	private ElementReference _timelineSliderElement;

	public async Task TriggerExportAsync()
	{
		var selectedCameras = _cameraSelection.Where(kv => kv.Value.IsSelected).Select(kv => kv.Key).ToList();
		var request = new ExportRequest
		{
			ClipStartDate = _clip.StartDate,
			EventFolderName = _clip.Event?.Timestamp != null ?
				_currentSegment?.CameraFront?.EventFolderName
				: null,
			StartTime = _clip.StartDate.AddSeconds(_exportStart),
			EndTime = _clip.StartDate.AddSeconds(_exportEnd),
			MainCamera = _mainCamera,
			SelectedCameras = selectedCameras
		};

		await OnExportRequested.InvokeAsync(request);
	}

    private async Task Toggle360Mode()
    {
        _is360Mode = !_is360Mode;

        if (_is360Mode)
        {
            await Update360ModeAsync();
        }
        else
        {
            // Dispose
            try
            {
                await JsRuntime.InvokeVoidAsync("teslaPano.dispose");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to dispose 3D mode: {ex}");
            }
        }
    }

    private async Task Update360ModeAsync()
    {
        // Initialize Three.js Pano
        // Collect video elements
        var videoElements = new Dictionary<string, ElementReference>();
        if (_videoPlayerFront != null) videoElements["Front"] = _videoPlayerFront.VideoElement;
        if (_videoPlayerLeftBPillar != null) videoElements["LeftBPillar"] = _videoPlayerLeftBPillar.VideoElement;
        if (_videoPlayerLeftRepeater != null) videoElements["LeftRepeater"] = _videoPlayerLeftRepeater.VideoElement;
        if (_videoPlayerBack != null) videoElements["Back"] = _videoPlayerBack.VideoElement;
        if (_videoPlayerRightRepeater != null) videoElements["RightRepeater"] = _videoPlayerRightRepeater.VideoElement;
        if (_videoPlayerRightBPillar != null) videoElements["RightBPillar"] = _videoPlayerRightBPillar.VideoElement;

        await InvokeAsync(StateHasChanged); // Ensure DOM is updated with IDs/classes
        await Task.Delay(50); // Small delay to allow DOM render

        try
        {
            await JsRuntime.InvokeVoidAsync("teslaPano.init", "pano-container", videoElements);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to init 3D mode: {ex}");
            _is360Mode = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        // Cleanup if component is destroyed while in 360 mode
         _ = JsRuntime.InvokeVoidAsync("teslaPano.dispose").AsTask().ContinueWith(t => { /* ignore */ });
         _setVideoTimeDebounceTimer?.Dispose();
         _syncTimer?.Dispose();
         _loadSegmentCts?.Dispose();
    }
}
