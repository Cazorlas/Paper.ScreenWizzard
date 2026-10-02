# UI shots compared with a recorded baseline

Reference for `drive-wpf`, "Look at it". A screenshot opened and judged proves the window looked right once.
A screenshot compared with a recorded baseline proves it still does after the next change. The kit ships the
comparison (`scripts/compare-shot.ps1`, decisions in `scripts/shot-baseline-plan.ps1`); the code that takes
the shot lives in the project's tests.

## Taking the shot

- **WPF content: let WPF draw it again.** `new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32)`, then
  `Render(window)` and a `PngBitmapEncoder`. No screen involved: it works for a window that is hidden,
  covered or off-screen, and it is the same on every machine at the same DPI.
- **Direct3D or other 3D content comes out white that way**: it is not drawn by WPF. Shoot the window as
  the system composed it instead - `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT = 2)` into a bitmap of the
  window's size. That also works for a window outside every monitor.
- **One baseline per framework**: `<name>-<tfm>.png` - `main-window-net48.png`, `main-window-net8.0.png`.
  The two frameworks print the same number as different text, so one shared baseline fails on one of them.

## Comparing

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/drive-wpf/scripts/compare-shot.ps1 -Actual <shot.png> -Recorded <baselines\main-window-net8.0.png>
```

Both PNGs are **decoded** first and redrawn to 32-bit ARGB, then compared pixel by pixel, alpha included -
two encoders write the same picture as different bytes, so the files themselves are never compared.

| Exit | Means |
| --- | --- |
| 0 | `same as recorded` |
| 1 | not recorded yet (the message names `PAPER_UI_BASELINE_RECORD=1`), another size (both sizes), or the first different pixel, row by row, with both colours as `#AARRGGBB` and both paths |
| 2 | the actual shot is missing or cannot be read |
| 3 | `RECORDED` - this run wrote the baseline |

## Recording a baseline

Set `PAPER_UI_BASELINE_RECORD=1` for one run, on a build known to be right: the shot is copied over the
baseline (its folder made) and the run exits 3, printing `RECORDED`. **A recording run is never evidence**:
it compared nothing. The evidence is the next run, without the variable, exiting 0. A CI job that forgets
to unset the variable fails every run instead of passing forever.

## Real-input tests stay out of the default run

A test that drives the real window with real input (FlaUI clicking, a full-screen window) is marked
`[Explicit]` and `[Category("RealUi")]`: it runs only when named, never in the default test command. Each
one runs on its own STA thread, with the window full screen and `Topmost`, so nothing of the desktop is in
the shot. When it fails, it shoots the window itself into the run's shot folder before it throws, and the
failure message names the file.
