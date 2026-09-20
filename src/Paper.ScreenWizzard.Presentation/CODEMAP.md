# Paper.ScreenWizzard.Presentation

Everything the user sees, on `net10.0-windows` with WPF: windows, view models, named commands, the light/dark themes and the vi/en strings.
It references UseCases and Domain but not Infrastructure (ADR 0001), so a view model reaches the adapters only through UseCases interfaces.
The three flows below open and close windows around what the interactors answer.

## Map

| Folder / file | Open it for |
| --- | --- |
| `Mvvm/` | `BindableBase`, `CommandBase`, `AsyncCommandBase`: the small MVVM base this repo owns |
| `Resources/Themes/Light.xaml`, `Dark.xaml`, `Controls.xaml`, `Icons.xaml` | brushes, control templates (two-ring focus), icon geometry |
| `Resources/Strings.vi.xaml`, `Strings.en.xaml` | every user text and every `NotificationMessage` key |
| `ViewModels/Shell/` | capture bar, settings (`SettingsViewModel`), tray menu, `LanguageService` contract |
| `ViewModels/Capture/CaptureFlow.cs` | the whole capture run: countdown, overlay, dialog, delivery |
| `ViewModels/Capture/SelectionOverlayViewModel.cs`, `DisplayUnits.cs` | the drag on the frozen snapshot; display units to physical pixels |
| `ViewModels/Capture/CaptureDoneViewModel.cs` | the five buttons of the "Đã chụp" dialog |
| `ViewModels/Editor/EditorViewModel.cs`, `EditorFlow.cs` | tools, zoom, save/copy/close, open; `EditorCoordinates.cs` view-to-image maths |
| `Rendering/AnnotationRenderer.cs`, `WpfImageFlattener.cs` | drawing annotations and producing the flattened `PixelImage` |
| `Views/Shell/`, `Views/Capture/`, `Views/Editor/` | the windows; `Services/` folders hold the WPF services (theme, language, dialogs) |
| `Views/Capture/PhysicalWindowPlacer.cs` | the only place that positions a window in physical pixels |
| `Views/Editor/EditorWindowPlacement.cs` | keeps the editor inside the monitor that holds the pointer |

## Flow

Capture: `CaptureFlow.StartAsync` -> `ICaptureInteractor.BeginAsync` -> `SelectionOverlayViewModel` (`WpfCaptureViews.OpenSelection`) ->
`CaptureFlow.HandleCaptured` -> `CaptureDoneViewModel` or `EditorFlow`.
Editor: `EditorFlow` -> `EditorWindow` / `EditorViewModel` -> `IEditorInteractor` -> `WpfImageFlattener.Flatten` when saving or copying.
Shell: `SettingsViewModel` -> `IShellInteractor.Apply` -> `LanguageService`, `ThemeService`.
