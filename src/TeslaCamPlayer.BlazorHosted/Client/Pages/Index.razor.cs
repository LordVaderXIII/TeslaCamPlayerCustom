using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using System.Timers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using TeslaCamPlayer.BlazorHosted.Client.Components;
using TeslaCamPlayer.BlazorHosted.Client.Helpers;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using TeslaCamPlayer.BlazorHosted.Client.Models;
using TeslaCamPlayer.BlazorHosted.Client.Services.Interfaces;

namespace TeslaCamPlayer.BlazorHosted.Client.Pages;

public partial class Index : ComponentBase
{
	private const int EventItemHeight = 60;

	[Inject]
	private HttpClient HttpClient { get; set; }

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; }

	[Inject]
	private IExportClientService ExportClientService { get; set; }

	[Inject]
	private IDialogService DialogService { get; set; }

	[Inject]
	private ISnackbar Snackbar { get; set; }

	[Inject]
	private IJSRuntime JsRuntime { get; set; }

	private Clip[] _clips;
	private Clip[] _filteredclips;
	private HashSet<DateTime> _eventDates;
	private MudDatePicker _datePicker;
	private bool _setDatePickerInitialDate;
	private ElementReference _eventsList;
	private System.Timers.Timer _scrollDebounceTimer;
	private DateTime _ignoreDatePicked;
	private Clip _activeClip;
	private ClipViewer _clipViewer;
	private bool _showFilter;
	private bool _filterChanged;
	private EventFilterValues _eventFilter = new();
	private bool _isExportMode;
	private bool _hasProcessingJobs;
	private System.Timers.Timer _jobsCheckTimer;
    private bool _isBrowserVisible;
	private string _userInitials = "AD";
    private bool _isMapVisible;
    private TelemetryData _currentTelemetry;

	protected override async Task OnInitializedAsync()
	{
		var state = await AuthStateProvider.GetAuthenticationStateAsync();
		var firstName = state.User.FindFirst("FirstName")?.Value ?? "Admin";
		_userInitials = firstName.Substring(0, Math.Min(2, firstName.Length)).ToUpper();

		_scrollDebounceTimer = new(100);
		_scrollDebounceTimer.Elapsed += ScrollDebounceTimerTick;

		_jobsCheckTimer = new(5000);
		_jobsCheckTimer.Elapsed += async (_, __) => await CheckProcessingJobs();
		_jobsCheckTimer.Enabled = true;

		await RefreshEventsAsync(SyncMode.None);
		await CheckProcessingJobs();
	}

	private async Task CheckProcessingJobs()
	{
		try
		{
			var jobs = await ExportClientService.GetJobsAsync();
			var processing = jobs.Any(j => j.Status == ExportStatus.Processing || j.Status == ExportStatus.Queued);
			if (processing != _hasProcessingJobs)
			{
				_hasProcessingJobs = processing;
				await InvokeAsync(StateHasChanged);
			}
		}
		catch
		{
			// Ignore errors during background check
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!_setDatePickerInitialDate && _filteredclips?.Any() == true && _datePicker != null)
		{
			_setDatePickerInitialDate = true;
			var latestClip = _filteredclips.MaxBy(c => c.EndDate)!;
			await _datePicker.GoToDate(latestClip.EndDate);
			await SetActiveClip(latestClip);
		}
	}

	private async Task RefreshEventsAsync(SyncMode syncMode)
	{
		_filteredclips = null;
		_clips = null;
		await Task.Delay(10);
		await InvokeAsync(StateHasChanged);

		_setDatePickerInitialDate = false;
        try
        {
		    _clips = await HttpClient.GetFromNewtonsoftJsonAsync<Clip[]>($"Api/GetClips?syncMode={syncMode}&_={DateTime.Now.Ticks}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching clips: {ex}");
            Snackbar.Add("Failed to load clips. Please try again.", Severity.Error);
            _clips = Array.Empty<Clip>();
        }

		FilterClips();
	}

	private void FilterClips()
	{
		var clips = _clips ??= Array.Empty<Clip>();
		// Optimization: Combine filtering and date collection into a single pass to avoid multiple iterations and LINQ allocations.
		var filteredList = new List<Clip>(clips.Length);
		var eventDates = new HashSet<DateTime>(clips.Length * 2);

		foreach (var clip in clips)
		{
			if (_eventFilter.IsInFilter(clip))
			{
				filteredList.Add(clip);
				eventDates.Add(clip.StartDate.Date);
				eventDates.Add(clip.EndDate.Date);
			}
		}

		_filteredclips = filteredList.ToArray();
		_eventDates = eventDates;
	}

	private async Task ToggleFilter()
	{
		_showFilter = !_showFilter;
		if (_showFilter || !_filterChanged)
			return;

		FilterClips();
		await InvokeAsync(StateHasChanged);
		await Task.CompletedTask;
	}

	private void EventFilterValuesChanged(EventFilterValues values)
	{
		_eventFilter = values;
		_filterChanged = true;
	}

	private bool IsDateDisabledFunc(DateTime date)
		=> !_eventDates.Contains(date);

	// Cache common single-icon arrays to avoid allocation in hot path
	private static readonly string[] IconsRecent = { Icons.Material.Filled.History };
	private static readonly string[] IconsSaved = { Icons.Material.Filled.CameraAlt };
	private static readonly string[] IconsSentry = { Icons.Material.Filled.RadioButtonChecked };
	private static readonly string[] IconsUnknown = { Icons.Material.Filled.QuestionMark };

	private static string[] GetClipIcons(Clip clip)
	{
		// sentry_aware_object_detection
		// user_interaction_honk
		// user_interaction_dashcam_panel_save
		// user_interaction_dashcam_icon_tapped
		// sentry_aware_accel_0.532005

		if (clip.Type == ClipType.Recent) return IconsRecent;
		if (clip.Type == ClipType.Unknown) return IconsUnknown;

		string baseIconStr;
		string[] baseIconArray;

		switch (clip.Type)
		{
			case ClipType.Saved:
				baseIconStr = Icons.Material.Filled.CameraAlt;
				baseIconArray = IconsSaved;
				break;
			case ClipType.Sentry:
				baseIconStr = Icons.Material.Filled.RadioButtonChecked;
				baseIconArray = IconsSentry;
				break;
			default:
				baseIconStr = Icons.Material.Filled.QuestionMark;
				baseIconArray = IconsUnknown;
				break;
		}

		if (clip.Event == null)
			return baseIconArray;

		var reason = clip.Event.Reason;
		string secondIcon = null;

		if (reason == CamEvents.SentryAwareObjectDetection)
			secondIcon = Icons.Material.Filled.Animation;
		else if (reason == CamEvents.UserInteractionHonk)
			secondIcon = Icons.Material.Filled.Campaign;
		else if (reason == CamEvents.UserInteractionDashcamPanelSave || reason == CamEvents.UserInteractionDashcamIconTapped)
			secondIcon = Icons.Material.Filled.Archive;
		else if (reason != null && reason.StartsWith(CamEvents.SentryAwareAccelerationPrefix, StringComparison.Ordinal))
			secondIcon = Icons.Material.Filled.OpenWith;

		return secondIcon == null ? baseIconArray : new[] { baseIconStr, secondIcon };
	}

	private class ScrollToOptions
	{
		public int? Left { get; set; }

		public int? Top { get; set; }

		public string Behavior { get; set; }
	}

	private async Task DatePicked(DateTime? pickedDate)
	{
		if (!pickedDate.HasValue || _ignoreDatePicked == pickedDate || _filteredclips == null)
			return;

		var firstClipAtDate = _filteredclips.FirstOrDefault(c => c.StartDate.Date == pickedDate);
		if (firstClipAtDate == null)
			return;

		await SetActiveClip(firstClipAtDate);
		await ScrollListToActiveClip();
		await Task.Delay(500);
	}

	private async Task ScrollListToActiveClip()
	{
		if (_filteredclips == null)
			return;

		var listBoundingRect = await _eventsList.MudGetBoundingClientRectAsync();
		var index = Array.IndexOf(_filteredclips, _activeClip);
		if (index < 0)
			return;

		var top = (int)(index * EventItemHeight - listBoundingRect.Height / 2 + EventItemHeight / 2);

		await JsRuntime.InvokeVoidAsync("HTMLElement.prototype.scrollTo.call", _eventsList, new ScrollToOptions
		{
			Behavior = "smooth",
			Top = top
		});
	}

	private async Task SetActiveClip(Clip clip)
	{
		_activeClip = clip;
		await _clipViewer.SetClipAsync(_activeClip);
		_ignoreDatePicked = clip.StartDate.Date;
		await _datePicker.GoToDate(clip.StartDate.Date);
	}

    // New method for mobile interaction
    private async Task SetActiveClipMobile(Clip clip)
    {
        await SetActiveClip(clip);
        // On mobile, hide the browser after selection
        _isBrowserVisible = false;
    }

	private void EventListScrolled()
	{
		if (!_scrollDebounceTimer.Enabled)
			_scrollDebounceTimer.Enabled = true;
	}

	private async void ScrollDebounceTimerTick(object _, ElapsedEventArgs __)
	{
		if (_filteredclips == null || _filteredclips.Length == 0)
			return;

		var scrollTop = await JsRuntime.InvokeAsync<double>("getProperty", _eventsList, "scrollTop");
		var listBoundingRect = await _eventsList.MudGetBoundingClientRectAsync();
		var centerScrollPosition = scrollTop + listBoundingRect.Height / 2 + EventItemHeight / 2;
		var itemIndex = (int)centerScrollPosition / EventItemHeight;
		var atClip = _filteredclips.ElementAt(Math.Min(_filteredclips.Length - 1, itemIndex));

		_ignoreDatePicked = atClip.StartDate.Date;
		await _datePicker.GoToDate(atClip.StartDate.Date);

		_scrollDebounceTimer.Enabled = false;
	}

	private async Task PreviousButtonClicked()
	{
		if (_filteredclips == null)
			return;

		// Optimization: _filteredclips is already sorted descending by StartDate.
		// Finding the first clip with StartDate < ActiveClip.StartDate gives us the next older clip.
		// This avoids O(N log N) sorting.
		var previous = _filteredclips
			.FirstOrDefault(c => c.StartDate < _activeClip.StartDate);

		if (previous != null)
		{
			await SetActiveClip(previous);
			await ScrollListToActiveClip();
		}
	}

	private async Task NextButtonClicked()
	{
		if (_filteredclips == null)
			return;

		// Optimization: _filteredclips is already sorted descending by StartDate.
		// We want the smallest StartDate that is still > ActiveClip.StartDate.
		// Iterating the sorted list, we track the last item > active.
		// This avoids O(N log N) sorting.
		Clip next = null;
		for (var i = 0; i < _filteredclips.Length; i++)
		{
			if (_filteredclips[i].StartDate > _activeClip.StartDate)
				next = _filteredclips[i];
			else
				break;
		}

		if (next != null)
		{
			await SetActiveClip(next);
			await ScrollListToActiveClip();
		}
	}

	private async Task DatePickerOnMouseWheel(WheelEventArgs e)
	{
		if (e.DeltaY == 0 && e.DeltaX == 0 || !_datePicker.PickerMonth.HasValue || _filteredclips == null)
			return;

		var goToNextMonth = e.DeltaY + e.DeltaX * -1 < 0;
		var targetDate = _datePicker.PickerMonth.Value.AddMonths(goToNextMonth ? 1 : -1);
		var endOfMonth = targetDate.AddMonths(1);

		var clipsInOrAfterTargetMonth = _filteredclips.Any(c => c.StartDate >= targetDate);
		var clipsInOrBeforeTargetMonth = _filteredclips.Any(c => c.StartDate <= endOfMonth);
		
		if (goToNextMonth && !clipsInOrAfterTargetMonth)
			return;
		
		if (!goToNextMonth && !clipsInOrBeforeTargetMonth)
			return;
		
		_ignoreDatePicked = targetDate;
		await _datePicker.GoToDate(targetDate);
	}

	private void ToggleExportMode()
	{
		_isExportMode = !_isExportMode;
	}

	private void ShowDownloads()
	{
		var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
		DialogService.Show<DownloadsDialog>("Export Jobs", options);
	}

	private async Task OnExportRequested(ExportRequest request)
	{
		try
		{
			Snackbar.Add("Export started", Severity.Info);
			await ExportClientService.StartExportAsync(request);
			_isExportMode = false;
		}
		catch (Exception ex)
		{
			Snackbar.Add($"Export failed: {ex.Message}", Severity.Error);
		}
	}

    private void ToggleBrowser()
    {
        _isBrowserVisible = !_isBrowserVisible;
    }

    private void ToggleMap()
    {
        _isMapVisible = !_isMapVisible;
    }

    private void OnTelemetryUpdated(TelemetryData data)
    {
        _currentTelemetry = data;
        StateHasChanged();
    }

    private MapViewer _mapViewer;
    private List<double[]> _currentPath;

    private void OnPathAvailable(List<double[]> path)
    {
        _currentPath = path;
        StateHasChanged();
    }

	private void ShowChangelog()
	{
		var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
		DialogService.Show<ChangelogDialog>("Version History", options);
	}

    private string GetInitials()
    {
        return _userInitials;
    }

    private void ShowSettings()
    {
        var options = new DialogOptions { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        DialogService.Show<SettingsDialog>("Settings", options);
    }
}
