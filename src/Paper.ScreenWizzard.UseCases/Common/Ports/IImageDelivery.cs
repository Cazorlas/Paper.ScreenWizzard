using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UseCases.Common.Ports;

/// <summary>
/// Puts a finished image somewhere: a file or the clipboard. Shared by the capture dialog and the editor so the file
/// naming rule, the white background of a JPG and the wording of a failure exist once.
/// </summary>
public interface IImageDelivery
{
    /// <summary>Writes into <paramref name="folder"/> under an automatic name that never overwrites an existing file.</summary>
    DeliveryResult SaveToFolder(PixelImage image, string folder, ImageFormat format, int jpgQuality);

    /// <summary>Writes to exactly <paramref name="path"/>; the format follows the extension.</summary>
    DeliveryResult SaveToPath(PixelImage image, string path, int jpgQuality);

    DeliveryResult CopyToClipboard(PixelImage image);
}

public enum DeliveryIssue
{
    None,
    FolderNotWritable,
    ClipboardBusy,
}

/// <param name="Path">The file written, when there is one.</param>
/// <param name="Detail">The system's reason on failure.</param>
public sealed record DeliveryResult(bool Success, DeliveryIssue Issue, string? Path, string? Detail);
