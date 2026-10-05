---
name: review-ui
description: On-demand UI/UX review of the windows, pages and views in a folder, a branch, some files or the whole project, and of the screenshots the profile names - usability, hierarchy, consistency and accessibility, read only; one report of proposals, nothing fixed. Manual only - run when the user types /review-ui; never on its own while UI is being built, where the host's style skill and the plan's wireframe own the work.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder> | <file> <file> ...] [--yes] [--repo <path> --out <folder>]"
---

# /review-ui

**One command, one report, nothing fixed.** `/review-ui` reads the UI files of a scope - `.xaml .axaml .cshtml .razor
.html .htm .css .scss .less .vue .svelte .tsx .jsx .dcl` - and the screenshots `review.ui.screens` names, in one lane,
`ui`. Its findings are proposals for `/task-spec`, never bug ledger entries (ADR-0044).

Follow [the steps](../../paperflow/review/steps.md) (`.claude/paperflow/review/steps.md` from the project root; from the
plugin, under its `kit:` folder) with `-Kind ui`. No analyzer build. Scope words are those of `/review-architecture`:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project |
| `project` / `branch [base]` / a folder / files | `-Scope project` / `-Scope branch [-Base <base>]` / `-Scope path -Path <folder>` / `-Scope files -Files <file>,<file>` |
| `--yes` | `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command |

No lane question: one lane. The estimate is still the stop before paid reading.

## What the lane reads with

`review.ps1 prompt` selects instructions from each batch's files, case-insensitively, and pastes them word for word:

- Web (`.html .htm .css .scss .less .vue .svelte .tsx .jsx .cshtml .razor`):
  [.claude/paperflow/review/ui-web.md](../../paperflow/review/ui-web.md), then impeccable's
  `reference/critique.md`, `reference/audit.md` and `reference/craft-floor.md` under `.claude/skills/impeccable`.
- Desktop (XAML `.xaml .axaml`, DCL `.dcl`), or no web file (including screenshots only):
  `.claude/skills/design-critique/SKILL.md`, then `.claude/skills/accessibility-review/SKILL.md`.
- A mixed web/desktop batch: the web instructions first, then the desktop instructions; each applies to its files.

The web preface keeps critique and audit read only: no launcher, browser, sub-agents or writes; Assessment A then B
in the same turn, closing questions skipped, findings in this command's format. These manual skills are read here
because the user typed `/review-ui` and agreed to the estimate. For a project review the reader also uses the project's
UI style skill under `.claude/skills` when it has one.

For depth on one finding - a palette, a font pair, a stack's guideline - you (not a lane) may run
`.claude/skills/ui-ux-pro-max/SKILL.md`'s search script and quote its row in the finding's FIX. Never as a lane: its data
is large and nothing in it is about this project.

The lane applies when the scope has UI files or the profile names screenshots, on every host but `cli` and `ai`
(`review.lanes.ui: true` forces it). A WinForms `.Designer.cs` is generated: only its screenshot reaches the lane.

## Never

- Change a window, a style or a resource: every finding is a proposal.
- Start the lane before the user saw the estimate and agreed (`approve`).
- Judge a screen nobody can see: a finding names the file and line, or the screenshot.
- Report the lane as clean when it did not apply: it says why.
