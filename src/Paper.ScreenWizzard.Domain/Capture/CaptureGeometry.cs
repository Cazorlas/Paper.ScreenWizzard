using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Domain.Capture;

/// <summary>
/// The pure rules of choosing a region on the frozen desktop (SPEC capture, "Vùng chữ nhật" and "Vùng tự do"). Every number
/// is a physical pixel of the virtual desktop, so x and y may be negative.
/// </summary>
public static class CaptureGeometry
{
    /// <summary>A region under this many pixels wide or high is not captured (SPEC capture F2).</summary>
    public const int MinimumRegionSide = 3;

    /// <summary>An outline enclosing fewer square pixels than this is not captured (SPEC capture F3).</summary>
    public const int MinimumOutlineArea = 9;

    /// <summary>A drag in either direction is the same rectangle: its width is |x2 - x1| and its height |y2 - y1|, the end pixel excluded.</summary>
    public static PixelRect Normalise(PixelPoint from, PixelPoint to) =>
        new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));

    /// <summary>The part of <paramref name="rect"/> that lies inside <paramref name="bounds"/>; zero width and height when they do not meet.</summary>
    public static PixelRect Clamp(PixelRect rect, PixelRect bounds)
    {
        var left = Math.Max(rect.X, bounds.X);
        var top = Math.Max(rect.Y, bounds.Y);
        var right = Math.Min(rect.X + rect.Width, bounds.X + bounds.Width);
        var bottom = Math.Min(rect.Y + rect.Height, bounds.Y + bounds.Height);
        return right <= left || bottom <= top
            ? new PixelRect(left, top, 0, 0)
            : new PixelRect(left, top, right - left, bottom - top);
    }

    public static bool IsTooSmall(PixelRect rect) => rect.Width < MinimumRegionSide || rect.Height < MinimumRegionSide;

    public static bool Contains(PixelRect rect, PixelPoint point) =>
        point.X >= rect.X && point.X < rect.X + rect.Width && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;

    /// <summary>The smallest rectangle holding every monitor rectangle.</summary>
    public static PixelRect Union(IEnumerable<PixelRect> rects)
    {
        var any = false;
        int left = 0, top = 0, right = 0, bottom = 0;
        foreach (var rect in rects)
        {
            if (!any)
            {
                (left, top, right, bottom) = (rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
                any = true;
                continue;
            }

            left = Math.Min(left, rect.X);
            top = Math.Min(top, rect.Y);
            right = Math.Max(right, rect.X + rect.Width);
            bottom = Math.Max(bottom, rect.Y + rect.Height);
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    // ---- Freeform ----

    public static int DistinctPointCount(IReadOnlyList<PixelPoint> outline) => outline.Distinct().Count();

    /// <summary>The area the outline encloses once closed back to its first point (shoelace), in square pixels.</summary>
    public static double OutlineArea(IReadOnlyList<PixelPoint> outline)
    {
        long twice = 0;
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            twice += ((long)a.X * b.Y) - ((long)b.X * a.Y);
        }

        return Math.Abs(twice) / 2.0;
    }

    /// <summary>True when the outline has too few distinct points or encloses too little (SPEC capture F3).</summary>
    public static bool IsOutlineTooSmall(IReadOnlyList<PixelPoint> outline) =>
        DistinctPointCount(outline) < 3 || OutlineArea(outline) < MinimumOutlineArea;

    /// <summary>The rectangle that just holds every point of the outline, end excluded (a triangle from x 100 to 300 is 200 wide).</summary>
    public static PixelRect BoundingBox(IReadOnlyList<PixelPoint> outline)
    {
        var left = outline.Min(p => p.X);
        var top = outline.Min(p => p.Y);
        var right = outline.Max(p => p.X);
        var bottom = outline.Max(p => p.Y);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// For each pixel of <paramref name="area"/>, row by row, whether the pixel's centre (x + 0.5, y + 0.5) lies inside the
    /// outline (even-odd rule; the outline is closed implicitly, so the closing edge needs no extra point).
    /// </summary>
    public static bool[] InsideMask(IReadOnlyList<PixelPoint> outline, PixelRect area)
    {
        var mask = new bool[area.Width * area.Height];
        var crossings = new List<double>();
        for (var row = 0; row < area.Height; row++)
        {
            var yCentre = area.Y + row + 0.5;
            crossings.Clear();
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                if ((a.Y <= yCentre) == (b.Y <= yCentre))
                {
                    continue;
                }

                crossings.Add(a.X + ((yCentre - a.Y) * (b.X - a.X) / (b.Y - a.Y)));
            }

            crossings.Sort();
            for (var pair = 0; pair + 1 < crossings.Count; pair += 2)
            {
                var first = (int)Math.Ceiling(crossings[pair] - 0.5) - area.X;
                var last = (int)Math.Ceiling(crossings[pair + 1] - 0.5) - area.X;
                for (var column = Math.Max(first, 0); column < Math.Min(last, area.Width); column++)
                {
                    mask[(row * area.Width) + column] = true;
                }
            }
        }

        return mask;
    }
}
