# Paper.ScreenWizzard.App

The entry host, the exe `Paper.ScreenWizzard.exe`: no main window, the tray icon is the app. It builds the container, starts the shell and
ends only through "Exit" in the tray menu. It is the one project that references every layer. Started with `--autostart` it shows only
the tray icon; the environment variables `PAPER_SCREENWIZZARD_DATA` and `PAPER_SCREENWIZZARD_INSTANCE` move its data and its OS names.

## Map

| File | Open it for |
| --- | --- |
| `App.xaml`, `App.xaml.cs` | `OnStartup`: options, container, second copy leaves with code 0, unhandled-error reporting |
| `Startup/CompositionRoot.cs` | which adapter implements which port, and the view-model and service graph |
| `Startup/StartupOptions.cs` | the flag and the two environment variables |
| `Startup/AppShell.cs` | the run of the app: start, hotkeys to captures, the bar, Settings, Open image, exit |
| `Startup/TrayIcon.cs`, `TrayIconImage.cs` | the `NotifyIcon` and its icon (the exe's own, `app.ico`) |
| `Startup/SettingsHolder.cs` | the settings the running app uses |
| `Startup/NativeMethods.cs` | the few Win32 calls the shell needs (bar position) |
| `Startup/AppStrings.en.xaml`, `AppStrings.vi.xaml` | the texts of the crash box and the open-image dialog |
| `app.manifest` | per-monitor v2 DPI, `asInvoker` |

## Flow

`App.OnStartup` -> `CompositionRoot.Build` -> `AppShell.Start` -> `IShellInteractor.Start` -> hotkey `Pressed` ->
`AppShell.StartCapture` -> `CaptureFlow.StartAsync` (Presentation). Exit: tray "Exit" -> `AppShell.Exit` -> `App.OnExit` disposes the tray, the hotkeys and the mutex.
