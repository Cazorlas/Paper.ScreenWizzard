using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>Which drawing a finished gesture makes (SPEC editor, "Hình vẽ và lịch sử"): pure rules on numbers, the window only feeds them.</summary>
public static class AnnotationFactory
{
    /// <summary>The two corners as a normalized rectangle: the same whichever corner was pressed first.</summary>
    public static PixelRect Normalize(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>
    /// The shape a drag from <paramref name="from"/> to <paramref name="to"/> makes with <paramref name="tool"/>, or null: a click without
    /// a drag draws no line or arrow, a box with no width or no height draws no rectangle, ellipse or blur, and the other tools draw no
    /// dragged shape.
    /// </summary>
    public static Annotation? Shape(ToolKind tool, PixelPoint from, PixelPoint to, RgbaColor color, int thickness)
    {
        switch (tool)
        {
            case ToolKind.Line:
                return from == to ? null : new LineAnnotation(Guid.NewGuid(), color, thickness, from, to);
            case ToolKind.Arrow:
                return from == to ? null : new ArrowAnnotation(Guid.NewGuid(), color, thickness, from, to);
            case ToolKind.Rectangle or ToolKind.Ellipse or ToolKind.Blur:
                var box = Normalize(from, to);
                if (box.Width == 0 || box.Height == 0)
                {
                    return null;
                }

                return tool switch
                {
                    ToolKind.Rectangle => new RectangleAnnotation(Guid.NewGuid(), color, thickness, box),
                    ToolKind.Ellipse => new EllipseAnnotation(Guid.NewGuid(), color, thickness, box),
                    _ => new BlurAnnotation(Guid.NewGuid(), color, thickness, box),
                };
            default:
                return null;
        }
    }

    /// <summary>A pen or highlighter stroke; one click is a dot, made of two equal points because a stroke needs two to be a line.</summary>
    public static StrokeAnnotation Stroke(IReadOnlyList<PixelPoint> points, RgbaColor color, int thickness, bool highlighter) =>
        new(Guid.NewGuid(), color, thickness, points.Count == 1 ? [points[0], points[0]] : points.ToArray(), highlighter);

    /// <summary>A text of blanks is the empty text.</summary>
    public static string NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? string.Empty : text;
}
