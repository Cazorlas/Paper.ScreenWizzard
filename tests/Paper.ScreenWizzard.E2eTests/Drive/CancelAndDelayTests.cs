using FlaUI.Core.WindowsAPI;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>Plan T18, flow 5 (cancelling, too-small drag, another kind's hotkey) and flow 6 (the delay).</summary>
[TestFixture]
public sealed class CancelAndDelayTests : DriveBase
{
    private const string Sentinel = "e2e-sentinel-clipboard-text";

    [Test]
    public void Escape_DuringSelection_CapturesNothing_AndTheOverlayGoes()
    {
        ProtectClipboard();
        Desk.SetClipboardText(Sentinel);
        NewApp();
        StartAndWaitForBar();
        ShowProbe(Desk.Monitor(0), "esc");
        var overlay = OpenOverlay(CaptureKind.Rectangle);
        var frontAfterHotkey = AppKeys.ForegroundBelongsTo(App);

        // A drag in progress, then Esc.
        var probe = Probe!;
        var (from, to) = DragInside(probe, 10, 20, 300, 200);
        Desk.PointerTo(from.X, from.Y);
        FlaUI.Core.Input.Mouse.Down(FlaUI.Core.Input.MouseButton.Left);
        Desk.PointerTo(from.X + 100, from.Y + 80);
        Thread.Sleep(200);
        AppKeys.Type(App, overlay, VirtualKeyShort.ESCAPE);
        FlaUI.Core.Input.Mouse.Up(FlaUI.Core.Input.MouseButton.Left);

        Assert.That(App.WaitWindowGone("SelectionOverlay"), Is.True, "Esc removes the overlay (the overlay had the keyboard right after the hotkey: " + frontAfterHotkey + ")");
        Thread.Sleep(600);
        Assert.That(App.FindWindow("CaptureDoneWindow"), Is.Null, "nothing was captured: no dialog");
        Assert.That(App.SavedFiles(), Is.Empty);
        Assert.That(Desk.ClipboardText(), Is.EqualTo(Sentinel), "clipboard unchanged");
        Assert.That(Desk.ClipboardImage(), Is.Null);
        Assert.That(App.FindWindow("CaptureBarWindow"), Is.Not.Null, "the screen is back to normal: the capture bar is shown again");
        _ = to;
    }

    [Test]
    public void RightClick_DuringSelection_CapturesNothing_AndTheOverlayGoes()
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "rightclick");
        OpenOverlay(CaptureKind.Rectangle);
        var client = probe.ClientInPixels();

        Desk.RightClickAt(client.X + 50, client.Y + 50);

        Assert.That(App.WaitWindowGone("SelectionOverlay"), Is.True, "a right click removes the overlay");
        Thread.Sleep(600);
        Assert.That(App.FindWindow("CaptureDoneWindow"), Is.Null, "no dialog");
        Assert.That(App.SavedFiles(), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void TooSmallDrag_KeepsTheOverlay_ShowsTheMessage_AndABigDragStillWorks(int monitorIndex)
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(monitorIndex), "small");
        var overlay = OpenOverlay(CaptureKind.Rectangle);
        var (from, _) = DragInside(probe, 30, 100, 2, 2);

        Desk.Drag(from, new PixelPoint(from.X + 2, from.Y + 2), 2);
        Thread.Sleep(400);

        Assert.That(App.Windows("SelectionOverlay"), Has.Count.EqualTo(1), "the overlay is still there");
        Assert.That(App.FindWindow("CaptureDoneWindow"), Is.Null, "a 2 x 2 drag captures nothing");
        var message = AppRun.TextOf(App.FindWindow("SelectionOverlay")!, "OverlayMessage");
        Assert.That(message, Does.Contain("quá nhỏ"), "the overlay says the region is too small");
        var (big, bigTo) = DragInside(probe, 30, 100, 120, 90);
        Desk.Drag(big, bigTo);
        var dialog = App.WaitWindow("CaptureDoneWindow");
        Assert.That(AppRun.TextOf(dialog, "SizeText"), Is.EqualTo("120 × 90"), "after the message the same selection screen still takes a real drag");
        _ = overlay;
    }

    [Test]
    public void AnotherKindsHotkey_WhileSelecting_ReplacesTheRun_NeverStackingOverlays()
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "replace");
        OpenOverlay(CaptureKind.Rectangle);
        Assert.That(App.Windows("SelectionOverlay"), Has.Count.EqualTo(1), "one overlay after the first hotkey");

        AppRun.PressHotkey(CaptureKind.Window);
        var most = 0;
        var end = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < end)
        {
            most = Math.Max(most, App.Windows("SelectionOverlay").Count);
            Thread.Sleep(60);
        }

        Assert.That(most, Is.EqualTo(1), "the second hotkey replaced the first run: exactly one overlay at every moment (SPEC capture F8)");

        // Full screen has no overlay at all: it replaces a running selection and ends in the dialog.
        AppRun.PressHotkey(CaptureKind.FullScreen);
        App.WaitWindow("CaptureDoneWindow", 10);
        Assert.That(App.WaitWindowGone("SelectionOverlay"), Is.True, "the full-screen run replaced the selection: no overlay left");
    }

    [Test]
    [Description("DEFECT D1: a capture started while an overlay is up snapshots the old overlay (dimmed pixels, and the old overlay is picked as the window under the pointer)")]
    public void ReplacedRun_TheImageHoldsNoTraceOfTheOldOverlay()
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "replaced-pixels");
        var (from, to) = DragInside(probe, 10, 20, 300, 200);
        var inside = BlocksInside(probe, from, 300, 200);
        OpenOverlay(CaptureKind.Rectangle);
        AppRun.PressHotkey(CaptureKind.Rectangle);
        Thread.Sleep(1500);
        Assert.That(App.Windows("SelectionOverlay"), Has.Count.EqualTo(1));

        Desk.Drag(from, to);
        var dialog = App.WaitWindow("CaptureDoneWindow");
        AppRun.Invoke(dialog, "SaveButton");
        var image = Desk.ReadImage(App.WaitForSavedFile());

        Assert.That((image.Width, image.Height), Is.EqualTo((300, 200)));
        RectangleCaptureTests.AssertBlockPixels(image, inside, 0);
        RectangleCaptureTests.AssertOnlyTestWindowColours(image);
    }

    [Test]
    [Description("DEFECT D1: same cause, seen through the window kind: the click captures the whole virtual desktop")]
    public void ReplacedRun_ByTheWindowKind_HoverFindsTheWindowUnderThePointer()
    {
        NewApp();
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "replaced-window");
        OpenOverlay(CaptureKind.Rectangle);
        AppRun.PressHotkey(CaptureKind.Window);
        Thread.Sleep(1500);
        var frame = Desk.VisibleFrame(probe);
        for (var i = 0; i < 8; i++)
        {
            Desk.PointerTo(frame.X + (frame.Width / 2) - 80 + (i * 10), frame.Y + (frame.Height / 2) - 20 + (i * 3));
            Thread.Sleep(60);
        }

        Thread.Sleep(600);
        var overlay = App.FindWindow("SelectionOverlay")!;
        var title = StaHost.Instance.Invoke(() => probe.Window.Title);
        var hover = AppRun.Find(overlay, "WindowTitleLabel")?.Name;
        Desk.ClickAt(frame.X + (frame.Width / 2), frame.Y + (frame.Height / 2));
        var dialog = App.WaitWindow("CaptureDoneWindow", 6);
        Assert.That(AppRun.TextOf(dialog, "SizeText"), Is.EqualTo($"{frame.Width} × {frame.Height}"), "after the window-kind hotkey replaced a rectangle run, a click on the test window captures its visible frame (hover label was [" + hover + "], title " + title + ")");
    }

    [Test]
    public void Delay_ThreeSeconds_CountdownShows_AndThePixelsAreThoseAfterTheDelay()
    {
        NewApp(s => s with { DelaySeconds = 3 });
        StartAndWaitForBar();
        var probe = ShowProbe(Desk.Monitor(0), "delay");
        var (from, to) = DragInside(probe, 10, 20, 300, 200);

        var pressedAt = DateTime.UtcNow;
        AppRun.PressHotkey(CaptureKind.Rectangle);
        var seen = new List<string>();
        var repainted = false;
        DateTime? overlayAt = null;
        while ((DateTime.UtcNow - pressedAt).TotalSeconds < 8 && overlayAt is null)
        {
            var countdown = App.FindWindow("CountdownWindow");
            if (countdown is not null)
            {
                string text;
                try
                {
                    text = AppRun.Find(countdown, "CountdownText")?.Name ?? string.Empty;
                }
                catch (Exception)
                {
                    text = string.Empty;
                }

                if (text.Length > 0 && (seen.Count == 0 || seen[^1] != text))
                {
                    seen.Add(text);
                    if (!repainted)
                    {
                        AppRun.Shot(countdown, ShotName("countdown", "m0"));
                    }
                }
            }

            if (!repainted && (DateTime.UtcNow - pressedAt).TotalSeconds >= 1.0)
            {
                // One second in: the window changes; a capture taken at the keypress would still show red.
                Desk.RepaintBlock(probe, 0, 150, 30, 200);
                StaHost.Instance.Settle();
                repainted = true;
            }

            if (App.FindWindow("SelectionOverlay") is not null)
            {
                overlayAt = DateTime.UtcNow;
            }

            Thread.Sleep(100);
        }

        Assert.That(overlayAt, Is.Not.Null, "the overlay appeared; the exe's windows: " + App.Describe());
        var elapsed = (overlayAt!.Value - pressedAt).TotalSeconds;
        Assert.That(elapsed, Is.InRange(2.7, 4.5), "nothing is on screen but the countdown for about 3 seconds; the overlay came after " + elapsed.ToString("F2"));
        Assert.That(seen, Is.EqualTo(new[] { "3", "2", "1" }), "the countdown numbers read from the countdown window");

        Thread.Sleep(400);
        Desk.Drag(from, to);
        var dialog = App.WaitWindow("CaptureDoneWindow");
        AppRun.Invoke(dialog, "SaveButton");
        var image = Desk.ReadImage(App.WaitForSavedFile());
        var red = probe.Blocks()[0];
        var x = red.CentreX - from.X;
        var y = red.CentreY - from.Y;
        var actual = Desk.At(image, x, y);
        Assert.That(Desk.Distance(actual, 150, 30, 200), Is.LessThanOrEqualTo(2), $"the first block was repainted 1 s after the keypress; the file has {Pixels.Describe(actual)} at ({x},{y}), so the capture is of the moment after the delay");
        var green = probe.Blocks()[1];
        var g = Desk.At(image, green.CentreX - from.X, green.CentreY - from.Y);
        Assert.That(Desk.Distance(g, 20, 200, 60), Is.LessThanOrEqualTo(2), "the untouched block is unchanged");
    }
}
