using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.UseCases;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture, "Ảnh đi ra đúng như đã chụp": the file name rule, the white JPG, the failures that name their reason.</summary>
[TestFixture]
public sealed class ImageDeliveryTests
{
    private const string Folder = @"C:\Users\Hung\Pictures\Paper.ScreenWizzard";
    private const string BaseName = @"C:\Users\Hung\Pictures\Paper.ScreenWizzard\Screenshot 2026-09-20 14.03.05";

    private CaptureFixture _fixture = null!;
    private ImageDelivery _delivery = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new CaptureFixture();
        _fixture.Files.Directories.Add(Folder);
        _delivery = _fixture.Delivery();
    }

    // ---- an encoder that runs out of memory ----

    [Test]
    public void AnEncoderThatRunsOutOfMemorySavesNothingAndNamesTheReasonInsteadOfCrashing()
    {
        _fixture.Codec.EncodeFailure = new OutOfMemoryException("Insufficient memory to continue the execution of the program.");

        var result = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.EncodeFailed));
        Assert.That(result.Detail, Does.Contain("memory"));
        Assert.That(_fixture.Files.Files, Is.Empty, "no half-written file");
    }

    // ---- Naming ----

    [Test]
    public void APngOf20260920At140305IsNamedScreenshot20260920140305Png()
    {
        var result = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.None));
        Assert.That(result.Path, Is.EqualTo(BaseName + ".png"));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { BaseName + ".png" }));
    }

    [Test]
    public void AJpgIsNamedWithTheJpgExtension()
    {
        var result = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Jpg, 90);

        Assert.That(result.Path, Is.EqualTo(BaseName + ".jpg"));
    }

    [Test]
    public void ASecondFileOfTheSameSecondIsNumberedTwoAndThenThreeAndTheOldOnesAreNotOverwritten()
    {
        var oldBytes = new byte[] { 9, 9, 9 };
        _fixture.Files.Files[BaseName + ".png"] = oldBytes;

        var second = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);
        var third = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);

        Assert.That(second.Path, Is.EqualTo(BaseName + " (2).png"));
        Assert.That(third.Path, Is.EqualTo(BaseName + " (3).png"));
        Assert.That(_fixture.Files.Files[BaseName + ".png"], Is.EqualTo(oldBytes), "the first file is untouched");
        Assert.That(_fixture.Files.WrittenPaths, Is.EqualTo(new[] { BaseName + " (2).png", BaseName + " (3).png" }), "the old name was never written to");
    }

    // ---- JPG has no transparency ----

    [Test]
    public void AJpgIsFlattenedOntoWhiteBeforeItIsEncodedAndTheCallersImageIsNotChanged()
    {
        // Two pixels: fully transparent, and fully opaque. Bytes are B, G, R, A.
        var image = new PixelImage(2, 1, [10, 20, 30, 0, 10, 20, 30, 255]);

        _delivery.SaveToFolder(image, Folder, ImageFormat.Jpg, 90);

        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Jpg));
        Assert.That(_fixture.Codec.Calls[0].Bgra, Is.EqualTo(new byte[] { 255, 255, 255, 255, 10, 20, 30, 255 }));
        Assert.That(image.Bgra, Is.EqualTo(new byte[] { 10, 20, 30, 0, 10, 20, 30, 255 }), "the source image keeps its transparency");
    }

    [Test]
    public void APartlyTransparentPixelIsBlendedWithWhiteInJpg()
    {
        // Alpha 128 of (B 10, G 20, R 30) over white: (c * 128 + 255 * 127) / 255 = 132, 137, 142 worked out by hand.
        var image = new PixelImage(1, 1, [10, 20, 30, 128]);

        _delivery.SaveToFolder(image, Folder, ImageFormat.Jpg, 90);

        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        var sent = _fixture.Codec.Calls[0].Bgra;
        Assert.That(sent[0], Is.EqualTo(132).Within(1));
        Assert.That(sent[1], Is.EqualTo(137).Within(1));
        Assert.That(sent[2], Is.EqualTo(142).Within(1));
        Assert.That(sent[3], Is.EqualTo(255));
    }

    [Test]
    public void APngKeepsTheTransparencyItWasGiven()
    {
        var image = new PixelImage(2, 1, [10, 20, 30, 0, 10, 20, 30, 255]);

        _delivery.SaveToFolder(image, Folder, ImageFormat.Png, 90);

        Assert.That(_fixture.Codec.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(ImageFormat.Png));
        Assert.That(_fixture.Codec.Calls[0].Bgra, Is.EqualTo(new byte[] { 10, 20, 30, 0, 10, 20, 30, 255 }));
    }

    // ---- Failures name their reason (F4, F5) ----

    [Test]
    public void AFolderThatIsNotThereIsNotCreatedAndTheResultSaysWhy()
    {
        _fixture.Files.Directories.Clear();

        var result = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.FolderNotWritable));
        Assert.That(result.Path, Is.Null);
        Assert.That(result.Detail, Is.Not.Empty);
        Assert.That(_fixture.Files.CreateDirectoryCalls, Is.Empty);
        Assert.That(_fixture.Files.Files, Is.Empty);
    }

    [Test]
    public void AFolderThatRefusesTheWriteGivesTheSystemsReason()
    {
        _fixture.Files.WriteFailure = "Access to the path is denied.";

        var result = _delivery.SaveToFolder(CaptureData.Ramp(4, 4), Folder, ImageFormat.Png, 90);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.FolderNotWritable));
        Assert.That(result.Detail, Is.EqualTo("Access to the path is denied."));
    }

    [Test]
    public void ABusyClipboardIsReportedWithItsReason()
    {
        _fixture.Clipboard.SetResult = PortResult.Fail("Clipboard is held by another program.");

        var result = _delivery.CopyToClipboard(CaptureData.Ramp(4, 4));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.ClipboardBusy));
        Assert.That(result.Detail, Is.EqualTo("Clipboard is held by another program."));
    }

    [Test]
    public void CopyGivesTheClipboardTheImageUntouched()
    {
        var image = CaptureData.Ramp(4, 4);

        var result = _delivery.CopyToClipboard(image);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.None));
        Assert.That(_fixture.Clipboard.SetCalls[0].Bgra, Is.EqualTo(image.Bgra));
    }

    // ---- Save to a chosen path ----

    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a.png", ImageFormat.Png)]
    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a.PNG", ImageFormat.Png)]
    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a.jpg", ImageFormat.Jpg)]
    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a.jpeg", ImageFormat.Jpg)]
    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a.bmp", ImageFormat.Png)]
    [TestCase(@"C:\Users\Hung\Pictures\Paper.ScreenWizzard\a", ImageFormat.Png)]
    public void SaveToPathWritesExactlyThatPathInTheFormatOfItsExtension(string path, ImageFormat expected)
    {
        var result = _delivery.SaveToPath(CaptureData.Ramp(4, 4), path, 70);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Path, Is.EqualTo(path));
        Assert.That(_fixture.Files.Files.Keys, Is.EqualTo(new[] { path }));
        Assert.That(_fixture.Codec.Calls[0].Format, Is.EqualTo(expected));
        Assert.That(_fixture.Codec.Calls[0].Quality, Is.EqualTo(70));
    }

    [Test]
    public void SaveToPathThatFailsGivesTheFolderProblemAndTheReason()
    {
        _fixture.Files.WriteFailure = "The disk is full.";

        var result = _delivery.SaveToPath(CaptureData.Ramp(4, 4), Folder + @"\a.png", 90);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Issue, Is.EqualTo(DeliveryIssue.FolderNotWritable));
        Assert.That(result.Detail, Is.EqualTo("The disk is full."));
    }
}
