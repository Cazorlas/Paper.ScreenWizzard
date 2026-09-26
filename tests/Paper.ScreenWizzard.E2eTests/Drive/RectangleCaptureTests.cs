using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>
/// Plan T18, flow 2 and 3: the real exe, the real hotkey, a real mouse drag over a test window whose block colours and places are known, then
/// the "Đã chụp" dialog and the file it writes. Run on both monitors: the secondary one of this machine is 200%, so the same 300 x 200
/// physical pixel drag covers a window drawn twice as big in device pixels (SPEC capture "đúng pixel thật").
/// </summary>
[TestFixture]
public sealed class RectangleCaptureTests : DriveBase
{
    [TestCase(0, 300, 200, 10, 20)]
    [TestCase(1, 300, 200, 10, 20)]
    [TestCase(0, 301, 199, 11, 21)]
    [TestCase(1, 301, 199, 11, 21)]
    public void Rectangle_HotkeyThenDrag_DialogSaysTheSize_AndSaveWritesThoseExactPixels(int monitorIndex, int width, int height, int left, int top)
    {
        var monitor = Desk.Monitor(monitorIndex);
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(monitor, "rect");
        var (from, to) = DragInside(probe, left, top, width, height);
        var inside = BlocksInside(probe, from, width, height);
        Assert.That(inside.Count, Is.GreaterThanOrEqualTo(2), "set-up: the drag covers at least two block centres");

        // Overlay: over every monitor, one window that is the whole virtual desktop.
        var overlay = OpenOverlay(CaptureKind.Rectangle);
        var overlayBounds = AppRun.BoundsOf(overlay);
        var desktop = Desk.Monitors();
        var deskLeft = desktop.Min(m => m.Bounds.X);
        var deskTop = desktop.Min(m => m.Bounds.Y);
        var deskRight = desktop.Max(m => m.Bounds.X + m.Bounds.Width);
        var deskBottom = desktop.Max(m => m.Bounds.Y + m.Bounds.Height);
        Assert.That(overlayBounds, Is.EqualTo(new PixelRect(deskLeft, deskTop, deskRight - deskLeft, deskBottom - deskTop)), "the overlay covers the virtual desktop of every monitor");

        // Mid-drag, before the release: the size label reads what the drag is so far.
        Desk.PointerTo(from.X, from.Y);
        Thread.Sleep(120);
        FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left);
        Thread.Sleep(100);
        for (var i = 1; i <= 14; i++)
        {
            Desk.PointerTo(from.X + ((to.X - from.X) * i / 14), from.Y + ((to.Y - from.Y) * i / 14));
            Thread.Sleep(15);
        }

        Thread.Sleep(250);
        var label = AppRun.TextOf(overlay, "SizeLabel");
        var midShot = AppRun.ShotOf(Union(from, to, probe), ShotName("rect-overlay-middrag", $"m{monitorIndex}-{width}x{height}"));
        FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left);
        Assert.That(label, Is.EqualTo($"{width} × {height}"), "the size label on the overlay while dragging (see " + midShot + ")");

        var dialog = App.WaitWindow("CaptureDoneWindow");
        Assert.That(App.WaitWindowGone("SelectionOverlay"), Is.True, "the overlay is gone once the mouse is released");
        var size = AppRun.TextOf(dialog, "SizeText");
        Assert.That(size, Is.EqualTo($"{width} × {height}"), "the dialog shows the size");
        Assert.That(AppRun.TextOf(dialog, "FormatText"), Is.EqualTo("PNG"));
        Assert.That(App.SavedFiles(), Is.Empty, "before a choice nothing is written");
        AppRun.Shot(dialog, ShotName("done", $"m{monitorIndex}-{width}x{height}"));

        AppRun.Invoke(dialog, "SaveButton");
        var path = App.WaitForSavedFile();
        Assert.That(App.WaitWindowGone("CaptureDoneWindow"), Is.True, "Lưu closes the dialog");
        Assert.That(App.SavedFiles(), Has.Count.EqualTo(1), "exactly one file");

        var image = Desk.ReadImage(path);
        Assert.That((image.Width, image.Height), Is.EqualTo((width, height)), $"file size; monitor {monitorIndex} is {monitor.Dpi} dpi");
        AssertBlockPixels(image, inside, monitorIndex);
        AssertOnlyTestWindowColours(image);
    }

    internal static void AssertBlockPixels(PixelImage image, IReadOnlyList<(BlockPlace Block, int X, int Y)> inside, int monitorIndex)
    {
        foreach (var (block, x, y) in inside)
        {
            var actual = Desk.At(image, x, y);
            Assert.That(
                Desk.Distance(actual, block.R, block.G, block.B),
                Is.LessThanOrEqualTo(2),
                $"monitor {monitorIndex}: the {block.Name} block centre at ({x},{y}) of the file is {Pixels.Describe(actual)}, the window draws rgb({block.R},{block.G},{block.B})");
            Assert.That(actual.A, Is.EqualTo(255), "opaque");
        }
    }

    /// <summary>Every pixel of a capture of the test window is white or one of the four block colours: no dim, no frame, no label, no countdown.</summary>
    internal static void AssertOnlyTestWindowColours(PixelImage image)
    {
        (byte R, byte G, byte B)[] allowed = [(255, 255, 255), (230, 25, 25), (20, 200, 60), (25, 60, 230), (10, 10, 10)];
        var foreign = 0;
        string first = string.Empty;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var c = Desk.At(image, x, y);
                if (!allowed.Any(a => Desk.Distance(c, a.R, a.G, a.B) <= 3))
                {
                    if (foreign++ == 0)
                    {
                        first = $"({x},{y}) {Pixels.Describe(c)}";
                    }
                }
            }
        }

        Assert.That(foreign, Is.Zero, "pixels that are neither white nor a block colour (overlay dim, frame, label?), first: " + first);
    }

    // The rectangle that holds the drag and the test window's frame, so a picture of it holds nothing of the developer's other windows.
    private static PixelRect Union(PixelPoint from, PixelPoint to, ProbeWindow probe)
    {
        var frame = Desk.VisibleFrame(probe);
        var x0 = Math.Min(frame.X, Math.Min(from.X, to.X));
        var y0 = Math.Min(frame.Y, Math.Min(from.Y, to.Y));
        var x1 = Math.Max(frame.X + frame.Width, Math.Max(from.X, to.X));
        var y1 = Math.Max(frame.Y + frame.Height, Math.Max(from.Y, to.Y));
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }
}
