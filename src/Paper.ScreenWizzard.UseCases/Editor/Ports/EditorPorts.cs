using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
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

    /// <summary>Adds the text unless it is empty (SPEC editor F4); returns whether an annotation was created.</summary>
    bool AddText(IEditorSession session, PixelPoint origin, string text, RgbaColor color, int fontSize);

    SaveDecision DecideSave(IEditorSession session, AppSettings settings);

    /// <summary>Writes the flattened image; a failure keeps the window and every edit (SPEC editor F1).</summary>
    EditorSaveResult Save(IEditorSession session, PixelImage flattened, string path, AppSettings settings);

    EditorSaveResult Copy(PixelImage flattened);

    CloseAction DecideClose(IEditorSession session);
}
