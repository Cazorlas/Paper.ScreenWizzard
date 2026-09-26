using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Common.Ports;

namespace Paper.ScreenWizzard.Infrastructure.Common;

/// <summary>
/// The Windows clipboard through WPF's <c>Clipboard</c>, which must be used on an STA thread (the UI thread). Another program may hold
/// the clipboard open for a moment while it copies, and WPF then throws <see cref="COMException"/> (CLIPBRD_E_CANT_OPEN), so a copy is
/// tried up to <c>tries</c> times, <c>delayMilliseconds</c> apart (SPEC capture F5). Measured on this Windows: WPF's own SetImage already
/// retries about a second before it throws, so the total wait for a held clipboard is roughly tries x 1.1 s.
/// </summary>
public sealed class ClipboardService : IClipboardPort
{
    private readonly int _tries;
    private readonly int _delayMilliseconds;

    public ClipboardService(int tries = 5, int delayMilliseconds = 100)
    {
        _tries = Math.Max(1, tries);
        _delayMilliseconds = Math.Max(0, delayMilliseconds);
    }

    public PortResult SetImage(PixelImage image)
    {
        BitmapSource source;
        try
        {
            source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
            source.Freeze();
        }
        catch (Exception exception)
        {
            return PortResult.Fail("The image could not be prepared for the clipboard: " + exception.Message);
        }

        Exception? last = null;
        for (var attempt = 1; attempt <= _tries; attempt++)
        {
            try
            {
                Clipboard.SetImage(source);
                return PortResult.Ok;
            }
            catch (COMException exception)
            {
                // CLIPBRD_E_CANT_OPEN and its kin: somebody else has the clipboard open. Wait and ask again.
                last = exception;
            }
            catch (Exception exception)
            {
                // Not a busy clipboard (wrong thread, no clipboard at all): asking again will not help.
                return PortResult.Fail("The clipboard could not be written: " + exception.Message);
            }

            if (attempt < _tries)
            {
                Thread.Sleep(_delayMilliseconds);
            }
        }

        return PortResult.Fail($"The clipboard is held by another program (tried {_tries} times): {last?.Message}");
    }

    public ClipboardImageResult GetImage()
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= _tries; attempt++)
        {
            try
            {
                var source = Clipboard.GetImage();
                return source is null ? new ClipboardImageResult(false, null, null) : new ClipboardImageResult(true, ToPixels(source), null);
            }
            catch (COMException exception)
            {
                last = exception;
            }
            catch (Exception exception)
            {
                return new ClipboardImageResult(false, null, "The clipboard could not be read: " + exception.Message, ReadFailed: true);
            }

            if (attempt < _tries)
            {
                Thread.Sleep(_delayMilliseconds);
            }
        }

        return new ClipboardImageResult(false, null, $"The clipboard is held by another program (tried {_tries} times): {last?.Message}", ReadFailed: true);
    }

    private static PixelImage ToPixels(BitmapSource source)
    {
        // Whatever WPF handed back (a 24- or 32-bit DIB, an HBITMAP) becomes B, G, R, A with straight alpha.
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var bytes = new byte[width * height * 4];
        converted.CopyPixels(bytes, width * 4, 0);
        return new PixelImage(width, height, bytes);
    }
}
