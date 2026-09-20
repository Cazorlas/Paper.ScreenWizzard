using Paper.ScreenWizzard.Domain.Geometry;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>
/// Whether a click, in image pixels, lands on one drawing (SPEC editor, "What the user does" step 4). A line, arrow, pen stroke,
/// rectangle or ellipse is hit on its stroke only, that is within tolerance + thickness / 2 of its centre line, so an empty
/// rectangle is not selected by clicking inside it; a blur area, a text and a step number are solid, hit inside their box grown
/// by the tolerance. Plain double arithmetic on squared distances, no square root and no allocation except a text's line split.
/// </summary>
public static class AnnotationHit
{
    /// <summary>A tolerance below zero counts as zero.</summary>
    public static bool IsHit(Annotation annotation, PixelPoint point, int tolerance)
    {
        var reach = Math.Max(tolerance, 0);
        var radius = reach + (annotation.Thickness / 2.0);
        switch (annotation)
        {
            case LineAnnotation l:
                return SegmentIsNear(l.From, l.To, point, radius);
            case ArrowAnnotation a:
                return SegmentIsNear(a.From, a.To, point, radius);
            case StrokeAnnotation s:
                return StrokeIsNear(s.Points, point, radius);
            case RectangleAnnotation r:
                return OutlineOfRectangleIsNear(r.Bounds, point, radius);
            case EllipseAnnotation e:
                return OutlineOfEllipseIsNear(e.Bounds, point, radius);
            case BlurAnnotation:
            case TextAnnotation:
            case StepAnnotation:
                var (left, top, right, bottom) = AnnotationOps.Box(annotation);
                return point.X >= left - reach && point.X <= right + reach && point.Y >= top - reach && point.Y <= bottom + reach;
            default:
                return false;
        }
    }

    private static bool StrokeIsNear(IReadOnlyList<PixelPoint> points, PixelPoint point, double radius)
    {
        if (points.Count == 1)
        {
            return SegmentIsNear(points[0], points[0], point, radius);
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (SegmentIsNear(points[i - 1], points[i], point, radius))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Distance from the point to the segment (clamped projection), squared, against the radius squared.</summary>
    private static bool SegmentIsNear(PixelPoint from, PixelPoint to, PixelPoint point, double radius)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double px = point.X - from.X;
        double py = point.Y - from.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((px * dx) + (py * dy)) / lengthSquared, 0, 1);
        var ex = px - (t * dx);
        var ey = py - (t * dy);
        return (ex * ex) + (ey * ey) <= radius * radius;
    }

    /// <summary>
    /// Outside the rectangle the distance to the outline is the distance to the rectangle; inside it is the distance to the
    /// nearest of the four edges. So the middle of an empty rectangle is far from its outline and does not count.
    /// </summary>
    private static bool OutlineOfRectangleIsNear(PixelRect bounds, PixelPoint point, double radius)
    {
        double left = bounds.X;
        double top = bounds.Y;
        double right = bounds.X + bounds.Width;
        double bottom = bounds.Y + bounds.Height;
        var outsideX = Math.Max(Math.Max(left - point.X, point.X - right), 0);
        var outsideY = Math.Max(Math.Max(top - point.Y, point.Y - bottom), 0);
        if (outsideX > 0 || outsideY > 0)
        {
            return (outsideX * outsideX) + (outsideY * outsideY) <= radius * radius;
        }

        var inside = Math.Min(Math.Min(point.X - left, right - point.X), Math.Min(point.Y - top, bottom - point.Y));
        return inside <= radius;
    }

    /// <summary>
    /// The exact distance to an ellipse has no closed form, so the stroke is taken as a band between two ellipses with the same
    /// centre: the semi-axes grown by the radius (the outer edge) and shrunk by it (the inner edge). A click is hit when it is
    /// inside the outer ellipse and not strictly inside the inner one. That is exact on both axes and within a pixel or two at
    /// 45 degrees for the few-pixel radii a click uses; when the shrunk axis reaches zero the whole small ellipse is the stroke.
    /// </summary>
    private static bool OutlineOfEllipseIsNear(PixelRect bounds, PixelPoint point, double radius)
    {
        var a = bounds.Width / 2.0;
        var b = bounds.Height / 2.0;
        var dx = point.X - (bounds.X + a);
        var dy = point.Y - (bounds.Y + b);
        var outerA = a + radius;
        var outerB = b + radius;
        if (outerA <= 0 || outerB <= 0)
        {
            return false;
        }

        if (((dx * dx) / (outerA * outerA)) + ((dy * dy) / (outerB * outerB)) > 1)
        {
            return false;
        }

        var innerA = a - radius;
        var innerB = b - radius;
        if (innerA <= 0 || innerB <= 0)
        {
            return true;
        }

        return ((dx * dx) / (innerA * innerA)) + ((dy * dy) / (innerB * innerB)) >= 1;
    }
}
