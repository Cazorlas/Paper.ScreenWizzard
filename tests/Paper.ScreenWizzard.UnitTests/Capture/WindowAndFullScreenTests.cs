using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Capture.Models;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture: "Cửa sổ" and "Toàn màn hình".</summary>
[TestFixture]
public sealed class WindowAndFullScreenTests
{
    private CaptureFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new CaptureFixture();

    private static CaptureRequest WindowRequest => CaptureData.Request(CaptureKind.Window);

    private static PixelImage ImageOf(CaptureOutcome outcome)
    {
        Assert.That(outcome.Image, Is.Not.Null, "no image; issue " + outcome.Issue);
        return outcome.Image!;
    }

    // ---- Cửa sổ trên cùng dưới con trỏ được chọn ----

    [Test]
    public async Task OfTwoOverlappingWindowsTheTopmostUnderThePointerIsChosen()
    {
        // The catalog lists the back window first on purpose: the winner is decided by ZOrder, not by list position.
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "back", new PixelRect(100, 100, 800, 600), 1));
        _fixture.Windows.Windows.Add(CaptureData.Window(2, "front", new PixelRect(300, 200, 800, 600), 0));
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(500, 300));

        Assert.That(hit.Found, Is.True);
        Assert.That(hit.Title, Is.EqualTo("front"));
        Assert.That(hit.Frame, Is.EqualTo(new PixelRect(300, 200, 800, 600)));
        Assert.That(hit.IsWholeMonitor, Is.False);
    }

    [Test]
    public async Task OutsideTheOverlapOnlyTheWindowThatContainsThePointerIsChosen()
    {
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "back", new PixelRect(100, 100, 800, 600), 1));
        _fixture.Windows.Windows.Add(CaptureData.Window(2, "front", new PixelRect(300, 200, 800, 600), 0));
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(150, 150));

        Assert.That(hit.Title, Is.EqualTo("back"));
    }

    [TestCase("minimized")]
    [TestCase("hidden")]
    [TestCase("cloaked")]
    [TestCase("ownOverlay")]
    public async Task AMinimizedHiddenCloakedOrOverlayWindowIsNeverChosen(string kind)
    {
        var excluded = kind switch
        {
            "minimized" => CaptureData.Window(9, "excluded", new PixelRect(0, 0, 1920, 1080), 0, minimized: true),
            "hidden" => CaptureData.Window(9, "excluded", new PixelRect(0, 0, 1920, 1080), 0, visible: false),
            "cloaked" => CaptureData.Window(9, "excluded", new PixelRect(0, 0, 1920, 1080), 0, cloaked: true),
            _ => CaptureData.Window(9, "excluded", new PixelRect(0, 0, 1920, 1080), 0, overlay: true),
        };
        _fixture.Windows.Windows.Add(excluded);
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "normal", new PixelRect(100, 100, 800, 600), 3));
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(500, 300));

        Assert.That(hit.Found, Is.True);
        Assert.That(hit.Title, Is.EqualTo("normal"));
    }

    [Test]
    public async Task AllExcludedWindowsOverThePointerLeaveTheWholeMonitor()
    {
        _fixture.Windows.Windows.Add(CaptureData.Window(9, "overlay", new PixelRect(0, 0, 1920, 1080), 0, overlay: true));
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(500, 300));

        Assert.That(hit.IsWholeMonitor, Is.True);
    }

    [Test]
    public async Task OverTheDesktopBackgroundTheWholeMonitorUnderThePointerIsChosen()
    {
        _fixture.UseMonitors(CaptureData.SideBySide);
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "on monitor 1", new PixelRect(100, 100, 800, 600), 0));
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(2500, 500));

        Assert.That(hit.Found, Is.True);
        Assert.That(hit.IsWholeMonitor, Is.True);
        Assert.That(hit.Frame, Is.EqualTo(new PixelRect(1920, 0, 1920, 1080)));
    }

    [Test]
    public async Task ThePointerOnNoMonitorFindsNothing()
    {
        var session = await _fixture.BeginAsync(WindowRequest);

        var hit = session.HitTestWindow(new PixelPoint(5000, 5000));

        Assert.That(hit.Found, Is.False);
    }

    [Test]
    public async Task ClickingTheDesktopBackgroundCapturesTheWholeMonitorUnderThePointer()
    {
        _fixture.UseMonitors(CaptureData.SideBySide);
        var session = await _fixture.BeginAsync(WindowRequest);

        var outcome = session.CompleteWindow(new PixelPoint(2500, 500));

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(1920, 0, 1920, 1080)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(1920));
        Assert.That(image.Height, Is.EqualTo(1080));
    }

    [Test]
    public async Task ClickingOnNoMonitorAndNoWindowReportsFailedAndTheSessionStays()
    {
        var session = await _fixture.BeginAsync(WindowRequest);

        var outcome = session.CompleteWindow(new PixelPoint(5000, 5000));

        Assert.That(outcome.Image, Is.Null);
        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.Failed));
        Assert.That(session.IsActive, Is.True);
    }

    // ---- Ảnh cửa sổ không dính viền bóng ----

    [Test]
    public async Task AWindowWhoseVisibleFrameIs1000x700GivesA1000x700ImageOfThatFrame()
    {
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "app", new PixelRect(200, 100, 1000, 700), 0));
        var session = await _fixture.BeginAsync(WindowRequest);

        var outcome = session.CompleteWindow(new PixelPoint(600, 400));

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(200, 100, 1000, 700)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(1000));
        Assert.That(image.Height, Is.EqualTo(700));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 200, 100), Is.EqualTo(-1));
    }

    [Test]
    public async Task AMaximizedWindowOverhangingTheScreenGives1920x1080()
    {
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "maximized", new PixelRect(-8, -8, 1936, 1096), 0));
        var session = await _fixture.BeginAsync(WindowRequest);

        var outcome = session.CompleteWindow(new PixelPoint(900, 500));

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 0, 1920, 1080)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(1920));
        Assert.That(image.Height, Is.EqualTo(1080));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 0, 0), Is.EqualTo(-1));
    }

    // ---- Toàn màn hình ----

    [Test]
    public async Task FullScreenOfTheMonitorUnderTheCursorGivesThe1920x1080OfMonitorTwo()
    {
        _fixture.UseMonitors(CaptureData.SideBySide);
        _fixture.Monitors.Cursor = new PixelPoint(2500, 500);
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen, scope: FullScreenScope.MonitorUnderCursor));

        // The cursor moves on after the snapshot; the snapshot's own cursor position decides.
        _fixture.Monitors.Cursor = new PixelPoint(10, 10);
        var outcome = session.CompleteFullScreen();

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(1920, 0, 1920, 1080)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(1920));
        Assert.That(image.Height, Is.EqualTo(1080));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 1920, 0), Is.EqualTo(-1));
    }

    [Test]
    public async Task FullScreenOfAllMonitorsGivesOneImage3840x1080()
    {
        _fixture.UseMonitors(CaptureData.SideBySide);
        _fixture.Monitors.Cursor = new PixelPoint(2500, 500);
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen, scope: FullScreenScope.AllMonitors));

        var outcome = session.CompleteFullScreen();

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 0, 3840, 1080)));
        var image = ImageOf(outcome);
        Assert.That(image.Width, Is.EqualTo(3840));
        Assert.That(image.Height, Is.EqualTo(1080));
        Assert.That(CaptureData.FirstPixelDifferingFromScreen(image, 0, 0), Is.EqualTo(-1));
    }

    [Test]
    public async Task FullScreenWithTheCursorOnNoMonitorFallsBackToThePrimaryMonitor()
    {
        _fixture.UseMonitors(CaptureData.LeftAndPrimary);
        _fixture.Monitors.Cursor = new PixelPoint(9000, 9000);
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen));

        var outcome = session.CompleteFullScreen();

        Assert.That(outcome.Region, Is.EqualTo(new PixelRect(0, 0, 1920, 1080)));
    }
}
