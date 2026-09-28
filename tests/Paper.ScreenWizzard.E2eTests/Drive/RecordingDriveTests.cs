using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.E2eTests.Recorder;
using Paper.ScreenWizzard.E2eTests.Support;

namespace Paper.ScreenWizzard.E2eTests.Drive;

/// <summary>
/// Recorder plan T11 on the exe: the start/stop hotkey starts a recording and stops it, the bars step aside while it runs and come back after,
/// the tray tells the time, and "Thoát" during a recording still leaves the video. The recording keys are chords nobody uses
/// (Ctrl+Alt+Shift+F17, F18), the video folder is the run's scratch folder, sound is off and there is no countdown.
/// </summary>
[TestFixture]
public sealed class RecordingDriveTests : DriveBase
{
    private const int StartStopKey = 17;
    private const int PauseKey = 18;

    [Test]
    public void StartStopHotkey_RecordsTheScreen_BarsStepAside_TrayTellsTheTime_AndTheVideoLandsInTheVideoFolder()
    {
        NewRecordingApp();
        StartAndWaitForBar();

        PressRecordKey(StartStopKey);
        Assert.That(App.WaitWindowGone("CaptureBarWindow", 6), Is.True, "the capture bar steps aside while recording");
        Thread.Sleep(2500);
        var text = new TrayDriver(App).IconText();
        Assert.That(text, Does.StartWith("Đang quay 00:0"), "the tray icon's tooltip tells the time recorded");

        PressRecordKey(StartStopKey);
        var recorded = App.WaitWindow("RecordedWindow", 15);
        var video = VideoIn(VideoFolder);
        Assert.That(AppRun.TextOf(recorded, "RecordedFileName"), Is.EqualTo(Path.GetFileName(video)), "the Recorded window names the file");
        Assert.That(Path.GetFileName(video), Does.Match(@"^Recording \d{4}-\d{2}-\d{2} \d{2}\.\d{2}\.\d{2}\.mp4$"));
        var probe = Mp4Probe.Read(video);
        Assert.That(probe.Duration.TotalSeconds, Is.GreaterThan(1.5), "the video holds the seconds between the two presses");
        Assert.That((probe.Width % 2, probe.Height % 2), Is.EqualTo((0, 0)), "an even size");
        Assert.That(probe.HasSound, Is.False, "no sound track with both switches off");
        Assert.That(App.WaitWindow("CaptureBarWindow", 6), Is.Not.Null, "the capture bar comes back after the recording");
        AppRun.Invoke(recorded, "CloseRecordedButton");
    }

    [Test]
    public void PauseHotkey_TheTimeInThePauseIsNotInTheVideo()
    {
        NewRecordingApp();
        StartAndWaitForBar();

        PressRecordKey(StartStopKey);
        Thread.Sleep(2000);
        PressRecordKey(PauseKey);
        Thread.Sleep(300);
        Assert.That(new TrayDriver(App).IconText(), Does.StartWith("Tạm dừng quay"), "the tooltip says the recording is paused");
        Thread.Sleep(3000);
        PressRecordKey(PauseKey);
        Thread.Sleep(2000);
        PressRecordKey(StartStopKey);
        App.WaitWindow("RecordedWindow", 15);

        var seconds = Mp4Probe.Read(VideoIn(VideoFolder)).Duration.TotalSeconds;
        Assert.That(seconds, Is.InRange(3.0, 5.5), "about 4 seconds recorded; the 3 paused seconds are cut out");
    }

    [Test]
    public void ExitDuringARecording_StopsAndSavesFirst()
    {
        NewRecordingApp();
        StartAndWaitForBar();

        PressRecordKey(StartStopKey);
        Thread.Sleep(2500);
        var process = App.Process!;
        new TrayDriver(App).Choose("Thoát");
        Assert.That(process.WaitForExit(15_000), Is.True, "the exe ended after Thoát");

        var video = VideoIn(VideoFolder);
        Assert.That(Mp4Probe.Read(video).Duration.TotalSeconds, Is.GreaterThan(1.5), "the video recorded before Thoát was saved");
        Assert.That(Directory.GetFiles(VideoFolder, "*.part"), Is.Empty, "no half-written file is left");
    }

    private string VideoFolder => Path.Combine(App.ScratchPath, "videos");

    private static void PressRecordKey(int number) => KeySender.Press(AppRun.Chorded, KeySender.FunctionKey(number));

    private static string VideoIn(string folder)
    {
        var end = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < end)
        {
            var videos = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp4") : Array.Empty<string>();
            if (videos.Length == 1)
            {
                return videos[0];
            }

            Assert.That(videos, Has.Length.LessThanOrEqualTo(1), "one recording gives one video");
            Thread.Sleep(200);
        }

        Assert.Fail("no video appeared in " + folder);
        return string.Empty;
    }

    private void NewRecordingApp() => NewApp(settings => settings with
    {
        RecordHotkeys = new RecordingHotkeys(new HotkeyChord(AppRun.Chorded, "F" + StartStopKey), new HotkeyChord(AppRun.Chorded, "F" + PauseKey)),
        Recorder = RecorderRules.Defaults(Path.GetTempPath()) with
        {
            Target = RecordTargetKind.Monitor,
            MonitorIndex = null,
            SystemSound = false,
            Microphone = false,
            Pointer = false,
            CountdownSeconds = 0,
            VideoFolder = Path.GetFullPath(Path.Combine(settings.SaveFolder, "..", "videos")),
        },
    });
}
