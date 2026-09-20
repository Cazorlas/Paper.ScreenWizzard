using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture: "Vùng tự do" and F3.</summary>
[TestFixture]
public sealed class FreeformCaptureTests
{
    private static readonly PixelPoint[] _triangle = [new(100, 100), new(300, 100), new(200, 300)];

    private CaptureFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new CaptureFixture();

    private static CaptureRequest FreeformRequest => CaptureData.Request(CaptureKind.Freeform);

    private static PixelImage ImageOf(CaptureOutcome outcome)
    {
        Assert.That(outcome.Image, Is.Not.Null, "no image; issue " + outcome.Issue);
        return outcome.Image!;
    }

    private static byte AlphaAt(PixelImage image, int x, int y) => CaptureData.PixelAt(image, x, y)[3];

    [Test]
    public async Task TheImageIsTheBoundingBoxOfTheOutline200x200()
    {
        var session = await _fixture.BeginAsync(FreeformRequest);

        var outcome = session.CompleteFreeform(_triangle);

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(100, 100, 200, 200)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(200));
        Assert.That(image.Height, Is.EqualTo(200));
    }

    [Test]
    public async Task InsideTheOutlineIsOpaqueWithTheScreensColourAndOutsideIsTransparent()
    {
        var session = await _fixture.BeginAsync(FreeformRequest);

        var image = ImageOf(session.CompleteFreeform(_triangle));

        // The image pixel of desktop (200, 150) is (100, 50); that of desktop (110, 290) is (10, 190).
        Assert.That(AlphaAt(image, 100, 50), Is.EqualTo(255), "inside, mid-height");
        Assert.That(AlphaAt(image, 5, 5), Is.EqualTo(255), "inside, near the top-left corner");
        Assert.That(AlphaAt(image, 190, 5), Is.EqualTo(255), "inside, near the top-right corner");
        Assert.That(AlphaAt(image, 10, 190), Is.EqualTo(0), "outside, low left");
        Assert.That(AlphaAt(image, 0, 199), Is.EqualTo(0), "outside, bottom-left corner of the box");
        Assert.That(AlphaAt(image, 199, 199), Is.EqualTo(0), "outside, bottom-right corner of the box");

        // The screen at desktop (200, 150), typed by hand: R = 200, G = 150, B = 16 * 0 + 0 = 0.
        Assert.That(CaptureData.PixelAt(image, 100, 50), Is.EqualTo(new byte[] { 0, 150, 200, 255 }));
    }

    [Test]
    public async Task AnOutlineThatWasNotClosedGivesTheSameResultAsTheClosedTriangle()
    {
        var open = await _fixture.BeginAsync(FreeformRequest);
        var openOutcome = open.CompleteFreeform(_triangle);
        var closed = await _fixture.BeginAsync(FreeformRequest);
        var closedOutcome = closed.CompleteFreeform([.. _triangle, _triangle[0]]);

        Assert.That(closedOutcome.Region, Is.EqualTo(openOutcome.Region));
        Assert.That(ImageOf(closedOutcome).Bgra, Is.EqualTo(ImageOf(openOutcome).Bgra));
        Assert.That(AlphaAt(ImageOf(openOutcome), 100, 50), Is.EqualTo(255));
        Assert.That(AlphaAt(ImageOf(openOutcome), 10, 190), Is.EqualTo(0));
    }

    [Test]
    public async Task AnOutlineOverhangingTheDesktopIsClampedToIt()
    {
        var session = await _fixture.BeginAsync(FreeformRequest);

        var outcome = session.CompleteFreeform([new PixelPoint(-50, 100), new PixelPoint(100, 100), new PixelPoint(100, 300)]);

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 100, 100, 200)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(100));
        Assert.That(image.Height, Is.EqualTo(200));
        Assert.That(AlphaAt(image, 90, 150), Is.EqualTo(255), "desktop (90, 250), above the slanted edge");
        Assert.That(AlphaAt(image, 10, 190), Is.EqualTo(0), "desktop (10, 290), below the slanted edge");
    }

    [Test]
    public async Task AnOutlineOfExactlyNineSquarePixelsIsCaptured()
    {
        var session = await _fixture.BeginAsync(FreeformRequest);

        // A right triangle 6 wide and 3 high: shoelace area 9.
        var outcome = session.CompleteFreeform([new PixelPoint(0, 0), new PixelPoint(6, 0), new PixelPoint(0, 3)]);

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.None));
        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 0, 6, 3)));
    }

    // ---- F3 ----

    [Test]
    public async Task F3_AnOutlineWithFewerThanThreeDistinctPointsOrUnderNineSquarePixelsIsNotCapturedAndTheSessionStays()
    {
        var session = await _fixture.BeginAsync(FreeformRequest);

        var none = session.CompleteFreeform([]);
        var onePointRepeated = session.CompleteFreeform([new PixelPoint(50, 50), new PixelPoint(50, 50), new PixelPoint(50, 50)]);
        var twoDistinct = session.CompleteFreeform([new PixelPoint(10, 10), new PixelPoint(90, 90), new PixelPoint(10, 10)]);
        var underNine = session.CompleteFreeform([new PixelPoint(0, 0), new PixelPoint(4, 0), new PixelPoint(0, 4)]);
        var collinear = session.CompleteFreeform([new PixelPoint(10, 10), new PixelPoint(50, 50), new PixelPoint(90, 90)]);

        Assert.Multiple(() =>
        {
            Assert.That(none.Issue, Is.EqualTo(CaptureIssue.OutlineTooSmall), "no point");
            Assert.That(onePointRepeated.Issue, Is.EqualTo(CaptureIssue.OutlineTooSmall), "one point three times");
            Assert.That(twoDistinct.Issue, Is.EqualTo(CaptureIssue.OutlineTooSmall), "two distinct points");
            Assert.That(underNine.Issue, Is.EqualTo(CaptureIssue.OutlineTooSmall), "area 8");
            Assert.That(collinear.Issue, Is.EqualTo(CaptureIssue.OutlineTooSmall), "area 0");
            Assert.That(underNine.Image, Is.Null);
            Assert.That(underNine.Message?.Key, Is.EqualTo("Capture.OutlineTooSmall"));
            Assert.That(session.IsActive, Is.True, "still on the selection screen");
        });

        var again = session.CompleteFreeform(_triangle);
        Assert.That(again.Issue, Is.EqualTo(CaptureIssue.None), "the user can draw again");
    }
}
