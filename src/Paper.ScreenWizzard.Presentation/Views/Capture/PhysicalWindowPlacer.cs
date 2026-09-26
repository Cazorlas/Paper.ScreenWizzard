using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Presentation.Views.Capture;

/// <summary>
/// The one class that places a window in PHYSICAL desktop pixels. WPF positions windows in device-independent units of one
/// monitor's DPI, which cannot say "exactly the virtual desktop, whose origin may be negative and which spans monitors of
/// different scale"; SetWindowPos on the window's handle can. Everything else in the capture views works in the units WPF gives
/// it and converts through <c>DisplayUnits</c>.
/// </summary>
internal static class PhysicalWindowPlacer
{
    // SetWindowPos flags (winuser.h): keep the Z order (the window is already topmost), keep the size, do not activate.
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>Puts the window exactly on <paramref name="rect"/>: position and size in physical pixels. Creates the handle when it does not exist yet.</summary>
    public static void Place(Window window, PixelRect rect)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, SwpNoZOrder | SwpNoActivate);
    }

    /// <summary>Moves the window's top-left corner to a physical pixel, keeping its size.</summary>
    public static void MoveTo(Window window, int x, int y)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    /// <summary>The window's outer rectangle in physical pixels.</summary>
    public static PixelRect BoundsOf(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        return GetWindowRect(handle, out var rect)
            ? new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
            : default;
    }

    /// <summary>Centres the window, at its present size, on <paramref name="area"/>.</summary>
    public static void CenterIn(Window window, PixelRect area)
    {
        var bounds = BoundsOf(window);
        MoveTo(window, area.X + ((area.Width - bounds.Width) / 2), area.Y + ((area.Height - bounds.Height) / 2));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
}
