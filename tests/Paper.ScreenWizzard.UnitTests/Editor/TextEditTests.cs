using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>
/// SPEC editor, "Chữ" (double-click edits a committed text; an emptied text is deleted) and "Số bước" (the font size of a text or a
/// step number changes after it is drawn). Every change is one history step; a wrong id or a wrong kind changes nothing.
/// </summary>
[TestFixture]
public sealed class TextEditTests
{
    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new EditorFixture();

    private static TextAnnotation Text(int id, string text, int fontSize = 18) =>
        new(EditorData.Id(id), EditorData.Red, 1, new PixelPoint(10, 10), text, fontSize);

    private static string? TextOf(IEditorSession session, int id) =>
        session.Document.Annotations.OfType<TextAnnotation>().FirstOrDefault(a => a.Id == EditorData.Id(id))?.Text;

    private static int? FontOf(IEditorSession session, int id) => session.Document.Annotations.Select(a => a switch
    {
        TextAnnotation t when t.Id == EditorData.Id(id) => (int?)t.FontSize,
        StepAnnotation s when s.Id == EditorData.Id(id) => s.FontSize,
        _ => null,
    }).FirstOrDefault(size => size is not null);

    private static int Count(IEditorSession session) => session.Document.Annotations.Count;

    // ---- SetText: a new text ----

    [Test]
    public void SetTextReplacesTheTextOfThatAnnotation()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.Add(Text(2, "Giữ nguyên"));

        session.SetText(EditorData.Id(1), "Van 2");

        Assert.That(TextOf(session, 1), Is.EqualTo("Van 2"));
        Assert.That(TextOf(session, 2), Is.EqualTo("Giữ nguyên"));
        Assert.That(Count(session), Is.EqualTo(2));
    }

    [Test]
    public void SetTextIsOneStepAndUndoGivesTheOldTextBack()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.SetText(EditorData.Id(1), "Van 2");

        var undone = session.Undo();

        Assert.That(undone, Is.True);
        Assert.That(TextOf(session, 1), Is.EqualTo("Đường ống"), "one Undo undoes the whole edit");
        Assert.That(session.Redo(), Is.True);
        Assert.That(TextOf(session, 1), Is.EqualTo("Van 2"));
    }

    [Test]
    public void SetTextKeepsTheSelection()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.Select(EditorData.Id(1));

        session.SetText(EditorData.Id(1), "Van 2");

        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));
    }

    [Test]
    public void SetTextMakesTheDocumentDirtyAndUndoToTheSavedStepMakesItCleanAgain()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.MarkSaved(@"C:\shots\a.png");
        Assert.That(session.IsDirty, Is.False, "precondition: just saved");

        session.SetText(EditorData.Id(1), "Van 2");
        var dirtyAfterEdit = session.IsDirty;
        session.Undo();

        Assert.That(dirtyAfterEdit, Is.True);
        Assert.That(session.IsDirty, Is.False);
    }

    // ---- SetText: an empty text deletes ----

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\t ")]
    [TestCase(null)]
    public void SetTextToNothingDeletesTheAnnotation(string? empty)
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.Add(Text(2, "Giữ nguyên"));

        session.SetText(EditorData.Id(1), empty!);

        Assert.That(TextOf(session, 1), Is.Null);
        Assert.That(Count(session), Is.EqualTo(1));
        Assert.That(TextOf(session, 2), Is.EqualTo("Giữ nguyên"));
    }

    [Test]
    public void DeletingByEmptyTextIsOneStepAndUndoBringsBackTheOldText()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.SetText(EditorData.Id(1), "");

        var undone = session.Undo();

        Assert.That(undone, Is.True);
        Assert.That(TextOf(session, 1), Is.EqualTo("Đường ống"));
        Assert.That(Count(session), Is.EqualTo(1));
        Assert.That(session.Redo(), Is.True);
        Assert.That(TextOf(session, 1), Is.Null, "Redo deletes it again");
    }

    [Test]
    public void DeletingByEmptyTextClearsTheSelection()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.Select(EditorData.Id(1));

        session.SetText(EditorData.Id(1), "  ");

        Assert.That(session.SelectedId, Is.Null);
    }

    // ---- SetText: what it must not touch ----

    [Test]
    public void SetTextOnAnotherKindOfAnnotationChangesNothing()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 10, 10, 50, 20));
        session.Add(EditorData.Step(2, 3));
        var before = session.Document;

        session.SetText(EditorData.Id(1), "Van 2");
        session.SetText(EditorData.Id(2), "Van 2");

        Assert.That(session.Document, Is.SameAs(before), "no new step, the same snapshot");
        Assert.That(session.Document.Annotations.Count, Is.EqualTo(2));
    }

    [Test]
    public void SetTextToNothingOnAnotherKindDoesNotDeleteIt()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 10, 10, 50, 20));
        var before = session.Document;

        session.SetText(EditorData.Id(1), "");

        Assert.That(session.Document, Is.SameAs(before));
        Assert.That(session.Document.Annotations.Count, Is.EqualTo(1));
    }

    [Test]
    public void SetTextOnAnUnknownIdChangesNothing()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        var before = session.Document;

        session.SetText(EditorData.Id(99), "Van 2");
        session.SetText(EditorData.Id(99), "");

        Assert.That(session.Document, Is.SameAs(before));
        Assert.That(TextOf(session, 1), Is.EqualTo("Đường ống"));
    }

    [Test]
    public void ANoOpSetTextDoesNotEndTheRedoBranch()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống"));
        session.Add(EditorData.Rect(2, 0, 0, 10, 10));
        session.Undo();

        session.SetText(EditorData.Id(99), "Van 2");

        Assert.That(session.CanRedo, Is.True);
    }

    // ---- SetFontSize ----

    [Test]
    public void SetFontSizeChangesTheSizeOfAText()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 18));

        session.SetFontSize(EditorData.Id(1), 30);

        Assert.That(FontOf(session, 1), Is.EqualTo(30));
        Assert.That(TextOf(session, 1), Is.EqualTo("Đường ống"), "only the size changes");
    }

    [Test]
    public void SetFontSizeChangesTheSizeOfAStepNumber()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Step(1, 3));

        session.SetFontSize(EditorData.Id(1), 24);

        Assert.That(FontOf(session, 1), Is.EqualTo(24));
        var step = session.Document.Annotations.OfType<StepAnnotation>().FirstOrDefault();
        Assert.That(step?.Number, Is.EqualTo(3), "the number is not renumbered");
    }

    [Test]
    public void SetFontSizeIsOneStepAndUndoRestoresTheOldSize()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 18));
        session.Add(EditorData.Step(2, 1));
        session.SetFontSize(EditorData.Id(1), 30);
        session.SetFontSize(EditorData.Id(2), 24);

        session.Undo();
        Assert.That(FontOf(session, 2), Is.EqualTo(18), "step number back to 18 after one Undo");
        Assert.That(FontOf(session, 1), Is.EqualTo(30));
        session.Undo();

        Assert.That(FontOf(session, 1), Is.EqualTo(18));
    }

    [TestCase(5, 6)]
    [TestCase(0, 6)]
    [TestCase(-20, 6)]
    [TestCase(6, 6)]
    [TestCase(7, 7)]
    [TestCase(199, 199)]
    [TestCase(200, 200)]
    [TestCase(201, 200)]
    [TestCase(5000, 200)]
    public void SetFontSizeIsClampedToSixAndTwoHundred(int asked, int expected)
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 18));

        session.SetFontSize(EditorData.Id(1), asked);

        Assert.That(FontOf(session, 1), Is.EqualTo(expected));
    }

    [Test]
    public void SetFontSizeToTheCurrentSizeIsNotAStep()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 18));
        session.MarkSaved(@"C:\shots\a.png");
        var before = session.Document;

        session.SetFontSize(EditorData.Id(1), 18);

        Assert.That(session.Document, Is.SameAs(before));
        Assert.That(session.IsDirty, Is.False);
    }

    [Test]
    public void SetFontSizeThatClampsToTheCurrentSizeIsNotAStep()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 200));
        var before = session.Document;

        session.SetFontSize(EditorData.Id(1), 500);

        Assert.That(session.Document, Is.SameAs(before));
    }

    [Test]
    public void SetFontSizeMakesTheDocumentDirtyAndUndoToTheSavedStepMakesItCleanAgain()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Step(1, 1));
        session.MarkSaved(@"C:\shots\a.png");

        session.SetFontSize(EditorData.Id(1), 40);
        var dirtyAfterEdit = session.IsDirty;
        session.Undo();

        Assert.That(dirtyAfterEdit, Is.True);
        Assert.That(session.IsDirty, Is.False);
    }

    [Test]
    public void SetFontSizeKeepsTheSelection()
    {
        var session = _fixture.NewSession();
        session.Add(Text(1, "Đường ống", 18));
        session.Select(EditorData.Id(1));

        session.SetFontSize(EditorData.Id(1), 30);

        Assert.That(session.SelectedId, Is.EqualTo(EditorData.Id(1)));
    }

    [Test]
    public void SetFontSizeOnAnotherKindOrAnUnknownIdChangesNothing()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 10, 10, 50, 20));
        session.Add(Text(2, "Đường ống", 18));
        var before = session.Document;

        session.SetFontSize(EditorData.Id(1), 40);
        session.SetFontSize(EditorData.Id(99), 40);

        Assert.That(session.Document, Is.SameAs(before));
        Assert.That(FontOf(session, 2), Is.EqualTo(18));
    }
}
