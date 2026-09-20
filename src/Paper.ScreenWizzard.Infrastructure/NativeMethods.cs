using System.Runtime.InteropServices;

namespace Paper.ScreenWizzard.Infrastructure;

/// <summary>
/// Every Win32 call of this project, in one place. Each signature was read on Microsoft Learn before it was written (the plan's "API đã
/// tra" table). A function that reports failure through GetLastError is declared with <c>SetLastError = true</c>, and its caller checks the
/// return value and reads <see cref="Marshal.GetLastWin32Error"/> straight away.
/// </summary>
internal static class NativeMethods
{
    // GDI
    public const uint SrcCopy = 0x00CC0020;
    public const uint CaptureBlt = 0x40000000;
    public const uint DibRgbColors = 0;
    public const uint DiNormal = 0x0003;

    // Errors that mean "not enough memory" (winerror.h: ERROR_NOT_ENOUGH_MEMORY 8, ERROR_OUTOFMEMORY 14).
    public const int ErrorNotEnoughMemory = 8;
    public const int ErrorOutOfMemory = 14;

    // DWMWINDOWATTRIBUTE: the page lists DWMWA_NCRENDERING_ENABLED = 1, so EXTENDED_FRAME_BOUNDS is 9 and CLOAKED is 14.
    public const uint DwmwaExtendedFrameBounds = 9;
    public const uint DwmwaCloaked = 14;

    public const uint CursorShowing = 0x00000001;
    public const uint MonitorInfoPrimary = 0x00000001;
    public const int MdtEffectiveDpi = 0;

    // Hot keys
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;
    public const int WmHotkey = 0x0312;
    public static readonly IntPtr HwndMessage = new(-3);

    public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr deviceContext, ref Rect rect, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CursorInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Cursor;
        public Point ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public uint HotspotX;
        public uint HotspotY;
        public IntPtr Mask;
        public IntPtr Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfoRaw
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    // BITMAPINFOHEADER followed by the one RGBQUAD BITMAPINFO declares; unused for a 32-bit BI_RGB bitmap but part of the struct's size.
    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfo
    {
        public uint HeaderSize;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
        public uint Colors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDIBSection(IntPtr deviceContext, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GdiFlush();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(ref CursorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DrawIconEx(IntPtr deviceContext, int x, int y, IntPtr icon, int width, int height, uint stepIfAnimated, IntPtr flickerFreeBrush, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLengthW(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextW(IntPtr window, [Out] char[] text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassNameW(IntPtr window, [Out] char[] name, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out Rect value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out uint value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr deviceContext, IntPtr clip, MonitorEnumProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfoRaw info);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr window, int id);
}
