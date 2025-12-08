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

public partial class ClipViewer : ComponentBase
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
	private int _videoLoadedEventCount = 0;
	private bool _isPlaying;
	private ClipVideoSegment _currentSegment;
	private MudSlider<double> _timelineSlider;
	private double _timelineMaxSeconds;
	private double _ignoreTimelineValue;
	private bool _wasPlayingBeforeScrub;
	private bool _isScrubbing;
	private double _timelineValue;
	private System.Timers.Timer _setVideoTimeDebounceTimer;
	private CancellationTokenSource _loadSegmentCts = new();
	private Cameras _mainCamera = Cameras.Front;

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

		_videoPlayerFront.Loaded += () =>
		{
			Console.WriteLine("Loaded: Front");
			_videoLoadedEventCount++;
		};
		_videoPlayerLeftRepeater.Loaded += () =>
		{
			Console.WriteLine("Loaded: Left");
			_videoLoadedEventCount++;
		};
		_videoPlayerRightRepeater.Loaded += () =>
		{
			Console.WriteLine("Loaded: Right");
			_videoLoadedEventCount++;
		};
		_videoPlayerBack.Loaded += () =>
		{
			Console.WriteLine("Loaded: Back");
			_videoLoadedEventCount++;
		};
		_videoPlayerLeftBPillar.Loaded += () =>
		{
			Console.WriteLine("Loaded: LeftBPillar");
			_videoLoadedEventCount++;
		};
		_videoPlayerRightBPillar.Loaded += () =>
		{
			Console.WriteLine("Loaded: RightBPillar");
			_videoLoadedEventCount++;
		};
		_videoPlayerFisheye.Loaded += () =>
		{
			Console.WriteLine("Loaded: Fisheye");
			_videoLoadedEventCount++;
		};
		_videoPlayerNarrow.Loaded += () =>
		{
			Console.WriteLine("Loaded: Narrow");
			_videoLoadedEventCount++;
		};
		_videoPlayerCabin.Loaded += () =>
		{
			Console.WriteLine("Loaded: Cabin");
			_videoLoadedEventCount++;
		};
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
		
		_videoLoadedEventCount = 0;
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
			while (_videoLoadedEventCount < cameraCount && !_loadSegmentCts.IsCancellationRequested)
				await Task.Delay(10, _loadSegmentCts.Token);
			
			Console.WriteLine("Loading done");
		}, _loadSegmentCts.Token), timeout);

		if (completedTask == timeout)
		{
			Console.WriteLine("Loading timed out");
			return false;
		}

		if (wasPlaying)
			await TogglePlayingAsync(true);

		return !_loadSegmentCts.IsCancellationRequested;
	}

	private async Task ExecuteOnPlayers(Func<VideoPlayer, Task> action)
	{
		try
		{
			await action(_videoPlayerFront);
			await action(_videoPlayerLeftRepeater);
			await action(_videoPlayerRightRepeater);
			await action(_videoPlayerBack);
			await action(_videoPlayerLeftBPillar);
			await action(_videoPlayerRightBPillar);
			await action(_videoPlayerFisheye);
			await action(_videoPlayerNarrow);
			await action(_videoPlayerCabin);
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
		
		var seconds = await _videoPlayerFront.GetTimeAsync();
		var currentTime = _currentSegment.StartDate.AddSeconds(seconds);
		var secondsSinceClipStart = (currentTime - _clip.StartDate).TotalSeconds;
		
		_ignoreTimelineValue = secondsSinceClipStart;
		TimelineValue = secondsSinceClipStart;
	}

	private async Task TimelineSliderPointerDown()
	{
		if (IsExportMode) return; // Disable regular scrubbing in export mode? Or allow it? User said "Existing timeline slider only when export is clicked... drag brackets". Regular scrubbing is useful to verify brackets. Let's keep it.

		_isScrubbing = true;
		_wasPlayingBeforeScrub = _isPlaying;
		await TogglePlayingAsync(false);
		
		// Allow value change event to trigger, then scrub before user releases mouse click
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

	private async void ScrubVideoDebounceTick(object _, ElapsedEventArgs __)
		=> await ScrubToSliderTime();

	private async Task ScrubToSliderTime()
	{
		_setVideoTimeDebounceTimer.Enabled = false;
		
		if (!_isScrubbing)
			return;

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
			// ignore, happens sometimes
		}
	}

	private async Task SwapCamera(Cameras camera)
	{
		if (_mainCamera == camera)
			return;

		var wasPlaying = _isPlaying;
		if (wasPlaying)
			await TogglePlayingAsync(false);

		_mainCamera = camera;
		await InvokeAsync(StateHasChanged);

		// After DOM update (and player recreation/move), we need to restore time.
		if (_currentSegment != null)
		{
			// We can use the current TimelineValue to restore the position
			var scrubToDate = _clip.StartDate.AddSeconds(TimelineValue);
			var secondsIntoSegment = (scrubToDate - _currentSegment.StartDate).TotalSeconds;

			// We need to wait a bit for the new player to be ready?
			// Or just set the time. Since src is the same, maybe it loads fast.
			// But we should try to set the time on all players.
			await ExecuteOnPlayers(async p => await p.SetTimeAsync(secondsIntoSegment));
		}

		if (wasPlaying)
			await TogglePlayingAsync(true);
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

	// We need to track mouse movement on the slider container to update drag handle
	// But the event is on the handle itself which moves.
	// Actually better to have onmousemove on the slider container.
	// But `MudSlider` consumes events?
	// The container `.seeker-slider-container` or `.slider-container` should handle it.
	// But we can't easily modify the razor structure around MudSlider to add MouseMove without replacing it or wrapping it.
	// ClipViewer.razor has `.slider-container`. We can add @onpointermove there.

	// In `ClipViewer.razor` I added handlers for handles, but movement needs to be tracked on container.
	// I'll update `ClipViewer.razor` to add `onpointermove` to `slider-container`.

	private void OnTimelinePointerMove(PointerEventArgs e)
	{
		if (_isDraggingExportHandle)
		{
			// We need to calculate position relative to the container width.
			// This requires JS interop to get bounding rect of the container.
			// For now, let's assume we can use e.OffsetX if it's relative to target.
			// But target might be the slider or handle.
			// A reliable way requires JS.

			// Let's defer to a JS function to get percentage.
			// Or simpler: Use `TimelineValue` as a proxy if we were dragging the slider, but we are dragging handles.

			// Let's implement a simple JS helper to get relative click position.
			_ = UpdateHandlePosition(e);
		}
	}

	private async Task UpdateHandlePosition(PointerEventArgs e)
	{
		// We need to know the width of the slider container and the click position relative to it.
		// Since we can't easily get that from Blazor event args without knowing the element reference bounding box...
		// Let's use JS.

		// For now, I'll rely on the user dragging the handle, which might be tricky without full mouse capture.
		// Let's update `ClipViewer.razor` to include `onpointermove` on the `slider-container`.

		var result = await JsRuntime.InvokeAsync<double>("getSliderPercentage", e.ClientX, _timelineSliderElement);

		// result is 0..1
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

	private ElementReference _timelineSliderElement; // Ref to slider-container

	public async Task TriggerExportAsync()
	{
		var selectedCameras = _cameraSelection.Where(kv => kv.Value.IsSelected).Select(kv => kv.Key).ToList();
		var request = new ExportRequest
		{
			ClipStartDate = _clip.StartDate,
			EventFolderName = _clip.Event?.Timestamp != null ?
				// Need to find event folder name. The current segment front camera has it.
				_currentSegment?.CameraFront?.EventFolderName
				: null,
			StartTime = _clip.StartDate.AddSeconds(_exportStart),
			EndTime = _clip.StartDate.AddSeconds(_exportEnd),
			MainCamera = _mainCamera,
			SelectedCameras = selectedCameras
		};

		await OnExportRequested.InvokeAsync(request);
	}
}
