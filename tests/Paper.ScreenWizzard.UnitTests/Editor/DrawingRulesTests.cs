using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Models;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>
/// SPEC editor, "Hình vẽ và lịch sử" and "Chữ": which drawing a finished gesture makes is the use case's decision, not the window's:
/// a click without a drag draws no line, arrow, rectangle, ellipse or blur; a rectangle is the same whichever corner was pressed
/// first; one click with the pen is a dot; a move by nothing is not a history step; a text edited to blanks is removed.
/// </summary>
[TestFixture]
public sealed class DrawingRulesTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static readonly RgbaColor Red = EditorData.Red;

    [TestCase(ToolKind.Line)]
    [TestCase(ToolKind.Arrow)]
    [TestCase(ToolKind.Rectangle)]
    [TestCase(ToolKind.Ellipse)]
    [TestCase(ToolKind.Blur)]
    public void AClickWithoutADragMakesNoShape(ToolKind tool)
    {
        var shape = _fixture.Interactor.CreateShape(tool, new DragShape(new PixelPoint(40, 40), new PixelPoint(40, 40)), Red, 4);

        Assert.That(shape, Is.Null);
    }

    [TestCase(ToolKind.Rectangle)]
    [TestCase(ToolKind.Ellipse)]
    [TestCase(ToolKind.Blur)]
    public void ABoxWithNoWidthOrNoHeightMakesNoShape(ToolKind tool)
    {
        var flat = _fixture.Interactor.CreateShape(tool, new DragShape(new PixelPoint(10, 50), new PixelPoint(90, 50)), Red, 4);
        var thin = _fixture.Interactor.CreateShape(tool, new DragShape(new PixelPoint(50, 10), new PixelPoint(50, 90)), Red, 4);

        Assert.That(flat, Is.Null);
        Assert.That(thin, Is.Null);
    }

    [Test]
    public void ARectangleDraggedBackwardsIsTheSameRectangleAsDraggedForwards()
    {
        var forward = _fixture.Interactor.CreateShape(ToolKind.Rectangle, new DragShape(new PixelPoint(100, 50), new PixelPoint(500, 350)), Red, 4);
        var backward = _fixture.Interactor.CreateShape(ToolKind.Rectangle, new DragShape(new PixelPoint(500, 350), new PixelPoint(100, 50)), Red, 4);

        Assert.That(((RectangleAnnotation)forward!).Bounds, Is.EqualTo(new PixelRect(100, 50, 400, 300)));
        Assert.That(((RectangleAnnotation)backward!).Bounds, Is.EqualTo(new PixelRect(100, 50, 400, 300)));
    }

    [Test]
    public void ALineKeepsItsTwoEndsInTheOrderTheyWereDragged()
    {
        var line = (LineAnnotation)_fixture.Interactor.CreateShape(ToolKind.Line, new DragShape(new PixelPoint(300, 200), new PixelPoint(10, 20)), Red, 4)!;

        Assert.That(line.From, Is.EqualTo(new PixelPoint(300, 200)));
        Assert.That(line.To, Is.EqualTo(new PixelPoint(10, 20)));
        Assert.That(line.Thickness, Is.EqualTo(4));
    }

    [Test]
    public void ToolsThatDrawNoDraggedShapeMakeNone()
    {
        var pen = _fixture.Interactor.CreateShape(ToolKind.Pen, new DragShape(new PixelPoint(0, 0), new PixelPoint(90, 90)), Red, 4);
        var crop = _fixture.Interactor.CreateShape(ToolKind.Crop, new DragShape(new PixelPoint(0, 0), new PixelPoint(90, 90)), Red, 4);

        Assert.That(pen, Is.Null);
        Assert.That(crop, Is.Null);
    }

    [Test]
    public void OneClickWithThePenIsADotOfTwoEqualPoints()
    {
        var dot = _fixture.Interactor.CreateStroke([new PixelPoint(70, 80)], Red, 6, highlighter: false);

        Assert.That(dot.Points, Is.EqualTo(new[] { new PixelPoint(70, 80), new PixelPoint(70, 80) }));
        Assert.That(dot.IsHighlighter, Is.False);
    }

    [Test]
    public void AStrokeOfSeveralPointsKeepsThemAndTheHighlighterFlag()
    {
        var stroke = _fixture.Interactor.CreateStroke([new PixelPoint(1, 1), new PixelPoint(9, 9), new PixelPoint(20, 5)], Red, 6, highlighter: true);

        Assert.That(stroke.Points, Has.Count.EqualTo(3));
        Assert.That(stroke.IsHighlighter, Is.True);
    }

    [Test]
    public void MovingADrawingByNothingIsNotAHistoryStep()
    {
        var session = _fixture.NewSession(EditorData.Positional(200, 100));
        session.Add(EditorData.Rect(1, 10, 10, 30, 20));
        session.MarkSaved(@"C:\x\a.png");

        session.Move(EditorData.Id(1), 0, 0);

        Assert.That(session.IsDirty, Is.False, "nothing changed");
        Assert.That(session.CanUndo, Is.True, "only the Add is in the history");
        session.Undo();
        Assert.That(session.Document.Annotations, Is.Empty, "one undo takes back the Add, not a phantom move");
    }

    [Test]
    public void EditingATextToBlanksRemovesItAndEditingItToItselfChangesNothing()
    {
        var session = _fixture.NewSession(EditorData.Positional(200, 100));
        session.Add(new TextAnnotation(EditorData.Id(1), Red, 1, new PixelPoint(10, 10), "Van", 18));
        session.MarkSaved(@"C:\x\a.png");

        var same = _fixture.Interactor.EditText(session, EditorData.Id(1), "Van");
        Assert.That(same, Is.False);
        Assert.That(session.IsDirty, Is.False);

        var blanked = _fixture.Interactor.EditText(session, EditorData.Id(1), "   ");
        Assert.That(blanked, Is.True);
        Assert.That(session.Document.Annotations, Is.Empty, "a text of blanks is the empty text, and an empty text is deleted");
    }

    [Test]
    public void EditingATextToNewWordsReplacesThem()
    {
        var session = _fixture.NewSession(EditorData.Positional(200, 100));
        session.Add(new TextAnnotation(EditorData.Id(1), Red, 1, new PixelPoint(10, 10), "Van", 18));

        var changed = _fixture.Interactor.EditText(session, EditorData.Id(1), "Ống 45°");

        Assert.That(changed, Is.True);
        Assert.That(((TextAnnotation)session.Document.Annotations[0]).Text, Is.EqualTo("Ống 45°"));
    }
}
