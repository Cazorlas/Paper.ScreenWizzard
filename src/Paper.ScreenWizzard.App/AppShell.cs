using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Recorder;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.Presentation.Recorder.ViewModels;
using Paper.ScreenWizzard.Presentation.Recorder.Views;
using Paper.ScreenWizzard.Presentation.Shared.Views;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.Views;
using Paper.ScreenWizzard.UseCases.Recorder.Models;
using Paper.ScreenWizzard.UseCases.Shared.Models;
using Paper.ScreenWizzard.UseCases.Shared.Ports;
using Paper.ScreenWizzard.UseCases.Shell.Models;
using Paper.ScreenWizzard.UseCases.Shell.Ports;

namespace Paper.ScreenWizzard.App;

/// <summary>
/// The running app: the one place that connects the pieces. It decides nothing - <see cref="IShellInteractor"/> answers every question of
/// start-up, settings, hotkeys and placement - it only builds the windows, listens to their events and the hotkeys, and calls the capture and
/// editor flows. Everything runs on the UI thread; the one event that arrives on another thread (a second launch) is marshalled first.
/// </summary>
public sealed class AppShell : IDisposable
{
    public const string AutostartFlag = AutostartService.AutostartFlag;

    private readonly StartupOptions _options;
    private readonly IShellInteractor _shell;
    private readonly IUpdateInteractor _updates;
    private readonly IMonitorCatalog _monitors;
    private readonly IHotkeys _hotkeys;
    private readonly ISingleInstance _singleInstance;
    private readonly INotifications _notifications;
    private readonly LanguageService _language;
    private readonly ThemeService _theme;
    private readonly IAppearanceService _appearance;
    private readonly ILog _log;
    private readonly CaptureFlow _captureFlow;
    private readonly EditorFlow _editorFlow;
    private readonly RecordingFlow _recording;
    private readonly SettingsHolder _settings;
    private readonly IFolderPickerService _folderPicker;
    private readonly ISettingsPrompts _prompts;
    private readonly Action _shutdown;
    private ResourceDictionary? _appStrings;
    private TrayMenuViewModel? _trayMenu;
    private TrayIcon? _tray;
    private CaptureBarWindow? _bar;
    private SettingsWindow? _settingsWindow;
    private bool _exiting;
    private bool _disposed;
    private bool _reportingFailure;
    private DispatcherTimer? _updateTimer;
    private DispatcherTimer? _recordingTicker;
    private RecordingBarWindow? _recordingBar;
    private bool _captureBarBeforeRecording;
    private bool _recordingBarBeforeRecording;
    private UpdateOffer? _update;

    public AppShell(
        StartupOptions options,
        IShellInteractor shell,
        IUpdateInteractor updates,
        IMonitorCatalog monitors,
        IHotkeys hotkeys,
        ISingleInstance singleInstance,
        INotifications notifications,
        LanguageService language,
        ThemeService theme,
        IAppearanceService appearance,
        ILog log,
        CaptureFlow captureFlow,
        EditorFlow editorFlow,
        RecordingFlow recording,
        SettingsHolder settings,
        IFolderPickerService folderPicker,
        ISettingsPrompts prompts,
        Action shutdown)
    {
        _options = options;
        _shell = shell;
        _updates = updates;
        _monitors = monitors;
        _hotkeys = hotkeys;
        _singleInstance = singleInstance;
        _notifications = notifications;
        _language = language;
        _theme = theme;
        _appearance = appearance;
        _log = log;
        _captureFlow = captureFlow;
        _editorFlow = editorFlow;
        _recording = recording;
        _settings = settings;
        _folderPicker = folderPicker;
        _prompts = prompts;
        _shutdown = shutdown;
    }

    /// <summary>
    /// Starts the app. Returns false when this is not the first copy: the first one has been told, and the caller exits without a window.
    /// </summary>
    public bool Start()
    {
        var first = _singleInstance.TryBecomeFirstInstance();
        var cultureName = CultureInfo.CurrentUICulture.Name;

        // The language and the theme of the system are in place before the first window or message, so nothing is ever drawn without text.
        _language.Apply(_shell.ResolveLanguage(AppLanguage.System, cultureName));
        _theme.Apply(AppTheme.System);
        _language.LanguageChanged += (_, _) => SwapAppStrings();
        SwapAppStrings();

        var monitors = _monitors.GetMonitors();
        var picturesFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(picturesFolder))
        {
            picturesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
        }

        var videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var started = _shell.Start(new ShellStartInput(
            first,
            picturesFolder,
            cultureName,
            monitors,
            first ? MeasureCaptureBar(monitors) : default,
            string.IsNullOrWhiteSpace(videosFolder) ? null : videosFolder));
        if (!started.ContinueRunning)
        {
            // A second copy: tell the first one and leave. No window, no message (SPEC shell F6).
            _singleInstance.NotifyFirstInstance();
            return false;
        }

        _settings.Current = started.Settings;
        _appearance.ApplyLanguage(started.Language);
        _appearance.ApplyTheme(started.Settings.Theme);
        _singleInstance.SecondInstanceLaunched += OnSecondInstanceLaunched;
        _hotkeys.Pressed += OnHotkeyPressed;
        _hotkeys.RecordPressed += OnRecordHotkeyPressed;
        _recording.Began += (_, _) => StepAsideForRecording();
        _recording.Ended += (_, _) => ComeBackAfterRecording();
        _recording.StateChanged += (_, _) => ShowRecordingState();
        _recording.AreaRefused += message => OpenRecordingBar(message);
        _captureFlow.EditRequested += image => _editorFlow.Open(image, null);

        _trayMenu = new TrayMenuViewModel(started.Settings.Hotkeys, false);
        _trayMenu.CaptureRequested += StartCapture;
        _trayMenu.OpenImageRequested += (_, _) => OpenImage();
        _trayMenu.ToggleCaptureBarRequested += (_, _) => ToggleCaptureBar();
        _trayMenu.SettingsRequested += (_, _) => OpenSettings();
        _trayMenu.ExitRequested += (_, _) => Exit();
        _trayMenu.UpdateRequested += (_, _) => OpenUpdatePage();
        _trayMenu.CheckForUpdatesRequested += async (_, _) => await CheckForUpdateNowAsync();
        _trayMenu.RecordRequested += (_, _) => OpenRecordingBar(null);
        _trayMenu.RecordPauseRequested += (_, _) => _recording.TogglePause();
        _trayMenu.RecordStopRequested += async (_, _) => await StopRecordingAsync();
        _trayMenu.SetRecordHotkeys(started.Settings.RecordHotkeys);
        _tray = new TrayIcon(_trayMenu, _language, ShowCaptureBar);

        // What went wrong at start (a corrupt settings file, a hotkey another program holds) is said once the language is right.
        foreach (var notice in started.Notices)
        {
            // A hotkey that is not available does not stop anything else, and on some PCs it is so at every start (Windows or a
            // screenshot tool holds Alt+PrintScreen): a box to close each time is the wrong size of message, so it is a toast.
            // A settings file that was corrupt or unreadable is different: the user's choices are gone or at stake, so that stays a box.
            if (notice.Key is "Shell.HotkeyUnavailableAtStart" or "Shell.HotkeyUnsafeAtStart")
            {
                _notifications.ShowToast(notice);
            }
            else
            {
                _notifications.ShowError(notice);
            }
        }

        if (started.ShowCaptureBar && !_options.Autostart)
        {
            ShowCaptureBar(started.CaptureBarPosition);
        }

        StartUpdateChecks();
        return true;
    }

    /// <summary>Logs an exception nobody caught and tells the user, once at a time, without ending the app (SPEC: no silent death).</summary>
    public void ReportFailure(Exception exception, string where)
    {
        _log.Error("Unhandled error in " + where, exception);
        if (_reportingFailure)
        {
            return;
        }

        _reportingFailure = true;
        try
        {
            _notifications.ShowError(NotificationMessage.Of("App.UnhandledError", exception.Message));
        }
        catch (Exception nested)
        {
            _log.Error("The error box itself failed", nested);
        }
        finally
        {
            _reportingFailure = false;
        }
    }

    public async void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        SaveCaptureBarPosition();

        // A recording is stopped and saved first (SPEC recorder, "Pause and stop"); one that is already finishing by itself (F1, F2) is
        // waited for, so the process never ends half way through writing the file, and a countdown is called off.
        if (_recording.State != RecorderState.Idle)
        {
            try
            {
                await _recording.StopAsync();
            }
            catch (Exception exception)
            {
                _log.Error("The recording could not be stopped at exit", exception);
            }
        }

        _shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _updateTimer?.Stop();
        _recordingTicker?.Stop();
        _tray?.Dispose();
        if (_hotkeys is IDisposable hotkeys)
        {
            hotkeys.Dispose();
        }

        (_singleInstance as IDisposable)?.Dispose();
    }

    // The bar's size in physical pixels, needed before the start result can place it. It is measured, not shown: no window may appear at
    // logon (--autostart) and the size decides where the first one goes.
    private PixelSize MeasureCaptureBar(IReadOnlyList<MonitorInfo> monitors)
    {
        var window = new CaptureBarWindow(new CaptureBarViewModel());
        try
        {
            var content = (FrameworkElement)window.Content;
            content.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            var scale = primary is null || primary.Dpi <= 0 ? 1.0 : primary.Dpi / 96.0;
            return new PixelSize(
                (int)Math.Ceiling(content.DesiredSize.Width * scale),
                (int)Math.Ceiling(content.DesiredSize.Height * scale));
        }
        finally
        {
            window.Close();
        }
    }

    private void SwapAppStrings()
    {
        var code = _language.Current == ResolvedLanguage.Vietnamese ? "vi" : "en";
        var next = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Paper.ScreenWizzard;component/AppStrings.{code}.xaml"),
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Add(next);
        if (_appStrings is not null)
        {
            merged.Remove(_appStrings);
        }

        _appStrings = next;
    }

    // ---- the capture bar ----

    private void ToggleCaptureBar()
    {
        if (_bar is { } bar)
        {
            bar.Close();
        }
        else
        {
            ShowCaptureBar();
        }
    }

    private void ShowCaptureBar() => ShowCaptureBar(null);

    private void ShowCaptureBar(PixelPoint? position)
    {
        if (_bar is { } existing)
        {
            existing.Show();
            existing.Activate();
            return;
        }

        var viewModel = new CaptureBarViewModel();
        viewModel.CaptureRequested += StartCapture;
        viewModel.SettingsRequested += (_, _) => OpenSettings();
        var bar = new CaptureBarWindow(viewModel) { ShowActivated = false };
        _bar = bar;

        // Where it goes: the place the caller computed at start, else where the shell says now (saved place, or the primary monitor's top right).
        var monitors = _monitors.GetMonitors();
        var place = position ?? _shell.PlaceCaptureBar(_settings.Current.CaptureBarPosition, monitors, MeasureCaptureBar(monitors));
        bar.SourceInitialized += (_, _) =>
            NativeMethods.SetWindowPos(new WindowInteropHelper(bar).Handle, IntPtr.Zero, place.X, place.Y, 0, 0, NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        // The place is read while the window still exists: once it is Closed its handle is gone and GetWindowRect fails, which is how a
        // bar dragged and then closed with X kept its old place across an exit (defect D2, found by driving the exe).
        bar.Closing += (_, _) =>
        {
            if (ReferenceEquals(_bar, bar))
            {
                SaveCaptureBarPosition();
            }
        };
        bar.Closed += (_, _) =>
        {
            if (ReferenceEquals(_bar, bar))
            {
                _bar = null;
            }

            _trayMenu?.SetCaptureBarVisible(false);
        };
        bar.Show();
        _trayMenu?.SetCaptureBarVisible(true);
    }

    // Kept for the next start: where the user dragged the bar (SPEC shell). Written only when it moved, so closing the bar does not rewrite the file.
    private void SaveCaptureBarPosition()
    {
        if (_bar is not { } bar || !NativeMethods.GetWindowRect(new WindowInteropHelper(bar).Handle, out var rect))
        {
            return;
        }

        var place = new PixelPoint(rect.Left, rect.Top);
        if (_settings.Current.CaptureBarPosition == place)
        {
            return;
        }

        var result = _shell.Apply(_settings.Current with { CaptureBarPosition = place });
        _settings.Current = result.Settings;
        if (!result.Saved && result.Message is not null)
        {
            _notifications.ShowError(result.Message);
        }
    }

    // ---- capture ----

    // While Settings is open a press of a chord this app holds is most likely someone typing it into a hotkey box: Windows hands it to the
    // app instead of the box, and starting a capture under the window would be the wrong answer.
    private void OnHotkeyPressed(CaptureKind kind)
    {
        if (_settingsWindow is not null)
        {
            return;
        }

        StartCapture(kind);
    }

    private async void StartCapture(CaptureKind kind)
    {
        // The bar is not part of a screenshot: it steps aside until the snapshot is taken (the flow returns once the overlay is up).
        var restoreBar = _bar is { IsVisible: true } bar ? bar : null;
        restoreBar?.Hide();
        var restoreRecordingBar = _recordingBar is { IsVisible: true } recordingBar ? recordingBar : null;
        restoreRecordingBar?.Hide();
        try
        {
            await _captureFlow.StartAsync(kind);
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "capture " + kind);
        }
        finally
        {
            if (restoreBar is not null && ReferenceEquals(_bar, restoreBar) && !_exiting)
            {
                restoreBar.Show();
            }

            if (restoreRecordingBar is not null && ReferenceEquals(_recordingBar, restoreRecordingBar) && !_exiting && !_recording.IsBusy)
            {
                restoreRecordingBar.Show();
            }
        }
    }

    // ---- settings ----

    private void OpenSettings()
    {
        if (_settingsWindow is { } open)
        {
            open.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(
            _shell,
            _settings.Current,
            _folderPicker,
            _prompts,
            _appearance,
            _notifications,
            _language,
            CultureInfo.CurrentUICulture.Name);
        var window = new SettingsWindow(viewModel);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            // Whatever the use case accepted is real by now - a hotkey Windows took is registered and saved - so it is what the app uses,
            // whether the window closed with Save or with Esc.
            _settings.Current = viewModel.Current;
            _trayMenu?.SetHotkeys(viewModel.Current.Hotkeys);
            _trayMenu?.SetRecordHotkeys(viewModel.Current.RecordHotkeys);
            _settingsWindow = null;
        };
        window.Show();
        window.Activate();
    }

    // ---- images from disk ----

    private void OpenImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = _language.GetString("App.OpenImage.Title"),
            Filter = _language.GetString("App.OpenImage.Filter"),
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var path in dialog.FileNames)
        {
            _editorFlow.OpenFile(path);
        }
    }

    // ---- recording ----

    // The recording bar (SPEC recorder, "What the user does" 1 and 2): one at a time; opened again, it comes forward. A message is why the
    // last Record did not go on (F7).
    private void OpenRecordingBar(NotificationMessage? message)
    {
        if (!_recording.MayOpenBar)
        {
            return;
        }

        if (_recordingBar is { } open)
        {
            (open.DataContext as RecordingBarViewModel)?.ShowMessage(message);
            open.Show();
            open.Activate();
            return;
        }

        var viewModel = new RecordingBarViewModel(_settings.Current.Recorder, _monitors.GetMonitors(), _language);
        viewModel.ShowMessage(message);
        viewModel.RecordRequested += choices => StartRecording(choices);
        var bar = new RecordingBarWindow(viewModel) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        _recordingBar = bar;
        bar.Closed += (_, _) =>
        {
            if (ReferenceEquals(_recordingBar, bar))
            {
                _recordingBar = null;
            }
        };
        bar.Show();
        bar.Activate();
    }

    // The bar's choices are kept for the next recording and the next start (SPEC recorder, Inputs), then the flow records with them.
    private async void StartRecording(RecorderSettings choices)
    {
        try
        {
            if (_shell.KeepRecorderChoices(_settings.Current, choices) is { } kept)
            {
                _settings.Current = kept.Settings;
                if (!kept.Saved && kept.Message is not null)
                {
                    _notifications.ShowError(kept.Message);
                }
            }

            await _recording.StartAsync(choices);
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "recording");
        }
    }

    private async void OnRecordHotkeyPressed(RecordHotkey key)
    {
        try
        {
            if (key == RecordHotkey.Pause)
            {
                _recording.TogglePause();
            }
            else
            {
                await _recording.StartOrStopAsync(_settings.Current.Recorder, mayStart: _settingsWindow is null);
            }
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "recording hotkey");
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            await _recording.StopAsync();
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "stop recording");
        }
    }

    // While recording nothing of the app stays on screen (SPEC recorder, "What the user does" 3, as FastStone does).
    private void StepAsideForRecording()
    {
        _captureBarBeforeRecording = _bar is { IsVisible: true };
        _recordingBarBeforeRecording = _recordingBar is { IsVisible: true };
        _bar?.Hide();
        _recordingBar?.Hide();
    }

    private void ComeBackAfterRecording()
    {
        if (_exiting)
        {
            return;
        }

        if (_captureBarBeforeRecording)
        {
            _bar?.Show();
        }

        if (_recordingBarBeforeRecording)
        {
            _recordingBar?.Show();
        }
    }

    // The tray tells a recording: a red icon, the time recorded in its tooltip, Pause and Stop in its menu (SPEC recorder, "What the user does" 4).
    private void ShowRecordingState()
    {
        var recording = _recording.IsRecording;
        var paused = _recording.State == RecorderState.Paused;
        _trayMenu?.SetRecording(recording, paused);
        if (recording)
        {
            _recordingTicker ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => ShowRecordingTime(), Dispatcher.CurrentDispatcher);
            _recordingTicker.Start();
            ShowRecordingTime();
        }
        else
        {
            _recordingTicker?.Stop();
            _tray?.ShowRecording(null);
        }
    }

    private void ShowRecordingTime()
    {
        if (!_recording.IsRecording)
        {
            return;
        }

        var paused = _recording.State == RecorderState.Paused;
        _tray?.ShowRecording(RecordingTimeText.Tooltip(_language, paused, _recording.Elapsed));
        _trayMenu?.SetRecording(true, paused);
    }

    // ---- a new version ----

    // A minute after start, then once a day (UpdateRules); whether to ask GitHub at all and what counts as newer is the use case's.
    private void StartUpdateChecks()
    {
        var timer = new DispatcherTimer { Interval = UpdateRules.FirstCheckDelay };
        timer.Tick += async (_, _) =>
        {
            timer.Interval = UpdateRules.CheckInterval;
            await CheckForUpdateAsync();
        };
        _updateTimer = timer;
        timer.Start();
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var result = await _updates.CheckAsync(_settings.Current, RunningVersion(), CancellationToken.None);
            if (_exiting || result.Offer is not { } offer)
            {
                return;
            }

            _update = offer;
            _trayMenu?.SetUpdateAvailable(true);
            if (result.Notice is { } notice)
            {
                _tray?.ShowNotice(_language.GetString("Shell.UpdateAvailable.Title"), _language.Format(notice), OpenUpdatePage);
            }
        }
        catch (Exception exception)
        {
            // The check is a courtesy: whatever went wrong is logged, and the user is not interrupted for it.
            _log.Error("The update check failed", exception);
        }
    }

    // "Kiểm bản mới" in the tray: asked now, and the answer is always said - newer (click to open its page), newest, or why not.
    private async Task CheckForUpdateNowAsync()
    {
        try
        {
            var result = await _updates.CheckNowAsync(RunningVersion(), CancellationToken.None);
            if (_exiting || result.Notice is not { } notice)
            {
                return;
            }

            Action? clicked = null;
            var title = _language.GetString("Shell.UpdateCheck.Title");
            if (result.Offer is { } offer)
            {
                _update = offer;
                _trayMenu?.SetUpdateAvailable(true);
                clicked = OpenUpdatePage;
                title = _language.GetString("Shell.UpdateAvailable.Title");
            }

            _tray?.ShowNotice(title, _language.Format(notice), clicked);
        }
        catch (Exception exception)
        {
            ReportFailure(exception, "check for updates");
        }
    }

    private static string? RunningVersion() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private void OpenUpdatePage()
    {
        if (_update is not { } offer)
        {
            return;
        }

        var message = _updates.OpenDownloadPage(offer);
        if (message is not null)
        {
            _notifications.ShowError(message);
        }
    }

    // ---- a second launch ----

    // Raised on the waiting thread of the single-instance adapter: move to the UI thread before touching a window.
    private void OnSecondInstanceLaunched()
    {
        var dispatcher = Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            try
            {
                if (_shell.OnSecondInstanceLaunched().ShowCaptureBar)
                {
                    ShowCaptureBar();
                }
            }
            catch (Exception exception)
            {
                ReportFailure(exception, "second launch");
            }
        });
    }
}
