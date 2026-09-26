using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>
/// One capture run. It holds the frozen snapshot the user chooses on. Coordinates in and out are physical pixels of the
/// virtual desktop.
/// </summary>
public interface ICaptureSession
{
    CaptureKind Kind { get; }

    DesktopSnapshot Snapshot { get; }

    /// <summary>False once the session finished, was cancelled, or was replaced by a newer one (SPEC capture F8).</summary>
    bool IsActive { get; }

    /// <summary>The rectangle a drag from <paramref name="from"/> to <paramref name="to"/> would capture: normalised and clamped, for the live size label.</summary>
    PixelRect PreviewRectangle(PixelPoint from, PixelPoint to);

    CaptureOutcome CompleteRectangle(PixelPoint from, PixelPoint to);

    CaptureOutcome CompleteFreeform(IReadOnlyList<PixelPoint> outline);

    /// <summary>The window under the pointer, or the whole monitor when the pointer is over the desktop background.</summary>
    WindowHit HitTestWindow(PixelPoint pointer);

    CaptureOutcome CompleteWindow(PixelPoint pointer);

    CaptureOutcome CompleteFullScreen();

    /// <summary>Checks that the monitors still match the snapshot; reports <see cref="CaptureIssue.DisplayChanged"/> if not (F6).</summary>
    CaptureIssue CheckDisplayUnchanged();

    void Cancel();
}
