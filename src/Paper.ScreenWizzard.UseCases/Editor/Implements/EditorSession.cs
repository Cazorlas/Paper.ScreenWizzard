using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UseCases.Editor.Implements;

/// <summary>
/// One image being edited and its history (SPEC editor, "Hình vẽ và lịch sử"). Every mutating call is one step: the history is
/// a list of immutable <see cref="EditorDocument"/> snapshots and a position in it. The image bytes are shared between
/// snapshots (only a crop makes new ones), and a step copies the annotation list, so a step costs O(annotations on one screenshot),
/// which is tens, and never the pixels.
/// </summary>
public sealed class EditorSession : IEditorSession
{
    private readonly List<EditorDocument> _history;
    private int _index;
    private int _savedIndex;
    private Guid? _selectedId;
    private EditorDocument? _renderedFor;
    private PixelImage? _rendered;

    public EditorSession(PixelImage image, string? sourcePath)
    {
        _history = [new EditorDocument(image, [])];
        SourcePath = sourcePath;
    }

    public string? SourcePath { get; private set; }

    public EditorDocument Document => _history[_index];

    public Guid? SelectedId => _selectedId;

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index < _history.Count - 1;

    /// <summary>The document differs from the saved one; -1 in <c>_savedIndex</c> means the saved snapshot is gone from the history.</summary>
    public bool IsDirty => _index != _savedIndex;

    public int NextStepNumber => AnnotationOps.NextStepNumber(Document.Annotations);

    public void Add(Annotation annotation) => Push(Document with { Annotations = [.. Document.Annotations, annotation] });

    public void Move(Guid id, int dx, int dy) => Replace(id, a => AnnotationOps.Translate(a, dx, dy));

    public void Recolor(Guid id, RgbaColor color) => Replace(id, a => a with { Color = color });

    public void SetThickness(Guid id, int thickness) => Replace(id, a => a with { Thickness = thickness });

    public void Delete(Guid id)
    {
        if (Document.Annotations.All(a => a.Id != id))
        {
            return;
        }

        Push(Document with { Annotations = Document.Annotations.Where(a => a.Id != id).ToArray() });
    }

    // SKELETONS (plan T24): the contract exists so tests can be written; the rules arrive with T26.
    public void SetText(Guid id, string text)
    {
    }

    public void SetFontSize(Guid id, int fontSize)
    {
    }

    public void Select(Guid? id) => _selectedId = id is not null && Document.Annotations.Any(a => a.Id == id) ? id : null;

    public CropOutcome Crop(PixelRect area)
    {
        var source = Document.Source;
        var clamped = EditorGeometry.ClampToImage(area, source.Width, source.Height);
        if (clamped is null)
        {
            return new CropOutcome(false, area, NotificationMessage.Of("Editor.CropInvalid"));
        }

        var kept = clamped.Value;
        var cropped = PixelImageOps.Crop(source, 0, 0, kept);
        var annotations = Document.Annotations
            .Where(a => AnnotationOps.Touches(a, kept))
            .Select(a => AnnotationOps.Translate(a, -kept.X, -kept.Y))
            .ToArray();
        Push(new EditorDocument(cropped, annotations));
        return new CropOutcome(true, kept, null);
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        _index--;
        KeepSelectionIfItStillExists();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        _index++;
        KeepSelectionIfItStillExists();
        return true;
    }

    /// <summary>The blur areas pixelated; remembered for the current snapshot, so a repaint does not pixelate again.</summary>
    public PixelImage RenderBase()
    {
        var document = Document;
        if (!ReferenceEquals(_renderedFor, document) || _rendered is null)
        {
            var areas = document.Annotations.OfType<BlurAnnotation>().Select(b => b.Area).ToArray();
            _rendered = Mosaic.Pixelate(document.Source, areas);
            _renderedFor = document;
        }

        return _rendered;
    }

    public void MarkSaved(string path)
    {
        _savedIndex = _index;
        SourcePath = path;
    }

    private void Replace(Guid id, Func<Annotation, Annotation> change)
    {
        var at = -1;
        for (var i = 0; i < Document.Annotations.Count; i++)
        {
            if (Document.Annotations[i].Id == id)
            {
                at = i;
                break;
            }
        }

        if (at < 0)
        {
            return;
        }

        var next = Document.Annotations.ToArray();
        next[at] = change(next[at]);
        Push(Document with { Annotations = next });
    }

    /// <summary>A new step: whatever was undone and not redone is dropped (SPEC editor: a new drawing after Undo ends Redo).</summary>
    private void Push(EditorDocument next)
    {
        if (CanRedo)
        {
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);
            if (_savedIndex > _index)
            {
                _savedIndex = -1;
            }
        }

        _history.Add(next);
        _index++;
        KeepSelectionIfItStillExists();
    }

    private void KeepSelectionIfItStillExists()
    {
        if (_selectedId is { } id && Document.Annotations.All(a => a.Id != id))
        {
            _selectedId = null;
        }
    }
}
