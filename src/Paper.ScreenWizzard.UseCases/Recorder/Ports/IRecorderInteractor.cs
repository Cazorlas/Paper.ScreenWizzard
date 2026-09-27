using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Recorder.Ports;

/// <summary>Everything the recorder decides (SPEC recorder): the area, the countdown, pause, the sound, the file, and why it stops.</summary>
public interface IRecorderInteractor
{
    RecorderState State { get; }

    /// <summary>The video time recorded so far, pauses left out.</summary>
    TimeSpan Elapsed { get; }

    /// <summary>The windows on screen now, for picking one to record.</summary>
    IReadOnlyList<WindowInfo> ListWindows();

    /// <summary>The window a click at <paramref name="pointer"/> records: the topmost one there (SPEC recorder, "What is recorded").</summary>
    WindowInfo? WindowAt(IReadOnlyList<WindowInfo> windows, PixelPoint pointer);

    /// <summary>Opens the saved video; a failure is said (null when it opened).</summary>
    NotificationMessage? OpenVideo(string path);

    NotificationMessage? ShowInFolder(string path);

    RecordAreaResult ResolveArea(RecordTargetKind kind, int? monitorIndex, PixelRect? picked, IReadOnlyList<MonitorInfo> monitors, PixelPoint pointer);

    /// <summary>
    /// Counts down (reporting the seconds left), then starts recording and returns. Stopping during the countdown records nothing and
    /// makes no file.
    /// </summary>
    Task<RecordStartResult> StartAsync(RecordRequest request, IProgress<int>? countdown, CancellationToken cancellationToken);

    void Pause();

    void Resume();

    /// <summary>Stops a countdown or a recording; a recording is saved first (also when the app exits).</summary>
    Task<RecordingResult?> StopAsync();

    /// <summary>Raised once a recording has ended, however it ended; on the recording thread.</summary>
    event Action<RecordingResult>? Finished;

    /// <summary>Something the user should know while recording goes on (F4, F5); on the thread that found it.</summary>
    event Action<NotificationMessage>? Notice;
}
