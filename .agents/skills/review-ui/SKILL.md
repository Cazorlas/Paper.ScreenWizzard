---
name: review-ui
description: On-demand UI/UX review of the windows, pages and views in a folder, a branch, some files or the whole project - web, desktop and native (SwiftUI, Compose, Flutter, QML) - and of the screenshots the profile names - usability, hierarchy, consistency and accessibility; one read-only report of proposals, then fixes only for the findings the owner picks. Manual only - run when the user types /review-ui; never on its own while UI is being built, where the host's style skill and the plan's wireframe own the work.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder> | <file> <file> ...] [--yes] [--repo <path> --out <folder>]"
---

# /review-ui

**One command, one report; fixes only on the owner's pick.** `/review-ui` reads the UI files of a scope - `.xaml .axaml
.cshtml .razor .html .htm .css .scss .less .vue .svelte .tsx .jsx .astro .dcl .qml`, Android `res/layout/*.xml`, and
SwiftUI/Compose/Flutter sources - and the screenshots `review.ui.screens` names, in one lane, `ui`. Its findings are
proposals, never bug ledger entries (ADR-0044); the fix stage below turns the ones the owner picks into work (ADR-0049).

Follow [the steps](../../paperflow/review/steps.md) (`.claude/paperflow/review/steps.md` from the project root; from the
plugin, under its `kit:` folder) with `-Kind ui`. No analyzer build. Scope words are those of `/review-architecture`:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project |
| `project` / `branch [base]` / a folder / files | `-Scope project` / `-Scope branch [-Base <base>]` / `-Scope path -Path <folder>` / `-Scope files -Files <file>,<file>` |
| `--yes` | `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command |

No lane question: one lane. The estimate is still the stop before paid reading.

With the collab base, UI uses Claude subagents. Batches keep related files together by their nearest ancestor
with SPEC.md or CODEMAP.md, splitting only oversized units. Start from the list; read other repository files
to trace calls, implementations and tests, and run read-only commands that write nothing when they prove a
finding. `seen N/N` is coverage information; a valid WHERE outside the list is marked `(ngoài lô)`.

## Visual pass before the lane

After approval and before starting the lane, the main session captures the screens in scope when the host
can run: desktop with `drive-wpf`, web with Playwright MCP, a video project with its still command (`remotion still`). Save the captures in the run folder and put
their paths in `screens` for the lane, alongside the screenshots the profile names. Open the captures
and check that each shows the intended screen and current state.

When capture cannot run, the report says `visual pass: not run - <reason>` and identifies the lane as a
code-only review. Reading UI files does not prove how a screen renders.

For every screen with a screenshot, the lane returns one finding of severity `info`, RULE `overall`:
its first impression and the order in which to fix the screen's issues. WHERE is the source file that draws the screen (`path:line`);
the screenshot is named in WHY. Save captures in `<run>/screens/`: `review.ps1 prompt` lists them on the lane's `screens:` line.

## Newest sources before the lane

- **Skills.** `plan` compares each vendored skill the lane reads with its upstream release (`upstreams.json`) and prints
  one `update:` line per skill: `current`, `fetched` (the newer upstream text is pasted from the machine cache),
  `check-only` (a Paper-adapted skill: tell the owner a newer upstream exists), or `skipped - <reason>` (offline: the
  review goes on with the vendored copy). `review.upstreamRefresh: false` turns it off.
- **Detector.** After `approve` and before the lane, run `review.ps1 detect -Run <run>` in the background: it runs
  `npx impeccable@latest detect` on the scope's web folders (the newest release every time, telemetry off, nothing
  written in the repository) and hands each batch its own findings. The first run on a machine downloads the package
  (measured 5 min); later runs take seconds. Exit 4 `detector pass: not run - <reason>` and exit 5 (no web file, or
  `review.ui.detector: false`) are reported as they are; the lane still runs.
- Plugins come from `/setup-skills`; worker CLIs are checked by collab's `probe`. Neither is part of this command.

## What the lane reads with

`review.ps1 prompt` selects instructions from each batch's files, case-insensitively, and pastes them word for word:

- Web (`.html .htm .css .scss .less .vue .svelte .tsx .jsx .cshtml .razor`):
  [.claude/paperflow/review/ui-web.md](../../paperflow/review/ui-web.md), then impeccable's
  `reference/critique.md`, `reference/audit.md` and `reference/craft-floor.md` under `.claude/skills/impeccable`.
- Desktop (XAML `.xaml .axaml`, DCL `.dcl`), or no web file (including screenshots only):
  `.claude/skills/design-critique/SKILL.md`, then `.claude/skills/accessibility-review/SKILL.md`.
- Native (`.qml`, Android layouts, SwiftUI/Compose/Flutter sources): [.claude/paperflow/review/ui-native.md](../../paperflow/review/ui-native.md),
  then impeccable's `reference/audit.native.md`, plus `ios.md` and/or `android.md`.
- A mixed batch: web, then native, then desktop instructions; each applies to its files. Web or native batches also
  read [ui-libraries.md](../../paperflow/review/ui-libraries.md), the library catalog (Vue, React, Svelte, Angular,
  WPF, Flutter, native) with each library's licence.

The web preface keeps critique and audit read only: no launcher, browser, sub-agents or writes; Assessment A then B
in the same turn, closing questions skipped, findings in this command's format. These manual skills are read here
because the user typed `/review-ui` and agreed to the estimate. For a project review the reader also uses the project's
UI style skill under `.claude/skills` when it has one.

For depth on one finding - a palette, a font pair, a stack's guideline - the lane may run
`.claude/skills/ui-ux-pro-max/SKILL.md`'s search script and quote a relevant row in the finding's FIX.
Keep the search tied to that finding; it supplies a proposal, not evidence about this project's rendering.

The lane applies when the scope has UI files or the profile names screenshots, on every host but `cli` and `ai`
(`review.lanes.ui: true` forces it). A WinForms `.Designer.cs` is generated: only its screenshot reaches the lane.

## Fix stage - only the findings the owner picks

After the report, ask which findings to fix (ids such as `UI-1,UI-3`, or `all` = every ui finding but `overall`), then
`review.ps1 fix -Run <run> -Ids <ids>`:

- In a kit project it writes `<run>/fix/task-spec-input.md` (findings grouped by folder, `{files:}`, how to verify each
  file kind) and prints `next: /task-spec <path>`: the fix goes through the project's own lifecycle, planner and workers.
- With `-Repo -Out` it writes one collab brief per group under `-Out` and prints the `dispatch:` lines; nothing is
  written in the repository. A `review.profile.json` with `readOnly: true` refuses (exit 2).
- Verification is part of each group's done-when: web - `detect` again shows none of the chosen rules and a Playwright
  capture; XAML - drive-wpf in both themes against the project's style skill; native - `visual check: not run` unless
  the project has a capture command.

## Never

- Change a window, a style or a resource during the review: every finding is a proposal until the fix stage.
- Start the lane before the user saw the estimate and agreed (`approve`).
- Judge a screen nobody can see: a finding names the file and line, or the screenshot.
- Report the lane as clean when it did not apply: it says why.
