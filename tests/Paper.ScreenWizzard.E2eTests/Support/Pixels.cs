using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>Reading and building <see cref="PixelImage"/> values in tests.</summary>
public static class Pixels
{
    /// <summary>The colour at desktop position (x, y) of an image whose top-left corner is the desktop position (originX, originY).</summary>
    public static RgbaColor At(PixelImage image, int originX, int originY, int x, int y)
    {
        var column = x - originX;
        var row = y - originY;
        if (column < 0 || row < 0 || column >= image.Width || row >= image.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside the image at ({originX},{originY}) {image.Width}x{image.Height}");
        }

        var offset = ((row * image.Width) + column) * 4;
        return new RgbaColor(image.Bgra[offset + 2], image.Bgra[offset + 1], image.Bgra[offset], image.Bgra[offset + 3]);
    }

    public static string Describe(RgbaColor c) => $"rgba({c.R},{c.G},{c.B},{c.A})";

    /// <summary>A picture with a different colour in every <paramref name="cell"/> x <paramref name="cell"/> cell, so a swapped channel, a flipped row or a shifted column shows.</summary>
    public static PixelImage Pattern(int width, int height, byte alpha = 255, int cell = 8)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cellX = x / cell;
                var cellY = y / cell;
                var offset = ((y * width) + x) * 4;
                bytes[offset] = (byte)((cellX * 37) + 11);
                bytes[offset + 1] = (byte)((cellY * 53) + 29);
                bytes[offset + 2] = (byte)(((cellX + cellY) * 71) + 5);
                bytes[offset + 3] = alpha;
            }
        }

        return new PixelImage(width, height, bytes);
    }
}
