using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using NUnit.Framework;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Presentation.Views.Capture;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UiTests.Support;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// The coordinator that ties countdown, overlay, dialog and delivery together (plan T9/T10), on fakes for the use case and for
/// the windows; the last two tests use the real windows over a fake session. The flow runs on the WPF dispatcher thread, as it
/// does in the app, because its progress callbacks post back to the thread that started it.
/// </summary>
[TestFixture]
public sealed class CaptureFlowTests : UiTestBase
{
    private sealed class Rig
    {
        public Rig(ICaptureViews? views = null)
        {
            Views = views ?? Fake;
            Flow = WpfHost.Instance.Invoke(() => new CaptureFlow(
                Interactor,
                Views,
                Notes,
                Dialogs,
                new FakeLocalizer(),
                () => CaptureSettings.Default()));
            Flow.EditRequested += image => Edited.Add(image);
        }

        public FakeCaptureInteractor Interactor { get; } = new();

        public FakeCaptureViews Fake { get; } = new();

        public ICaptureViews Views { get; }

        public RecordingNotifications Notes { get; } = new();

        public FakeFileDialogs Dialogs { get; } = new();

        public CaptureFlow Flow { get; }

        public List<PixelImage> Edited { get; } = [];

        /// <summary>Starts a capture and waits until the flow has done what it does before the user chooses.</summary>
        public void Start(CaptureKind kind) =>
            WpfHost.Instance.Dispatcher.InvokeAsync(() => Flow.StartAsync(kind)).Task.Unwrap().GetAwaiter().GetResult();

        /// <summary>Starts a capture without waiting for it (its begin is still pending), and waits until the use case was asked.</summary>
        public void StartWithoutWaiting(CaptureKind kind, int expectedBegins)
        {
            _ = WpfHost.Instance.Dispatcher.InvokeAsync(() => Flow.StartAsync(kind));
            Assert.That(
                Retry.WhileFalse(() => Interactor.Begun.Count >= expectedBegins, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(20)).Success,
                Is.True,
                "the flow never asked the use case to begin");
            WpfHost.Instance.Settle();
        }

        public void OnUi(Action action) => WpfHost.Instance.Invoke(action);

        /// <summary>The overlay the flow opened as number <paramref name="index"/>; a missing one is a failed assertion.</summary>
        public FakeHandle<SelectionOverlayViewModel> Overlay(int index)
        {
            Assert.That(Fake.Selections, Has.Count.GreaterThan(index), "the flow opened no such selection overlay");
            return Fake.Selections[index];
        }

        public FakeHandle<CaptureDoneViewModel> Dialog(int index)
        {
            Assert.That(Fake.Dones, Has.Count.GreaterThan(index), "the flow opened no such \"Đã chụp\" dialog");
            return Fake.Dones[index];
        }
    }

    private static FakeCaptureSession NewSession(CaptureKind kind) => new(kind);

    private static void DragRectangle(Rig rig, SelectionOverlayViewModel viewModel, PixelPoint from, PixelPoint to) =>
        rig.OnUi(() =>
        {
            viewModel.PointerDown(from);
            viewModel.PointerMoved(to);
            viewModel.PointerUp(to);
        });

    [Test]
    public void Rectangle_BeginThenOverlayThenTheDialogWithTheCapturedImage()
    {
        var rig = new Rig();
        var session = NewSession(CaptureKind.Rectangle);
        rig.Interactor.SessionToReturn = session;

        rig.Start(CaptureKind.Rectangle);

        Assert.That(rig.Interactor.Begun.Only("begin").Kind, Is.EqualTo(CaptureKind.Rectangle));
        Assert.That(rig.Fake.Selections, Has.Count.EqualTo(1), "the overlay opens on the frozen snapshot");
        Assert.That(rig.Overlay(0).ViewModel.Session, Is.SameAs(session));
        Assert.That(rig.Fake.Dones, Is.Empty, "nothing is asked until the user has chosen");

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(rig.Overlay(0).IsClosed, Is.True, "the overlay is gone once the region is chosen");
        Assert.That(rig.Fake.Dones, Has.Count.EqualTo(1), "the default after-capture plan is the dialog");
        var dialog = rig.Dialog(0).ViewModel;
        Assert.That(dialog.Image.Width, Is.EqualTo(300));
        Assert.That(dialog.Image.Height, Is.EqualTo(200));
        Assert.That(rig.Interactor.Deliveries, Is.Empty, "the dialog delivers nothing by itself (SPEC: chưa ghi gì)");
        Assert.That(rig.Notes.Errors, Is.Empty);
    }

    [Test]
    public void Dialog_OpensCenteredOnTheMonitorThatHoldsTheCapture()
    {
        var rig = new Rig();
        var secondMonitor = new PixelRect(1920, 0, 1280, 800);
        var snapshot = CaptureTestData.Snapshot() with
        {
            VirtualScreen = new PixelRect(0, 0, 3200, 1080),
            Monitors = [new MonitorInfo(0, new PixelRect(0, 0, 1920, 1080), true, 96), new MonitorInfo(1, secondMonitor, false, 96)],
        };
        var session = new FakeCaptureSession(CaptureKind.Rectangle, snapshot);
        session.Answers.Enqueue(CaptureTestData.Captured(new PixelRect(2000, 100, 300, 200)));
        rig.Interactor.SessionToReturn = session;
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(2000, 100), new PixelPoint(2300, 300));

        Assert.That(rig.Fake.DoneAreas.Only("dialog area"), Is.EqualTo(secondMonitor), "the dialog goes to the monitor the capture was on");
    }

    [Test]
    public void Countdown_ShowsEachSecondThenGoesAwayBeforeTheOverlayComes()
    {
        var rig = new Rig();
        rig.Interactor.CountdownToReport = [5, 4, 3, 2, 1];
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);

        rig.Start(CaptureKind.Rectangle);
        WpfHost.Instance.Settle();

        Assert.That(rig.Fake.Countdowns, Has.Count.EqualTo(1), "one countdown window for the whole delay");
        Assert.That(rig.Fake.CountdownValues, Is.EqualTo(new[] { 5, 4, 3, 2, 1 }), "SPEC: số đếm ngược hiện 5, 4, 3, 2, 1");
        Assert.That(rig.Fake.Countdowns[0].IsClosed, Is.True, "the number is gone before the frozen picture appears");
        Assert.That(rig.Fake.Selections, Has.Count.EqualTo(1));
    }

    [Test]
    public void Countdown_AskingToCancelItStopsTheRunClosesTheNumberAndNothingIsCaptured()
    {
        // SPEC capture, "Cách dùng" step 5: cancelling at any step captures nothing and the screen returns to normal.
        var rig = new Rig();
        rig.Interactor.CountdownToReport = [5];
        var gate = new TaskCompletionSource<CaptureBeginResult>();
        rig.Interactor.BeginGate = gate;
        rig.StartWithoutWaiting(CaptureKind.Rectangle, expectedBegins: 1);
        WpfHost.Instance.Settle();
        Assert.That(rig.Fake.Countdowns, Has.Count.EqualTo(1), "the number is up");

        rig.Fake.Countdowns[0].ViewModel.Cancel();

        Assert.That(rig.Interactor.LastToken.IsCancellationRequested, Is.True, "the use case is told to stop waiting");
        Assert.That(rig.Fake.Countdowns[0].IsClosed, Is.True, "the number is gone at once");
        gate.SetCanceled();
        WpfHost.Instance.Settle();
        Assert.That(rig.Fake.Selections, Is.Empty, "no overlay comes after a cancelled countdown");
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
    }

    [Test]
    public void Countdown_WithNoDelay_OpensNoCountdownWindow()
    {
        var rig = new Rig();
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);

        rig.Start(CaptureKind.Rectangle);

        Assert.That(rig.Overlay(0).IsClosed, Is.False, "the capture itself went ahead");
        Assert.That(rig.Fake.Countdowns, Is.Empty, "a capture with no delay must not flash a window");
    }

    [Test]
    public void FullScreen_NeedsNoOverlay_TheFlowCompletesAtOnce()
    {
        var rig = new Rig();
        var session = NewSession(CaptureKind.FullScreen);
        rig.Interactor.SessionToReturn = session;

        rig.Start(CaptureKind.FullScreen);

        Assert.That(session.FullScreenCompletions, Is.EqualTo(1));
        Assert.That(rig.Fake.Selections, Is.Empty, "nothing to choose, so no overlay");
        Assert.That(rig.Fake.Dones, Has.Count.EqualTo(1));
        Assert.That(rig.Dialog(0).ViewModel.Image.Width, Is.EqualTo(CaptureTestData.SmallScreen.Width));
    }

    [Test]
    public void FullScreen_TheUseCaseRefuses_ShowsTheErrorAndNoDialog()
    {
        var rig = new Rig();
        var session = NewSession(CaptureKind.FullScreen);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.OutOfMemory, NotificationMessage.Of("Capture.ImageTooLarge")));
        rig.Interactor.SessionToReturn = session;

        rig.Start(CaptureKind.FullScreen);

        Assert.That(rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.ImageTooLarge" }));
        Assert.That(rig.Fake.Dones, Is.Empty);
    }

    [Test]
    public void Plan_NoDialog_DeliversToEachDestinationInOrderAndNeverOpensTheDialog()
    {
        var rig = new Rig();
        rig.Interactor.Plan = new AfterCapturePlan(false, [CaptureDestination.Clipboard, CaptureDestination.File]);
        var copied = NotificationMessage.Of("Capture.CopiedToClipboard", "300", "200");
        var saved = NotificationMessage.Of("Capture.SavedToFile", @"C:\Pics\a.png", "300", "200");
        rig.Interactor.DeliverAnswer = d => d == CaptureDestination.Clipboard
            ? new CaptureDeliveryResult(true, false, null, copied)
            : new CaptureDeliveryResult(true, false, @"C:\Pics\a.png", saved);
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(rig.Fake.Dones, Is.Empty, "the user asked for no dialog");
        Assert.That(rig.Interactor.Deliveries.Select(d => d.Destination), Is.EqualTo(new[] { CaptureDestination.Clipboard, CaptureDestination.File }));
        Assert.That(rig.Notes.Toasts, Is.EqualTo(new[] { copied, saved }));
        Assert.That(rig.Edited, Is.Empty);
    }

    [Test]
    public void Plan_GoStraightToTheEditor_RaisesEditRequestedWithTheImageAndDeliversNothing()
    {
        var rig = new Rig();
        rig.Interactor.Plan = new AfterCapturePlan(false, [CaptureDestination.Editor]);
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(rig.Edited.Only("edited image").Width, Is.EqualTo(300));
        Assert.That(rig.Interactor.Deliveries, Is.Empty, "the editor is not a delivery of the use case");
        Assert.That(rig.Fake.Dones, Is.Empty);
    }

    [Test]
    public void Plan_ClipboardAndEditor_KeepsTheOrderTheUseCaseGave()
    {
        var rig = new Rig();
        rig.Interactor.Plan = new AfterCapturePlan(false, [CaptureDestination.Editor, CaptureDestination.Clipboard]);
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        var order = new List<string>();
        rig.Flow.EditRequested += _ => order.Add("editor");
        rig.Interactor.DeliverAnswer = d =>
        {
            order.Add(d.ToString());
            return new CaptureDeliveryResult(true, false, null, null);
        };
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(order, Is.EqualTo(new[] { "editor", "Clipboard" }));
    }

    [Test]
    public void Plan_NoDialogAndTheDeliveryFails_TheUserIsToldWithAnErrorBox()
    {
        var rig = new Rig();
        rig.Interactor.Plan = new AfterCapturePlan(false, [CaptureDestination.File]);
        rig.Interactor.DeliverAnswer = _ => new CaptureDeliveryResult(false, true, null, NotificationMessage.Of("Capture.SaveFailed", @"D:\ReadOnly", "access denied"));
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.SaveFailed" }), "no dialog to hold the error, so it must not vanish");
        Assert.That(rig.Notes.Toasts, Is.Empty);
    }

    [Test]
    public void Dialog_Edit_IsForwardedByTheFlowAndTheDialogCloses()
    {
        var rig = new Rig();
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);
        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        rig.OnUi(() => rig.Dialog(0).ViewModel.EditCommand.Execute(null));

        Assert.That(rig.Edited, Has.Count.EqualTo(1));
        Assert.That(rig.Edited[0], Is.SameAs(rig.Dialog(0).ViewModel.Image));
        Assert.That(rig.Dialog(0).IsClosed, Is.True);
        Assert.That(rig.Flow.OpenDialogCount, Is.Zero);
    }

    [Test]
    public void Dialog_Discard_ClosesItAndDeliversNothing()
    {
        var rig = new Rig();
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);
        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        rig.OnUi(() => rig.Dialog(0).ViewModel.DiscardCommand.Execute(null));

        Assert.That(rig.Dialog(0).IsClosed, Is.True);
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Edited, Is.Empty);
        Assert.That(rig.Notes.Toasts, Is.Empty);
    }

    [Test]
    public void Dialogs_SeveralCaptures_EachOpensItsOwnDialogWithItsOwnImageAndPlace()
    {
        var rig = new Rig();
        for (var i = 0; i < 2; i++)
        {
            rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
            rig.Start(CaptureKind.Rectangle);
            var to = i == 0 ? new PixelPoint(550, 450) : new PixelPoint(650, 550);
            DragRectangle(rig, rig.Overlay(i).ViewModel, new PixelPoint(250, 250), to);
        }

        Assert.That(rig.Fake.Dones, Has.Count.EqualTo(2));
        Assert.That(rig.Flow.OpenDialogCount, Is.EqualTo(2), "the first dialog stays where it is (SPEC: mỗi hộp thoại giữ ảnh của riêng nó)");
        Assert.That(rig.Dialog(0).ViewModel.Image.Width, Is.EqualTo(300));
        Assert.That(rig.Dialog(1).ViewModel.Image.Width, Is.EqualTo(400));
        Assert.That(rig.Dialog(0).IsClosed, Is.False);
        Assert.That(rig.Fake.DoneAreas[1], Is.Not.EqualTo(rig.Fake.DoneAreas[0]), "the second dialog is offset so the first can still be seen");
    }

    [Test]
    public void Cancel_TheUserEscapes_NoDialogNoDeliveryNoError()
    {
        var rig = new Rig();
        var session = NewSession(CaptureKind.Rectangle);
        rig.Interactor.SessionToReturn = session;
        rig.Start(CaptureKind.Rectangle);

        rig.OnUi(() => rig.Overlay(0).ViewModel.CancelCommand.Execute(null));

        Assert.That(rig.Overlay(0).IsClosed, Is.True);
        Assert.That(rig.Fake.Dones, Is.Empty);
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Notes.Errors, Is.Empty);
        Assert.That(session.CancelCount, Is.EqualTo(1));
    }

    [Test]
    public void F6_TheDisplayChanged_ShowsOneErrorAndNoDialog()
    {
        var rig = new Rig();
        var session = NewSession(CaptureKind.Rectangle);
        session.DisplayAnswer = CaptureIssue.DisplayChanged;
        rig.Interactor.SessionToReturn = session;
        rig.Start(CaptureKind.Rectangle);

        DragRectangle(rig, rig.Overlay(0).ViewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.DisplayChanged" }), "said once, not once by the overlay and again by the flow");
        Assert.That(rig.Overlay(0).IsClosed, Is.True);
        Assert.That(rig.Fake.Dones, Is.Empty);
    }

    [Test]
    public void F9_TheSnapshotCouldNotBeTaken_ShowsTheErrorAndOpensNothing()
    {
        var rig = new Rig();
        rig.Interactor.BeginIssue = CaptureIssue.OutOfMemory;

        rig.Start(CaptureKind.Rectangle);

        Assert.That(rig.Notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.ImageTooLarge" }));
        Assert.That(rig.Fake.Selections, Is.Empty);
        Assert.That(rig.Fake.Dones, Is.Empty);
    }

    [Test]
    public void Failed_TheScreenCouldNotBeRead_ShowsAnErrorThatNamesNoBlankMessage()
    {
        var rig = new Rig();
        rig.Interactor.BeginIssue = CaptureIssue.Failed;

        rig.Start(CaptureKind.Window);

        Assert.That(rig.Notes.Errors, Has.Count.EqualTo(1));
        Assert.That(rig.Notes.Errors[0].Key, Is.EqualTo("Capture.Failed"));
    }

    [Test]
    public void Begin_TheRunWasCancelledWithNoSession_ShowsNothing()
    {
        var rig = new Rig();
        rig.Interactor.SessionToReturn = null;

        rig.Start(CaptureKind.Rectangle);

        Assert.That(rig.Interactor.Begun, Has.Count.EqualTo(1), "the flow did ask the use case");
        Assert.That(rig.Notes.Errors, Is.Empty);
        Assert.That(rig.Fake.Selections, Is.Empty);
    }

    [Test]
    public void F8_StartingAnotherCaptureWhileSelecting_ClosesTheOldOverlayAtOnceAndNeverStacksTwo()
    {
        var rig = new Rig();
        var first = NewSession(CaptureKind.Rectangle);
        rig.Interactor.SessionToReturn = first;
        rig.Start(CaptureKind.Rectangle);
        Assert.That(rig.Fake.OpenSelectionCount, Is.EqualTo(1));

        // The new run is still counting down (its begin is pending), as with a 5 s delay.
        var gate = new TaskCompletionSource<CaptureBeginResult>();
        rig.Interactor.RunningSession = first;
        rig.Interactor.BeginGate = gate;
        rig.StartWithoutWaiting(CaptureKind.Freeform, expectedBegins: 2);

        Assert.That(first.IsActive, Is.False, "the use case cancelled the old run");
        Assert.That(rig.Overlay(0).IsClosed, Is.True, "the old overlay goes at once, not when the new one is ready");
        Assert.That(rig.Fake.OpenSelectionCount, Is.Zero);

        var second = NewSession(CaptureKind.Freeform);
        gate.SetResult(new CaptureBeginResult(second, CaptureIssue.None));
        Assert.That(
            Retry.WhileFalse(() => rig.Fake.Selections.Count == 2, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(20)).Success,
            Is.True,
            "the new overlay opens when its snapshot is ready");
        Assert.That(rig.Fake.OpenSelectionCount, Is.EqualTo(1), "never two overlays at once");
        Assert.That(rig.Overlay(1).ViewModel.Kind, Is.EqualTo(CaptureKind.Freeform));
    }

    // Found by the E2E drive of the real exe (defect D1): with no delay BeginAsync goes straight on to take the snapshot, so an overlay
    // closed AFTER the call is still on the screen when the pixels are read - the image held the dimmed old overlay (SPEC capture:
    // the image never contains the app's own dim layer), and for the window kind the old overlay was picked as the window under the pointer.
    [Test]
    public void Replaced_RunTakesItsSnapshotOnlyAfterTheOldOverlayIsGone()
    {
        var rig = new Rig();
        var first = NewSession(CaptureKind.Rectangle);
        rig.Interactor.SessionToReturn = first;
        rig.Start(CaptureKind.Rectangle);
        Assert.That(rig.Fake.OpenSelectionCount, Is.EqualTo(1), "the first run put its overlay up");

        var openAtBegin = -1;
        rig.Interactor.RunningSession = first;
        rig.Interactor.BeforeBegin = () => openAtBegin = rig.Fake.OpenSelectionCount;
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);

        Assert.That(openAtBegin, Is.Zero, "the old overlay must be closed before the use case can take the snapshot");
        Assert.That(rig.Fake.OpenSelectionCount, Is.EqualTo(1), "and the new run still puts exactly one overlay up");
    }

    [Test]
    public void F8_TheOlderRunFinishesAfterTheNewerStarted_ItsSessionIsCancelledAndNoOverlayOpensForIt()
    {
        var rig = new Rig();
        var olderGate = new TaskCompletionSource<CaptureBeginResult>();
        rig.Interactor.BeginGate = olderGate;
        rig.StartWithoutWaiting(CaptureKind.Rectangle, expectedBegins: 1);
        var newerGate = new TaskCompletionSource<CaptureBeginResult>();
        rig.Interactor.BeginGate = newerGate;
        rig.StartWithoutWaiting(CaptureKind.Window, expectedBegins: 2);

        var stale = NewSession(CaptureKind.Rectangle);
        olderGate.SetResult(new CaptureBeginResult(stale, CaptureIssue.None));
        WpfHost.Instance.Settle();
        Thread.Sleep(100);
        WpfHost.Instance.Settle();

        Assert.That(rig.Fake.Selections, Is.Empty, "the stale run must not show an overlay over the newer one");
        Assert.That(stale.CancelCount, Is.EqualTo(1), "and its session is not left running");

        var current = NewSession(CaptureKind.Window);
        newerGate.SetResult(new CaptureBeginResult(current, CaptureIssue.None));
        Assert.That(
            Retry.WhileFalse(() => rig.Fake.Selections.Count == 1, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(20)).Success,
            Is.True);
        Assert.That(rig.Overlay(0).ViewModel.Kind, Is.EqualTo(CaptureKind.Window));
    }

    [Test]
    public void RealWindows_DragOnTheOverlayThenTheDialogAppearsWithTheCapturedSizeAndDiscardLeavesNothing()
    {
        var rig = new Rig(new WpfCaptureViews());
        WpfHost.Instance.Invoke(() => WpfHost.Instance.Language.Apply(Paper.ScreenWizzard.UseCases.Shell.Models.ResolvedLanguage.Vietnamese));
        rig.Interactor.SessionToReturn = NewSession(CaptureKind.Rectangle);
        rig.Start(CaptureKind.Rectangle);
        using var overlay = WpfHost.Instance.Attach("SelectionOverlay");

        Mouse.MoveTo(new System.Drawing.Point(250, 250));
        Mouse.Down(MouseButton.Left);
        Mouse.MoveTo(new System.Drawing.Point(550, 450));
        Mouse.Up(MouseButton.Left);
        WpfHost.Instance.Settle();

        Assert.That(overlay.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True, "the overlay closes when the drag ends");
        using var dialog = WpfHost.Instance.Attach("CaptureDoneWindow");
        Assert.That(dialog.TextOf("SizeText"), Is.EqualTo("300 × 200"));
        dialog.Screenshot("capture-flow-dialog-after-drag");

        dialog.Click("DiscardButton");

        Assert.That(dialog.WaitUntilClosed(TimeSpan.FromSeconds(3)), Is.True);
        Assert.That(rig.Interactor.Deliveries, Is.Empty);
        Assert.That(rig.Edited, Is.Empty);
    }

    [Test]
    public void RealWindows_StartingAnotherCaptureLeavesExactlyOneOverlayOnTheScreen()
    {
        var rig = new Rig(new WpfCaptureViews());
        var first = NewSession(CaptureKind.Rectangle);
        rig.Interactor.SessionToReturn = first;
        rig.Start(CaptureKind.Rectangle);
        Assert.That(OverlayCount(), Is.EqualTo(1));

        var gate = new TaskCompletionSource<CaptureBeginResult>();
        rig.Interactor.RunningSession = first;
        rig.Interactor.BeginGate = gate;
        rig.StartWithoutWaiting(CaptureKind.Window, expectedBegins: 2);
        Assert.That(OverlayCount(), Is.Zero, "F8: the old overlay is gone while the new run counts down");

        gate.SetResult(new CaptureBeginResult(NewSession(CaptureKind.Window), CaptureIssue.None));
        Assert.That(Retry.WhileFalse(() => OverlayCount() == 1, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(50)).Success, Is.True);
        Assert.That(OverlayCount(), Is.EqualTo(1), "one overlay, never two");
    }

    private static int OverlayCount() => WpfHost.Instance.Invoke(() =>
        System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().Count(
            w => w.IsVisible && System.Windows.Automation.AutomationProperties.GetAutomationId(w) == "SelectionOverlay"));
}
