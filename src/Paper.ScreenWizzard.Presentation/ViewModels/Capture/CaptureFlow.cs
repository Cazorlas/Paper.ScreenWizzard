using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.ViewModels.Shell;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Presentation.ViewModels.Capture;

/// <summary>
/// Ties one capture together (SPEC capture): countdown, the overlay on the frozen snapshot, then what happens to the image, the
/// "Đã chụp" dialog or straight to the destinations the user set. It decides nothing about the pixels or where they go: the use
/// case does, and this class opens and closes the windows around the answers. It must be created and used on the UI thread,
/// because the countdown reports come back to the thread that started the run.
/// </summary>
public sealed class CaptureFlow
{
    // Each further dialog opens this many physical pixels down and to the right, so the earlier one can still be seen and used.
    private const int DialogCascadeStep = 32;

    // How long the flow waits after closing its own windows before it lets the screen be read: the desktop compositor repaints
    // the freed area within a frame or two (measured on this machine as well under 100 ms), so this is a margin, not a guess at zero.
    private static readonly TimeSpan SettleAfterClose = TimeSpan.FromMilliseconds(120);

    private readonly ICaptureInteractor _interactor;
    private readonly ICaptureViews _views;
    private readonly INotificationPort _notifications;
    private readonly IFileDialogService _fileDialogs;
    private readonly ILocalizer _localizer;
    private readonly Func<AppSettings> _settings;
    private readonly List<DialogRun> _dialogs = [];
    private CancellationTokenSource? _current;
    private SelectionRun? _selection;
    private CountdownRun? _countdown;
    private int _generation;

    public CaptureFlow(
        ICaptureInteractor interactor,
        ICaptureViews views,
        INotificationPort notifications,
        IFileDialogService fileDialogs,
        ILocalizer localizer,
        Func<AppSettings> settings)
    {
        _interactor = interactor;
        _views = views;
        _notifications = notifications;
        _fileDialogs = fileDialogs;
        _localizer = localizer;
        _settings = settings;
    }

    /// <summary>An image is going to the editor: after Sửa, or when the settings send captures straight there.</summary>
    public event Action<PixelImage>? EditRequested;

    /// <summary>How many "Đã chụp" dialogs are open now.</summary>
    public int OpenDialogCount => _dialogs.Count;

    /// <summary>
    /// Starts a capture of <paramref name="kind"/>. Another capture while one is running just asks the use case again: it cancels
    /// the old session (SPEC capture F8), and this flow closes the old overlay at once and drops the old run's late answer, so two
    /// overlays never stack. The task ends when the overlay is up (or the capture failed or had nothing to choose), not when the
    /// user has chosen.
    /// </summary>
    public async Task StartAsync(CaptureKind kind)
    {
        var generation = ++_generation;
        _current?.Cancel();
        var cancellation = new CancellationTokenSource();
        _current = cancellation;

        var request = _interactor.RequestFor(kind, _settings());
        CountdownRun? countdown = null;
        var progress = new Progress<int>(seconds =>
        {
            if (generation != _generation)
            {
                return;
            }

            countdown ??= OpenCountdown(seconds);
            _countdown = countdown;
            countdown.ViewModel.SecondsLeft = seconds;
        });

        try
        {
            // Whatever the run this one replaces left on the screen goes BEFORE the use case is asked. With no delay BeginAsync runs
            // straight on to read the screen, so an overlay closed after the call is still in the pixels: the image held the dimmed old
            // overlay, and for the window kind the old overlay was the "window" under the pointer (defect D1, found by driving the exe).
            // The closed windows also need the desktop compositor to repaint once before the screen is read, hence the short wait.
            var closedSomething = CloseSelection(cancelSession: true);
            if (_countdown is { } earlier)
            {
                earlier.Close();
                _countdown = null;
                closedSomething = true;
            }

            if (closedSomething)
            {
                await Task.Delay(SettleAfterClose);
                if (generation != _generation)
                {
                    return;
                }
            }

            var begin = _interactor.BeginAsync(request, progress, cancellation.Token);

            CaptureBeginResult result;
            try
            {
                result = await begin;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Progress<T> posts each report to the UI thread; a begin that finished at once has its last reports still queued, so
            // give them their turn before the countdown is taken down.
            await Task.Yield();
            countdown?.Close();

            if (generation != _generation)
            {
                CancelStale(result.Session);
                return;
            }

            if (result.Session is null)
            {
                if (result.Issue != CaptureIssue.None)
                {
                    _notifications.ShowError(CaptureMessages.For(result.Issue, null));
                }

                return;
            }

            Continue(result.Session);
        }
        finally
        {
            countdown?.Close();
            if (ReferenceEquals(_countdown, countdown))
            {
                _countdown = null;
            }

            if (ReferenceEquals(_current, cancellation))
            {
                _current = null;
            }

            cancellation.Dispose();
        }
    }

    private static void CancelStale(ICaptureSession? session)
    {
        if (session is { IsActive: true })
        {
            session.Cancel();
        }
    }

    private CountdownRun OpenCountdown(int firstSecond)
    {
        var viewModel = new CountdownViewModel { SecondsLeft = firstSecond };
        var run = new CountdownRun(viewModel, _views.OpenCountdown(viewModel));
        viewModel.CancelRequested += (_, _) => CancelCountdown(run);
        return run;
    }

    // The user gave up while the number was counting (SPEC capture, "Cách dùng" step 5): the use case stops waiting, so no snapshot is
    // taken and no overlay follows, and the number goes at once rather than when the cancelled task unwinds.
    private void CancelCountdown(CountdownRun run)
    {
        _current?.Cancel();
        run.Close();
        if (ReferenceEquals(_countdown, run))
        {
            _countdown = null;
        }
    }

    private void Continue(ICaptureSession session)
    {
        if (session.Kind == CaptureKind.FullScreen)
        {
            // Nothing to choose: the whole screen is the answer at once (SPEC capture, "Toàn màn hình").
            var outcome = session.CompleteFullScreen();
            if (outcome.Issue == CaptureIssue.None && outcome.Image is not null)
            {
                HandleCaptured(outcome, session.Snapshot);
            }
            else
            {
                _notifications.ShowError(CaptureMessages.For(outcome.Issue == CaptureIssue.None ? CaptureIssue.Failed : outcome.Issue, outcome.Message));
            }

            return;
        }

        // Never two overlays: whatever is still up belongs to an older run.
        CloseSelection(cancelSession: true);
        var viewModel = new SelectionOverlayViewModel(session, _notifications, _localizer);
        viewModel.Finished += OnSelectionFinished;
        viewModel.Start();
        _selection = new SelectionRun(viewModel, _views.OpenSelection(viewModel));
    }

    private void OnSelectionFinished(object? sender, SelectionFinishedEventArgs e)
    {
        if (sender is not SelectionOverlayViewModel viewModel)
        {
            return;
        }

        viewModel.Finished -= OnSelectionFinished;
        if (_selection is not null && ReferenceEquals(_selection.ViewModel, viewModel))
        {
            var run = _selection;
            _selection = null;
            run.Handle.Close();
        }

        // Cancelled and failed selections ended themselves: the overlay told the user (F6, F9) or there is nothing to tell.
        if (e.End == SelectionEnd.Captured && e.Outcome?.Image is not null)
        {
            HandleCaptured(e.Outcome, viewModel.Snapshot);
        }
    }

    private void CloseOverlayOfInactiveSession()
    {
        if (_selection is { } run && !run.ViewModel.Session.IsActive)
        {
            CloseSelection(cancelSession: false);
        }
    }

    private bool CloseSelection(bool cancelSession)
    {
        if (_selection is not { } run)
        {
            return false;
        }

        _selection = null;
        run.ViewModel.Finished -= OnSelectionFinished;
        if (cancelSession)
        {
            run.ViewModel.CancelCommand.Execute(null);
        }
        else
        {
            run.ViewModel.EndIfSessionInactive();
        }

        run.Handle.Close();
        return true;
    }

    private void HandleCaptured(CaptureOutcome outcome, DesktopSnapshot snapshot)
    {
        var image = outcome.Image!;
        var settings = _settings();
        var plan = _interactor.PlanAfterCapture(settings);
        if (plan.ShowDialog)
        {
            OpenDialog(image, DialogArea(outcome, snapshot), settings);
            return;
        }

        foreach (var destination in plan.Destinations)
        {
            if (destination == CaptureDestination.Editor)
            {
                EditRequested?.Invoke(image);
                continue;
            }

            var result = _interactor.Deliver(image, destination, settings);
            if (result.Delivered)
            {
                if (result.Message is not null)
                {
                    _notifications.ShowToast(result.Message);
                }
            }
            else
            {
                // No dialog to hold the reason, so it is an error box the user must read (SPEC capture F4, F5).
                _notifications.ShowError(CaptureMessages.For(CaptureIssue.Failed, result.Message));
            }
        }
    }

    // The dialog centres on the monitor the capture was on, in physical pixels (SPEC: giữa màn hình vừa chụp).
    private PixelRect DialogArea(CaptureOutcome outcome, DesktopSnapshot snapshot)
    {
        var region = outcome.Region ?? snapshot.VirtualScreen;
        var centreX = region.X + (region.Width / 2);
        var centreY = region.Y + (region.Height / 2);
        var monitor = snapshot.Monitors.FirstOrDefault(m =>
            centreX >= m.Bounds.X && centreX < m.Bounds.X + m.Bounds.Width && centreY >= m.Bounds.Y && centreY < m.Bounds.Y + m.Bounds.Height);
        var area = monitor?.Bounds ?? region;
        var offset = _dialogs.Count * DialogCascadeStep;
        return new PixelRect(area.X + offset, area.Y + offset, area.Width, area.Height);
    }

    private void OpenDialog(PixelImage image, PixelRect area, AppSettings settings)
    {
        var viewModel = new CaptureDoneViewModel(_interactor, image, settings, _fileDialogs, _notifications, _localizer);
        var run = new DialogRun(viewModel);
        viewModel.EditRequested += forwarded => EditRequested?.Invoke(forwarded);
        viewModel.CloseRequested += (_, _) => run.Handle?.Close();
        _dialogs.Add(run);
        run.Handle = _views.OpenDone(viewModel, area);
        run.Handle.Closed += (_, _) =>
        {
            // Whoever closed it (Bỏ, Esc, the title bar's ✕, a delivery that worked): the dialog is gone and its image with it.
            _dialogs.Remove(run);
            viewModel.Dispose();
        };
    }

    private sealed class CountdownRun
    {
        private bool _closed;

        public CountdownRun(CountdownViewModel viewModel, IViewHandle handle)
        {
            ViewModel = viewModel;
            Handle = handle;
        }

        public CountdownViewModel ViewModel { get; }

        public IViewHandle Handle { get; }

        public void Close()
        {
            if (!_closed)
            {
                _closed = true;
                Handle.Close();
            }
        }
    }

    private sealed class SelectionRun
    {
        public SelectionRun(SelectionOverlayViewModel viewModel, IViewHandle handle)
        {
            ViewModel = viewModel;
            Handle = handle;
        }

        public SelectionOverlayViewModel ViewModel { get; }

        public IViewHandle Handle { get; }
    }

    private sealed class DialogRun
    {
        public DialogRun(CaptureDoneViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public CaptureDoneViewModel ViewModel { get; }

        public IViewHandle? Handle { get; set; }
    }
}
