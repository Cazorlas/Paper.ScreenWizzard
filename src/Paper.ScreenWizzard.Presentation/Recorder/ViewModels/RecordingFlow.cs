using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

/// <summary>
/// Ties one recording together (SPEC recorder): pick the region or the window, the outline and its countdown, the recording, and the
/// "Recorded" window. It decides nothing about the area, the sound or the file: the use case does, and this class opens and closes the
/// windows around its answers. It is created and used on the UI thread; the use case's events arrive on the recording thread and are
/// moved to the UI thread here.
/// </summary>
public sealed class RecordingFlow
{
    private readonly IRecorderInteractor _recorder;
    private readonly IRecorderViews _views;
    private readonly INotifications _notifications;
    private readonly ILocalizer _localizer;
    private readonly IMonitorCatalog _monitors;
    private readonly SynchronizationContext _ui;
    private IRecordingOutline? _outline;
    private PixelRect _area;
    private bool _busy;

    public RecordingFlow(
        IRecorderInteractor recorder,
        IRecorderViews views,
        INotifications notifications,
        ILocalizer localizer,
        IMonitorCatalog monitors)
    {
        _recorder = recorder;
        _views = views;
        _notifications = notifications;
        _localizer = localizer;
        _monitors = monitors;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();
        _recorder.Finished += result => _ui.Post(_ => OnFinished(result), null);
        _recorder.Notice += message => _ui.Post(_ => _notifications.ShowToast(message), null);
    }

    /// <summary>A recording is on its way (picking, counting down or recording): the app's bars step aside (SPEC recorder).</summary>
    public event EventHandler? Began;

    /// <summary>Nothing is being recorded any more: the bars that were open come back.</summary>
    public event EventHandler? Ended;

    /// <summary>Recording started, paused, resumed or stopped: the tray shows it.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The area was too small (F7): the recording bar says so.</summary>
    public event Action<NotificationMessage>? AreaRefused;

    public bool IsBusy => _busy;

    public RecorderState State => _recorder.State;

    public TimeSpan Elapsed => _recorder.Elapsed;

    public bool IsRecording => _recorder.State is RecorderState.Recording or RecorderState.Paused;

    /// <summary>The recording bar opens only while nothing records: a recording is controlled from the tray (SPEC recorder, step 4).</summary>
    public bool MayOpenBar => !IsRecording;

    /// <summary>Records with <paramref name="choices"/>; the task ends once recording runs, or it gave up.</summary>
    public async Task StartAsync(RecorderSettings choices)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        Began?.Invoke(this, EventArgs.Empty);
        var recording = false;
        try
        {
            recording = await BeginAsync(choices);
        }
        finally
        {
            if (!recording)
            {
                End();
            }
        }
    }

    /// <summary>
    /// The start/stop hotkey: stops what runs, or records with the last choices (SPEC recorder, Inputs). While Settings is open
    /// (<paramref name="mayStart"/> false) it still stops a recording but starts none, since the key may be being typed into a hotkey box.
    /// </summary>
    public async Task StartOrStopAsync(RecorderSettings lastChoices, bool mayStart = true)
    {
        if (await _recorder.StopIfActiveAsync())
        {
            return;
        }

        if (mayStart)
        {
            await StartAsync(lastChoices);
        }
    }

    /// <summary>Stops a countdown or a recording; a recording is saved first (also when the app exits).</summary>
    public async Task StopAsync()
    {
        await _recorder.StopAsync();
    }

    public void TogglePause()
    {
        _recorder.TogglePause();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> BeginAsync(RecorderSettings choices)
    {
        var monitors = _monitors.GetMonitors();
        var pointer = _views.PointerPosition();
        PixelRect? picked = null;
        long? window = null;
        switch (choices.Target)
        {
            case RecordTargetKind.Region:
                picked = await _views.PickRegionAsync(monitors);
                if (picked is null)
                {
                    return false;
                }

                break;
            case RecordTargetKind.Window:
                var windows = _recorder.ListWindows();
                var chosen = await _views.PickWindowAsync(monitors, point => _recorder.WindowAt(windows, point) is { } w ? new PickedWindow(w.VisibleFrame, w.Handle) : null);
                if (chosen is null)
                {
                    return false;
                }

                picked = chosen.Frame;
                window = chosen.Handle;
                break;
        }

        var area = _recorder.ResolveArea(choices.Target, choices.MonitorIndex, picked, monitors, pointer);
        if (!area.IsUsable)
        {
            // F7: nothing starts; the bar comes back and says why.
            if (area.Message is { } refused)
            {
                AreaRefused?.Invoke(refused);
            }

            return false;
        }

        _area = area.Area;
        var outline = _views.OpenOutline(area.Area);
        _outline = outline;
        var progress = new Progress<int>(seconds => outline.ShowCountdown(seconds));
        var started = await _recorder.StartAsync(new RecordRequest(area.Area, window, choices), progress, CancellationToken.None);

        // Progress<T> posts each report; give the last ones their turn before the number goes.
        await Task.Yield();
        outline.ShowCountdown(null);
        if (!started.Started)
        {
            CloseOutline();
            if (started.Issue == RecordStartIssue.AreaUnusable && started.Message is { } small)
            {
                AreaRefused?.Invoke(small);
            }
            else if (started.Message is { } message)
            {
                _notifications.ShowError(message);
            }

            return false;
        }

        foreach (var notice in started.Notices)
        {
            _notifications.ShowToast(notice);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void OnFinished(RecordingResult result)
    {
        CloseOutline();
        if (result.Saved && result.FilePath is { } path)
        {
            var viewModel = new RecordedViewModel(result, _localizer);
            var handle = _views.OpenRecorded(viewModel, _area);
            viewModel.OpenVideoRequested += (_, _) => viewModel.ShowProblem(_recorder.OpenVideo(path));
            viewModel.ShowInFolderRequested += (_, _) => viewModel.ShowProblem(_recorder.ShowInFolder(path));
            viewModel.CloseRequested += (_, _) => handle.Close();
        }
        else if (result.Message is { } message)
        {
            _notifications.ShowError(message);
        }

        End();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CloseOutline()
    {
        _outline?.Close();
        _outline = null;
    }

    private void End()
    {
        if (!_busy)
        {
            return;
        }

        _busy = false;
        Ended?.Invoke(this, EventArgs.Empty);
    }
}
