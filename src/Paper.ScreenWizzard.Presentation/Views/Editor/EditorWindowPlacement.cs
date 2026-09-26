using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Presentation.Views.Editor;

/// <summary>
/// Where the editor window opens: inside the monitor that holds the pointer, in PHYSICAL pixels of the virtual desktop. WPF's own
/// <c>CenterScreen</c> centres on the primary monitor's DPI, so a 1120-unit window hung outside a 200% secondary monitor (found at T14).
/// The function is pure: it only turns monitors, a pointer and a size into a rectangle, so a test with two made-up monitors proves it.
/// </summary>
public static class EditorWindowPlacement
{
    // Left free on each side, in display units at 100%: room for the taskbar and the edge of the screen (a monitor's bounds include the taskbar).
    private const int MarginUnits = 48;

    /// <summary>The monitor that holds <paramref name="cursor"/>, else the nearest one; null when there are no monitors.</summary>
    public static MonitorInfo? MonitorFor(IReadOnlyList<MonitorInfo> monitors, PixelPoint cursor)
    {
        MonitorInfo? nearest = null;
        long nearestDistance = long.MaxValue;
        foreach (var monitor in monitors)
        {
            var bounds = monitor.Bounds;
            var dx = Math.Max(0, Math.Max(bounds.X - cursor.X, cursor.X - (bounds.X + bounds.Width - 1)));
            var dy = Math.Max(0, Math.Max(bounds.Y - cursor.Y, cursor.Y - (bounds.Y + bounds.Height - 1)));
            var distance = ((long)dx * dx) + ((long)dy * dy);
            if (distance == 0)
            {
                return monitor;
            }

            if (distance < nearestDistance)
            {
                nearest = monitor;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The rectangle for a window of <paramref name="desiredPixels"/> (already in physical pixels of this monitor), centred in
    /// <paramref name="monitor"/> and kept inside it with a margin on every side; too large a window is made smaller.
    /// </summary>
    public static PixelRect Place(MonitorInfo monitor, PixelSize desiredPixels)
    {
        var bounds = monitor.Bounds;
        var margin = (int)Math.Round(MarginUnits * ScaleOf(monitor));
        var width = Math.Clamp(desiredPixels.Width, 1, Math.Max(1, bounds.Width - (2 * margin)));
        var height = Math.Clamp(desiredPixels.Height, 1, Math.Max(1, bounds.Height - (2 * margin)));
        return new PixelRect(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }

    /// <summary>
    /// The same for a size given in display units at 100%: the monitor that holds the pointer is chosen first and its DPI scales the
    /// size. With no monitors the size is returned at the origin.
    /// </summary>
    public static PixelRect Place(IReadOnlyList<MonitorInfo> monitors, PixelPoint cursor, PixelSize desiredAt96Dpi)
    {
        if (MonitorFor(monitors, cursor) is not { } monitor)
        {
            return new PixelRect(0, 0, desiredAt96Dpi.Width, desiredAt96Dpi.Height);
        }

        var scale = ScaleOf(monitor);
        return Place(
            monitor,
            new PixelSize((int)Math.Round(desiredAt96Dpi.Width * scale), (int)Math.Round(desiredAt96Dpi.Height * scale)));
    }

    private static double ScaleOf(MonitorInfo monitor) => monitor.Dpi > 0 ? monitor.Dpi / 96.0 : 1.0;
}
