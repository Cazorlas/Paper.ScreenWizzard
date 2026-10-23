using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.Presentation.Shared.Views;

namespace Paper.ScreenWizzard.Presentation.Recorder.Views;

/// <summary>
/// The red dashed outline just outside the recorded area, with the countdown in its middle (SPEC recorder, "What the user does" 3).
/// The mouse goes through it to the programs below, and it is never in the video (<see cref="CaptureExclusion"/>).
/// </summary>
public sealed class RecordingOutlineWindow : Window, IRecordingOutline
{
    private const int OutlineGap = 3;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    private readonly TextBlock _number;
    private readonly Border _numberPlate;
    private bool _closed;

    public RecordingOutlineWindow(PixelRect area)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        AutomationProperties.SetAutomationId(this, "RecordingOutline");

        var frame = new Rectangle { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
        frame.SetResourceReference(Shape.StrokeProperty, "Brush.Recording");
        _number = new TextBlock { FontSize = 72, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        _number.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ToastText");
        _numberPlate = new Border
        {
            Child = _number,
            Padding = new Thickness(28, 4, 28, 8),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        _numberPlate.SetResourceReference(Border.BackgroundProperty, "Brush.ToastBackground");
        AutomationProperties.SetAutomationId(_number, "RecordingCountdown");

        var grid = new Grid();
        grid.Children.Add(frame);
        grid.Children.Add(_numberPlate);
        Content = grid;

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExTransparent | WsExToolWindow | WsExNoActivate);
            CaptureExclusion.Apply(this);
            PhysicalWindowPlacer.Place(this, new PixelRect(area.X - OutlineGap, area.Y - OutlineGap, area.Width + (2 * OutlineGap), area.Height + (2 * OutlineGap)));
        };
        Closed += (_, _) =>
        {
            _closed = true;
            ClosedByAnyone?.Invoke(this, EventArgs.Empty);
        };
    }

    event EventHandler? IViewHandle.Closed
    {
        add => ClosedByAnyone += value;
        remove => ClosedByAnyone -= value;
    }

    private event EventHandler? ClosedByAnyone;

    public void ShowCountdown(int? secondsLeft)
    {
        _number.Text = secondsLeft?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        _numberPlate.Visibility = secondsLeft is > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void IViewHandle.Close()
    {
        if (!_closed)
        {
            Close();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
