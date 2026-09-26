using System.Globalization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.UseCases.Capture.Implements;

/// <summary>Everything the capture feature decides (SPEC capture): the countdown, the snapshot, one session at a time, where an image goes.</summary>
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
    private readonly object _lock = new();
    private CaptureSession? _current;
    private long _generation;

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

    public async Task<CaptureBeginResult> BeginAsync(
        CaptureRequest request,
        IProgress<int>? countdown,
        CancellationToken cancellationToken)
    {
        // A newer capture drops the open session and any countdown still running (SPEC capture F8).
        var mine = StartNewGeneration();

        for (var secondsLeft = request.DelaySeconds; secondsLeft > 0; secondsLeft--)
        {
            countdown?.Report(secondsLeft);
            try
            {
                await _delay.DelayAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new CaptureBeginResult(null, CaptureIssue.None);
            }

            if (!IsCurrent(mine))
            {
                return new CaptureBeginResult(null, CaptureIssue.None);
            }
        }

        var snapshot = TakeSnapshot(request, out var issue);
        if (snapshot is null)
        {
            return new CaptureBeginResult(null, issue);
        }

        var session = new CaptureSession(request.Kind, request.FullScreenScope, snapshot, _monitors, _log);
        lock (_lock)
        {
            if (mine != _generation)
            {
                return new CaptureBeginResult(null, CaptureIssue.None);
            }

            _current = session;
        }

        return new CaptureBeginResult(session, CaptureIssue.None);
    }

    public CaptureRequest RequestFor(CaptureKind kind, AppSettings settings) =>
        new(kind, settings.DelaySeconds, settings.IncludeCursor, settings.FullScreenScope);

    public AfterCapturePlan PlanAfterCapture(AppSettings settings) => settings.AfterCapture switch
    {
        AfterCaptureAction.OpenEditor => new AfterCapturePlan(false, [CaptureDestination.Editor]),
        AfterCaptureAction.CopyToClipboard => new AfterCapturePlan(false, [CaptureDestination.Clipboard]),
        AfterCaptureAction.SaveToFile => new AfterCapturePlan(false, [CaptureDestination.File]),
        AfterCaptureAction.ClipboardAndFile => new AfterCapturePlan(false, [CaptureDestination.Clipboard, CaptureDestination.File]),
        _ => new AfterCapturePlan(true, []),
    };

    public CaptureDeliveryResult Deliver(PixelImage image, CaptureDestination destination, AppSettings settings)
    {
        switch (destination)
        {
            case CaptureDestination.Clipboard:
                var copied = _delivery.CopyToClipboard(image);
                return copied.Success
                    ? Delivered(null, NotificationMessage.Of("Capture.CopiedToClipboard", Text(image.Width), Text(image.Height)))
                    : Refused(NotificationMessage.Of("Capture.ClipboardFailed", copied.Detail ?? string.Empty));
            case CaptureDestination.File:
                var saved = _delivery.SaveToFolder(image, settings.SaveFolder, settings.Format, settings.JpgQuality);
                return SavedOrRefused(saved, image, settings.SaveFolder);
            default:
                // The window opens the editor itself; there is nothing for the interactor to write.
                return new CaptureDeliveryResult(true, false, null, null);
        }
    }

    public SaveAsSuggestion SuggestSaveAs(AppSettings settings) =>
        new(
            settings.SaveFolder,
            ScreenshotNaming.FileName(_clock.Now, settings.Format, name => _files.FileExists(Path.Combine(settings.SaveFolder, name))));

    public CaptureDeliveryResult DeliverToPath(PixelImage image, string path, AppSettings settings) =>
        SavedOrRefused(_delivery.SaveToPath(image, path, settings.JpgQuality), image, path);

    private static CaptureDeliveryResult Delivered(string? path, NotificationMessage message) => new(true, false, path, message);

    // The dialog stays open with the image intact, so the user can pick another button (SPEC capture F4, F5).
    private static CaptureDeliveryResult Refused(NotificationMessage message) => new(false, true, null, message);

    private static CaptureDeliveryResult SavedOrRefused(DeliveryResult result, PixelImage image, string where) =>
        result.Success
            ? Delivered(result.Path, NotificationMessage.Of("Capture.SavedToFile", result.Path ?? where, Text(image.Width), Text(image.Height)))
            : Refused(NotificationMessage.Of("Capture.SaveFailed", where, result.Detail ?? string.Empty));

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private long StartNewGeneration()
    {
        lock (_lock)
        {
            _current?.Cancel();
            _current = null;
            return ++_generation;
        }
    }

    private bool IsCurrent(long generation)
    {
        lock (_lock)
        {
            return generation == _generation;
        }
    }

    private DesktopSnapshot? TakeSnapshot(CaptureRequest request, out CaptureIssue issue)
    {
        issue = CaptureIssue.None;
        var monitors = _monitors.GetMonitors().ToList();
        if (monitors.Count == 0)
        {
            _log.Error("No monitor is reported.", null);
            issue = CaptureIssue.Failed;
            return null;
        }

        var virtualScreen = CaptureGeometry.Union(monitors.Select(m => m.Bounds));
        ScreenCaptureResult read;
        try
        {
            read = _screen.Capture(virtualScreen, request.IncludeCursor);
        }
        catch (OutOfMemoryException exception)
        {
            _log.Error("Reading the screen ran out of memory.", exception);
            issue = CaptureIssue.OutOfMemory;
            return null;
        }

        if (read.Issue == ScreenCaptureIssue.OutOfMemory)
        {
            issue = CaptureIssue.OutOfMemory;
            return null;
        }

        if (read.Issue != ScreenCaptureIssue.None || read.Image is null)
        {
            _log.Error("The screen could not be read.", null);
            issue = CaptureIssue.Failed;
            return null;
        }

        return new DesktopSnapshot(
            virtualScreen,
            read.Image,
            monitors,
            _windows.GetWindows().ToList(),
            _monitors.GetCursorPosition(),
            _monitors.GetLayoutSignature());
    }
}
