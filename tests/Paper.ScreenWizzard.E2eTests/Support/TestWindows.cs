using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>One coloured block of the test window and where it really is, in physical pixels of the desktop.</summary>
public sealed record BlockPlace(string Name, byte R, byte G, byte B, int Left, int Top, int Width, int Height)
{
    public int CentreX => Left + (Width / 2);

    public int CentreY => Top + (Height / 2);
}

/// <summary>
/// A plain WPF window on the desktop the tests can look for: an always-on-top window of a known title with four solid blocks at known
/// places on a white surface. It is built and used on the <see cref="StaHost"/> thread only.
/// </summary>
public sealed class ProbeWindow
{
    private static readonly (string Name, byte R, byte G, byte B)[] _colours =
    [
        ("Red", 230, 25, 25),
        ("Green", 20, 200, 60),
        ("Blue", 25, 60, 230),
        ("Black", 10, 10, 10),
    ];

    private readonly Window _window;
    private readonly List<Rectangle> _blocks = [];

    private ProbeWindow(Window window)
    {
        _window = window;
    }

    public Window Window => _window;

    public IntPtr Handle => StaHost.Instance.Invoke(() => new System.Windows.Interop.WindowInteropHelper(_window).Handle);

    /// <summary>Creates and shows the window with <paramref name="title"/> at (left, top) in display units, sized in display units.</summary>
    public static ProbeWindow Show(string title, double left, double top, double width, double height, bool withBlocks, bool topmost = true)
    {
        var host = StaHost.Instance;
        return host.Invoke(() =>
        {
            var canvas = new Canvas { Background = Brushes.White };
            var window = new Window
            {
                Title = title,
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                Topmost = topmost,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Content = canvas,
            };
            var probe = new ProbeWindow(window);
            if (withBlocks)
            {
                // Blocks are 60 x 40 display units, in a row 40 units from the top of the client area.
                for (var i = 0; i < _colours.Length; i++)
                {
                    var (_, r, g, b) = _colours[i];
                    var block = new Rectangle
                    {
                        Width = 60,
                        Height = 40,
                        Fill = new SolidColorBrush(Color.FromRgb(r, g, b)),
                        SnapsToDevicePixels = true,
                    };
                    Canvas.SetLeft(block, 20 + (i * 80));
                    Canvas.SetTop(block, 40);
                    canvas.Children.Add(block);
                    probe._blocks.Add(block);
                }
            }

            window.Show();
            window.Activate();
            return probe;
        });
    }

    /// <summary>Waits until the window has been drawn and given to the desktop compositor.</summary>
    public void WaitUntilDrawn()
    {
        StaHost.Instance.Settle();
        Thread.Sleep(400);
        StaHost.Instance.Settle();
    }

    /// <summary>Where each block is, in physical pixels: WPF answers <c>PointToScreen</c> in device pixels because the process is per-monitor aware.</summary>
    public IReadOnlyList<BlockPlace> Blocks() => StaHost.Instance.Invoke(() =>
    {
        var places = new List<BlockPlace>();
        var scale = VisualTreeHelper.GetDpi(_window);
        for (var i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            var corner = block.PointToScreen(new System.Windows.Point(0, 0));
            var (name, r, g, b) = _colours[i];
            places.Add(new BlockPlace(
                name,
                r,
                g,
                b,
                (int)Math.Round(corner.X),
                (int)Math.Round(corner.Y),
                (int)Math.Round(block.ActualWidth * scale.DpiScaleX),
                (int)Math.Round(block.ActualHeight * scale.DpiScaleY)));
        }

        return places;
    });

    /// <summary>The window's own size in physical pixels (display units times the scale of its monitor).</summary>
    public (int Width, int Height) SizeInPixels() => StaHost.Instance.Invoke(() =>
    {
        var scale = VisualTreeHelper.GetDpi(_window);
        return ((int)Math.Round(_window.ActualWidth * scale.DpiScaleX), (int)Math.Round(_window.ActualHeight * scale.DpiScaleY));
    });

    /// <summary>The client area (the white surface) in physical pixels of the desktop.</summary>
    public Paper.ScreenWizzard.Domain.Geometry.PixelRect ClientInPixels() => StaHost.Instance.Invoke(() =>
    {
        var content = (FrameworkElement)_window.Content;
        var scale = VisualTreeHelper.GetDpi(_window);
        var corner = content.PointToScreen(new System.Windows.Point(0, 0));
        return new Paper.ScreenWizzard.Domain.Geometry.PixelRect(
            (int)Math.Round(corner.X),
            (int)Math.Round(corner.Y),
            (int)Math.Round(content.ActualWidth * scale.DpiScaleX),
            (int)Math.Round(content.ActualHeight * scale.DpiScaleY));
    });

    public void Activate() => StaHost.Instance.Invoke(() =>
    {
        _window.Activate();
        Native.SetForegroundWindow(Handle);
    });

    public void Minimise() => StaHost.Instance.Invoke(() => _window.WindowState = WindowState.Minimized);

    public void Close() => StaHost.Instance.Invoke(() => _window.Close());
}
