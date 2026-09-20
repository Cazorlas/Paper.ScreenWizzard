using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor.Commands;
using Paper.ScreenWizzard.UseCases.Common.Models;
using Paper.ScreenWizzard.UseCases.Editor.Models;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Editor;

/// <summary>
/// One editor window (SPEC editor): the image, the tool, colour, thickness, zoom, and what a mouse drag or a click does with them. The
/// session holds the drawings and the history; this holds only what is being done right now (the tool, the shape being dragged, the crop
/// region, the text being typed) and shows what the session says. Every position it takes and every one it gives to the session is an IMAGE
/// pixel: the window converts from its own units in <see cref="EditorCoordinates"/> and nowhere else.
/// </summary>
public sealed class EditorViewModel : BindableBase, IDisposable
{
    private const double MinimumZoom = 0.1;
    private const double MaximumZoom = 8.0;
    private const int MinimumThickness = 1;
    private const int MaximumThickness = 20;
    private const int MinimumFontSize = 6;
    private const int MaximumFontSize = 200;

    // The space around the image inside the scroll area, both sides together, in display units (the stage's margin in the window).
    private const double StageMargin = 24.0;

    // A click may miss a thin line by this many display units and still select it; the use case is asked in image pixels (this over the zoom).
    private const double HitToleranceInViewUnits = 4.0;

    private readonly EditorServices _services;
    private readonly EditorFlow _flow;
    private readonly UndoCommand _undoCommand;
    private readonly RedoCommand _redoCommand;
    private readonly DeleteSelectionCommand _deleteCommand;
    private ToolKind _tool = ToolKind.Select;
    private RgbaColor _color = EditorColors.Default;
    private int _thickness = 4;
    private int _fontSize = 18;
    private string _fontSizeText = "18";
    private double _zoom = 1.0;
    private double _dpiScale = 1.0;
    private double _viewportWidth;
    private double _viewportHeight;
    private bool _fit = true;
    private string _customColorText = string.Empty;
    private bool _customColorError;
    private PaletteColorViewModel? _customSwatch;
    private Gesture? _gesture;
    private Annotation? _draft;
    private Guid? _draftReplaces;
    private PixelRect? _cropRect;
    private PixelPoint? _textOrigin;
    private string _textDraftText = string.Empty;
    private Guid? _editingTextId;
    private int? _pendingFontSize;
    private bool _thicknessDragging;
    private int? _thicknessDragValue;
    private EditorDocument? _baseFor;
    private PixelImage? _basePixels;

    public EditorViewModel(IEditorSession session, EditorServices services, EditorFlow flow)
    {
        Session = session;
        _services = services;
        _flow = flow;

        foreach (var (key, color) in EditorColors.Palette)
        {
            Palette.Add(new PaletteColorViewModel(key, color, services.Localizer, () => ShownColor));
        }

        _undoCommand = new UndoCommand(this);
        _redoCommand = new RedoCommand(this);
        _deleteCommand = new DeleteSelectionCommand(this);
        SelectToolCommand = new SelectToolCommand(this);
        SetColorCommand = new SetColorCommand(this);
        ApplyCustomColorCommand = new ApplyCustomColorCommand(this);
        ConfirmCommand = new ConfirmCommand(this);
        CancelCommand = new CancelCommand(this);
        CommitTextCommand = new CommitTextCommand(this);
        ZoomFitCommand = new ZoomFitCommand(this);
        Zoom100Command = new ZoomActualSizeCommand(this);
        SaveCommand = new SaveEditedImageCommand(this);
        SaveAsCommand = new SaveEditedImageAsCommand(this);
        CopyCommand = new CopyEditedImageCommand(this);
        PasteCommand = new PasteImageCommand(this);
        OpenFileCommand = new OpenFileCommand(this);
        services.Localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>The picture on the canvas is out of date: the session, the zoom, the selection or a shape being dragged changed.</summary>
    public event EventHandler? CanvasInvalidated;

    public IEditorSession Session { get; }

    /// <summary>The tool; the toolbar's radio buttons write it, and choosing another tool than Select lets go of the selection.</summary>
    public ToolKind Tool
    {
        get => _tool;
        set => SelectTool(value);
    }

    /// <summary>The colour of the NEXT shapes. The selected shape's own colour is <see cref="ShownColor"/>.</summary>
    public RgbaColor Color => _color;

    /// <summary>The colour the palette shows as current: the selected shape's, else the next shapes'.</summary>
    public RgbaColor ShownColor => SelectedAnnotation?.Color ?? _color;

    /// <summary>
    /// The thickness shown by the slider: the selected shape's, else the next shapes'. Setting it changes the selected shape (one history
    /// step, and none when the value is the one it already has) or, with nothing selected, the next shapes. While the slider's thumb is held
    /// (<see cref="BeginThicknessChange"/> to <see cref="EndThicknessChange"/>) a selected shape is only previewed, and the whole drag is ONE step.
    /// </summary>
    public int Thickness
    {
        get => _thicknessDragValue ?? SelectedAnnotation?.Thickness ?? _thickness;
        set
        {
            var clamped = Math.Clamp(value, MinimumThickness, MaximumThickness);
            if (SelectedAnnotation is { } selected)
            {
                if (_thicknessDragging)
                {
                    _thicknessDragValue = clamped;
                    _draft = selected with { Thickness = clamped };
                    _draftReplaces = selected.Id;
                    RaisePropertyChanged();
                    Invalidate();
                }
                else if (selected.Thickness != clamped)
                {
                    CommitPending();
                    Session.SetThickness(selected.Id, clamped);
                    NotifySessionChanged();
                }

                return;
            }

            if (_thickness != clamped)
            {
                _thickness = clamped;
                RaisePropertyChanged();
            }
            else if (value != clamped)
            {
                // The slider or box holds a value out of range: tell it the real one.
                RaisePropertyChanged();
            }
        }
    }

    /// <summary>
    /// The size shown by the size box: the selected text's or step marker's, else the size of the NEXT text and step markers. Setting it
    /// changes the selected one (one history step, none when it already has that size) or, with none selected, the next ones.
    /// </summary>
    public int FontSize
    {
        get => SelectedSizedAnnotation?.Size ?? _fontSize;
        set
        {
            var clamped = Math.Clamp(value, MinimumFontSize, MaximumFontSize);
            if (SelectedSizedAnnotation is { } selected)
            {
                _pendingFontSize = null;
                if (selected.Size != clamped)
                {
                    Session.SetFontSize(selected.Id, clamped);
                    NotifySessionChanged();
                }

                return;
            }

            if (SetProperty(ref _fontSize, clamped))
            {
                _fontSizeText = clamped.ToString(CultureInfo.InvariantCulture);
                RaisePropertyChanged(nameof(FontSizeText));
            }
        }
    }

    /// <summary>
    /// The font-size box's text. Only a whole number in range is taken; anything else leaves the size and the text alone while typing. With
    /// nothing selected a valid number is the next size at once; with a text or step marker selected it waits, however many keys it takes,
    /// until <see cref="CommitFontSize"/> (Enter, or the box losing the keyboard), and is then ONE change of that annotation.
    /// </summary>
    public string FontSizeText
    {
        get => _fontSizeText;
        set
        {
            _fontSizeText = value;
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed is >= MinimumFontSize and <= MaximumFontSize)
            {
                if (SelectedSizedAnnotation is not null)
                {
                    _pendingFontSize = parsed;
                }
                else
                {
                    SetProperty(ref _fontSize, parsed, nameof(FontSize));
                }
            }

            RaisePropertyChanged();
        }
    }

    public double Zoom => _zoom;

    public string ZoomText => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(_zoom * 100)}%");

    /// <summary>True until the user picks a zoom: the image is then fitted to the window and follows its size.</summary>
    public bool IsFitMode => _fit;

    /// <summary>The monitor's scale (1.0 at 96 dpi, 1.5 at 150%): the window reports it, and the zoom means physical pixels.</summary>
    public double DpiScale => _dpiScale;

    /// <summary>Display units per image pixel: how big the canvas draws one pixel of the image.</summary>
    public double ViewScale => EditorCoordinates.ViewUnitsPerImagePixel(_zoom, _dpiScale);

    public string SizeText => string.Create(CultureInfo.InvariantCulture, $"{Session.Document.Source.Width} × {Session.Document.Source.Height}");

    /// <summary>"Unsaved" for a capture that was never saved and for edits since the last save; "Saved" for a file nobody changed.</summary>
    public string SaveStateText => _services.Localizer.GetString(Session.SourcePath is null || Session.IsDirty ? "Editor.Status.Unsaved" : "Editor.Status.Saved");

    /// <summary>What the status bar says the keys do right now (crop, text), or the reason a colour code was refused.</summary>
    public string HintText
    {
        get
        {
            if (_customColorError)
            {
                return _services.Localizer.GetString("Editor.Color.Invalid");
            }

            if (IsEditingText)
            {
                return _services.Localizer.GetString("Editor.Hint.Text");
            }

            return _tool == ToolKind.Crop && _cropRect is not null ? _services.Localizer.GetString("Editor.Hint.Crop") : string.Empty;
        }
    }

    public ObservableCollection<PaletteColorViewModel> Palette { get; } = [];

    public string CustomColorText
    {
        get => _customColorText;
        set
        {
            if (SetProperty(ref _customColorText, value) && _customColorError)
            {
                // Typing again withdraws the complaint about the last code.
                _customColorError = false;
                RaisePropertyChanged(nameof(HasCustomColorError));
                RaisePropertyChanged(nameof(HintText));
            }
        }
    }

    public bool HasCustomColorError => _customColorError;

    /// <summary>The shape being dragged, or the moved copy of the shape being moved: the canvas draws it over the image.</summary>
    public Annotation? Draft => _draft;

    /// <summary>While a shape is being moved, the id of the original: the canvas leaves it out, because <see cref="Draft"/> is where it is now.</summary>
    public Guid? DraftReplaces => _draftReplaces;

    /// <summary>The crop region chosen so far (not clamped: the session clamps it), until Enter cuts it or Esc drops it.</summary>
    public PixelRect? CropRect => _cropRect;

    /// <summary>Where the text box is (its top-left, in image pixels) while text is typed.</summary>
    public PixelPoint? TextDraftOrigin => _textOrigin;

    public string TextDraftText
    {
        get => _textDraftText;
        set => SetProperty(ref _textDraftText, value ?? string.Empty);
    }

    public bool IsEditingText => _textOrigin is not null;

    /// <summary>The colour of the text in the box: the edited text's own, else the colour of the next shapes.</summary>
    public RgbaColor TextDraftColor => EditedText?.Color ?? _color;

    /// <summary>The size of the text in the box: the edited text's own, else the size of the next text.</summary>
    public int TextDraftFontSize => EditedText?.FontSize ?? _fontSize;

    /// <summary>The image with its mosaic, as the use case renders it; recomputed only when the document changes.</summary>
    public PixelImage BasePixels
    {
        get
        {
            var document = Session.Document;
            if (_basePixels is null || !ReferenceEquals(_baseFor, document))
            {
                _basePixels = Session.RenderBase();
                _baseFor = document;
            }

            return _basePixels;
        }
    }

    public IReadOnlyList<Annotation> Annotations => Session.Document.Annotations;

    public Guid? SelectedId => Session.SelectedId;

    public ICommand SelectToolCommand { get; }

    public ICommand SetColorCommand { get; }

    public ICommand ApplyCustomColorCommand { get; }

    public ICommand UndoCommand => _undoCommand;

    public ICommand RedoCommand => _redoCommand;

    public ICommand DeleteCommand => _deleteCommand;

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand CommitTextCommand { get; }

    public ICommand ZoomFitCommand { get; }

    public ICommand Zoom100Command { get; }

    public ICommand SaveCommand { get; }

    public ICommand SaveAsCommand { get; }

    public ICommand CopyCommand { get; }

    public ICommand PasteCommand { get; }

    public ICommand OpenFileCommand { get; }

    private Annotation? SelectedAnnotation =>
        Session.SelectedId is { } id ? Session.Document.Annotations.FirstOrDefault(a => a.Id == id) : null;

    // The selected annotation when it has a font size (a text or a step marker), with that size.
    private (Guid Id, int Size)? SelectedSizedAnnotation => SelectedAnnotation switch
    {
        TextAnnotation text => (text.Id, text.FontSize),
        StepAnnotation step => (step.Id, step.FontSize),
        _ => null,
    };

    // The committed text a double-click reopened, while its box is open.
    private TextAnnotation? EditedText =>
        _editingTextId is { } id ? Session.Document.Annotations.FirstOrDefault(a => a.Id == id) as TextAnnotation : null;

    // ---- tools, colour, thickness ----

    /// <summary>Chooses the tool. Leaving Select lets go of the selection, so a colour chosen next sets the next shapes and not the old one.</summary>
    public void SelectTool(ToolKind tool)
    {
        CommitPending();
        _gesture = null;
        _draft = null;
        _draftReplaces = null;
        if (tool != ToolKind.Crop)
        {
            _cropRect = null;
        }

        var changed = _tool != tool;
        _tool = tool;
        if (tool != ToolKind.Select && Session.SelectedId is not null)
        {
            Session.Select(null);
        }

        RaisePropertyChanged(nameof(Tool));
        if (changed)
        {
            NotifySessionChanged();
        }
    }

    /// <summary>A swatch was chosen: the selected shape takes the colour (one history step), else the next shapes will.</summary>
    public void SetColor(RgbaColor color)
    {
        CommitPending();
        if (Session.SelectedId is { } id)
        {
            if (SelectedAnnotation?.Color != color)
            {
                Session.Recolor(id, color);
            }
        }
        else if (_color != color)
        {
            _color = color;
            RaisePropertyChanged(nameof(Color));
        }

        NotifySessionChanged();
    }

    internal void ApplyCustomColor()
    {
        if (!EditorColors.TryParseHex(_customColorText, out var color))
        {
            _customColorError = true;
            RaisePropertyChanged(nameof(HasCustomColorError));
            RaisePropertyChanged(nameof(HintText));
            return;
        }

        _customColorError = false;
        RaisePropertyChanged(nameof(HasCustomColorError));

        // A colour that is already a swatch just selects it; anything else takes the one extra swatch after the eight.
        if (Palette.All(swatch => swatch.Color != color))
        {
            if (_customSwatch is null)
            {
                _customSwatch = new PaletteColorViewModel(PaletteColorViewModel.CustomKey, color, _services.Localizer, () => ShownColor);
                Palette.Add(_customSwatch);
            }
            else
            {
                _customSwatch.SetColor(color);
            }
        }

        SetColor(color);
    }

    // ---- zoom ----

    /// <summary>Sets the zoom (10% to 800%) and leaves fit mode.</summary>
    public void SetZoom(double zoom)
    {
        _fit = false;
        ApplyZoom(zoom);
    }

    public void ZoomBy(double factor) => SetZoom(_zoom * factor);

    /// <summary>Fits the whole image into the window (never above 100%) and follows the window's size from now on.</summary>
    public void FitToViewport()
    {
        _fit = true;
        RaisePropertyChanged(nameof(IsFitMode));
        RefitIfNeeded();
    }

    /// <summary>The scroll area's size in display units and the monitor's scale, reported by the window whenever either changes.</summary>
    public void SetViewport(double widthUnits, double heightUnits, double dpiScale)
    {
        if (dpiScale > 0)
        {
            _dpiScale = dpiScale;
        }

        _viewportWidth = widthUnits;
        _viewportHeight = heightUnits;
        if (_fit)
        {
            RefitIfNeeded();
        }
        else
        {
            RaiseZoomChanged();
        }
    }

    private void RefitIfNeeded()
    {
        if (!_fit || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        var source = Session.Document.Source;
        var fitWidth = Math.Max(1.0, _viewportWidth - StageMargin) * _dpiScale / Math.Max(1, source.Width);
        var fitHeight = Math.Max(1.0, _viewportHeight - StageMargin) * _dpiScale / Math.Max(1, source.Height);

        // Never enlarge a small image to fill the window: 100% is its own size.
        ApplyZoom(Math.Min(1.0, Math.Min(fitWidth, fitHeight)));
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        RaisePropertyChanged(nameof(IsFitMode));
        RaiseZoomChanged();
    }

    private void RaiseZoomChanged()
    {
        RaisePropertyChanged(nameof(Zoom));
        RaisePropertyChanged(nameof(ZoomText));
        RaisePropertyChanged(nameof(ViewScale));
        RaisePropertyChanged(nameof(DpiScale));
        Invalidate();
    }

    // ---- the pointer, in image pixels ----

    public void PointerDown(PixelPoint point, bool shiftHeld)
    {
        CommitFontSize();
        if (IsEditingText)
        {
            // A click elsewhere finishes the text; it does not also start another box (SPEC editor: bấm ra ngoài chốt chữ).
            CommitText();
            return;
        }

        switch (_tool)
        {
            case ToolKind.Select:
                SelectAt(point, HitAt(point));
                break;
            case ToolKind.Pen or ToolKind.Highlighter:
                _gesture = new Gesture(GestureKind.Stroke, point, null, [point]);
                _draft = new StrokeAnnotation(Guid.NewGuid(), _color, _thickness, [point], _tool == ToolKind.Highlighter);
                Invalidate();
                break;
            case ToolKind.Line or ToolKind.Arrow or ToolKind.Rectangle or ToolKind.Ellipse or ToolKind.Blur or ToolKind.Crop:
                _gesture = new Gesture(GestureKind.Shape, point, null, [point]);
                if (_tool == ToolKind.Crop)
                {
                    _cropRect = null;
                    RaisePropertyChanged(nameof(CropRect));
                    RaisePropertyChanged(nameof(HintText));
                }

                break;
            case ToolKind.Text:
                _textOrigin = point;
                _textDraftText = string.Empty;
                RaiseTextDraftChanged();
                break;
            case ToolKind.StepNumber:
                Session.Add(new StepAnnotation(Guid.NewGuid(), _color, _thickness, point, Session.NextStepNumber, _fontSize));
                NotifySessionChanged();
                break;
        }
    }

    /// <summary>
    /// The second press of a double-click (the window says so by the click count). With the Select tool on a committed text it opens the text
    /// box on that text, at its place, to edit it again (SPEC editor "Chữ"); anywhere else it is an ordinary press, so a fast second click of
    /// any other tool still draws and a double-click on another kind of shape selects it and does nothing more.
    /// </summary>
    public void PointerDoubleClicked(PixelPoint point, bool shiftHeld)
    {
        if (_tool != ToolKind.Select || IsEditingText)
        {
            PointerDown(point, shiftHeld);
            return;
        }

        CommitFontSize();
        var hit = HitAt(point);
        if (hit is { } id && Annotations.FirstOrDefault(a => a.Id == id) is TextAnnotation text)
        {
            BeginEditText(text);
            return;
        }

        SelectAt(point, hit);
    }

    // What is under the point: the use case decides (IEditorInteractor.HitTest), the window only says how far a click may miss, in image pixels.
    private Guid? HitAt(PixelPoint point)
    {
        var tolerance = Math.Max(1, (int)Math.Ceiling(HitToleranceInViewUnits / Math.Max(ViewScale, 0.01)));
        return _services.Interactor.HitTest(Session, point, tolerance);
    }

    private void SelectAt(PixelPoint point, Guid? hit)
    {
        var target = hit is { } id ? Annotations.FirstOrDefault(a => a.Id == id) : null;
        Session.Select(target?.Id);
        _gesture = target is null ? null : new Gesture(GestureKind.Move, point, target, [point]);
        NotifySessionChanged();
    }

    // Opens the one text box on a committed text: its text, its place, its colour and size; the text itself is left out of the picture while
    // the box shows it, and nothing changes in the history until the box is committed.
    private void BeginEditText(TextAnnotation text)
    {
        Session.Select(text.Id);
        _gesture = null;
        _draft = null;
        _draftReplaces = text.Id;
        _editingTextId = text.Id;
        _textOrigin = text.Origin;
        _textDraftText = text.Text;
        RaiseTextDraftChanged();
        NotifySessionChanged();
    }

    public void PointerMoved(PixelPoint point, bool shiftHeld)
    {
        if (_gesture is not { } gesture)
        {
            return;
        }

        switch (gesture.Kind)
        {
            case GestureKind.Stroke:
                gesture.Points.Add(point);
                _draft = new StrokeAnnotation(Guid.NewGuid(), _color, _thickness, gesture.Points.ToList(), _tool == ToolKind.Highlighter);
                break;
            case GestureKind.Shape when _tool == ToolKind.Crop:
                _cropRect = AnnotationGeometry.Normalize(gesture.Start, point);
                RaisePropertyChanged(nameof(CropRect));
                RaisePropertyChanged(nameof(HintText));
                break;
            case GestureKind.Shape:
                _draft = BuildShape(_services.Interactor.ConstrainDrag(_tool, gesture.Start, point, shiftHeld));
                break;
            case GestureKind.Move when gesture.Original is { } original:
                _draft = AnnotationGeometry.Translate(original, point.X - gesture.Start.X, point.Y - gesture.Start.Y);
                _draftReplaces = original.Id;
                break;
        }

        Invalidate();
    }

    public void PointerUp(PixelPoint point, bool shiftHeld)
    {
        if (_gesture is not { } gesture)
        {
            return;
        }

        _gesture = null;
        _draft = null;
        _draftReplaces = null;
        switch (gesture.Kind)
        {
            case GestureKind.Stroke:
                if (gesture.Points[^1] != point)
                {
                    gesture.Points.Add(point);
                }

                Session.Add(_services.Interactor.CreateStroke(gesture.Points, _color, _thickness, _tool == ToolKind.Highlighter));
                break;
            case GestureKind.Shape when _tool == ToolKind.Crop:
                _cropRect = AnnotationGeometry.Normalize(gesture.Start, point);
                RaisePropertyChanged(nameof(CropRect));
                RaisePropertyChanged(nameof(HintText));
                break;
            case GestureKind.Shape:
                if (BuildShape(_services.Interactor.ConstrainDrag(_tool, gesture.Start, point, shiftHeld)) is { } shape)
                {
                    Session.Add(shape);
                }

                break;
            case GestureKind.Move:
                var (dx, dy) = (point.X - gesture.Start.X, point.Y - gesture.Start.Y);
                if (gesture.Original is { } original)
                {
                    // One drag is one history step, however many times the mouse moved; a drag back to where it began is none (the session decides).
                    Session.Move(original.Id, dx, dy);
                }

                break;
        }

        NotifySessionChanged();
    }

    /// <summary>The mouse capture was lost (Alt+Tab, a dialog): drop the drag without adding anything.</summary>
    public void PointerCancelled()
    {
        if (_gesture is null)
        {
            return;
        }

        _gesture = null;
        _draft = null;
        _draftReplaces = null;
        Invalidate();
    }

    // ---- text, crop, keys ----

    /// <summary>
    /// Commits the text being typed. A new text goes through the use case (an empty one creates nothing and says nothing, SPEC editor F4); a text
    /// that was reopened goes through <see cref="IEditorSession.SetText"/> (one history step; an emptied box deletes it; a text left as it was
    /// is no step at all).
    /// </summary>
    public void CommitText()
    {
        if (_textOrigin is not { } origin)
        {
            return;
        }

        var text = _textDraftText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var edited = EditedText;
        var editedId = _editingTextId;
        _textOrigin = null;
        _textDraftText = string.Empty;
        _editingTextId = null;
        if (editedId is not null)
        {
            _draftReplaces = null;
        }

        RaiseTextDraftChanged();
        if (editedId is { } id)
        {
            _services.Interactor.EditText(Session, id, text);
        }
        else
        {
            _services.Interactor.AddText(Session, origin, text, _color, _fontSize);
        }

        NotifySessionChanged();
    }

    /// <summary>
    /// Applies the size typed in the size box to the selected text or step marker: one <see cref="IEditorSession.SetFontSize"/> however many
    /// keys it took, and none when nothing was typed or the size is the one it has. The window calls it on Enter and when the box loses the
    /// keyboard; it also puts the box back to the real size when what was typed was not a size. With nothing selected there is nothing to apply.
    /// </summary>
    public void CommitFontSize()
    {
        if (_pendingFontSize is { } pending)
        {
            _pendingFontSize = null;
            if (SelectedSizedAnnotation is { } selected && selected.Size != pending)
            {
                Session.SetFontSize(selected.Id, pending);
                NotifySessionChanged();
                return;
            }
        }

        SyncFontSizeText();
    }

    /// <summary>The slider's thumb was pressed: a selected shape is only previewed from now until <see cref="EndThicknessChange"/>.</summary>
    public void BeginThicknessChange() => _thicknessDragging = SelectedAnnotation is not null;

    /// <summary>The thumb was released: the shape takes the thickness the drag ended on, in ONE history step (none if it ended where it began).</summary>
    public void EndThicknessChange()
    {
        if (!_thicknessDragging)
        {
            return;
        }

        var value = _thicknessDragValue;
        _thicknessDragging = false;
        _thicknessDragValue = null;
        _draft = null;
        _draftReplaces = null;
        if (value is { } thickness && SelectedAnnotation is { } selected && selected.Thickness != thickness)
        {
            Session.SetThickness(selected.Id, thickness);
        }

        NotifySessionChanged();
    }

    // Everything typed or half-done that a new action must not leave behind: the text in the box, the size in the size box.
    private void CommitPending()
    {
        CommitFontSize();
        CommitText();
    }

    /// <summary>Enter: cuts the chosen region. A region the session refuses shows its message and changes nothing (SPEC editor F5).</summary>
    internal void Confirm()
    {
        if (_tool != ToolKind.Crop || _cropRect is not { } region)
        {
            return;
        }

        var outcome = Session.Crop(region);
        _cropRect = null;
        RaisePropertyChanged(nameof(CropRect));
        if (!outcome.Applied)
        {
            _services.Notifications.ShowToast(outcome.Message ?? NotificationMessage.Of("Editor.CropInvalid"));
        }
        else if (_fit)
        {
            RefitIfNeeded();
        }

        NotifySessionChanged();
    }

    /// <summary>Esc: finishes the text (SPEC: Esc chốt chữ), else drops the crop region, else drops the selection.</summary>
    internal void Cancel()
    {
        if (_pendingFontSize is not null)
        {
            // Esc in the size box takes back what was typed there.
            _pendingFontSize = null;
            SyncFontSizeText();
        }
        else if (IsEditingText)
        {
            CommitText();
        }
        else if (_cropRect is not null)
        {
            _cropRect = null;
            RaisePropertyChanged(nameof(CropRect));
            NotifySessionChanged();
        }
        else if (Session.SelectedId is not null)
        {
            Session.Select(null);
            NotifySessionChanged();
        }
    }

    internal void Undo()
    {
        CommitPending();
        Session.Undo();
        RefitIfNeeded();
        NotifySessionChanged();
    }

    internal void Redo()
    {
        CommitPending();
        Session.Redo();
        RefitIfNeeded();
        NotifySessionChanged();
    }

    internal void DeleteSelection()
    {
        CommitFontSize();
        if (Session.SelectedId is { } id)
        {
            Session.Delete(id);
            NotifySessionChanged();
        }
    }

    // ---- save, copy, close, open ----

    /// <summary>Ctrl+S: what the use case decides, then that. Returns whether the image was written.</summary>
    internal bool Save() => RunSave(forceSaveAs: false);

    /// <summary>Lưu thành…: the box, always.</summary>
    internal bool SaveAs() => RunSave(forceSaveAs: true);

    /// <summary>Ctrl+C: the flattened image (its own size, whatever the zoom) to the clipboard.</summary>
    internal void Copy()
    {
        CommitPending();
        var result = _services.Interactor.Copy(_services.Flattener.Flatten(Session));
        if (result.Saved)
        {
            if (result.Message is not null)
            {
                _services.Notifications.ShowToast(result.Message);
            }
        }
        else
        {
            _services.Notifications.ShowError(result.Message ?? NotificationMessage.Of("Editor.CopyFailed", "unknown reason"));
        }
    }

    /// <summary>Ctrl+V: another image goes to a new window; this one is not touched (SPEC editor F3 when the clipboard has none).</summary>
    internal void Paste() => _flow.OpenFromClipboard();

    /// <summary>A file dropped on the window: a new window (SPEC editor F2 and F8 when it cannot be opened).</summary>
    internal void OpenFile(string path) => _flow.OpenFile(path);

    /// <summary>
    /// Asked when the window is about to close (its ✕, Alt+F4, the close command). True lets it close. With nothing unsaved it does; otherwise
    /// the user is asked, and Save closes only if the save worked, so a failed save never loses the edits (SPEC editor F1, F7).
    /// </summary>
    public bool ConfirmClose()
    {
        CommitPending();
        if (_services.Interactor.DecideClose(Session) == CloseAction.Close)
        {
            return true;
        }

        return _services.Prompts.AskSaveDiscardCancel() switch
        {
            CloseChoice.Save => RunSave(forceSaveAs: false),
            CloseChoice.Discard => true,
            _ => false,
        };
    }

    public void Dispose() => _services.Localizer.LanguageChanged -= OnLanguageChanged;

    private bool RunSave(bool forceSaveAs)
    {
        CommitPending();
        var decision = _services.Interactor.DecideSave(Session, _services.Settings());
        if (forceSaveAs)
        {
            return SaveThroughBox(decision);
        }

        switch (decision.Action)
        {
            case SaveAction.AskSaveAs:
                return SaveThroughBox(decision);
            case SaveAction.AskOverwriteOrCopy:
                var original = decision.Path ?? Session.SourcePath ?? string.Empty;
                return _services.Prompts.AskOverwriteOrCopy(original) switch
                {
                    OverwriteChoice.Overwrite => WriteTo(original),
                    OverwriteChoice.SaveCopy => SaveThroughBox(decision),
                    _ => false,
                };
            case SaveAction.FileChangedOnDisk:
                // Never write over a file another program changed: say so, and offer Lưu thành… (SPEC editor F9).
                _services.Notifications.ShowError(NotificationMessage.Of("Editor.FileChangedOnDisk", decision.Path ?? Session.SourcePath ?? string.Empty));
                return SaveThroughBox(decision);
            default:
                return WriteTo(decision.Path ?? Session.SourcePath ?? string.Empty);
        }
    }

    private bool SaveThroughBox(SaveDecision decision)
    {
        var path = decision.Path;
        var folder = decision.Folder.Length > 0 ? decision.Folder : (path is null ? string.Empty : System.IO.Path.GetDirectoryName(path) ?? string.Empty);
        var name = decision.FileName.Length > 0 ? decision.FileName : (path is null ? string.Empty : System.IO.Path.GetFileName(path));
        var chosen = _services.FileDialogs.PickSavePath(folder, name);
        return chosen is not null && WriteTo(chosen);
    }

    // Writes the flattened image through the use case. A failure shows its message, keeps every edit and leaves the window open (SPEC editor F1).
    private bool WriteTo(string path)
    {
        var flattened = _services.Flattener.Flatten(Session);
        var result = _services.Interactor.Save(Session, flattened, path, _services.Settings());
        if (result.Saved)
        {
            // The use case marks the session saved; if it did not, the window still must not call the image unsaved.
            if (Session.IsDirty)
            {
                Session.MarkSaved(result.Path ?? path);
            }

            if (result.Message is not null)
            {
                _services.Notifications.ShowToast(result.Message);
            }

            NotifySessionChanged();
            return true;
        }

        _services.Notifications.ShowError(result.Message ?? NotificationMessage.Of("Editor.SaveFailed", path, "unknown reason"));
        NotifySessionChanged();
        return false;
    }

    // ---- bookkeeping ----

    // Everything the window shows that depends on the session: called after every change to it.
    private void NotifySessionChanged()
    {
        RaisePropertyChanged(nameof(Annotations));
        RaisePropertyChanged(nameof(SelectedId));
        RaisePropertyChanged(nameof(SizeText));
        RaisePropertyChanged(nameof(SaveStateText));
        RaisePropertyChanged(nameof(ShownColor));
        RaisePropertyChanged(nameof(Thickness));
        RaisePropertyChanged(nameof(HintText));
        RaisePropertyChanged(nameof(CropRect));
        SyncFontSizeText();
        foreach (var swatch in Palette)
        {
            swatch.Refresh();
        }

        _undoCommand.RaiseCanExecuteChanged();
        _redoCommand.RaiseCanExecuteChanged();
        _deleteCommand.RaiseCanExecuteChanged();
        Invalidate();
    }

    private void RaiseTextDraftChanged()
    {
        RaisePropertyChanged(nameof(IsEditingText));
        RaisePropertyChanged(nameof(TextDraftOrigin));
        RaisePropertyChanged(nameof(TextDraftText));
        RaisePropertyChanged(nameof(TextDraftColor));
        RaisePropertyChanged(nameof(TextDraftFontSize));
        RaisePropertyChanged(nameof(HintText));
        _deleteCommand.RaiseCanExecuteChanged();
        Invalidate();
    }

    // The size box shows the selected text's or marker's size, else the next size; not while a typed size is still waiting to be applied.
    private void SyncFontSizeText()
    {
        if (_pendingFontSize is not null)
        {
            return;
        }

        var shown = FontSize.ToString(CultureInfo.InvariantCulture);
        if (_fontSizeText != shown)
        {
            _fontSizeText = shown;
            RaisePropertyChanged(nameof(FontSizeText));
        }

        RaisePropertyChanged(nameof(FontSize));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RaisePropertyChanged(nameof(SaveStateText));
        RaisePropertyChanged(nameof(HintText));
        foreach (var swatch in Palette)
        {
            swatch.Refresh();
        }
    }

    private void Invalidate() => CanvasInvalidated?.Invoke(this, EventArgs.Empty);

    // The shape a drag makes from the two ends the use case answered; the use case says which drags draw nothing.
    private Annotation? BuildShape(DragShape shape) => _services.Interactor.CreateShape(_tool, shape, _color, _thickness);

    private enum GestureKind
    {
        Stroke,
        Shape,
        Move,
    }

    // The drag in progress: what kind, where it began, the shape it moves (for Move) and the points it has collected so far.
    private sealed record Gesture(GestureKind Kind, PixelPoint Start, Annotation? Original, List<PixelPoint> Points);
}
