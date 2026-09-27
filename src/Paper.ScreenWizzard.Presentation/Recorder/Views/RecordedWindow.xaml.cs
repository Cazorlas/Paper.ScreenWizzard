using System.Windows;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;

namespace Paper.ScreenWizzard.Presentation.Recorder.Views;

/// <summary>Code-behind: centring on the recorded area and the title bar's theme; the buttons are the view model's.</summary>
public partial class RecordedWindow : Window
{
    public RecordedWindow(RecordedViewModel viewModel, PixelRect area)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) =>
        {
            TitleBarTheme.Sync(this);
            // Opened while nothing records any more, but a second recording may start before it is closed: it stays out of that one.
            CaptureExclusion.Apply(this);
        };
        ContentRendered += (_, _) => PhysicalWindowPlacer.CenterIn(this, area);
    }
}
