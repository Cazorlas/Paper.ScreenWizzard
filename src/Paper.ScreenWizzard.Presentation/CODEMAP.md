# Paper.ScreenWizzard.Presentation

Everything the user sees, on `net10.0-windows` with WPF: windows, view models, named commands, the light/dark themes and the vi/en strings.
It references UseCases and Domain but not Infrastructure (ADR 0001), so a view model reaches the adapters only through UseCases interfaces.
The three flows below open and close windows around what the interactors answer. Each domain folder holds `ViewModels/`,
`Commands/` and `Views/` (ADR 0003), and `Shared/` what two domains use: `ILocalizer`, the file dialog, the toast and error
windows, the converters, the title-bar theme, physical-pixel placement. `Mvvm/` and `Resources/` are the two first-level folders that
are not a domain.

## Map

| Folder / file | Open it for |
| --- | --- |
| `Mvvm/` | `BindableBase`, `CommandBase`, `AsyncCommandBase`: the small MVVM base this repo owns |
| `Resources/Themes/Light.xaml`, `Dark.xaml`, `Controls.xaml`, `Icons.xaml` | brushes, control templates (two-ring focus), icon geometry |
| `Resources/Strings.vi.xaml`, `Strings.en.xaml` | every user text and every `NotificationMessage` key |
| `Shell/ViewModels/`, `Shell/Commands/` | capture bar, settings (`SettingsViewModel`), tray menu, `LanguageService` contract |
| `Capture/ViewModels/CaptureFlow.cs` | the whole capture run: countdown, overlay, dialog, delivery |
| `Capture/ViewModels/SelectionOverlayViewModel.cs`, `DisplayUnits.cs` | the drag on the frozen snapshot; display units to physical pixels |
| `Capture/ViewModels/CaptureDoneViewModel.cs` | the five buttons of the "Đã chụp" dialog |
| `Editor/ViewModels/EditorViewModel.cs`, `EditorFlow.cs` | tools, zoom, save/copy/close, open; `EditorCoordinates.cs` view-to-image maths |
| `Editor/Rendering/AnnotationRenderer.cs`, `WpfImageFlattener.cs` | drawing annotations and producing the flattened `PixelImage` |
| `Shell/Views/`, `Capture/Views/`, `Editor/Views/` | the windows and, beside them, the WPF services of that domain (theme, folder picker, prompts) |
| `Shared/Views/LanguageService.cs`, `NotificationPresenter.cs`, `ToastWindow`, `ErrorDialogWindow` | text in the language in use; toasts and error boxes for every domain |
| `Shared/Views/PhysicalWindowPlacer.cs` | the only place that positions a window in physical pixels |
| `Editor/Views/EditorWindowPlacement.cs` | keeps the editor inside the monitor that holds the pointer |

## Flow

Capture: `CaptureFlow.StartAsync` -> `ICaptureInteractor.BeginAsync` -> `SelectionOverlayViewModel` (`WpfCaptureViews.OpenSelection`) ->
`CaptureFlow.HandleCaptured` -> `CaptureDoneViewModel` or `EditorFlow`.
Editor: `EditorFlow` -> `EditorWindow` / `EditorViewModel` -> `IEditorInteractor` -> `WpfImageFlattener.Flatten` when saving or copying.
Shell: `SettingsViewModel` -> `IShellInteractor.Apply` -> `LanguageService`, `ThemeService`.
