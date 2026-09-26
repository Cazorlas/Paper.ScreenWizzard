using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>The Shift rules of a drag and the clamp of a crop (SPEC editor, "Đường thẳng, mũi tên, khung, elip" and "Cắt"). Integers only.</summary>
public static class EditorGeometry
{
    // The eight directions a line may take, indexed by angle / 45 degrees, y growing downward on screen.
    private static readonly int[] _directionX = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] _directionY = [0, 1, 1, 1, 0, -1, -1, -1];

    /// <summary>
    /// Where a dragged shape ends. With Shift, a line or arrow snaps to the nearest multiple of 45 degrees (the drag projected onto
    /// that direction, rounded to whole pixels: (200, 20) gives (200, 0), (200, 190) gives (195, 195)), a rectangle or ellipse
    /// becomes a square or circle whose side is the longer side of the drag, growing toward the side the drag went.
    /// Without Shift, or for any other tool, the end is where the pointer is.
    /// </summary>
    public static PixelPoint ConstrainEnd(ToolKind tool, PixelPoint start, PixelPoint current, bool shiftHeld)
    {
        if (!shiftHeld)
        {
            return current;
        }

        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        switch (tool)
        {
            case ToolKind.Line:
            case ToolKind.Arrow:
                return SnapToEightDirections(start, dx, dy);
            case ToolKind.Rectangle:
            case ToolKind.Ellipse:
                var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
                return new PixelPoint(start.X + ((dx < 0 ? -1 : 1) * side), start.Y + ((dy < 0 ? -1 : 1) * side));
            default:
                return current;
        }
    }

    /// <summary>
    /// The part of <paramref name="area"/> that lies inside a <paramref name="width"/> x <paramref name="height"/> image, or null
    /// when that part is smaller than one pixel each way (SPEC editor F5).
    /// </summary>
    public static PixelRect? ClampToImage(PixelRect area, int width, int height)
    {
        var left = Math.Max(area.X, 0);
        var top = Math.Max(area.Y, 0);
        var right = Math.Min(area.X + area.Width, width);
        var bottom = Math.Min(area.Y + area.Height, height);
        return right - left < 1 || bottom - top < 1 ? null : new PixelRect(left, top, right - left, bottom - top);
    }

    private static PixelPoint SnapToEightDirections(PixelPoint start, int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return start;
        }

        var step = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        var index = ((step % 8) + 8) % 8;
        var ux = _directionX[index];
        var uy = _directionY[index];

        // Projection of the drag on (ux, uy): t * (ux, uy) with t = (d . u) / (u . u), which is 200 for (200, 20) flat and 195 for (200, 190) diagonal.
        var t = ((dx * ux) + (dy * uy)) / (double)((ux * ux) + (uy * uy));
        return new PixelPoint(
            start.X + (int)Math.Round(t * ux, MidpointRounding.AwayFromZero),
            start.Y + (int)Math.Round(t * uy, MidpointRounding.AwayFromZero));
    }
}
