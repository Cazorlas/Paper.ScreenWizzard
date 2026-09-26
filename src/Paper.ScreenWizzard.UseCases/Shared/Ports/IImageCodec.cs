using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UseCases.Shared.Ports;

/// <summary>Turns bytes into pixels and back. Decoding refuses what is not an image; encoding is PNG or JPG.</summary>
public interface IImageCodec
{
    ImageDecodeResult Decode(byte[] fileBytes);

    byte[] Encode(PixelImage image, ImageFormat format, int jpgQuality);
}
