# Paper.ScreenWizzard.UseCases

Every decision of the three features, on `net10.0` with no Windows: each feature has an interactor behind `I<Feature>Interactor`, the
ports it needs (`I<X>Port`, plain data only), the records that cross them, and a stateful session object. The Presentation flows call the
interfaces; Infrastructure implements the ports.

## Map

| File | Open it for |
| --- | --- |
| `Shell/Ports/ShellPorts.cs` | `IShellInteractor` and the ports of the shell: settings store, hotkeys, autostart, single instance, monitors |
| `Shell/Implements/ShellInteractor.cs` | start-up (load or default settings, register the hotkeys, notices), hotkey change, apply, autostart, bar placement |
| `Capture/Ports/CapturePorts.cs` | `ICaptureInteractor`, `ICaptureSession`, the screen and window ports |
| `Capture/Implements/CaptureInteractor.cs` | countdown, snapshot, one session at a time, where a captured image goes |
| `Capture/Implements/CaptureSession.cs` | choosing on the frozen snapshot: rectangle, freeform, window hit-test, full screen |
| `Common/Ports/CommonPorts.cs` | clock, delay, log, notification, clipboard, file store, image codec |
| `Common/Implements/ImageDelivery.cs` | save to a folder (automatic name), to a path, to the clipboard; JPG on white |
| `Editor/Ports/EditorPorts.cs` | `IEditorInteractor`, `IEditorSession` |
| `Editor/Implements/EditorSession.cs` | the history: one step per change, redo branch, dirty tracking, crop, blur render |
| `Editor/Implements/EditorInteractor.cs` | open from file or clipboard, Shift constraint, text, `HitTest`, where Ctrl+S goes, close decision |
| `*/Models/*.cs` | the plain records and issue enums each feature returns |

## Flow

Capture: `CaptureInteractor.BeginAsync` -> `IDelayPort` countdown -> `IScreenSourcePort` -> `CaptureSession` -> `CaptureSession.CompleteRectangle` ->
`CaptureInteractor.PlanAfterCapture` -> `CaptureInteractor.Deliver` -> `ImageDelivery.SaveToFolder`.
Editor: `EditorInteractor.OpenFile` -> `EditorSession` -> `EditorInteractor.DecideSave` -> `EditorInteractor.Save` -> `ImageDelivery.SaveToPath`.
Shell: `ShellInteractor.Start` -> `ISettingsStorePort.Load` -> `IHotkeyPort.Register` -> notices for what could not be registered.
