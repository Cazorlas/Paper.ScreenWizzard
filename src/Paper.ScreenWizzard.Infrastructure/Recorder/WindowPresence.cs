using Paper.ScreenWizzard.UseCases.Recorder.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Recorder;

/// <summary>Whether a window handle still names a window (<c>IsWindow</c>): a closed window's handle does not.</summary>
public sealed class WindowPresence : IWindowPresence
{
    public bool IsOpen(long windowHandle) => NativeMethods.IsWindow(new IntPtr(windowHandle));
}
