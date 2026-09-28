using System.Windows;
using System.Windows.Input;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;

namespace Paper.ScreenWizzard.Presentation.Recorder.Views;

/// <summary>Code-behind: only what is about the window itself - dragging it, closing it, and staying out of every picture.</summary>
public partial class RecordingBarWindow : Window
{
    public RecordingBarWindow(RecordingBarViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) =>
        {
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.Dispose();
        };
        SourceInitialized += (_, _) =>
        {
            TitleBarTheme.Sync(this);
            CaptureExclusion.Apply(this);
        };
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
