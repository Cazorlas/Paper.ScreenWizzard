using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Capture.Fakes;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;

namespace Paper.ScreenWizzard.UnitTests.Capture;

/// <summary>SPEC capture: "Độ trễ và con trỏ", the snapshot, and F6, F8, F9.</summary>
[TestFixture]
public sealed class BeginAndSessionTests
{
    private CaptureFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = new CaptureFixture();

    // ---- Độ trễ ----

    [Test]
    public async Task ADelayOfFiveSecondsCountsFiveToOneAndTakesNothingUntilTheLastWaitEnded()
    {
        var progress = new FakeProgress(_fixture.Events);

        var result = await _fixture.Interactor.BeginAsync(CaptureData.Request(CaptureKind.Rectangle, delaySeconds: 5), progress, CancellationToken.None);

        Assert.That(
            _fixture.Events,
            Is.EqualTo(new[]
            {
                "report 5", "delay 1s",
                "report 4", "delay 1s",
                "report 3", "delay 1s",
                "report 2", "delay 1s",
                "report 1", "delay 1s",
                "capture",
            }));
        Assert.That(progress.Reports, Is.EqualTo(new[] { 5, 4, 3, 2, 1 }));
        Assert.That(result.Session, Is.Not.Null);
        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(1), "the screen is read once");
    }

    [Test]
    public async Task ADelayOfZeroWaitsAndReportsNothing()
    {
        var progress = new FakeProgress(_fixture.Events);

        var result = await _fixture.Interactor.BeginAsync(CaptureData.Request(CaptureKind.Rectangle), progress, CancellationToken.None);

        Assert.That(result.Session, Is.Not.Null);
        Assert.That(_fixture.Delay.Durations, Is.Empty);
        Assert.That(progress.Reports, Is.Empty);
        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(1));
    }

    // ---- Con trỏ ----

    [Test]
    public async Task TheCursorFlagOffIsPassedToTheScreenAsOff()
    {
        await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle, includeCursor: false));

        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Screen.Calls[0].IncludeCursor, Is.False);
    }

    [Test]
    public async Task TheCursorFlagOnIsPassedToTheScreenAsOn()
    {
        await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle, includeCursor: true));

        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Screen.Calls[0].IncludeCursor, Is.True);
    }

    // ---- The snapshot ----

    [Test]
    public async Task TheSnapshotHoldsTheWholeVirtualScreenTheMonitorsTheWindowsTheCursorAndTheLayoutSignature()
    {
        _fixture.UseMonitors(CaptureData.LeftAndPrimary);
        _fixture.Monitors.Cursor = new PixelPoint(-500, 700);
        _fixture.Monitors.Signature = "two-monitors-v7";
        _fixture.Windows.Windows.Add(CaptureData.Window(1, "app", new PixelRect(100, 100, 800, 600), 0));

        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Window, includeCursor: true));

        var snapshot = session.Snapshot;
        Assert.That(session.Kind, Is.EqualTo(CaptureKind.Window));
        Assert.That(session.IsActive, Is.True);
        Assert.That(snapshot.VirtualScreen, Is.EqualTo(new PixelRect(-1920, 0, 3840, 1080)));
        Assert.That(snapshot.Image.Width, Is.EqualTo(3840));
        Assert.That(snapshot.Image.Height, Is.EqualTo(1080));
        Assert.That(snapshot.Monitors, Has.Count.EqualTo(2));
        Assert.That(snapshot.Windows, Has.Count.EqualTo(1));
        Assert.That(snapshot.CursorPosition, Is.EqualTo(new PixelPoint(-500, 700)));
        Assert.That(snapshot.LayoutSignature, Is.EqualTo("two-monitors-v7"));
        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(1));
        Assert.That(_fixture.Screen.Calls[0].Area, Is.EqualTo(new PixelRect(-1920, 0, 3840, 1080)));
    }

    // ---- Session lifecycle ----

    [Test]
    public async Task ASuccessfulCompletionEndsTheSessionAndALaterCompletionReportsFailed()
    {
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));

        var first = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 200));
        var second = session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 200));

        Assert.That(first.Issue, Is.EqualTo(CaptureIssue.None));
        Assert.That(session.IsActive, Is.False);
        Assert.That(second.Issue, Is.EqualTo(CaptureIssue.Failed));
        Assert.That(second.Image, Is.Null);
    }

    [Test]
    public async Task CancelEndsTheSessionAndNothingCanBeCompletedAfterwards()
    {
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen));

        session.Cancel();
        var outcome = session.CompleteFullScreen();

        Assert.That(session.IsActive, Is.False);
        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.Failed));
        Assert.That(outcome.Image, Is.Null);
    }

    [Test]
    public async Task TheScreenFailingIsReportedAsFailedWithNoSession()
    {
        _fixture.Screen.Override = new ScreenCaptureResult(null, ScreenCaptureIssue.Failed);

        var result = await _fixture.Interactor.BeginAsync(CaptureData.Request(CaptureKind.Rectangle), null, CancellationToken.None);

        Assert.That(result.Session, Is.Null);
        Assert.That(result.Issue, Is.EqualTo(CaptureIssue.Failed));
    }

    // ---- F6 ----

    [Test]
    public async Task F6_AMonitorChangeWhileChoosingCancelsTheSessionWithDisplayChanged()
    {
        _fixture.Monitors.Signature = "layout-A";
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));

        var unchanged = session.CheckDisplayUnchanged();
        var stillActive = session.IsActive;
        _fixture.Monitors.Signature = "layout-B";
        var changed = session.CheckDisplayUnchanged();

        Assert.Multiple(() =>
        {
            Assert.That(unchanged, Is.EqualTo(CaptureIssue.None), "same layout");
            Assert.That(stillActive, Is.True);
            Assert.That(changed, Is.EqualTo(CaptureIssue.DisplayChanged), "a monitor was unplugged");
            Assert.That(session.IsActive, Is.False, "the capture is cancelled");
        });
        Assert.That(session.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 200)).Image, Is.Null, "nothing is captured afterwards");
    }

    // ---- F8 ----

    [Test]
    public async Task F8_ANewCaptureCancelsTheOpenSessionAndTheCountdownOfAnEarlierOne()
    {
        // A newer capture replaces the session that is open.
        var a = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Rectangle));
        var b = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.Freeform));
        Assert.That(a.IsActive, Is.False, "the older session is dropped");
        Assert.That(b.IsActive, Is.True, "the newer one runs");
        Assert.That(a.CompleteRectangle(new PixelPoint(10, 10), new PixelPoint(200, 200)).Image, Is.Null, "the dropped session captures nothing");

        // A newer capture also replaces a countdown that has not finished.
        _fixture.Delay.Manual = true;
        var progress = new FakeProgress(_fixture.Events);
        var counting = _fixture.Interactor.BeginAsync(CaptureData.Request(CaptureKind.Window, delaySeconds: 5), progress, CancellationToken.None);
        Assert.That(_fixture.Delay.Pending, Has.Count.EqualTo(1), "the earlier capture is waiting out its first second");
        var d = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen));
        Assert.That(b.IsActive, Is.False, "the session open when the newest began is dropped as well");
        Assert.That(d.IsActive, Is.True);

        _fixture.Delay.CompleteAll();
        var finished = await Task.WhenAny(counting, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.That(finished, Is.SameAs(counting), "the superseded countdown must end at its next check, not wait out the other four seconds");
        var earlier = await counting;

        Assert.That(earlier.Session, Is.Null, "the superseded countdown gives no session");
        Assert.That(earlier.Issue, Is.EqualTo(CaptureIssue.None), "and no error either");
        Assert.That(progress.Reports, Is.EqualTo(new[] { 5 }), "it does not go on counting");
        Assert.That(_fixture.Screen.Calls, Has.Count.EqualTo(3), "reads for a, b and d; none for the superseded countdown");
        Assert.That(d.IsActive, Is.True, "the newest session is untouched by the older countdown ending");
    }

    // ---- F9 ----

    [Test]
    public async Task F9_AnImageTooLargeToHoldIsReportedAsOutOfMemoryAtTheSnapshotAndWhileCropping()
    {
        // At the snapshot: the screen source runs out of memory.
        _fixture.Screen.Override = new ScreenCaptureResult(null, ScreenCaptureIssue.OutOfMemory);
        var snapshot = await _fixture.Interactor.BeginAsync(CaptureData.Request(CaptureKind.Rectangle), null, CancellationToken.None);
        Assert.That(snapshot.Session, Is.Null);
        Assert.That(snapshot.Issue, Is.EqualTo(CaptureIssue.OutOfMemory));

        // While cropping: a 30000 x 30000 desktop cannot be cropped whole (3.6 GB of pixels). The fake hands back a token image,
        // so nothing that big is ever allocated by the test.
        _fixture.UseMonitors([CaptureData.Monitor(0, 0, 0, 30000, 30000, true)]);
        _fixture.Screen.Override = new ScreenCaptureResult(new PixelImage(30000, 30000, []), ScreenCaptureIssue.None);
        var session = await _fixture.BeginAsync(CaptureData.Request(CaptureKind.FullScreen));

        var outcome = session.CompleteFullScreen();

        Assert.That(outcome.Issue, Is.EqualTo(CaptureIssue.OutOfMemory));
        Assert.That(outcome.Image, Is.Null);
        Assert.That(outcome.Message?.Key, Is.EqualTo("Capture.ImageTooLarge"));
    }
}
