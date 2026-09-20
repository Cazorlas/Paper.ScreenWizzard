using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.Views.Capture;

/// <summary>
/// Code-behind: only what is about the window itself. It covers the virtual desktop, draws the frozen picture one bitmap pixel per
/// physical pixel, and converts the mouse from display units into the physical desktop pixels the view model and the session
/// speak. The conversion is <see cref="DisplayUnits"/>, and the placement is <see cref="PhysicalWindowPlacer"/>; nothing else in
/// this file knows a DPI.
/// </summary>
public partial class SelectionOverlayWindow : Window
{
    // Gap between a label and what it labels, in display units.
    private const double LabelGap = 6;

    private readonly SelectionOverlayViewModel _viewModel;
    private readonly PixelRect _screen;
    private double _scale = 1.0;
    private bool _closed;

    public SelectionOverlayWindow(SelectionOverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _screen = viewModel.Snapshot.VirtualScreen;
        DataContext = viewModel;
        Cursor = viewModel.Kind == CaptureKind.Window ? Cursors.Arrow : Cursors.Cross;

        var picture = PixelImageBitmap.ToBitmapSource(viewModel.Snapshot.Image);
        FrozenImage.Source = picture;
        LitImage.Source = picture;

        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Finished += OnFinished;

        // The handle exists at SourceInitialized, before the window is visible: place it there so it never flashes elsewhere.
        // Loaded places it once more, because WPF sizes a window once it is shown and must not win.
        SourceInitialized += (_, _) => PhysicalWindowPlacer.Place(this, _screen);
        Loaded += (_, _) =>
        {
            PhysicalWindowPlacer.Place(this, _screen);
            ApplyScale();
        };
        Closed += OnClosed;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // WPF resizes a window that crosses to a monitor of another scale; the overlay is exactly the desktop, so put it back.
        PhysicalWindowPlacer.Place(this, _screen);
        ApplyScale();
    }

    // The stage measures exactly the desktop's physical size in display units at the scale the window is drawn at.
    private void ApplyScale()
    {
        _scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = DisplayUnits.ToUnits(_screen.Width, _scale);
        var height = DisplayUnits.ToUnits(_screen.Height, _scale);
        foreach (var layer in new FrameworkElement[] { Stage, FrozenImage, Dim, LitImage })
        {
            layer.Width = width;
            layer.Height = height;
        }

        Refresh();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnFinished(object? sender, SelectionFinishedEventArgs e)
    {
        if (!_closed)
        {
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.Finished -= OnFinished;

        // A window closed by anything but the view model (Alt+F4, the test harness) must not leave the session running.
        _viewModel.CancelCommand.Execute(null);
        _viewModel.Dispose();
    }

    private PixelPoint DesktopPointOf(MouseEventArgs e)
    {
        var position = e.GetPosition(Stage);
        return new PixelPoint(_screen.X + DisplayUnits.ToPixels(position.X, _scale), _screen.Y + DisplayUnits.ToPixels(position.Y, _scale));
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Stage.CaptureMouse();
        _viewModel.PointerDown(DesktopPointOf(e));
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e) => _viewModel.PointerMoved(DesktopPointOf(e));

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Stage.ReleaseMouseCapture();
        _viewModel.PointerUp(DesktopPointOf(e));
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.CancelCommand.Execute(null);
        e.Handled = true;
    }

    // Brings what is drawn in line with the view model: lit region, frame, labels. All positions go desktop pixel -> stage unit here.
    private void Refresh()
    {
        if (_closed)
        {
            return;
        }

        Geometry lit = Geometry.Empty;
        Geometry? frame = null;
        Rect? labelTarget = null;

        switch (_viewModel.Kind)
        {
            case CaptureKind.Rectangle when _viewModel.SelectionRect is { } selection:
                lit = new RectangleGeometry(StageRect(selection));
                frame = lit;
                labelTarget = StageRect(selection);
                break;
            case CaptureKind.Freeform when _viewModel.Outline.Count >= 2:
                lit = OutlineGeometry(closed: true);
                frame = OutlineGeometry(closed: false);
                break;
            case CaptureKind.Window when _viewModel.HoverFrame is { } hover:
                lit = new RectangleGeometry(StageRect(hover));
                frame = lit;
                break;
        }

        LitImage.Clip = lit;
        SelectionFrame.Data = frame;

        ShowLabel(SizeLabelBox, _viewModel.SizeLabel.Length > 0 && labelTarget is not null);
        if (labelTarget is { } target)
        {
            PlaceBelowOrAbove(SizeLabelBox, target);
        }

        var showTitle = _viewModel.Kind == CaptureKind.Window && _viewModel.HoverFrame is not null && _viewModel.HoverTitle.Length > 0;
        ShowLabel(WindowTitleBox, showTitle);
        if (showTitle && _viewModel.HoverFrame is { } frameOfWindow)
        {
            WindowTitleBox.MaxWidth = Math.Max(120, StageRect(frameOfWindow).Width);
            PlaceAboveOrInside(WindowTitleBox, StageRect(frameOfWindow));
        }

        ShowLabel(MessageBox, _viewModel.HasMessage);
        PlaceHintAndMessage();
    }

    private static void ShowLabel(FrameworkElement label, bool visible) =>
        label.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private Rect StageRect(PixelRect rect) => new(
        DisplayUnits.ToUnits(rect.X - _screen.X, _scale),
        DisplayUnits.ToUnits(rect.Y - _screen.Y, _scale),
        DisplayUnits.ToUnits(rect.Width, _scale),
        DisplayUnits.ToUnits(rect.Height, _scale));

    private Point StagePoint(PixelPoint point) =>
        new(DisplayUnits.ToUnits(point.X - _screen.X, _scale), DisplayUnits.ToUnits(point.Y - _screen.Y, _scale));

    // The outline as the mouse drew it. The lit part closes it (as a release will); the frame shows only the path so far.
    private Geometry OutlineGeometry(bool closed)
    {
        var outline = _viewModel.Outline;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(StagePoint(outline[0]), isFilled: closed, isClosed: closed);
            context.PolyLineTo(outline.Skip(1).Select(StagePoint).ToList(), isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    // Under the region when there is room, else above it, else inside its top edge.
    private void PlaceBelowOrAbove(FrameworkElement label, Rect region)
    {
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = label.DesiredSize;
        var top = region.Bottom + LabelGap;
        if (top + size.Height > Stage.Height)
        {
            top = region.Top - size.Height - LabelGap;
        }

        if (top < 0)
        {
            top = region.Top + LabelGap;
        }

        Canvas.SetLeft(label, Math.Clamp(region.Left, 0, Math.Max(0, Stage.Width - size.Width)));
        Canvas.SetTop(label, top);
    }

    // The window's title sits on top of its frame's upper-left corner.
    private void PlaceAboveOrInside(FrameworkElement label, Rect region)
    {
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = label.DesiredSize;
        var top = region.Top - size.Height - LabelGap;
        if (top < 0)
        {
            top = region.Top + LabelGap;
        }

        Canvas.SetLeft(label, Math.Clamp(region.Left, 0, Math.Max(0, Stage.Width - size.Width)));
        Canvas.SetTop(label, top);
    }

    // The hint sits in the bottom-left corner of the monitor the pointer was on (as in the plan's wireframe) and the message just above
    // it, so neither covers the size label or the title of a window near the top of the screen.
    private void PlaceHintAndMessage()
    {
        var cursor = _viewModel.Snapshot.CursorPosition;
        var here = _viewModel.Snapshot.Monitors
            .Select(m => m.Bounds)
            .Where(b => cursor.X >= b.X && cursor.X < b.X + b.Width && cursor.Y >= b.Y && cursor.Y < b.Y + b.Height)
            .ToList();
        var area = StageRect(here.Count > 0 ? here[0] : _screen);
        const double margin = 24;
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);

        HintBox.Measure(infinite);
        var hintTop = area.Bottom - margin - HintBox.DesiredSize.Height;
        Canvas.SetLeft(HintBox, area.Left + margin);
        Canvas.SetTop(HintBox, hintTop);

        MessageBox.Measure(infinite);
        Canvas.SetLeft(MessageBox, area.Left + margin);
        Canvas.SetTop(MessageBox, hintTop - LabelGap - MessageBox.DesiredSize.Height);
    }
}
