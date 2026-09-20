using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Common;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

// The namespace is not ...UiTests.Editor on purpose: a namespace of that name would hide the Presentation.Views.Editor and
// ViewModels.Editor namespaces' short names, the same trap the capture lane met with FlaUI's Capture class.
namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>Plain data and pixel reading for the editor tests.</summary>
public static class EditorTestData
{
    public static readonly RgbaColor PureRed = new(255, 0, 0, 255);

    public static readonly RgbaColor PureYellow = new(255, 255, 0, 255);

    public static readonly RgbaColor PureBlue = new(0, 0, 255, 255);

    public static readonly RgbaColor Black = new(0, 0, 0, 255);

    public static readonly RgbaColor WhiteColor = new(255, 255, 255, 255);

    public static PixelImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = b;
            bgra[i + 1] = g;
            bgra[i + 2] = r;
            bgra[i + 3] = 255;
        }

        return new PixelImage(width, height, bgra);
    }

    public static PixelImage White(int width, int height) => Solid(width, height, 255, 255, 255);

    public static RgbaColor At(PixelImage image, int x, int y)
    {
        var i = ((y * image.Width) + x) * 4;
        return new RgbaColor(image.Bgra[i + 2], image.Bgra[i + 1], image.Bgra[i], image.Bgra[i + 3]);
    }

    /// <summary>True when every channel of the pixel is within <paramref name="tolerance"/> of <paramref name="color"/>.</summary>
    public static bool PixelIs(PixelImage image, int x, int y, RgbaColor color, int tolerance = 12)
    {
        var pixel = At(image, x, y);
        return Math.Abs(pixel.R - color.R) <= tolerance && Math.Abs(pixel.G - color.G) <= tolerance && Math.Abs(pixel.B - color.B) <= tolerance;
    }

    /// <summary>The pixel differs from white by at least <paramref name="threshold"/> in some channel: something was painted there.</summary>
    public static bool IsPainted(PixelImage image, int x, int y, int threshold = 20)
    {
        var pixel = At(image, x, y);
        return (255 - pixel.R) >= threshold || (255 - pixel.G) >= threshold || (255 - pixel.B) >= threshold;
    }

    /// <summary>The inclusive box around every painted pixel, or null when the image is still blank.</summary>
    public static (int MinX, int MinY, int MaxX, int MaxY)? PaintedBox(PixelImage image, int threshold = 20)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (IsPainted(image, x, y, threshold))
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    /// <summary>The painted box, failing the test with a plain message when nothing was painted at all.</summary>
    public static (int MinX, int MinY, int MaxX, int MaxY) PaintedBoxOrFail(PixelImage image, int threshold = 20)
    {
        var box = PaintedBox(image, threshold);
        Assert.That(box, Is.Not.Null, "nothing was painted on the image");
        return box!.Value;
    }

    public static int CountDifferent(PixelImage a, PixelImage b, int threshold)
    {
        var count = 0;
        for (var i = 0; i + 3 < a.Bgra.Length; i += 4)
        {
            if (Math.Abs(a.Bgra[i] - b.Bgra[i]) > threshold || Math.Abs(a.Bgra[i + 1] - b.Bgra[i + 1]) > threshold || Math.Abs(a.Bgra[i + 2] - b.Bgra[i + 2]) > threshold)
            {
                count++;
            }
        }

        return count;
    }

    public static StrokeAnnotation Stroke(RgbaColor color, int thickness, bool highlighter, params PixelPoint[] points) =>
        new(Guid.NewGuid(), color, thickness, points, highlighter);

    public static RectangleAnnotation Rectangle(RgbaColor color, int thickness, PixelRect bounds) => new(Guid.NewGuid(), color, thickness, bounds);

    public static TextAnnotation Text(RgbaColor color, PixelPoint origin, string text, int fontSize) => new(Guid.NewGuid(), color, 1, origin, text, fontSize);

    public static ArrowAnnotation Arrow(RgbaColor color, int thickness, PixelPoint from, PixelPoint to) => new(Guid.NewGuid(), color, thickness, from, to);

    /// <summary>The image the tests draw on: white, so any painted pixel is the drawing's.</summary>
    public static PixelImage Canvas300x200() => White(300, 200);

    /// <summary>Replaces the image of a document with a copy that has the drawings; the fake session keeps them as history steps.</summary>
    public static FakeEditorSession SessionWith(PixelImage image, params Annotation[] annotations)
    {
        var session = new FakeEditorSession(image);
        foreach (var annotation in annotations)
        {
            session.Add(annotation);
        }

        session.MarkSaved(string.Empty);
        return session;
    }
}

/// <summary>
/// A session on a list: every mutating call is one history step, snapshots are kept for Undo and Redo. It holds no rule of the real
/// use case beyond what the view model needs to be proven against (the rules are the logic lane's, tested in the unit project).
/// </summary>
public sealed class FakeEditorSession : IEditorSession
{
    private readonly List<(EditorDocument Document, Guid? Selected)> _undo = [];
    private readonly List<(EditorDocument Document, Guid? Selected)> _redo = [];
    private EditorDocument _document;
    private EditorDocument _saved;
    private Guid? _selected;

    public FakeEditorSession(PixelImage image, string? sourcePath = null)
    {
        _document = new EditorDocument(image, []);
        _saved = _document;
        SourcePath = sourcePath;
    }

    public string? SourcePath { get; private set; }

    public EditorDocument Document => _document;

    public Guid? SelectedId => _selected;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool IsDirty => !ReferenceEquals(_document, _saved);

    public int NextStepNumber => _document.Annotations.OfType<StepAnnotation>().Select(s => s.Number).DefaultIfEmpty(0).Max() + 1;

    /// <summary>How many steps Undo can take back: the history depth, for "one step per drag".</summary>
    public int HistorySteps => _undo.Count;

    public List<string> Calls { get; } = [];

    public void Add(Annotation annotation)
    {
        Calls.Add("Add");
        Push(_document with { Annotations = [.. _document.Annotations, annotation] });
    }

    public void Move(Guid id, int dx, int dy)
    {
        Calls.Add($"Move {dx},{dy}");
        Replace(id, annotation => Translate(annotation, dx, dy));
    }

    public void Recolor(Guid id, RgbaColor color)
    {
        Calls.Add("Recolor");
        Replace(id, annotation => annotation with { Color = color });
    }

    public void SetThickness(Guid id, int thickness)
    {
        Calls.Add("SetThickness");
        Replace(id, annotation => annotation with { Thickness = thickness });
    }

    // Plan T24 stubs so the fake still implements the contract; T27 gives them the behaviour the window is tested against.
    public void SetText(Guid id, string text) => Calls.Add("SetText");

    public void SetFontSize(Guid id, int fontSize) => Calls.Add("SetFontSize");

    public void Delete(Guid id)
    {
        Calls.Add("Delete");
        var next = _document with { Annotations = _document.Annotations.Where(a => a.Id != id).ToList() };
        var keepSelection = _selected == id ? null : _selected;
        Push(next, keepSelection);
    }

    public void Select(Guid? id)
    {
        Calls.Add("Select");
        _selected = id;
    }

    public CropOutcome Crop(PixelRect area)
    {
        Calls.Add("Crop");
        var left = Math.Max(0, area.X);
        var top = Math.Max(0, area.Y);
        var right = Math.Min(_document.Source.Width, area.X + area.Width);
        var bottom = Math.Min(_document.Source.Height, area.Y + area.Height);
        if (right - left < 1 || bottom - top < 1)
        {
            return new CropOutcome(false, default, NotificationMessage.Of("Editor.CropInvalid"));
        }

        var kept = new PixelRect(left, top, right - left, bottom - top);
        var cropped = PixelImageOps.Crop(_document.Source, 0, 0, kept);
        Push(new EditorDocument(cropped, _document.Annotations.Select(a => Translate(a, -kept.X, -kept.Y)).ToList()));
        return new CropOutcome(true, kept, null);
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        _redo.Add((_document, _selected));
        (_document, _selected) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        _undo.Add((_document, _selected));
        (_document, _selected) = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return true;
    }

    public PixelImage RenderBase() => _document.Source;

    public void MarkSaved(string path)
    {
        Calls.Add("MarkSaved");
        SourcePath = string.IsNullOrEmpty(path) ? SourcePath : path;
        _saved = _document;
    }

    private void Replace(Guid id, Func<Annotation, Annotation> change) =>
        Push(_document with { Annotations = _document.Annotations.Select(a => a.Id == id ? change(a) : a).ToList() });

    private void Push(EditorDocument next, Guid? selected = null)
    {
        _undo.Add((_document, _selected));
        _redo.Clear();
        _document = next;
        _selected = selected ?? (_selected is { } id && next.Annotations.Any(a => a.Id == id) ? id : null);
    }

    private static Annotation Translate(Annotation annotation, int dx, int dy) => annotation switch
    {
        StrokeAnnotation s => s with { Points = s.Points.Select(p => new PixelPoint(p.X + dx, p.Y + dy)).ToList() },
        LineAnnotation l => l with { From = Shift(l.From, dx, dy), To = Shift(l.To, dx, dy) },
        ArrowAnnotation a => a with { From = Shift(a.From, dx, dy), To = Shift(a.To, dx, dy) },
        RectangleAnnotation r => r with { Bounds = Shift(r.Bounds, dx, dy) },
        EllipseAnnotation e => e with { Bounds = Shift(e.Bounds, dx, dy) },
        TextAnnotation t => t with { Origin = Shift(t.Origin, dx, dy) },
        StepAnnotation s => s with { Center = Shift(s.Center, dx, dy) },
        BlurAnnotation b => b with { Area = Shift(b.Area, dx, dy) },
        _ => annotation,
    };

    private static PixelPoint Shift(PixelPoint p, int dx, int dy) => new(p.X + dx, p.Y + dy);

    private static PixelRect Shift(PixelRect r, int dx, int dy) => new(r.X + dx, r.Y + dy, r.Width, r.Height);
}

/// <summary>The editor use case on canned answers, with every call recorded.</summary>
public sealed class FakeEditorInteractor : IEditorInteractor
{
    public const string Folder = @"C:\Users\Test\Pictures\Paper.ScreenWizzard";

    public const string SuggestedName = "Screenshot 2026-09-20 14.03.05.png";

    public List<FakeEditorSession> Opened { get; } = [];

    public List<string> FilesOpened { get; } = [];

    public int ClipboardOpened { get; private set; }

    public List<(ToolKind Tool, PixelPoint Start, PixelPoint Current, bool Shift)> Constrained { get; } = [];

    public List<(PixelPoint Origin, string Text, RgbaColor Color, int FontSize)> TextCalls { get; } = [];

    public List<(IEditorSession Session, PixelImage Flattened, string Path)> Saves { get; } = [];

    public List<PixelImage> Copies { get; } = [];

    public int DecideSaveCalls { get; private set; }

    /// <summary>Decisions the next DecideSave calls return, in order; when empty the decision follows the session's SourcePath.</summary>
    public Queue<SaveDecision> Decisions { get; } = new();

    /// <summary>What OpenFile answers; by default a session on a 300 x 200 white image.</summary>
    public Func<string, EditorOpenResult> OpenFileAnswer { get; set; } = _ =>
        new EditorOpenResult(new FakeEditorSession(EditorTestData.Canvas300x200()), EditorOpenIssue.None, null);

    public Func<EditorOpenResult> ClipboardAnswer { get; set; } = () =>
        new EditorOpenResult(new FakeEditorSession(EditorTestData.Canvas300x200()), EditorOpenIssue.None, null);

    /// <summary>When set, Save fails with this message (SPEC editor F1) and changes nothing.</summary>
    public NotificationMessage? SaveFailsWith { get; set; }

    /// <summary>When set, Save fails and says nothing, the way a use case with a gap would (the view model must still tell the user).</summary>
    public bool SaveFailsSilently { get; set; }

    public NotificationMessage? CopyFailsWith { get; set; }

    public Func<ToolKind, PixelPoint, PixelPoint, bool, DragShape?>? ConstrainAnswer { get; set; }

    public IEditorSession Open(PixelImage image, string? sourcePath)
    {
        var session = new FakeEditorSession(image, sourcePath);
        Opened.Add(session);
        return session;
    }

    public EditorOpenResult OpenFile(string path)
    {
        FilesOpened.Add(path);
        return OpenFileAnswer(path);
    }

    public EditorOpenResult OpenFromClipboard()
    {
        ClipboardOpened++;
        return ClipboardAnswer();
    }

    public DragShape ConstrainDrag(ToolKind tool, PixelPoint start, PixelPoint current, bool shiftHeld)
    {
        Constrained.Add((tool, start, current, shiftHeld));
        if (ConstrainAnswer?.Invoke(tool, start, current, shiftHeld) is { } answer)
        {
            return answer;
        }

        if (!shiftHeld)
        {
            return new DragShape(start, current);
        }

        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        return tool switch
        {
            ToolKind.Rectangle or ToolKind.Ellipse => new DragShape(
                start,
                new PixelPoint(start.X + (Math.Sign(dx) * Math.Max(Math.Abs(dx), Math.Abs(dy))), start.Y + (Math.Sign(dy) * Math.Max(Math.Abs(dx), Math.Abs(dy))))),
            _ => new DragShape(start, current),
        };
    }

    // Plan T24 stub: nothing hit until T27 makes the fake answer like the real rule.
    public Guid? HitTest(IEditorSession session, PixelPoint point, int tolerance) => null;

    public bool AddText(IEditorSession session, PixelPoint origin, string text, RgbaColor color, int fontSize)
    {
        TextCalls.Add((origin, text, color, fontSize));
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        session.Add(new TextAnnotation(Guid.NewGuid(), color, 1, origin, text, fontSize));
        return true;
    }

    public SaveDecision DecideSave(IEditorSession session, AppSettings settings)
    {
        DecideSaveCalls++;
        if (Decisions.Count > 0)
        {
            return Decisions.Dequeue();
        }

        return session.SourcePath is null
            ? new SaveDecision(SaveAction.AskSaveAs, Folder, SuggestedName, null)
            : new SaveDecision(SaveAction.AskOverwriteOrCopy, Folder, SuggestedName, session.SourcePath);
    }

    public EditorSaveResult Save(IEditorSession session, PixelImage flattened, string path, AppSettings settings)
    {
        Saves.Add((session, flattened, path));
        if (SaveFailsSilently)
        {
            return new EditorSaveResult(false, null, null);
        }

        if (SaveFailsWith is { } failure)
        {
            return new EditorSaveResult(false, null, failure);
        }

        session.MarkSaved(path);
        return new EditorSaveResult(true, path, NotificationMessage.Of("Editor.Saved", path));
    }

    public EditorSaveResult Copy(PixelImage flattened)
    {
        Copies.Add(flattened);
        return CopyFailsWith is { } failure
            ? new EditorSaveResult(false, null, failure)
            : new EditorSaveResult(true, null, NotificationMessage.Of("Editor.Copied", flattened.Width.ToString(), flattened.Height.ToString()));
    }

    public CloseAction DecideClose(IEditorSession session) => session.IsDirty ? CloseAction.AskSaveDiscardCancel : CloseAction.Close;
}

public sealed class FakeEditorPrompts : IEditorPrompts
{
    public OverwriteChoice OverwriteAnswer { get; set; } = OverwriteChoice.Cancel;

    /// <summary>Discard by default, so a test that closes a dirty window in its TearDown never hangs on a question nobody answers.</summary>
    public CloseChoice CloseAnswer { get; set; } = CloseChoice.Discard;

    public List<string> OverwriteAsked { get; } = [];

    public int CloseAsked { get; private set; }

    public OverwriteChoice AskOverwriteOrCopy(string path)
    {
        OverwriteAsked.Add(path);
        return OverwriteAnswer;
    }

    public CloseChoice AskSaveDiscardCancel()
    {
        CloseAsked++;
        return CloseAnswer;
    }
}

/// <summary>Editor windows the flow "opened", recorded instead of shown.</summary>
public sealed class FakeEditorViews : IEditorViews
{
    public List<FakeHandle<EditorViewModel>> Opened { get; } = [];

    public int OpenCount => Opened.Count(o => !o.IsClosed);

    public IViewHandle OpenEditor(EditorViewModel viewModel)
    {
        var handle = new FakeHandle<EditorViewModel>(viewModel);
        Opened.Add(handle);
        return handle;
    }
}

/// <summary>An editor view model on fakes, on the thread of the caller. No window: the pointer is driven in image pixels.</summary>
public sealed class EditorRig
{
    private EditorRig(
        FakeEditorSession session,
        FakeEditorInteractor interactor,
        RecordingNotifications notes,
        FakeFileDialogs dialogs,
        FakeEditorPrompts prompts,
        FakeEditorViews views,
        EditorFlow flow,
        EditorViewModel viewModel)
    {
        Session = session;
        Interactor = interactor;
        Notes = notes;
        Dialogs = dialogs;
        Prompts = prompts;
        Views = views;
        Flow = flow;
        ViewModel = viewModel;
    }

    public FakeEditorSession Session { get; }

    public FakeEditorInteractor Interactor { get; }

    public RecordingNotifications Notes { get; }

    public FakeFileDialogs Dialogs { get; }

    public FakeEditorPrompts Prompts { get; }

    public FakeEditorViews Views { get; }

    public EditorFlow Flow { get; }

    public EditorViewModel ViewModel { get; }

    public static EditorRig Create(
        PixelImage? image = null,
        string? sourcePath = null,
        ILocalizer? localizer = null,
        IEditorFlattener? flattener = null,
        IEditorPrompts? prompts = null)
    {
        var interactor = new FakeEditorInteractor();
        var notes = new RecordingNotifications();
        var dialogs = new FakeFileDialogs();
        var fakePrompts = new FakeEditorPrompts();
        var views = new FakeEditorViews();
        var services = new EditorServices(
            interactor,
            notes,
            dialogs,
            prompts ?? fakePrompts,
            flattener ?? new WpfImageFlattener(),
            localizer ?? new FakeLocalizer(),
            () => ShellTestData.DefaultSettings());
        var flow = new EditorFlow(services, views);
        var session = new FakeEditorSession(image ?? EditorTestData.Canvas300x200(), sourcePath);
        var viewModel = new EditorViewModel(session, services, flow);
        return new EditorRig(session, interactor, notes, dialogs, fakePrompts, views, flow, viewModel);
    }

    public void Click(int x, int y, bool shift = false)
    {
        ViewModel.PointerDown(new PixelPoint(x, y), shift);
        ViewModel.PointerUp(new PixelPoint(x, y), shift);
    }

    /// <summary>Press at <paramref name="from"/>, move through <paramref name="via"/>, release at <paramref name="to"/>.</summary>
    public void Drag(PixelPoint from, PixelPoint to, bool shift = false, params PixelPoint[] via)
    {
        ViewModel.PointerDown(from, shift);
        foreach (var point in via)
        {
            ViewModel.PointerMoved(point, shift);
        }

        ViewModel.PointerMoved(to, shift);
        ViewModel.PointerUp(to, shift);
    }

    public void Draw(ToolKind tool, PixelPoint from, PixelPoint to, bool shift = false)
    {
        ViewModel.SelectTool(tool);
        Drag(from, to, shift);
    }
}
