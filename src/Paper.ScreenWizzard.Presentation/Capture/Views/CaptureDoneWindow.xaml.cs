using System.Windows;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.Views;

namespace Paper.ScreenWizzard.Presentation.Capture.Views;

/// <summary>
/// Code-behind: only what is about the window itself. It opens centred on the monitor of the capture, in physical pixels
/// (<see cref="PhysicalWindowPlacer"/>), and closes when the view model says so. Closing it any other way (the title bar's ✕)
/// delivers nothing, the same as Bỏ (SPEC capture F10).
/// </summary>
public partial class CaptureDoneWindow : Window
{
    // About how big the dialog is at 100%, in pixels: only used to open it near the middle before its real size is known.
    private const int EstimatedWidth = 520;
    private const int EstimatedHeight = 280;

    private readonly CaptureDoneViewModel _viewModel;
    private bool _closed;

    public CaptureDoneWindow(CaptureDoneViewModel viewModel, PixelRect monitorArea)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;

        // Two steps, because the window's physical size is only known once it is shown (SizeToContent, and the monitor's DPI).
        // First near the middle of the monitor, so the window opens on the right monitor at about the right place and does not
        // flash in a corner; then exactly centred once it has been drawn (ContentRendered: its size is final by then, at Loaded
        // it was not - measured 2026-09-20, 212 high at Loaded and 279 at ContentRendered).
        SourceInitialized += (_, _) =>
        {
            TitleBarTheme.Sync(this);
            PhysicalWindowPlacer.MoveTo(
                this,
                monitorArea.X + ((monitorArea.Width - EstimatedWidth) / 2),
                monitorArea.Y + ((monitorArea.Height - EstimatedHeight) / 2));
        };
        ContentRendered += (_, _) => PhysicalWindowPlacer.CenterIn(this, monitorArea);
        Loaded += (_, _) => SaveButton.Focus();
        Closed += OnClosed;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        if (!_closed)
        {
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _viewModel.CloseRequested -= OnCloseRequested;
    }
}
