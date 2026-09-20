using System.Windows.Media;
using System.Windows.Media.Imaging;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>
/// PNG, JPG and BMP through the WPF codecs. Decoding reads the frame's size from the header first (the cache option is
/// <c>None</c>, so no pixel is decoded by <c>BitmapDecoder.Create</c>) and refuses a picture that is too large before it allocates a byte
/// for it (SPEC editor F8); anything the three codecs do not read is "not an image" (F2). Pixels come out as B, G, R, A with straight alpha.
/// </summary>
public sealed class ImageCodec : IImageCodecPort
{
    /// <summary>The longest side the editor accepts, in pixels.</summary>
    public const int MaxSide = 16384;

    /// <summary>The most memory a decoded picture may need: 1.5 GB of B, G, R, A.</summary>
    public const long MaxBytes = 1_610_612_736;

    public ImageDecodeResult Decode(byte[] fileBytes)
    {
        if (fileBytes.Length == 0)
        {
            return new ImageDecodeResult(null, ImageDecodeIssue.NotAnImage);
        }

        try
        {
            using var stream = new MemoryStream(fileBytes, writable: false);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder) || decoder.Frames.Count == 0)
            {
                return new ImageDecodeResult(null, ImageDecodeIssue.NotAnImage);
            }

            var frame = decoder.Frames[0];
            var width = frame.PixelWidth;
            var height = frame.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return new ImageDecodeResult(null, ImageDecodeIssue.NotAnImage);
            }

            if (width > MaxSide || height > MaxSide || ((long)width * height * 4) > MaxBytes)
            {
                return new ImageDecodeResult(null, ImageDecodeIssue.TooLarge);
            }

            // The pixels are decoded here, while the stream is still open.
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[width * height * 4];
            converted.CopyPixels(bytes, width * 4, 0);
            return new ImageDecodeResult(new PixelImage(width, height, bytes), ImageDecodeIssue.None);
        }
        catch (OutOfMemoryException)
        {
            return new ImageDecodeResult(null, ImageDecodeIssue.TooLarge);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or InvalidOperationException
            or ArgumentException or System.Runtime.InteropServices.COMException or FormatException)
        {
            // FileFormatException (an IOException) for a file no codec claims; NotSupportedException for an unknown container.
            return new ImageDecodeResult(null, ImageDecodeIssue.NotAnImage);
        }
    }

    public byte[] Encode(PixelImage image, ImageFormat format, int jpgQuality)
    {
        BitmapSource source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
        BitmapEncoder encoder;
        if (format == ImageFormat.Jpg)
        {
            // A JPG has no alpha; the caller has already flattened the picture onto white, so dropping it here loses nothing.
            source = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
            encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpgQuality, 1, 100) };
        }
        else
        {
            encoder = new PngBitmapEncoder();
        }

        encoder.Frames.Add(BitmapFrame.Create(source));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }
}
