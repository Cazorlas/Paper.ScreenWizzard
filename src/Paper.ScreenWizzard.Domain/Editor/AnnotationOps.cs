using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>Pure operations on annotations: move, box, and the next step number (SPEC editor, "Hình vẽ và lịch sử", "Số bước", "Cắt").</summary>
public static class AnnotationOps
{
    /// <summary>A copy of <paramref name="annotation"/> with every coordinate shifted by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    public static Annotation Translate(Annotation annotation, int dx, int dy) => annotation switch
    {
        StrokeAnnotation s => s with { Points = s.Points.Select(p => Shift(p, dx, dy)).ToArray() },
        LineAnnotation l => l with { From = Shift(l.From, dx, dy), To = Shift(l.To, dx, dy) },
        ArrowAnnotation a => a with { From = Shift(a.From, dx, dy), To = Shift(a.To, dx, dy) },
        RectangleAnnotation r => r with { Bounds = Shift(r.Bounds, dx, dy) },
        EllipseAnnotation e => e with { Bounds = Shift(e.Bounds, dx, dy) },
        TextAnnotation t => t with { Origin = Shift(t.Origin, dx, dy) },
        StepAnnotation n => n with { Center = Shift(n.Center, dx, dy) },
        BlurAnnotation b => b with { Area = Shift(b.Area, dx, dy) },
        _ => annotation,
    };

    /// <summary>The largest step number in use plus one, or 1 when there is none; nothing is renumbered (SPEC editor, "Số bước").</summary>
    public static int NextStepNumber(IEnumerable<Annotation> annotations)
    {
        var largest = 0;
        foreach (var annotation in annotations)
        {
            if (annotation is StepAnnotation step && step.Number > largest)
            {
                largest = step.Number;
            }
        }

        return largest + 1;
    }

    /// <summary>
    /// Whether the drawing touches <paramref name="area"/> at all. The box is generous where the real size is only known to the
    /// renderer (text, the number circle, the thickness of a stroke): it errs toward keeping a drawing, because the renderer clips
    /// the part outside and a wrongly dropped drawing cannot be seen again, while a kept one that shows nothing costs nothing.
    /// The area is half-open: a drawing that only touches its right or bottom edge is outside.
    /// </summary>
    public static bool Touches(Annotation annotation, PixelRect area)
    {
        var (left, top, right, bottom) = Box(annotation);
        return right > area.X && left < area.X + area.Width && bottom > area.Y && top < area.Y + area.Height;
    }

    private static (int Left, int Top, int Right, int Bottom) Box(Annotation annotation)
    {
        var pad = annotation.Thickness;
        switch (annotation)
        {
            case StrokeAnnotation s:
                return Around(s.Points, pad);
            case LineAnnotation l:
                return Around([l.From, l.To], pad);
            case ArrowAnnotation a:
                return Around([a.From, a.To], pad);
            case RectangleAnnotation r:
                return (r.Bounds.X - pad, r.Bounds.Y - pad, r.Bounds.X + r.Bounds.Width + pad, r.Bounds.Y + r.Bounds.Height + pad);
            case EllipseAnnotation e:
                return (e.Bounds.X - pad, e.Bounds.Y - pad, e.Bounds.X + e.Bounds.Width + pad, e.Bounds.Y + e.Bounds.Height + pad);
            case BlurAnnotation b:
                return (b.Area.X, b.Area.Y, b.Area.X + b.Area.Width, b.Area.Y + b.Area.Height);
            case StepAnnotation n:
                return (n.Center.X - n.FontSize, n.Center.Y - n.FontSize, n.Center.X + n.FontSize, n.Center.Y + n.FontSize);
            case TextAnnotation t:
                var lines = t.Text.Split('\n');
                var longest = lines.Max(line => line.Length);
                return (t.Origin.X, t.Origin.Y, t.Origin.X + (longest * t.FontSize), t.Origin.Y + (lines.Length * t.FontSize * 2));
            default:
                return (0, 0, 0, 0);
        }
    }

    private static (int Left, int Top, int Right, int Bottom) Around(IReadOnlyList<PixelPoint> points, int pad)
    {
        if (points.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        foreach (var p in points)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }

        return (left - pad, top - pad, right + pad, bottom + pad);
    }

    private static PixelPoint Shift(PixelPoint p, int dx, int dy) => new(p.X + dx, p.Y + dy);

    private static PixelRect Shift(PixelRect r, int dx, int dy) => new(r.X + dx, r.Y + dy, r.Width, r.Height);
}
