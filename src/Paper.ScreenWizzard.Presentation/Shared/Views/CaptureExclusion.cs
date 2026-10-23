using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Paper.ScreenWizzard.Presentation.Shared.Views;

/// <summary>
/// Keeps one of the app's windows out of every screen capture and recording (SPEC recorder, "The app never records itself"):
/// <c>SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)</c>, Windows 10 version 2004 and later. Like <see cref="PhysicalWindowPlacer"/>
/// it sits beside the windows it serves and decides nothing. On an older Windows the call fails and the window simply shows.
/// </summary>
public static class CaptureExclusion
{
    private const uint WdaExcludeFromCapture = 0x00000011;

    /// <summary>Call once the window has a handle (SourceInitialized).</summary>
    public static bool Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        return SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
}
