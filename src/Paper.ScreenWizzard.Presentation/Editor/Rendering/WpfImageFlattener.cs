using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.UseCases.Editor.Ports;

namespace Paper.ScreenWizzard.Presentation.Editor.Rendering;

/// <summary>
/// Builds the one bitmap that is saved or copied: <c>session.RenderBase()</c> (the image with its mosaic already applied by the use case)
/// with every other annotation drawn over it, one bitmap pixel per image pixel. It renders into a <see cref="RenderTargetBitmap"/> of the
/// image's own size at 96 dpi and never looks at the window, so the zoom the user is at cannot change the result (SPEC editor, "Lưu và chép").
/// Annotations that leave the image are clipped by the bitmap's edge.
/// </summary>
public sealed class WpfImageFlattener : IEditorFlattener
{
    public PixelImage Flatten(IEditorSession session)
    {
        var baseImage = session.RenderBase();
        var buffer = (byte[])baseImage.Bgra.Clone();
        var width = baseImage.Width;
        var height = baseImage.Height;

        // Everything but a highlighter is composited by WPF in one pass per run of such annotations; a highlighter is a multiply and WPF has
        // no multiply, so it is applied to the pixels between the runs. The order of the list is the order of the layers.
        var run = new List<Annotation>();
        foreach (var annotation in session.Document.Annotations)
        {
            switch (annotation)
            {
                case BlurAnnotation:
                    continue;
                case StrokeAnnotation { IsHighlighter: true } highlighter:
                    Flush(buffer, width, height, run);
                    ApplyHighlighter(buffer, width, height, highlighter);
                    break;
                default:
                    run.Add(annotation);
                    break;
            }
        }

        Flush(buffer, width, height, run);
        return new PixelImage(width, height, buffer);
    }

    // Draws the pending run over the current pixels and reads the result back (WPF hands out premultiplied alpha, the image is straight).
    private static void Flush(byte[] buffer, int width, int height, List<Annotation> run)
    {
        if (run.Count == 0)
        {
            return;
        }

        var current = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, buffer, width * 4);
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(current, new Rect(0, 0, width, height));
            foreach (var annotation in run)
            {
                AnnotationRenderer.Draw(context, annotation);
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var premultiplied = new byte[buffer.Length];
        target.CopyPixels(premultiplied, width * 4, 0);
        Unpremultiply(premultiplied, buffer);
        run.Clear();
    }

    private static void Unpremultiply(byte[] premultiplied, byte[] straight)
    {
        for (var i = 0; i + 3 < premultiplied.Length; i += 4)
        {
            var alpha = premultiplied[i + 3];
            if (alpha == 255)
            {
                straight[i] = premultiplied[i];
                straight[i + 1] = premultiplied[i + 1];
                straight[i + 2] = premultiplied[i + 2];
            }
            else if (alpha == 0)
            {
                straight[i] = 0;
                straight[i + 1] = 0;
                straight[i + 2] = 0;
            }
            else
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    straight[i + channel] = (byte)Math.Min(255, ((premultiplied[i + channel] * 255) + (alpha / 2)) / alpha);
                }
            }

            straight[i + 3] = alpha;
        }
    }

    // A highlighter multiplies the paper by the highlight colour, softened to 40%: white paper turns 40% of the way to the colour, and a
    // black letter stays black (0 times anything is 0), which is what a real highlighter does and a plain 40% alpha blend does not (SPEC
    // editor: điểm ảnh của chữ vẫn đen sau khi ghép, nền quanh chữ ngả vàng). The stroke's shape is rendered once into a mask.
    private static void ApplyHighlighter(byte[] buffer, int width, int height, StrokeAnnotation stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        var reach = (int)Math.Ceiling(AnnotationRenderer.StrokeWidthOf(stroke) / 2.0) + 2;
        var left = Math.Max(0, stroke.Points.Min(p => p.X) - reach);
        var top = Math.Max(0, stroke.Points.Min(p => p.Y) - reach);
        var right = Math.Min(width, stroke.Points.Max(p => p.X) + reach + 1);
        var bottom = Math.Min(height, stroke.Points.Max(p => p.Y) + reach + 1);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var maskWidth = right - left;
        var maskHeight = bottom - top;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(-left, -top));
            AnnotationRenderer.DrawHighlighterMask(context, stroke);
            context.Pop();
        }

        var target = new RenderTargetBitmap(maskWidth, maskHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var mask = new byte[maskWidth * maskHeight * 4];
        target.CopyPixels(mask, maskWidth * 4, 0);

        var strength = AnnotationRenderer.HighlighterStrength * (stroke.Color.A / 255.0);
        var colour = new[] { stroke.Color.B, stroke.Color.G, stroke.Color.R };
        for (var y = 0; y < maskHeight; y++)
        {
            for (var x = 0; x < maskWidth; x++)
            {
                var coverage = mask[(((y * maskWidth) + x) * 4) + 3] / 255.0 * strength;
                if (coverage <= 0)
                {
                    continue;
                }

                var at = ((((top + y) * width) + left + x) * 4);
                for (var channel = 0; channel < 3; channel++)
                {
                    var factor = 1.0 - (coverage * (1.0 - (colour[channel] / 255.0)));
                    buffer[at + channel] = (byte)Math.Clamp(Math.Round(buffer[at + channel] * factor), 0, 255);
                }
            }
        }
    }
}
