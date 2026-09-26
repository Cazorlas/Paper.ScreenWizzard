using System.Drawing;

namespace Paper.ScreenWizzard.App;

/// <summary>
/// The tray icon is the icon of the exe itself (<c>app.ico</c>, drawn by <c>installer/make-icon.ps1</c>): one picture for Explorer, the Start
/// menu, Apps in Settings, the installer and the tray, so they cannot drift apart. It is read at the size of the tray's small icon.
/// A process with no icon of its own (the test host) gets the system's default program icon.
/// </summary>
internal static class TrayIconImage
{
    public static Icon Create()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var size = System.Windows.Forms.SystemInformation.SmallIconSize.Width;
            try
            {
                if (Icon.ExtractIcon(path, 0, size) is { } own)
                {
                    return own;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The exe has no readable icon resource: fall through to the default.
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
