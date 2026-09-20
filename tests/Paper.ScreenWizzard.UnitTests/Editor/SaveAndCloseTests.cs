using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Editor.Fakes;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Editor.Models;

namespace Paper.ScreenWizzard.UnitTests.Editor;

/// <summary>SPEC editor, "Lưu và chép", F1, F7, F9: what Ctrl+S does, what a failed save keeps, what closing asks.</summary>
[TestFixture]
public sealed class SaveAndCloseTests
{
    private const string Folder = EditorData.SaveFolder;
    private const string AutoPng = Folder + @"\Screenshot 2026-09-20 14.03.05.png";
    private const string Original = @"C:\Users\Hung\Pictures\loi.png";
    private const string Copy = @"C:\Users\Hung\Pictures\loi copy.png";

    private EditorFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new EditorFixture();
        _fixture.Files.Directories.Add(Folder);
    }

    private static PixelImage Flat(int width = 40, int height = 30) => EditorData.Positional(width, height);

    // ---- Ctrl+S decisions ----

    [Test]
    public void AFreshCaptureCtrlSOpensSaveAsWithTheSaveFolderAndTheAutomaticName()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.AskSaveAs));
        Assert.That(decision.Folder, Is.EqualTo(Folder));
        Assert.That(decision.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05.png"));
        Assert.That(decision.Path, Is.Null);
    }

    [Test]
    public void TheSuggestedNameFollowsTheChosenFormatAndNeverPicksAFileThatIsAlreadyThere()
    {
        var session = _fixture.NewSession();
        _fixture.Files.Seed(Folder + @"\Screenshot 2026-09-20 14.03.05.jpg", [1]);

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings(ImageFormat.Jpg));

        Assert.That(decision.Action, Is.EqualTo(SaveAction.AskSaveAs));
        Assert.That(decision.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05 (2).jpg"));
    }

    [Test]
    public void AnImageOpenedFromAFileAsksOnceWhetherToOverwriteOrSaveACopy()
    {
        var session = _fixture.OpenFileOnDisk(Original, Flat());
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.AskOverwriteOrCopy));
        Assert.That(decision.Path, Is.EqualTo(Original));
        Assert.That(_fixture.Files.WrittenPaths, Is.Empty, "asking writes nothing");
    }

    [Test]
    public void AfterOverwritingOnceEveryLaterCtrlSWritesStraightToTheFileWithoutAsking()
    {
        var session = _fixture.OpenFileOnDisk(Original, Flat());
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        var first = _fixture.Interactor.Save(session, Flat(), Original, EditorData.Settings());
        Assert.That(first.Saved, Is.True);
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.WriteDirect));
        Assert.That(decision.Path, Is.EqualTo(Original));
        Assert.That(_fixture.Files.WrittenPaths, Is.EqualTo(new[] { Original }), "only the first save wrote; deciding writes nothing");
    }

    [Test]
    public void AfterSavingAFreshCaptureUnderANameCtrlSWritesStraightToThatName()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        _fixture.Interactor.Save(session, Flat(), AutoPng, EditorData.Settings());
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.WriteDirect));
        Assert.That(decision.Path, Is.EqualTo(AutoPng));
    }

    [Test]
    public void SavingACopyUnderAnotherNameMakesThatNameTheOneCtrlSWritesTo()
    {
        var session = _fixture.OpenFileOnDisk(Original, Flat());
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        _fixture.Files.Directories.Add(@"C:\Users\Hung\Pictures");

        var saved = _fixture.Interactor.Save(session, Flat(), Copy, EditorData.Settings());
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(saved.Saved, Is.True);
        Assert.That(session.SourcePath, Is.EqualTo(Copy));
        Assert.That(decision.Action, Is.EqualTo(SaveAction.WriteDirect));
        Assert.That(decision.Path, Is.EqualTo(Copy));
        Assert.That(_fixture.Files.Files[Original], Is.EqualTo(new byte[] { 9, 9, 9 }), "the original file was not touched");
    }

    [TestCase("changed before the question", false, false, TestName = "F9_TheFileWasRewrittenBeforeTheOverwriteQuestion")]
    [TestCase("changed after overwrite was agreed", true, false, TestName = "F9_TheFileWasRewrittenAfterOverwriteWasAgreed")]
    [TestCase("deleted after overwrite was agreed", true, true, TestName = "F9_TheFileWasDeletedAfterOverwriteWasAgreed")]
    public void F9_WhenTheOriginalWasChangedOrRemovedByAnotherProgramNothingIsWrittenAndSaveAsIsOffered(string why, bool agreedBefore, bool deleted)
    {
        var session = _fixture.OpenFileOnDisk(Original, Flat());
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        if (agreedBefore)
        {
            _fixture.Interactor.Save(session, Flat(), Original, EditorData.Settings());
            session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        }

        var writesBefore = _fixture.Files.WrittenPaths.Count;
        if (deleted)
        {
            _fixture.Files.Remove(Original);
        }
        else
        {
            _fixture.Files.ChangeBehindTheBack(Original);
        }

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.FileChangedOnDisk), why);
        Assert.That(decision.Path, Is.EqualTo(Original), "the file the message names");
        Assert.That(decision.Folder, Is.EqualTo(Folder), "the Save as suggestion");
        Assert.That(decision.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05.png"));
        Assert.That(_fixture.Files.WrittenPaths, Has.Count.EqualTo(writesBefore), "nothing is written, not even somewhere else");
        Assert.That(session.IsDirty, Is.True, "the edits stay");
    }

    // ---- Writing ----

    [Test]
    public void ASuccessfulSaveWritesThePathMarksTheImageCleanAndGivesAToastWithThePath()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));

        var result = _fixture.Interactor.Save(session, Flat(40, 30), AutoPng, EditorData.Settings(jpgQuality: 70));

        Assert.That(result.Saved, Is.True);
        Assert.That(result.Path, Is.EqualTo(AutoPng));
        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message!.Key, Is.EqualTo("Editor.Saved"));
        Assert.That(result.Message.Arguments, Is.EqualTo(new[] { AutoPng }));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { AutoPng }));
        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Width, Is.EqualTo(40));
        Assert.That(_fixture.Codec.Calls[0].Height, Is.EqualTo(30));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Png));
        Assert.That(session.IsDirty, Is.False);
        Assert.That(session.SourcePath, Is.EqualTo(AutoPng));
    }

    [Test]
    public void ATransparentPixelSavedAsJpgIsWhiteAndThePixelSizeIsTheImagesOwn()
    {
        var session = _fixture.NewSession();
        var flattened = new PixelImage(2, 1, [10, 20, 30, 0, 10, 20, 30, 255]);
        var path = Folder + @"\a.jpg";

        var result = _fixture.Interactor.Save(session, flattened, path, EditorData.Settings(jpgQuality: 70));

        Assert.That(result.Saved, Is.True);
        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Jpg));
        Assert.That(_fixture.Codec.Calls[0].Quality, Is.EqualTo(70));
        Assert.That(_fixture.Codec.Calls[0].Width, Is.EqualTo(2));
        Assert.That(_fixture.Codec.Calls[0].Bgra, Is.EqualTo(new byte[] { 255, 255, 255, 255, 10, 20, 30, 255 }));
        Assert.That(flattened.Bgra, Is.EqualTo(new byte[] { 10, 20, 30, 0, 10, 20, 30, 255 }), "the image in memory keeps its transparency");
    }

    [Test]
    public void F1_ASaveThatFailsNamesThePathAndTheReasonKeepsEveryEditAndTheImageDirty()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        session.Add(EditorData.Rect(2, 20, 0, 10, 10));
        _fixture.Files.WriteFailure = "There is not enough space on the disk.";

        var result = _fixture.Interactor.Save(session, Flat(), AutoPng, EditorData.Settings());

        Assert.That(result.Saved, Is.False);
        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message!.Key, Is.EqualTo("Editor.SaveFailed"));
        Assert.That(result.Message.Arguments, Is.EqualTo(new[] { AutoPng, "There is not enough space on the disk." }));
        Assert.That(session.IsDirty, Is.True);
        Assert.That(session.SourcePath, Is.Null, "the image has still no file");
        Assert.That(session.Document.Annotations, Has.Count.EqualTo(2), "every edit remains");
        Assert.That(session.CanUndo, Is.True);
        Assert.That(_fixture.Interactor.DecideClose(session), Is.EqualTo(CloseAction.AskSaveDiscardCancel), "the window does not close on its own");
    }

    [Test]
    public void AFailedOverwriteDoesNotCountAsAgreedSoTheNextCtrlSStillAsks()
    {
        var session = _fixture.OpenFileOnDisk(Original, Flat());
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));
        _fixture.Files.WriteFailure = "The file is read-only.";
        var failed = _fixture.Interactor.Save(session, Flat(), Original, EditorData.Settings());
        Assert.That(failed.Saved, Is.False);
        _fixture.Files.WriteFailure = null;

        var decision = _fixture.Interactor.DecideSave(session, EditorData.Settings());

        Assert.That(decision.Action, Is.EqualTo(SaveAction.AskOverwriteOrCopy));
    }

    // ---- Copy ----

    [Test]
    public void CopyPutsTheFlattenedImageOnTheClipboardAtItsOwnPixelSizeAndToastsTheSize()
    {
        var flattened = Flat(40, 30);

        var result = _fixture.Interactor.Copy(flattened);

        Assert.That(result.Saved, Is.True);
        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message!.Key, Is.EqualTo("Editor.Copied"));
        Assert.That(result.Message.Arguments, Is.EqualTo(new[] { "40", "30" }));
        Assert.That(_fixture.Clipboard.SetCalls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Clipboard.SetCalls[0].Width, Is.EqualTo(40));
        Assert.That(_fixture.Clipboard.SetCalls[0].Height, Is.EqualTo(30));
        Assert.That(_fixture.Clipboard.SetCalls[0].Bgra, Is.EqualTo(flattened.Bgra));
    }

    [Test]
    public void ACopyThatFailsSaysWhy()
    {
        _fixture.Clipboard.SetResult = PortResultOf("Clipboard is held by another program.");

        var result = _fixture.Interactor.Copy(Flat());

        Assert.That(result.Saved, Is.False);
        Assert.That(result.Message, Is.Not.Null);
        Assert.That(result.Message!.Key, Is.EqualTo("Editor.CopyFailed"));
        Assert.That(result.Message.Arguments, Is.EqualTo(new[] { "Clipboard is held by another program." }));
    }

    // ---- Closing ----

    [Test]
    public void F7_ClosingWithUnsavedChangesAsksSaveDiscardOrCancel()
    {
        var session = _fixture.NewSession();
        session.Add(EditorData.Rect(1, 0, 0, 10, 10));

        var action = _fixture.Interactor.DecideClose(session);

        Assert.That(action, Is.EqualTo(CloseAction.AskSaveDiscardCancel));
    }

    [Test]
    public void ClosingWithNothingUnsavedJustCloses()
    {
        var fresh = _fixture.NewSession();
        var saved = _fixture.NewSession();
        saved.Add(EditorData.Rect(1, 0, 0, 10, 10));
        Assert.That(saved.IsDirty, Is.True, "the drawn shape is unsaved until MarkSaved");
        saved.MarkSaved(AutoPng);

        Assert.That(_fixture.Interactor.DecideClose(fresh), Is.EqualTo(CloseAction.Close));
        Assert.That(_fixture.Interactor.DecideClose(saved), Is.EqualTo(CloseAction.Close));
    }

    private static PortResult PortResultOf(string detail) => PortResult.Fail(detail);
}
