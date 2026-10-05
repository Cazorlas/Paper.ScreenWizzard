---
name: review-bugs
description: On-demand bug hunt in a folder, a branch, some files or the whole project - logic read line by line against the SPEC, each finding with the concrete input that breaks it, confirmed by a second reader of the other model before it can be proposed for the bug ledger; the free analyzer build's bug diagnostics are hints. Nothing fixed. Manual only - run when the user types /review-bugs; never on its own during a task, where /task-verify and find-bug own the review of a plan's change.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder> | <file> <file> ...] [--skip static] [--yes] [--repo <path> --out <folder>]"
---

# /review-bugs

**One command, one report, nothing fixed.** `/review-bugs` asks: does this code do what its SPEC says, for every input?
Two lanes: `static` (the shared analyzer build, free in tokens) and `bug`, read only, by the method of
`.claude/skills/find-bug/SKILL.md`. It is the expensive review - logic read line by line - so give it a narrow scope (a
folder, a branch, a few files). Structure is `/review-architecture`'s question, attackers `/review-security`'s
(ADR-0044).

Follow [the steps](../../paperflow/review/steps.md) (`.claude/paperflow/review/steps.md` from the project root; from the
plugin, under its `kit:` folder) with `-Kind bugs`. Scope words are those of `/review-architecture`:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project |
| `project` / `branch [base]` / a folder / files | `-Scope project` / `-Scope branch [-Base <base>]` / `-Scope path -Path <folder>` / `-Scope files -Files <file>,<file>` |
| `--skip static` / `--yes` | `-Skip static` / `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command |

No lane question: one reading lane. The estimate is the stop before paid reading - on a whole project it is large; say
so and offer a folder or the branch.

## Findings

- A finding is a place, the SPEC line (or the code's own contract when no SPEC covers the file) it breaks, and the
  INPUT that gives the wrong result. No input: a suspicion (INPUT `-`), reported, never proposed.
- **The analyzer's bug diagnostics are hints, not findings.** The static lane runs first (one build serves every review
  of the same checkout state) and its bug-kind diagnostics go to the bug lane's list as `Analyzer hints (check, do not
  copy)` and to the report's hints section; a hint becomes a finding only when the lane names its breaking input.
  Vulnerabilities and smells of that build are counted, left to `/review-security` and `/review-architecture`.
- Every finding with an input is read again by the other model - Codex for yours or Claude's, Claude for Codex's - given
  only the rule and the input (the steps, "Verify"). Only a confirmed finding with an input becomes a bug ledger
  proposal, and each one waits for the owner: `/task-bug` with the line the report gives.

## Never

- Fix code, write the bug ledger, commit, or change the project's configuration.
- Start the bug lane before the user saw the estimate and agreed (`approve`).
- Let the reader be the one who found it while the other model is available, or propose a finding that is not confirmed.
- Report an analyzer diagnostic as a bug without a breaking input.
