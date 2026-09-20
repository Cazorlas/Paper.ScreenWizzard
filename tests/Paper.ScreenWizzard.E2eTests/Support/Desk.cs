using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NUnit.Framework;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Infrastructure.Capture;
using MonitorInfo = Paper.ScreenWizzard.Domain.Capture.MonitorInfo;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The desktop as the drive tests see it: the monitors, the test window on a chosen monitor, the real mouse and the files the exe wrote.
/// Everything is in physical pixels of the virtual desktop; the test process is per-monitor v2 aware (E2eSetup).
/// </summary>
public static class Desk
{
    static Desk()
    {
        // FlaUI's default speed spends seconds crossing the screen; the overlay needs a real path of moves, not a jump, so the steps stay small.
        Mouse.MovePixelsPerMillisecond = 20;
        Mouse.MovePixelsPerStep = 10;
    }

    public static IReadOnlyList<MonitorInfo> Monitors() => new MonitorCatalog().GetMonitors();

    /// <summary>The monitor with this index (0 is the primary, 1 the next one left to right); the test is ignored when the machine has none.</summary>
    public static MonitorInfo Monitor(int index)
    {
        var all = Monitors();
        if (index >= all.Count)
        {
            Assert.Ignore($"this machine has {all.Count} monitor(s); monitor {index} is not there");
        }

        return all[index];
    }

    public static PixelPoint Cursor() => new MonitorCatalog().GetCursorPosition();

    // ---- the test window ----

    /// <summary>
    /// A test window with the four coloured blocks, 700 x 500 display units, its client area put at the top left of <paramref name="monitor"/>
    /// plus (<paramref name="offsetX"/>, <paramref name="offsetY"/>) physical pixels. The layout is read back: whatever DPI WPF gave the
    /// window is asserted to be the monitor's, so a window drawn at the wrong scale fails here and not in a pixel comparison.
    /// </summary>
    public static ProbeWindow ShowProbeOn(MonitorInfo monitor, string title, int offsetX = 120, int offsetY = 200)
    {
        var probe = ProbeWindow.Show(title, 100, 100, 700, 500, withBlocks: true);
        MoveTo(probe, monitor.Bounds.X + offsetX, monitor.Bounds.Y + offsetY);
        probe.WaitUntilDrawn();
        var scale = StaHost.Instance.Invoke(() => VisualTreeHelper.GetDpi(probe.Window).DpiScaleX);
        Assert.That(scale * 96.0, Is.EqualTo((double)monitor.Dpi).Within(1), $"the test window is drawn at the DPI of monitor {monitor.Index}");
        var client = probe.ClientInPixels();
        Assert.That(
            client.X >= monitor.Bounds.X && client.Y >= monitor.Bounds.Y
            && client.X + client.Width <= monitor.Bounds.X + monitor.Bounds.Width && client.Y + client.Height <= monitor.Bounds.Y + monitor.Bounds.Height,
            Is.True,
            $"the test window's client area {client} lies inside monitor {monitor.Index} {monitor.Bounds}");
        return probe;
    }

    public static void MoveTo(ProbeWindow probe, int x, int y)
    {
        DriveNative.SetWindowPos(probe.Handle, IntPtr.Zero, x, y, 0, 0, DriveNative.SwpNoSize | DriveNative.SwpNoZOrder | DriveNative.SwpNoActivate);
        StaHost.Instance.Settle();
        Thread.Sleep(300);
        StaHost.Instance.Settle();
    }

    /// <summary>The visible frame (DWM extended frame bounds), read by the test itself with the same call the app's window catalog documents.</summary>
    public static PixelRect VisibleFrame(ProbeWindow probe)
    {
        var hr = DriveNative.DwmGetWindowAttribute(probe.Handle, DriveNative.DwmExtendedFrameBounds, out var rect, System.Runtime.InteropServices.Marshal.SizeOf<Native.Rect>());
        Assert.That(hr, Is.Zero, "DwmGetWindowAttribute(EXTENDED_FRAME_BOUNDS)");
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static PixelRect WindowRect(ProbeWindow probe)
    {
        Native.GetWindowRect(probe.Handle, out var rect);
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>Repaints one block of the test window (0 red, 1 green, 2 blue, 3 black) in a new colour.</summary>
    public static void RepaintBlock(ProbeWindow probe, int index, byte r, byte g, byte b) => StaHost.Instance.Invoke(() =>
    {
        var canvas = (Canvas)probe.Window.Content;
        ((System.Windows.Shapes.Rectangle)canvas.Children[index]).Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
    });

    // ---- the real mouse ----

    public static void PointerTo(int x, int y) => Mouse.Position = new System.Drawing.Point(x, y);

    /// <summary>Press at <paramref name="from"/>, move along a straight line in small steps, release at <paramref name="to"/>. Every point must lie over a window of the test.</summary>
    public static void Drag(PixelPoint from, PixelPoint to, int steps = 14)
    {
        PointerTo(from.X, from.Y);
        Thread.Sleep(120);
        Mouse.Down(MouseButton.Left);
        Thread.Sleep(80);
        for (var i = 1; i <= steps; i++)
        {
            PointerTo(from.X + ((to.X - from.X) * i / steps), from.Y + ((to.Y - from.Y) * i / steps));
            Thread.Sleep(15);
        }

        Thread.Sleep(100);
        Mouse.Up(MouseButton.Left);
        Thread.Sleep(100);
    }

    /// <summary>Press at the first point, follow the others, release at the last.</summary>
    public static void DragAlong(IReadOnlyList<PixelPoint> path, int stepsPerLeg = 8)
    {
        PointerTo(path[0].X, path[0].Y);
        Thread.Sleep(120);
        Mouse.Down(MouseButton.Left);
        Thread.Sleep(80);
        for (var leg = 1; leg < path.Count; leg++)
        {
            var a = path[leg - 1];
            var b = path[leg];
            for (var i = 1; i <= stepsPerLeg; i++)
            {
                PointerTo(a.X + ((b.X - a.X) * i / stepsPerLeg), a.Y + ((b.Y - a.Y) * i / stepsPerLeg));
                Thread.Sleep(15);
            }
        }

        Thread.Sleep(100);
        Mouse.Up(MouseButton.Left);
        Thread.Sleep(100);
    }

    public static void ClickAt(int x, int y)
    {
        PointerTo(x, y);
        Thread.Sleep(100);
        Mouse.Click(new System.Drawing.Point(x, y));
        Thread.Sleep(100);
    }

    public static void RightClickAt(int x, int y)
    {
        PointerTo(x, y);
        Thread.Sleep(100);
        Mouse.RightClick(new System.Drawing.Point(x, y));
        Thread.Sleep(100);
    }

    public static void Type(VirtualKeyShort key) => Keyboard.Type(key);

    public static void Chord(params VirtualKeyShort[] keys) => Keyboard.TypeSimultaneously(keys);

    // ---- files and pixels ----

    /// <summary>Decodes a PNG or JPG from disk into straight-alpha BGRA, the same shape the app uses.</summary>
    public static PixelImage ReadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return ToPixels(decoder.Frames[0]);
    }

    public static PixelImage ToPixels(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
        return new PixelImage(converted.PixelWidth, converted.PixelHeight, bytes);
    }

    /// <summary>The width and height of a PNG read from its header only (IHDR), so a full-screen file is never decoded.</summary>
    public static (int Width, int Height) PngSize(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[24];
        var read = stream.Read(header, 0, header.Length);
        Assert.That(read, Is.EqualTo(24), "a PNG header");
        Assert.That(header[1], Is.EqualTo((byte)'P'), "PNG signature");
        return (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
    }

    public static RgbaColor At(PixelImage image, int x, int y) => Pixels.At(image, 0, 0, x, y);

    /// <summary>The largest per-channel distance of a pixel from a colour.</summary>
    public static int Distance(RgbaColor c, byte r, byte g, byte b) => Math.Max(Math.Abs(c.R - r), Math.Max(Math.Abs(c.G - g), Math.Abs(c.B - b)));

    /// <summary>The clipboard's image as pixels, or null when it holds none. Runs on the STA thread of the tests.</summary>
    public static PixelImage? ClipboardImage() => StaHost.Instance.Invoke(() =>
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return Clipboard.ContainsImage() && Clipboard.GetImage() is { } source ? ToPixels(source) : null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(100);
            }
        }

        return null;
    });

    public static string? ClipboardText() => StaHost.Instance.Invoke(() =>
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(100);
            }
        }

        return null;
    });

    public static void SetClipboardText(string text) => StaHost.Instance.Invoke(() => Clipboard.SetDataObject(text, true));

    public static void ClearClipboard() => StaHost.Instance.Invoke(() =>
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Clipboard.Clear();
                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(100);
            }
        }
    });

    /// <summary>The screenshot files of the drive lane: a git-ignored folder inside the test project.</summary>
    public static string ShotsFolder { get; } = ResolveShots();

    public static string ShotPath(string name) => System.IO.Path.Combine(ShotsFolder, name + ".png");

    private static string ResolveShots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Paper.ScreenWizzard.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;
        var folder = System.IO.Path.Combine(root, "tests", "Paper.ScreenWizzard.E2eTests", "shots");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>The repository root (the folder that holds the solution).</summary>
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Paper.ScreenWizzard.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("the solution folder was not found above " + AppContext.BaseDirectory);
    }
}
