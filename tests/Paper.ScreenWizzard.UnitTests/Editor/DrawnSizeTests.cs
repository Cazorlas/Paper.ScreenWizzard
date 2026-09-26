using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>
/// The size a drawing really has on the image decides which drawing a click selects and which drawings a crop keeps (SPEC editor,
/// "Hình vẽ và lịch sử" and "Cắt"). A highlighter is drawn three times as wide as its slider value, a step number is a disc of
/// max(20, 1.8 x font size) pixels across, and a text is about 0.6 x font size wide per character: the boxes must follow that, or a
/// click on a painted pixel finds nothing and a click beside a text finds the text. Numbers are typed by hand.
/// </summary>
[TestFixture]
public sealed class DrawnSizeTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private Guid? Hit(IEditorSession session, int x, int y, int tolerance) =>
        _fixture.Interactor.HitTest(session, new PixelPoint(x, y), tolerance);

    private static StrokeAnnotation Highlighter(int id, int thickness, params (int X, int Y)[] points) =>
        new(EditorData.Id(id), EditorData.Red, thickness, points.Select(p => new PixelPoint(p.X, p.Y)).ToArray(), true);

    [Test]
    public void AHighlighterOfSliderValue20IsHitAt25PixelsFromItsLineBecauseItIsDrawn60Wide()
    {
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(Highlighter(1, 20, (50, 50), (250, 50)));

        Assert.That(Hit(session, 100, 75, 0), Is.EqualTo(EditorData.Id(1)), "y 75 is inside 20..80");
        Assert.That(Hit(session, 100, 85, 0), Is.Null, "y 85 is outside the 60 wide band (edge at 80)");
    }

    [Test]
    public void AHighlighterDrawnPartlyInsideTheCropIsKept()
    {
        // A stroke along y 25, 60 wide, covers y -5..55 in image pixels; the crop starts at y 50, so five rows show.
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(Highlighter(1, 20, (50, 25), (250, 25)));

        session.Crop(new PixelRect(0, 50, 400, 250));

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
    }

    [Test]
    public void AHighlighterThatEndsBeforeTheCropIsDropped()
    {
        // The same stroke drawn along y 15 covers y -15..45: nothing of it reaches y 50.
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(Highlighter(1, 20, (50, 15), (250, 15)));

        session.Crop(new PixelRect(0, 50, 400, 250));

        Assert.That(session.Document.Annotations, Is.Empty);
    }

    [Test]
    public void ATextWhoseDrawnWidthEndsBeforeTheCropIsDroppedNotKeptAsAGhost()
    {
        // "abc" at size 18 starting at x 60 is about 3 x 0.6 x 18 = 32 wide, so it ends near x 92; the crop starts at x 100.
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(new TextAnnotation(EditorData.Id(1), EditorData.Red, 1, new PixelPoint(60, 100), "abc", 18));

        session.Crop(new PixelRect(100, 0, 300, 300));

        Assert.That(session.Document.Annotations, Is.Empty);
    }

    [Test]
    public void AClickAt40PixelsRightOfAShortTextFindsTheArrowUnderItNotTheText()
    {
        // "Van" at size 18 at (100, 100) is drawn about 32 wide; the arrow was drawn first, the text on top of the arrow's start.
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(new ArrowAnnotation(EditorData.Id(1), EditorData.Red, 6, new PixelPoint(100, 110), new PixelPoint(300, 110)));
        session.Add(new TextAnnotation(EditorData.Id(2), EditorData.Red, 1, new PixelPoint(100, 100), "Van", 18));

        Assert.That(Hit(session, 140, 110, 1), Is.EqualTo(EditorData.Id(1)), "40 px right of the text start is arrow only");
        Assert.That(Hit(session, 110, 105, 1), Is.EqualTo(EditorData.Id(2)), "inside the text still selects the text");
    }

    [Test]
    public void AStepNumberIsHitAcrossItsWholeDiscNotOnlyAFontSizeAroundItsCentre()
    {
        // Font size 6: the disc is max(20, 10.8) = 20 across, radius 10. A click 9 pixels from the centre is on the disc.
        var session = _fixture.NewSession(EditorData.Positional(400, 300));
        session.Add(new StepAnnotation(EditorData.Id(1), EditorData.Red, 1, new PixelPoint(200, 150), 1, 6));

        Assert.That(Hit(session, 209, 150, 1), Is.EqualTo(EditorData.Id(1)), "9 px from the centre");
        Assert.That(Hit(session, 215, 150, 1), Is.Null, "15 px from the centre is off the disc");
    }
}
