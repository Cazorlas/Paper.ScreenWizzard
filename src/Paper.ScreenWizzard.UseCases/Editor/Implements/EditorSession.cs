using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UseCases.Editor.Implements;

/// <summary>
/// SKELETON (plan T2): holds the image and does nothing else, so tests written from SPEC editor fail at their
/// assertions and not at the build. The history, numbering, crop and blur arrive with plan T12.
/// </summary>
public sealed class EditorSession : IEditorSession
{
    private readonly EditorDocument _document;

    public EditorSession(PixelImage image, string? sourcePath)
    {
        _document = new EditorDocument(image, []);
        SourcePath = sourcePath;
    }

    public string? SourcePath { get; }

    public EditorDocument Document => _document;

    public Guid? SelectedId => null;

    public bool CanUndo => false;

    public bool CanRedo => false;

    public bool IsDirty => false;

    public int NextStepNumber => 0;

    public void Add(Annotation annotation)
    {
    }

    public void Move(Guid id, int dx, int dy)
    {
    }

    public void Recolor(Guid id, RgbaColor color)
    {
    }

    public void SetThickness(Guid id, int thickness)
    {
    }

    public void Delete(Guid id)
    {
    }

    public void Select(Guid? id)
    {
    }

    public CropOutcome Crop(PixelRect area) => new(false, default, null);

    public bool Undo() => false;

    public bool Redo() => false;

    public PixelImage RenderBase() => _document.Source;

    public void MarkSaved(string path)
    {
    }
}
