using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.UseCases.Capture.UseCases;

/// <summary>
/// One capture run over a frozen snapshot (SPEC capture). It crops the snapshot; it never reads the screen again, so
/// what the user chose on is exactly what comes out.
/// </summary>
public sealed class CaptureSession : ICaptureSession
{
    /// <summary>How far the frame of a maximized window may reach past its monitor (the invisible resize border), in physical pixels.</summary>
    private const int MaximizedOverhang = 16;

    private readonly FullScreenScope _scope;
    private readonly IMonitorCatalogPort _monitors;
    private readonly ILogPort _log;
    private volatile bool _isActive = true;

    public CaptureSession(
        CaptureKind kind,
        FullScreenScope scope,
        DesktopSnapshot snapshot,
        IMonitorCatalogPort monitors,
        ILogPort log)
    {
        Kind = kind;
        Snapshot = snapshot;
        _scope = scope;
        _monitors = monitors;
        _log = log;
    }

    public CaptureKind Kind { get; }

    public DesktopSnapshot Snapshot { get; }

    public bool IsActive => _isActive;

    public PixelRect PreviewRectangle(PixelPoint from, PixelPoint to) =>
        CaptureGeometry.Clamp(CaptureGeometry.Normalise(from, to), Snapshot.VirtualScreen);

    public CaptureOutcome CompleteRectangle(PixelPoint from, PixelPoint to)
    {
        if (!_isActive)
        {
            return NotActive();
        }

        var region = PreviewRectangle(from, to);
        return CaptureGeometry.IsTooSmall(region)
            ? Refuse(CaptureIssue.RegionTooSmall, NotificationMessage.Of("Capture.RegionTooSmall"))
            : Finish(region, mask: null);
    }

    public CaptureOutcome CompleteFreeform(IReadOnlyList<PixelPoint> outline)
    {
        if (!_isActive)
        {
            return NotActive();
        }

        if (CaptureGeometry.IsOutlineTooSmall(outline))
        {
            return Refuse(CaptureIssue.OutlineTooSmall, NotificationMessage.Of("Capture.OutlineTooSmall"));
        }

        var region = CaptureGeometry.Clamp(CaptureGeometry.BoundingBox(outline), Snapshot.VirtualScreen);
        if (region.Width == 0 || region.Height == 0)
        {
            return Refuse(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "The outline is outside every monitor."));
        }

        return Finish(region, outline);
    }

    public WindowHit HitTestWindow(PixelPoint pointer)
    {
        var top = Snapshot.Windows
            .Where(w => w.IsVisible && !w.IsMinimized && !w.IsCloaked && !w.IsOwnOverlay && !w.IsDesktop && CaptureGeometry.Contains(w.VisibleFrame, pointer))
            .OrderBy(w => w.ZOrder)
            .FirstOrDefault();
        if (top is not null)
        {
            return new WindowHit(true, top.VisibleFrame, top.Title, false);
        }

        var monitor = Snapshot.Monitors.FirstOrDefault(m => CaptureGeometry.Contains(m.Bounds, pointer));
        return monitor is null
            ? new WindowHit(false, default, string.Empty, false)
            : new WindowHit(true, monitor.Bounds, string.Empty, true);
    }

    public CaptureOutcome CompleteWindow(PixelPoint pointer)
    {
        if (!_isActive)
        {
            return NotActive();
        }

        var hit = HitTestWindow(pointer);
        if (!hit.Found)
        {
            return Refuse(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "There is no window or monitor under the pointer."));
        }

        var region = CaptureGeometry.Clamp(hit.Frame, ClampArea(hit.Frame));
        return region.Width == 0 || region.Height == 0
            ? Refuse(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "The window is outside every monitor."))
            : Finish(region, mask: null);
    }

    /// <summary>
    /// The frame of a window maximized on one monitor overhangs it by a border of a few pixels; on the second monitor the overhang lands
    /// on the first one, so clamping to the whole desktop would add a sliver of the neighbour (SPEC capture, "Ảnh cửa sổ"). A frame
    /// that fits its own monitor plus that border is cut to the monitor; a window that really spans monitors keeps the whole desktop.
    /// </summary>
    private PixelRect ClampArea(PixelRect frame)
    {
        var centre = new PixelPoint(frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2));
        var home = Snapshot.Monitors.FirstOrDefault(m => CaptureGeometry.Contains(m.Bounds, centre));
        if (home is null)
        {
            return Snapshot.VirtualScreen;
        }

        var bounds = home.Bounds;
        var fits = frame.X >= bounds.X - MaximizedOverhang
            && frame.Y >= bounds.Y - MaximizedOverhang
            && frame.X + frame.Width <= bounds.X + bounds.Width + MaximizedOverhang
            && frame.Y + frame.Height <= bounds.Y + bounds.Height + MaximizedOverhang;
        return fits ? bounds : Snapshot.VirtualScreen;
    }

    public CaptureOutcome CompleteFullScreen()
    {
        if (!_isActive)
        {
            return NotActive();
        }

        PixelRect area;
        if (_scope == FullScreenScope.AllMonitors)
        {
            area = Snapshot.VirtualScreen;
        }
        else
        {
            // The pointer where it was when the snapshot was taken; off every monitor, the primary one.
            var monitor = Snapshot.Monitors.FirstOrDefault(m => CaptureGeometry.Contains(m.Bounds, Snapshot.CursorPosition))
                ?? Snapshot.Monitors.FirstOrDefault(m => m.IsPrimary)
                ?? Snapshot.Monitors.FirstOrDefault();
            if (monitor is null)
            {
                return Refuse(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "There is no monitor."));
            }

            area = CaptureGeometry.Clamp(monitor.Bounds, Snapshot.VirtualScreen);
        }

        return Finish(area, mask: null);
    }

    public CaptureIssue CheckDisplayUnchanged()
    {
        if (_monitors.GetLayoutSignature() == Snapshot.LayoutSignature)
        {
            return CaptureIssue.None;
        }

        _isActive = false;
        _log.Warning("The monitor layout changed while the user was choosing; the capture was cancelled.");
        return CaptureIssue.DisplayChanged;
    }

    public void Cancel() => _isActive = false;

    private CaptureOutcome Finish(PixelRect region, IReadOnlyList<PixelPoint>? mask)
    {
        try
        {
            var virtualScreen = Snapshot.VirtualScreen;
            var image = PixelImageOps.Crop(Snapshot.Image, virtualScreen.X, virtualScreen.Y, region);
            if (mask is not null)
            {
                image = PixelImageOps.ClearOutside(image, CaptureGeometry.InsideMask(mask, region));
            }

            _isActive = false;
            return new CaptureOutcome(image, region, CaptureIssue.None, null);
        }
        catch (OutOfMemoryException exception)
        {
            _log.Error("The captured image is too large to hold.", exception);
            return Refuse(CaptureIssue.OutOfMemory, NotificationMessage.Of("Capture.ImageTooLarge"));
        }
    }

    private static CaptureOutcome Refuse(CaptureIssue issue, NotificationMessage message) => new(null, null, issue, message);

    private static CaptureOutcome NotActive() =>
        Refuse(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "The capture is no longer active."));
}
