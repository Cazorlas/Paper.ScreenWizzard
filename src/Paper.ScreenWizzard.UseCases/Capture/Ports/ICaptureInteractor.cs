using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UseCases.Capture.Ports;

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
