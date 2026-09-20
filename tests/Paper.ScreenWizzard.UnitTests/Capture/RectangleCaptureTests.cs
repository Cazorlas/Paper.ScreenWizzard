using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture: "Vùng chữ nhật" and F2.</summary>
[TestFixture]
public sealed class RectangleCaptureTests
{
    private CaptureFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new CaptureFixture();

    private static CaptureRequest RectangleRequest => CaptureData.Request(CaptureKind.Rectangle);

    private static PixelImage ImageOf(CaptureOutcome outcome)
    {
        Assert.That(outcome.Image, Is.Not.Null, "no image; issue " + outcome.Issue);
        return outcome.Image!;
    }

    // ---- Kéo theo hướng nào cũng ra cùng một vùng ----

    [Test]
    public async Task DragFromBottomRightToTopLeftGivesRegionAt300x250Of200x150()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(500, 400), new PixelPoint(300, 250));

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(300, 250, 200, 150)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(200));
        Assert.That(image.Height, Is.EqualTo(150));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 300, 250), Is.EqualTo(-1), "first pixel that is not the screen's");
    }

    [Test]
    public async Task DragFromTopLeftToBottomRightGivesTheSameRegionAndTheSamePixels()
    {
        var first = await _fixture.BeginAsync(RectangleRequest);
        var backwards = first.CompleteRectangle(new PixelPoint(500, 400), new PixelPoint(300, 250));
        var second = await _fixture.BeginAsync(RectangleRequest);
        var forwards = second.CompleteRectangle(new PixelPoint(300, 250), new PixelPoint(500, 400));

        Assert.That(forwards.Region, Is.EqualTo(new PixelRect(300, 250, 200, 150)));
        Assert.That(forwards.Region, Is.EqualTo(backwards.Region));
        Assert.That(ImageOf(forwards).Bgra, Is.EqualTo(ImageOf(backwards).Bgra));
    }

    [Test]
    public async Task PreviewShowsTheSameNormalisedRegionInEitherDirection()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        Assert.That(session.PreviewRectangle(new PixelPoint(500, 400), new PixelPoint(300, 250)), Is.EqualTo(new PixelRect(300, 250, 200, 150)));
        Assert.That(session.PreviewRectangle(new PixelPoint(300, 250), new PixelPoint(500, 400)), Is.EqualTo(new PixelRect(300, 250, 200, 150)));
        Assert.That(session.PreviewRectangle(new PixelPoint(1800, 500), new PixelPoint(2100, 700)), Is.EqualTo(new PixelRect(1800, 500, 120, 200)));
    }

    // ---- Vùng bị cắt theo mép desktop ----

    [Test]
    public async Task DragPastTheRightEdgeKeepsOnlyTheRealPart120x200()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(1800, 500), new PixelPoint(2100, 700));

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(1800, 500, 120, 200)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(120));
        Assert.That(image.Height, Is.EqualTo(200));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 1800, 500), Is.EqualTo(-1));
    }

    [Test]
    public async Task DragPastTheLeftEdgeStartsAtZeroAnd100x200()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(100, 100), new PixelPoint(-50, 300));

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 100, 100, 200)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(100));
        Assert.That(image.Height, Is.EqualTo(200));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 0, 100), Is.EqualTo(-1));
    }

    [Test]
    public async Task NegativeCoordinatesAreValidWhenAMonitorSitsLeftOfTheOrigin()
    {
        _fixture.UseMonitors(CaptureData.LeftAndPrimary);
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(-100, 100), new PixelPoint(100, 300));

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(-100, 100, 200, 200)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(200));
        Assert.That(image.Height, Is.EqualTo(200));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, -100, 100), Is.EqualTo(-1));
    }

    // ---- Hai màn hình liền nhau ra một ảnh liền ----

    [Test]
    public async Task DragAcrossTwoMonitorsGivesOneImage300x200WithNoGap()
    {
        _fixture.UseMonitors(CaptureData.SideBySide);
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(1800, 400), new PixelPoint(2100, 600));

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(300));
        Assert.That(image.Height, Is.EqualTo(200));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 1800, 400), Is.EqualTo(-1));

        // Typed by hand from the fake screen's colour rule (R = x, G = y, B = 16 * (x / 256) + y / 256): column 119 is desktop
        // x 1919, the last of monitor 1; column 120 is desktop x 1920, the first of monitor 2. Row 0 is desktop y 400.
        Assert.That(CaptureData.PixelAt(image, 119, 0), Is.EqualTo(new byte[] { 113, 144, 127, 255 }));
        Assert.That(CaptureData.PixelAt(image, 120, 0), Is.EqualTo(new byte[] { 113, 144, 128, 255 }));
    }

    // ---- Ảnh ra bằng số pixel thật ----

    [Test]
    public async Task OnA150PercentMonitorARegionOf450x300PhysicalPixelsGivesA450x300Image()
    {
        _fixture.UseMonitors([CaptureData.Monitor(0, 0, 0, 1920, 1080, true, dpi: 144)]);
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(100, 100), new PixelPoint(550, 400));

        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(450));
        Assert.That(image.Height, Is.EqualTo(300));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 100, 100), Is.EqualTo(-1));
    }

    [Test]
    public async Task OnA100PercentMonitorARegionOf300x200GivesA300x200Image()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(100, 100), new PixelPoint(400, 300));

        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(300));
        Assert.That(image.Height, Is.EqualTo(200));
    }

    [Test]
    public async Task ARegionOfExactlyThreeByThreeIsCaptured()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var outcome = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(13, 13));

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(3));
        Assert.That(image.Height, Is.EqualTo(3));
    }

    // ---- F2 ----

    [Test]
    public async Task F2_ARegionUnderThreeByThreeIsNotCapturedAndTheSessionStaysOpenForAnotherDrag()
    {
        var session = await _fixture.BeginAsync(RectangleRequest);

        var narrow = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(12, 200));
        var flat = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 12));
        var clippedToOnePixel = session.CompleteRectangle(new PixelPoint(1919, 100), new PixelPoint(2100, 300));

        Assert.Multiple(() =>
        {
            Assert.That(narrow.Issue, Is.EqualTo(CaptureIssue.RegionTooSmall), "2 wide");
            Assert.That(narrow.Image, Is.Null);
            Assert.That(narrow.Message?.Key, Is.EqualTo("Capture.RegionTooSmall"));
            Assert.That(flat.Issue, Is.EqualTo(CaptureIssue.RegionTooSmall), "2 high");
            Assert.That(flat.Image, Is.Null);
            Assert.That(clippedToOnePixel.Issue, Is.EqualTo(CaptureIssue.RegionTooSmall), "1 wide after clamping");
            Assert.That(session.IsActive, Is.True, "still on the selection screen");
        });

        var again = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 200));
        Assert.That(again.Issue, Is.EqualTo(CaptureIssue.None), "the user can drag again");
    }
}
