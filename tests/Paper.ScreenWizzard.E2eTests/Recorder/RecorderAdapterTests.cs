using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.E2eTests.Support;
using Paper.ScreenWizzard.Infrastructure.Recorder;
using Paper.ScreenWizzard.Infrastructure.Shared;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Recorder.UseCases;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.E2eTests.Recorder;

/// <summary>
/// SPEC recorder on the real adapters of ADR 0004 (Desktop Duplication, WASAPI, Media Foundation): a few seconds of the primary monitor
/// recorded into a scratch folder and read back. Needs the interactive desktop; never touches the user's Videos folder.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class RecorderAdapterTests
{
    private ScratchFolder _scratch = null!;
    private MonotonicClock _clock = null!;
    private RecorderInteractor _recorder = null!;

    [SetUp]
    public void SetUp()
    {
        _scratch = new ScratchFolder();
        _clock = new MonotonicClock();
        _recorder = new RecorderInteractor(
            new DesktopDuplicationFrames(_clock),
            new WasapiSoundSources(_clock),
            new MediaFoundationVideoWriter(),
            _clock,
            new WindowPresence(),
            new WindowCatalog(),
            new FileLauncher(),
            new TaskDelay(),
            new SystemClock(),
            new FileStore(),
            new TestLog());
    }

    [TearDown]
    public void TearDown() => _scratch.Dispose();

    private RecorderSettings Settings(bool system = true, bool microphone = false) =>
        new(RecordTargetKind.Monitor, null, system, microphone, true, 0, 30, _scratch.Path);

    private PixelRect PrimaryArea()
    {
        var primary = new MonitorCatalog().GetMonitors().First(m => m.IsPrimary);
        return _recorder.ResolveArea(RecordTargetKind.Monitor, primary.Index, null, [primary], default).Area;
    }

    private async Task<RecordingResult> RecordAsync(RecorderSettings settings, TimeSpan length, Action? midway = null)
    {
        var start = await _recorder.StartAsync(new RecordRequest(PrimaryArea(), null, settings), null, CancellationToken.None);
        Assert.That(start.Started, Is.True, $"recording did not start: {start.Issue} {start.Message?.Key} {string.Join(",", start.Message?.Arguments ?? [])}");
        await Task.Delay(length / 2);
        midway?.Invoke();
        await Task.Delay(length / 2);
        var result = await _recorder.StopAsync();
        Assert.That(result, Is.Not.Null);
        return result!;
    }

    [Test]
    public async Task ThreeSeconds_OfThePrimaryMonitor_IsAnMp4OfItsSize_AndLength()
    {
        var area = PrimaryArea();

        var result = await RecordAsync(Settings(system: false), TimeSpan.FromSeconds(3));

        Assert.That(result.Saved, Is.True, result.Message?.Key);
        Assert.That(Path.GetExtension(result.FilePath), Is.EqualTo(".mp4"));
        Assert.That(Directory.GetFiles(_scratch.Path, "*.part"), Is.Empty, "the .part name is gone once complete");
        var probe = Mp4Probe.Read(result.FilePath!);
        Assert.That((probe.Width, probe.Height), Is.EqualTo((area.Width, area.Height)));
        Assert.That(probe.Duration.TotalSeconds, Is.EqualTo(3).Within(0.4));
        Assert.That(probe.HasSound, Is.False, "no sound asked: no sound track");
        Assert.That(result.Bytes, Is.EqualTo(new FileInfo(result.FilePath!).Length));
    }

    [Test]
    public async Task SystemSound_GivesASoundTrack()
    {
        var result = await RecordAsync(Settings(system: true), TimeSpan.FromSeconds(2));

        Assert.That(Mp4Probe.Read(result.FilePath!).HasSound, Is.True);
    }

    [Test]
    public async Task APauseOfTwoSeconds_IsNotInTheVideo()
    {
        var start = await _recorder.StartAsync(new RecordRequest(PrimaryArea(), null, Settings(system: false)), null, CancellationToken.None);
        Assert.That(start.Started, Is.True);
        await Task.Delay(1500);
        _recorder.Pause();
        await Task.Delay(2000);
        _recorder.Resume();
        await Task.Delay(1500);
        var result = await _recorder.StopAsync();

        Assert.That(Mp4Probe.Read(result!.FilePath!).Duration.TotalSeconds, Is.EqualTo(3).Within(0.4));
    }

    [Test]
    public async Task F3_AFolderThatCannotBeWritten_LeavesNoPartFile()
    {
        var locked = Path.Combine(_scratch.Path, "locked");
        Directory.CreateDirectory(locked);
        var info = new DirectoryInfo(locked);
        var security = System.IO.FileSystemAclExtensions.GetAccessControl(info);
        var everyone = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null);
        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(everyone, System.Security.AccessControl.FileSystemRights.CreateFiles, System.Security.AccessControl.AccessControlType.Deny));
        System.IO.FileSystemAclExtensions.SetAccessControl(info, security);
        try
        {
            var start = await _recorder.StartAsync(new RecordRequest(PrimaryArea(), null, Settings(system: false) with { VideoFolder = locked }), null, CancellationToken.None);

            Assert.That(start.Issue, Is.EqualTo(RecordStartIssue.FolderNotWritable));
            Assert.That(Directory.GetFiles(locked), Is.Empty);
        }
        finally
        {
            security.RemoveAccessRule(new System.Security.AccessControl.FileSystemAccessRule(everyone, System.Security.AccessControl.FileSystemRights.CreateFiles, System.Security.AccessControl.AccessControlType.Deny));
            System.IO.FileSystemAclExtensions.SetAccessControl(info, security);
        }
    }

    private sealed class TestLog : ILog
    {
        public void Info(string message) => TestContext.Progress.WriteLine("I " + message);

        public void Warning(string message) => TestContext.Progress.WriteLine("W " + message);

        public void Error(string message, Exception? exception) => TestContext.Progress.WriteLine("E " + message + " " + exception);
    }
}
