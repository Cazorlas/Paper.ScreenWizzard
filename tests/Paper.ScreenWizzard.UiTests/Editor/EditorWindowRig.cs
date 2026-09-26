using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core.Input;
using Paper.ScreenWizzard.Domain.Editor;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Rendering;
using Paper.ScreenWizzard.Presentation.ViewModels.Editor;
using Paper.ScreenWizzard.Presentation.Views.Editor;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Shell.Models;

namespace Paper.ScreenWizzard.UiTests.ImageEditor;

/// <summary>
/// An editor window on the fakes of <see cref="EditorRig"/>, shown by the shared WPF host, with FlaUI attached. Everything that touches the
/// view model goes through <see cref="Ui(Action)"/>, because the window's bindings belong to the UI thread.
/// </summary>
public sealed class EditorWindowRig : IDisposable
{
    private EditorWindowRig(EditorRig rig, WindowSession window)
    {
        Rig = rig;
        Window = window;
    }

    public EditorRig Rig { get; }

    public WindowSession Window { get; }

    public EditorViewModel ViewModel => Rig.ViewModel;

    public static EditorWindowRig Open(
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light,
        PixelImage? image = null,
        string? sourcePath = null,
        bool realPrompts = false)
    {
        EditorRig? rig = null;
        var window = WpfHost.Instance.Show(
            () =>
            {
                rig = EditorRig.Create(
                    image,
                    sourcePath,
                    WpfHost.Instance.Language,
                    prompts: realPrompts ? new WpfEditorPrompts(WpfHost.Instance.Language) : null);
                // Always the same place on the primary monitor: CenterScreen puts a 1120-unit window where the mouse last was, and on a second
                // monitor of another scale part of it can end up beyond the screen, out of reach of a real click.
                return new EditorWindow(rig.ViewModel)
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Left = 20,
                    Top = 20,
                };
            },
            language,
            theme);
        return new EditorWindowRig(rig!, window);
    }

    public void Ui(Action action)
    {
        Window.Host.Invoke(action);
        Window.Host.Settle();
    }

    public T Ui<T>(Func<T> function)
    {
        var result = Window.Host.Invoke(function);
        Window.Host.Settle();
        return result;
    }

    public string ZoomText => Window.TextOf("ZoomText");

    /// <summary>The screen pixel at the middle of an image pixel (rounded down: a screen pixel is whole), read from the canvas's own rectangle so it needs no knowledge of DPI or zoom.</summary>
    public System.Drawing.Point ScreenPointOf(int imageX, int imageY)
    {
        var rect = Window.Require("ImageCanvas").BoundingRectangle;
        var image = Rig.Session.Document.Source;
        var perPixelX = rect.Width / (double)image.Width;
        var perPixelY = rect.Height / (double)image.Height;
        return new System.Drawing.Point(
            (int)Math.Floor(rect.Left + ((imageX + 0.5) * perPixelX)),
            (int)Math.Floor(rect.Top + ((imageY + 0.5) * perPixelY)));
    }

    /// <summary>A real mouse drag between two image pixels: press, move, release.</summary>
    public void MouseDrag(PixelPoint from, PixelPoint to)
    {
        Mouse.MoveTo(ScreenPointOf(from.X, from.Y));
        Mouse.Down(MouseButton.Left);
        var middle = new System.Drawing.Point(
            (ScreenPointOf(from.X, from.Y).X + ScreenPointOf(to.X, to.Y).X) / 2,
            (ScreenPointOf(from.X, from.Y).Y + ScreenPointOf(to.X, to.Y).Y) / 2);
        Mouse.MoveTo(middle);
        Mouse.MoveTo(ScreenPointOf(to.X, to.Y));
        Window.Host.Settle();
        Mouse.Up(MouseButton.Left);
        Window.Host.Settle();
    }

    public void MouseClick(PixelPoint at)
    {
        Mouse.MoveTo(ScreenPointOf(at.X, at.Y));
        Mouse.Click(MouseButton.Left);
        Window.Host.Settle();
    }

    /// <summary>Two quick presses of the left button on an image pixel: what the user does to edit a text.</summary>
    public void MouseDoubleClick(PixelPoint at)
    {
        Mouse.MoveTo(ScreenPointOf(at.X, at.Y));
        Mouse.DoubleClick(MouseButton.Left);
        Window.Host.Settle();
    }

    /// <summary>
    /// Drags the thumb of the thickness slider <paramref name="pixels"/> to the right (screen pixels) with the real mouse, in small steps so the
    /// slider passes through every notch on the way.
    /// </summary>
    public void DragThicknessThumb(int pixels)
    {
        var thumb = Window.Require("ThicknessSlider").FindFirstDescendant(conditions => conditions.ByControlType(FlaUI.Core.Definitions.ControlType.Thumb));
        NUnit.Framework.Assert.That(thumb, NUnit.Framework.Is.Not.Null, "the slider's thumb is not in the UI Automation tree");
        var rectangle = thumb!.BoundingRectangle;
        var start = new System.Drawing.Point(rectangle.Left + (rectangle.Width / 2), rectangle.Top + (rectangle.Height / 2));
        Mouse.MoveTo(start);
        Mouse.Down(MouseButton.Left);
        for (var moved = 4; moved <= pixels; moved += 4)
        {
            Mouse.MoveTo(new System.Drawing.Point(start.X + moved, start.Y));
        }

        Window.Host.Settle();
        Mouse.Up(MouseButton.Left);
        Window.Host.Settle();
    }

    public void ChooseTool(ToolKind tool) => Window.Click("ToolButton." + tool);

    /// <summary>Asks the window to close from the UI thread and returns at once, so a question the close raises never blocks the test thread.</summary>
    public void RequestClose()
    {
        Window.Host.Dispatcher.BeginInvoke(new Action(() => Window.Window.Close()));
        Window.Host.Settle();
    }

    /// <summary>
    /// Leaves nothing behind: a question still on the screen is closed and the edits are marked saved, so the harness closing the window in
    /// its TearDown never meets a modal prompt nobody answers (a failed assertion would otherwise hang the whole run).
    /// </summary>
    public void Dispose()
    {
        Window.Host.Invoke(() =>
        {
            foreach (var open in Application.Current.Windows.OfType<Window>().Where(w => System.Windows.Automation.AutomationProperties.GetAutomationId(w) == "EditorPromptDialog").ToList())
            {
                open.Close();
            }

            Rig.Session.MarkSaved(string.Empty);
        });
        Window.Dispose();
    }

    /// <summary>A screenshot-like image with text, panels and a table, so arrows, highlights and step numbers have something to point at.</summary>
    public static PixelImage MockScreenshot(int width = 760, int height = 440) => WpfHost.Instance.Invoke(() =>
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            void Text(string text, double x, double y, double size, Color color, bool bold = false)
            {
                var formatted = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                    size,
                    new SolidColorBrush(color),
                    1.0);
                dc.DrawText(formatted, new Point(x, y));
            }

            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(243, 243, 243)), null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32, 88, 160)), null, new Rect(0, 0, width, 34));
            Text("Quản lý đường ống — Dự án Nhà máy A", 12, 6, 15, Colors.White, bold: true);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(228, 232, 238)), null, new Rect(0, 34, 150, height - 34));
            foreach (var (label, index) in new[] { "Tổng quan", "Đường ống", "Van & phụ kiện", "Báo cáo", "Cài đặt" }.Select((l, i) => (l, i)))
            {
                Text(label, 16, 52 + (index * 30), 14, index == 1 ? Color.FromRgb(32, 88, 160) : Color.FromRgb(50, 50, 50), bold: index == 1);
            }

            Text("Danh sách đường ống", 170, 50, 18, Color.FromRgb(30, 30, 30), bold: true);
            var headers = new[] { "Mã", "Tên tuyến", "Đường kính", "Áp suất" };
            var columns = new[] { 170.0, 250.0, 470.0, 590.0 };
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(210, 216, 226)), null, new Rect(170, 86, 560, 26));
            for (var i = 0; i < headers.Length; i++)
            {
                Text(headers[i], columns[i] + 6, 90, 13, Color.FromRgb(30, 30, 30), bold: true);
            }

            var rows = new[]
            {
                ("P-101", "Đường ống chính 45°", "DN200", "16 bar"),
                ("P-102", "Nhánh cấp nước khu B", "DN100", "10 bar"),
                ("P-103", "Ống thoát — thử nghiệm", "DN150", "6 bar"),
                ("P-104", "Cấp khí nén xưởng", "DN80", "8 bar"),
                ("P-105", "Hồi nước làm mát", "DN125", "10 bar"),
            };
            for (var r = 0; r < rows.Length; r++)
            {
                var top = 114 + (r * 30);
                dc.DrawRectangle(new SolidColorBrush(r % 2 == 0 ? Colors.White : Color.FromRgb(248, 249, 251)), null, new Rect(170, top, 560, 30));
                var cells = new[] { rows[r].Item1, rows[r].Item2, rows[r].Item3, rows[r].Item4 };
                for (var c = 0; c < cells.Length; c++)
                {
                    Text(cells[c], columns[c] + 6, top + 6, 13, Color.FromRgb(40, 40, 40));
                }
            }

            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(32, 88, 160)), null, new Rect(170, 300, 120, 34), 4, 4);
            Text("Thêm tuyến", 190, 307, 13, Colors.White, bold: true);
            Text("Mật khẩu quản trị: hunter2-Admin!", 170, 360, 14, Color.FromRgb(60, 60, 60));
            Text("Cập nhật lần cuối: 20/09/2026 14:03", 170, 392, 12, Color.FromRgb(110, 110, 110));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return new PixelImage(width, height, pixels);
    });

    /// <summary>
    /// The two colours that dominate what the element draws (its background and its ink, the pixel furthest from the background) and their
    /// WCAG ratio, measured on WPF's own render of the element so it does not depend on what is in front of the window.
    /// </summary>
    public double DominantContrast(string automationId) => Window.Host.Invoke(() =>
    {
        var element = VisualTreeFinder.FindByAutomationId(Window.Window, automationId)
            ?? throw new NUnit.Framework.AssertionException($"'{automationId}' is not in the window's visual tree");
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            // Under the element, the window's own background: a button with no fill is drawn over it in the real window.
            context.DrawRectangle(Window.Window.Background, null, new Rect(0, 0, width, height));
            context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, width, height));
        }

        bitmap.Render(drawing);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var counts = new Dictionary<(byte, byte, byte), int>();
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var key = (pixels[i + 2], pixels[i + 1], pixels[i]);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var background = counts.MaxBy(pair => pair.Value).Key;
        (byte, byte, byte) ink = background;
        var best = -1;
        foreach (var key in counts.Keys)
        {
            var distance = Math.Abs(key.Item1 - background.Item1) + Math.Abs(key.Item2 - background.Item2) + Math.Abs(key.Item3 - background.Item3);
            if (distance > best)
            {
                best = distance;
                ink = key;
            }
        }

        return Contrast.Ratio(Color.FromRgb(background.Item1, background.Item2, background.Item3), Color.FromRgb(ink.Item1, ink.Item2, ink.Item3));
    });
}
