using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.Views.Capture;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Color = System.Windows.Media.Color;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The selection overlay as a real window on a fake session (plan T9). The "screen" it covers is 800 x 600 at (100, 100): a
/// small virtual screen and a gradient image whose colour says where each pixel came from, so a pixel read from the rendered
/// window proves both which region is lit and that the image is drawn one pixel per physical pixel. The mouse is real
/// (FlaUI), so a drag proves the units the window hands the session too. Nothing here covers the real desktop.
/// </summary>
[TestFixture]
public sealed class SelectionOverlayTests : UiTestBase
{
    private static readonly PixelRect _screen = CaptureTestData.SmallScreen;

    private sealed record Rig(
        SelectionOverlayViewModel ViewModel,
        FakeCaptureSession Session,
        RecordingNotifications Notes,
        WindowSession Window,
        List<SelectionFinishedEventArgs> Finished);

    private static Rig Open(
        CaptureKind kind,
        ResolvedLanguage language = ResolvedLanguage.Vietnamese,
        AppTheme theme = AppTheme.Light,
        PixelPoint? cursor = null)
    {
        var session = new FakeCaptureSession(kind, CaptureTestData.Snapshot(cursor));
        var notes = new RecordingNotifications();
        var finished = new List<SelectionFinishedEventArgs>();
        SelectionOverlayViewModel? viewModel = null;
        var window = WpfHost.Instance.Show(
            () =>
            {
                viewModel = new SelectionOverlayViewModel(session, notes, WpfHost.Instance.Language);
                viewModel.Finished += (_, e) => finished.Add(e);
                return new SelectionOverlayWindow(viewModel);
            },
            language,
            theme);
        return new Rig(viewModel!, session, notes, window, finished);
    }

    private static System.Drawing.Point OnDesktop(int x, int y) => new(x, y);

    private static void Drag(Rig rig, PixelPoint from, PixelPoint to, bool release)
    {
        Mouse.MoveTo(OnDesktop(from.X, from.Y));
        Mouse.Down(MouseButton.Left);
        Mouse.MoveTo(OnDesktop(to.X, to.Y));
        rig.Window.Host.Settle();
        if (release)
        {
            Mouse.Up(MouseButton.Left);
            rig.Window.Host.Settle();
        }
    }

    // What the gradient image holds at an overlay-local pixel, and what the overlay should show: as is when lit, 60% when dimmed.
    private static Color Expected(int x, int y, bool lit)
    {
        var r = CaptureTestData.GradientR(x, _screen.Width);
        var g = CaptureTestData.GradientG(y, _screen.Height);
        const byte b = 128;
        return lit
            ? Color.FromRgb(r, g, b)
            : Color.FromRgb((byte)Math.Round(r * 0.6), (byte)Math.Round(g * 0.6), (byte)Math.Round(b * 0.6));
    }

    private static void AssertPixel(RenderedOverlay rendered, int x, int y, bool lit, string because)
    {
        var expected = Expected(x, y, lit);
        var actual = rendered.At(x, y);
        Assert.That(
            (Math.Abs(actual.R - expected.R) <= 3) && (Math.Abs(actual.G - expected.G) <= 3) && (Math.Abs(actual.B - expected.B) <= 3),
            Is.True,
            $"{because}: pixel ({x}, {y}) of the overlay is {actual}, expected about {expected} ({(lit ? "lit" : "dimmed to 60%")})");
    }

    [Test]
    public void Overlay_IsPlacedOnTheVirtualScreenInPhysicalPixels()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            var bounds = rig.Window.Root.BoundingRectangle;

            Assert.That(
                (bounds.Left, bounds.Top, bounds.Width, bounds.Height),
                Is.EqualTo((_screen.X, _screen.Y, _screen.Width, _screen.Height)),
                "the overlay covers the virtual screen exactly, one window pixel per desktop pixel");
        }
    }

    [Test]
    public void Overlay_StaysOnTopOffTheTaskbarAndHasAName()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            var (topmost, inTaskbar) = WpfHost.Instance.Invoke(() =>
            {
                var bare = new SelectionOverlayWindow(new SelectionOverlayViewModel(new FakeCaptureSession(CaptureKind.Rectangle), new RecordingNotifications(), new FakeLocalizer()));
                return (bare.Topmost, bare.ShowInTaskbar);
            });

            Assert.That(topmost, Is.True, "nothing may sit above the frozen picture");
            Assert.That(inTaskbar, Is.False);
            Assert.That(rig.Window.Root.AutomationId, Is.EqualTo("SelectionOverlay"));
            Assert.That(rig.Window.Root.Name, Is.Not.Empty.And.Not.Contain("Capture."));
        }
    }

    [Test]
    public void Overlay_ShowsTheFrozenImageOnePixelPerPixelDimmedBy40Percent()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            var rendered = RenderedOverlay.Take(rig.Window);

            // A gradient where colour = position: a picture scaled by even 1% would miss these values.
            AssertPixel(rendered, 0, 0, false, "top-left corner");
            AssertPixel(rendered, 400, 300, false, "centre");
            AssertPixel(rendered, 799, 599, false, "bottom-right corner");
            AssertPixel(rendered, 123, 456, false, "an odd place");
            AssertPixel(rendered, 700, 50, false, "top-right area");
        }
    }

    [Test]
    public void Overlay_HintNamesTheCancelKeys_InBothLanguages()
    {
        var vi = Open(CaptureKind.Rectangle, ResolvedLanguage.Vietnamese);
        using (vi.Window)
        {
            Assert.That(vi.Window.TextOf("CancelHint"), Does.Contain("Esc").And.Contain("huỷ"));
        }

        WpfHost.Instance.CloseAllWindows();
        var en = Open(CaptureKind.Rectangle, ResolvedLanguage.English);
        using (en.Window)
        {
            Assert.That(en.Window.TextOf("CancelHint"), Does.Contain("Esc").And.Contain("cancel").IgnoreCase);
        }
    }

    [Test]
    public void Rectangle_MidDrag_LightsTheRegionAndShowsItsWidthByHeight()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            Drag(rig, new PixelPoint(250, 250), new PixelPoint(550, 450), release: false);

            Assert.That(rig.Window.TextOf("SizeLabel"), Is.EqualTo("300 × 200"));
            var rendered = RenderedOverlay.Take(rig.Window);
            // Overlay-local: the region is (150, 150) to (450, 350).
            AssertPixel(rendered, 300, 250, true, "inside the dragged region");
            AssertPixel(rendered, 200, 200, true, "inside, near the corner");
            AssertPixel(rendered, 60, 60, false, "outside the region");
            AssertPixel(rendered, 600, 500, false, "outside the region, other side");
            Assert.That(rendered.CountNear(ThemeColor("Brush.Accent"), 30), Is.GreaterThan(400), "the region has a visible frame");
            rig.Window.Screenshot("capture-overlay-rectangle-drag");

            Mouse.Up(MouseButton.Left);
            rig.Window.Host.Settle();
        }
    }

    [Test]
    public void Rectangle_Release_HandsTheSessionThePhysicalPixelsAndTheOverlayCloses()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            Drag(rig, new PixelPoint(250, 250), new PixelPoint(550, 450), release: true);

            Assert.That(
                rig.Session.RectangleCompletions,
                Is.EqualTo(new[] { (new PixelPoint(250, 250), new PixelPoint(550, 450)) }),
                "desktop physical pixels, origin (100, 100) included");
            Assert.That(rig.Session.Calls, Is.EqualTo(new[] { "CheckDisplayUnchanged", "CompleteRectangle" }));
            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "a finished selection leaves nothing on the screen");
            Assert.That(rig.Finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Captured));
        }
    }

    [Test]
    public void Rectangle_DraggedBackwards_HandsTheSessionBothPointsAsTheyWereDragged()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            Drag(rig, new PixelPoint(550, 450), new PixelPoint(250, 250), release: true);

            Assert.That(rig.Session.RectangleCompletions.Only("rectangle completion"), Is.EqualTo((new PixelPoint(550, 450), new PixelPoint(250, 250))));
        }
    }

    [TestCase(ResolvedLanguage.Vietnamese, "quá nhỏ")]
    [TestCase(ResolvedLanguage.English, "too small")]
    public void F2_RegionUnder3x3_ShowsTheMessageInTheOverlayAndStaysInSelection(ResolvedLanguage language, string expectedFragment)
    {
        var rig = Open(CaptureKind.Rectangle, language);
        using (rig.Window)
        {
            rig.Session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.RegionTooSmall, NotificationMessage.Of("Capture.RegionTooSmall")));

            Drag(rig, new PixelPoint(300, 300), new PixelPoint(302, 302), release: true);

            Assert.That(rig.Window.IsOpen, Is.True, "F2: the user stays on the selection screen to drag again");
            Assert.That(rig.Window.TextOf("OverlayMessage"), Does.Contain(expectedFragment).IgnoreCase.And.Not.StartWith("Capture."));
            Assert.That(rig.Notes.Errors, Is.Empty, "the message is inside the overlay, not a box");
            Assert.That(rig.Session.CancelCount, Is.Zero);
            if (language == ResolvedLanguage.Vietnamese)
            {
                rig.Window.Screenshot("capture-overlay-f2-too-small");
            }

            Drag(rig, new PixelPoint(300, 300), new PixelPoint(500, 450), release: true);
            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "a second, real drag captures");
        }
    }

    [Test]
    public void F3_OutlineTooSmall_ShowsTheMessageInTheOverlayAndStaysInSelection()
    {
        var rig = Open(CaptureKind.Freeform);
        using (rig.Window)
        {
            rig.Session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.OutlineTooSmall, NotificationMessage.Of("Capture.OutlineTooSmall")));

            Drag(rig, new PixelPoint(300, 300), new PixelPoint(301, 300), release: true);

            Assert.That(rig.Window.IsOpen, Is.True, "F3: still on the selection screen");
            Assert.That(rig.Window.TextOf("OverlayMessage"), Does.Contain("quá nhỏ").And.Not.StartWith("Capture."));
            Assert.That(rig.Session.CancelCount, Is.Zero);
            rig.Window.Screenshot("capture-overlay-f3-outline-too-small");
        }
    }

    [Test]
    public void Freeform_MidDrag_ShowsTheOutlineAndLightsWhatItEncloses()
    {
        var rig = Open(CaptureKind.Freeform);
        using (rig.Window)
        {
            var corners = new[] { new PixelPoint(300, 200), new PixelPoint(600, 200), new PixelPoint(450, 500) };
            Mouse.MoveTo(OnDesktop(corners[0].X, corners[0].Y));
            Mouse.Down(MouseButton.Left);
            Mouse.MoveTo(OnDesktop(corners[1].X, corners[1].Y));
            Mouse.MoveTo(OnDesktop(corners[2].X, corners[2].Y));
            rig.Window.Host.Settle();

            var rendered = RenderedOverlay.Take(rig.Window);
            // The triangle in overlay-local pixels is (200,100) (500,100) (350,400); its centroid is inside, a corner of the screen is not.
            AssertPixel(rendered, 350, 200, true, "inside the outline");
            AssertPixel(rendered, 30, 500, false, "outside the outline");
            Assert.That(rendered.CountNear(ThemeColor("Brush.Accent"), 30), Is.GreaterThan(300), "the outline being drawn is visible");
            rig.Window.Screenshot("capture-overlay-freeform-drag");

            Mouse.Up(MouseButton.Left);
            rig.Window.Host.Settle();

            var outline = rig.Session.FreeformCompletions.Only("freeform completion");
            Assert.That(outline.First(), Is.EqualTo(corners[0]));
            Assert.That(outline.Last(), Is.EqualTo(corners[2]));
            Assert.That(
                outline.Any(p => Math.Abs(p.X - corners[1].X) <= 12 && Math.Abs(p.Y - corners[1].Y) <= 12),
                Is.True,
                "the outline follows the whole path of the mouse, not just its two ends");
            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        }
    }

    [Test]
    public void Window_Hover_HighlightsTheFrameShowsItsTitleAndAClickCapturesIt()
    {
        var rig = Open(CaptureKind.Window, cursor: new PixelPoint(120, 120));
        using (rig.Window)
        {
            Mouse.MoveTo(OnDesktop(400, 250));
            rig.Window.Host.Settle();

            Assert.That(rig.Window.TextOf("WindowTitleLabel"), Is.EqualTo("Notepad"));
            var rendered = RenderedOverlay.Take(rig.Window);
            // Notepad's frame is (200,200,300,200) on the desktop: overlay-local (100,100,300,200).
            AssertPixel(rendered, 250, 200, true, "inside the highlighted window");
            AssertPixel(rendered, 60, 60, false, "outside the highlighted window");
            AssertPixel(rendered, 600, 450, false, "the other window is not lit while the pointer is not over it");
            Assert.That(rendered.CountNear(ThemeColor("Brush.Accent"), 30), Is.GreaterThan(400), "the frame of the window is outlined");
            rig.Window.Screenshot("capture-overlay-window-hover");

            Mouse.Click(MouseButton.Left);
            rig.Window.Host.Settle();

            Assert.That(rig.Session.WindowCompletions, Is.EqualTo(new[] { new PixelPoint(400, 250) }));
            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        }
    }

    [Test]
    public void Window_HoverOnTheDesktopBackground_HighlightsTheWholeMonitor()
    {
        var rig = Open(CaptureKind.Window);
        using (rig.Window)
        {
            Mouse.MoveTo(OnDesktop(150, 600));
            rig.Window.Host.Settle();

            var rendered = RenderedOverlay.Take(rig.Window);
            AssertPixel(rendered, 60, 300, true, "the whole monitor is lit");
            AssertPixel(rendered, 700, 500, true, "the whole monitor is lit, far corner");
        }
    }

    [Test]
    public void Escape_CancelsTheSessionAndClosesTheOverlay()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            rig.Window.Press(VirtualKeyShort.ESCAPE);

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "Esc leaves the screen as it was");
            Assert.That(rig.Session.CancelCount, Is.EqualTo(1));
            Assert.That(rig.Session.Calls, Does.Not.Contain("CompleteRectangle"));
            Assert.That(rig.Finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Cancelled));
        }
    }

    [Test]
    public void RightClick_CancelsTheSessionAndClosesTheOverlay()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            Mouse.MoveTo(OnDesktop(300, 300));
            Mouse.Click(MouseButton.Right);
            rig.Window.Host.Settle();

            Assert.That(rig.Window.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
            Assert.That(rig.Session.CancelCount, Is.EqualTo(1));
            Assert.That(rig.Finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Cancelled));
        }
    }

    [Test]
    public void RightClick_WhileDragging_CancelsInsteadOfCapturing()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            Drag(rig, new PixelPoint(250, 250), new PixelPoint(450, 400), release: false);

            Mouse.Click(MouseButton.Right);
            rig.Window.Host.Settle();
            Mouse.Up(MouseButton.Left);
            rig.Window.Host.Settle();

            Assert.That(rig.Session.CancelCount, Is.EqualTo(1));
            Assert.That(rig.Session.Calls, Does.Not.Contain("CompleteRectangle"), "the drag that was cancelled must not capture when the button comes up");
        }
    }

    [Test]
    public void Overlay_ClosedByAnythingElse_CancelsTheSessionSoItIsNeverLeftRunning()
    {
        var rig = Open(CaptureKind.Rectangle);
        using (rig.Window)
        {
            WpfHost.Instance.Invoke(() => rig.Window.Window.Close());

            Assert.That(rig.Session.CancelCount, Is.EqualTo(1));
            Assert.That(rig.Session.IsActive, Is.False);
        }
    }

    [TestCase(AppTheme.Light)]
    [TestCase(AppTheme.Dark)]
    public void Contrast_TheOverlaysLabels_ReachTheRatiosInBothThemes(AppTheme theme)
    {
        var rig = Open(CaptureKind.Window, theme: theme, cursor: new PixelPoint(250, 250));
        using (rig.Window)
        {
            rig.Session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.RegionTooSmall, NotificationMessage.Of("Capture.RegionTooSmall")));
            WpfHost.Instance.Invoke(() => rig.ViewModel.PointerMoved(new PixelPoint(250, 250)));
            WpfHost.Instance.Invoke(() => rig.ViewModel.PointerDown(new PixelPoint(250, 250)));
            WpfHost.Instance.Invoke(() => rig.ViewModel.PointerUp(new PixelPoint(250, 250)));
            rig.Window.Host.Settle();

            var problems = new List<string>();
            foreach (var id in new[] { "WindowTitleLabel", "CancelHint", "OverlayMessage" })
            {
                problems.AddRange(LabelContrast(rig.Window, id));
            }

            Assert.That(problems, Is.Empty, string.Join('\n', problems));
            rig.Window.Screenshot($"capture-overlay-labels-{theme.ToString().ToLowerInvariant()}");
        }
    }

    private static IEnumerable<string> LabelContrast(WindowSession session, string automationId) =>
        WpfHost.Instance.Invoke(() =>
        {
            var problems = new List<string>();
            var text = VisualTreeFinder.FindByAutomationId(session.Window, automationId) as System.Windows.Controls.TextBlock;
            if (text is null)
            {
                problems.Add($"{automationId}: not in the overlay");
                return problems;
            }

            var background = FindAncestorBackground(text);
            if (background is null || text.Foreground is not SolidColorBrush foreground)
            {
                problems.Add($"{automationId}: no solid text or background brush to measure");
                return problems;
            }

            var ratio = Contrast.Ratio(foreground, background);
            if (ratio < Contrast.MinimumForText)
            {
                problems.Add($"{automationId}: {ratio:0.00}:1, needs {Contrast.MinimumForText}:1");
            }

            return problems;
        });

    private static SolidColorBrush? FindAncestorBackground(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is System.Windows.Controls.Border { Background: SolidColorBrush brush })
            {
                return brush;
            }
        }

        return null;
    }

    private static Color ThemeColor(string brushKey) => WpfHost.Instance.Invoke(() =>
        ((SolidColorBrush)Application.Current.FindResource(brushKey)).Color);

    /// <summary>The overlay drawn by WPF at one bitmap pixel per physical pixel, so a test can read what the user would see.</summary>
    private sealed class RenderedOverlay
    {
        private readonly byte[] _pixels;
        private readonly int _width;
        private readonly int _height;

        private RenderedOverlay(byte[] pixels, int width, int height)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
        }

        public static RenderedOverlay Take(WindowSession session) => WpfHost.Instance.Invoke(() =>
        {
            var stage = VisualTreeFinder.FindByAutomationId(session.Window, "OverlayStage")
                ?? throw new AssertionException("the overlay has no 'OverlayStage' to render");
            var dpi = VisualTreeHelper.GetDpi(stage);
            var width = (int)Math.Round(stage.ActualWidth * dpi.DpiScaleX);
            var height = (int)Math.Round(stage.ActualHeight * dpi.DpiScaleY);
            Assert.That((width, height), Is.EqualTo((_screen.Width, _screen.Height)), "the stage must be one bitmap pixel per desktop pixel");
            var bitmap = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(new VisualBrush(stage), null, new Rect(0, 0, stage.ActualWidth, stage.ActualHeight));
            }

            bitmap.Render(visual);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            return new RenderedOverlay(pixels, width, height);
        });

        public Color At(int x, int y)
        {
            var i = ((y * _width) + x) * 4;
            return Color.FromRgb(_pixels[i + 2], _pixels[i + 1], _pixels[i]);
        }

        /// <summary>How many pixels are within <paramref name="tolerance"/> of the colour in every channel.</summary>
        public int CountNear(Color color, int tolerance)
        {
            var count = 0;
            for (var i = 0; i < _width * _height * 4; i += 4)
            {
                if (Math.Abs(_pixels[i + 2] - color.R) <= tolerance
                    && Math.Abs(_pixels[i + 1] - color.G) <= tolerance
                    && Math.Abs(_pixels[i] - color.B) <= tolerance)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
