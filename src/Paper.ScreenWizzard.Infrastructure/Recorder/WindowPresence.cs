using System.Runtime.InteropServices;
using Paper.ScreenWizzard.UseCases.Recorder.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>Whether a window handle still names a window (<c>IsWindow</c>): a closed window's handle does not.</summary>
public sealed class WindowPresence : IWindowPresence
{
    public bool IsOpen(long windowHandle) => IsWindow(new IntPtr(windowHandle));

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
}
