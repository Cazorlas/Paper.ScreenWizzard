# Paper.ScreenWizzard.UseCases

Every decision of the three features, on `net10.0` with no Windows: each domain folder holds `Ports/` (one interface per file:
`I<Feature>Interactor`, the session, and the ports it needs, named after what they provide, plain data only), `UseCases/` (the
interactor and the session) and `Models/` (the records that cross them). `Shared/` holds what two or more domains call (ADR 0003). The Presentation flows call the
interfaces; Infrastructure implements the ports.

## Map

| File | Open it for |
| --- | --- |
| `Shell/Ports/` | `IShellInteractor` and the ports of the shell: `ISettingsStore`, `IHotkeys`, `IAutostart`, `ISingleInstance`; `IUpdateInteractor` and its ports `IReleaseFeed`, `IBrowser` |
| `Shell/UseCases/ShellInteractor.cs` | start-up (load or default settings, register the hotkeys, notices), hotkey change, apply, autostart, bar placement |
| `Shell/UseCases/UpdateInteractor.cs` | whether a newer version is out (check on, running x.y.z, newest tag), said once per version; opens its release page |
| `Capture/Ports/` | `ICaptureInteractor`, `ICaptureSession`, `IScreenSource`, `IWindowCatalog`, `IDelay` |
| `Capture/UseCases/CaptureInteractor.cs` | countdown, snapshot, one session at a time, where a captured image goes |
| `Capture/UseCases/CaptureSession.cs` | choosing on the frozen snapshot: rectangle, freeform, window hit-test, full screen |
| `Shared/Ports/` | `IMonitorCatalog` (capture and the editor's window placement), `IClock`, `ILog`, `INotifications`, `IClipboard`, `IFileStore`, `IImageCodec`, `IImageDelivery` |
| `Shared/UseCases/ImageDelivery.cs` | save to a folder (automatic name), to a path, to the clipboard; JPG on white |
| `Editor/Ports/` | `IEditorInteractor`, `IEditorSession` |
| `Editor/UseCases/EditorSession.cs` | the history: one step per change, redo branch, dirty tracking, crop, blur render |
| `Editor/UseCases/EditorInteractor.cs` | open from file or clipboard, Shift constraint, text, `HitTest`, where Ctrl+S goes, close decision |
| `*/Models/*.cs` | the plain records and issue enums each domain returns (`Capture/Models/ScreenCaptureModels.cs`, `Shared/Models/DeliveryModels.cs` too) |

## Flow

Capture: `CaptureInteractor.BeginAsync` -> `IDelay` countdown -> `IScreenSource` -> `CaptureSession` -> `CaptureSession.CompleteRectangle` ->
`CaptureInteractor.PlanAfterCapture` -> `CaptureInteractor.Deliver` -> `ImageDelivery.SaveToFolder`.
Editor: `EditorInteractor.OpenFile` -> `EditorSession` -> `EditorInteractor.DecideSave` -> `EditorInteractor.Save` -> `ImageDelivery.SaveToPath`.
Shell: `ShellInteractor.Start` -> `ISettingsStore.Load` -> `IHotkeys.Register` -> notices for what could not be registered.
New version: `UpdateInteractor.CheckAsync` -> `IReleaseFeed.GetLatestAsync` -> `AppVersion` compare -> offer and notice; `OpenDownloadPage` -> `IBrowser.Open`.
