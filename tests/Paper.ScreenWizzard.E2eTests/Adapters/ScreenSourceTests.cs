using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Capture;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>
/// The real GDI capture on the real desktop. A WPF window of four solid blocks sits on the primary monitor; the capture of the
/// virtual screen must hold each block, in exact colour, at the pixel WPF says it is. A capture is only ever held in memory: the
/// desktop can show private work, so no test writes one to disk.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ScreenSourceTests
{
    private ProbeWindow? _probe;
    private PixelPoint _cursorBefore;

    [SetUp]
    public void RememberCursor()
    {
        Native.GetCursorPos(out var point);
        _cursorBefore = new PixelPoint(point.X, point.Y);
    }

    [TearDown]
    public void CloseProbe()
    {
        _probe?.Close();
        _probe = null;
        Native.SetCursorPos(_cursorBefore.X, _cursorBefore.Y);
    }

    private static PixelRect VirtualScreen() => new(
        Native.GetSystemMetrics(Native.SmXVirtualScreen),
        Native.GetSystemMetrics(Native.SmYVirtualScreen),
        Native.GetSystemMetrics(Native.SmCxVirtualScreen),
        Native.GetSystemMetrics(Native.SmCyVirtualScreen));

    // A window is on the desktop a moment after Show returns; ask again (one pixel at a time) until the first block shows, so a slow
    // compositor is not a failure. The adapter does the looking: the check is the same call the real capture makes.
    private static void WaitUntilFirstBlockIsOnTheDesktop(ScreenSource source, ProbeWindow probe)
    {
        var first = probe.Blocks()[0];
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var result = source.Capture(new PixelRect(first.CentreX, first.CentreY, 1, 1), false);
            if (result.Image is { } pixel && pixel.Bgra[2] == first.R && pixel.Bgra[1] == first.G && pixel.Bgra[0] == first.B)
            {
                return;
            }

            Thread.Sleep(200);
        }
    }

    [Test]
    public void VirtualScreen_HoldsEveryBlockOfTheProbeWindowInExactColourAndPlace()
    {
        _probe = ProbeWindow.Show("E2E probe " + Guid.NewGuid().ToString("N"), 200, 200, 400, 300, withBlocks: true);
        _probe.WaitUntilDrawn();
        var area = VirtualScreen();

        var source = new ScreenSource();
        WaitUntilFirstBlockIsOnTheDesktop(source, _probe);
        var result = source.Capture(area, includeCursor: false);
        Assert.That(result.Issue, Is.EqualTo(ScreenCaptureIssue.None), "the capture itself failed");
        Assert.That(result.Image, Is.Not.Null, "no image came back");
        var image = result.Image!;

        Assert.That(image.Width, Is.EqualTo(area.Width), "capture width is the virtual screen width in physical pixels");
        Assert.That(image.Height, Is.EqualTo(area.Height));
        Assert.That(image.Bgra.Length, Is.EqualTo(area.Width * area.Height * 4));
        foreach (var block in _probe.Blocks())
        {
            var centre = Pixels.At(image, area.X, area.Y, block.CentreX, block.CentreY);
            Assert.That(
                (centre.R, centre.G, centre.B, centre.A),
                Is.EqualTo((block.R, block.G, block.B, (byte)255)),
                $"{block.Name} block centre ({block.CentreX},{block.CentreY}): {Pixels.Describe(centre)}");

            // The block edge is exact to the pixel: inside the left edge is the block, one pixel outside it is the white surface.
            var inside = Pixels.At(image, area.X, area.Y, block.Left, block.CentreY);
            var outside = Pixels.At(image, area.X, area.Y, block.Left - 1, block.CentreY);
            Assert.That((inside.R, inside.G, inside.B), Is.EqualTo((block.R, block.G, block.B)), $"{block.Name} left edge, inside: {Pixels.Describe(inside)}");
            Assert.That((outside.R, outside.G, outside.B), Is.EqualTo(((byte)255, (byte)255, (byte)255)), $"{block.Name} left edge, one pixel out: {Pixels.Describe(outside)}");
            var above = Pixels.At(image, area.X, area.Y, block.CentreX, block.Top - 1);
            Assert.That((above.R, above.G, above.B), Is.EqualTo(((byte)255, (byte)255, (byte)255)), $"{block.Name} top edge, one pixel out: {Pixels.Describe(above)}");
        }
    }

    [Test]
    public void SubArea_ReturnsExactlyThatArea()
    {
        _probe = ProbeWindow.Show("E2E probe " + Guid.NewGuid().ToString("N"), 200, 200, 400, 300, withBlocks: true);
        _probe.WaitUntilDrawn();
        var blocks = _probe.Blocks();
        var blue = blocks[2];

        // 20 x 20 pixels inside the blue block, offset from its corner so a wrong origin lands on a different colour.
        var area = new PixelRect(blue.Left + 5, blue.Top + 4, 20, 20);
        var source = new ScreenSource();
        WaitUntilFirstBlockIsOnTheDesktop(source, _probe);
        var result = source.Capture(area, includeCursor: false);
        Assert.That(result.Issue, Is.EqualTo(ScreenCaptureIssue.None), "the capture itself failed");
        Assert.That(result.Image, Is.Not.Null, "no image came back");
        var image = result.Image!;
        Assert.That((image.Width, image.Height), Is.EqualTo((20, 20)));
        Assert.That(image.Bgra.Length, Is.EqualTo(20 * 20 * 4));
        for (var i = 0; i < image.Bgra.Length; i += 4)
        {
            if (image.Bgra[i + 2] != blue.R || image.Bgra[i + 1] != blue.G || image.Bgra[i] != blue.B || image.Bgra[i + 3] != 255)
            {
                Assert.Fail($"pixel {i / 4} is bgra({image.Bgra[i]},{image.Bgra[i + 1]},{image.Bgra[i + 2]},{image.Bgra[i + 3]}), not the blue block's colour with alpha 255");
            }
        }
    }

    [Test]
    public void IncludeCursor_DrawsThePointerOnlyWhenAsked()
    {
        _probe = ProbeWindow.Show("E2E probe " + Guid.NewGuid().ToString("N"), 200, 200, 400, 300, withBlocks: false);
        _probe.WaitUntilDrawn();
        Native.GetWindowRect(_probe.Handle, out var rect);
        var x = (rect.Left + rect.Right) / 2;
        var y = (rect.Top + rect.Bottom) / 2;
        Native.SetCursorPos(x, y);
        Thread.Sleep(300);

        var info = new Native.CursorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.CursorInfo>() };
        Assert.That(Native.GetCursorInfo(ref info), Is.True);
        if ((info.Flags & Native.CursorShowing) == 0)
        {
            Assert.Inconclusive($"Windows reports the pointer as not showing (flags {info.Flags}); there is nothing to draw");
        }

        // A 64 x 64 window of the desktop around the pointer: the arrow hangs down and right of its hot spot.
        var area = new PixelRect(x - 8, y - 8, 64, 64);
        var source = new ScreenSource();
        var without = source.Capture(area, includeCursor: false);
        var with = source.Capture(area, includeCursor: true);
        Assert.That(without.Issue, Is.EqualTo(ScreenCaptureIssue.None));
        Assert.That(with.Issue, Is.EqualTo(ScreenCaptureIssue.None));

        var nonWhiteWithout = CountNotWhite(without.Image!);
        var nonWhiteWith = CountNotWhite(with.Image!);
        Assert.That(nonWhiteWithout, Is.Zero, "the blank spot is white; nothing but the pointer can change it");
        Assert.That(nonWhiteWith, Is.GreaterThan(20), "the pointer was not drawn into the capture");
        Assert.That(with.Image!.Bgra.Where((_, index) => index % 4 == 3).All(alpha => alpha == 255), Is.True, "every pixel keeps alpha 255");
    }

    private static int CountNotWhite(PixelImage image)
    {
        var count = 0;
        for (var i = 0; i < image.Bgra.Length; i += 4)
        {
            if (image.Bgra[i] != 255 || image.Bgra[i + 1] != 255 || image.Bgra[i + 2] != 255)
            {
                count++;
            }
        }

        return count;
    }
}
