using System.Globalization;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Capture.Commands;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.UseCases.Capture.Models;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;

namespace Paper.ScreenWizzard.Presentation.Capture.ViewModels;

/// <summary>How the selection ended.</summary>
public enum SelectionEnd
{
    /// <summary>The user chose and the use case gave an image.</summary>
    Captured,

    /// <summary>Esc, the right mouse button, or the session was replaced: nothing was captured.</summary>
    Cancelled,

    /// <summary>A step failed for good (F6, F9, the screen could not be read); the user was told.</summary>
    Failed,
}

public sealed class SelectionFinishedEventArgs : EventArgs
{
    public SelectionFinishedEventArgs(SelectionEnd end, CaptureOutcome? outcome)
    {
        End = end;
        Outcome = outcome;
    }

    public SelectionEnd End { get; }

    /// <summary>The finished selection when <see cref="End"/> is <see cref="SelectionEnd.Captured"/>; otherwise null or the refusal.</summary>
    public CaptureOutcome? Outcome { get; }
}

/// <summary>
/// The selection overlay on the frozen snapshot (SPEC capture, "What the user does" steps 4 and 5). It holds what is drawn
/// (the dragged rectangle, the outline, the highlighted window, a message) and turns the mouse into calls on the
/// <see cref="ICaptureSession"/>; every point in and out is a physical desktop pixel, so the window converts display units at
/// the edge, in one place (<see cref="DisplayUnits"/>), and nothing here knows about DPI.
/// </summary>
public sealed class SelectionOverlayViewModel : BindableBase, IDisposable
{
    private readonly INotifications _notifications;
    private readonly ILocalizer _localizer;
    private readonly List<PixelPoint> _outline = [];
    private PixelPoint _anchor;
    private bool _isDragging;
    private bool _isFinished;
    private PixelRect? _selectionRect;
    private string _sizeLabel = string.Empty;
    private PixelRect? _hoverFrame;
    private string _hoverTitle = string.Empty;
    private NotificationMessage? _message;

    public SelectionOverlayViewModel(ICaptureSession session, INotifications notifications, ILocalizer localizer)
    {
        Session = session;
        _notifications = notifications;
        _localizer = localizer;
        CancelCommand = new CancelSelectionCommand(this);
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Raised once, when the overlay has nothing more to do: the window closes on it, the flow moves on.</summary>
    public event EventHandler<SelectionFinishedEventArgs>? Finished;

    public ICaptureSession Session { get; }

    public CaptureKind Kind => Session.Kind;

    public DesktopSnapshot Snapshot => Session.Snapshot;

    public CancelSelectionCommand CancelCommand { get; }

    public bool IsFinished => _isFinished;

    public bool IsDragging
    {
        get => _isDragging;
        private set => SetProperty(ref _isDragging, value);
    }

    /// <summary>The rectangle the drag would capture, as the session clamped it; null when no rectangle is being dragged.</summary>
    public PixelRect? SelectionRect
    {
        get => _selectionRect;
        private set => SetProperty(ref _selectionRect, value);
    }

    /// <summary>"width × height" of <see cref="SelectionRect"/>, empty when there is none.</summary>
    public string SizeLabel
    {
        get => _sizeLabel;
        private set => SetProperty(ref _sizeLabel, value);
    }

    /// <summary>The freeform outline drawn so far.</summary>
    public IReadOnlyList<PixelPoint> Outline => _outline;

    /// <summary>The frame of the window (or monitor) under the pointer, for the Window kind.</summary>
    public PixelRect? HoverFrame
    {
        get => _hoverFrame;
        private set => SetProperty(ref _hoverFrame, value);
    }

    public string HoverTitle
    {
        get => _hoverTitle;
        private set => SetProperty(ref _hoverTitle, value);
    }

    /// <summary>The refusal shown inside the overlay (F2, F3), in the language in use; null when there is none.</summary>
    public string? MessageText => _message is null ? null : _localizer.Format(_message);

    public bool HasMessage => _message is not null;

    /// <summary>Call once before the overlay is shown: the Window kind highlights what is under the pointer at once.</summary>
    public void Start()
    {
        if (Kind == CaptureKind.Window)
        {
            UpdateHover(Snapshot.CursorPosition);
        }
    }

    public void PointerDown(PixelPoint point)
    {
        if (_isFinished || IsDragging)
        {
            return;
        }

        SetMessage(null);
        _anchor = point;
        IsDragging = true;
        if (Kind == CaptureKind.Freeform)
        {
            _outline.Clear();
            _outline.Add(point);
            RaisePropertyChanged(nameof(Outline));
        }
    }

    public void PointerMoved(PixelPoint point)
    {
        if (_isFinished)
        {
            return;
        }

        switch (Kind)
        {
            case CaptureKind.Rectangle when IsDragging:
                var rectangle = Session.PreviewRectangle(_anchor, point);
                SelectionRect = rectangle;
                SizeLabel = string.Create(CultureInfo.InvariantCulture, $"{rectangle.Width} × {rectangle.Height}");
                break;
            case CaptureKind.Freeform when IsDragging:
                AddToOutline(point);
                break;
            case CaptureKind.Window:
                UpdateHover(point);
                break;
        }
    }

    /// <summary>The window lost the mouse without a button-up (Alt+Tab, a system box): the drag is abandoned, the overlay stays.</summary>
    public void PointerCaptureLost()
    {
        if (_isFinished || !IsDragging)
        {
            return;
        }

        ClearSelection();
    }

    /// <summary>
    /// The screen layout may have changed (a monitor plugged or unplugged, a resolution or scale change): the frozen picture no
    /// longer matches what the user sees, so it ends now with the message of F6 rather than waiting for the drag to end.
    /// </summary>
    public void DisplayChanged()
    {
        if (_isFinished)
        {
            return;
        }

        var display = Session.CheckDisplayUnchanged();
        if (display != CaptureIssue.None)
        {
            Fail(CaptureMessages.For(display, null), null);
        }
    }

    public void PointerUp(PixelPoint point)
    {
        if (_isFinished || !IsDragging)
        {
            return;
        }

        IsDragging = false;
        if (Kind == CaptureKind.Freeform)
        {
            AddToOutline(point);
        }

        // The pixels were taken on a screen layout: if it changed since, they no longer match what the user sees (F6).
        var display = Session.CheckDisplayUnchanged();
        if (display != CaptureIssue.None)
        {
            Fail(CaptureMessages.For(display, null), null);
            return;
        }

        var outcome = Kind switch
        {
            CaptureKind.Rectangle => Session.CompleteRectangle(_anchor, point),
            CaptureKind.Freeform => Session.CompleteFreeform(_outline.ToList()),
            CaptureKind.Window => Session.CompleteWindow(point),
            _ => Session.CompleteFullScreen(),
        };
        Handle(outcome);
    }

    /// <summary>Ends the overlay when the use case replaced or ended its session (F8); true when it did. The session is not cancelled again.</summary>
    public bool EndIfSessionInactive()
    {
        if (_isFinished || Session.IsActive)
        {
            return false;
        }

        Finish(SelectionEnd.Cancelled, null);
        return true;
    }

    public void Dispose() => _localizer.LanguageChanged -= OnLanguageChanged;

    /// <summary>Esc or the right mouse button (<see cref="CancelSelectionCommand"/>).</summary>
    internal void Cancel()
    {
        if (_isFinished)
        {
            return;
        }

        CancelSession();
        Finish(SelectionEnd.Cancelled, null);
    }

    private void Handle(CaptureOutcome outcome)
    {
        switch (outcome.Issue)
        {
            case CaptureIssue.None when outcome.Image is not null:
                Finish(SelectionEnd.Captured, outcome);
                break;

            // F2, F3: not an error box; the user stays on the frozen screen and drags again.
            case CaptureIssue.RegionTooSmall:
            case CaptureIssue.OutlineTooSmall:
                ClearSelection();
                SetMessage(CaptureMessages.For(outcome.Issue, outcome.Message));
                break;

            default:
                // F6, F9, or a screen that could not be read: nothing more to choose on, so say why and leave.
                Fail(CaptureMessages.For(outcome.Issue == CaptureIssue.None ? CaptureIssue.Failed : outcome.Issue, outcome.Message), outcome);
                break;
        }
    }

    private void Fail(NotificationMessage message, CaptureOutcome? outcome)
    {
        CancelSession();
        _notifications.ShowError(message);
        Finish(SelectionEnd.Failed, outcome);
    }

    private void CancelSession()
    {
        if (Session.IsActive)
        {
            Session.Cancel();
        }
    }

    private void Finish(SelectionEnd end, CaptureOutcome? outcome)
    {
        _isFinished = true;
        RaisePropertyChanged(nameof(IsFinished));
        Dispose();
        Finished?.Invoke(this, new SelectionFinishedEventArgs(end, outcome));
    }

    private void AddToOutline(PixelPoint point)
    {
        if (_outline.Count > 0 && _outline[^1] == point)
        {
            return;
        }

        _outline.Add(point);
        RaisePropertyChanged(nameof(Outline));
    }

    private void UpdateHover(PixelPoint point)
    {
        var hit = Session.HitTestWindow(point);
        HoverFrame = hit.Found ? hit.Frame : null;
        HoverTitle = hit.Found && !hit.IsWholeMonitor ? hit.Title : string.Empty;
    }

    private void ClearSelection()
    {
        IsDragging = false;
        SelectionRect = null;
        SizeLabel = string.Empty;
        if (_outline.Count > 0)
        {
            _outline.Clear();
            RaisePropertyChanged(nameof(Outline));
        }
    }

    private void SetMessage(NotificationMessage? message)
    {
        _message = message;
        RaisePropertyChanged(nameof(MessageText));
        RaisePropertyChanged(nameof(HasMessage));
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(MessageText));
}
