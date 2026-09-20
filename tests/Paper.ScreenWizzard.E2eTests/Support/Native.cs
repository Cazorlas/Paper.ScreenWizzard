using System.Runtime.InteropServices;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>What the tests need from Windows to look at the desktop and to press keys; the adapters have their own copy in Infrastructure.</summary>
internal static class Native
{
    public static readonly IntPtr DpiPerMonitorAwareV2 = new(-4);

    public const int SmXVirtualScreen = 76;
    public const int SmYVirtualScreen = 77;
    public const int SmCxVirtualScreen = 78;
    public const int SmCyVirtualScreen = 79;
    public const int SmCMonitors = 80;

    public const uint InputKeyboard = 1;
    public const uint KeyEventKeyUp = 0x0002;

    public const ushort VkControl = 0x11;
    public const ushort VkMenu = 0x12;
    public const ushort VkShift = 0x10;

    public const uint CursorShowing = 0x00000001;

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

    // INPUT is a type followed by a union of MOUSEINPUT, KEYBDINPUT and HARDWAREINPUT. The union starts at offset 8 on 64-bit and
    // its largest member (MOUSEINPUT) sets the size SendInput checks, so the struct below holds a KEYBDINPUT padded to 40 bytes.
    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public uint Type;
        public uint Alignment;
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
        public ulong Padding;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    public static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint count, KeyboardInput[] inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(ref CursorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(IntPtr newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();
}
