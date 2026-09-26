using FlaUI.Core.AutomationElements;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.E2eTests.Support;

/// <summary>
/// The common ground of the drive tests: each test owns one exe process and one test window and gets them back cleaned up in
/// <see cref="TearDown"/> whatever happened, including the clipboard when the test asked to protect it.
/// </summary>
[NonParallelizable]
public abstract class DriveBase
{
    private ClipboardBackup? _clipboard;

    protected AppRun App { get; private set; } = null!;

    protected ProbeWindow? Probe { get; set; }

    [SetUp]
    public void RefuseWhileAnotherCopyRuns()
    {
        var refusal = HarnessGuard.Refusal([]);
        if (refusal is not null)
        {
            Assert.Inconclusive(refusal);
        }
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            try
            {
                Probe?.Close();
            }
            catch (Exception)
            {
                // The window is gone already.
            }

            Probe = null;
        }
        finally
        {
            try
            {
                App?.Dispose();
            }
            finally
            {
                _clipboard?.Restore();
                _clipboard = null;
            }
        }
    }

    /// <summary>Makes an app run (not started yet) with the seeded settings, tweaked when the test needs other values.</summary>
    protected AppRun NewApp(Func<AppSettings, AppSettings>? tweak = null, bool seed = true, bool defaultBarPlace = true)
    {
        App = new AppRun(tweak, seed, defaultBarPlace);
        return App;
    }

    /// <summary>Starts the exe and waits for its capture bar, the sign that start-up finished and the hotkeys are registered.</summary>
    protected AutomationElement StartAndWaitForBar()
    {
        App.Start();
        return App.WaitWindow("CaptureBarWindow", 15);
    }

    /// <summary>Backs the clipboard up now and restores it in <see cref="TearDown"/>; call it before the first thing that can write to it.</summary>
    protected void ProtectClipboard() => _clipboard ??= ClipboardBackup.Take();

    protected ProbeWindow ShowProbe(MonitorInfo monitor, string tag = "probe", int offsetX = 120, int offsetY = 200)
    {
        Probe = Desk.ShowProbeOn(monitor, "E2E drive " + tag + " " + Guid.NewGuid().ToString("N"), offsetX, offsetY);
        return Probe;
    }

    /// <summary>Presses the hotkey of a kind and returns the overlay window (or fails naming the windows that did exist).</summary>
    protected AutomationElement OpenOverlay(CaptureKind kind)
    {
        AppRun.PressHotkey(kind);
        var overlay = App.WaitWindow("SelectionOverlay", 8);
        Thread.Sleep(400);
        return overlay;
    }

    /// <summary>
    /// The drag rectangle for a probe window: <paramref name="width"/> x <paramref name="height"/> physical pixels starting
    /// (<paramref name="left"/>, <paramref name="top"/>) physical pixels inside the probe's client area.
    /// </summary>
    protected static (PixelPoint From, PixelPoint To) DragInside(ProbeWindow probe, int left, int top, int width, int height)
    {
        var client = probe.ClientInPixels();
        var from = new PixelPoint(client.X + left, client.Y + top);
        return (from, new PixelPoint(from.X + width, from.Y + height));
    }

    /// <summary>The blocks of the probe whose centres lie inside the region, with where each is relative to the region's corner.</summary>
    protected static IReadOnlyList<(BlockPlace Block, int X, int Y)> BlocksInside(ProbeWindow probe, PixelPoint origin, int width, int height) =>
        probe.Blocks()
            .Where(b => b.CentreX >= origin.X && b.CentreX < origin.X + width && b.CentreY >= origin.Y && b.CentreY < origin.Y + height)
            .Select(b => (b, b.CentreX - origin.X, b.CentreY - origin.Y))
            .ToList();

    /// <summary>The whole user gesture for a rectangle: the hotkey, a drag over the test window, and the "Đã chụp" dialog that follows.</summary>
    protected AutomationElement CaptureToDialog(ProbeWindow probe, int left = 10, int top = 20, int width = 300, int height = 200)
    {
        var (from, to) = DragInside(probe, left, top, width, height);
        OpenOverlay(CaptureKind.Rectangle);
        Desk.Drag(from, to);
        return App.WaitWindow("CaptureDoneWindow");
    }

    protected static string ShotName(string flow, string monitor) => $"drive-{flow}-{monitor}";
}
