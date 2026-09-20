using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Presentation.Rendering;

/// <summary>
/// Draws one annotation with WPF, in IMAGE pixel space: one unit of the drawing context is one pixel of the image, so a shape at
/// (150, 100) is drawn at (150, 100) and the caller scales the whole context for the window's zoom or draws it 1:1 into the bitmap that
/// is saved. The blur regions are not drawn here: their pixels are the mosaic of <c>IEditorSession.RenderBase</c>.
/// </summary>
public static class AnnotationRenderer
{

    /// <summary>How strongly a highlighter tints what is under it: 40% of the way from the paper to the highlight colour.</summary>
    public const double HighlighterStrength = 0.4;

    private const string FontName = "Segoe UI";

    // Segoe UI has every Vietnamese letter, the degree sign and the em dash; a font without them would draw boxes, and the tests read the
    // pixels of exactly this string.
    private static readonly Typeface _typeface = new(new FontFamily(FontName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static readonly Typeface _boldTypeface = new(new FontFamily(FontName), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    /// <summary>The stroke width in image pixels: the annotation's thickness, three times that for a highlighter.</summary>
    public static double StrokeWidthOf(Annotation annotation) => AnnotationMetrics.StrokeWidthOf(annotation);

    /// <summary>Draws the annotation as the window shows it while editing (a highlighter as a translucent stroke).</summary>
    public static void Draw(DrawingContext context, Annotation annotation)
    {
        switch (annotation)
        {
            case StrokeAnnotation stroke:
                DrawStroke(context, stroke, HighlighterStrength);
                break;
            case LineAnnotation line:
                context.DrawLine(RoundPen(line.Color, line.Thickness), ToPoint(line.From), ToPoint(line.To));
                break;
            case ArrowAnnotation arrow:
                DrawArrow(context, arrow);
                break;
            case RectangleAnnotation rectangle:
                context.DrawRectangle(null, MiterPen(rectangle.Color, rectangle.Thickness), ToRect(rectangle.Bounds));
                break;
            case EllipseAnnotation ellipse:
                var bounds = ToRect(ellipse.Bounds);
                context.DrawEllipse(null, RoundPen(ellipse.Color, ellipse.Thickness), new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)), bounds.Width / 2, bounds.Height / 2);
                break;
            case TextAnnotation text:
                context.DrawText(MakeText(text.Text, text.FontSize, BrushOf(text.Color), bold: false), ToPoint(text.Origin));
                break;
            case StepAnnotation step:
                DrawStep(context, step);
                break;
        }
    }

    /// <summary>Draws a highlighter stroke as one fully opaque shape in its own colour: the flattener uses it as the coverage mask.</summary>
    public static void DrawHighlighterMask(DrawingContext context, StrokeAnnotation stroke) => DrawStroke(context, stroke with { Color = stroke.Color with { A = 255 } }, 1.0);

    /// <summary>A text as WPF will lay it out (also how tall and wide it is, for hit testing and selection).</summary>
    public static FormattedText MakeText(string text, double fontSize, Brush brush, bool bold) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        bold ? _boldTypeface : _typeface,
        fontSize,
        brush,
        1.0);

    public static Brush BrushOf(RgbaColor color, double opacity = 1.0)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>The disc of a step number: wide enough for two digits at this font size.</summary>
    public static double StepDiameter(int fontSize) => AnnotationMetrics.StepDiameter(fontSize);

    private static void DrawStroke(DrawingContext context, StrokeAnnotation stroke, double highlighterOpacity)
    {
        var width = StrokeWidthOf(stroke);
        var opacity = stroke.IsHighlighter ? highlighterOpacity : 1.0;
        var pen = new Pen(BrushOf(stroke.Color, opacity), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();

        if (stroke.Points.Count == 0)
        {
            return;
        }

        if (stroke.Points.Count == 1)
        {
            // A click with the pen is a dot.
            context.DrawEllipse(pen.Brush, null, ToPoint(stroke.Points[0]), width / 2, width / 2);
            return;
        }

        var geometry = new StreamGeometry();
        using (var open = geometry.Open())
        {
            open.BeginFigure(ToPoint(stroke.Points[0]), isFilled: false, isClosed: false);
            open.PolyLineTo(stroke.Points.Skip(1).Select(ToPoint).ToList(), isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();

        // One geometry, one stroke: where the path crosses itself the colour is not laid on twice, so a highlighter does not get darker there.
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawArrow(DrawingContext context, ArrowAnnotation arrow)
    {
        var from = ToPoint(arrow.From);
        var to = ToPoint(arrow.To);
        var direction = to - from;
        var length = direction.Length;
        if (length < 0.5)
        {
            context.DrawEllipse(BrushOf(arrow.Color), null, to, arrow.Thickness / 2.0, arrow.Thickness / 2.0);
            return;
        }

        direction /= length;
        var headLength = Math.Min(length, Math.Max(10.0, arrow.Thickness * 4.0));
        var halfWidth = headLength * 0.45;
        var perpendicular = new Vector(-direction.Y, direction.X);
        var baseCentre = to - (direction * headLength);

        // The shaft stops where the head starts (flat cap), and the head is a filled triangle with no outline of its own, so its tip is
        // exactly the end point: an outline would push the tip past it by half the pen width.
        var shaftPen = new Pen(BrushOf(arrow.Color), arrow.Thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        shaftPen.Freeze();
        if (length > headLength)
        {
            context.DrawLine(shaftPen, from, baseCentre);
        }

        var head = new StreamGeometry();
        using (var open = head.Open())
        {
            open.BeginFigure(to, isFilled: true, isClosed: true);
            open.LineTo(baseCentre + (perpendicular * halfWidth), isStroked: false, isSmoothJoin: false);
            open.LineTo(baseCentre - (perpendicular * halfWidth), isStroked: false, isSmoothJoin: false);
        }

        head.Freeze();
        context.DrawGeometry(BrushOf(arrow.Color), null, head);
    }

    private static void DrawStep(DrawingContext context, StepAnnotation step)
    {
        var diameter = StepDiameter(step.FontSize);
        var centre = ToPoint(step.Center);
        context.DrawEllipse(BrushOf(step.Color), null, centre, diameter / 2, diameter / 2);

        // The number is white on a dark disc and black on a light one, so it reads on every colour of the palette.
        var luminance = ((0.299 * step.Color.R) + (0.587 * step.Color.G) + (0.114 * step.Color.B)) / 255.0;
        var ink = luminance > 0.6 ? Brushes.Black : Brushes.White;
        var text = MakeText(step.Number.ToString(CultureInfo.InvariantCulture), step.FontSize, ink, bold: true);
        context.DrawText(text, new Point(centre.X - (text.Width / 2), centre.Y - (text.Height / 2)));
    }

    private static Pen RoundPen(RgbaColor color, int thickness)
    {
        var pen = new Pen(BrushOf(color), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    // A rectangle keeps its square corners.
    private static Pen MiterPen(RgbaColor color, int thickness)
    {
        var pen = new Pen(BrushOf(color), thickness) { LineJoin = PenLineJoin.Miter };
        pen.Freeze();
        return pen;
    }

    private static Point ToPoint(PixelPoint point) => new(point.X, point.Y);

    private static Rect ToRect(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
