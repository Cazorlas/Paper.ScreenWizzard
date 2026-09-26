using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>Plan T18, flow 4: freeform (transparent outside the outline), window (the visible frame, highlighted before the click) and full screen.</summary>
[TestFixture]
public sealed class CaptureKindsTests : DriveBase
{
    [TestCase(0, "Png")]
    [TestCase(1, "Png")]
    [TestCase(0, "Jpg")]
    public void Freeform_Triangle_OutsideIsTransparentInsideIsSolid(int monitorIndex, string format)
    {
        var monitor = Desk.Monitor(monitorIndex);
        var jpg = format == "Jpg";
        NewApp(s => s with { Format = jpg ? ImageFormat.Jpg : ImageFormat.Png });
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "freeform");
        var client = probe.ClientInPixels();

        // The SPEC triangle (100,100) (300,100) (200,300), moved over the white part of the test window (below the blocks on both scales).
        var a = new PixelPoint(client.X + 100, client.Y + 200);
        var b = new PixelPoint(a.X + 200, a.Y);
        var c = new PixelPoint(a.X + 100, a.Y + 200);
        OpenOverlay(CaptureKind.Freeform);
        Desk.DragAlong([a, b, c, a], 8);

        var dialog = App.WaitWindow("CaptureDoneWindow");
        var size = AppRun.TextOf(dialog, "SizeText");
        AppRun.Invoke(dialog, "SaveButton");
        var path = App.WaitForSavedFile(5, jpg ? ".jpg" : ".png");
        TestContext.Out.WriteLine($"freeform monitor {monitorIndex} {format}: dialog says {size}");
        var image = Desk.ReadImage(path);

        Assert.That(size, Is.EqualTo("201 × 201").Or.EqualTo("200 × 200"), "the dialog shows the bounding box of the outline");
        Assert.That((image.Width, image.Height), Is.EqualTo((200, 200)).Or.EqualTo((201, 201)), "the file is the bounding box of the outline (SPEC: 200 x 200)");
        var origin = (X: Math.Min(a.X, Math.Min(b.X, c.X)), Y: Math.Min(a.Y, Math.Min(b.Y, c.Y)));

        // (200,150) of the SPEC is 100 right and 50 below the box corner: inside. (110,290) is 10 right and 190 below it: outside.
        var inside = Desk.At(image, 100, 50);
        var outside = Desk.At(image, 10, 190);
        if (jpg)
        {
            Assert.That(Desk.Distance(outside, 255, 255, 255), Is.LessThanOrEqualTo(8), "SPEC: a freeform saved as JPG is white outside the outline, not black: " + Pixels.Describe(outside));
            Assert.That(Desk.Distance(inside, 255, 255, 255), Is.LessThanOrEqualTo(8), "inside is the white test window: " + Pixels.Describe(inside));
        }
        else
        {
            Assert.That(inside.A, Is.EqualTo(255), "inside the outline is opaque: " + Pixels.Describe(inside));
            Assert.That(Desk.Distance(inside, 255, 255, 255), Is.LessThanOrEqualTo(2), "and it is the pixel of the test window");
            Assert.That(outside.A, Is.EqualTo(0), "outside the outline is transparent: " + Pixels.Describe(outside));
        }

        _ = origin;
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Window_HoverHighlightsTheWindow_ClickCapturesItsVisibleFrame_WithoutTheShadow(int monitorIndex)
    {
        var monitor = Desk.Monitor(monitorIndex);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "window");
        var title = StaHost.Instance.Invoke(() => probe.Window.Title);
        var frame = Desk.VisibleFrame(probe);
        var rect = Desk.WindowRect(probe);
        var centre = new PixelPoint(frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2));

        var overlay = OpenOverlay(CaptureKind.Window);
        Desk.PointerTo(centre.X - 60, centre.Y - 40);
        Thread.Sleep(150);
        Desk.PointerTo(centre.X, centre.Y);
        Thread.Sleep(500);

        // Before the click: the overlay names the window under the pointer and frames exactly its visible frame.
        var hoverTitle = AppRun.TextOf(overlay, "WindowTitleLabel");
        Assert.That(hoverTitle, Does.Contain(title), "the overlay shows the name of the window under the pointer");
        var shot = AppRun.ShotOf(new PixelRect(frame.X - 4, frame.Y - 4, frame.Width + 8, frame.Height + 8), ShotName("window-hover", $"m{monitorIndex}"));
        var picture = Desk.ReadImage(shot);
        var midY = 4 + (frame.Height / 2);
        var midX = 4 + (frame.Width / 2);
        var lit = Desk.At(picture, 4 + 20, midY);
        Assert.That(Desk.Distance(lit, 255, 255, 255), Is.LessThanOrEqualTo(3), "inside the frame the window is lit, not dimmed: " + Pixels.Describe(lit) + " (see " + shot + ")");
        foreach (var (name, x, y) in new[] { ("left", 4 - 3, midY), ("right", 4 + frame.Width + 2, midY), ("above", midX, 4 - 3), ("below", midX, 4 + frame.Height + 2) })
        {
            var dim = Desk.At(picture, x, y);
            Assert.That(Math.Max(dim.R, Math.Max(dim.G, dim.B)), Is.LessThanOrEqualTo(165), $"just outside the {name} edge of the visible frame the screen is dimmed (frame {frame}): " + Pixels.Describe(dim) + " (see " + shot + ")");
        }

        Desk.ClickAt(centre.X, centre.Y);
        var dialog = App.WaitWindow("CaptureDoneWindow");
        var size = AppRun.TextOf(dialog, "SizeText");
        Assert.That(frame.Width < rect.Width || frame.Height < rect.Height, Is.True, $"set-up: the window has an invisible border (rect {rect} vs frame {frame})");
        Assert.That(size, Is.EqualTo($"{frame.Width} × {frame.Height}"), $"the image is the visible frame, not the window rectangle {rect.Width} x {rect.Height}");
        AppRun.Invoke(dialog, "SaveButton");
        var image = Desk.ReadImage(App.WaitForSavedFile());
        Assert.That((image.Width, image.Height), Is.EqualTo((frame.Width, frame.Height)), "the file has the size of the visible frame");

        // Block centres, relative to the frame's corner.
        foreach (var block in probe.Blocks())
        {
            var x = block.CentreX - frame.X;
            var y = block.CentreY - frame.Y;
            var actual = Desk.At(image, x, y);
            Assert.That(Desk.Distance(actual, block.R, block.G, block.B), Is.LessThanOrEqualTo(2), $"{block.Name} block centre ({x},{y}) in the window image is {Pixels.Describe(actual)}");
        }
    }

    [TestCase(0, "MonitorUnderCursor")]
    [TestCase(1, "MonitorUnderCursor")]
    [TestCase(0, "AllMonitors")]
    public void FullScreen_ImageIsTheMonitorUnderThePointer_OrAllMonitors_AndTheFileIsDeletedAtOnce(int monitorIndex, string scope)
    {
        var monitor = Desk.Monitor(monitorIndex);
        var all = scope == "AllMonitors";
        NewApp(s => s with { FullScreenScope = all ? FullScreenScope.AllMonitors : FullScreenScope.MonitorUnderCursor });
        StartAndWaitForBar();
        Desk.PointerTo(monitor.Bounds.X + (monitor.Bounds.Width / 2), monitor.Bounds.Y + (monitor.Bounds.Height / 2));
        Thread.Sleep(200);

        AppRun.PressHotkey(CaptureKind.FullScreen);
        var dialog = App.WaitWindow("CaptureDoneWindow", 10);
        Assert.That(App.FindWindow("SelectionOverlay"), Is.Null, "full screen has nothing to choose: no overlay");
        var size = AppRun.TextOf(dialog, "SizeText");
        var desktop = Desk.Monitors();
        var expected = all
            ? (Width: desktop.Max(m => m.Bounds.X + m.Bounds.Width) - desktop.Min(m => m.Bounds.X), Height: desktop.Max(m => m.Bounds.Y + m.Bounds.Height) - desktop.Min(m => m.Bounds.Y))
            : (monitor.Bounds.Width, monitor.Bounds.Height);
        Assert.That(size, Is.EqualTo($"{expected.Item1} × {expected.Item2}"), $"the dialog shows the size of {(all ? "the whole desktop" : $"monitor {monitorIndex}")}");

        // The one full-screen file of the drive tests: only its header is read, and it is deleted at once.
        AppRun.Invoke(dialog, "SaveButton");
        var path = App.WaitForSavedFile();
        try
        {
            var (w, h) = Desk.PngSize(path);
            Assert.That((w, h), Is.EqualTo((expected.Item1, expected.Item2)), "the file's size");
        }
        finally
        {
            File.Delete(path);
        }

        Assert.That(File.Exists(path), Is.False, "the full-screen file is deleted");
    }
}
