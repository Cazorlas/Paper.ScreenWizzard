using System.Runtime.InteropServices;

namespace Paper.ScreenWizzard.App.Startup;

/// <summary>
/// The Win32 calls of the entry host: putting the capture bar at a physical-pixel position, reading where it is, and freeing the icon handle
/// the tray icon was drawn from. (Infrastructure has its own copy for the adapters; this project does not reference their internals.)
/// </summary>
internal static class NativeMethods
{
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

}
