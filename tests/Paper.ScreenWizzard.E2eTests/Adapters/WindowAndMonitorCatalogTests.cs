using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Capture;
using Paper.ScreenWizzard.Infrastructure.Shared;

namespace Paper.ScreenWizzard.E2eTests.Adapters;

/// <summary>The real window and monitor lists of this desktop, checked against WPF's own windows and against GetSystemMetrics.</summary>
[TestFixture]
[NonParallelizable]
public sealed class WindowAndMonitorCatalogTests
{
    private readonly List<ProbeWindow> _open = [];

    [TearDown]
    public void CloseProbes()
    {
        foreach (var probe in _open)
        {
            probe.Close();
        }

        _open.Clear();
    }

    private ProbeWindow OpenProbe(string title, double left, double top, double width = 400, double height = 300, bool topmost = true)
    {
        var probe = ProbeWindow.Show(title, left, top, width, height, withBlocks: false, topmost);
        _open.Add(probe);
        return probe;
    }

    private static WindowInfo Find(IReadOnlyList<WindowInfo> windows, string title)
    {
        var matches = windows.Where(w => w.Title == title).ToList();
        Assert.That(matches, Has.Count.EqualTo(1), $"window '{title}' listed {matches.Count} times among {windows.Count} windows");
        return matches[0];
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr window, [System.Runtime.InteropServices.Out] char[] name, int maxCount);

    private static string ClassOf(long handle)
    {
        var buffer = new char[256];
        var copied = GetClassNameW(new IntPtr(handle), buffer, buffer.Length);
        return copied > 0 ? new string(buffer, 0, copied) : string.Empty;
    }

    [Test]
    public void TheDesktopItself_ProgramManagerAndWorkerW_IsReportedWithTheDesktopFlagAndNothingElseIs()
    {
        // Progman and WorkerW are the desktop's own windows: as big as every monitor together, visible, and under every real window.
        // The adapter only reports what they are (IsDesktop); the use case decides they are no target, so a click on the wallpaper
        // captures the monitor under the pointer and not the whole desktop under the name "Program Manager".
        var windows = new WindowCatalog().GetWindows();

        var unflagged = windows.Where(w => ClassOf(w.Handle) is "Progman" or "WorkerW" && !w.IsDesktop).Select(w => $"{ClassOf(w.Handle)} '{w.Title}'").ToList();
        var wronglyFlagged = windows.Where(w => w.IsDesktop && ClassOf(w.Handle) is not ("Progman" or "WorkerW")).Select(w => w.Title).ToList();

        Assert.That(unflagged, Is.Empty, "every Progman / WorkerW is flagged");
        Assert.That(wronglyFlagged, Is.Empty, "and nothing else is");
        Assert.That(windows.Any(w => w.IsDesktop), Is.True, "control: this desktop has at least one, so the test looked at something");
    }

    [Test]
    public void OverlappingWindows_BothListed_TheLaterActivatedOneIsNearerTheTop()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var lower = OpenProbe("E2E lower " + suffix, 300, 260);
        var upper = OpenProbe("E2E upper " + suffix, 380, 320);
        lower.WaitUntilDrawn();
        upper.WaitUntilDrawn();
        lower.Activate();
        Thread.Sleep(200);
        upper.Activate();
        Thread.Sleep(300);

        var windows = new WindowCatalog().GetWindows();

        var lowerInfo = Find(windows, "E2E lower " + suffix);
        var upperInfo = Find(windows, "E2E upper " + suffix);
        Assert.That(upperInfo.ZOrder, Is.LessThan(lowerInfo.ZOrder), $"upper {upperInfo.ZOrder}, lower {lowerInfo.ZOrder}: the window activated last is on top");
        Assert.That(windows.Select(w => w.ZOrder), Is.EqualTo(Enumerable.Range(0, windows.Count)), "ZOrder is the position in the list, 0 first");
        Assert.That(upperInfo.IsVisible, Is.True);
        Assert.That(upperInfo.IsMinimized, Is.False);
        Assert.That(upperInfo.IsCloaked, Is.False);
        Assert.That(upperInfo.IsOwnOverlay, Is.False, "the snapshot is taken before the overlay is shown");
        Assert.That(upperInfo.Handle, Is.EqualTo(upper.Handle.ToInt64()));

        // Bring the lower one back on top: the order follows.
        lower.Activate();
        Thread.Sleep(300);
        var again = new WindowCatalog().GetWindows();
        Assert.That(Find(again, "E2E lower " + suffix).ZOrder, Is.LessThan(Find(again, "E2E upper " + suffix).ZOrder), "after activating the lower window it is on top");
    }

    [Test]
    public void VisibleFrame_LeavesOutTheInvisibleShadowBorder_ButHoldsTheWholeClientArea()
    {
        var title = "E2E frame " + Guid.NewGuid().ToString("N");
        var probe = OpenProbe(title, 300, 260);
        probe.WaitUntilDrawn();
        Native.GetWindowRect(probe.Handle, out var rect);
        var rectWidth = rect.Right - rect.Left;
        var rectHeight = rect.Bottom - rect.Top;
        var client = probe.ClientInPixels();

        var frame = Find(new WindowCatalog().GetWindows(), title).VisibleFrame;

        // Measured on this Windows 11: a WPF window of 400 x 300 has a GetWindowRect of 400 x 300 that includes an invisible resize border
        // (7 px left, right and bottom), and DWMWA_EXTENDED_FRAME_BOUNDS is the 386 x 293 the user sees. WPF's own ActualWidth is the
        // window rectangle, not the visible frame, so it is not what the visible frame equals.
        TestContext.Out.WriteLine($"window rect {rect.Left},{rect.Top} {rectWidth}x{rectHeight}; visible frame {frame}; client {client}");
        Assert.That(frame.Width, Is.LessThan(rectWidth), "the invisible shadow border is excluded: the frame is narrower than the window rectangle");
        Assert.That(frame.Height, Is.LessThan(rectHeight));
        Assert.That(rectWidth - frame.Width, Is.InRange(2, 40), "by a border's worth of pixels, not by a different window");
        Assert.That(rectHeight - frame.Height, Is.InRange(2, 40));
        Assert.That(frame.X, Is.GreaterThanOrEqualTo(rect.Left));
        Assert.That(frame.Y, Is.GreaterThanOrEqualTo(rect.Top));
        Assert.That(frame.X + frame.Width, Is.LessThanOrEqualTo(rect.Right));
        Assert.That(frame.Y + frame.Height, Is.LessThanOrEqualTo(rect.Bottom));

        // The frame the user sees holds everything the window draws inside it.
        Assert.That(frame.X, Is.LessThanOrEqualTo(client.X), "left of the client area");
        Assert.That(frame.Y, Is.LessThan(client.Y), "the title bar is above the client area");
        Assert.That(frame.X + frame.Width, Is.GreaterThanOrEqualTo(client.X + client.Width), "right of the client area");
        Assert.That(frame.Y + frame.Height, Is.GreaterThanOrEqualTo(client.Y + client.Height), "below the client area");
    }

    [Test]
    public void MinimisedWindow_IsReportedAsMinimised()
    {
        var title = "E2E minimised " + Guid.NewGuid().ToString("N");
        var probe = OpenProbe(title, 300, 260, topmost: false);
        probe.WaitUntilDrawn();
        probe.Minimise();
        StaHost.Instance.Settle();
        Thread.Sleep(400);

        var info = Find(new WindowCatalog().GetWindows(), title);

        Assert.That(info.IsMinimized, Is.True);
    }

    [Test]
    public void EveryListedWindow_HasATitleOrASize()
    {
        var windows = new WindowCatalog().GetWindows();

        Assert.That(windows, Is.Not.Empty);
        Assert.That(
            windows.Where(w => string.IsNullOrEmpty(w.Title) && w.VisibleFrame.Width == 0 && w.VisibleFrame.Height == 0),
            Is.Empty,
            "a window with no title and no size is not a capture target");
    }

    [Test]
    public void Monitors_MatchTheSystemMetrics()
    {
        var monitors = new MonitorCatalog().GetMonitors();

        Assert.That(monitors, Has.Count.EqualTo(Native.GetSystemMetrics(Native.SmCMonitors)), "one entry per visible monitor");
        var left = monitors.Min(m => m.Bounds.X);
        var top = monitors.Min(m => m.Bounds.Y);
        var right = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
        var bottom = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);
        Assert.That(
            new PixelRect(left, top, right - left, bottom - top),
            Is.EqualTo(new PixelRect(
                Native.GetSystemMetrics(Native.SmXVirtualScreen),
                Native.GetSystemMetrics(Native.SmYVirtualScreen),
                Native.GetSystemMetrics(Native.SmCxVirtualScreen),
                Native.GetSystemMetrics(Native.SmCyVirtualScreen))),
            "the union of the monitor bounds is the virtual screen");
        Assert.That(monitors.Count(m => m.IsPrimary), Is.EqualTo(1));
        Assert.That(monitors.Single(m => m.IsPrimary).Bounds.X, Is.Zero, "the primary monitor's corner is the origin");
        Assert.That(monitors.Single(m => m.IsPrimary).Bounds.Y, Is.Zero);
        Assert.That(monitors.Select(m => m.Index), Is.EqualTo(Enumerable.Range(0, monitors.Count)));
        Assert.That(monitors.All(m => m.Dpi >= 96), Is.True, "DPI is at least 100%: " + string.Join(", ", monitors.Select(m => m.Dpi)));
        TestContext.Out.WriteLine(string.Join(" | ", monitors.Select(m => $"#{m.Index} {m.Bounds} {m.Dpi} dpi primary={m.IsPrimary}")));
    }

    [Test]
    public void Cursor_IsInPhysicalPixels_AndTheLayoutSignatureIsStable()
    {
        var catalog = new MonitorCatalog();
        Native.GetCursorPos(out var before);

        var position = catalog.GetCursorPosition();
        Native.GetCursorPos(out var after);

        Assert.That(position.X, Is.InRange(Math.Min(before.X, after.X), Math.Max(before.X, after.X)));
        Assert.That(position.Y, Is.InRange(Math.Min(before.Y, after.Y), Math.Max(before.Y, after.Y)));
        var first = catalog.GetLayoutSignature();
        var second = new MonitorCatalog().GetLayoutSignature();
        Assert.That(first, Is.Not.Empty);
        Assert.That(second, Is.EqualTo(first), "the signature only changes when the monitors do");
        foreach (var monitor in catalog.GetMonitors())
        {
            Assert.That(first, Does.Contain(monitor.Bounds.Width.ToString()).And.Contain(monitor.Dpi.ToString()), "bounds and DPI are both in the signature");
        }
    }
}
