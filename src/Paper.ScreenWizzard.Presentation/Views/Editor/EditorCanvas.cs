using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.Presentation.Views.Capture;

namespace Paper.ScreenWizzard.Presentation.Views.Editor;

/// <summary>
/// The picture area: the image (with its mosaic) and the drawings over it, drawn at the view model's zoom, plus what is in progress (the
/// shape being dragged, the selection frame, the crop region). It draws only what the view model says and holds no state of its own beyond
/// a cached bitmap. The image is <c>ViewScale</c> display units per image pixel; the drawings are drawn in image pixels under a scale
/// transform, so a shape is exactly where the session says it is at any zoom. The mouse is handled by the window, which converts with
/// <see cref="EditorCoordinates"/>.
/// </summary>
public sealed class EditorCanvas : FrameworkElement
{
    private EditorViewModel? _viewModel;
    private BitmapSource? _bitmap;
    private PixelImage? _bitmapFor;

    public EditorCanvas()
    {
        // The canvas is a stop of Tab and takes keyboard focus when clicked, so Ctrl+Z, Delete and Enter work after drawing; its own focus
        // ring is drawn in OnRender because WPF's default one is a dotted line that disappears on a themed background.
        Focusable = true;
        FocusVisualStyle = null;
        SnapsToDevicePixels = false;
        IsKeyboardFocusedChanged += (_, _) => InvalidateVisual();
    }

    public EditorViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
            {
                _viewModel.CanvasInvalidated -= OnCanvasInvalidated;
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _viewModel = value;
            if (_viewModel is not null)
            {
                _viewModel.CanvasInvalidated += OnCanvasInvalidated;
                _viewModel.PropertyChanged += OnViewModelChanged;
            }

            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_viewModel is null)
        {
            return default;
        }

        var source = _viewModel.Session.Document.Source;
        var scale = _viewModel.ViewScale;
        return new Size(source.Width * scale, source.Height * scale);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorCanvasAutomationPeer(this);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_viewModel is null)
        {
            return;
        }

        var viewModel = _viewModel;
        var scale = viewModel.ViewScale;
        var source = viewModel.Session.Document.Source;
        var full = new Rect(0, 0, source.Width * scale, source.Height * scale);

        // Enlarged pixels stay square (SPEC: phóng to để vẽ cho chính xác); shrunk ones are averaged so text stays readable.
        RenderOptions.SetBitmapScalingMode(this, scale >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        drawingContext.DrawImage(BitmapOf(viewModel.BasePixels), full);

        drawingContext.PushClip(new RectangleGeometry(full));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        foreach (var annotation in viewModel.Annotations)
        {
            if (annotation.Id != viewModel.DraftReplaces)
            {
                AnnotationRenderer.Draw(drawingContext, annotation);
            }
        }

        if (viewModel.Draft is { } draft)
        {
            AnnotationRenderer.Draw(drawingContext, draft);
            if (draft is BlurAnnotation blur)
            {
                DrawFrame(drawingContext, new Rect(blur.Area.X, blur.Area.Y, blur.Area.Width, blur.Area.Height), scale);
            }
        }

        DrawSelection(drawingContext, viewModel, scale);
        if (viewModel.CropRect is { } crop)
        {
            DrawCrop(drawingContext, crop, source, scale);
        }

        drawingContext.Pop();
        drawingContext.Pop();

        if (IsKeyboardFocused)
        {
            // The focus ring: two rings inside the canvas, the focus colour and a window-coloured line inside it, as on the buttons.
            var ring = ThemeBrush("Brush.Focus");
            var gap = ThemeBrush("Brush.Window");
            drawingContext.DrawRectangle(null, new Pen(ring, 2), new Rect(1, 1, Math.Max(0, full.Width - 2), Math.Max(0, full.Height - 2)));
            drawingContext.DrawRectangle(null, new Pen(gap, 2), new Rect(3, 3, Math.Max(0, full.Width - 6), Math.Max(0, full.Height - 6)));
        }
    }

    // The frame of the selected shape: a dashed line one display unit thick whatever the zoom, in the focus colour with a window-coloured
    // line under it so it shows on both dark and light parts of the picture.
    private void DrawSelection(DrawingContext context, EditorViewModel viewModel, double scale)
    {
        if (viewModel.SelectedId is not { } id || viewModel.IsEditingText)
        {
            // While a text is typed the box is its own frame.
            return;
        }

        var selected = viewModel.Draft is { } draft && draft.Id == id ? draft : viewModel.Annotations.FirstOrDefault(a => a.Id == id);
        if (selected is null)
        {
            return;
        }

        if (viewModel.DraftReplaces == id && viewModel.Draft is { } moved)
        {
            selected = moved;
        }

        var bounds = AnnotationGeometry.BoundsOf(selected);
        bounds.Inflate(3 / scale, 3 / scale);
        DrawFrame(context, bounds, scale);
    }

    private void DrawFrame(DrawingContext context, Rect bounds, double scale)
    {
        var thin = 1.5 / Math.Max(scale, 0.001);
        context.DrawRectangle(null, new Pen(ThemeBrush("Brush.Window"), thin * 2), bounds);
        context.DrawRectangle(null, new Pen(ThemeBrush("Brush.Focus"), thin) { DashStyle = new DashStyle([4, 3], 0) }, bounds);
    }

    // Everything outside the chosen crop region is dimmed and the region is framed with its size beside it, so what stays is plain.
    private void DrawCrop(DrawingContext context, PixelRect crop, PixelImage source, double scale)
    {
        var image = new Rect(0, 0, source.Width, source.Height);
        var region = Rect.Intersect(new Rect(crop.X, crop.Y, crop.Width, crop.Height), image);
        var dim = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(image), new RectangleGeometry(region.IsEmpty ? Rect.Empty : region));
        var dimBrush = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
        context.DrawGeometry(dimBrush, null, dim);
        if (!region.IsEmpty)
        {
            DrawFrame(context, region, scale);
            DrawCropSize(context, region, scale);
        }
    }

    // "width x height" of what the crop would keep, on a small label in the toast colours under the region (above it near the bottom edge).
    private void DrawCropSize(DrawingContext context, Rect region, double scale)
    {
        var text = AnnotationRenderer.MakeText($"{(int)region.Width} × {(int)region.Height}", 13.0 / Math.Max(scale, 0.001), ThemeBrush("Brush.ToastText"), bold: true);
        var pad = 4.0 / Math.Max(scale, 0.001);
        var box = new Rect(region.X, region.Bottom + pad, text.Width + (pad * 2), text.Height + (pad * 2));
        if (box.Bottom > _viewModel!.Session.Document.Source.Height)
        {
            box.Y = Math.Max(0, region.Y - box.Height - pad);
        }

        context.DrawRoundedRectangle(ThemeBrush("Brush.ToastBackground"), null, box, pad, pad);
        context.DrawText(text, new Point(box.X + pad, box.Y + pad));
    }

    private BitmapSource BitmapOf(PixelImage image)
    {
        if (_bitmap is null || !ReferenceEquals(_bitmapFor, image))
        {
            _bitmap = PixelImageBitmap.ToBitmapSource(image);
            _bitmapFor = image;
        }

        return _bitmap;
    }

    private Brush ThemeBrush(string key) => TryFindResource(key) as Brush ?? Brushes.Magenta;

    private void OnCanvasInvalidated(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorViewModel.ViewScale) or nameof(EditorViewModel.Zoom))
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The canvas as a UI Automation element, so a screen reader finds it and a test can click on it by its AutomationId.</summary>
    private sealed class EditorCanvasAutomationPeer : FrameworkElementAutomationPeer
    {
        public EditorCanvasAutomationPeer(EditorCanvas owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;
    }
}
