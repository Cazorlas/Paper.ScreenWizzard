# Paper.ScreenWizzard.Infrastructure

The adapters behind the UseCases ports, on `net10.0-windows` with WPF: GDI capture, the window and monitor lists, global hotkeys, the
clipboard, image codecs, files, settings, start-with-Windows, the one-instance guard, the log. Plain data crosses every port; no
decision is made here. Each adapter sits in the folder of its port's domain (ADR 0003). Every OS-visible name (data folder, mutex, Run value) comes from the constructor so tests never touch the real ones.

## Map

| File | Open it for |
| --- | --- |
| `NativeMethods.cs` | every P/Invoke of the project (GDI, DWM, monitors, cursor, hotkey) |
| `Capture/ScreenSource.cs` | `BitBlt` of the virtual desktop into a DIB, the cursor drawn in |
| `Capture/WindowCatalog.cs` | top-level windows top to bottom with the DWM visible frame |
| `Capture/TaskDelay.cs` | the countdown's wait |
| `Shell/HotkeyService.cs` | `RegisterHotKey` through a message-only window; atomic re-register, F12 refused |
| `Shell/SettingsStore.cs` | `settings.json` read as written (missing settings null), the `.bak` of a file it cannot read or the use case calls broken, the path in every failure |
| `Shell/AutostartService.cs` | the HKCU Run value |
| `Shell/SingleInstance.cs` | the named mutex and the wake event |
| `Shared/ClipboardService.cs` | set and get an image, with the retries of a busy clipboard |
| `Shared/ImageCodec.cs` | decode PNG/JPG/BMP (size checked first), encode PNG/JPG |
| `Shared/MonitorCatalog.cs` | monitors with DPI, the pointer, the layout signature |
| `Shared/FileStore.cs`, `FileLogger.cs`, `SystemClock.cs` | the small ports |

## Flow

`CompositionRoot.Build` (App) constructs each adapter and registers it as its port; a port call goes straight to the class above,
for example `IScreenSource.Capture` -> `ScreenSource.Capture` -> `NativeMethods` -> a `PixelImage`.
