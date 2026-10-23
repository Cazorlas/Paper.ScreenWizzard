using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

/// <summary>A window picked to record: its visible frame and its handle, watched while recording (SPEC recorder F1).</summary>
public sealed record PickedWindow(PixelRect Frame, long Handle);

/// <summary>The outline around the recorded area, which also shows the countdown in its middle.</summary>
public interface IRecordingOutline : IViewHandle
{
    /// <summary>The number of the countdown; null hides it.</summary>
    void ShowCountdown(int? secondsLeft);
}

/// <summary>Opens the recorder's windows, so the flow is tested with fakes and the real windows are tested on their own.</summary>
public interface IRecorderViews
{
    /// <summary>Where the pointer is now, in physical desktop pixels.</summary>
    PixelPoint PointerPosition();

    /// <summary>A region dragged on the live screen; null when the user pressed Esc or the right button.</summary>
    Task<PixelRect?> PickRegionAsync(IReadOnlyList<MonitorInfo> monitors);

    /// <summary>A window clicked on the live screen, highlighted under the pointer as <paramref name="windowAt"/> answers; null when cancelled.</summary>
    Task<PickedWindow?> PickWindowAsync(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?> windowAt);

    IRecordingOutline OpenOutline(PixelRect area);

    IViewHandle OpenRecorded(RecordedViewModel viewModel, PixelRect area);
}
