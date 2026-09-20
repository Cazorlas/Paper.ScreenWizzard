using System.Windows;
using System.Windows.Media;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.Rendering;

/// <summary>
/// Where an annotation is, for drawing only: its outline box (for the selection frame) and a moved copy (for the preview while a drag is still
/// going on). All in image pixels. The session owns the real move and the history. Which annotation a click chooses is NOT decided here: that
/// is <c>IEditorInteractor.HitTest</c> (ADR 0001); text and step numbers need WPF to be measured for the frame, so this stays with the drawing.
/// </summary>
public static class AnnotationGeometry
{
    /// <summary>The smallest box a text or step number occupies even when empty, so its selection frame is still visible.</summary>
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

    private static Rect Inflate(Rect rect, double amount)
    {
        rect.Inflate(amount, amount);
        return rect;
    }

    private static PixelPoint Shift(PixelPoint point, int dx, int dy) => new(point.X + dx, point.Y + dy);

    private static PixelRect Shift(PixelRect rect, int dx, int dy) => new(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);

    private static Point ToPoint(PixelPoint point) => new(point.X, point.Y);

    private static Rect ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
