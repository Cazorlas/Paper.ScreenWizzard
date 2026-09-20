using NUnit.Framework;
using Paper.ScreenWizzard.App.ViewModels.Capture;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The "Đã chụp" dialog's logic on a fake interactor (no window): SPEC capture "Sau khi chụp" and rows F4, F5, F10. The dialog
/// holds its own image, so a button that fails must leave the image and the other buttons exactly as they were.
/// </summary>
[TestFixture]
public sealed class CaptureDoneViewModelTests
{
    private sealed record Rig(
        CaptureDoneViewModel ViewModel,
        FakeCaptureInteractor Interactor,
        FakeFileDialogs Dialogs,
        RecordingNotifications Notes,
        PixelImage Image,
        List<string> Events);

    private static Rig Create(ImageFormat format = ImageFormat.Png, int width = 1280, int height = 720)
    {
        var interactor = new FakeCaptureInteractor();
        var dialogs = new FakeFileDialogs();
        var notes = new RecordingNotifications();
        var image = CaptureTestData.Solid(width, height);
        var viewModel = new CaptureDoneViewModel(interactor, image, CaptureSettings.Default(format), dialogs, notes, new FakeLocalizer());
        var events = new List<string>();
        viewModel.CloseRequested += (_, _) => events.Add("closed");
        viewModel.EditRequested += _ => events.Add("edit");
        return new Rig(viewModel, interactor, dialogs, notes, image, events);
    }

    [Test]
    public void Dialog_ShowsTheImagesSizeAndTheFormatItWillBeSavedIn()
    {
        var png = Create(ImageFormat.Png);
        var jpg = Create(ImageFormat.Jpg, 640, 480);

        Assert.That(png.ViewModel.SizeText, Is.EqualTo("1280 × 720"));
        Assert.That(png.ViewModel.FormatText, Is.EqualTo("PNG"));
        Assert.That(jpg.ViewModel.SizeText, Is.EqualTo("640 × 480"));
        Assert.That(jpg.ViewModel.FormatText, Is.EqualTo("JPG"));
        Assert.That(png.ViewModel.HasError, Is.False);
    }

    [Test]
    public void Dialog_WhenOpened_HasWrittenAndCopiedNothing()
    {
        var rig = Create();

        Assert.That(rig.ViewModel.SizeText, Is.Not.Empty, "the dialog is really built, not an empty shell");
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Interactor.PathDeliveries, Is.Empty);
        Assert.That(rig.Events, Is.Empty);
    }

    [Test]
    public void Save_DeliversToAFileClosesTheDialogAndToastsWhereTheImageWent()
    {
        var rig = Create();
        var saved = NotificationMessage.Of("Capture.SavedToFile", @"C:\Pics\shot.png", "1280", "720");
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(true, false, @"C:\Pics\shot.png", saved);

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Interactor.Deliveries, Is.EqualTo(new[] { (rig.Image, CaptureDestination.File) }));
        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.Notes.Toasts, Is.EqualTo(new[] { saved }));
        Assert.That(rig.Notes.Errors, Is.Empty);
    }

    [Test]
    public void Copy_DeliversToTheClipboardClosesTheDialogAndToasts()
    {
        var rig = Create();
        var copied = NotificationMessage.Of("Capture.CopiedToClipboard", "1280", "720");
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(true, false, null, copied);

        rig.ViewModel.CopyCommand.Execute(null);

        Assert.That(rig.Interactor.Deliveries, Is.EqualTo(new[] { (rig.Image, CaptureDestination.Clipboard) }));
        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.Notes.Toasts, Is.EqualTo(new[] { copied }));
    }

    [Test]
    public void Save_DeliveredWithNoMessage_ClosesWithoutAToast()
    {
        var rig = Create();

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.Notes.Toasts, Is.Empty);
    }

    [Test]
    public void F4_SaveFails_TheDialogStaysWithTheImageAndShowsTheErrorInsideItself()
    {
        var rig = Create();
        var failure = NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access denied");
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, failure);

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Events, Is.Empty, "the dialog is not closed: the image would be lost");
        Assert.That(rig.ViewModel.HasError, Is.True);
        Assert.That(rig.ViewModel.ErrorText, Is.EqualTo(@"Capture.SaveFailed(D:\ReadOnly,access denied)"));
        Assert.That(rig.ViewModel.Image, Is.SameAs(rig.Image), "the image is still the very one that was captured");
        Assert.That(rig.Notes.Errors, Is.Empty, "the error is inside the dialog, not in a second box on top of it");
        Assert.That(rig.Notes.Toasts, Is.Empty);
        Assert.That(rig.ViewModel.SaveCommand.CanExecute(null), Is.True);
        Assert.That(rig.ViewModel.CopyCommand.CanExecute(null), Is.True);
        Assert.That(rig.ViewModel.EditCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void F4_AfterASaveFailure_AnotherButtonStillDeliversTheSameImage()
    {
        var rig = Create();
        rig.Interactor.DeliverAnswer = destination => destination == CaptureDestination.File
            ? new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access denied"))
            : new CaptureDeliveryResult(true, false, null, NotificationMessage.Of("Capture.CopiedToClipboard", "1280", "720"));
        rig.ViewModel.SaveCommand.Execute(null);

        rig.ViewModel.CopyCommand.Execute(null);

        Assert.That(rig.Interactor.Deliveries.Select(d => d.Image), Is.All.SameAs(rig.Image));
        Assert.That(rig.Interactor.Deliveries.Select(d => d.Destination), Is.EqualTo(new[] { CaptureDestination.File, CaptureDestination.Clipboard }));
        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.ViewModel.HasError, Is.False, "a new attempt begins with a clean dialog");
    }

    [Test]
    public void F5_CopyFails_TheDialogStaysWithTheImageAndShowsTheErrorInsideItself()
    {
        var rig = Create();
        var failure = NotificationMessage.Of("Capture.ClipboardFailed", "the clipboard is held by another program");
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, failure);

        rig.ViewModel.CopyCommand.Execute(null);

        Assert.That(rig.Events, Is.Empty);
        Assert.That(rig.ViewModel.ErrorText, Is.EqualTo("Capture.ClipboardFailed(the clipboard is held by another program)"));
        Assert.That(rig.ViewModel.Image, Is.SameAs(rig.Image));
        Assert.That(rig.Notes.Errors, Is.Empty);
        Assert.That(rig.Notes.Toasts, Is.Empty);
    }

    [Test]
    public void F5_ThenSaveWorks_SoTheUserIsNotStuck()
    {
        var rig = Create();
        rig.Interactor.DeliverAnswer = destination => destination == CaptureDestination.Clipboard
            ? new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.ClipboardFailed", "locked"))
            : new CaptureDeliveryResult(true, false, @"C:\Pics\a.png", NotificationMessage.Of("Capture.SavedToFile", @"C:\Pics\a.png", "1280", "720"));
        rig.ViewModel.CopyCommand.Execute(null);

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.Notes.Toasts, Has.Count.EqualTo(1));
    }

    [Test]
    public void Failure_WithNoMessageFromTheUseCase_StillTellsTheUserSomething()
    {
        var rig = Create();
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, null);

        rig.ViewModel.SaveCommand.Execute(null);

        Assert.That(rig.ViewModel.HasError, Is.True, "a refusal nobody explains must not look like nothing happened");
        Assert.That(rig.ViewModel.ErrorText, Is.Not.Empty);
        Assert.That(rig.Events, Is.Empty);
    }

    [Test]
    public void SaveAs_AsksForAPathStartingFromTheSuggestionAndDeliversToIt()
    {
        var rig = Create();
        rig.Dialogs.Answer = @"D:\Chosen\mine.png";
        var saved = NotificationMessage.Of("Capture.SavedToFile", @"D:\Chosen\mine.png", "1280", "720");
        rig.Interactor.PathAnswer = new CaptureDeliveryResult(true, false, @"D:\Chosen\mine.png", saved);

        rig.ViewModel.SaveAsCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Is.EqualTo(new[] { (rig.Interactor.SaveAsAnswer.Folder, rig.Interactor.SaveAsAnswer.FileName) }));
        Assert.That(rig.Interactor.PathDeliveries, Is.EqualTo(new[] { (rig.Image, @"D:\Chosen\mine.png") }));
        Assert.That(rig.Interactor.Deliveries, Is.Empty, "Save as goes through the path, not through the default folder");
        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }));
        Assert.That(rig.Notes.Toasts, Is.EqualTo(new[] { saved }));
    }

    [Test]
    public void SaveAs_TheUserCancelsTheBox_ReturnsToTheDialogWithTheImageStillThere()
    {
        var rig = Create();
        rig.Dialogs.Answer = null;

        rig.ViewModel.SaveAsCommand.Execute(null);

        Assert.That(rig.Dialogs.Asked, Has.Count.EqualTo(1), "the box was opened, and the user backed out of it");
        Assert.That(rig.Interactor.PathDeliveries, Is.Empty, "nothing is written when the user backs out");
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Events, Is.Empty, "back to the \"Đã chụp\" dialog");
        Assert.That(rig.ViewModel.HasError, Is.False, "cancelling is not an error");
        Assert.That(rig.ViewModel.Image, Is.SameAs(rig.Image));
        Assert.That(rig.ViewModel.SaveAsCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void SaveAs_TheWriteFails_TheDialogStaysWithTheErrorLikeSave()
    {
        var rig = Create();
        rig.Dialogs.Answer = @"Z:\nowhere\x.png";
        rig.Interactor.PathAnswer = new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.SaveFailed", @"Z:\nowhere\x.png", "path not found"));

        rig.ViewModel.SaveAsCommand.Execute(null);

        Assert.That(rig.Events, Is.Empty);
        Assert.That(rig.ViewModel.ErrorText ?? string.Empty, Does.Contain("path not found"));
        Assert.That(rig.ViewModel.Image, Is.SameAs(rig.Image));
    }

    [Test]
    public void Edit_RaisesEditRequestedWithTheImageAndClosesWithoutDeliveringAnything()
    {
        var rig = Create();
        PixelImage? sent = null;
        rig.ViewModel.EditRequested += image => sent = image;

        rig.ViewModel.EditCommand.Execute(null);

        Assert.That(sent, Is.SameAs(rig.Image));
        Assert.That(rig.Events, Is.EqualTo(new[] { "edit", "closed" }), "the editor gets the image, then the dialog goes");
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Interactor.PathDeliveries, Is.Empty);
    }

    [Test]
    public void F10_Discard_ClosesAndDeliversNothingAtAll()
    {
        var rig = Create();

        rig.ViewModel.DiscardCommand.Execute(null);

        Assert.That(rig.Events, Is.EqualTo(new[] { "closed" }), "no editor either: \"edit\" must not appear");
        Assert.That(rig.Interactor.Deliveries, Is.Empty, "no file, no clipboard");
        Assert.That(rig.Interactor.PathDeliveries, Is.Empty);
        Assert.That(rig.Notes.Toasts, Is.Empty, "F10: no message when the user chose to throw it away");
        Assert.That(rig.Notes.Errors, Is.Empty);
    }

    [Test]
    public void TwoDialogs_EachHoldTheirOwnImageAndDoNotShareState()
    {
        var first = Create(width: 640, height: 480);
        var second = Create(width: 1920, height: 1080);
        first.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.SaveFailed", "x", "y"));

        first.ViewModel.SaveCommand.Execute(null);

        Assert.That(first.ViewModel.HasError, Is.True);
        Assert.That(second.ViewModel.HasError, Is.False, "an error in one dialog is not the other's");
        Assert.That(first.ViewModel.SizeText, Is.EqualTo("640 × 480"));
        Assert.That(second.ViewModel.SizeText, Is.EqualTo("1920 × 1080"));
        Assert.That(second.Interactor.Deliveries, Is.Empty);
    }
}
