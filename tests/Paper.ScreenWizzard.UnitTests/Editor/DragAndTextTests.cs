using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Đường thẳng, mũi tên, khung, elip" (the Shift rules) and "Chữ" (F4).</summary>
[TestFixture]
public sealed class DragAndTextTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    // ---- Lines and arrows snap to 0, 45 or 90 degrees ----

    // The end point is the drag projected onto the nearest of the eight directions, rounded to whole pixels; the numbers
    // are worked out by hand: (200, 20) is 5.7 degrees so it goes flat; (200, 190) is 43.5 degrees so it goes to 45.
    [TestCase(ToolKind.Line, 100, 100, 300, 120, 300, 100)]
    [TestCase(ToolKind.Arrow, 100, 100, 300, 120, 300, 100)]
    [TestCase(ToolKind.Line, 100, 100, 300, 290, 295, 295)]
    [TestCase(ToolKind.Arrow, 100, 100, 300, 290, 295, 295)]
    [TestCase(ToolKind.Line, 100, 100, 110, 300, 100, 300)]
    [TestCase(ToolKind.Line, 300, 300, 100, 120, 110, 110)]
    [TestCase(ToolKind.Line, 300, 100, 100, 110, 100, 100)]
    public void ShiftSnapsALineOrArrowToTheNearestMultipleOf45Degrees(ToolKind tool, int sx, int sy, int cx, int cy, int ex, int ey)
    {
        var shape = _fixture.Interactor.ConstrainDrag(tool, new PixelPoint(sx, sy), new PixelPoint(cx, cy), true);

        Assert.That(shape.From, Is.EqualTo(new PixelPoint(sx, sy)));
        Assert.That(shape.To, Is.EqualTo(new PixelPoint(ex, ey)));
    }

    // ---- Rectangles and ellipses become squares and circles ----

    [TestCase(ToolKind.Rectangle, 100, 100, 260, 180, 260, 260)]
    [TestCase(ToolKind.Ellipse, 100, 100, 260, 180, 260, 260)]
    [TestCase(ToolKind.Rectangle, 100, 100, 40, 70, 40, 40)]
    [TestCase(ToolKind.Rectangle, 100, 100, 60, 150, 50, 150)]
    [TestCase(ToolKind.Ellipse, 100, 100, 130, 10, 190, 10)]
    public void ShiftMakesARectangleASquareAndAnEllipseACircleGrowingTowardTheDrag(ToolKind tool, int sx, int sy, int cx, int cy, int ex, int ey)
    {
        var shape = _fixture.Interactor.ConstrainDrag(tool, new PixelPoint(sx, sy), new PixelPoint(cx, cy), true);

        Assert.That(shape.From, Is.EqualTo(new PixelPoint(sx, sy)));
        Assert.That(shape.To, Is.EqualTo(new PixelPoint(ex, ey)));
    }

    // ---- Without Shift, or for other tools, nothing changes ----

    [TestCase(ToolKind.Line, false)]
    [TestCase(ToolKind.Arrow, false)]
    [TestCase(ToolKind.Rectangle, false)]
    [TestCase(ToolKind.Ellipse, false)]
    [TestCase(ToolKind.Pen, true)]
    [TestCase(ToolKind.Highlighter, true)]
    [TestCase(ToolKind.Blur, true)]
    [TestCase(ToolKind.Crop, true)]
    public void WithoutShiftOrForAnotherToolTheDragIsUnchanged(ToolKind tool, bool shift)
    {
        var shape = _fixture.Interactor.ConstrainDrag(tool, new PixelPoint(100, 100), new PixelPoint(260, 180), shift);

        Assert.That(shape.From, Is.EqualTo(new PixelPoint(100, 100)));
        Assert.That(shape.To, Is.EqualTo(new PixelPoint(260, 180)));
    }

    // ---- Text ----

    [Test]
    public void TextWithVietnameseMarksADegreeSignAndAnEmDashIsStoredExactlyAsTyped()
    {
        var session = _fixture.NewSession();
        const string typed = "Đường ống 45° — thử nghiệm";

        var created = _fixture.Interactor.AddText(session, new PixelPoint(30, 40), typed, EditorData.Blue, 18);

        Assert.That(created, Is.True);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        var text = session.Document.Annotations[0] as TextAnnotation;
        Assert.That(text, Is.Not.Null);
        Assert.That(text!.Text, Is.EqualTo(typed));
        Assert.That(text.Origin, Is.EqualTo(new PixelPoint(30, 40)));
        Assert.That(text.Color, Is.EqualTo(EditorData.Blue));
        Assert.That(text.FontSize, Is.EqualTo(18));
        Assert.That(text.Thickness, Is.EqualTo(1));
        Assert.That(text.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(session.CanUndo, Is.True, "the text is one history step");
    }

    [Test]
    public void TwoTextsGetDifferentIds()
    {
        var session = _fixture.NewSession();

        _fixture.Interactor.AddText(session, new PixelPoint(0, 0), "a", EditorData.Red, 18);
        _fixture.Interactor.AddText(session, new PixelPoint(0, 30), "b", EditorData.Red, 18);

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(2));
        Assert.That(session.Document.Annotations[0].Id, Is.Not.EqualTo(session.Document.Annotations[1].Id));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\r\n\t ")]
    public void F4_CommittingATextWithNothingTypedCreatesNothingAndSaysNothing(string? typed)
    {
        var session = _fixture.NewSession();
        var first = _fixture.Interactor.AddText(session, new PixelPoint(10, 10), "ok", EditorData.Red, 18);
        Assert.That(first, Is.True, "a text that was typed is created (so the empty case below is not passing by doing nothing at all)");
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));

        var created = _fixture.Interactor.AddText(session, new PixelPoint(30, 40), typed!, EditorData.Red, 18);

        Assert.That(created, Is.False);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1), "no text was added for the empty box");
        Assert.That(session.Undo(), Is.True);
        Assert.That(session.CanUndo, Is.False, "no history step for an empty text box: the one Undo took back the first text");
        Assert.That(_fixture.Log.Lines, Is.Empty, "an empty text box is what the user meant, not an error");
    }
}