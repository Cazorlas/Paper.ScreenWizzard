using System.Windows;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>Which window a dialog opens over: the one the user is in, else the newest visible one, else none.</summary>
internal static class OwnerWindow
{
    public static Window? Find()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(window => window.IsVisible).ToList();
        return windows.FirstOrDefault(window => window.IsActive) ?? windows.LastOrDefault();
    }
}
