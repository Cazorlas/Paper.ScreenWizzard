using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Paper.ScreenWizzard.Domain.Capture;
using Paper.ScreenWizzard.Domain.Shared;
using Paper.ScreenWizzard.Domain.Shell;
using Paper.ScreenWizzard.Infrastructure.Shell;
using Paper.ScreenWizzard.Presentation.Capture.ViewModels;
using Paper.ScreenWizzard.Presentation.Editor.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.ViewModels;
using Paper.ScreenWizzard.Presentation.Shell.Views;
using Paper.ScreenWizzard.UseCases.Capture.Ports;
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

    public AppShell(
        StartupOptions options,
        IShellInteractor shell,
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
        SettingsHolder settings,
        IFolderPickerService folderPicker,
        ISettingsPrompts prompts,
        Action shutdown)
    {
        _options = options;
        _shell = shell;
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

        var started = _shell.Start(new ShellStartInput(first, picturesFolder, cultureName, monitors, first ? MeasureCaptureBar(monitors) : default));
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
        _captureFlow.EditRequested += image => _editorFlow.Open(image, null);

        _trayMenu = new TrayMenuViewModel(started.Settings.Hotkeys, false);
        _trayMenu.CaptureRequested += StartCapture;
        _trayMenu.OpenImageRequested += (_, _) => OpenImage();
        _trayMenu.ToggleCaptureBarRequested += (_, _) => ToggleCaptureBar();
        _trayMenu.SettingsRequested += (_, _) => OpenSettings();
        _trayMenu.ExitRequested += (_, _) => Exit();
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

    public void Exit()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        SaveCaptureBarPosition();
        _shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
