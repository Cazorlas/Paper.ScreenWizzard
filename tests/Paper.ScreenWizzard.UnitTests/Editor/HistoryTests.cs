using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Hình vẽ và lịch sử": every mutating call is one step; Undo and Redo walk the steps (F6 when there are none).</summary>
[TestFixture]
public sealed class HistoryTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static Guid[] Ids(IEditorSession session) => session.Document.Annotations.Select(a => a.Id).ToArray();

    // ---- Undo and Redo count in steps ----

    [Test]
    public void ThreeShapesThenUndoTwiceLeavesOneShape()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        session.Add(EditorData.Rect(3, 40, 0, 10, 10));

        var first = session.Undo();
        var second = session.Undo();

        Assert.That(first, Is.True);
        Assert.That(second, Is.True);
        Assert.That(Ids(session), Is.EqualTo(new[] { EditorData.Id(1) }));
    }

    [Test]
    public void ThreeShapesUndoTwiceThenRedoOnceGivesTwoShapes()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        session.Add(EditorData.Rect(3, 40, 0, 10, 10));
        session.Undo();
        session.Undo();

        var redone = session.Redo();

        Assert.That(redone, Is.True);
        Assert.That(Ids(session), Is.EqualTo(new[] { EditorData.Id(1), EditorData.Id(2) }));
    }

    [Test]
    public void UndoOneStepThenDrawingANewShapeDropsTheRedoBranch()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        session.Undo();
        Assert.That(session.CanRedo, Is.True, "an undone step can be redone until something new is drawn");

        session.Add(EditorData.Rect(3, 40, 0, 10, 10));

        Assert.That(session.CanRedo, Is.False);
        Assert.That(session.Redo(), Is.False);
        Assert.That(Ids(session), Is.EqualTo(new[] { EditorData.Id(1), EditorData.Id(3) }));
    }

    [Test]
    public void EveryMutatingCallIsOneStepAndUndoWalksThemBackOneByOne()
    {
        var session = _fixture.NewSession();
        var id = EditorData.Id(1);
        session.Add(EditorData.Rect(1, 0, 0, 10, 10, thickness: 4));
        session.Move(id, 5, 5);
        session.Recolor(id, EditorData.Blue);
        session.SetThickness(id, 9);
        session.Delete(id);
        Assert.That(session.Document.Annotations, Is.Empty);

        Assert.That(session.Undo(), Is.True, "undo the delete");
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        var afterDeleteUndone = (RectangleAnnotation)session.Document.Annotations[0];
        Assert.That(afterDeleteUndone.Thickness, Is.EqualTo(9));

        Assert.That(session.Undo(), Is.True, "undo the thickness");
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Thickness, Is.EqualTo(4));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Color, Is.EqualTo(EditorData.Blue));

        Assert.That(session.Undo(), Is.True, "undo the colour");
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Color, Is.EqualTo(EditorData.Red));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(5, 5, 10, 10)));

        Assert.That(session.Undo(), Is.True, "undo the move");
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(0, 0, 10, 10)));

        Assert.That(session.Undo(), Is.True, "undo the add");
        Assert.That(session.Document.Annotations, Is.Empty);
        Assert.That(session.Undo(), Is.False, "nothing left to undo");
    }

    // ---- Recolor, thickness, selection ----

    [Test]
    public void ChoosingAShapeRecolouringItAndUndoGivesTheOldColourAndKeepsItSelected()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Select(EditorData.Id(1));
        session.Recolor(EditorData.Id(1), EditorData.Blue);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.Document.Annotations[0].Color, Is.EqualTo(EditorData.Blue));

        session.Undo();

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.Document.Annotations[0].Color, Is.EqualTo(EditorData.Red));
        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));
    }

    [Test]
    public void RecolourReplacesTheColourAndKeepsTheAlphaOfTheNewColourAsGiven()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));

        session.Recolor(EditorData.Id(1), new RgbaColor(10, 20, 30, 128));

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.Document.Annotations[0].Color, Is.EqualTo(new RgbaColor(10, 20, 30, 128)));
    }

    [Test]
    public void SetThicknessReplacesTheThickness()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10, thickness: 4));

        session.SetThickness(EditorData.Id(1), 12);

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.Document.Annotations[0].Thickness, Is.EqualTo(12));
    }

    [Test]
    public void SelectingIsNotAHistoryStep()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.MarkSaved(@"C:\a.png");

        session.Select(EditorData.Id(1));

        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));
        Assert.That(session.IsDirty, Is.False, "choosing a shape changes nothing to save");
        session.Undo();
        Assert.That(session.Document.Annotations, Is.Empty, "the one Undo took back the Add, not the selection");
        Assert.That(session.Undo(), Is.False);
    }

    [Test]
    public void DeletingTheSelectedShapeClearsTheSelection()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Select(EditorData.Id(1));
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));

        session.Delete(EditorData.Id(1));

        Assert.That(session.Document.Annotations, Is.Empty);
        Assert.That(session.SelectedId, Is.Null);
    }

    [Test]
    public void UndoingTheAddOfTheSelectedShapeClearsTheSelection()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Select(EditorData.Id(1));
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));

        session.Undo();

        Assert.That(session.Document.Annotations, Is.Empty);
        Assert.That(session.SelectedId, Is.Null);
    }

    [Test]
    public void ChangingAnIdThatIsNotInTheImageChangesNothingAndIsNoStep()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.MarkSaved(@"C:\a.png");

        session.Move(EditorData.Id(99), 5, 5);
        session.Recolor(EditorData.Id(99), EditorData.Blue);
        session.SetThickness(EditorData.Id(99), 3);
        session.Delete(EditorData.Id(99));

        Assert.That(session.IsDirty, Is.False, "no step was recorded");
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(session.Document.Annotations[0], Is.EqualTo(EditorData.Rect(1, 0, 0, 10, 10)));
    }

    // ---- Move ----

    [Test]
    public void DrawingAShapeMovingItAndUndoPutsItBackWhereItWasDrawn()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 10, 20, 30, 40));

        session.Move(EditorData.Id(1), 5, 7);
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1));
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(15, 27, 30, 40)));
        session.Undo();

        Assert.That(session.Document.Annotations, Has.Count.EqualTo(1), "the shape is not lost");
        Assert.That(((RectangleAnnotation)session.Document.Annotations[0]).Bounds, Is.EqualTo(new PixelRect(10, 20, 30, 40)));
    }

    [Test]
    public void MoveShiftsEveryCoordinateOfEveryKindOfShape()
    {
        var session = _fixture.NewSession(400, 300);
        session.Add(new StrokeAnnotation(EditorData.Id(1), EditorData.Red, 4, [new PixelPoint(1, 2), new PixelPoint(3, 4)], false));
        session.Add(new LineAnnotation(EditorData.Id(2), EditorData.Red, 4, new PixelPoint(10, 20), new PixelPoint(30, 40)));
        session.Add(new ArrowAnnotation(EditorData.Id(3), EditorData.Red, 4, new PixelPoint(100, 100), new PixelPoint(300, 200)));
        session.Add(EditorData.Rect(4, 5, 6, 7, 8));
        session.Add(new EllipseAnnotation(EditorData.Id(5), EditorData.Red, 4, new PixelRect(9, 10, 11, 12)));
        session.Add(new TextAnnotation(EditorData.Id(6), EditorData.Red, 1, new PixelPoint(50, 60), "abc", 18));
        session.Add(EditorData.Step(7, 1, 70, 80));
        session.Add(new BlurAnnotation(EditorData.Id(8), EditorData.Red, 1, new PixelRect(13, 14, 15, 16)));

        // Moving by (+10, -1): every coordinate shifts, sizes do not change.
        for (var n = 1; n <= 8; n++)
        {
            session.Move(EditorData.Id(n), 10, -1);
        }

        var doc = session.Document.Annotations;
        Assert.That(doc, Has.Count.EqualTo(8));
        Assert.That(((StrokeAnnotation)doc[0]).Points, Is.EqualTo(new[] { new PixelPoint(11, 1), new PixelPoint(13, 3) }));
        Assert.That(((LineAnnotation)doc[1]).From, Is.EqualTo(new PixelPoint(20, 19)));
        Assert.That(((LineAnnotation)doc[1]).To, Is.EqualTo(new PixelPoint(40, 39)));
        Assert.That(((ArrowAnnotation)doc[2]).From, Is.EqualTo(new PixelPoint(110, 99)));
        Assert.That(((ArrowAnnotation)doc[2]).To, Is.EqualTo(new PixelPoint(310, 199)));
        Assert.That(((RectangleAnnotation)doc[3]).Bounds, Is.EqualTo(new PixelRect(15, 5, 7, 8)));
        Assert.That(((EllipseAnnotation)doc[4]).Bounds, Is.EqualTo(new PixelRect(19, 9, 11, 12)));
        Assert.That(((TextAnnotation)doc[5]).Origin, Is.EqualTo(new PixelPoint(60, 59)));
        Assert.That(((StepAnnotation)doc[6]).Center, Is.EqualTo(new PixelPoint(80, 79)));
        Assert.That(((BlurAnnotation)doc[7]).Area, Is.EqualTo(new PixelRect(23, 13, 15, 16)));
    }

    // ---- IsDirty ----

    [Test]
    public void AFreshCaptureIsNotDirtyAndKeepsTheGivenSourcePath()
    {
        var session = _fixture.Interactor.Open(EditorData.Blank(10, 10), @"C:\Pictures\a.png");

        Assert.That(session.IsDirty, Is.False);
        Assert.That(session.SourcePath, Is.EqualTo(@"C:\Pictures\a.png"));
        Assert.That(session.CanUndo, Is.False);
        Assert.That(session.CanRedo, Is.False);
    }

    [Test]
    public void ADrawnShapeMakesTheImageDirtyAndMarkSavedSetsThePathAndCleansIt()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        Assert.That(session.IsDirty, Is.True);

        session.MarkSaved(@"C:\Pictures\b.png");

        Assert.That(session.IsDirty, Is.False);
        Assert.That(session.SourcePath, Is.EqualTo(@"C:\Pictures\b.png"));
    }

    [Test]
    public void UndoingBackToTheSavedPositionIsCleanAndGoingPastItIsDirtyAgain()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.MarkSaved(@"C:\Pictures\b.png");
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        Assert.That(session.IsDirty, Is.True);

        session.Undo();
        Assert.That(session.IsDirty, Is.False, "back at the saved picture");

        session.Undo();
        Assert.That(session.IsDirty, Is.True, "one step before the saved picture");

        session.Redo();
        Assert.That(session.IsDirty, Is.False, "forward to the saved picture again");
    }

    [Test]
    public void DroppingTheBranchThatHeldTheSavedPictureLeavesTheImageDirtyForGood()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.MarkSaved(@"C:\Pictures\b.png");
        session.Undo();
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));

        Assert.That(session.IsDirty, Is.True);
        session.Undo();
        Assert.That(session.IsDirty, Is.True, "the saved picture (with shape 1) is gone for good; the empty picture is not it");
    }

    // ---- F6 ----

    [Test]
    public void F6_UndoAndRedoWithNothingToDoReturnFalseChangeNothingAndDoNotThrow()
    {
        var session = _fixture.NewSession();
        Assert.That(session.CanUndo, Is.False);
        Assert.That(session.CanRedo, Is.False);
        Assert.That(() => session.Undo(), Throws.Nothing);
        Assert.That(session.Undo(), Is.False);
        Assert.That(session.Redo(), Is.False);

        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        Assert.That(session.CanUndo, Is.True);
        Assert.That(session.Undo(), Is.True);
        var before = session.Document;

        Assert.That(session.CanUndo, Is.False, "the only step is undone: the Undo button goes dim");
        Assert.That(session.Undo(), Is.False);
        Assert.That(session.CanRedo, Is.True);
        Assert.That(session.Redo(), Is.True);
        Assert.That(session.CanRedo, Is.False, "the only undone step is redone: the Redo button goes dim");
        Assert.That(session.Redo(), Is.False);
        Assert.That(Ids(session), Is.EqualTo(new[] { EditorData.Id(1) }));
        Assert.That(before.Annotations, Is.Empty);
    }
}
