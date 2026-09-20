using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>
/// The blur of the editor: square cells filled with their average colour (SPEC editor, "Làm mờ"). A pixelated cell keeps one
/// colour for all its pixels, so the original cannot be read back from it, which a soft blur would not promise.
/// </summary>
public static class Mosaic
{
    /// <summary>The side of a cell in pixels (SPEC editor: 12 x 12).</summary>
    public const int CellSize = 12;

    /// <summary>
    /// A copy of <paramref name="source"/> with every area pixelated: cells of <see cref="CellSize"/> counted from the area's
    /// top-left, edge cells clipped to the area, each cell filled with the average B, G, R and A of its pixels (rounded to nearest).
    /// The source array is never touched; with no area inside the image the source itself is returned, unchanged.
    /// Areas are applied in order, each on the result of the one before. Each pixel of an area is read once and written once:
    /// O(pixels in the areas), no nested scan of the whole image per cell.
    /// </summary>
    public static PixelImage Pixelate(PixelImage source, IReadOnlyList<PixelRect> areas)
    {
        byte[]? result = null;
        foreach (var area in areas)
        {
            var left = Math.Max(area.X, 0);
            var top = Math.Max(area.Y, 0);
            var right = Math.Min(area.X + area.Width, source.Width);
            var bottom = Math.Min(area.Y + area.Height, source.Height);
            if (right <= left || bottom <= top)
            {
                continue;
            }

            result ??= source.Bgra.ToArray();

            // Cells are counted from the area's own top-left even when that corner lies outside the image; the part outside is clipped.
            for (var cellTop = area.Y; cellTop < bottom; cellTop += CellSize)
            {
                var from = Math.Max(cellTop, top);
                var to = Math.Min(cellTop + CellSize, bottom);
                if (to <= from)
                {
                    continue;
                }

                for (var cellLeft = area.X; cellLeft < right; cellLeft += CellSize)
                {
                    var fromX = Math.Max(cellLeft, left);
                    var toX = Math.Min(cellLeft + CellSize, right);
                    if (toX > fromX)
                    {
                        FillWithAverage(result, source.Width, fromX, from, toX, to);
                    }
                }
            }
        }

        return result is null ? source : new PixelImage(source.Width, source.Height, result);
    }

    private static void FillWithAverage(byte[] bytes, int stride, int left, int top, int right, int bottom)
    {
        long b = 0;
        long g = 0;
        long r = 0;
        long a = 0;
        for (var y = top; y < bottom; y++)
        {
            var at = ((y * stride) + left) * 4;
            for (var x = left; x < right; x++)
            {
                b += bytes[at];
                g += bytes[at + 1];
                r += bytes[at + 2];
                a += bytes[at + 3];
                at += 4;
            }
        }

        var count = (long)(right - left) * (bottom - top);
        var half = count / 2;
        var average = new[] { (byte)((b + half) / count), (byte)((g + half) / count), (byte)((r + half) / count), (byte)((a + half) / count) };
        for (var y = top; y < bottom; y++)
        {
            var at = ((y * stride) + left) * 4;
            for (var x = left; x < right; x++)
            {
                bytes[at] = average[0];
                bytes[at + 1] = average[1];
                bytes[at + 2] = average[2];
                bytes[at + 3] = average[3];
                at += 4;
            }
        }
    }
}
