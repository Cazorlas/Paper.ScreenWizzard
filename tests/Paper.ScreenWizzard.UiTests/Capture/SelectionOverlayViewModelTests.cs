using NUnit.Framework;
using Paper.ScreenWizzard.Presentation.ViewModels.Capture;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Geometry;
using Paper.ScreenWizzard.UiTests.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Common.Models;

namespace Paper.ScreenWizzard.UiTests.ScreenCapture;

/// <summary>
/// What the selection overlay does with the mouse, proven on a view model fed a fake session (no window): SPEC capture
/// "What the user does" steps 4 and 5, and rows F2, F3, F6, F9. Points are physical desktop pixels, as the session speaks them.
/// </summary>
[TestFixture]
public sealed class SelectionOverlayViewModelTests
{
    private static (SelectionOverlayViewModel ViewModel, FakeCaptureSession Session, RecordingNotifications Notes, List<SelectionFinishedEventArgs> Finished)
        Create(CaptureKind kind)
    {
        var session = new FakeCaptureSession(kind);
        var notes = new RecordingNotifications();
        var viewModel = new SelectionOverlayViewModel(session, notes, new FakeLocalizer());
        var finished = new List<SelectionFinishedEventArgs>();
        viewModel.Finished += (_, e) => finished.Add(e);
        return (viewModel, session, notes, finished);
    }

    private static void Drag(SelectionOverlayViewModel viewModel, PixelPoint from, PixelPoint to)
    {
        viewModel.PointerDown(from);
        viewModel.PointerMoved(to);
        viewModel.PointerUp(to);
    }

    [Test]
    public void Rectangle_WhileDragging_ShowsTheRegionTheSessionSaysAndItsWidthByHeight()
    {
        var (viewModel, _, _, _) = Create(CaptureKind.Rectangle);

        viewModel.PointerDown(new PixelPoint(150, 150));
        viewModel.PointerMoved(new PixelPoint(450, 350));

        Assert.That(viewModel.IsDragging, Is.True);
        Assert.That(viewModel.SelectionRect, Is.EqualTo(new PixelRect(150, 150, 300, 200)));
        Assert.That(viewModel.SizeLabel, Is.EqualTo("300 × 200"));
    }

    [Test]
    public void Rectangle_DraggedBackwards_ShowsTheSameRegion()
    {
        var (viewModel, _, _, _) = Create(CaptureKind.Rectangle);

        viewModel.PointerDown(new PixelPoint(450, 350));
        viewModel.PointerMoved(new PixelPoint(150, 150));

        Assert.That(viewModel.SelectionRect, Is.EqualTo(new PixelRect(150, 150, 300, 200)));
        Assert.That(viewModel.SizeLabel, Is.EqualTo("300 × 200"));
    }

    [Test]
    public void Rectangle_DraggedPastTheEdge_ShowsTheClampedRegionOfTheSessionNotItsOwnGuess()
    {
        var (viewModel, _, _, _) = Create(CaptureKind.Rectangle);

        viewModel.PointerDown(new PixelPoint(150, 150));
        viewModel.PointerMoved(new PixelPoint(2000, 1500));

        Assert.That(viewModel.SelectionRect, Is.EqualTo(new PixelRect(150, 150, 750, 550)), "the screen is 800 x 600 at (100, 100)");
        Assert.That(viewModel.SizeLabel, Is.EqualTo("750 × 550"));
    }

    [Test]
    public void Rectangle_Release_ChecksTheDisplayThenCompletesWithTheDraggedPoints()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Rectangle);

        Drag(viewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(session.Calls, Is.EqualTo(new[] { "CheckDisplayUnchanged", "CompleteRectangle" }));
        Assert.That(session.RectangleCompletions, Is.EqualTo(new[] { (new PixelPoint(250, 250), new PixelPoint(550, 450)) }));
        Assert.That(finished, Has.Count.EqualTo(1));
        Assert.That(finished[0].End, Is.EqualTo(SelectionEnd.Captured));
        Assert.That(finished[0].Outcome!.Image, Is.Not.Null);
        Assert.That(viewModel.IsFinished, Is.True);
    }

    [Test]
    public void Rectangle_MovingWithoutAPress_ChangesNothing()
    {
        var (viewModel, session, _, _) = Create(CaptureKind.Rectangle);

        viewModel.PointerMoved(new PixelPoint(300, 300));
        viewModel.PointerUp(new PixelPoint(300, 300));

        Assert.That(viewModel.SelectionRect, Is.Null);
        Assert.That(session.Calls, Is.Empty, "a release with no press is not a drag");

        Drag(viewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));
        Assert.That(session.RectangleCompletions, Has.Count.EqualTo(1), "while a real drag right after it does capture");
    }

    [Test]
    public void F2_RegionUnder3x3_ShowsTheMessageAndStaysInSelectionForAnotherDrag()
    {
        var (viewModel, session, notes, finished) = Create(CaptureKind.Rectangle);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.RegionTooSmall, NotificationMessage.Of("Capture.RegionTooSmall")));

        Drag(viewModel, new PixelPoint(300, 300), new PixelPoint(302, 302));

        Assert.That(viewModel.HasMessage, Is.True);
        Assert.That(viewModel.MessageText, Is.EqualTo("Capture.RegionTooSmall"));
        Assert.That(finished, Is.Empty, "the overlay stays so the user can drag again");
        Assert.That(viewModel.IsFinished, Is.False);
        Assert.That(session.CancelCount, Is.Zero, "the session is still the user's to use");
        Assert.That(notes.Errors, Is.Empty, "F2 is said inside the overlay, not in an error box");
        Assert.That(viewModel.IsDragging, Is.False);
        Assert.That(viewModel.SelectionRect, Is.Null, "the too-small rectangle is not left on the screen");
        Assert.That(viewModel.SizeLabel, Is.Empty);

        viewModel.PointerDown(new PixelPoint(300, 300));
        Assert.That(viewModel.HasMessage, Is.False, "starting a new drag clears the old message");
        viewModel.PointerMoved(new PixelPoint(500, 450));
        viewModel.PointerUp(new PixelPoint(500, 450));

        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Captured), "the second drag captures");
    }

    [Test]
    public void F2_TheUseCaseSendsNoMessage_TheOverlayStillNamesTheProblem()
    {
        var (viewModel, session, _, _) = Create(CaptureKind.Rectangle);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.RegionTooSmall, null));

        Drag(viewModel, new PixelPoint(300, 300), new PixelPoint(301, 301));

        Assert.That(viewModel.MessageText, Is.EqualTo("Capture.RegionTooSmall"), "an unexplained refusal must never be a silent nothing");
    }

    [Test]
    public void F3_OutlineTooSmall_ShowsTheMessageAndStaysInSelection()
    {
        var (viewModel, session, notes, finished) = Create(CaptureKind.Freeform);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.OutlineTooSmall, NotificationMessage.Of("Capture.OutlineTooSmall")));

        viewModel.PointerDown(new PixelPoint(300, 300));
        viewModel.PointerMoved(new PixelPoint(301, 300));
        viewModel.PointerUp(new PixelPoint(301, 300));

        Assert.That(viewModel.MessageText, Is.EqualTo("Capture.OutlineTooSmall"));
        Assert.That(finished, Is.Empty);
        Assert.That(session.CancelCount, Is.Zero);
        Assert.That(notes.Errors, Is.Empty);
        Assert.That(viewModel.Outline, Is.Empty, "the refused outline is wiped so the user draws again from nothing");
    }

    [Test]
    public void Freeform_WhileDragging_CollectsTheOutlineAndReleaseCompletesWithIt()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Freeform);
        var triangle = new[] { new PixelPoint(200, 200), new PixelPoint(400, 200), new PixelPoint(300, 400) };

        viewModel.PointerDown(triangle[0]);
        viewModel.PointerMoved(triangle[1]);
        viewModel.PointerMoved(triangle[1]);
        viewModel.PointerMoved(triangle[2]);
        Assert.That(viewModel.Outline, Is.EqualTo(triangle), "a repeated point is not added twice");
        viewModel.PointerUp(triangle[2]);

        Assert.That(session.FreeformCompletions.Only("freeform completion"), Is.EqualTo(triangle));
        Assert.That(session.Calls, Is.EqualTo(new[] { "CheckDisplayUnchanged", "CompleteFreeform" }));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Captured));
    }

    [Test]
    public void Window_Start_HighlightsTheWindowUnderTheCursorAtOnce()
    {
        var session = new FakeCaptureSession(CaptureKind.Window, CaptureTestData.Snapshot(new PixelPoint(250, 250)));
        var viewModel = new SelectionOverlayViewModel(session, new RecordingNotifications(), new FakeLocalizer());

        viewModel.Start();

        Assert.That(viewModel.HoverFrame, Is.EqualTo(CaptureTestData.NotepadFrame));
        Assert.That(viewModel.HoverTitle, Is.EqualTo("Notepad"));
    }

    [Test]
    public void Window_Hover_AsksTheSessionAndShowsTheFrameAndTitleOfTheTopWindow()
    {
        var (viewModel, _, _, _) = Create(CaptureKind.Window);

        viewModel.PointerMoved(new PixelPoint(400, 350));
        Assert.That(viewModel.HoverFrame, Is.EqualTo(CaptureTestData.NotepadFrame), "the cursor is in the overlap: the window on top wins");
        Assert.That(viewModel.HoverTitle, Is.EqualTo("Notepad"));

        viewModel.PointerMoved(new PixelPoint(700, 550));
        Assert.That(viewModel.HoverFrame, Is.EqualTo(CaptureTestData.ExplorerFrame));
        Assert.That(viewModel.HoverTitle, Is.EqualTo("Explorer"));

        viewModel.PointerMoved(new PixelPoint(120, 120));
        Assert.That(viewModel.HoverFrame, Is.EqualTo(CaptureTestData.SmallScreen), "on the desktop background the whole monitor is highlighted");
    }

    [Test]
    public void Window_Click_CompletesTheWindowAtThePointer()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Window);

        viewModel.PointerMoved(new PixelPoint(250, 250));
        viewModel.PointerDown(new PixelPoint(250, 250));
        viewModel.PointerUp(new PixelPoint(250, 250));

        Assert.That(session.WindowCompletions, Is.EqualTo(new[] { new PixelPoint(250, 250) }));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Captured));
    }

    [Test]
    public void Cancel_CancelsTheSessionAndFinishesWithNothingCaptured()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Rectangle);
        viewModel.PointerDown(new PixelPoint(200, 200));

        viewModel.CancelCommand.Execute(null);

        Assert.That(session.CancelCount, Is.EqualTo(1));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Cancelled));
        Assert.That(finished.Only("Finished event").Outcome, Is.Null);
        Assert.That(session.Calls, Does.Not.Contain("CompleteRectangle"));
    }

    [Test]
    public void Cancel_AfterTheSelectionEnded_DoesNotCancelTwice()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Rectangle);
        Drag(viewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        viewModel.CancelCommand.Execute(null);

        Assert.That(finished, Has.Count.EqualTo(1));
        Assert.That(session.CancelCount, Is.Zero);
    }

    [Test]
    public void F6_TheDisplayChangedWhileChoosing_ShowsTheErrorCancelsAndNeverCompletes()
    {
        var (viewModel, session, notes, finished) = Create(CaptureKind.Rectangle);
        session.DisplayAnswer = CaptureIssue.DisplayChanged;

        Drag(viewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Assert.That(session.Calls, Does.Not.Contain("CompleteRectangle"), "the pixels were taken on a screen layout that no longer exists");
        Assert.That(session.CancelCount, Is.EqualTo(1));
        Assert.That(notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.DisplayChanged" }));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Failed));
    }

    [Test]
    public void F9_ImageTooLarge_ShowsTheErrorAndClosesInsteadOfStaying()
    {
        var (viewModel, session, notes, finished) = Create(CaptureKind.Rectangle);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.OutOfMemory, NotificationMessage.Of("Capture.ImageTooLarge")));

        Drag(viewModel, new PixelPoint(150, 150), new PixelPoint(850, 650));

        Assert.That(notes.Errors.Select(m => m.Key), Is.EqualTo(new[] { "Capture.ImageTooLarge" }));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Failed));
        Assert.That(viewModel.HasMessage, Is.False, "the error box says it, the overlay is already going away");
    }

    [Test]
    public void Failed_TheScreenCouldNotBeRead_ShowsTheErrorWithTheReasonAndCloses()
    {
        var (viewModel, session, notes, finished) = Create(CaptureKind.Window);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.Failed, NotificationMessage.Of("Capture.Failed", "access denied")));

        viewModel.PointerDown(new PixelPoint(250, 250));
        viewModel.PointerUp(new PixelPoint(250, 250));

        Assert.That(notes.Errors.Only("error").Key, Is.EqualTo("Capture.Failed"));
        Assert.That(notes.Errors.Only("error").Arguments, Is.EqualTo(new[] { "access denied" }));
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Failed));
    }

    [Test]
    public void F9_TheUseCaseSendsNoMessage_TheErrorStillNamesTheProblem()
    {
        var (viewModel, session, notes, _) = Create(CaptureKind.Rectangle);
        session.Answers.Enqueue(CaptureTestData.Refused(CaptureIssue.OutOfMemory, null));

        Drag(viewModel, new PixelPoint(150, 150), new PixelPoint(850, 650));

        Assert.That(notes.Errors.Only("error").Key, Is.EqualTo("Capture.ImageTooLarge"));
    }

    [Test]
    public void F8_TheSessionWasReplaced_TheOverlayEndsWithoutCancellingAgain()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Rectangle);
        Assert.That(viewModel.EndIfSessionInactive(), Is.False, "a live session keeps its overlay");

        session.IsActive = false;

        Assert.That(viewModel.EndIfSessionInactive(), Is.True);
        Assert.That(finished.Only("Finished event").End, Is.EqualTo(SelectionEnd.Cancelled));
        Assert.That(session.CancelCount, Is.Zero, "the interactor already ended it");
    }

    [Test]
    public void Finished_IgnoresEveryLaterInput()
    {
        var (viewModel, session, _, finished) = Create(CaptureKind.Rectangle);
        Drag(viewModel, new PixelPoint(250, 250), new PixelPoint(550, 450));

        Drag(viewModel, new PixelPoint(300, 300), new PixelPoint(500, 500));

        Assert.That(session.RectangleCompletions, Has.Count.EqualTo(1));
        Assert.That(finished, Has.Count.EqualTo(1));
    }
}
