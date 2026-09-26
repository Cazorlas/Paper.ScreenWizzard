using System.Globalization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Capture;

/// <summary>
/// The monitors and the pointer as plain data. <c>EnumDisplayMonitors</c> with no device context and no clip lists every monitor in
/// virtual-desktop coordinates; each answers its rectangle and primary flag through <c>GetMonitorInfo</c> and its effective DPI through
/// <c>GetDpiForMonitor</c> (per-monitor-aware process, so the true value). The primary monitor comes first, then left to right.
/// </summary>
public sealed class MonitorCatalog : IMonitorCatalogPort
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var found = new List<(PixelRect Bounds, bool IsPrimary, int Dpi)>();
        NativeMethods.MonitorEnumProc collect = (IntPtr monitor, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
        {
            var info = new NativeMethods.MonitorInfoRaw { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfoRaw>() };
            if (NativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                var bounds = new PixelRect(
                    info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right - info.Monitor.Left,
                    info.Monitor.Bottom - info.Monitor.Top);
                var dpi = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0 ? (int)dpiX : 96;
                found.Add((bounds, (info.Flags & NativeMethods.MonitorInfoPrimary) != 0, dpi));
            }

            return true;
        };

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, collect, IntPtr.Zero))
        {
            return [];
        }

        GC.KeepAlive(collect);
        return found
            .Where(m => m.Bounds.Width > 0 && m.Bounds.Height > 0)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .Select((m, index) => new MonitorInfo(index, m.Bounds, m.IsPrimary, m.Dpi))
            .ToList();
    }

    public PixelPoint GetCursorPosition() =>
        NativeMethods.GetCursorPos(out var point) ? new PixelPoint(point.X, point.Y) : default;

    public string GetLayoutSignature() => string.Join(
        ';',
        GetMonitors().Select(m => string.Create(
            CultureInfo.InvariantCulture,
            $"{m.Bounds.X},{m.Bounds.Y},{m.Bounds.Width}x{m.Bounds.Height}@{m.Dpi}{(m.IsPrimary ? "P" : string.Empty)}")));
}
