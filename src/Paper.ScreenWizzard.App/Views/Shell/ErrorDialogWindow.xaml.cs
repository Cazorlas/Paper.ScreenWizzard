using System.Windows;
using Paper.ScreenWizzard.App.Views.Shell.Services;

namespace Paper.ScreenWizzard.App.Views.Shell;

/// <summary>Shows an error until the user closes it.</summary>
public partial class ErrorDialogWindow : Window
{
    public ErrorDialogWindow(string title, string text)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = text;
        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
