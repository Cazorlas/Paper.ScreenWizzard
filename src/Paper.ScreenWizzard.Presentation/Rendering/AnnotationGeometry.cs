using System.Windows;
using System.Windows.Media;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.Rendering;

/// <summary>
/// Where an annotation is: its outline box (for the selection frame), the topmost one under a point (for Select), and a moved copy (for the
/// preview while a drag is still going on). All in image pixels. The session owns the real move and the history; the use case has no
/// "what is under this point" question, and text and step numbers need WPF to be measured, so this lives with the drawing.
/// </summary>
public static class AnnotationGeometry
{
    /// <summary>The smallest box a text or step number occupies even when empty, so it can still be found and selected.</summary>
    private const double MinimumTextBox = 6.0;

    /// <summary>The two corners as a normalized rectangle: the same whichever corner was pressed first (SPEC editor: kéo theo hướng nào cũng ra cùng hình).</summary>
    public static PixelRect Normalize(PixelPoint a, PixelPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>The box the annotation covers, its stroke included.</summary>
    public static Rect BoundsOf(Annotation annotation)
    {
        var half = AnnotationRenderer.StrokeWidthOf(annotation) / 2.0;
        switch (annotation)
        {
            case StrokeAnnotation stroke when stroke.Points.Count > 0:
                return Inflate(
                    new Rect(new Point(stroke.Points.Min(p => p.X), stroke.Points.Min(p => p.Y)), new Point(stroke.Points.Max(p => p.X), stroke.Points.Max(p => p.Y))),
                    half);
            case LineAnnotation line:
                return Inflate(new Rect(ToPoint(line.From), ToPoint(line.To)), half);
            case ArrowAnnotation arrow:
                // The head is wider than the shaft: half its width, or the shaft's, whichever is more.
                return Inflate(new Rect(ToPoint(arrow.From), ToPoint(arrow.To)), Math.Max(half, Math.Max(10.0, arrow.Thickness * 4.0) * 0.45));
            case RectangleAnnotation rectangle:
                return Inflate(ToRect(rectangle.Bounds), half);
            case EllipseAnnotation ellipse:
                return Inflate(ToRect(ellipse.Bounds), half);
            case TextAnnotation text:
                var measured = AnnotationRenderer.MakeText(text.Text, text.FontSize, Brushes.Black, bold: false);
                return new Rect(text.Origin.X, text.Origin.Y, Math.Max(MinimumTextBox, measured.Width), Math.Max(MinimumTextBox, measured.Height));
            case StepAnnotation step:
                var radius = AnnotationRenderer.StepDiameter(step.FontSize) / 2.0;
                return new Rect(step.Center.X - radius, step.Center.Y - radius, radius * 2, radius * 2);
            case BlurAnnotation blur:
                return ToRect(blur.Area);
            default:
                return Rect.Empty;
        }
    }

    /// <summary>
    /// The topmost annotation (the last in the list) under the point, or null. <paramref name="tolerance"/> is how far, in image pixels, a
    /// click may miss a thin line and still count: the caller passes a fixed number of screen pixels divided by the zoom, so a line is as easy
    /// to hit when the image is shrunk as when it is enlarged. Frames and ellipses are hit on their outline, not inside; text, step numbers and
    /// blur regions are hit anywhere inside.
    /// </summary>
    public static Annotation? HitTest(IReadOnlyList<Annotation> annotations, double x, double y, double tolerance)
    {
        var point = new Point(x, y);
        for (var i = annotations.Count - 1; i >= 0; i--)
        {
            if (Hits(annotations[i], point, tolerance))
            {
                return annotations[i];
            }
        }

        return null;
    }

    /// <summary>The annotation moved by (dx, dy) image pixels; the id is kept, because it is still the same shape.</summary>
    public static Annotation Translate(Annotation annotation, int dx, int dy) => annotation switch
    {
        StrokeAnnotation stroke => stroke with { Points = stroke.Points.Select(p => new PixelPoint(p.X + dx, p.Y + dy)).ToList() },
        LineAnnotation line => line with { From = Shift(line.From, dx, dy), To = Shift(line.To, dx, dy) },
        ArrowAnnotation arrow => arrow with { From = Shift(arrow.From, dx, dy), To = Shift(arrow.To, dx, dy) },
        RectangleAnnotation rectangle => rectangle with { Bounds = Shift(rectangle.Bounds, dx, dy) },
        EllipseAnnotation ellipse => ellipse with { Bounds = Shift(ellipse.Bounds, dx, dy) },
        TextAnnotation text => text with { Origin = Shift(text.Origin, dx, dy) },
        StepAnnotation step => step with { Center = Shift(step.Center, dx, dy) },
        BlurAnnotation blur => blur with { Area = Shift(blur.Area, dx, dy) },
        _ => annotation,
    };

    private static bool Hits(Annotation annotation, Point point, double tolerance)
    {
        var reach = (AnnotationRenderer.StrokeWidthOf(annotation) / 2.0) + tolerance;
        switch (annotation)
        {
            case StrokeAnnotation stroke:
                return DistanceToPolyline(stroke.Points, point) <= reach;
            case LineAnnotation line:
                return DistanceToSegment(point, ToPoint(line.From), ToPoint(line.To)) <= reach;
            case ArrowAnnotation arrow:
                return DistanceToSegment(point, ToPoint(arrow.From), ToPoint(arrow.To)) <= Math.Max(reach, (Math.Max(10.0, arrow.Thickness * 4.0) * 0.45) + tolerance);
            case RectangleAnnotation rectangle:
                var outer = Inflate(ToRect(rectangle.Bounds), reach);
                var inner = Deflate(ToRect(rectangle.Bounds), reach);
                return outer.Contains(point) && (inner.IsEmpty || !inner.Contains(point));
            case EllipseAnnotation ellipse:
                return HitsEllipseOutline(ToRect(ellipse.Bounds), point, reach);
            case TextAnnotation or StepAnnotation or BlurAnnotation:
                return Inflate(BoundsOf(annotation), tolerance).Contains(point);
            default:
                return false;
        }
    }

    private static bool HitsEllipseOutline(Rect bounds, Point point, double reach)
    {
        var rx = bounds.Width / 2.0;
        var ry = bounds.Height / 2.0;
        var centre = new Point(bounds.X + rx, bounds.Y + ry);
        if (rx <= 0.5 || ry <= 0.5)
        {
            // Flat: a line segment.
            return Inflate(bounds, reach).Contains(point);
        }

        // Distance to the outline, approximated by how far the normalized radius is from 1, scaled by the smaller radius: exact for a circle
        // and within a few percent for the ellipses a screenshot markup uses, which is far below the size of a click.
        var normalized = Math.Sqrt((Math.Pow((point.X - centre.X) / rx, 2)) + Math.Pow((point.Y - centre.Y) / ry, 2));
        return Math.Abs(normalized - 1.0) * Math.Min(rx, ry) <= reach;
    }

    private static double DistanceToPolyline(IReadOnlyList<PixelPoint> points, Point point)
    {
        if (points.Count == 0)
        {
            return double.MaxValue;
        }

        if (points.Count == 1)
        {
            return (point - ToPoint(points[0])).Length;
        }

        var best = double.MaxValue;
        for (var i = 1; i < points.Count; i++)
        {
            best = Math.Min(best, DistanceToSegment(point, ToPoint(points[i - 1]), ToPoint(points[i])));
        }

        return best;
    }

    private static double DistanceToSegment(Point point, Point a, Point b)
    {
        var segment = b - a;
        var lengthSquared = segment.LengthSquared;
        if (lengthSquared < 1e-9)
        {
            return (point - a).Length;
        }

        var t = Math.Clamp(Vector.Multiply(point - a, segment) / lengthSquared, 0.0, 1.0);
        return (point - (a + (segment * t))).Length;
    }

    private static Rect Inflate(Rect rect, double amount)
    {
        rect.Inflate(amount, amount);
        return rect;
    }

    private static Rect Deflate(Rect rect, double amount)
    {
        if (rect.Width <= amount * 2 || rect.Height <= amount * 2)
        {
            return Rect.Empty;
        }

        rect.Inflate(-amount, -amount);
        return rect;
    }

    private static PixelPoint Shift(PixelPoint point, int dx, int dy) => new(point.X + dx, point.Y + dy);

    private static PixelRect Shift(PixelRect rect, int dx, int dy) => new(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);

    private static Point ToPoint(PixelPoint point) => new(point.X, point.Y);

    private static Rect ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
