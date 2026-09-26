using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
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
