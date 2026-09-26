using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Presentation.Mvvm;
using Paper.ScreenWizzard.Presentation.Shared.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.Commands;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.Presentation.Shell.ViewModels;

/// <summary>Why the Settings window wants to close.</summary>
public sealed class SettingsClosedEventArgs : EventArgs
{
    public SettingsClosedEventArgs(bool saved)
    {
        Saved = saved;
    }

    /// <summary>True after Save (Enter), false after Cancel (Esc): the unsaved changes were dropped.</summary>
    public bool Saved { get; }
}

/// <summary>
/// The Settings window. DataContext of <c>SettingsWindow</c>, built by the composition root with the settings the shell
/// loaded. It edits a copy; nothing reaches the use case until Save, so Esc really discards.
/// </summary>
public sealed class SettingsViewModel : BindableBase, IDisposable
{
    private readonly ILocalizer _localizer;
    private AfterCaptureAction _afterCapture;
    private string _saveFolder;
    private ImageFormat _format;
    private int _jpgQuality;
    private int _delaySeconds;
    private bool _includeCursor;
    private FullScreenScope _fullScreenScope;
    private bool _startWithWindows;
    private AppLanguage _language;
    private AppTheme _theme;
    private NotificationMessage? _message;

    public SettingsViewModel(
        IShellInteractor shell,
        AppSettings settings,
        IFolderPickerService folderPicker,
        ISettingsPrompts prompts,
        IAppearanceService appearance,
        INotifications notifications,
        ILocalizer localizer,
        string systemCultureName)
    {
        _localizer = localizer;
        Current = settings;
        _afterCapture = settings.AfterCapture;
        _saveFolder = settings.SaveFolder;
        _format = settings.Format;
        _jpgQuality = settings.JpgQuality;
        _delaySeconds = settings.DelaySeconds;
        _includeCursor = settings.IncludeCursor;
        _fullScreenScope = settings.FullScreenScope;
        _startWithWindows = settings.StartWithWindows;
        _language = settings.Language;
        _theme = settings.Theme;

        var rows = new List<HotkeyRowViewModel>();
        foreach (var kind in Enum.GetValues<CaptureKind>())
        {
            rows.Add(new HotkeyRowViewModel(kind, settings.Hotkeys.TryGetValue(kind, out var chord) ? chord : default));
        }

        Hotkeys = rows;
        SaveCommand = new SaveSettingsCommand(this, shell, prompts, appearance, notifications, systemCultureName);
        CancelCommand = new CancelSettingsCommand(this);
        BrowseFolderCommand = new BrowseFolderCommand(this, folderPicker);
        _localizer.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Raised when the window should close; the window closes itself, the view model never holds it.</summary>
    public event EventHandler<SettingsClosedEventArgs>? CloseRequested;

    public IReadOnlyList<HotkeyRowViewModel> Hotkeys { get; }

    public SaveSettingsCommand SaveCommand { get; }

    public CancelSettingsCommand CancelCommand { get; }

    public BrowseFolderCommand BrowseFolderCommand { get; }

    /// <summary>The settings the use case last accepted; what the caller reads after the window closed as saved.</summary>
    public AppSettings Current { get; internal set; }

    public AfterCaptureAction AfterCapture
    {
        get => _afterCapture;
        set => SetProperty(ref _afterCapture, value);
    }

    public string SaveFolder
    {
        get => _saveFolder;
        set => SetProperty(ref _saveFolder, value);
    }

    public ImageFormat Format
    {
        get => _format;
        set
        {
            if (SetProperty(ref _format, value))
            {
                RaisePropertyChanged(nameof(IsJpg));
            }
        }
    }

    /// <summary>The quality box only means something for JPG, so it is disabled for PNG.</summary>
    public bool IsJpg => _format == ImageFormat.Jpg;

    /// <summary>Kept in 1..100: a typed value outside it is pulled back instead of being sent to the encoder.</summary>
    public int JpgQuality
    {
        get => _jpgQuality;
        set => SetProperty(ref _jpgQuality, Math.Clamp(value, SettingsRules.JpgQualityMin, SettingsRules.JpgQualityMax));
    }

    public int DelaySeconds
    {
        get => _delaySeconds;
        set => SetProperty(ref _delaySeconds, value);
    }

    public bool IncludeCursor
    {
        get => _includeCursor;
        set => SetProperty(ref _includeCursor, value);
    }

    public FullScreenScope FullScreenScope
    {
        get => _fullScreenScope;
        set => SetProperty(ref _fullScreenScope, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public AppLanguage Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public AppTheme Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    /// <summary>The reason the last Save stopped, in the language in use; empty when there is none.</summary>
    public string MessageText => _message is null ? string.Empty : _localizer.Format(_message);

    public bool HasMessage => _message is not null;

    /// <summary>Puts a use-case message in the window (or clears it with null).</summary>
    internal void ShowMessage(NotificationMessage? message)
    {
        _message = message;
        RaisePropertyChanged(nameof(MessageText));
        RaisePropertyChanged(nameof(HasMessage));
    }

    /// <summary>
    /// The settings as edited now. The hotkeys and "start with Windows" stay as the use case last accepted them: each of those
    /// has its own step in Save, with its own way to be refused (SPEC shell F3, F4).
    /// </summary>
    internal AppSettings BuildCandidate() => Current with
    {
        AfterCapture = _afterCapture,
        SaveFolder = _saveFolder,
        Format = _format,
        JpgQuality = _jpgQuality,
        DelaySeconds = _delaySeconds,
        IncludeCursor = _includeCursor,
        FullScreenScope = _fullScreenScope,
        Language = _language,
        Theme = _theme,
    };

    internal void RequestClose(bool saved) => CloseRequested?.Invoke(this, new SettingsClosedEventArgs(saved));

    public void Dispose() => _localizer.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => RaisePropertyChanged(nameof(MessageText));
}
