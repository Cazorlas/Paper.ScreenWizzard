# Paper.ScreenWizzard.App

The entry host, the exe `Paper.ScreenWizzard.exe`: no main window, the tray icon is the app. It builds the container, starts the shell and
ends only through "Exit" in the tray menu. It is the one project that references every layer, and it has no folders: every file sits at its root (ADR 0003). Started with `--autostart` it shows only
the tray icon; the environment variables `PAPER_SCREENWIZZARD_DATA` and `PAPER_SCREENWIZZARD_INSTANCE` move its data and its OS names.

## Map

| File | Open it for |
| --- | --- |
| `App.xaml`, `App.xaml.cs` | `OnStartup`: options, container, second copy leaves with code 0, unhandled-error reporting |
| `CompositionRoot.cs` | which adapter implements which port, and the view-model and service graph |
| `StartupOptions.cs` | the flag and the two environment variables |
| `AppShell.cs` | the run of the app: start, hotkeys to captures, the bar, Settings, Open image, exit |
| `TrayIcon.cs`, `TrayIconImage.cs` | the `NotifyIcon` and its icon (the exe's own, `app.ico`) |
| `SettingsHolder.cs` | the settings the running app uses |
| `NativeMethods.cs` | the few Win32 calls the shell needs (bar position) |
| `AppStrings.en.xaml`, `AppStrings.vi.xaml` | the texts of the crash box and the open-image dialog |
| `app.manifest` | per-monitor v2 DPI, `asInvoker` |

## Flow

`App.OnStartup` -> `CompositionRoot.Build` -> `AppShell.Start` -> `IShellInteractor.Start` -> hotkey `Pressed` ->
`AppShell.StartCapture` -> `CaptureFlow.StartAsync` (Presentation). Exit: tray "Exit" -> `AppShell.Exit` -> `App.OnExit` disposes the tray, the hotkeys and the mutex.
