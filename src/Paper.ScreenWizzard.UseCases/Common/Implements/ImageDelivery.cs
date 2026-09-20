using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.UseCases.Common.Implements;

/// <summary>
/// SKELETON (plan T2): returns "did nothing" for every call so the tests written from SPEC capture and editor fail at
/// their assertions, not at the build. The rules arrive with plan T8 and T12.
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

    public DeliveryResult SaveToFolder(PixelImage image, string folder, ImageFormat format, int jpgQuality) =>
        new(false, DeliveryIssue.None, null, null);

    public DeliveryResult SaveToPath(PixelImage image, string path, int jpgQuality) =>
        new(false, DeliveryIssue.None, null, null);

    public DeliveryResult CopyToClipboard(PixelImage image) =>
        new(false, DeliveryIssue.None, null, null);
}
