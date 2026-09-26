namespace Paper.ScreenWizzard.Domain.Shared;

/// <summary>Pure operations on <see cref="PixelImage"/> bytes (BGRA, straight alpha, no row padding).</summary>
public static class PixelImageOps
{
    /// <summary>
    /// Copies the part of <paramref name="source"/> that <paramref name="area"/> covers. The source's top-left pixel sits at
    /// (<paramref name="sourceX"/>, <paramref name="sourceY"/>) on the desktop, and <paramref name="area"/> is in desktop
    /// coordinates and must lie inside the source. Throws <see cref="OutOfMemoryException"/> when the copy cannot be held.
    /// </summary>
    public static PixelImage Crop(PixelImage source, int sourceX, int sourceY, PixelRect area)
    {
        var length = (long)area.Width * area.Height * 4;
        if (length > Array.MaxLength)
        {
            throw new OutOfMemoryException("The cropped image would need " + length + " bytes.");
        }

        var result = new byte[length];
        var fromLeft = area.X - sourceX;
        var fromTop = area.Y - sourceY;
        for (var row = 0; row < area.Height; row++)
        {
            var from = (((fromTop + row) * source.Width) + fromLeft) * 4;
            Buffer.BlockCopy(source.Bgra, from, result, row * area.Width * 4, area.Width * 4);
        }

        return new PixelImage(area.Width, area.Height, result);
    }

    /// <summary>Sets the alpha of every pixel outside <paramref name="inside"/> to 0; a new image, the source is untouched.</summary>
    public static PixelImage ClearOutside(PixelImage source, bool[] inside)
    {
        var bytes = source.Bgra.ToArray();
        for (var i = 0; i < inside.Length; i++)
        {
            bytes[(i * 4) + 3] = inside[i] ? (byte)255 : (byte)0;
        }

        return new PixelImage(source.Width, source.Height, bytes);
    }

    /// <summary>The image laid over an opaque white background, for formats without transparency (JPG); a new image.</summary>
    public static PixelImage FlattenOnWhite(PixelImage source)
    {
        var bytes = new byte[source.Bgra.Length];
        for (var at = 0; at + 3 < bytes.Length; at += 4)
        {
            var alpha = source.Bgra[at + 3];
            for (var channel = 0; channel < 3; channel++)
            {
                bytes[at + channel] = (byte)(((source.Bgra[at + channel] * alpha) + (255 * (255 - alpha)) + 127) / 255);
            }

            bytes[at + 3] = 255;
        }

        return new PixelImage(source.Width, source.Height, bytes);
    }
}
