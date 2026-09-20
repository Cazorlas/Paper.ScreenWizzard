using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.UseCases.Common.Ports;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.UseCases.Editor.Implements;

/// <summary>
/// SKELETON (plan T2): every answer is the empty one, so tests written from SPEC editor fail at their assertions and not
/// at the build. The rules arrive with plan T12.
/// </summary>
public sealed class EditorInteractor : IEditorInteractor
{
    private readonly IFileStorePort _files;
    private readonly IImageCodecPort _codec;
    private readonly IClipboardPort _clipboard;
    private readonly IImageDelivery _delivery;
    private readonly ILogPort _log;

    public EditorInteractor(
        IFileStorePort files,
        IImageCodecPort codec,
        IClipboardPort clipboard,
        IImageDelivery delivery,
        ILogPort log)
    {
        _files = files;
        _codec = codec;
        _clipboard = clipboard;
        _delivery = delivery;
        _log = log;
    }

    public IEditorSession Open(PixelImage image, string? sourcePath) => new EditorSession(image, sourcePath);

    public EditorOpenResult OpenFile(string path) => new(null, EditorOpenIssue.None, null);

    public EditorOpenResult OpenFromClipboard() => new(null, EditorOpenIssue.None, null);

    public DragShape ConstrainDrag(ToolKind tool, PixelPoint start, PixelPoint current, bool shiftHeld) =>
        new(start, start);

    public bool AddText(IEditorSession session, PixelPoint origin, string text, RgbaColor color, int fontSize) => false;

    public SaveDecision DecideSave(IEditorSession session, AppSettings settings) =>
        new(SaveAction.WriteDirect, string.Empty, string.Empty, null);

    public EditorSaveResult Save(IEditorSession session, PixelImage flattened, string path, AppSettings settings) =>
        new(false, null, null);

    public EditorSaveResult Copy(PixelImage flattened) => new(false, null, null);

    public CloseAction DecideClose(IEditorSession session) => CloseAction.Close;
}
