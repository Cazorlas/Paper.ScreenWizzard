using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.UseCases.Common.Implements;

/// <summary>
/// Puts a finished image in a file or on the clipboard: the automatic file name that never overwrites, the white background
/// of a JPG, and the reason of a failure (SPEC capture F4, F5). Shared by the capture dialog and the editor.
/// </summary>
public sealed class ImageDelivery : IImageDelivery
{
    private readonly IClockPort _clock;
    private readonly IFileStorePort _files;
    private readonly IImageCodecPort _codec;
    private readonly IClipboardPort _clipboard;

    public ImageDelivery(IClockPort clock, IFileStorePort files, IImageCodecPort codec, IClipboardPort clipboard)
    {
        _clock = clock;
        _files = files;
        _codec = codec;
        _clipboard = clipboard;
    }

    public DeliveryResult SaveToFolder(PixelImage image, string folder, ImageFormat format, int jpgQuality)
    {
        if (!_files.DirectoryExists(folder))
        {
            return new DeliveryResult(false, DeliveryIssue.FolderNotWritable, null, "The folder does not exist: " + folder);
        }

        var name = ScreenshotNaming.FileName(_clock.Now, format, taken => _files.FileExists(Path.Combine(folder, taken)));
        return Write(image, Path.Combine(folder, name), format, jpgQuality);
    }

    public DeliveryResult SaveToPath(PixelImage image, string path, int jpgQuality) =>
        Write(image, path, ScreenshotNaming.FormatOfPath(path), jpgQuality);

    public DeliveryResult CopyToClipboard(PixelImage image)
    {
        var result = _clipboard.SetImage(image);
        return result.Success
            ? new DeliveryResult(true, DeliveryIssue.None, null, null)
            : new DeliveryResult(false, DeliveryIssue.ClipboardBusy, null, result.Detail);
    }

    private DeliveryResult Write(PixelImage image, string path, ImageFormat format, int jpgQuality)
    {
        // A JPG has no transparency: lay the image on white first, on a copy, so a freeform capture is not black outside its outline.
        var toEncode = format == ImageFormat.Jpg ? PixelImageOps.FlattenOnWhite(image) : image;
        var bytes = _codec.Encode(toEncode, format, jpgQuality);
        var written = _files.WriteAllBytes(path, bytes);
        return written.Success
            ? new DeliveryResult(true, DeliveryIssue.None, path, null)
            : new DeliveryResult(false, DeliveryIssue.FolderNotWritable, null, written.Detail);
    }
}
