using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Capture;

/// <summary>
/// Lists the top-level windows in the order <c>EnumWindows</c> gives them, which is top to bottom in z-order (the Learn page does not say
/// so; the E2E test with two overlapping windows proves it on this Windows). <c>ZOrder</c> is the position in the returned list. The visible
/// frame is <c>DWMWA_EXTENDED_FRAME_BOUNDS</c>, not <c>GetWindowRect</c>, so the invisible resize border and shadow are left out; when DWM
/// cannot answer the window rectangle is used. No decisions here: hidden, minimised and cloaked windows are reported with their flags and
/// the use case chooses.
/// </summary>
public sealed class WindowCatalog : IWindowCatalog
{
    public IReadOnlyList<WindowInfo> GetWindows()
    {
        var handles = new List<IntPtr>();

        // The callback must not throw into native code, and it returns TRUE to go on to the next window (EnumWindows page).
        NativeMethods.EnumWindowsProc collect = (window, _) =>
        {
            handles.Add(window);
            return true;
        };
        if (!NativeMethods.EnumWindows(collect, IntPtr.Zero))
        {
            return [];
        }

        GC.KeepAlive(collect);

        var windows = new List<WindowInfo>(handles.Count);
        foreach (var handle in handles)
        {
            var title = TitleOf(handle);
            var frame = FrameOf(handle);
            if (title.Length == 0 && frame.Width <= 0 && frame.Height <= 0)
            {
                continue;
            }


            windows.Add(new WindowInfo(
                handle.ToInt64(),
                title,
                frame,
                NativeMethods.IsWindowVisible(handle),
                NativeMethods.IsIconic(handle),
                IsCloaked(handle),
                false,
                windows.Count,
                IsDesktopWindow(handle)));
        }

        return windows;
    }

    private static bool IsDesktopWindow(IntPtr window)
    {
        var buffer = new char[64];
        var copied = NativeMethods.GetClassNameW(window, buffer, buffer.Length);
        var name = copied > 0 ? new string(buffer, 0, copied) : string.Empty;
        return name is "Progman" or "WorkerW";
    }

    private static string TitleOf(IntPtr window)
    {
        // Another process's window answers from its caption without waiting for that process (GetWindowText page), so this cannot hang on
        // a frozen program; a window of this process is sent WM_GETTEXT and answers at once.
        var length = NativeMethods.GetWindowTextLengthW(window);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        var copied = NativeMethods.GetWindowTextW(window, buffer, buffer.Length);
        return copied > 0 ? new string(buffer, 0, copied) : string.Empty;
    }

    private static PixelRect FrameOf(IntPtr window)
    {
        if (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaExtendedFrameBounds, out NativeMethods.Rect frame, 16) == 0)
        {
            return ToRect(frame);
        }

        return NativeMethods.GetWindowRect(window, out var rect) ? ToRect(rect) : default;
    }

    private static bool IsCloaked(IntPtr window) =>
        NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaCloaked, out uint reason, sizeof(uint)) == 0 && reason != 0;

    private static PixelRect ToRect(NativeMethods.Rect rect) =>
        new(rect.Left, rect.Top, Math.Max(0, rect.Right - rect.Left), Math.Max(0, rect.Bottom - rect.Top));
}
