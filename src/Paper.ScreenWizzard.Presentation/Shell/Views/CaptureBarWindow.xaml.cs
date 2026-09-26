using System.Windows;
using System.Windows.Input;
using Paper.ScreenWizzard.Presentation.Shared.Views;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;

namespace Paper.ScreenWizzard.Presentation.Shell.Views;

/// <summary>Code-behind: only what is about the window itself - dragging it and closing it.</summary>
public partial class CaptureBarWindow : Window
{
    public CaptureBarWindow(CaptureBarViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    // A press on the bar's own surface (a button has already handled its press) starts a drag anywhere on the desktop.
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
