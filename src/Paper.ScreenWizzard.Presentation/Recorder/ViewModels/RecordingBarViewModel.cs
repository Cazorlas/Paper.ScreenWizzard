using System.Globalization;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Recorder.Commands;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.UseCases.Shared.Models;

namespace Paper.ScreenWizzard.Presentation.Recorder.ViewModels;

/// <summary>One line of the "which monitor" menu: "1 (1920 × 1080, main)".</summary>
public sealed record MonitorChoice(int Index, string Label);

/// <summary>
/// The recording bar (SPEC recorder, "What the user does" 2 and 3): what to record, the three switches, Record. It records nothing
/// itself: Record raises <see cref="RecordRequested"/> with the choices, and the recording flow does the rest.
/// </summary>
public sealed class RecordingBarViewModel : BindableBase, IDisposable
{
    private readonly ILocalizer _localizer;
    private readonly RecorderSettings _settings;
    private RecordTargetKind _target;
    private int? _monitorIndex;
    private bool _systemSound;
    private bool _microphone;
    private bool _pointer;
    private NotificationMessage? _message;

    public RecordingBarViewModel(RecorderSettings settings, IReadOnlyList<MonitorInfo> monitors, ILocalizer localizer)
    {
        _localizer = localizer;
        _settings = settings;
        _target = settings.Target;
        _systemSound = settings.SystemSound;
        _microphone = settings.Microphone;
        _pointer = settings.Pointer;
        Monitors = monitors
            .OrderBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .Select((m, i) => new MonitorChoice(m.Index, Label(i + 1, m)))
            .ToList();
        _monitorIndex = Monitors.Any(m => m.Index == settings.MonitorIndex) ? settings.MonitorIndex : null;
        SelectTargetCommand = new SelectRecordTargetCommand(this);
        RecordCommand = new StartRecordingCommand(this);
        CloseCommand = new CloseRecordingBarCommand(this);
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Record was pressed: the choices of the bar, on top of the recording settings.</summary>
    public event Action<RecorderSettings>? RecordRequested;

    /// <summary>The X of the bar: closes this window only.</summary>
    public event EventHandler? CloseRequested;

    public IReadOnlyList<MonitorChoice> Monitors { get; }

    /// <summary>The "which monitor" menu is there only when there is a choice.</summary>
    public bool HasSeveralMonitors => Monitors.Count > 1;

    public RecordTargetKind Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    /// <summary>The monitor chosen in the menu; null is "the monitor under the pointer".</summary>
    public int? MonitorIndex
    {
        get => _monitorIndex;
        set => SetProperty(ref _monitorIndex, value);
    }

    public bool SystemSound
    {
        get => _systemSound;
        set => SetProperty(ref _systemSound, value);
    }

    public bool Microphone
    {
        get => _microphone;
        set => SetProperty(ref _microphone, value);
    }

    public bool Pointer
    {
        get => _pointer;
        set => SetProperty(ref _pointer, value);
    }

    /// <summary>Why Record did not go on (a region too small, F7); empty when there is nothing to say.</summary>
    public string MessageText => _message is null ? string.Empty : _localizer.Format(_message);

    public bool HasMessage => _message is not null;

    public SelectRecordTargetCommand SelectTargetCommand { get; }

    public StartRecordingCommand RecordCommand { get; }

    public CloseRecordingBarCommand CloseCommand { get; }

    /// <summary>What the bar holds now, as recording settings.</summary>
    public RecorderSettings Choices => _settings with
    {
        Target = _target,
        MonitorIndex = _monitorIndex,
        SystemSound = _systemSound,
        Microphone = _microphone,
        Pointer = _pointer,
    };

    public void ShowMessage(NotificationMessage? message)
    {
        _message = message;
        RaisePropertyChanged(nameof(MessageText));
        RaisePropertyChanged(nameof(HasMessage));
    }

    public void Dispose() => _localizer.LanguageChanged -= OnLanguageChanged;

    internal void RaiseRecordRequested()
    {
        ShowMessage(null);
        RecordRequested?.Invoke(Choices);
    }

    internal void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private string Label(int number, MonitorInfo monitor)
    {
        var size = string.Create(CultureInfo.InvariantCulture, $"{monitor.Bounds.Width} × {monitor.Bounds.Height}");
        return monitor.IsPrimary
            ? string.Format(CultureInfo.CurrentCulture, _localizer.GetString("Recorder.MonitorPrimary"), number, size)
            : string.Format(CultureInfo.CurrentCulture, _localizer.GetString("Recorder.MonitorOther"), number, size);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(MessageText));
}
