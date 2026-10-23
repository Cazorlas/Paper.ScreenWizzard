using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;

namespace Paper.ScreenWizzard.Presentation.Recorder.Views;

/// <summary>
/// A light veil over the whole live desktop to pick what to record (SPEC recorder, "What the user does" 2): drag a region, or click the
/// window the pointer is on, which is outlined as the pointer moves. Esc or the right button gives up. Positions are read from the
/// pointer in physical pixels (<c>GetCursorPos</c>), so the answer is right on every monitor whatever its scale; the drawing only shows
/// it. The veil is never in a picture (<see cref="CaptureExclusion"/>).
/// </summary>
public sealed class AreaPickerWindow : Window
{
    private readonly PixelRect _desktop;
    private readonly Func<PixelPoint, PickedWindow?>? _windowAt;
    private readonly Rectangle _mark;
    private readonly TaskCompletionSource<(PixelRect Area, long? Window)?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private PixelPoint? _dragFrom;
    private PickedWindow? _hovered;

    private AreaPickerWindow(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?>? windowAt)
    {
        _windowAt = windowAt;
        _desktop = BoundsOf(monitors);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Cursor = windowAt is null ? Cursors.Cross : Cursors.Hand;
        AutomationProperties.SetAutomationId(this, "AreaPicker");

        // Nearly clear, but not fully: a window with a fully clear background lets the clicks through.
        var veil = new Rectangle { Opacity = 0.25 };
        veil.SetResourceReference(Shape.FillProperty, "Brush.OverlayDim");
        _mark = new Rectangle { StrokeThickness = 2, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _mark.SetResourceReference(Shape.StrokeProperty, "Brush.Recording");
        var canvas = new Canvas { Background = Brushes.Transparent };
        canvas.Children.Add(_mark);
        var grid = new Grid();
        grid.Children.Add(veil);
        grid.Children.Add(canvas);
        Content = grid;

        SourceInitialized += (_, _) =>
        {
            CaptureExclusion.Apply(this);
            PhysicalWindowPlacer.Place(this, _desktop);
        };
        Closed += (_, _) => _answer.TrySetResult(null);
    }

    /// <summary>A dragged region; null when the user gave up.</summary>
    public static async Task<PixelRect?> PickRegionAsync(IReadOnlyList<MonitorInfo> monitors)
    {
        var answer = await Open(monitors, null);
        return answer?.Area;
    }

    /// <summary>A clicked window; null when the user gave up or clicked where there is none.</summary>
    public static async Task<PickedWindow?> PickWindowAsync(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?> windowAt)
    {
        var answer = await Open(monitors, windowAt);
        return answer is { Window: { } handle } picked ? new PickedWindow(picked.Area, handle) : null;
    }

    public static PixelPoint Pointer() => GetCursorPos(out var point) ? new PixelPoint(point.X, point.Y) : default;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        Close();
        base.OnMouseRightButtonDown(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_windowAt is null)
        {
            _dragFrom = Pointer();
            CaptureMouse();
        }
        else
        {
            Answer(_hovered is { } window ? (window.Frame, window.Handle) : null);
        }

        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pointer = Pointer();
        if (_windowAt is not null)
        {
            _hovered = _windowAt(pointer);
            Mark(_hovered?.Frame);
        }
        else if (_dragFrom is { } from)
        {
            Mark(Normalise(from, pointer));
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_windowAt is null && _dragFrom is { } from)
        {
            ReleaseMouseCapture();
            Answer((Normalise(from, Pointer()), null));
        }

        base.OnMouseLeftButtonUp(e);
    }

    private static Task<(PixelRect Area, long? Window)?> Open(IReadOnlyList<MonitorInfo> monitors, Func<PixelPoint, PickedWindow?>? windowAt)
    {
        var window = new AreaPickerWindow(monitors, windowAt);
        window.Show();
        window.Activate();
        return window._answer.Task;
    }

    private void Answer((PixelRect Area, long? Window)? answer)
    {
        _answer.TrySetResult(answer);
        Close();
    }

    // Shows a physical rectangle on the veil, in the window's own units.
    private void Mark(PixelRect? area)
    {
        if (area is not { } rect)
        {
            _mark.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = VisualTreeHelper.GetDpi(this);
        Canvas.SetLeft(_mark, (rect.X - _desktop.X) / scale.DpiScaleX);
        Canvas.SetTop(_mark, (rect.Y - _desktop.Y) / scale.DpiScaleY);
        _mark.Width = rect.Width / scale.DpiScaleX;
        _mark.Height = rect.Height / scale.DpiScaleY;
        _mark.Visibility = Visibility.Visible;
    }

    private static PixelRect Normalise(PixelPoint from, PixelPoint to) =>
        new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));

    private static PixelRect BoundsOf(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return new PixelRect(0, 0, 1, 1);
        }

        var left = monitors.Min(m => m.Bounds.X);
        var top = monitors.Min(m => m.Bounds.Y);
        var right = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        var bottom = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
