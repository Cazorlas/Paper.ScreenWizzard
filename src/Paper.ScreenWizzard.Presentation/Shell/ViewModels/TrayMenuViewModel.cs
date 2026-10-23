using System.Windows.Input;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Shell.Commands;

namespace Paper.ScreenWizzard.Presentation.Shell.ViewModels;

/// <summary>One line of the tray menu.</summary>
public sealed class TrayMenuItemViewModel : BindableBase
{
    private bool _isChecked;

    public TrayMenuItemViewModel(string textKey, string shortcutText, ICommand command, bool isCheckable, bool isChecked)
    {
        TextKey = textKey;
        ShortcutText = shortcutText;
        Command = command;
        IsCheckable = isCheckable;
        _isChecked = isChecked;
    }

    /// <summary>The resource key of the item's text; the menu turns it into the language in use.</summary>
    public string TextKey { get; }

    /// <summary>The chord shown at the right ("Ctrl+PrintScreen"); empty when the item has no hotkey.</summary>
    public string ShortcutText { get; }

    public ICommand Command { get; }

    /// <summary>True for the "Thanh chụp" line, which shows a tick while the capture bar is visible.</summary>
    public bool IsCheckable { get; }

    public bool IsChecked
    {
        get => _isChecked;
        internal set => SetProperty(ref _isChecked, value);
    }
}

/// <summary>
/// The content of the tray menu as data. The <c>NotifyIcon</c> that shows it comes with the adapters (plan T17); it
/// reads <see cref="Items"/> and calls each item's command.
/// </summary>
public sealed class TrayMenuViewModel : BindableBase
{
    private IReadOnlyDictionary<CaptureKind, HotkeyChord> _hotkeys;
    private IReadOnlyList<TrayMenuItemViewModel> _items;
    private bool _captureBarVisible;
    private bool _updateAvailable;
    private RecordingHotkeys? _recordHotkeys;
    private bool _recording;
    private bool _paused;

    public TrayMenuViewModel(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys, bool captureBarVisible)
    {
        _captureBarVisible = captureBarVisible;
        _hotkeys = hotkeys;
        _items = BuildItems();
    }

    /// <summary>A capture line was chosen.</summary>
    public event Action<CaptureKind>? CaptureRequested;

    public event EventHandler? OpenImageRequested;

    /// <summary>The "Thanh chụp" line was chosen; the owner shows or hides the bar and calls <see cref="SetCaptureBarVisible"/>.</summary>
    public event EventHandler? ToggleCaptureBarRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    /// <summary>The "Tải bản mới…" line was chosen.</summary>
    public event EventHandler? UpdateRequested;

    /// <summary>The "Kiểm bản mới" line was chosen.</summary>
    public event EventHandler? CheckForUpdatesRequested;

    /// <summary>"Quay màn hình…": the owner opens the recording bar.</summary>
    public event EventHandler? RecordRequested;

    /// <summary>"Tạm dừng quay" or "Tiếp tục quay", while recording.</summary>
    public event EventHandler? RecordPauseRequested;

    /// <summary>"Dừng quay", while recording.</summary>
    public event EventHandler? RecordStopRequested;

    /// <summary>
    /// Capture x4, Open image, Record screen (while recording: Pause or Resume, and Stop), Capture bar, Settings, Kiểm bản mới, then
    /// "Tải bản mới…" while a newer version is out, then Exit.
    /// </summary>
    public IReadOnlyList<TrayMenuItemViewModel> Items => _items;

    /// <summary>Redraws the shortcut texts after a hotkey changed in Settings.</summary>
    public void SetHotkeys(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys)
    {
        _hotkeys = hotkeys;
        _items = BuildItems();
        RaisePropertyChanged(nameof(Items));
    }

    /// <summary>Shows the recording hotkeys at the right of the recording lines.</summary>
    public void SetRecordHotkeys(RecordingHotkeys hotkeys)
    {
        _recordHotkeys = hotkeys;
        _items = BuildItems();
        RaisePropertyChanged(nameof(Items));
    }

    /// <summary>While recording the "Quay màn hình…" line gives way to Pause (or Resume) and Stop (SPEC recorder, "What the user does" 4).</summary>
    public void SetRecording(bool recording, bool paused)
    {
        if (_recording == recording && _paused == paused)
        {
            return;
        }

        _recording = recording;
        _paused = paused;
        _items = BuildItems();
        RaisePropertyChanged(nameof(Items));
    }

    /// <summary>Adds the "Tải bản mới…" line once a newer version is known (SPEC shell, "Báo bản mới").</summary>
    public void SetUpdateAvailable(bool available)
    {
        if (_updateAvailable == available)
        {
            return;
        }

        _updateAvailable = available;
        _items = BuildItems();
        RaisePropertyChanged(nameof(Items));
    }

    /// <summary>Puts or removes the tick of the "Thanh chụp" line.</summary>
    public void SetCaptureBarVisible(bool visible)
    {
        _captureBarVisible = visible;
        foreach (var item in _items.Where(item => item.IsCheckable))
        {
            item.IsChecked = visible;
        }
    }

    internal void RaiseCaptureRequested(CaptureKind kind) => CaptureRequested?.Invoke(kind);

    internal void RaiseOpenImageRequested() => OpenImageRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseToggleCaptureBarRequested() => ToggleCaptureBarRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseSettingsRequested() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseExitRequested() => ExitRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseUpdateRequested() => UpdateRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseCheckForUpdatesRequested() => CheckForUpdatesRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseRecordRequested() => RecordRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseRecordPauseRequested() => RecordPauseRequested?.Invoke(this, EventArgs.Empty);

    internal void RaiseRecordStopRequested() => RecordStopRequested?.Invoke(this, EventArgs.Empty);

    private static string ShortcutOf(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys, CaptureKind kind) =>
        hotkeys.TryGetValue(kind, out var chord) ? HotkeyChordFormatter.Format(chord) : string.Empty;

    private List<TrayMenuItemViewModel> BuildItems()
    {
        var hotkeys = _hotkeys;
        List<TrayMenuItemViewModel> items =
        [
            new("Tray.CaptureRectangle", ShortcutOf(hotkeys, CaptureKind.Rectangle), new TrayCaptureCommand(this, CaptureKind.Rectangle), false, false),
            new("Tray.CaptureFreeform", ShortcutOf(hotkeys, CaptureKind.Freeform), new TrayCaptureCommand(this, CaptureKind.Freeform), false, false),
            new("Tray.CaptureWindow", ShortcutOf(hotkeys, CaptureKind.Window), new TrayCaptureCommand(this, CaptureKind.Window), false, false),
            new("Tray.CaptureFullScreen", ShortcutOf(hotkeys, CaptureKind.FullScreen), new TrayCaptureCommand(this, CaptureKind.FullScreen), false, false),
            new("Tray.OpenImage", string.Empty, new TrayOpenImageCommand(this), false, false),
            new("Tray.CaptureBar", string.Empty, new TrayToggleCaptureBarCommand(this), true, _captureBarVisible),
            new("Tray.Settings", string.Empty, new TraySettingsCommand(this), false, false),
            new("Tray.CheckForUpdates", string.Empty, new TrayCheckForUpdatesCommand(this), false, false),
            new("Tray.Exit", string.Empty, new TrayExitCommand(this), false, false),
        ];
        var startStop = _recordHotkeys is { } keys ? HotkeyChordFormatter.Format(keys.StartStop) : string.Empty;
        var pause = _recordHotkeys is { } both ? HotkeyChordFormatter.Format(both.Pause) : string.Empty;
        var afterOpenImage = items.FindIndex(item => item.TextKey == "Tray.OpenImage") + 1;
        if (_recording)
        {
            items.Insert(afterOpenImage, new(_paused ? "Tray.RecordResume" : "Tray.RecordPause", pause, new TrayRecordPauseCommand(this), false, false));
            items.Insert(afterOpenImage + 1, new("Tray.RecordStop", startStop, new TrayRecordStopCommand(this), false, false));
        }
        else
        {
            items.Insert(afterOpenImage, new("Tray.Record", startStop, new TrayRecordCommand(this), false, false));
        }

        if (_updateAvailable)
        {
            items.Insert(items.Count - 1, new("Tray.Update", string.Empty, new TrayUpdateCommand(this), false, false));
        }

        return items;
    }
}
