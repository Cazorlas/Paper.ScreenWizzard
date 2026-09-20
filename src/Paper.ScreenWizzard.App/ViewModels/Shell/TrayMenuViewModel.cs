using System.Windows.Input;
using Paper.ScreenWizzard.App.Mvvm;
using Paper.ScreenWizzard.App.ViewModels.Shell.Commands;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shell;

namespace Paper.ScreenWizzard.App.ViewModels.Shell;

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
    private IReadOnlyList<TrayMenuItemViewModel> _items;
    private bool _captureBarVisible;

    public TrayMenuViewModel(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys, bool captureBarVisible)
    {
        _captureBarVisible = captureBarVisible;
        _items = BuildItems(hotkeys);
    }

    /// <summary>A capture line was chosen.</summary>
    public event Action<CaptureKind>? CaptureRequested;

    public event EventHandler? OpenImageRequested;

    /// <summary>The "Thanh chụp" line was chosen; the owner shows or hides the bar and calls <see cref="SetCaptureBarVisible"/>.</summary>
    public event EventHandler? ToggleCaptureBarRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    /// <summary>Capture x4, Open image, Capture bar, Settings, Exit, in that order.</summary>
    public IReadOnlyList<TrayMenuItemViewModel> Items => _items;

    /// <summary>Redraws the shortcut texts after a hotkey changed in Settings.</summary>
    public void SetHotkeys(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys)
    {
        _items = BuildItems(hotkeys);
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

    private static string ShortcutOf(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys, CaptureKind kind) =>
        hotkeys.TryGetValue(kind, out var chord) ? HotkeyChordFormatter.Format(chord) : string.Empty;

    private List<TrayMenuItemViewModel> BuildItems(IReadOnlyDictionary<CaptureKind, HotkeyChord> hotkeys) =>
    [
        new("Tray.CaptureRectangle", ShortcutOf(hotkeys, CaptureKind.Rectangle), new TrayCaptureCommand(this, CaptureKind.Rectangle), false, false),
        new("Tray.CaptureFreeform", ShortcutOf(hotkeys, CaptureKind.Freeform), new TrayCaptureCommand(this, CaptureKind.Freeform), false, false),
        new("Tray.CaptureWindow", ShortcutOf(hotkeys, CaptureKind.Window), new TrayCaptureCommand(this, CaptureKind.Window), false, false),
        new("Tray.CaptureFullScreen", ShortcutOf(hotkeys, CaptureKind.FullScreen), new TrayCaptureCommand(this, CaptureKind.FullScreen), false, false),
        new("Tray.OpenImage", string.Empty, new TrayOpenImageCommand(this), false, false),
        new("Tray.CaptureBar", string.Empty, new TrayToggleCaptureBarCommand(this), true, _captureBarVisible),
        new("Tray.Settings", string.Empty, new TraySettingsCommand(this), false, false),
        new("Tray.Exit", string.Empty, new TrayExitCommand(this), false, false),
    ];
}
