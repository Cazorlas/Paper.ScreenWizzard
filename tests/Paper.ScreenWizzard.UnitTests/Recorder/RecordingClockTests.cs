using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;

namespace Paper.ScreenWizzard.UnitTests.Recorder;

/// <summary>SPEC recorder, "Pause and stop" and "Sound": the video's clock, its frame slots, the sound mix and the file name.</summary>
[TestFixture]
public sealed class RecordingClockTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Test]
    public void APauseOf20Seconds_IsCutOut_30Plus30Is60()
    {
        var timeline = new RecordingTimeline();
        timeline.Start(S(100));
        timeline.Pause(S(130));
        timeline.Resume(S(150));

        Assert.That(timeline.MediaTime(S(129)), Is.EqualTo(S(29)));
        Assert.That(timeline.MediaTime(S(140)), Is.Null, "taken while paused: not in the video");
        Assert.That(timeline.MediaTime(S(150)), Is.EqualTo(S(30)), "goes on from the moment of pausing, no gap");
        Assert.That(timeline.MediaTime(S(180)), Is.EqualTo(S(60)));
        Assert.That(timeline.Elapsed(S(180)), Is.EqualTo(S(60)));
    }

    [Test]
    public void WhilePaused_TheElapsedTimeStands_AndNothingHasAVideoTime()
    {
        var timeline = new RecordingTimeline();
        timeline.Start(S(0));
        timeline.Pause(S(30));

        Assert.That(timeline.Elapsed(S(45)), Is.EqualTo(S(30)));
        Assert.That(timeline.MediaTime(S(40)), Is.Null);
        Assert.That(timeline.MediaTime(S(-1)), Is.Null, "before the start");
    }

    [Test]
    public void TheFrameClock_FillsAGap_AndDropsASecondPictureForTheSameFrame()
    {
        var clock = new FrameClock(30);

        Assert.That(clock.Place(S(0)), Is.EqualTo(new FrameSlots(0, 1)));
        Assert.That(clock.Place(S(0.01)), Is.EqualTo(new FrameSlots(1, 0)), "same frame: dropped");
        Assert.That(clock.Place(S(0.1)), Is.EqualTo(new FrameSlots(1, 3)), "two frames late: slots 1 and 2 repeat, 3 is the picture");
        Assert.That(clock.End, Is.EqualTo(TimeSpan.FromTicks(4 * TimeSpan.TicksPerSecond / 30)));
    }

    [Test]
    public void TwoSounds_AreAddedTogether_AndClampedToFullScale()
    {
        var mixer = new AudioMixer([SoundSource.System, SoundSource.Microphone]);
        mixer.Push(SoundSource.System, S(0), Constant(0.25f, 480));
        mixer.Push(SoundSource.Microphone, S(0), Constant(0.5f, 480));

        var mixed = mixer.Drain(S(0.01));

        Assert.That(mixed, Has.Length.EqualTo(480 * 2));
        Assert.That(mixed, Is.All.EqualTo(0.75f).Within(1e-6));

        mixer.Push(SoundSource.System, S(0.01), Constant(0.8f, 480));
        mixer.Push(SoundSource.Microphone, S(0.01), Constant(0.8f, 480));
        Assert.That(mixer.Drain(S(0.02)), Is.All.EqualTo(1f), "clamped, not wrapped");
    }

    [Test]
    public void AMixer_WaitsForTheSlowerSource_ButNotMoreThan200ms()
    {
        var mixer = new AudioMixer([SoundSource.System, SoundSource.Microphone]);
        mixer.Push(SoundSource.Microphone, S(0), Constant(0.5f, 48_000));

        Assert.That(mixer.Drain(S(0.1)), Is.Empty, "the computer's sound may still come for this time");
        var later = mixer.Drain(S(1));

        Assert.That(later, Has.Length.EqualTo((int)(0.8 * 48_000) * 2), "a silent source counts as silence 200 ms behind the video");
        Assert.That(later, Is.All.EqualTo(0.5f));
    }

    [Test]
    public void AtTheEnd_TheSoundIsPaddedWithSilence_ToTheLastPicture()
    {
        var mixer = new AudioMixer([SoundSource.System]);
        mixer.Push(SoundSource.System, S(0), Constant(0.5f, 24_000));
        mixer.Drain(S(0.5));

        var rest = mixer.Flush(S(2));

        Assert.That(mixer.WrittenTime, Is.EqualTo(S(2)));
        Assert.That(rest, Has.Length.EqualTo(1.5 * 48_000 * 2));
        Assert.That(rest, Is.All.EqualTo(0f));
    }

    [Test]
    public void TheFile_IsNamedFromTheStart_AndNeverOverwritesOne()
    {
        var started = new DateTime(2026, 9, 27, 14, 3, 5);

        Assert.That(RecordingFileName.For(started, _ => false), Is.EqualTo("Recording 2026-09-27 14.03.05.mp4"));
        Assert.That(
            RecordingFileName.For(started, name => name == "Recording 2026-09-27 14.03.05.mp4"),
            Is.EqualTo("Recording 2026-09-27 14.03.05 (2).mp4"));
        Assert.That(
            RecordingFileName.For(started, name => name is "Recording 2026-09-27 14.03.05.mp4.part" or "Recording 2026-09-27 14.03.05 (2).mp4"),
            Is.EqualTo("Recording 2026-09-27 14.03.05 (3).mp4"),
            "a recording still being written holds its name too");
    }

    [Test]
    public void TheLimits_AreTheChoicesOfTheSpec()
    {
        Assert.That(RecorderRules.FrameRates, Is.EqualTo(new[] { 15, 30, 60 }));
        Assert.That(RecorderRules.Countdowns, Is.EqualTo(new[] { 0, 3, 5 }));
        var defaults = RecorderRules.Defaults(@"C:\Users\An\Videos");
        Assert.That(defaults, Is.EqualTo(new RecorderSettings(RecordTargetKind.Monitor, null, true, false, true, 3, 30, @"C:\Users\An\Videos\Paper.ScreenWizzard")));
    }

    private static float[] Constant(float value, int frames)
    {
        var samples = new float[frames * 2];
        Array.Fill(samples, value);
        return samples;
    }
}
