using Paper.ScreenWizzard.Domain.Shared;

namespace Paper.ScreenWizzard.Domain.Editor;

/// <summary>The tools on the editor's toolbar (SPEC editor, "What the user does" step 2).</summary>
public enum ToolKind
{
    Select,
    Pen,
    Highlighter,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    StepNumber,
    Blur,
    Crop,
}

/// <summary>
/// Something drawn on the image. Kept as an object until the image is saved, so it can be selected, moved, recoloured
/// and deleted (SPEC editor, "Hình vẽ và lịch sử"). All coordinates are in image pixels, origin at the image's top-left.
/// </summary>
public abstract record Annotation(Guid Id, RgbaColor Color, int Thickness);

/// <summary>A freehand stroke; a highlighter stroke is the same with <see cref="IsHighlighter"/> set.</summary>
public sealed record StrokeAnnotation(Guid Id, RgbaColor Color, int Thickness, IReadOnlyList<PixelPoint> Points, bool IsHighlighter)
    : Annotation(Id, Color, Thickness);

public sealed record LineAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelPoint From, PixelPoint To)
    : Annotation(Id, Color, Thickness);

/// <summary>An arrow whose head is at <see cref="To"/>.</summary>
public sealed record ArrowAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelPoint From, PixelPoint To)
    : Annotation(Id, Color, Thickness);

public sealed record RectangleAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelRect Bounds)
    : Annotation(Id, Color, Thickness);

public sealed record EllipseAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelRect Bounds)
    : Annotation(Id, Color, Thickness);

public sealed record TextAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelPoint Origin, string Text, int FontSize)
    : Annotation(Id, Color, Thickness);

/// <summary>A circle with a number, for "step 1, 2, 3".</summary>
public sealed record StepAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelPoint Center, int Number, int FontSize)
    : Annotation(Id, Color, Thickness);

/// <summary>A region that is pixelated when the image is rendered; it cannot be undone from a saved file.</summary>
public sealed record BlurAnnotation(Guid Id, RgbaColor Color, int Thickness, PixelRect Area)
    : Annotation(Id, Color, Thickness);

/// <summary>The image being edited plus everything drawn on it: one immutable snapshot, which is what the history keeps.</summary>
public sealed record EditorDocument(PixelImage Source, IReadOnlyList<Annotation> Annotations);
