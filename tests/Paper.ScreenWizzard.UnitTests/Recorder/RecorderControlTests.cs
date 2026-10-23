using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Recorder.Fakes;
using Paper.ScreenWizzard.UseCases.Recorder.Models;

namespace Paper.ScreenWizzard.UnitTests.Recorder;

/// <summary>
/// SPEC recorder, Inputs and Assumptions: the two hotkeys' decisions (pause or resume, stop or start), the area's refusal message (F7),
/// the monitors' numbering on the bar, and the bitrate that keeps 10 minutes near 300 MB.
/// </summary>
[TestFixture]
public sealed class RecorderControlTests
{
    private RecorderRig _rig = null!;

    [SetUp]
    public void SetUp() => _rig = new RecorderRig();

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static MonitorInfo Monitor(int index, int x, int y, bool primary = false) => new(index, new PixelRect(x, y, 1920, 1080), primary, 96);

    [Test]
    public async Task ThePauseHotkey_PausesARecording_ThenResumesIt()
    {
        var states = new List<RecorderState>();
        _rig.Frames.Frames(S(0), S(2), 30);
        _rig.Frames.At(S(2.01), () => states.Add(_rig.Recorder.TogglePause()));
        _rig.Frames.At(S(3), () => states.Add(_rig.Recorder.TogglePause()));
        _rig.Frames.Frames(S(3.01), S(5), 30);

        var (_, result) = await _rig.RunAsync();

        Assert.That(states, Is.EqualTo(new[] { RecorderState.Paused, RecorderState.Recording }));
        Assert.That(result!.Duration.TotalSeconds, Is.EqualTo(4).Within(0.1), "the paused second is not in the video");
    }

    [Test]
    public void ThePauseHotkey_WithNothingRecording_DoesNothing()
    {
        Assert.That(_rig.Recorder.TogglePause(), Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public async Task TheStartStopHotkey_WithNothingRunning_LetsARecordingStart()
    {
        Assert.That(await _rig.Recorder.StopIfActiveAsync(), Is.False);
        Assert.That(_rig.Writer.Opened, Is.Null, "and it made no file");
    }

    [Test]
    public async Task TheStartStopHotkey_DuringTheCountdown_StopsIt_AndStartsNothing()
    {
        _rig.Countdown.Hold = true;
        var start = _rig.Recorder.StartAsync(RecorderRig.Request(RecorderRig.Settings(countdown: 3)), null, CancellationToken.None);
        await _rig.Countdown.Waiting.Task;

        Assert.That(await _rig.Recorder.StopIfActiveAsync(), Is.True);
        Assert.That((await start).Issue, Is.EqualTo(RecordStartIssue.Cancelled));
        Assert.That(_rig.Recorder.State, Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public void ARegionTooSmall_IsRefusedWithTheMessageThatAsksForALargerOne()
    {
        var answer = _rig.Recorder.ResolveArea(RecordTargetKind.Region, null, new PixelRect(10, 10, 15, 40), [Monitor(0, 0, 0, true)], default);

        Assert.That(answer.IsUsable, Is.False);
        Assert.That(answer.Message?.Key, Is.EqualTo("Recorder.AreaTooSmall"));
    }

    [Test]
    public void ARegionOnNoMonitor_IsRefusedWithItsOwnMessage()
    {
        var answer = _rig.Recorder.ResolveArea(RecordTargetKind.Region, null, new PixelRect(5000, 5000, 400, 300), [Monitor(0, 0, 0, true)], default);

        Assert.That(answer.Issue, Is.EqualTo(RecordAreaIssue.OffScreen));
        Assert.That(answer.Message?.Key, Is.EqualTo("Recorder.AreaOffScreen"));
    }

    [Test]
    public void AUsableArea_HasNoMessage()
    {
        var answer = _rig.Recorder.ResolveArea(RecordTargetKind.Region, null, new PixelRect(300, 200, 800, 450), [Monitor(0, 0, 0, true)], default);

        Assert.That(answer.IsUsable, Is.True);
        Assert.That(answer.Area, Is.EqualTo(new PixelRect(300, 200, 800, 450)));
        Assert.That(answer.Message, Is.Null);
    }

    [Test]
    public void TheBarNumbersMonitors_LeftToRight_ThenTopToBottom()
    {
        IReadOnlyList<MonitorInfo> monitors = [Monitor(0, 1920, 0, true), Monitor(1, -1920, 0), Monitor(2, 1920, -1080)];

        Assert.That(RecordArea.InBarOrder(monitors).Select(m => m.Index), Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void AKeptMonitorThatIsGone_BecomesTheMonitorUnderThePointer()
    {
        IReadOnlyList<MonitorInfo> monitors = [Monitor(0, 0, 0, true), Monitor(1, 1920, 0)];

        Assert.That(RecordArea.KnownMonitor(1, monitors), Is.EqualTo(1));
        Assert.That(RecordArea.KnownMonitor(4, monitors), Is.Null);
        Assert.That(RecordArea.KnownMonitor(null, monitors), Is.Null);
    }

    [Test]
    public void TenMinutesOf1920x1080At30Fps_WithSound_IsAbout300MB()
    {
        var bitsPerSecond = RecorderRules.VideoBitsPerSecond(new PixelSize(1920, 1080), 30) + (RecorderRules.SoundBytesPerSecond * 8L);
        var megabytes = bitsPerSecond / 8.0 * 600 / 1_000_000;

        Assert.That(megabytes, Is.InRange(250, 320), "SPEC recorder, Assumptions: at most about 300 MB");
    }

    [TestCase(16, 16, 15, 1_000_000L)]
    [TestCase(7680, 4320, 60, 40_000_000L)]
    public void TheBitrate_StaysBetween1And40Mbit(int width, int height, int fps, long expected)
    {
        Assert.That(RecorderRules.VideoBitsPerSecond(new PixelSize(width, height), fps), Is.EqualTo(expected));
    }
}
