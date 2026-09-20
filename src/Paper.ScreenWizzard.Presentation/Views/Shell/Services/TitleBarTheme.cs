using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Paper.ScreenWizzard.Presentation.Views.Shell.Services;

/// <summary>
/// The title bar belongs to Windows, not to WPF, so a brush cannot reach it: a dark window under a white caption looks broken.
/// This asks DWM for a dark caption when the window's own background brush is dark, so the caption follows the theme without
/// the window naming a theme.
/// </summary>
public static class TitleBarTheme
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE, documented for Windows 10 build 18985 and later; older builds ignore the call.
    private const int DwmUseImmersiveDarkMode = 20;

    /// <summary>Matches the caption of <paramref name="window"/> to its background; a window with no handle yet is skipped.</summary>
    public static void Sync(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || window.TryFindResource("Brush.Window") is not SolidColorBrush brush)
        {
            return;
        }

        var dark = IsDark(brush.Color) ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    // A quick perceived-brightness split is enough to tell a dark theme from a light one.
    private static bool IsDark(Color color) => ((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B)) < 128;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
