# Paper.ScreenWizzard.Domain

The pure rules and value types of the app, on `net10.0` with no Windows: physical-pixel geometry, the capture selection maths, the
editor's drawing model and its hit/mosaic maths, hotkey rules, file naming. Everything here is tested in `UnitTests` without Windows;
UseCases call it, nothing else references it except Infrastructure/Presentation/App for the plain types. Its first-level folders are
the domains every other layer reuses (ADR 0003); `Shared/` is what two or more of them need, including `ImageFormat`.

## Map

| File | Open it for |
| --- | --- |
| `Shared/Geometry.cs` | `PixelPoint`, `PixelRect`, `PixelImage` (BGRA, straight alpha): the only shapes that cross a port |
| `Capture/CaptureTypes.cs` | `CaptureKind`, `MonitorInfo`, `WindowInfo`, `DesktopSnapshot`, the after-capture choices |
| `Capture/CaptureGeometry.cs` | normalise and clamp a drag, the 3-pixel minimum (`MinimumRegionSide`), the virtual-screen union |
| `Shared/PixelImageOps.cs` | crop, freeform mask, flatten a transparent image onto white for JPG |
| `Shared/ScreenshotNaming.cs` | `Screenshot yyyy-MM-dd HH.mm.ss`, the (2) (3) suffix, format from an extension |
| `Editor/EditorTypes.cs` | the annotation records (stroke, line, arrow, rectangle, ellipse, text, step, blur) and `EditorDocument` |
| `Editor/AnnotationMetrics.cs` | how big a drawing really is: highlighter 3x width, step disc, text box estimate |
| `Editor/AnnotationFactory.cs` | which drawing a finished gesture makes: no-size shapes, backwards box, the pen dot, blank text |
| `Editor/AnnotationOps.cs` | move an annotation, the next step number, whether a crop keeps it |
| `Editor/AnnotationHit.cs` | which annotation a click lands on (outline bands, boxes) |
| `Editor/EditorGeometry.cs` | clamp a crop to the image, the Shift constraint of a drag |
| `Editor/Mosaic.cs` | the 12 x 12 pixelation of a blur area |
| `Shell/ShellTypes.cs` | `AppSettings`, `HotkeyChord`, language and theme choices |
| `Shell/HotkeyRules.cs` | is a chord safe, are two the same, how it is written |
| `Shell/SettingsDefaults.cs` | the settings a new user gets (SPEC shell, Inputs) |

## Flow

No flow of its own: `CaptureSession` (UseCases) calls `CaptureGeometry` and `PixelImageOps`; `ImageDelivery` calls `ScreenshotNaming`;
`EditorSession` and `EditorInteractor` call `AnnotationOps`, `AnnotationHit`, `AnnotationFactory`, `EditorGeometry` and `Mosaic`; the renderer in Presentation reads `AnnotationMetrics`; `ShellInteractor` calls
`HotkeyRules` and `SettingsDefaults`.
