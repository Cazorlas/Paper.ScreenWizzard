using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// Save, copy, close and open on the view model and the fakes (plan T13): what the user is asked, what is written, and what the window
/// does when it goes wrong. The decisions (ask or write, ask on close) are the use case's canned answers; the prompts and the box are
/// fakes; the flattener is the real one, so the bitmap that reaches the use case is the real pixels.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorSaveFlowTests : UiTestBase
{
    private const string Original = @"C:\Pics\original.png";

    private static PixelPoint P(int x, int y) => new(x, y);

    private static EditorRig RigWithADrawing(string? sourcePath = null, PixelImage? image = null)
    {
        var rig = EditorRig.Create(image, sourcePath);
        rig.Draw(ToolKind.Rectangle, P(150, 100), P(250, 180));
        return rig;
    }

    [Test]
    public void Save_ANewImage_OpensSaveAsWithTheSuggestion_AndWritesTheFlattenedImage()
    {
        var rig = RigWithADrawing();
        rig.Dialogs.Answer = @"D:\Out\shot.png";

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Is.EqualTo(new[] { (FakeEditorInteractor.Folder, FakeEditorInteractor.SuggestedName) }), "the box starts at the folder and name the use case suggests");
        var save = rig.Interactor.Saves.Only();
        Assert.That(save.Path, Is.EqualTo(@"D:\Out\shot.png"));
        Assert.That((save.Flattened.Width, save.Flattened.Height), Is.EqualTo((300, 200)), "the image's own size");
        Assert.That(EditorTestData.PixelIs(save.Flattened, 150, 140, EditorColors.Default), Is.True, "with the rectangle drawn into it");
        Assert.That(rig.Notes.Toasts.Select(m => m.Key), Is.EqualTo(new[] { "Editor.Saved" }), "the saved message");
        Assert.That(rig.Session.IsDirty, Is.False);
        Assert.That(rig.ViewModel.SaveStateText, Is.EqualTo("Editor.Status.Saved"));
    }

    [Test]
    public void Save_TheUserCancelsTheSaveAsBox_NothingHappens()
    {
        var rig = RigWithADrawing();
        rig.Dialogs.Answer = null;

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1), "the box was shown");
        Assert.That(rig.Interactor.Saves, Is.Empty);
        Assert.That(rig.Session.IsDirty, Is.True, "the edits are still unsaved");
        Assert.That(rig.Notes.Toasts.Concat(rig.Notes.Errors), Is.Empty, "and no message: the user cancelled");
    }

    [Test]
    public void Save_AFileOpenedFromDisk_AsksOnceAndThenWritesStraightAway()
    {
        var rig = RigWithADrawing(Original);
        rig.Interactor.Decisions.Enqueue(new SaveDecision(SaveAction.AskOverwriteOrCopy, @"C:\Pics", "original.png", Original));
        rig.Interactor.Decisions.Enqueue(new SaveDecision(SaveAction.WriteDirect, @"C:\Pics", "original.png", Original));
        rig.Prompts.OverwriteAnswer = OverwriteChoice.Overwrite;

        rig.ViewModel.SaveCommand.Execute(null);
        Assert.That(rig.Prompts.OverwriteAsked, Is.EqualTo(new[] { Original }), "SPEC: hỏi một lần");
        Assert.That(rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { Original }), "overwritten");

        rig.Draw(ToolKind.Ellipse, P(20, 20), P(90, 70));
        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Prompts.OverwriteAsked, Has.Count.EqualTo(1), "not asked again");
        Assert.That(rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { Original, Original }));
        Assert.That(rig.Dialogs.Asked, Is.Empty, "and no box either");
    }

    [Test]
    public void Save_ChoosingASecondFile_OpensTheSaveAsBoxAndWritesThere()
    {
        var rig = RigWithADrawing(Original);
        rig.Interactor.Decisions.Enqueue(new SaveDecision(SaveAction.AskOverwriteOrCopy, @"C:\Pics", "original.png", Original));
        rig.Prompts.OverwriteAnswer = OverwriteChoice.SaveCopy;
        rig.Dialogs.Answer = @"C:\Pics\copy.png";

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1));
        Assert.That(rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { @"C:\Pics\copy.png" }), "the original is not touched");
    }

    [Test]
    public void Save_CancellingTheOverwriteQuestion_WritesNothing()
    {
        var rig = RigWithADrawing(Original);
        rig.Interactor.Decisions.Enqueue(new SaveDecision(SaveAction.AskOverwriteOrCopy, @"C:\Pics", "original.png", Original));
        rig.Prompts.OverwriteAnswer = OverwriteChoice.Cancel;

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Prompts.OverwriteAsked, Has.Count.EqualTo(1), "it was asked");
        Assert.That(rig.Interactor.Saves, Is.Empty);
        Assert.That(rig.Dialogs.Asked, Is.Empty);
        Assert.That(rig.Session.IsDirty, Is.True);
    }

    [Test]
    public void F9_TheFileChangedOnDisk_TellsTheUserAndOffersSaveAsInsteadOfWritingOver()
    {
        var rig = RigWithADrawing(Original);
        rig.Interactor.Decisions.Enqueue(new SaveDecision(SaveAction.FileChangedOnDisk, @"C:\Pics", "original.png", Original));
        rig.Dialogs.Answer = @"C:\Pics\mine.png";

        rig.ViewModel.SaveCommand.Execute(null);

        var shown = rig.Notes.Errors.Concat(rig.Notes.Toasts).ToList();
        Assert.That(shown.Select(m => m.Key), Does.Contain("Editor.FileChangedOnDisk"), "names the file that changed");
        Assert.That(shown.First(m => m.Key == "Editor.FileChangedOnDisk").Arguments, Is.EqualTo(new[] { Original }));
        Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1), "and offers Lưu thành…");
        Assert.That(rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { @"C:\Pics\mine.png" }), "never silently over the original");
    }

    [Test]
    public void F1_ASaveThatFails_ShowsTheReasonAndKeepsEveryEditAndTheWindow()
    {
        var rig = RigWithADrawing();
        rig.Dialogs.Answer = @"D:\ReadOnly\shot.png";
        rig.Interactor.SaveFailsWith = NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\shot.png", "access is denied");

        rig.ViewModel.SaveCommand.Execute(null);

        var error = rig.Notes.Errors.Only();
        Assert.That(error.Key, Is.EqualTo("Editor.SaveFailed"));
        Assert.That(error.Arguments, Is.EqualTo(new[] { @"D:\ReadOnly\shot.png", "access is denied" }), "the path and the reason");
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the drawing is still there");
        Assert.That(rig.Session.IsDirty, Is.True, "still unsaved");
        Assert.That(rig.ViewModel.SaveStateText, Is.EqualTo("Editor.Status.Unsaved"));
        Assert.That(rig.Notes.Toasts, Is.Empty, "no success message for a failure");
    }

    [Test]
    public void F1_AFailureWithoutAMessage_StillTellsTheUserWhichFile()
    {
        var rig = RigWithADrawing();
        rig.Dialogs.Answer = @"D:\Out\shot.png";
        rig.Interactor.SaveFailsSilently = true;

        rig.ViewModel.SaveCommand.Execute(null);

        var error = rig.Notes.Errors.Only();
        Assert.That(error.Key, Is.EqualTo("Editor.SaveFailed"), "a refused save is never a silent nothing");
        Assert.That(error.Arguments[0], Is.EqualTo(@"D:\Out\shot.png"));
        Assert.That(rig.Session.IsDirty, Is.True);
    }

    [Test]
    public void Save_AtZoom400_StillWritesTheImagesOwnSize()
    {
        var rig = RigWithADrawing(image: EditorTestData.White(640, 360));
        rig.ViewModel.SetViewport(800, 600, 1.0);
        rig.ViewModel.SetZoom(4);
        rig.Dialogs.Answer = @"D:\Out\zoomed.png";

        rig.ViewModel.SaveCommand.Execute(null);

        var saved = rig.Interactor.Saves.Only().Flattened;
        Assert.That((saved.Width, saved.Height), Is.EqualTo((640, 360)), "SPEC: mức phóng 400% khi lưu → file vẫn đúng cỡ pixel của ảnh gốc");
    }

    [Test]
    public void SaveAs_AlwaysOpensTheBox_EvenForAFileThatWasOpenedFromDisk()
    {
        var rig = RigWithADrawing(Original);
        rig.Dialogs.Answer = @"C:\Pics\another.png";

        rig.ViewModel.SaveAsCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1));
        Assert.That(rig.Prompts.OverwriteAsked, Is.Empty, "no overwrite question: the user already chose to save under a new name");
        Assert.That(rig.Interactor.Saves.Select(s => s.Path), Is.EqualTo(new[] { @"C:\Pics\another.png" }));
    }

    [Test]
    public void Copy_PutsTheFlattenedImageOnTheClipboardAndSaysSo()
    {
        var rig = RigWithADrawing();

        rig.ViewModel.CopyCommand.Execute(null);

        var copy = rig.Interactor.Copies.Only();
        Assert.That((copy.Width, copy.Height), Is.EqualTo((300, 200)));
        Assert.That(EditorTestData.PixelIs(copy, 150, 140, EditorColors.Default), Is.True, "with the drawing in it");
        Assert.That(rig.Notes.Toasts.Select(m => m.Key), Is.EqualTo(new[] { "Editor.Copied" }));
        Assert.That(rig.Notes.Toasts.Only().Arguments, Is.EqualTo(new[] { "300", "200" }), "width then height");
    }

    [Test]
    public void Copy_DoesNotDependOnTheZoomTheWindowShows()
    {
        var atOne = RigWithADrawing();
        atOne.ViewModel.SetViewport(800, 600, 1.0);
        atOne.ViewModel.SetZoom(1);
        var atFour = RigWithADrawing();
        atFour.ViewModel.SetViewport(800, 600, 1.0);
        atFour.ViewModel.SetZoom(4);

        atOne.ViewModel.CopyCommand.Execute(null);
        atFour.ViewModel.CopyCommand.Execute(null);

        var a = atOne.Interactor.Copies.Only();
        var b = atFour.Interactor.Copies.Only();
        Assert.That((b.Width, b.Height), Is.EqualTo((a.Width, a.Height)));
        Assert.That(b.Bgra, Is.EqualTo(a.Bgra), "the very same pixels");
    }

    [Test]
    public void Copy_WhenTheClipboardIsHeld_ShowsTheReason()
    {
        var rig = RigWithADrawing();
        rig.Interactor.CopyFailsWith = NotificationMessage.Of("Editor.CopyFailed", "the clipboard is held by another program");

        rig.ViewModel.CopyCommand.Execute(null);

        Assert.That(rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Editor.CopyFailed" }));
        Assert.That(rig.Notes.Toasts, Is.Empty);
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "nothing lost");
    }

    [Test]
    public void F7_ClosingWithUnsavedEdits_AsksAndNeverClosesSilently()
    {
        var rig = RigWithADrawing();
        rig.Prompts.CloseAnswer = CloseChoice.Cancel;

        var allowed = rig.ViewModel.ConfirmClose();

        Assert.That(rig.Prompts.CloseAsked, Is.EqualTo(1), "asked");
        Assert.That(allowed, Is.False, "Cancel keeps the window");
        Assert.That(rig.Interactor.Saves, Is.Empty);
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the edits are still there");
    }

    [Test]
    public void F7_Discard_ClosesWithoutSaving()
    {
        var rig = RigWithADrawing();
        rig.Prompts.CloseAnswer = CloseChoice.Discard;

        Assert.That(rig.ViewModel.ConfirmClose(), Is.True);
        Assert.That(rig.Prompts.CloseAsked, Is.EqualTo(1));
        Assert.That(rig.Interactor.Saves, Is.Empty, "discard writes nothing");
    }

    [Test]
    public void F7_Save_ClosesOnlyWhenTheSaveWorked()
    {
        var works = RigWithADrawing();
        works.Prompts.CloseAnswer = CloseChoice.Save;
        works.Dialogs.Answer = @"D:\Out\a.png";

        Assert.That(works.ViewModel.ConfirmClose(), Is.True, "saved, so it may close");
        Assert.That(works.Interactor.Saves, Has.Count.EqualTo(1));

        var fails = RigWithADrawing();
        fails.Prompts.CloseAnswer = CloseChoice.Save;
        fails.Dialogs.Answer = @"D:\ReadOnly\a.png";
        fails.Interactor.SaveFailsWith = NotificationMessage.Of("Editor.SaveFailed", @"D:\ReadOnly\a.png", "access is denied");

        Assert.That(fails.ViewModel.ConfirmClose(), Is.False, "the save failed: the window stays with its edits");
        Assert.That(fails.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Editor.SaveFailed" }));

        var cancelsTheBox = RigWithADrawing();
        cancelsTheBox.Prompts.CloseAnswer = CloseChoice.Save;
        cancelsTheBox.Dialogs.Answer = null;

        Assert.That(cancelsTheBox.ViewModel.ConfirmClose(), Is.False, "the user backed out of Save as: nothing was saved, so nothing closes");
    }

    [Test]
    public void F7_NoUnsavedEdits_ClosesWithoutAsking()
    {
        var rig = EditorRig.Create(sourcePath: Original);

        Assert.That(rig.ViewModel.ConfirmClose(), Is.True);
        Assert.That(rig.Prompts.CloseAsked, Is.Zero, "nothing to lose, nothing to ask");

        // The same window after an edit does ask: the silence above is a decision, not a missing question.
        rig.Draw(ToolKind.Rectangle, P(10, 10), P(60, 60));
        rig.Prompts.CloseAnswer = CloseChoice.Cancel;
        Assert.That(rig.ViewModel.ConfirmClose(), Is.False);
        Assert.That(rig.Prompts.CloseAsked, Is.EqualTo(1));
    }

    // ---- opening: files, paste, drop (F2, F3, F8) ----

    [Test]
    public void F2_AFileThatIsNotAnImage_ShowsTheMessageAndOpensNoWindow()
    {
        var rig = EditorRig.Create();
        var message = NotificationMessage.Of("Editor.NotAnImage", @"C:\Docs\notes.txt");
        rig.Interactor.OpenFileAnswer = _ => new EditorOpenResult(null, EditorOpenIssue.NotAnImage, message);

        var opened = rig.Flow.OpenFile(@"C:\Docs\notes.txt");

        Assert.That(opened, Is.Null);
        Assert.That(rig.Views.Opened, Is.Empty, "no blank window");
        Assert.That(rig.Flow.OpenCount, Is.Zero);
        Assert.That(rig.Notes.Errors, Is.EqualTo(new[] { message }), "the file's name and 'không đọc được'");
    }

    [Test]
    public void F2_ARefusalWithNoMessageFromTheUseCase_StillNamesTheFile()
    {
        var rig = EditorRig.Create();
        rig.Interactor.OpenFileAnswer = _ => new EditorOpenResult(null, EditorOpenIssue.NotAnImage, null);

        rig.Flow.OpenFile(@"C:\Docs\notes.txt");

        var error = rig.Notes.Errors.Only();
        Assert.That(error.Key, Is.EqualTo("Editor.NotAnImage"));
        Assert.That(error.Arguments, Is.EqualTo(new[] { @"C:\Docs\notes.txt" }));
    }

    [Test]
    public void F8_AnImageTooLargeToOpen_ShowsTheMessageAndTheAppKeepsRunning()
    {
        var rig = EditorRig.Create();
        var message = NotificationMessage.Of("Editor.ImageTooLarge", @"C:\Pics\huge.png");
        rig.Interactor.OpenFileAnswer = _ => new EditorOpenResult(null, EditorOpenIssue.TooLarge, message);

        var opened = rig.Flow.OpenFile(@"C:\Pics\huge.png");

        Assert.That(opened, Is.Null);
        Assert.That(rig.Views.Opened, Is.Empty);
        Assert.That(rig.Notes.Errors, Is.EqualTo(new[] { message }));
    }

    [Test]
    public void Open_AGoodFile_OpensOneWindowWithItsOwnSessionAndTheFilesPath()
    {
        var rig = EditorRig.Create();
        var session = new FakeEditorSession(EditorTestData.White(64, 48), @"C:\Pics\ok.png");
        rig.Interactor.OpenFileAnswer = _ => new EditorOpenResult(session, EditorOpenIssue.None, null);

        var opened = rig.Flow.OpenFile(@"C:\Pics\ok.png");

        Assert.That(opened, Is.Not.Null);
        Assert.That(opened!.Session, Is.SameAs(session));
        Assert.That(rig.Views.OpenCount, Is.EqualTo(1));
        Assert.That(rig.Flow.OpenCount, Is.EqualTo(1));
        Assert.That(rig.Notes.Errors.Concat(rig.Notes.Toasts), Is.Empty);
    }

    [Test]
    public void Open_TwoImages_GiveTwoWindowsEachWithItsOwnImage_AndClosingOneLeavesTheOther()
    {
        var rig = EditorRig.Create();
        var first = rig.Flow.Open(EditorTestData.White(100, 80), null);
        var second = rig.Flow.Open(EditorTestData.White(200, 160), null);

        Assert.That(rig.Flow.OpenCount, Is.EqualTo(2));
        Assert.That(first!.SizeText, Is.EqualTo("100 × 80"));
        Assert.That(second!.SizeText, Is.EqualTo("200 × 160"));

        rig.Views.Opened[0].Close();

        Assert.That(rig.Flow.OpenCount, Is.EqualTo(1), "SPEC: mỗi cửa sổ sửa giữ một ảnh");
    }

    [Test]
    public void F3_PasteWithNoImageOnTheClipboard_ShowsTheMessageAndLeavesTheImageAlone()
    {
        var rig = RigWithADrawing();
        var message = NotificationMessage.Of("Editor.ClipboardHasNoImage");
        rig.Interactor.ClipboardAnswer = () => new EditorOpenResult(null, EditorOpenIssue.ClipboardHasNoImage, message);

        rig.ViewModel.PasteCommand.Execute(null);

        Assert.That(rig.Interactor.ClipboardOpened, Is.EqualTo(1), "the clipboard was asked");
        Assert.That(rig.Notes.Toasts.Concat(rig.Notes.Errors), Is.EqualTo(new[] { message }));
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1), "the image being edited did not change");
        Assert.That(rig.Session.Document.Source.Width, Is.EqualTo(300));
        Assert.That(rig.Views.Opened, Is.Empty, "and no window opened");
    }

    [Test]
    public void Paste_AnImageOnTheClipboard_OpensItInANewWindowAndLeavesThisOneAlone()
    {
        var rig = RigWithADrawing();
        var pasted = new FakeEditorSession(EditorTestData.White(50, 40));
        rig.Interactor.ClipboardAnswer = () => new EditorOpenResult(pasted, EditorOpenIssue.None, null);

        rig.ViewModel.PasteCommand.Execute(null);

        Assert.That(rig.Views.Opened.Select(o => o.ViewModel.Session), Is.EqualTo(new[] { pasted }), "SPEC: mở ảnh khác thì mở cửa sổ mới");
        Assert.That(rig.Session.Document.Annotations, Has.Count.EqualTo(1));
    }

    [Test]
    public void Drop_AFileOnTheWindow_OpensItThroughTheSameFlow()
    {
        var rig = EditorRig.Create();

        rig.ViewModel.OpenFileCommand.Execute(@"C:\Pics\dropped.png");

        Assert.That(rig.Interactor.FilesOpened, Is.EqualTo(new[] { @"C:\Pics\dropped.png" }));
        Assert.That(rig.Views.OpenCount, Is.EqualTo(1));
    }
}
