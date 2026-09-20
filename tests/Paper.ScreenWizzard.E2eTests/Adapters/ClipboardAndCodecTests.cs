using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Common;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>The real clipboard (backed up first and put back after) and the real WPF image codecs.</summary>
[TestFixture]
[NonParallelizable]
public sealed class ClipboardAndCodecTests
{
    private ClipboardBackup? _backup;

    [SetUp]
    public void BackUpClipboard() => _backup = ClipboardBackup.Take();

    [TearDown]
    public void RestoreClipboard() => _backup?.Restore();

    // ---- clipboard ----

    [Test]
    public void Clipboard_SetImageThenGetImage_ReturnsIdenticalPixels()
    {
        var picture = Pixels.Pattern(64, 40);
        var service = new ClipboardService();

        var set = StaHost.Instance.Invoke(() => service.SetImage(picture));
        var read = StaHost.Instance.Invoke(service.GetImage);

        Assert.That(set.Success, Is.True, set.Detail);
        Assert.That(read.HasImage, Is.True, read.Detail);
        Assert.That((read.Image!.Width, read.Image.Height), Is.EqualTo((64, 40)));
        Assert.That(read.Image.Bgra, Is.EqualTo(picture.Bgra), "the pixels that come back are the pixels that went in");
    }

    [Test]
    public void Clipboard_WithoutAnImage_SaysSoInsteadOfThrowing()
    {
        var service = new ClipboardService();

        // Control: with a picture on the clipboard the same call finds one.
        StaHost.Instance.Invoke(() => service.SetImage(Pixels.Pattern(8, 8)));
        Assert.That(StaHost.Instance.Invoke(service.GetImage).HasImage, Is.True);

        StaHost.Instance.Invoke(() => System.Windows.Clipboard.SetText("just words " + Guid.NewGuid().ToString("N")));
        var read = StaHost.Instance.Invoke(service.GetImage);

        Assert.That(read.HasImage, Is.False);
        Assert.That(read.Image, Is.Null);
    }

    [Test]
    public void Clipboard_HeldByAnotherProgramForShortWhile_IsRetriedAndThenWorks()
    {
        var picture = Pixels.Pattern(16, 16);
        var holder = ClipboardHolder.Hold();
        var release = Task.Run(() =>
        {
            Thread.Sleep(400);
            holder.Dispose();
        });

        var result = StaHost.Instance.Invoke(() => new ClipboardService(tries: 5, delayMilliseconds: 100).SetImage(picture));
        release.Wait();

        Assert.That(result.Success, Is.True, "the service retried until the other program let go: " + result.Detail);
    }

    // ---- codec ----

    [Test]
    public void Png_RoundTrip_IsExactIncludingAlpha()
    {
        var codec = new ImageCodec();
        var picture = Pixels.Pattern(50, 30, alpha: 200);

        var bytes = codec.Encode(picture, ImageFormat.Png, 90);
        var decoded = codec.Decode(bytes);

        Assert.That(bytes.Take(8).ToArray(), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), "PNG signature");
        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.None));
        Assert.That((decoded.Image!.Width, decoded.Image.Height), Is.EqualTo((50, 30)));
        Assert.That(decoded.Image.Bgra, Is.EqualTo(picture.Bgra));
    }

    [Test]
    public void Jpg_RoundTrip_IsCloseAndQualityChangesTheSize()
    {
        var codec = new ImageCodec();

        // Big flat cells: JPEG smears colour across a cell edge (chroma is stored at half resolution), so only the middle of each cell is compared.
        var picture = Pixels.Pattern(96, 64, cell: 32);

        var high = codec.Encode(picture, ImageFormat.Jpg, 95);
        var low = codec.Encode(picture, ImageFormat.Jpg, 10);
        var decoded = codec.Decode(high);

        Assert.That(high.Take(2).ToArray(), Is.EqualTo(new byte[] { 0xFF, 0xD8 }), "JPEG signature");
        Assert.That(low.Length, Is.LessThan(high.Length), "a lower quality level makes a smaller file");
        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.None));
        Assert.That((decoded.Image!.Width, decoded.Image.Height), Is.EqualTo((96, 64)));
        var worst = 0;
        for (var cellY = 0; cellY < 2; cellY++)
        {
            for (var cellX = 0; cellX < 3; cellX++)
            {
                var offset = (((cellY * 32) + 16) * 96 + (cellX * 32) + 16) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    worst = Math.Max(worst, Math.Abs(picture.Bgra[offset + channel] - decoded.Image.Bgra[offset + channel]));
                }

                Assert.That(decoded.Image.Bgra[offset + 3], Is.EqualTo(255), "a JPG has no transparency");
            }
        }

        Assert.That(worst, Is.LessThanOrEqualTo(12), "quality 95, middle of a flat 32 x 32 cell: worst channel error " + worst);
    }

    [Test]
    public void Bmp_IsDecoded()
    {
        var picture = Pixels.Pattern(20, 12);
        var source = BitmapSource.Create(20, 12, 96, 96, PixelFormats.Bgra32, null, picture.Bgra, 20 * 4);
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        var decoded = new ImageCodec().Decode(stream.ToArray());

        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.None));
        Assert.That((decoded.Image!.Width, decoded.Image.Height), Is.EqualTo((20, 12)));
        Assert.That(decoded.Image.Bgra[2], Is.EqualTo(picture.Bgra[2]), "red channel of the first pixel");
    }

    [Test]
    public void TextBytes_AreNotAnImage()
    {
        var codec = new ImageCodec();

        // Control: the same codec decodes a real picture, so "not an image" below is a judgement about the text.
        Assert.That(codec.Decode(codec.Encode(Pixels.Pattern(8, 8), ImageFormat.Png, 90)).Issue, Is.EqualTo(ImageDecodeIssue.None));
        var decoded = codec.Decode(System.Text.Encoding.UTF8.GetBytes("this is words, not a picture, and it is long enough to be sniffed"));

        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.NotAnImage));
        Assert.That(decoded.Image, Is.Null);
    }

    [Test]
    public void EmptyBytes_AreNotAnImage()
    {
        var codec = new ImageCodec();

        Assert.That(codec.Decode(codec.Encode(Pixels.Pattern(8, 8), ImageFormat.Png, 90)).Issue, Is.EqualTo(ImageDecodeIssue.None), "control: a real picture decodes");
        Assert.That(codec.Decode([]).Issue, Is.EqualTo(ImageDecodeIssue.NotAnImage));
    }

    [Test]
    public void ASideOverSixteenThousandThreeHundredEightyFour_IsTooLargeAndNoPixelsAreRead()
    {
        // 16385 x 2 of one colour: tiny as a PNG, over the limit as an image. The size is read from the frame, before any pixel.
        var wide = new byte[16385 * 2 * 4];
        var source = BitmapSource.Create(16385, 2, 96, 96, PixelFormats.Bgra32, null, wide, 16385 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        var decoded = new ImageCodec().Decode(stream.ToArray());

        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.TooLarge));
        Assert.That(decoded.Image, Is.Null);
    }

    [Test]
    public void ASideOfExactlySixteenThousandThreeHundredEightyFour_IsStillAccepted()
    {
        var wide = new byte[16384 * 1 * 4];
        var source = BitmapSource.Create(16384, 1, 96, 96, PixelFormats.Bgra32, null, wide, 16384 * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        var decoded = new ImageCodec().Decode(stream.ToArray());

        Assert.That(decoded.Issue, Is.EqualTo(ImageDecodeIssue.None));
        Assert.That(decoded.Image!.Width, Is.EqualTo(16384));
    }
}
