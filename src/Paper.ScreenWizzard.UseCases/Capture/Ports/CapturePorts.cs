using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

/// <summary>What a screen read gave back.</summary>
public enum ScreenCaptureIssue
{
    None,
    OutOfMemory,
    Failed,
}

public sealed record ScreenCaptureResult(PixelImage? Image, ScreenCaptureIssue Issue);

/// <summary>Reads the pixels of the virtual desktop, physical pixels, in one go.</summary>
public interface IScreenSourcePort
{
    /// <summary>Captures <paramref name="area"/> of the virtual desktop; the pointer is drawn into it when asked.</summary>
    ScreenCaptureResult Capture(PixelRect area, bool includeCursor);
}

/// <summary>The top-level windows at this instant, topmost first, each with its visible frame (no invisible shadow).</summary>
public interface IWindowCatalogPort
{
    IReadOnlyList<WindowInfo> GetWindows();
}

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

/// <summary>Everything the capture feature decides.</summary>
public interface ICaptureInteractor
{
    /// <summary>
    /// Counts down (reporting the seconds left), takes the snapshot, and starts a session. Any session still open is
    /// cancelled first, so two overlays never stack (SPEC capture F8).
    /// </summary>
    Task<CaptureBeginResult> BeginAsync(CaptureRequest request, IProgress<int>? countdown, CancellationToken cancellationToken);

    CaptureRequest RequestFor(CaptureKind kind, AppSettings settings);

    AfterCapturePlan PlanAfterCapture(AppSettings settings);

    /// <summary>Delivers to one destination (clipboard or file); a failure keeps the dialog open (F4, F5).</summary>
    CaptureDeliveryResult Deliver(PixelImage image, CaptureDestination destination, AppSettings settings);

    SaveAsSuggestion SuggestSaveAs(AppSettings settings);

    CaptureDeliveryResult DeliverToPath(PixelImage image, string path, AppSettings settings);
}
