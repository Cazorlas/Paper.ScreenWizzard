using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>
/// SPEC editor, "What the user does" step 4 (select a shape by clicking it) and "Hình vẽ và lịch sử": which drawing a click in
/// image pixels lands on. A line, arrow, pen stroke, rectangle or ellipse outline is hit within tolerance + thickness/2 of its
/// stroke; a blur area, a text and a step number are hit inside their box grown by the tolerance; the drawing added last wins.
/// All numbers are typed by hand.
/// </summary>
[TestFixture]
public sealed class HitTestTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static LineAnnotation Line(int id, int x1, int y1, int x2, int y2, int thickness = 4) =>
        new(EditorData.Id(id), EditorData.Red, thickness, new PixelPoint(x1, y1), new PixelPoint(x2, y2));

    private static ArrowAnnotation Arrow(int id, int x1, int y1, int x2, int y2, int thickness = 4) =>
        new(EditorData.Id(id), EditorData.Red, thickness, new PixelPoint(x1, y1), new PixelPoint(x2, y2));

    private static StrokeAnnotation Stroke(int id, int thickness, bool highlighter, params (int X, int Y)[] points) =>
        new(EditorData.Id(id), EditorData.Red, thickness, points.Select(p => new PixelPoint(p.X, p.Y)).ToArray(), highlighter);

    private static EllipseAnnotation Ellipse(int id, int x, int y, int width, int height, int thickness = 4) =>
        new(EditorData.Id(id), EditorData.Red, thickness, new PixelRect(x, y, width, height));

    private static BlurAnnotation Blur(int id, int x, int y, int width, int height) =>
        new(EditorData.Id(id), EditorData.Red, 1, new PixelRect(x, y, width, height));

    private static TextAnnotation Text(int id, int x, int y, string text, int fontSize) =>
        new(EditorData.Id(id), EditorData.Red, 1, new PixelPoint(x, y), text, fontSize);

    private Guid? Hit(IEditorSession session, int x, int y, int tolerance) =>
        _fixture.Interactor.HitTest(session, new PixelPoint(x, y), tolerance);

    private static Guid Id(int n) => EditorData.Id(n);

    // ---- nothing there ----

    [Test]
    public void AnEmptyImageHitsNothing()
    {
        var session = _fixture.NewSession();

        Assert.That(Hit(session, 50, 30, 5), Is.Null);
    }

    [Test]
    public void AShapeThatWasUndoneIsNotHit()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Line(1, 100, 100, 300, 100));
        session.Undo();

        Assert.That(Hit(session, 200, 100, 3), Is.Null);
    }

    // ---- line and arrow ----

    [Test]
    public void ALineIsHitWithinToleranceAndHalfItsThickness()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Line(1, 100, 100, 300, 100, thickness: 4));

        Assert.That(Hit(session, 200, 104, 3), Is.EqualTo(Id(1)), "distance 4 <= 3 + 4/2");
        Assert.That(Hit(session, 200, 96, 3), Is.EqualTo(Id(1)), "the other side too");
        Assert.That(Hit(session, 200, 110, 3), Is.Null, "distance 10 > 3 + 2");
        Assert.That(Hit(session, 200, 106, 3), Is.Null, "distance 6 > 5");
    }

    [Test]
    public void ALineIsASegmentNotAnInfiniteLine()
    {
        var session = _fixture.NewSession(600, 300);
        session.Add(Line(1, 100, 100, 300, 100, thickness: 4));

        Assert.That(Hit(session, 303, 100, 3), Is.EqualTo(Id(1)), "3 pixels past the end");
        Assert.That(Hit(session, 96, 100, 3), Is.EqualTo(Id(1)), "4 pixels before the start, 4 <= 5");
        Assert.That(Hit(session, 400, 100, 3), Is.Null, "100 pixels past the end");
        Assert.That(Hit(session, 306, 100, 3), Is.Null, "6 pixels past the end");
    }

    [Test]
    public void ADiagonalArrowIsHitByItsPerpendicularDistance()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Arrow(1, 0, 0, 100, 100, thickness: 2));

        Assert.That(Hit(session, 50, 51, 0), Is.EqualTo(Id(1)), "0.71 <= 0 + 1");
        Assert.That(Hit(session, 50, 54, 0), Is.Null, "2.83 > 1");
        Assert.That(Hit(session, 50, 54, 2), Is.EqualTo(Id(1)), "2.83 <= 2 + 1");
    }

    [Test]
    public void ANegativeToleranceCountsAsZero()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Line(1, 100, 100, 300, 100, thickness: 4));

        Assert.That(Hit(session, 200, 102, -5), Is.EqualTo(Id(1)), "distance 2 <= 0 + 2");
        Assert.That(Hit(session, 200, 103, -5), Is.Null, "distance 3 > 0 + 2");
    }

    // ---- pen and highlighter ----

    [Test]
    public void AStrokeIsHitAlongEverySegment()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Stroke(1, 6, false, (10, 10), (100, 10), (100, 100)));

        Assert.That(Hit(session, 52, 14, 2), Is.EqualTo(Id(1)), "distance 4 to the first segment <= 2 + 3");
        Assert.That(Hit(session, 100, 50, 2), Is.EqualTo(Id(1)), "on the second segment");
        Assert.That(Hit(session, 104, 60, 2), Is.EqualTo(Id(1)), "distance 4 to the second segment");
        Assert.That(Hit(session, 50, 20, 2), Is.Null, "distance 10 > 5");
        Assert.That(Hit(session, 50, 50, 2), Is.Null, "inside the corner, 40 from both segments");
    }

    [Test]
    public void AHighlighterStrokeIsHitLikeAPenStroke()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Stroke(1, 20, true, (10, 50), (200, 50)));

        Assert.That(Hit(session, 100, 58, 0), Is.EqualTo(Id(1)), "distance 8 <= 0 + 10");
        Assert.That(Hit(session, 100, 61, 0), Is.Null, "distance 11 > 10");
    }

    [Test]
    public void ASinglePointStrokeIsASmallDisc()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Stroke(1, 8, false, (40, 40)));

        Assert.That(Hit(session, 43, 44, 1), Is.EqualTo(Id(1)), "distance 5 (3-4-5) <= 1 + 4");
        Assert.That(Hit(session, 44, 45, 1), Is.Null, "distance 6.4 > 5");
    }

    [Test]
    public void AStrokeWithNoPointsIsNeverHit()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(Stroke(1, 8, false));

        Assert.That(Hit(session, 0, 0, 5), Is.Null);
    }

    // ---- rectangle: the outline only ----

    [Test]
    public void ARectangleIsHitOnItsOutline()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 200, 100, thickness: 4));

        Assert.That(Hit(session, 200, 200, 3), Is.EqualTo(Id(1)), "on the bottom edge");
        Assert.That(Hit(session, 100, 150, 3), Is.EqualTo(Id(1)), "on the left edge");
        Assert.That(Hit(session, 98, 150, 3), Is.EqualTo(Id(1)), "2 outside the left edge");
        Assert.That(Hit(session, 103, 150, 3), Is.EqualTo(Id(1)), "3 inside the left edge");
        Assert.That(Hit(session, 300, 200, 3), Is.EqualTo(Id(1)), "the corner");
    }

    [Test]
    public void ClickingInsideAnEmptyRectangleDoesNotSelectIt()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 200, 100, thickness: 4));

        Assert.That(Hit(session, 200, 150, 3), Is.Null, "the middle is 50 from every edge");
        Assert.That(Hit(session, 120, 150, 3), Is.Null, "20 inside the left edge > 5");
    }

    [Test]
    public void ARectangleIsNotHitFarOutsideItsOutline()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 200, 100, thickness: 4));

        Assert.That(Hit(session, 200, 210, 3), Is.Null, "10 below the bottom edge");
        Assert.That(Hit(session, 303, 204, 3), Is.EqualTo(Id(1)), "diagonal to the corner: 3 right, 4 down = distance 5 <= 5");
        Assert.That(Hit(session, 304, 204, 3), Is.Null, "4 right, 4 down = distance 5.66 > 5");
    }

    [Test]
    public void ATinyRectangleIsHitAtItsCentre()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 8, 8, thickness: 4));

        Assert.That(Hit(session, 104, 104, 3), Is.EqualTo(Id(1)), "4 from every edge <= 5");
    }

    // ---- ellipse: the outline only ----

    [Test]
    public void AnEllipseIsHitOnItsOutline()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Ellipse(1, 100, 100, 200, 100, thickness: 4));

        Assert.That(Hit(session, 300, 150, 3), Is.EqualTo(Id(1)), "the right end of the long axis");
        Assert.That(Hit(session, 200, 100, 3), Is.EqualTo(Id(1)), "the top");
        Assert.That(Hit(session, 271, 185, 3), Is.EqualTo(Id(1)), "on the curve at 45 degrees of its parameter (70.7, 35.4 from the centre)");
        Assert.That(Hit(session, 304, 150, 3), Is.EqualTo(Id(1)), "4 outside the right end <= 5");
        Assert.That(Hit(session, 200, 96, 3), Is.EqualTo(Id(1)), "4 above the top <= 5");
    }

    [Test]
    public void AnEllipseIsNotHitInsideOrFarOutside()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Ellipse(1, 100, 100, 200, 100, thickness: 4));

        Assert.That(Hit(session, 200, 150, 3), Is.Null, "the centre is 50 from the curve");
        Assert.That(Hit(session, 200, 120, 3), Is.Null, "20 inside the top");
        Assert.That(Hit(session, 310, 150, 3), Is.Null, "10 outside the right end");
        Assert.That(Hit(session, 200, 90, 3), Is.Null, "10 above the top");
        Assert.That(Hit(session, 105, 105, 3), Is.Null, "the corner of the bounding box is off the curve");
    }

    [Test]
    public void ATinyEllipseIsHitAtItsCentre()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Ellipse(1, 100, 100, 8, 8, thickness: 4));

        Assert.That(Hit(session, 104, 104, 3), Is.EqualTo(Id(1)), "4 from the curve <= 5");
    }

    // ---- blur area, text, step number: a box ----

    [Test]
    public void ABlurAreaIsHitInsideItsBoxGrownByTheTolerance()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Blur(1, 50, 50, 100, 100));

        Assert.That(Hit(session, 100, 100, 0), Is.EqualTo(Id(1)), "the middle of the area, not only its edge");
        Assert.That(Hit(session, 60, 60, 0), Is.EqualTo(Id(1)));
        Assert.That(Hit(session, 153, 100, 5), Is.EqualTo(Id(1)), "3 right of the area <= 5");
        Assert.That(Hit(session, 160, 100, 5), Is.Null, "10 right of the area > 5");
        Assert.That(Hit(session, 100, 40, 0), Is.Null, "10 above the area with no tolerance");
    }

    [Test]
    public void ATextIsHitInsideItsBoxGrownByTheTolerance()
    {
        // Three letters at font size 10: the box is 3 * 10 = 30 wide and 2 * 10 = 20 tall from the origin, (100,100) to (130,120).
        var session = _fixture.NewSession(500, 300);
        session.Add(Text(1, 100, 100, "Van", 10));

        Assert.That(Hit(session, 115, 110, 0), Is.EqualTo(Id(1)));
        Assert.That(Hit(session, 133, 110, 5), Is.EqualTo(Id(1)), "3 right of the box <= 5");
        Assert.That(Hit(session, 136, 110, 5), Is.Null, "6 right of the box > 5");
        Assert.That(Hit(session, 115, 90, 3), Is.Null, "10 above the box > 3");
    }

    [Test]
    public void ATextOfTwoLinesIsHitInTheSecondLine()
    {
        // "Van" and "2": longest line 3 letters, 2 lines, so the box is 30 wide and 2 * 10 * 2 = 40 tall.
        var session = _fixture.NewSession(500, 300);
        session.Add(Text(1, 100, 100, "Van\n2", 10));

        Assert.That(Hit(session, 110, 135, 0), Is.EqualTo(Id(1)));
        Assert.That(Hit(session, 110, 143, 5), Is.EqualTo(Id(1)), "3 under the box <= 5");
        Assert.That(Hit(session, 110, 146, 5), Is.Null, "6 under the box > 5");
    }

    [Test]
    public void AStepNumberIsHitInsideTheBoxAroundItsCentre()
    {
        // Step at (300,300), font size 18: the box is (282,282) to (318,318).
        var session = _fixture.NewSession(500, 400);
        session.Add(EditorData.Step(1, 1, 300, 300));

        Assert.That(Hit(session, 310, 300, 0), Is.EqualTo(Id(1)));
        Assert.That(Hit(session, 300, 300, 0), Is.EqualTo(Id(1)), "its centre");
        Assert.That(Hit(session, 321, 300, 5), Is.EqualTo(Id(1)), "3 right of the box <= 5");
        Assert.That(Hit(session, 330, 300, 5), Is.Null, "12 right of the box > 5");
        Assert.That(Hit(session, 330, 300, 0), Is.Null);
    }

    // ---- several drawings: the last one wins ----

    [Test]
    public void OfTwoOverlappingRectanglesTheLaterAddedIsReturned()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 100, 100));
        session.Add(EditorData.Rect(2, 150, 50, 100, 100));

        Assert.That(Hit(session, 150, 100, 3), Is.EqualTo(Id(2)), "(150,100) is on the top edge of 1 and the left edge of 2");
    }

    [Test]
    public void TheOrderOfAddingDecidesNotTheIds()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(2, 150, 50, 100, 100));
        session.Add(EditorData.Rect(1, 100, 100, 100, 100));

        Assert.That(Hit(session, 150, 100, 3), Is.EqualTo(Id(1)));
    }

    [Test]
    public void ALineDrawnOverABlurAreaWinsAndTheBlurWinsWhenItCameLast()
    {
        var blurFirst = _fixture.NewSession(500, 300);
        blurFirst.Add(Blur(1, 50, 50, 200, 100));
        blurFirst.Add(Line(2, 60, 100, 240, 100));

        var lineFirst = _fixture.NewSession(500, 300);
        lineFirst.Add(Line(2, 60, 100, 240, 100));
        lineFirst.Add(Blur(1, 50, 50, 200, 100));

        Assert.That(Hit(blurFirst, 150, 100, 3), Is.EqualTo(Id(2)));
        Assert.That(Hit(lineFirst, 150, 100, 3), Is.EqualTo(Id(1)));
    }

    [Test]
    public void ADrawingThatIsNotHitIsSkippedForOneUnderIt()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Blur(1, 50, 50, 300, 200));
        session.Add(EditorData.Rect(2, 400, 250, 50, 30));

        Assert.That(Hit(session, 100, 100, 3), Is.EqualTo(Id(1)), "the later rectangle is far away");
    }

    [Test]
    public void ASmallShapeInsideAnEmptyRectangleCanBeClicked()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(EditorData.Rect(1, 100, 100, 300, 150));
        session.Add(EditorData.Step(2, 1, 250, 175));

        Assert.That(Hit(session, 250, 175, 3), Is.EqualTo(Id(2)));
    }

    [Test]
    public void AShapeDrawnFirstUnderAnEmptyRectangleIsStillReachedThroughIt()
    {
        var session = _fixture.NewSession(500, 300);
        session.Add(Blur(1, 100, 100, 300, 150));
        session.Add(EditorData.Rect(2, 100, 100, 300, 150));

        Assert.That(Hit(session, 250, 175, 3), Is.EqualTo(Id(1)), "inside the rectangle only the blur is there");
        Assert.That(Hit(session, 100, 175, 3), Is.EqualTo(Id(2)), "on the rectangle's edge the rectangle is on top");
    }
}
