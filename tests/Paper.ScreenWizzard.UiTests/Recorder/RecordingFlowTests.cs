using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.UiTests.ScreenCapture;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.Recorder;

/// <summary>
/// The flow that ties one recording together (SPEC recorder, "What the user does" 1 to 5, F7), on fakes for the use case and the windows.
/// It runs on the WPF dispatcher thread, as in the app, because the use case's events are moved back to the thread that made the flow.
/// </summary>
[TestFixture]
public sealed class RecordingFlowTests : UiTestBase
{
    private sealed class Rig
    {
        public Rig()
        {
            Flow = WpfHost.Instance.Invoke(() => new RecordingFlow(Interactor, Views, Notes, new FakeLocalizer(), new FakeMonitors()));
            Flow.Began += (_, _) => Events.Add("began");
            Flow.Ended += (_, _) => Events.Add("ended");
            Flow.AreaRefused += message => Refusals.Add(message);
        }

        public FakeRecorderInteractor Interactor { get; } = new();

        public FakeRecorderViews Views { get; } = new();

        public RecordingNotifications Notes { get; } = new();

        public RecordingFlow Flow { get; }

        public List<string> Events { get; } = [];

        public List<NotificationMessage> Refusals { get; } = [];

        public static RecorderSettings Region => RecorderRules.Defaults(@"C:\Users\An\Videos") with { Target = RecordTargetKind.Region };

        public void Run(Func<RecordingFlow, Task> step) =>
            WpfHost.Instance.Dispatcher.InvokeAsync(() => step(Flow)).Task.Unwrap().GetAwaiter().GetResult();
    }

    [Test]
    public void ARegion_IsOutlined_AndRecorded_WhileTheBarsStepAside()
    {
        var rig = new Rig();

        rig.Run(flow => flow.StartAsync(Rig.Region));

        Assert.That(rig.Views.RegionPicks, Is.EqualTo(1));
        Assert.That(rig.Interactor.Started.Only("start").Area, Is.EqualTo(new PixelRect(300, 200, 800, 450)));
        Assert.That(rig.Views.Outlines.Only("outline").Area, Is.EqualTo(new PixelRect(300, 200, 800, 450)));
        Assert.That(rig.Events, Is.EqualTo(new[] { "began" }), "the bars stay hidden while recording");
        Assert.That(rig.Flow.MayOpenBar, Is.False, "the recording bar does not open while recording");
    }

    [Test]
    public void F7_ARefusedArea_SaysTheUseCasesMessage_AndRecordsNothing()
    {
        var rig = new Rig();
        var tooSmall = NotificationMessage.Of("Recorder.AreaTooSmall");
        rig.Interactor.AreaAnswer = new RecordAreaAnswer(new PixelRect(0, 0, 14, 14), RecordAreaIssue.TooSmall, tooSmall);

        rig.Run(flow => flow.StartAsync(Rig.Region));

        Assert.That(rig.Refusals, Is.EqualTo(new[] { tooSmall }));
        Assert.That(rig.Interactor.Started, Is.Empty);
        Assert.That(rig.Views.Outlines, Is.Empty);
        Assert.That(rig.Events, Is.EqualTo(new[] { "began", "ended" }), "the bars come back to say why");
    }

    [Test]
    public void APickCancelledWithEsc_RecordsNothing_AndTheBarsComeBack()
    {
        var rig = new Rig();
        rig.Views.RegionAnswer = null;

        rig.Run(flow => flow.StartAsync(Rig.Region));

        Assert.That(rig.Interactor.Started, Is.Empty);
        Assert.That(rig.Events, Is.EqualTo(new[] { "began", "ended" }));
    }

    [Test]
    public void TheStartStopHotkey_WhileRecording_Stops_AndStartsNoOther()
    {
        var rig = new Rig();
        rig.Run(flow => flow.StartAsync(Rig.Region));

        rig.Run(flow => flow.StartOrStopAsync(Rig.Region));

        Assert.That(rig.Interactor.Stops, Is.EqualTo(1));
        Assert.That(rig.Interactor.Started, Has.Count.EqualTo(1), "no second recording");
    }

    [Test]
    public void TheStartStopHotkey_WithSettingsOpen_StartsNothing()
    {
        var rig = new Rig();

        rig.Run(flow => flow.StartOrStopAsync(Rig.Region, mayStart: false));

        Assert.That(rig.Views.RegionPicks, Is.Zero);
        Assert.That(rig.Interactor.Started, Is.Empty);
    }

    [Test]
    public void ThePauseHotkey_AsksTheUseCase()
    {
        var rig = new Rig();
        var changes = 0;
        rig.Flow.StateChanged += (_, _) => changes++;

        WpfHost.Instance.Invoke(rig.Flow.TogglePause);

        Assert.That(rig.Interactor.Toggles, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1), "the tray is told");
    }

    [Test]
    public void ASavedRecording_OpensTheRecordedWindow_ClosesTheOutline_AndTheBarsComeBack()
    {
        var rig = new Rig();
        rig.Run(flow => flow.StartAsync(Rig.Region));

        rig.Interactor.State = RecorderState.Idle;
        rig.Interactor.Finish(new RecordingResult(RecordingEnd.Stopped, @"C:\Users\An\Videos\Recording 2026-09-27 14.03.05.mp4", new PixelSize(800, 450), TimeSpan.FromSeconds(83), null, 42 * 1024 * 1024));
        WpfHost.Instance.Settle();

        Assert.That(rig.Views.Recorded.Only("Recorded window").FileName, Is.EqualTo("Recording 2026-09-27 14.03.05.mp4"));
        Assert.That(rig.Views.Outlines.Only("outline").IsClosed, Is.True);
        Assert.That(rig.Events, Is.EqualTo(new[] { "began", "ended" }));
        Assert.That(rig.Flow.MayOpenBar, Is.True);
    }

    [Test]
    public void F3_NothingSaved_ShowsTheReason_AndNoRecordedWindow()
    {
        var rig = new Rig();
        rig.Run(flow => flow.StartAsync(Rig.Region));
        var why = NotificationMessage.Of("Recorder.WriteFailed", @"C:\Users\An\Videos", "disk full");

        rig.Interactor.State = RecorderState.Idle;
        rig.Interactor.Finish(new RecordingResult(RecordingEnd.WriteFailed, null, new PixelSize(800, 450), TimeSpan.FromSeconds(3), why));
        WpfHost.Instance.Settle();

        Assert.That(rig.Views.Recorded, Is.Empty);
        Assert.That(rig.Notes.Errors, Is.EqualTo(new[] { why }));
    }
}
