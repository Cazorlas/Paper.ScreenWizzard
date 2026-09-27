using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Recorder;

public enum RecordAreaIssue
{
    None,

    /// <summary>Smaller than <see cref="RecorderRules.MinimumSide"/> on a side once it is on screen and even (F7).</summary>
    TooSmall,

    /// <summary>No part of it lies on a monitor, or nothing was picked.</summary>
    OffScreen,

    NoMonitor,
}

/// <param name="Area">The rectangle of the desktop the video shows, in physical pixels; even width and height.</param>
public sealed record RecordAreaResult(PixelRect Area, RecordAreaIssue Issue)
{
    public bool IsUsable => Issue == RecordAreaIssue.None;
}

/// <summary>
/// Every choice of the recording bar becomes one rectangle of the desktop, fixed when recording starts (SPEC recorder, "What is
/// recorded"): a monitor's bounds, the bounds of every monitor, the dragged region, or the window's frame without its shadow.
/// </summary>
public static class RecordArea
{
    /// <param name="monitorIndex">For <see cref="RecordTargetKind.Monitor"/>: the chosen monitor; null or unknown is the one under the pointer.</param>
    /// <param name="picked">For a region or a window: the rectangle picked on screen, in physical pixels.</param>
    public static RecordAreaResult Resolve(
        RecordTargetKind kind,
        int? monitorIndex,
        PixelRect? picked,
        IReadOnlyList<MonitorInfo> monitors,
        PixelPoint pointer)
    {
        if (monitors.Count == 0)
        {
            return new RecordAreaResult(default, RecordAreaIssue.NoMonitor);
        }

        PixelRect? raw = kind switch
        {
            RecordTargetKind.Monitor => ChooseMonitor(monitorIndex, monitors, pointer).Bounds,
            RecordTargetKind.Desktop => BoundsOf(monitors.Select(m => m.Bounds)),
            _ => picked,
        };
        if (raw is not { } rect)
        {
            return new RecordAreaResult(default, RecordAreaIssue.OffScreen);
        }

        var onScreen = OnMonitors(rect, monitors);
        if (onScreen.Width <= 0 || onScreen.Height <= 0)
        {
            return new RecordAreaResult(default, RecordAreaIssue.OffScreen);
        }

        // A video needs an even width and height: the odd pixel goes at the right and the bottom.
        var even = onScreen with { Width = onScreen.Width & ~1, Height = onScreen.Height & ~1 };
        return even.Width < RecorderRules.MinimumSide || even.Height < RecorderRules.MinimumSide
            ? new RecordAreaResult(even, RecordAreaIssue.TooSmall)
            : new RecordAreaResult(even, RecordAreaIssue.None);
    }

    /// <summary>The chosen monitor, else the one under the pointer, else the primary one.</summary>
    public static MonitorInfo ChooseMonitor(int? monitorIndex, IReadOnlyList<MonitorInfo> monitors, PixelPoint pointer) =>
        monitors.FirstOrDefault(m => m.Index == monitorIndex)
        ?? monitors.FirstOrDefault(m => Contains(m.Bounds, pointer))
        ?? monitors.FirstOrDefault(m => m.IsPrimary)
        ?? monitors[0];

    // The part of the rectangle that lies on some monitor: the bounds of its overlap with each one.
    private static PixelRect OnMonitors(PixelRect rect, IReadOnlyList<MonitorInfo> monitors) =>
        BoundsOf(monitors.Select(m => Intersect(rect, m.Bounds)).Where(r => r.Width > 0 && r.Height > 0));

    private static PixelRect Intersect(PixelRect a, PixelRect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return right <= left || bottom <= top ? default : new PixelRect(left, top, right - left, bottom - top);
    }

    private static PixelRect BoundsOf(IEnumerable<PixelRect> rects)
    {
        var list = rects.ToList();
        if (list.Count == 0)
        {
            return default;
        }

        var left = list.Min(r => r.X);
        var top = list.Min(r => r.Y);
        var right = list.Max(r => r.X + r.Width);
        var bottom = list.Max(r => r.Y + r.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static bool Contains(PixelRect rect, PixelPoint point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;
}
