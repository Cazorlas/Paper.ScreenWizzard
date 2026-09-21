# Paper.ScreenWizzard

A Windows desktop app: capture the screen four ways, edit the image, later record the screen and note on it. Five source projects, one
per layer of ADR 0001 (alternative A), and three test projects. The user reaches it through the tray icon or a global hotkey.

## Map

| Project | Open it for |
| --- | --- |
| `src/Paper.ScreenWizzard.Domain` | pure rules and value types, no Windows |
| `src/Paper.ScreenWizzard.UseCases` | every decision: interactors, ports, records; no Windows |
| `src/Paper.ScreenWizzard.Infrastructure` | Win32/GDI/WPF adapters behind the ports |
| `src/Paper.ScreenWizzard.Presentation` | windows, view models, themes, strings; no Infrastructure |
| `src/Paper.ScreenWizzard.App` | the exe: tray, composition root |
| `tests/Paper.ScreenWizzard.UnitTests` | rules and the architecture tests (`Architecture/LayerTests.cs`) |
| `tests/Paper.ScreenWizzard.UiTests` | views on mock data, driven by FlaUI |
| `tests/Paper.ScreenWizzard.E2eTests` | the real adapters and the real exe on the desktop |
| `installer` | the release files: the installer script (`Setup.iss`, English and Vietnamese), `build-package.ps1`, `verify-installer.ps1`, `get-inno.ps1`, `make-icon.ps1`; the workflow is `.github/workflows/release.yml` |

## Flow

`App.OnStartup` -> `CompositionRoot.Build` -> `AppShell.Start`; a capture goes `CaptureFlow.StartAsync` -> `CaptureInteractor.BeginAsync` ->
`CaptureSession` -> the "Đã chụp" dialog -> `ImageDelivery` or `EditorFlow`. Each project has its own map beside its `csproj`.
