using System.Globalization;
using System.Runtime.CompilerServices;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.UseCases.Editor.UseCases;

/// <summary>
/// Everything the editor decides (SPEC editor): opening, the Shift rules, the empty text, where Ctrl+S goes, what a failed save
/// keeps, and whether closing asks. The image and its history live in <see cref="EditorSession"/>.
/// </summary>
public sealed class EditorInteractor : IEditorInteractor
{
    private readonly IFileStore _files;
    private readonly IImageCodec _codec;
    private readonly IClipboard _clipboard;
    private readonly IImageDelivery _delivery;
    private readonly IClock _clock;
    private readonly ILog _log;

    // What the interactor remembers about a session's file, kept beside the session because IEditorSession has no room for it
    // and dies with it: the write time of the file when it was opened or last written, and whether overwriting was agreed.
    private readonly ConditionalWeakTable<IEditorSession, FileState> _fileStates = new();

    public EditorInteractor(
        IFileStore files,
        IImageCodec codec,
        IClipboard clipboard,
        IImageDelivery delivery,
        IClock clock,
        ILog log)
    {
        _files = files;
        _codec = codec;
        _clipboard = clipboard;
        _delivery = delivery;
        _clock = clock;
        _log = log;
    }

    public IEditorSession Open(PixelImage image, string? sourcePath)
    {
        var session = new EditorSession(image, sourcePath);
        Remember(session, sourcePath);
        return session;
    }

    public EditorOpenResult OpenFile(string path)
    {
        var read = _files.ReadAllBytes(path);
        if (!read.Success || read.Bytes is null)
        {
            _log.Warning("Editor: could not read " + path + ": " + read.Detail);
            return Failed(EditorOpenIssue.ReadFailed, NotificationMessage.Of("Editor.ReadFailed", path, read.Detail ?? string.Empty));
        }

        var decoded = _codec.Decode(read.Bytes);
        switch (decoded.Issue)
        {
            case ImageDecodeIssue.TooLarge:
                _log.Warning("Editor: image too large: " + path);
                return Failed(EditorOpenIssue.TooLarge, NotificationMessage.Of("Editor.ImageTooLarge", path));
            case ImageDecodeIssue.NotAnImage:
                _log.Warning("Editor: not an image: " + path);
                return Failed(EditorOpenIssue.NotAnImage, NotificationMessage.Of("Editor.NotAnImage", path));
        }

        if (decoded.Image is null)
        {
            _log.Warning("Editor: the codec gave no image for " + path);
            return Failed(EditorOpenIssue.NotAnImage, NotificationMessage.Of("Editor.NotAnImage", path));
        }

        return new EditorOpenResult(Open(decoded.Image, path), EditorOpenIssue.None, null);
    }

    public EditorOpenResult OpenFromClipboard()
    {
        var clip = _clipboard.GetImage();
        if (clip.ReadFailed)
        {
            _log.Warning("Editor: the clipboard could not be read: " + clip.Detail);
            return Failed(EditorOpenIssue.ReadFailed, NotificationMessage.Of("Editor.ClipboardReadFailed", clip.Detail ?? string.Empty));
        }

        if (!clip.HasImage || clip.Image is null)
        {
            return Failed(EditorOpenIssue.ClipboardHasNoImage, NotificationMessage.Of("Editor.ClipboardHasNoImage"));
        }

        return new EditorOpenResult(Open(clip.Image, null), EditorOpenIssue.None, null);
    }

    public DragShape ConstrainDrag(ToolKind tool, PixelPoint start, PixelPoint current, bool shiftHeld) =>
        new(start, EditorGeometry.ConstrainEnd(tool, start, current, shiftHeld));

    /// <summary>
    /// Which drawing a click lands on: the last drawn one that is hit, else null. A linear scan from the top of the drawing order,
    /// each drawing costing O(its points) with no allocation but a text's line split, so a click costs O(all points on the
    /// screenshot), which is hundreds; it stops at the first hit, so a busy image is not scanned in full when the click is on top.
    /// </summary>
    public Guid? HitTest(IEditorSession session, PixelPoint point, int tolerance)
    {
        var reach = Math.Max(tolerance, 0);
        var annotations = session.Document.Annotations;
        for (var i = annotations.Count - 1; i >= 0; i--)
        {
            if (AnnotationHit.IsHit(annotations[i], point, reach))
            {
                return annotations[i].Id;
            }
        }

        return null;
    }

    public Annotation? CreateShape(ToolKind tool, DragShape drag, RgbaColor color, int thickness) =>
        AnnotationFactory.Shape(tool, drag.From, drag.To, color, thickness);

    public StrokeAnnotation CreateStroke(IReadOnlyList<PixelPoint> points, RgbaColor color, int thickness, bool highlighter) =>
        AnnotationFactory.Stroke(points, color, thickness, highlighter);

    public bool EditText(IEditorSession session, Guid id, string text)
    {
        var edited = session.Document.Annotations.OfType<TextAnnotation>().FirstOrDefault(t => t.Id == id);
        var newText = AnnotationFactory.NormalizeText(text);
        if (edited is null || edited.Text == newText)
        {
            return false;
        }

        session.SetText(id, newText);
        return true;
    }

    public bool AddText(IEditorSession session, PixelPoint origin, string text, RgbaColor color, int fontSize)
    {
        // An empty box is what the user meant (SPEC editor F4): nothing is created and nothing is said.
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        session.Add(new TextAnnotation(Guid.NewGuid(), color, 1, origin, text, fontSize));
        return true;
    }

    public SaveDecision DecideSave(IEditorSession session, AppSettings settings)
    {
        var path = session.SourcePath;
        if (path is null || !ScreenshotNaming.CanWriteInPlace(path))
        {
            // A .bmp is read only: its own name would end up holding PNG bytes, so the copy goes through Save as.
            return AskSaveAs(SaveAction.AskSaveAs, settings, null);
        }

        var state = StateOf(session);
        if (_files.GetLastWriteTimeUtc(path) != state.LastWriteUtc)
        {
            // F9: someone else changed or removed the file since we last saw it; do not write over it, offer "Save as" instead.
            return AskSaveAs(SaveAction.FileChangedOnDisk, settings, path);
        }

        return AskSaveAs(state.OverwriteConfirmed ? SaveAction.WriteDirect : SaveAction.AskOverwriteOrCopy, settings, path);
    }

    public EditorSaveResult Save(IEditorSession session, PixelImage flattened, string path, AppSettings settings)
    {
        path = ScreenshotNaming.WithWritableExtension(path);
        var written = _delivery.SaveToPath(flattened, path, settings.JpgQuality);
        if (!written.Success)
        {
            // F1: nothing is marked saved, every edit stays, and the message names the path and the system's reason.
            _log.Warning("Editor: could not save " + path + ": " + written.Detail);
            return new EditorSaveResult(false, null, NotificationMessage.Of("Editor.SaveFailed", path, written.Detail ?? string.Empty));
        }

        session.MarkSaved(path);
        var state = StateOf(session);
        state.LastWriteUtc = _files.GetLastWriteTimeUtc(path);

        // MarkSaved made the path the session's file, so it equals SourcePath from here: Ctrl+S writes to it without asking again.
        state.OverwriteConfirmed = string.Equals(path, session.SourcePath, StringComparison.Ordinal);
        return new EditorSaveResult(true, path, NotificationMessage.Of("Editor.Saved", path));
    }

    public EditorSaveResult Copy(PixelImage flattened)
    {
        var result = _delivery.CopyToClipboard(flattened);
        if (!result.Success)
        {
            _log.Warning("Editor: could not copy to the clipboard: " + result.Detail);
            return new EditorSaveResult(false, null, NotificationMessage.Of("Editor.CopyFailed", result.Detail ?? string.Empty));
        }

        return new EditorSaveResult(
            true,
            null,
            NotificationMessage.Of(
                "Editor.Copied",
                flattened.Width.ToString(CultureInfo.InvariantCulture),
                flattened.Height.ToString(CultureInfo.InvariantCulture)));
    }

    public CloseAction DecideClose(IEditorSession session) =>
        session.IsDirty ? CloseAction.AskSaveDiscardCancel : CloseAction.Close;

    private static EditorOpenResult Failed(EditorOpenIssue issue, NotificationMessage message) => new(null, issue, message);

    private SaveDecision AskSaveAs(SaveAction action, AppSettings settings, string? path)
    {
        var folder = settings.SaveFolder;
        var name = ScreenshotNaming.FileName(_clock.Now, settings.Format, taken => _files.FileExists(Path.Combine(folder, taken)));
        return new SaveDecision(action, folder, name, path);
    }

    private void Remember(IEditorSession session, string? sourcePath) =>
        _fileStates.AddOrUpdate(session, new FileState(sourcePath is null ? null : _files.GetLastWriteTimeUtc(sourcePath)));

    private FileState StateOf(IEditorSession session) =>
        _fileStates.GetValue(session, s => new FileState(s.SourcePath is null ? null : _files.GetLastWriteTimeUtc(s.SourcePath)));

    private sealed class FileState
    {
        public FileState(DateTime? lastWriteUtc) => LastWriteUtc = lastWriteUtc;

        public DateTime? LastWriteUtc { get; set; }

        public bool OverwriteConfirmed { get; set; }
    }
}
