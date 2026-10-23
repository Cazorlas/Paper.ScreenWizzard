using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.UiTests.Recorder;

/// <summary>The recorder use case as the flow sees it: answers are set by the test, calls are counted.</summary>
public sealed class FakeRecorderInteractor : IRecorderInteractor
{
    public RecorderState State { get; set; } = RecorderState.Idle;

    public TimeSpan Elapsed { get; set; }

    public RecordAreaAnswer AreaAnswer { get; set; } = new(new PixelRect(300, 200, 800, 450), RecordAreaIssue.None, null);

    public RecordStartResult StartAnswer { get; set; } = new(RecordStartIssue.None, @"C:\Videos\Recording.mp4.part", [], null);

    public List<RecordRequest> Started { get; } = [];

    public int Stops { get; private set; }

    public int Toggles { get; private set; }

    public event Action<RecordingResult>? Finished;

    public event Action<NotificationMessage>? Notice;

    public IReadOnlyList<WindowInfo> ListWindows() => [];

    public WindowInfo? WindowAt(IReadOnlyList<WindowInfo> windows, PixelPoint pointer) => null;

    public NotificationMessage? OpenVideo(string path) => null;

    public NotificationMessage? ShowInFolder(string path) => null;

    public RecordAreaAnswer ResolveArea(RecordTargetKind kind, int? monitorIndex, PixelRect? picked, IReadOnlyList<MonitorInfo> monitors, PixelPoint pointer) => AreaAnswer;

    public Task<RecordStartResult> StartAsync(RecordRequest request, IProgress<int>? countdown, CancellationToken cancellationToken)
    {
        Started.Add(request);
        if (StartAnswer.Started)
        {
            State = RecorderState.Recording;
        }

        return Task.FromResult(StartAnswer);
    }

    public void Pause() => State = RecorderState.Paused;

    public void Resume() => State = RecorderState.Recording;

    public RecorderState TogglePause()
    {
        Toggles++;
        return State;
    }

    public Task<bool> StopIfActiveAsync()
    {
        if (State == RecorderState.Idle)
        {
            return Task.FromResult(false);
        }

        Stops++;
        State = RecorderState.Idle;
        return Task.FromResult(true);
    }

    public Task<RecordingResult?> StopAsync()
    {
        Stops++;
        State = RecorderState.Idle;
        return Task.FromResult<RecordingResult?>(null);
    }

    /// <summary>The recording ended, raised off the UI thread as the real one does.</summary>
    public void Finish(RecordingResult result) => Task.Run(() => Finished?.Invoke(result)).GetAwaiter().GetResult();

    public void Say(NotificationMessage message) => Notice?.Invoke(message);
}

/// <summary>The recorder's windows as records of what the flow opened.</summary>
public sealed class FakeRecorderViews : IRecorderViews
{
    public PixelRect? RegionAnswer { get; set; } = new PixelRect(300, 200, 800, 450);

    public int RegionPicks { get; private set; }

    public List<FakeOutline> Outlines { get; } = [];

    public List<RecordedViewModel> Recorded { get; } = [];

    public PixelPoint PointerPosition() => new(10, 10);

    public Task<PixelRect?> PickRegionAsync(IReadOnlyList<MonitorInfo> monitors)
    {
        RegionPicks++;
        return Task.FromResult(RegionAnswer);
    }

    public Task<PickedWindow?> PickWindowAsync(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?> windowAt) =>
        Task.FromResult<PickedWindow?>(null);

    public IRecordingOutline OpenOutline(PixelRect area)
    {
        var outline = new FakeOutline(area);
        Outlines.Add(outline);
        return outline;
    }

    public IViewHandle OpenRecorded(RecordedViewModel viewModel, PixelRect area)
    {
        Recorded.Add(viewModel);
        return new FakeOutline(area);
    }
}

public sealed class FakeOutline(PixelRect area) : IRecordingOutline
{
    public PixelRect Area { get; } = area;

    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    public void ShowCountdown(int? secondsLeft)
    {
    }

    public void Close()
    {
        if (!IsClosed)
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class FakeMonitors : IMonitorCatalog
{
    public IReadOnlyList<MonitorInfo> GetMonitors() => [new MonitorInfo(0, new PixelRect(0, 0, 1920, 1080), true, 96)];

    public PixelPoint GetCursorPosition() => new(10, 10);

    public string GetLayoutSignature() => "one";
}
