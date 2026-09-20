---
name: paper-wpf-style
description: Use when building or refactoring WPF/MVVM presentation code in a Paper project (a Revit or AutoCAD add-in's windows, standalone Paper WPF apps, code on the shared Paper WPF library) — covers layer boundaries, CommandBase-first MVVM with the shared library's source generators, navigation, the DI composition root, persisted local settings and themed XAML.
---

# Paper WPF Style

Apply this to Paper WPF code unless the project already establishes a stronger, conflicting pattern.
The project's own `CLAUDE.md` is authoritative and overrides anything here that conflicts.

Worked code lives beside this file: `references/viewmodel-and-commands.md` (generator setup, full
ViewModel and command examples, `[PaperCommand]`) and `references/xaml-shared-library.md` (what to reuse,
the standard UserControl header). Open them when writing that kind of code.

## Layer boundaries

```text
WPF / Presentation -> Application -> Domain
Infrastructure     -> Application + Domain
```

- **Domain** — business models and contracts only. No WPF, no `HttpClient`, no JSON attributes, no
  provider DTOs, no host UI types.
- **Application** — interfaces/ports and use cases (skill `clean-architecture`). ViewModels depend on
  these interfaces.
- **Infrastructure** — HTTP clients, JSON DTOs, external providers, DTO→Domain mapping, persistence.
- **WPF** — views, ViewModels, commands, navigation, and the DI composition root.

`architecture.platformFree` in `.claude/paper.profile.json` keeps `System.Windows` out of the layers
that must not see it; hook `layer-guard` blocks the edit.

## Project layout

```text
Commands/             # Project-specific commands and their workflow logic
ViewModels/           # Presentation state and binding properties
Views/                # UserControls, one feature/screen per view
WPF/
  Commands/           # Reusable CommandBase, AsyncCommandBase, NavigateCommand
  Services/           # Reusable navigation services
  Stores/             # NavigationStore and other presentation stores
  BindableBase.cs     # Reusable INotifyPropertyChanged / validation base
```

Keep `WPF/` generic and reusable. Anything tied to a specific screen, API, or business feature belongs in
the root `Commands/` folder. When the project references the shared Paper WPF library, that library's `WPF/` already holds these
primitives — reuse them instead of re-creating a local copy.

## Before you write anything

Search the shared libraries the project's `CLAUDE.md` names first. Base classes, WPF controls,
converters, themes, icons, dialogs, progress bars, config, logging, and telemetry already exist there.
Write a local helper only when nothing fits — and say why.

## ViewModels — `[BindableBaseProperty]`

Paper uses **the shared library's own source generator**, not CommunityToolkit.Mvvm. The class is `partial`,
derives from `BindableBase`/`ViewModelBase`, and the project references the generator as an analyzer.

- A property is a field: `[BindableBaseProperty] private string _searchText = string.Empty;` — not static,
  const or readonly. Hooks `On<Name>Changing` / `On<Name>Changed` are generated as `partial void`.
- A computed property that commands depend on: `[RaiseCanExecuteChanged(nameof(ApplyCommand))]` generates
  `Notify<Prop>Changed()`.
- No handwritten `SetProperty(ref …)` in new code. `ObservableCollection<T>` properties are plain `{ get; }`.
  Properties a command writes back to may use `internal set`.
- ViewModels hold state, validation, command properties, and collections. **No HTTP, no transactions, no
  workflow orchestration.**

## Commands — `CommandBase` / `AsyncCommandBase`

Do not introduce `RelayCommand`, `[RelayCommand]`, or CommunityToolkit.Mvvm. A command is a named class;
that is what keeps the call flow readable and the workflow testable.

- Workflow logic lives in the command, not the ViewModel and not the code-behind.
- `AsyncCommandBase` already blocks re-entrancy while running; don't add your own guard. Restore UI state
  (`IsBusy`) in `finally`, including on the failure path; surface a recoverable error in the panel.
- `RaiseCanExecuteChanged()` also calls `CommandManager.InvalidateRequerySuggested()`;
  `OnCanExecuteChanged()` only raises the event.
- Declare commands with `[PaperCommand]` on a **nullable, non-readonly** field of the concrete command
  type, and call `InitializePaperCommands()` at the end of the constructor, after every field a command
  captures is assigned. Arguments are named with `nameof()`, never inferred.

## Navigation

Navigable screens are UserControls.

```text
MainWindow (shell)
  -> ContentControl bound to CurrentViewModel
  -> DataTemplates map ViewModel type to UserControl

NavigateCommand<TViewModel> -> NavigationService<TViewModel> -> NavigationStore.CurrentViewModel
```

- `MainWindow` owns shell chrome and navigation controls only; `NavigationStore` raises the change event.
- A navigation UserControl gets its own ViewModel which owns its `NavigateCommand<TViewModel>` instances,
  created through `NavigationService<TViewModel>` — never directly in XAML.
- The shell ViewModel exposes `CurrentViewModel` plus the child navigation ViewModel; bind the
  UserControl's `DataContext` to that child.
- Register view factories in DI; initialise the start ViewModel after the provider is built.

## Dependency injection and configuration

- One composition root. In a standalone app that is `App.xaml.cs`; in an add-in it is the host class the
  project names in its `CLAUDE.md` (usually a `Host` class extended through the configurators the project registers —
  not by editing its start method).
- Read configuration there, validate required values early, configure one shared `HttpClient`, and bind
  interfaces to concrete Infrastructure implementations.
- ViewModels receive Application interfaces — never `HttpClient`, configuration objects, API keys, or
  concrete provider clients. `NavigationStore` singleton, screen ViewModels transient.
- Keep secrets out of source control. Commit `appsettings.example.json`; ignore `appsettings.json`.

**Persisted local settings.** `appsettings.json` is startup configuration only; user-managed settings go
through a `ConfigService<T>`, stored under `%AppData%\Paper\<AppName>\configs` (built with
`Environment.SpecialFolder.ApplicationData` + `Path.Combine`, never a relative working directory), with
`System.IO` + `System.Text.Json`. Validate before an explicit save; on first load create the default file;
on a corrupt file report it and fall back to defaults. Never persist secrets there.

## XAML

Check the shared library's `WPF/` folder before writing XAML — styles, converters, icons, controls and dialogs already
exist (table and header: `references/xaml-shared-library.md`).

- `ThemeUtility.IsThemed="True"` on every window/page/UserControl, or derive from `PaperWindow` /
  `PaperUserControl`. Without it the element does not follow theme switching.
- Brushes via `DynamicResource`, never a hard-coded `#RRGGBB`. `StaticResource` is fine for `Geometry`
  icons and local styles.
- All user-visible text via `{DynamicResource <LocalizationKey>}`. A literal string in XAML is a
  localization bug.
- **Split screens into UserControls**: one shell `Views/<Screen>.xaml` plus `Views/Controls/<Part>.xaml`,
  each with its own ViewModel exposed on the parent. Split before a view passes ~200 lines or gains a
  second independent concern.
- Code-behind: `InitializeComponent()` and genuinely UI-only work only.
- **Comment the XAML**: what the `DataContext` is and who sets it, each top-level layout region, and why any
  non-obvious trigger, converter, or template exists.
- Always give real loading, empty, and error states. Never fabricate placeholder data the app does not fetch.

## Reachable by everyone

These are not extras. Each one has somewhere it is checked, so treat a miss as a defect, not a nicety.

- **Contrast: 4.5:1 for text, 3:1 for an icon or a control's border** — and **in both themes**. A colour that
  reads in dark and vanishes in light is a real defect, and `drive-wpf` already shoots both themes, so the
  screenshot you open is where this is judged.
- **The focus ring must be visible.** WPF's default `FocusVisualStyle` is a thin dotted rectangle that
  disappears on a themed background. Every focusable control needs a focus visual that survives the
  project's own brushes — check it by tabbing, not by reading XAML.
- **An icon-only control needs a name.** `AutomationProperties.Name` (or a `ToolTip` the style promotes to
  it), because a picture alone says nothing to a screen reader — and because the UI tests find buttons by
  that name. A control with no name is a control no test can reach.
- **Everything works from the keyboard.** Tab order follows the visual order, `Enter` commits and `Esc`
  cancels a dialog, and no action exists only behind a right-click or a hover.
- **Respect the user's animation setting.** Gate animation on `SystemParameters.ClientAreaAnimation`; it is
  WPF's `prefers-reduced-motion`.
- **Hit area before pixel size.** An icon button drawn at 16x16 needs padding to a comfortable target;
  judge it by clicking it, not by the glyph's dimensions.

## Readability beats purity

- **Some duplication is acceptable** when the alternative is an abstraction that hides the flow. Extract
  when the duplication is real (same rule, same reason to change), not merely similar-looking.
- **Do not over-split.** No interface for a single implementation with no test or provider benefit. If a
  reader must open five files to understand one button, the design is wrong.
- **Patterns and algorithms are welcome when they earn their place** — Strategy, Adapter, Facade,
  `Dictionary`/`HashSet` lookups, bucketing instead of nested loops — for a problem you actually have,
  said so in a comment.
- **Comment non-obvious blocks, in English**, explaining *why*: constraints, unit conversions, tolerances
  and where they came from, business rules, error paths. Do not restate syntax.

## Verify

1. Build after each structural slice, using the project's named configuration (verb `build`).
2. Confirm Domain/Application reference no WPF, `HttpClient`, JSON attributes, or Infrastructure types.
3. Confirm the shell starts and resolves its initial view through DI.
4. Report pre-existing warnings separately; do not silence a warning you do not understand.
5. A compile does not validate rendered UI. Say explicitly whether the screen was run and looked at
   (lane `ui`, agent `lane-ui`).
