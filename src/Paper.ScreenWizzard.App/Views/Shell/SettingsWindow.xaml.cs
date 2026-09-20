using System.Windows;
using Paper.ScreenWizzard.App.ViewModels.Shell;
using Paper.ScreenWizzard.App.Views.Shell.Services;

namespace Paper.ScreenWizzard.App.Views.Shell;

/// <summary>Code-behind: only what is about the window itself - closing it when the view model asks.</summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) =>
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.Dispose();
        };
        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
    }

    private void OnCloseRequested(object? sender, SettingsClosedEventArgs e) => Close();
}
