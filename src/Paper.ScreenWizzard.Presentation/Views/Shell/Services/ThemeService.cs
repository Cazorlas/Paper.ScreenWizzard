using System.Windows;
using Microsoft.Win32;
using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.Presentation.Views.Shell.Services;

/// <summary>
/// The colours of every window: swaps <c>Resources/Themes/Light.xaml</c> or <c>Dark.xaml</c> in Application.Resources. Views
/// paint only with DynamicResource brushes, so an open window recolours the moment this runs. "System" follows the Windows
/// app-mode setting (light or dark).
/// </summary>
public sealed class ThemeService
{
    private const string AssemblyName = "Paper.ScreenWizzard.Presentation";

    private ResourceDictionary? _current;

    /// <summary>Raised after the brushes were swapped.</summary>
    public event EventHandler? ThemeChanged;

    public bool IsDark { get; private set; }

    /// <summary>Swaps the brushes; call on the UI thread.</summary>
    public void Apply(AppTheme theme)
    {
        var dark = theme == AppTheme.Dark || (theme == AppTheme.System && !WindowsUsesLightApps());
        var next = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/{AssemblyName};component/Resources/Themes/{(dark ? "Dark" : "Light")}.xaml"),
        };

        // New dictionary first, then the old one out: the last merged dictionary wins a lookup, so no brush is ever missing.
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Add(next);
        if (_current is not null)
        {
            merged.Remove(_current);
        }

        _current = next;
        IsDark = dark;
        foreach (var window in Application.Current.Windows.OfType<Window>())
        {
            TitleBarTheme.Sync(window);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    // HKCU\...\Themes\Personalize\AppsUseLightTheme is 0 for dark and 1 for light; absent on old builds, which are light.
    private static bool WindowsUsesLightApps()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}
