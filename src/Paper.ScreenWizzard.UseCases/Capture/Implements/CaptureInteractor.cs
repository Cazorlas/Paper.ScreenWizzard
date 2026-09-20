using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UseCases.Capture.Implements;

/// <summary>
/// SKELETON (plan T2): every answer is the empty one, so tests written from SPEC capture fail at their assertions and not
/// at the build. The rules arrive with plan T8.
/// </summary>
public sealed class CaptureInteractor : ICaptureInteractor
{
    private readonly IScreenSourcePort _screen;
    private readonly IWindowCatalogPort _windows;
    private readonly IMonitorCatalogPort _monitors;
    private readonly IDelayPort _delay;
    private readonly IImageDelivery _delivery;
    private readonly IClockPort _clock;
    private readonly IFileStorePort _files;
    private readonly ILogPort _log;

    public CaptureInteractor(
        IScreenSourcePort screen,
        IWindowCatalogPort windows,
        IMonitorCatalogPort monitors,
        IDelayPort delay,
        IImageDelivery delivery,
        IClockPort clock,
        IFileStorePort files,
        ILogPort log)
    {
        _screen = screen;
        _windows = windows;
        _monitors = monitors;
        _delay = delay;
        _delivery = delivery;
        _clock = clock;
        _files = files;
        _log = log;
    }

    public Task<CaptureBeginResult> BeginAsync(
        CaptureRequest request,
        IProgress<int>? countdown,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CaptureBeginResult(null, CaptureIssue.None));

    public CaptureRequest RequestFor(CaptureKind kind, AppSettings settings) =>
        new(kind, 0, false, FullScreenScope.MonitorUnderCursor);

    public AfterCapturePlan PlanAfterCapture(AppSettings settings) => new(false, []);

    public CaptureDeliveryResult Deliver(PixelImage image, CaptureDestination destination, AppSettings settings) =>
        new(false, false, null, null);

    public SaveAsSuggestion SuggestSaveAs(AppSettings settings) => new(string.Empty, string.Empty);

    public CaptureDeliveryResult DeliverToPath(PixelImage image, string path, AppSettings settings) =>
        new(false, false, null, null);
}
