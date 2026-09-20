using System.Windows;

namespace Paper.ScreenWizzard.UiTests.Support;

/// <summary>Loads a resource dictionary of the app straight from its file, without swapping anything in the running windows.</summary>
public static class ResourceFiles
{
    private const string AppAssembly = "Paper.ScreenWizzard.Presentation";

    /// <param name="relativePath">Path under the app's Resources folder, for example <c>Themes/Light.xaml</c>.</param>
    public static ResourceDictionary Load(string relativePath) => WpfHost.Instance.Invoke(() =>
        new ResourceDictionary { Source = new Uri($"pack://application:,,,/{AppAssembly};component/Resources/{relativePath}") });
}
