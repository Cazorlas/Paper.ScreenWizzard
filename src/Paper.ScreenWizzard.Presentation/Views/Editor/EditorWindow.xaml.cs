using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.Presentation.Views.Capture;
using Paper.ScreenWizzard.Presentation.Views.Shell.Services;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.Presentation.Views.Editor;

/// <summary>
/// Code-behind: only what belongs to the window itself. It turns the mouse and the wheel into image pixels (through
/// <see cref="EditorCoordinates"/>, the one place that knows zoom and monitor scale), reports the scroll area's size and the monitor's scale to
/// the view model, lays the text box over the picture, takes files dropped on it, and asks the view model before it closes (SPEC editor F7).
/// The canvas draws itself from the view model; nothing here decides what a tool does.
/// </summary>
public partial class EditorWindow : Window
{
    // Each wheel notch (120 units) is one step of this factor.
    private const double ZoomPerNotch = 1.25;

    private readonly EditorViewModel _viewModel;

    /// <param name="viewModel">What the window shows.</param>
    /// <param name="monitors">
    /// When given, the window opens inside the monitor that holds the pointer (<see cref="EditorWindowPlacement"/>) instead of where WPF
    /// centres it, which is wrong on a monitor whose scale differs from the primary's. Tests that pin the window themselves pass none.
    /// </param>
    public EditorWindow(EditorViewModel viewModel, IMonitorCatalog? monitors = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Picture.ViewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        SourceInitialized += (_, _) => TitleBarTheme.Sync(this);
        Loaded += (_, _) =>
        {
            ReportViewport();
            PlaceTextBox();
        };

        // The slider's thumb, held and released: a selected shape is previewed while it is held and takes the thickness once, on release
        // (the slider may already have handled these events itself, hence handledEventsToo).
        ThicknessSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnThicknessDragStarted), true);
        ThicknessSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnThicknessDragCompleted), true);
        Closing += OnClosing;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;
        if (monitors is not null)
        {
            PlaceOnThePointersMonitor(monitors);
        }
    }

    // In two steps, like the "Đã chụp" dialog: first the window is put on that monitor so WPF lays it out at that monitor's scale, then, once
    // it has its real size in physical pixels, it is centred there and pulled inside if it is too big for the screen.
    private void PlaceOnThePointersMonitor(IMonitorCatalog monitors)
    {
        var all = monitors.GetMonitors();
        if (EditorWindowPlacement.MonitorFor(all, monitors.GetCursorPosition()) is not { } monitor)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => PhysicalWindowPlacer.MoveTo(this, monitor.Bounds.X + 48, monitor.Bounds.Y + 48);
        ContentRendered += (_, _) =>
        {
            var actual = PhysicalWindowPlacer.BoundsOf(this);
            PhysicalWindowPlacer.Place(this, EditorWindowPlacement.Place(monitor, new PixelSize(actual.Width, actual.Height)));
        };
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ReportViewport();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            // Each dropped file gets its own window, or its own message when it cannot be opened (SPEC editor F2).
            foreach (var path in paths)
            {
                _viewModel.OpenFileCommand.Execute(path);
            }

            e.Handled = true;
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    // The window is about to close (the X, Alt+F4, Close()): the view model decides whether it may. Nothing is closed silently with unsaved
    // edits, and a save that fails keeps the window (SPEC editor F1, F7). A prompt is another window, so showing it here is allowed;
    // calling Close() on THIS window from here is not (Window.Closing page), and nothing here does.
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmClose())
        {
            e.Cancel = true;
        }
    }

    // ---- the picture: mouse, in image pixels ----

    private PixelPoint ImagePointOf(MouseEventArgs e)
    {
        var position = e.GetPosition(Picture);
        return EditorCoordinates.ViewToImage(position.X, position.Y, _viewModel.Zoom, _viewModel.DpiScale);
    }

    private static bool ShiftIsDown => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

    private void OnPictureMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Taking the keyboard for the picture makes the open text box lose it, and losing it commits the text: ask BEFORE that, or this
        // press would look like a click with no box open and start a second one (SPEC editor: a click elsewhere only finishes the text).
        var endsTheText = _viewModel.IsEditingText;
        Picture.Focus();
        if (endsTheText)
        {
            _viewModel.CommitText();
            e.Handled = true;
            return;
        }

        Picture.CaptureMouse();

        // The second press of a double-click is its own question to the view model (it may open a text for editing); every other press is a press.
        if (e.ClickCount == 2)
        {
            _viewModel.PointerDoubleClicked(ImagePointOf(e), ShiftIsDown);
        }
        else
        {
            _viewModel.PointerDown(ImagePointOf(e), ShiftIsDown);
        }

        e.Handled = true;
    }

    private void OnPictureMouseMove(object sender, MouseEventArgs e) => _viewModel.PointerMoved(ImagePointOf(e), ShiftIsDown);

    private void OnPictureMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The gesture ends first: releasing the capture raises LostMouseCapture at once, and that cancels a gesture that is still running.
        _viewModel.PointerUp(ImagePointOf(e), ShiftIsDown);
        if (Picture.IsMouseCaptured)
        {
            Picture.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    // The mouse was taken away mid-drag (Alt+Tab, a dialog): drop the drag. Releasing the capture in the button-up handler lands here too,
    // but by then the gesture is finished and there is nothing to drop (that order matters: see OnPictureMouseLeftButtonUp).
    private void OnPictureLostMouseCapture(object sender, MouseEventArgs e) => _viewModel.PointerCancelled();

    // ---- zoom and the scroll area ----

    // Ctrl+wheel zooms, keeping the pixel under the pointer where it is; the plain wheel scrolls as usual.
    private void OnScrollerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;
        var anchorInPicture = e.GetPosition(Picture);
        var anchorInViewport = e.GetPosition(Scroller);
        var imageX = anchorInPicture.X / _viewModel.ViewScale;
        var imageY = anchorInPicture.Y / _viewModel.ViewScale;

        _viewModel.ZoomBy(Math.Pow(ZoomPerNotch, e.Delta / 120.0));

        // The layout follows the new zoom; once it has, scroll so the same image pixel is under the pointer again.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                // 13 is the stage's margin (12) plus the picture's 1 px frame, both in front of the picture's own origin.
                Scroller.ScrollToHorizontalOffset(Math.Max(0, (imageX * _viewModel.ViewScale) - anchorInViewport.X + 13));
                Scroller.ScrollToVerticalOffset(Math.Max(0, (imageY * _viewModel.ViewScale) - anchorInViewport.Y + 13));
            }));
    }

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => ReportViewport();

    private void OnScrollerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
        {
            ReportViewport();
        }
    }

    private void ReportViewport() =>
        _viewModel.SetViewport(Scroller.ViewportWidth, Scroller.ViewportHeight, VisualTreeHelper.GetDpi(this).DpiScaleX);

    // ---- the text box over the picture ----

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.IsEditingText) or nameof(EditorViewModel.TextDraftOrigin)
            or nameof(EditorViewModel.ViewScale) or nameof(EditorViewModel.FontSize) or nameof(EditorViewModel.Color)
            or nameof(EditorViewModel.TextDraftFontSize) or nameof(EditorViewModel.TextDraftColor))
        {
            PlaceTextBox();
        }
    }

    // The box sits at the image pixel that was clicked (or where the text being edited is), at the size the text will have once committed, in
    // the colour it will have.
    private void PlaceTextBox()
    {
        if (_viewModel.TextDraftOrigin is not { } origin)
        {
            var wasVisible = TextDraftBox.Visibility == Visibility.Visible;
            TextDraftBox.Visibility = Visibility.Collapsed;
            if (wasVisible)
            {
                // Hand the keyboard back to the picture, so Ctrl+Z and Delete keep working after the text is committed.
                Picture.Focus();
            }

            return;
        }

        var scale = _viewModel.ViewScale;
        Canvas.SetLeft(TextDraftBox, origin.X * scale);
        Canvas.SetTop(TextDraftBox, origin.Y * scale);
        TextDraftBox.FontSize = Math.Max(6.0, _viewModel.TextDraftFontSize * scale);
        TextDraftBox.Foreground = AnnotationRenderer.BrushOf(_viewModel.TextDraftColor);
        if (TextDraftBox.Visibility != Visibility.Visible)
        {
            TextDraftBox.Visibility = Visibility.Visible;

            // Focus after the mouse button that placed the box has gone up, or the click that follows takes it back.
            Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    TextDraftBox.Focus();
                    Keyboard.Focus(TextDraftBox);

                    // A text reopened for editing starts with its old text: the caret goes after it, ready to add to it.
                    TextDraftBox.CaretIndex = TextDraftBox.Text.Length;
                }));
        }
    }

    // ---- the size box and the thickness slider ----

    // The size typed in the box is applied to the selected text or step marker when Enter is pressed or the box loses the keyboard.
    private void OnFontSizeBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            _viewModel.CommitFontSize();

            // Return is also the window's key for "cut the crop region": here it only means "apply this size".
            e.Handled = true;
        }
    }

    private void OnFontSizeBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => _viewModel.CommitFontSize();

    private void OnThicknessDragStarted(object sender, DragStartedEventArgs e) => _viewModel.BeginThicknessChange();

    private void OnThicknessDragCompleted(object sender, DragCompletedEventArgs e) => _viewModel.EndThicknessChange();

    // A click on another control finishes the text. Losing the keyboard to nothing (another program in front) does not: the user is not done.
    private void OnTextDraftLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not null && !ReferenceEquals(e.NewFocus, TextDraftBox) && _viewModel.IsEditingText)
        {
            _viewModel.CommitText();
        }
    }
}
