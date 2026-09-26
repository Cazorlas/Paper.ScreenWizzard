using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Editor.Models;

namespace Paper.ScreenWizzard.UseCases.Editor.Ports;

/// <summary>
/// One image being edited and its history. Every mutating call is one history step (SPEC editor, "Mỗi thao tác là một
/// bước lịch sử"). The window keeps one session and draws from <see cref="Document"/>.
/// </summary>
public interface IEditorSession
{
    /// <summary>The path the image came from or was last saved to; null for a fresh capture.</summary>
    string? SourcePath { get; }

    EditorDocument Document { get; }

    Guid? SelectedId { get; }

    bool CanUndo { get; }

    bool CanRedo { get; }

    /// <summary>True when there are changes since the last save (SPEC editor F7).</summary>
    bool IsDirty { get; }

    /// <summary>The number the next step marker gets: the largest number in use plus one, or 1.</summary>
    int NextStepNumber { get; }

    void Add(Annotation annotation);

    void Move(Guid id, int dx, int dy);

    void Recolor(Guid id, RgbaColor color);

    void SetThickness(Guid id, int thickness);

    void Delete(Guid id);

    /// <summary>Replaces the text of a text annotation (double-click edit, SPEC editor "Chữ"); empty text deletes it. One history step.</summary>
    void SetText(Guid id, string text);

    /// <summary>Changes the font size of a text or step annotation. One history step.</summary>
    void SetFontSize(Guid id, int fontSize);

    void Select(Guid? id);

    /// <summary>Keeps <paramref name="area"/> (clamped to the image); moves the drawings with it and drops those outside (SPEC editor, "Cắt").</summary>
    CropOutcome Crop(PixelRect area);

    bool Undo();

    bool Redo();

    /// <summary>The source image with every blur region pixelated; the window draws the other shapes over it.</summary>
    PixelImage RenderBase();

    void MarkSaved(string path);
}

/// <summary>Everything the editor decides.</summary>
public interface IEditorInteractor
{
    IEditorSession Open(PixelImage image, string? sourcePath);

    EditorOpenResult OpenFile(string path);

    EditorOpenResult OpenFromClipboard();

    /// <summary>Applies the Shift rules: lines snap to 0, 45 or 90 degrees, rectangles become squares, ellipses circles.</summary>
    DragShape ConstrainDrag(ToolKind tool, PixelPoint start, PixelPoint current, bool shiftHeld);

    /// <summary>
    /// Which annotation a click at <paramref name="point"/> (image pixels) selects, or null: the topmost one, meaning the last
    /// drawn, within <paramref name="tolerance"/> pixels. The rule lives here, not in the window, so it is tested without WPF.
    /// </summary>
    Guid? HitTest(IEditorSession session, PixelPoint point, int tolerance);

    /// <summary>
    /// The shape a drag makes with <paramref name="tool"/>, or null when it draws nothing (a click without a drag, a box with no width
    /// or no height, a tool that draws no dragged shape).
    /// </summary>
    Annotation? CreateShape(ToolKind tool, DragShape drag, RgbaColor color, int thickness);

    /// <summary>A pen or highlighter stroke; one click is a dot of two equal points.</summary>
    StrokeAnnotation CreateStroke(IReadOnlyList<PixelPoint> points, RgbaColor color, int thickness, bool highlighter);

    /// <summary>
    /// Sets the text of the text annotation <paramref name="id"/>; a text of blanks is empty and an empty text deletes the annotation.
    /// Returns whether anything changed (the same words change nothing and make no history step).
    /// </summary>
    bool EditText(IEditorSession session, Guid id, string text);

    /// <summary>Adds the text unless it is empty (SPEC editor F4); returns whether an annotation was created.</summary>
    bool AddText(IEditorSession session, PixelPoint origin, string text, RgbaColor color, int fontSize);

    SaveDecision DecideSave(IEditorSession session, AppSettings settings);

    /// <summary>Writes the flattened image; a failure keeps the window and every edit (SPEC editor F1).</summary>
    EditorSaveResult Save(IEditorSession session, PixelImage flattened, string path, AppSettings settings);

    EditorSaveResult Copy(PixelImage flattened);

    CloseAction DecideClose(IEditorSession session);
}
