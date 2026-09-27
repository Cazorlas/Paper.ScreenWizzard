using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.UnitTests.Recorder.Fakes;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UnitTests.Recorder;

/// <summary>SPEC recorder: pause and stop, sound, the file, and F1 to F7, on fakes of the screen, the sound and the MP4 writer.</summary>
[TestFixture]
public sealed class RecorderSpecTests
{
    private const int Fps = 30;

    private RecorderRig _rig = null!;

    [SetUp]
    public void SetUp() => _rig = new RecorderRig();

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    // ---- Pause and stop ----

    [Test]
    public async Task Thirty_Paused20_Thirty_IsA60SecondVideo_WithNoFrozenPart()
    {
        _rig.Frames.Frames(S(0), S(30), Fps, id: 1);
        _rig.Frames.At(S(30.01), _rig.Recorder.Pause);
        _rig.Frames.Frames(S(30.02), S(50), Fps, id: 9);
        _rig.Frames.At(S(50), _rig.Recorder.Resume);
        _rig.Frames.Frames(S(50.01), S(80), Fps, id: 2);

        var (_, result) = await _rig.RunAsync();

        Assert.That(result!.Duration.TotalSeconds, Is.EqualTo(60).Within(0.1));
        Assert.That(_rig.Writer.VideoEnd.TotalSeconds, Is.EqualTo(60).Within(0.1));
        Assert.That(_rig.Writer.Pictures.Select(p => p.Image.Bgra[0]), Has.None.EqualTo(9), "nothing taken while paused is in the video");
    }

    [Test]
    public async Task StopWhilePaused_EndsTheVideoAtThePause()
    {
        _rig.Frames.Frames(S(0), S(10), Fps);
        _rig.Frames.At(S(10.01), _rig.Recorder.Pause);
        _rig.Frames.Frames(S(10.02), S(15), Fps);

        var (_, result) = await _rig.RunAsync();

        Assert.That(result!.End, Is.EqualTo(RecordingEnd.Stopped));
        Assert.That(result.Duration.TotalSeconds, Is.EqualTo(10).Within(0.1));
        Assert.That(result.Saved, Is.True);
    }

    [Test]
    public async Task TheStartStopHotkeyDuringTheCountdown_RecordsNothing_AndMakesNoFile()
    {
        _rig.Countdown.Hold = true;
        _rig.Files.Directories.Clear();
        var counting = new List<int>();

        var start = _rig.Recorder.StartAsync(RecorderRig.Request(RecorderRig.Settings(countdown: 3)), new SyncProgress(counting.Add), CancellationToken.None);
        await _rig.Countdown.Waiting.Task;
        var stopped = await _rig.Recorder.StopAsync();
        var result = await start;

        Assert.That(counting, Is.EqualTo(new[] { 3 }), "the countdown had begun");
        Assert.That(stopped, Is.Null);
        Assert.That(result.Issue, Is.EqualTo(RecordStartIssue.Cancelled));
        Assert.That(_rig.Writer.Opened, Is.Null, "no file");
        Assert.That(_rig.Frames.OpenedArea, Is.Null);
        Assert.That(_rig.Files.Directories, Is.Empty, "not even the folder");
        Assert.That(_rig.Recorder.State, Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public async Task TheCountdown_Counts3_2_1_ThenRecords()
    {
        var counting = new List<int>();
        _rig.Frames.Frames(S(0), S(1), Fps);

        var start = await _rig.Recorder.StartAsync(RecorderRig.Request(RecorderRig.Settings(countdown: 3)), new SyncProgress(counting.Add), CancellationToken.None);
        await _rig.Frames.ScriptDone.Task.WaitAsync(S(30));
        await _rig.Recorder.StopAsync();

        Assert.That(counting, Is.EqualTo(new[] { 3, 2, 1 }));
        Assert.That(start.Started, Is.True);
    }

    [Test]
    public async Task ExitingWhileRecording_StopsAndSavesFirst()
    {
        _rig.Frames.Frames(S(0), S(5), Fps);

        var (start, result) = await _rig.RunAsync();

        Assert.That(start.Started, Is.True);
        Assert.That(result!.Saved, Is.True);
        Assert.That(_rig.Writer.FinishedAs, Is.EqualTo(result.FilePath));
        Assert.That(_rig.Recorder.State, Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public async Task OnlyOneRecording_AtATime()
    {
        _rig.Frames.Frames(S(0), S(1), Fps);
        await _rig.Recorder.StartAsync(RecorderRig.Request(), null, CancellationToken.None);

        var second = await _rig.Recorder.StartAsync(RecorderRig.Request(), null, CancellationToken.None);
        await _rig.Recorder.StopAsync();

        Assert.That(second.Issue, Is.EqualTo(RecordStartIssue.Busy));
    }

    [Test]
    public async Task AScreenThatDoesNotChange_StillGivesEveryFrame()
    {
        // The screen sends one picture and then nothing new for two seconds: the video still has 30 frames a second.
        _rig.Frames.Frame(S(0));
        _rig.Frames.At(S(2), () => { });
        _rig.Frames.Frame(S(2.01), id: 2);

        await _rig.RunAsync();

        Assert.That(_rig.Writer.Pictures, Has.Count.EqualTo(61));
        Assert.That(_rig.Writer.Pictures.Take(60).Select(p => p.Image.Bgra[0]), Is.All.EqualTo(1), "the unchanged screen repeats");
    }

    // ---- Sound ----

    [Test]
    public async Task SystemSoundOnly_HasTheComputersSound()
    {
        _rig.Frames.Frames(S(0), S(1), Fps);

        await _rig.RunAsync();

        Assert.That(_rig.Sounds.Starts, Is.EqualTo(new[] { (true, false) }));
        Assert.That(_rig.Writer.Opened!.Value.WithSound, Is.True);
    }

    [Test]
    public async Task BothSounds_AreMixed_AtTheSameTimeAsThePicture()
    {
        var settings = RecorderRig.Settings(system: true, microphone: true);
        for (var at = 0.0; at < 2; at += 0.01)
        {
            var t = S(at);
            _rig.Frames.At(t, () =>
            {
                _rig.Sounds.Emit(SoundSource.System, t, 0.25f, S(0.01));
                _rig.Sounds.Emit(SoundSource.Microphone, t, 0.5f, S(0.01));
            });
            _rig.Frames.Frame(t);
        }

        await _rig.RunAsync(RecorderRig.Request(settings));

        var samples = _rig.Writer.Sounds.SelectMany(s => s.Samples).ToArray();
        Assert.That(samples.Take(48_000 * 2), Is.All.EqualTo(0.75f).Within(1e-5), "the first second, both sounds added");
    }

    [Test]
    public async Task NoSound_HasNoSoundTrack()
    {
        _rig.Frames.Frames(S(0), S(1), Fps);

        await _rig.RunAsync(RecorderRig.Request(RecorderRig.Settings(system: false, microphone: false)));

        Assert.That(_rig.Writer.Opened!.Value.WithSound, Is.False);
        Assert.That(_rig.Sounds.Starts, Is.Empty);
        Assert.That(_rig.Writer.SoundBlocks, Is.Empty);
    }

    [Test]
    public async Task AfterTenMinutes_SoundAndPictureDifferByLessThanATenthOfASecond()
    {
        // Pictures at 30 a second, the computer's sound in 10 ms blocks, as the real sources hand them.
        _rig.Writer.KeepSamples = false;
        var video = 0.0;
        for (var at = 0.0; at < 600; at += 0.01)
        {
            var t = S(at);
            _rig.Frames.At(t, () => _rig.Sounds.Emit(SoundSource.System, t, 0.1f, S(0.01)));
            if (at >= video)
            {
                _rig.Frames.Frame(t);
                video += 1.0 / Fps;
            }
        }

        await _rig.RunAsync();

        Assert.That(_rig.Writer.VideoEnd.TotalSeconds, Is.EqualTo(600).Within(0.1));
        Assert.That(Math.Abs((_rig.Writer.SoundEnd - _rig.Writer.VideoEnd).TotalSeconds), Is.LessThan(0.1));
    }

    // ---- The file ----

    [Test]
    public async Task TheFile_IsNamedFromTheStart_AndTakenNamesGetANumber()
    {
        _rig.Files.Files.Add(Path.Combine(RecorderRig.Folder, "Recording 2026-09-27 14.03.05.mp4"));
        _rig.Frames.Frames(S(0), S(1), Fps);

        var (start, result) = await _rig.RunAsync();

        var expected = Path.Combine(RecorderRig.Folder, "Recording 2026-09-27 14.03.05 (2).mp4");
        Assert.That(start.FilePath, Is.EqualTo(expected));
        Assert.That(result!.FilePath, Is.EqualTo(expected));
        Assert.That(_rig.Writer.Opened!.Value.Path, Is.EqualTo(expected + ".part"), "written under another name until it is complete");
        Assert.That(_rig.Writer.Opened!.Value.Size, Is.EqualTo(new PixelSize(1920, 1080)));
    }

    [Test]
    public async Task AMissingVideoFolder_IsCreated()
    {
        _rig.Files.Directories.Clear();
        _rig.Frames.Frames(S(0), S(1), Fps);

        await _rig.RunAsync();

        Assert.That(_rig.Files.Directories, Does.Contain(RecorderRig.Folder));
    }

    // ---- When it does not do the job ----

    [Test]
    public async Task F1_TheRecordedWindowClosed_StopsAndSaves()
    {
        _rig.Frames.Frames(S(0), S(2), Fps);
        _rig.Frames.At(S(2.01), () => _rig.Windows.Open = false);
        _rig.Frames.Frames(S(2.02), S(4), Fps);

        var (_, result) = await _rig.RunAsync(RecorderRig.Request(window: 0x1234));

        Assert.That(result!.End, Is.EqualTo(RecordingEnd.WindowClosed));
        Assert.That(result.Saved, Is.True, "what was recorded is saved");
        Assert.That(result.Message?.Key, Is.EqualTo("Recorder.WindowClosed"));
        Assert.That(result.Duration.TotalSeconds, Is.LessThan(3.2));
    }

    [Test]
    public async Task F2_TheMonitorChanged_StopsAndSaves()
    {
        _rig.Frames.Frames(S(0), S(2), Fps);
        _rig.Frames.DisplayChanges(S(2.5));
        _rig.Frames.Frames(S(2.6), S(4), Fps);

        var (_, result) = await _rig.RunAsync();

        Assert.That(result!.End, Is.EqualTo(RecordingEnd.DisplayChanged));
        Assert.That(result.Saved, Is.True);
        Assert.That(result.Message?.Key, Is.EqualTo("Recorder.DisplayChanged"));
        Assert.That(result.Duration.TotalSeconds, Is.EqualTo(2).Within(0.1));
    }

    [Test]
    public async Task F3_AFullDisk_StopsAndLeavesNothingHalfWritten()
    {
        _rig.Writer.FailPictureNumber = 40;
        _rig.Frames.Frames(S(0), S(5), Fps);

        var (_, result) = await _rig.RunAsync();

        Assert.That(result!.End, Is.EqualTo(RecordingEnd.WriteFailed));
        Assert.That(result.Saved, Is.False);
        Assert.That(_rig.Writer.Abandoned, Is.True, "the .part file is dropped");
        Assert.That(_rig.Writer.FinishedAs, Is.Null);
        Assert.That(result.Message?.Key, Is.EqualTo("Recorder.WriteFailed"));
        Assert.That(result.Message!.Arguments, Is.EqualTo(new[] { RecorderRig.Folder, "There is not enough space on the disk." }), "names the folder and the reason");
        Assert.That(_rig.Frames.Closed, Is.True);
    }

    [Test]
    public async Task F3_AFolderThatCannotBeMade_RecordsNothing()
    {
        _rig.Files.Directories.Clear();
        _rig.Files.CreateResult = PortResult.Fail("Access to the path is denied.");

        var (start, _) = await _rig.RunAsync();

        Assert.That(start.Issue, Is.EqualTo(RecordStartIssue.FolderNotWritable));
        Assert.That(start.Message?.Arguments, Is.EqualTo(new[] { RecorderRig.Folder, "Access to the path is denied." }));
        Assert.That(_rig.Writer.Opened, Is.Null);
        Assert.That(_rig.Recorder.State, Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public async Task F4_NoMicrophone_RecordsWithoutIt_AndSaysSo()
    {
        _rig.Sounds.Answer = new SoundStartResult(true, false, null, "No microphone was found.");
        _rig.Frames.Frames(S(0), S(1), Fps);

        var (start, result) = await _rig.RunAsync(RecorderRig.Request(RecorderRig.Settings(system: true, microphone: true)));

        Assert.That(start.Started, Is.True);
        Assert.That(start.Notices.Select(n => n.Key), Is.EqualTo(new[] { "Recorder.MicrophoneMissing" }));
        Assert.That(result!.Saved, Is.True);
    }

    [Test]
    public async Task F4_AMicrophoneUnpluggedWhileRecording_GoesOnWithoutIt()
    {
        var settings = RecorderRig.Settings(system: true, microphone: true);
        for (var i = 0; i < 300; i++)
        {
            var t = S(i * 0.01);
            var lost = i == 100;
            var plugged = i < 100;
            _rig.Frames.At(t, () =>
            {
                _rig.Sounds.Emit(SoundSource.System, t, 0.25f, S(0.01));
                if (plugged)
                {
                    _rig.Sounds.Emit(SoundSource.Microphone, t, 0.5f, S(0.01));
                }

                if (lost)
                {
                    _rig.Sounds.Lose(SoundSource.Microphone);
                }
            });
            _rig.Frames.Frame(t);
        }

        var (_, result) = await _rig.RunAsync(RecorderRig.Request(settings));

        Assert.That(_rig.Notices.Select(n => n.Key), Does.Contain("Recorder.MicrophoneLost"));
        Assert.That(result!.Saved, Is.True);
        Assert.That(_rig.Writer.SoundEnd.TotalSeconds, Is.EqualTo(_rig.Writer.VideoEnd.TotalSeconds).Within(0.1), "the computer's sound goes on to the end");
    }

    [Test]
    public async Task F5_NoSystemSound_RecordsWithoutIt_AndSaysSo()
    {
        _rig.Sounds.Answer = new SoundStartResult(false, false, "No playback device.", null);
        _rig.Frames.Frames(S(0), S(1), Fps);

        var (start, result) = await _rig.RunAsync();

        Assert.That(start.Notices.Select(n => n.Key), Is.EqualTo(new[] { "Recorder.SystemSoundMissing" }));
        Assert.That(_rig.Writer.Opened!.Value.WithSound, Is.False, "no sound at all: no sound track");
        Assert.That(result!.Saved, Is.True);
    }

    [Test]
    public async Task F6_TheScreenCannotBeCaptured_NothingIsRecorded()
    {
        _rig.Frames.OpenResult = PortResult.Fail("DXGI_ERROR_UNSUPPORTED");

        var (start, _) = await _rig.RunAsync();

        Assert.That(start.Issue, Is.EqualTo(RecordStartIssue.CannotRecord));
        Assert.That(start.Message?.Key, Is.EqualTo("Recorder.CannotRecord"));
        Assert.That(_rig.Writer.Opened, Is.Null);
        Assert.That(_rig.Recorder.State, Is.EqualTo(RecorderState.Idle));
    }

    [Test]
    public async Task F6_NoVideoEncoder_NothingIsRecorded()
    {
        _rig.Writer.OpenResult = new VideoWriterResult(VideoWriterIssue.Encoder, "No H.264 encoder.");

        var (start, _) = await _rig.RunAsync();

        Assert.That(start.Issue, Is.EqualTo(RecordStartIssue.CannotRecord));
        Assert.That(_rig.Frames.Closed, Is.True);
        Assert.That(_rig.Sounds.Stops, Is.EqualTo(1));
    }

    [Test]
    public async Task F7_ARegionTooSmall_IsRefused_BeforeAnythingStarts()
    {
        var request = RecorderRig.Request() with { Area = new PixelRect(0, 0, 14, 40) };

        var (start, _) = await _rig.RunAsync(request);

        Assert.That(start.Issue, Is.EqualTo(RecordStartIssue.AreaUnusable));
        Assert.That(start.Message?.Key, Is.EqualTo("Recorder.AreaTooSmall"));
        Assert.That(_rig.Frames.OpenedArea, Is.Null);
    }

    /// <summary>Reports on the calling thread, so the order of the countdown is the order of the list.</summary>
    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}

/// <summary>SPEC recorder, "What the user does" 2 and 5: picking a window, and the buttons of the "Recorded" window.</summary>
[TestFixture]
public sealed class RecorderWindowAndFileTests
{
    [Test]
    public void AClickOnTwoOverlappingWindows_PicksTheOneOnTop()
    {
        var rig = new RecorderRig();
        var back = new WindowInfo(1, "Back", new PixelRect(0, 0, 800, 600), true, false, false, false, 1);
        var front = new WindowInfo(2, "Front", new PixelRect(400, 300, 800, 600), true, false, false, false, 0);
        var hidden = new WindowInfo(3, "Hidden", new PixelRect(0, 0, 1920, 1080), true, true, false, false, 0);
        rig.Catalog.Windows.AddRange([back, front, hidden]);

        var windows = rig.Recorder.ListWindows();

        Assert.That(rig.Recorder.WindowAt(windows, new PixelPoint(500, 400)), Is.EqualTo(front));
        Assert.That(rig.Recorder.WindowAt(windows, new PixelPoint(100, 100)), Is.EqualTo(back), "a minimised window is never picked");
        Assert.That(rig.Recorder.WindowAt(windows, new PixelPoint(1500, 1000)), Is.Null);
    }

    [Test]
    public void OpenVideo_AndShowInFolder_AskWindows()
    {
        var rig = new RecorderRig();

        Assert.That(rig.Recorder.OpenVideo(@"C:\v.mp4"), Is.Null);
        Assert.That(rig.Recorder.ShowInFolder(@"C:\v.mp4"), Is.Null);
        Assert.That(rig.Launcher.Opened, Is.EqualTo(new[] { @"C:\v.mp4" }));
        Assert.That(rig.Launcher.Shown, Is.EqualTo(new[] { @"C:\v.mp4" }));
    }

    [Test]
    public void AVideoThatDoesNotOpen_IsSaid_WithItsPath()
    {
        var rig = new RecorderRig();
        rig.Launcher.Result = PortResult.Fail("No app is associated with .mp4.");

        var message = rig.Recorder.OpenVideo(@"C:\v.mp4");

        Assert.That(message?.Key, Is.EqualTo("Recorder.CannotOpen"));
        Assert.That(message!.Arguments, Is.EqualTo(new[] { @"C:\v.mp4", "No app is associated with .mp4." }));
    }
}
