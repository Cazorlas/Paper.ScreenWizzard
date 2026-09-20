namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>
/// How big a drawing really is on the image. The renderer draws with these numbers and the hit test and the crop measure with the
/// same ones, so a click lands on a painted pixel and a crop keeps a drawing that shows.
/// </summary>
public static class AnnotationMetrics
{
    /// <summary>The highlighter is this many times as wide as the pen for the same slider value: a highlighter that thin would be a pen.</summary>
    public const double HighlighterWidthFactor = 3.0;

    /// <summary>
    /// The average advance of one character as a share of the font size. Segoe UI draws "Van" at size 18 about 32 pixels wide; the
    /// exact width is only known to the renderer, so a text's box is this estimate.
    /// </summary>
    public const double TextAdvanceFactor = 0.6;

    /// <summary>The height of one text line as a share of the font size (Segoe UI's line spacing is about 1.33).</summary>
    public const double TextLineFactor = 1.4;

    /// <summary>The stroke width in image pixels: the annotation's thickness, three times that for a highlighter.</summary>
    public static double StrokeWidthOf(Annotation annotation) =>
        annotation is StrokeAnnotation { IsHighlighter: true } ? annotation.Thickness * HighlighterWidthFactor : annotation.Thickness;

    /// <summary>The disc of a step number: wide enough for two digits at this font size.</summary>
    public static double StepDiameter(int fontSize) => Math.Max(20.0, fontSize * 1.8);
}
