---
name: qa
description: On-demand QA sweep of a whole project, a branch or a folder - machine static analysis plus read-only lanes for security, bugs, architecture, smells and UI, one report, nothing fixed. Manual only - run when the user types /qa or explicitly asks for a QA sweep; never on its own during a task, where /task-verify owns the review of a plan's change.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder>] [--only lanes] [--skip lanes] [--static-only] [--yes] [--repo <path> --out <folder>]"
---

# /qa

**One command, one report, nothing fixed.** `/qa` sweeps the whole project, the current branch or one
folder: a machine static-analysis lane (a real build with analyzers, free) and read-only agent lanes for
security, bugs, architecture, smells and UI. Every finding lands in one report; a bug enters the ledger
only after a second reader confirmed it and the owner approved it.

## /qa and /task-verify

`/task-verify` closes one plan: it reviews exactly the change of that plan against its SPEC, inside the
task lifecycle, and its verdict gates the merge. `/qa` belongs to no plan and gates nothing: it is a
sweep the owner asks for - before a release, after a merge train, on code that never had a SPEC - and
its output is proposals: bug ledger entries to approve one by one, and work to plan with `/task-spec`.
Never run `/qa` as a step of a task, and never let its report stand in for `/task-verify` (ADR-0031).

## Running it

The script is `scripts/qa.ps1` (in a project: `.claude/skills/qa/scripts/qa.ps1`; Codex:
`.agents/skills/qa/scripts/qa.ps1`), always run as
`powershell -NoProfile -ExecutionPolicy Bypass -File <script> <command> ...`. The user's words map to
its parameters:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project; with no base branch (or no merge-base) the project, saying why |
| `project` | `-Scope project` |
| `branch [base]` | `-Scope branch [-Base <base>]` |
| a folder | `-Scope path -Path <folder>` |
| `--only a,b` / `--skip a,b` | `-Only a,b` / `-Skip a,b` (lanes: static, security, bug, architecture, smell, ui) |
| `--static-only` | `-StaticOnly` |
| `--yes` | `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command (`init` first, below) |

1. **Plan.** Run `qa.ps1 plan` with those parameters. Show its output to the user as it is: the scope and
   why, one line per lane with its state, files, agents and tokens, the verify readers, the total.
2. **Stop at the estimate.** Unless the last line says `approved: lanes may start` (`--yes`, or nothing
   but static analysis runs), stop and ask one question: run everything, skip a lane, or static only?
   A different choice is a new `plan` with `-Skip`/`-StaticOnly`. Only after the user agreed, run
   `qa.ps1 approve -Run <run>`. Until then `qa.ps1 files` refuses (exit 4) - no lane gets a file list.
3. **Static lane.** `qa.ps1 static -Run <run>` builds the project with the analyzers plugged in (see
   [static analysis](references/static-analysis.md)). It is machine work: start it in the background and
   go on. Exit 4 is not verifiable with the reason - never "0 findings"; exit 5 is not applicable.
4. **Agent lanes.** For every lane in state `run` and every batch of it, take the list from
   `qa.ps1 files -Run <run> -Lane <lane> -Batch <b>` and hand one agent the prompt of
   [lanes](references/lanes.md) - all lanes and batches in one message, in the background. Security,
   bug, smell and UI go to a read-only subagent; architecture goes to the `architecture-reviewer` agent.
5. **Save each answer** word for word to `.paper/qa/<run>/lanes/<lane>-<b>.md` - the agents only read;
   the main session writes.
6. **Check.** `qa.ps1 check -Run <run>`. Exit 1 lists what to send back, `<lane>-<b>: <error>`: send that
   batch back once with exactly those errors (or, for files not read, the list of
   `qa.ps1 files ... -Retry`), save the second answer as `<lane>-<b>.2.md`, and check again. There is no
   third time: a batch still wrong is reported not verifiable.
7. **Verify.** For each `verify: <id>` line, start one read-only reader in parallel, with only what
   [findings](references/findings.md) allows it (for an agent finding the RULE verbatim and the INPUT -
   never WHERE, WHY, FIX or who found it). Save each verdict block to `.paper/qa/<run>/verdicts/<id>.md`.
   A reader that returns nothing leaves the finding not verified - do not invent a verdict.
8. **Report.** `qa.ps1 report -Run <run>` writes the report and prints its path. Give the user a link
   that opens (paperflow rule 9), a short summary in Vietnamese, and ask which bug ledger proposals to
   open. Each one approved becomes `/task-bug` with the command line the report gives; nothing else does.

The checklists the lanes read: [security](references/security.md), [smell](references/smell.md); the bug
lane reads `.claude/skills/find-bug/SKILL.md`.

## An external repository (read only)

`/qa` also sweeps a repository it must never write into - another team's code, judged by its own rules
(ADR-0034). Add `-Repo <repository> -Out <folder outside it>` to every command.

1. **Once:** `qa.ps1 init -Repo <repository> -Out <folder>` writes `<folder>/qa.profile.json` (never over an
   existing one; run again, it checks the file) and lists the rule files it found. The owner fills in
   `build.command` - the build line, run from the repository root - and `build.noDeploy`, every MSBuild property
   that turns deployment off (`DeployAddin=false`), each also written in the command as `-p:Name=Value`. Until
   both are there the static lane is not applicable. Never write or guess the build line yourself.
2. Then steps 1-8 above, every path under `<folder>`: runs in `<folder>/runs/<run>/` (save `lanes/` and
   `verdicts/` there), the report in `<folder>/reports/`. Scopes are `project` (the default) and `path`; there is
   no branch scope.
3. Every file list has a `repo:` line: its paths are relative to that folder - tell each agent to read them
   there, and each verify reader to look for the code there (`check` prints it). The architecture list ends with
   a `rules:` line: the repository's own rule files for that batch; hand `architecture-reviewer` exactly those,
   never this project's ADRs or profile.
4. Bug proposals carry no `/task-bug`: the repository is not this project's; the owner reports them to its owners.

Nothing is written into the repository - not `.paper/`, not its `.claude/`, not git's index - and none of its
scripts runs except the declared build. Files that build writes into folders git ignores are the build's own;
a build that changes anything else leaves the static lane not verifiable, naming the files.

## Never

- Fix code, write anything under the bug ledger, commit, or change the project's config.
- Start an agent lane before the user saw the estimate and agreed (`approve`), or hand a lane a file that
  is not in its list.
- Let a lane verify its own findings, or put a finding that is not `confirmed` with an INPUT in the bug
  ledger proposals.
- Report a lane that did not run as clean: not applicable, skipped and not verifiable each say why.
- Run /qa on another repository without -Out, or edit its qa.profile.json build line yourself.

## Exit codes of qa.ps1

| Exit | Meaning |
| --- | --- |
| 0 | done |
| 1 | `check` found answers to send back |
| 2 | invalid request, broken profile key, unknown lane or run; `not verifiable:` for a branch scope with no base branch, a wrong qa.profile.json or -Out (external repository) |
| 4 | not verifiable: the run is not approved yet (`files`), or the static lane could not run (no dotnet, a package not downloaded, the build red, an analyzer that did not load) |
| 5 | NOT APPLICABLE: no file in scope, or the lane does not run |

## Paper-skills itself

Paper-skills runs `/qa` from the payload, not from a vendored copy:
`powershell -NoProfile -ExecutionPolicy Bypass -File paper-kit/payload/.claude/skills/qa/scripts/qa.ps1 plan -Repo .`
(and the same path for every other command). Being PowerShell only, its static, architecture and UI
lanes say not applicable with the reason; security, bug and smell run.
