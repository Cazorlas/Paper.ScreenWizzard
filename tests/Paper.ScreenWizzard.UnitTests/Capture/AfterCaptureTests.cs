using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture: "Sau khi chụp", "Ảnh đi ra đúng như đã chụp", F4, F5.</summary>
[TestFixture]
public sealed class AfterCaptureTests
{
    private const string PngPath = @"C:\Users\Hung\Pictures\Paper.ScreenWizzard\Screenshot 2026-09-20 14.03.05.png";

    private CaptureFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new CaptureFixture();
        _fixture.Files.Directories.Add(CaptureData.SaveFolder);
    }

    // ---- The request each key makes ----

    [Test]
    public void TheRequestCarriesTheKindTheDelayTheCursorFlagAndTheFullScreenScopeOfTheSettings()
    {
        var settings = CaptureData.Settings(delaySeconds: 5, includeCursor: true, scope: FullScreenScope.AllMonitors);

        var request = _fixture.Interactor.RequestFor(CaptureKind.Freeform, settings);

        Assert.That(request, Is.EqualTo(new CaptureRequest(CaptureKind.Freeform, 5, true, FullScreenScope.AllMonitors)));
    }

    [Test]
    public void TheRequestOfTheDefaultSettingsHasNoDelayNoCursorAndTheMonitorUnderTheCursor()
    {
        var request = _fixture.Interactor.RequestFor(CaptureKind.Window, CaptureData.Settings());

        Assert.That(request, Is.EqualTo(new CaptureRequest(CaptureKind.Window, 0, false, FullScreenScope.MonitorUnderCursor)));
    }

    // ---- Where a fresh image goes ----

    [Test]
    public async Task TheDefaultSettingsShowTheDialogAndNothingIsWrittenCopiedOrOpened()
    {
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));
        var outcome = session.CompleteRectangle(new PixelPoint(300, 250), new PixelPoint(500, 400));

        var plan = _fixture.Interactor.PlanAfterCapture(CaptureData.Settings());

        Assert.That(outcome.Image, Is.Not.Null);
        Assert.That(plan.ShowDialog, Is.True);
        Assert.That(plan.Destinations, Is.Empty, "no editor, clipboard or file yet");
        Assert.That(_fixture.Files.WrittenPaths, Is.Empty, "no file written");
        Assert.That(_fixture.Clipboard.SetCalls, Is.Empty, "clipboard untouched");
        Assert.That(_fixture.Codec.Calls, Is.Empty, "nothing encoded");
    }

    [TestCase(AfterCaptureAction.OpenEditor, new[] { CaptureDestination.Editor })]
    [TestCase(AfterCaptureAction.CopyToClipboard, new[] { CaptureDestination.Clipboard })]
    [TestCase(AfterCaptureAction.SaveToFile, new[] { CaptureDestination.File })]
    [TestCase(AfterCaptureAction.ClipboardAndFile, new[] { CaptureDestination.Clipboard, CaptureDestination.File })]
    public void WithoutTheDialogTheImageGoesStraightToTheChosenDestinations(AfterCaptureAction action, CaptureDestination[] expected)
    {
        var plan = _fixture.Interactor.PlanAfterCapture(CaptureData.Settings(after: action));

        Assert.That(plan.ShowDialog, Is.False);
        Assert.That(plan.Destinations, Is.EqualTo(expected));
    }

    // ---- Sao chép ----

    [Test]
    public void CopyPutsTheImageOnTheClipboardExactlyAndWritesNoFile()
    {
        var image = CaptureData.Ramp(200, 150);

        var result = _fixture.Interactor.Deliver(image, CaptureDestination.Clipboard, CaptureData.Settings());

        Assert.That(result.Delivered, Is.True);
        Assert.That(result.KeepDialogOpen, Is.False, "the dialog closes");
        Assert.That(result.Message?.Key, Is.EqualTo("Capture.CopiedToClipboard"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "200", "150" }));
        Assert.That(_fixture.Clipboard.SetCalls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Clipboard.SetCalls[0].Width, Is.EqualTo(200));
        Assert.That(_fixture.Clipboard.SetCalls[0].Height, Is.EqualTo(150));
        Assert.That(_fixture.Clipboard.SetCalls[0].Bgra, Is.EqualTo(image.Bgra));
        Assert.That(_fixture.Files.WrittenPaths, Is.Empty, "no file written");
    }

    // ---- Lưu ----

    [Test]
    public void SaveWritesOneFileNamedByTheClockIntoTheSaveFolderAndLeavesTheClipboardAlone()
    {
        var image = CaptureData.Ramp(200, 150);

        var result = _fixture.Interactor.Deliver(image, CaptureDestination.File, CaptureData.Settings());

        Assert.That(result.Delivered, Is.True);
        Assert.That(result.KeepDialogOpen, Is.False);
        Assert.That(result.Path, Is.EqualTo(PngPath));
        Assert.That(result.Message?.Key, Is.EqualTo("Capture.SavedToFile"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { PngPath, "200", "150" }));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { PngPath }));
        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Png));
        Assert.That(_fixture.Files.Files[PngPath], Is.EqualTo(_fixture.Codec.Calls[0].Result), "the bytes the codec made are the bytes written");
        Assert.That(_fixture.Clipboard.SetCalls, Is.Empty, "clipboard unchanged");
    }

    [Test]
    public void SaveAsJpgHandsTheCodecTheChosenQuality()
    {
        var result = _fixture.Interactor.Deliver(CaptureData.Ramp(20, 10), CaptureDestination.File, CaptureData.Settings(format: ImageFormat.Jpg, jpgQuality: 75));

        Assert.That(result.Path, Is.EqualTo(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\Screenshot 2026-09-20 14.03.05.jpg"));
        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Jpg));
        Assert.That(_fixture.Codec.Calls[0].Quality, Is.EqualTo(75));
    }

    // ---- Sửa ----

    [Test]
    public void EditDeliversNothingElseBecauseTheWindowOpensTheEditor()
    {
        var result = _fixture.Interactor.Deliver(CaptureData.Ramp(200, 150), CaptureDestination.Editor, CaptureData.Settings());

        Assert.That(result.Delivered, Is.True);
        Assert.That(result.KeepDialogOpen, Is.False);
        Assert.That(_fixture.Files.WrittenPaths, Is.Empty);
        Assert.That(_fixture.Clipboard.SetCalls, Is.Empty);
    }

    // ---- Straight to clipboard and file ----

    [Test]
    public void AClipboardAndFilePlanDeliversToBothAndOnlyToBoth()
    {
        var image = CaptureData.Ramp(200, 150);
        var settings = CaptureData.Settings(after: AfterCaptureAction.ClipboardAndFile);

        var plan = _fixture.Interactor.PlanAfterCapture(settings);
        var results = plan.Destinations.Select(d => _fixture.Interactor.Deliver(image, d, settings)).ToList();

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Delivered), Is.True);
        Assert.That(_fixture.Clipboard.SetCalls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { PngPath }));
    }

    // ---- Each dialog keeps its own image ----

    [Test]
    public async Task TwoDialogsEachDeliverTheirOwnImageWhateverWasCapturedInBetween()
    {
        var first = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));
        var firstImage = first.CompleteRectangle(new PixelPoint(300, 250), new PixelPoint(500, 400)).Image;
        var second = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));
        var secondImage = second.CompleteRectangle(new PixelPoint(0, 0), new PixelPoint(60, 40)).Image;
        Assert.That(firstImage, Is.Not.Null);
        Assert.That(secondImage, Is.Not.Null);

        var copyOfFirst = _fixture.Interactor.Deliver(firstImage!, CaptureDestination.Clipboard, CaptureData.Settings());
        var copyOfSecond = _fixture.Interactor.Deliver(secondImage!, CaptureDestination.File, CaptureData.Settings());

        Assert.That(copyOfFirst.Delivered, Is.True);
        Assert.That(copyOfSecond.Delivered, Is.True);
        Assert.That(_fixture.Clipboard.SetCalls[0].Width, Is.EqualTo(200), "the first dialog's image");
        Assert.That(_fixture.Clipboard.SetCalls[0].Height, Is.EqualTo(150));
        Assert.That(_fixture.Codec.Calls[0].Width, Is.EqualTo(60), "the second dialog's image");
        Assert.That(_fixture.Codec.Calls[0].Height, Is.EqualTo(40));
    }

    // ---- Lưu thành… ----

    [Test]
    public void SaveAsStartsInTheSaveFolderWithTheAutomaticName()
    {
        var suggestion = _fixture.Interactor.SuggestSaveAs(CaptureData.Settings());

        Assert.That(suggestion.Folder, Is.EqualTo(CaptureData.SaveFolder));
        Assert.That(suggestion.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05.png"));
    }

    [Test]
    public void SaveAsNeverSuggestsTheNameOfAnExistingFileAndFollowsTheFormat()
    {
        _fixture.Files.Files[PngPath] = [1];

        var taken = _fixture.Interactor.SuggestSaveAs(CaptureData.Settings());
        var jpg = _fixture.Interactor.SuggestSaveAs(CaptureData.Settings(format: ImageFormat.Jpg));

        Assert.That(taken.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05 (2).png"));
        Assert.That(jpg.FileName, Is.EqualTo("Screenshot 2026-09-20 14.03.05.jpg"));
    }

    [Test]
    public void SaveAsWritesExactlyTheChosenPathInTheFormatOfItsExtension()
    {
        _fixture.Files.Directories.Add(@"C:\Users\Hung\Desktop");
        var image = CaptureData.Ramp(200, 150);

        var result = _fixture.Interactor.DeliverToPath(image, @"C:\Users\Hung\Desktop\shot.jpg", CaptureData.Settings(jpgQuality: 80));

        Assert.That(result.Delivered, Is.True);
        Assert.That(result.KeepDialogOpen, Is.False);
        Assert.That(result.Path, Is.EqualTo(@"C:\Users\Hung\Desktop\shot.jpg"));
        Assert.That(result.Message?.Key, Is.EqualTo("Capture.SavedToFile"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { @"C:\Users\Hung\Desktop\shot.jpg", "200", "150" }));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { @"C:\Users\Hung\Desktop\shot.jpg" }));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Jpg));
        Assert.That(_fixture.Codec.Calls[0].Quality, Is.EqualTo(80));
    }

    [Test]
    public void SaveAsThatFailsNamesThePathKeepsTheDialogAndSaysWhy()
    {
        var result = _fixture.Interactor.DeliverToPath(CaptureData.Ramp(20, 10), @"D:\gone\shot.png", CaptureData.Settings());

        Assert.That(result.Delivered, Is.False);
        Assert.That(result.KeepDialogOpen, Is.True);
        Assert.That(result.Message?.Key, Is.EqualTo("Capture.SaveFailed"));
        Assert.That(result.Message?.Arguments[0], Is.EqualTo(@"D:\gone\shot.png"));
        Assert.That(result.Message?.Arguments[1], Is.Not.Empty, "the reason");
    }

    // ---- Freeform out of the dialog ----

    [Test]
    public async Task AFreeformCaptureSavedAsJpgIsWhiteOutsideTheOutlineNotBlack()
    {
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Freeform));
        var image = session.CompleteFreeform([new PixelPoint(100, 100), new PixelPoint(300, 100), new PixelPoint(200, 300)]).Image;
        Assert.That(image, Is.Not.Null);

        _fixture.Interactor.Deliver(image!, CaptureDestination.File, CaptureData.Settings(format: ImageFormat.Jpg));

        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        var sent = new PixelImage(_fixture.Codec.Calls[0].Width, _fixture.Codec.Calls[0].Height, _fixture.Codec.Calls[0].Bgra);
        Assert.That(CaptureData.PixelAt(sent, 10, 190), Is.EqualTo(new byte[] { 255, 255, 255, 255 }), "outside the outline: white");
        Assert.That(CaptureData.PixelAt(sent, 100, 50), Is.EqualTo(new byte[] { 0, 150, 200, 255 }), "inside: the screen, as it was");
    }

    [Test]
    public async Task AFreeformCaptureSavedAsPngKeepsItsTransparency()
    {
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Freeform));
        var image = session.CompleteFreeform([new PixelPoint(100, 100), new PixelPoint(300, 100), new PixelPoint(200, 300)]).Image;
        Assert.That(image, Is.Not.Null);

        _fixture.Interactor.Deliver(image!, CaptureDestination.File, CaptureData.Settings(format: ImageFormat.Png));

        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        var sent = new PixelImage(_fixture.Codec.Calls[0].Width, _fixture.Codec.Calls[0].Height, _fixture.Codec.Calls[0].Bgra);
        Assert.That(CaptureData.PixelAt(sent, 10, 190)[3], Is.EqualTo(0), "outside the outline: still transparent");
        Assert.That(CaptureData.PixelAt(sent, 100, 50)[3], Is.EqualTo(255));
    }

    // ---- F4 ----

    [Test]
    public void F4_SaveIntoAFolderThatIsMissingOrReadOnlyKeepsTheDialogNamesTheFolderAndTheReasonAndLosesNothing()
    {
        var image = CaptureData.Ramp(200, 150);
        var before = image.Bgra.ToArray();

        // The save folder does not exist (and is not created behind the user's back).
        _fixture.Files.Directories.Clear();
        var missing = _fixture.Interactor.Deliver(image, CaptureDestination.File, CaptureData.Settings());
        var createdNothing = _fixture.Files.CreateDirectoryCalls.Count == 0 && _fixture.Files.Files.Count == 0;

        // The save folder exists but Windows refuses the write.
        _fixture.Files.Directories.Add(CaptureData.SaveFolder);
        _fixture.Files.WriteFailure = "Access to the path is denied.";
        var readOnly = _fixture.Interactor.Deliver(image, CaptureDestination.File, CaptureData.Settings());

        Assert.Multiple(() =>
        {
            Assert.That(missing.Delivered, Is.False);
            Assert.That(missing.KeepDialogOpen, Is.True, "the dialog stays open");
            Assert.That(missing.Path, Is.Null);
            Assert.That(missing.Message?.Key, Is.EqualTo("Capture.SaveFailed"));
            Assert.That(missing.Message?.Arguments[0], Is.EqualTo(CaptureData.SaveFolder), "names the folder");
            Assert.That(missing.Message?.Arguments[1], Is.Not.Empty, "and the reason");
            Assert.That(createdNothing, Is.True, "no folder created, no file written");
            Assert.That(readOnly.Delivered, Is.False);
            Assert.That(readOnly.KeepDialogOpen, Is.True);
            Assert.That(readOnly.Message?.Key, Is.EqualTo("Capture.SaveFailed"));
            Assert.That(readOnly.Message?.Arguments, Is.EqualTo(new[] { CaptureData.SaveFolder, "Access to the path is denied." }));
            Assert.That(_fixture.Files.Files, Is.Empty, "no file was lost or half-written");
            Assert.That(image.Bgra, Is.EqualTo(before), "the image is intact for the next button");
        });
    }

    // ---- F5 ----

    [Test]
    public void F5_ABusyClipboardKeepsTheDialogSaysSoAndTheImageIsIntact()
    {
        var image = CaptureData.Ramp(200, 150);
        var before = image.Bgra.ToArray();
        _fixture.Clipboard.SetResult = PortResult.Fail("Clipboard is held by another program.");

        var result = _fixture.Interactor.Deliver(image, CaptureDestination.Clipboard, CaptureData.Settings());

        Assert.That(result.Delivered, Is.False);
        Assert.That(result.KeepDialogOpen, Is.True, "the dialog stays open");
        Assert.That(result.Message?.Key, Is.EqualTo("Capture.ClipboardFailed"));
        Assert.That(result.Message?.Arguments, Is.EqualTo(new[] { "Clipboard is held by another program." }));
        Assert.That(image.Bgra, Is.EqualTo(before), "the image is intact for the next button");
        Assert.That(_fixture.Files.WrittenPaths, Is.Empty, "and nothing went to a file instead");
    }
}
