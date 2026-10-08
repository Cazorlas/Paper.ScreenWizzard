# The lanes of the reviews

Read by the main session when it hands out the agent lanes (step 4 of [the steps](steps.md)). `review.ps1 plan` already
decided which lanes run; this page says why, which files each lane reads, who answers it and what it is told.
`review.ps1 lanes` prints the same decision before any run, with no choice applied, for the lane question of
`/review-architecture` (the only command that asks one).

## Which command runs which lane

| Command (`-Kind`) | Lanes |
| --- | --- |
| `/review-architecture` (`architecture`) | `static` (smells and the complexity table), `architecture`, `smell` |
| `/review-bugs` (`bugs`) | `static` (bug diagnostics are hints, never read again), `bug` |
| `/review-security` (`security`) | `static` (vulnerabilities), `security` |
| `/review-ui` (`ui`) | `ui` |

A lane of another command is refused (exit 2): `lane 'x' belongs to /review-y - /review-z runs a, b`. `-StaticOnly` on
`/review-ui` is refused too: it has no static lane.

## Which lanes run

The lanes are `static`, `security`, `bug`, `architecture`, `smell`, `ui`, in that order. For each, the
first rule that matches decides:

| # | When | State | Reason printed |
| --- | --- | --- | --- |
| 1 | a name in `-Only`/`-Skip` is not a lane of the command | refused (exit 2) | `unknown lane 'x' (<the lanes of the command>)`, or `lane 'x' belongs to /review-y - ...` |
| 2 | a lane is in both `-Only` and `-Skip` | refused (exit 2) | `lane 'x' is in both -Only and -Skip` |
| 3 | static: no .csproj, .vbproj, .fsproj or .sln in the repository | not applicable | `no .NET project` |
| 4 | static: the profile has no `build` verb | not applicable | `no build verb` |
| 4e | static, external repository: `build.command` blank, or `build.noDeploy` empty | not applicable | `no build command in review.profile.json (build.command)` / `build.noDeploy names no property - deploying is not ruled out` |
| 5 | the lane has no file in scope | not applicable | static `no C# or VB files in scope`, ui `no UI files in scope`, the others `no code files in scope` |
| 6 | architecture: the profile declares neither `architecture.platformFree` nor `architecture.reviewers`, and `review.lanes.architecture` is not true | not applicable | `profile declares no architecture` |
| 6e | architecture, external repository: no rule file (replaces 6; `review.lanes.architecture` cannot force it) | not applicable | `no rule files in the repository (review.profile.json rules)` |
| 7 | ui: no host but `cli` and `ai` - every other host has a user interface - and `review.lanes.ui` is not true; never on an external repository, where the UI files of the scope decide | not applicable | `no host with a user interface (hosts: <hosts, or none>)` |
| 8 | profile `review.lanes.<lane>` is false | skipped | `profile turns it off` |
| 9 | `-StaticOnly`, any lane but static | skipped | `-StaticOnly` |
| 10 | `-Only` given and the lane not in it | skipped | `-Only` |
| 11 | the lane in `-Skip` | skipped | `-Skip` |
| 12 | otherwise | run | |

Rules 3-7 come before the choices 8-11: a lane that does not apply says so with its reason, whatever the owner picked (ADR-0035).

## Files of each lane

| Group | Extensions |
| --- | --- |
| code | .cs .vb .fs .ps1 .psm1 .psd1 .py .ts .tsx .js .jsx .mjs .cjs .go .java .c .cc .cpp .h .hpp .rs .sql .sh |
| config | .json .config .xml .yml .yaml .props .targets .csproj .vbproj .fsproj .sln .addin .manifest .toml .ini |
| ui | .xaml .axaml .cshtml .razor .html .htm .css .scss .less .vue .svelte .tsx .jsx .dcl |

static: .cs .vb - security: code and config - bug: code - architecture: code, project files (.csproj,
.vbproj, .fsproj, .sln, .props, .targets) and CODEMAP.md - smell: code - ui: the ui group plus the
screenshots `review.ui.screens` names.

Files are grouped by the nearest ancestor folder containing SPEC.md or CODEMAP.md; without one, their containing
folder is the unit. Units run in path order. A unit that fits `review.batchTokens` (default 120000) stays together,
opening a new batch if needed; only an oversized unit is split greedily by file path. A file over the cap runs alone.
With base executor collab, security and smell batches use `review.codexBatchTokens` (default 160000, at least 20000); bug, architecture and UI keep `review.batchTokens` for their Claude subagents.

## Who runs each lane

`review.ps1 plan` says who answers in its `lanes run:` line (`-Executor auto|collab|subagents|solo`, ADR-0043, ADR-0044).
The files the reading lanes read, each once, are counted in content tokens (a screenshot counts 1,600):

1. At most `review.soloTokens` (default 40,000; 0 turns it off) and `auto`: **solo** - the session reads the files itself,
   one turn per lane, and the estimate says `lanes run: A alone - <n> files, ~<tokens> content tokens (solo limit <limit>)`.
   A finding that needs reading back is verified by Codex through a read-only collab session that opens only then
   (the `collab-start: ... (only if check queues a finding)` line), else by a fresh subagent. `-Executor solo` over the
   limit is refused (exit 2) with the tokens and how to narrow the scope.
2. Over the limit, with collab and codex on this machine, security and smell batches are questions to Codex in a read-only collab
   session on the real checkout (uncommitted work included; the Claude Sonnet worker answers when Codex is out of
   usage). Bug, architecture and UI use Claude subagents; the estimate names the reader per lane.
3. Otherwise, and when Codex or a collab turn runs the review, it is a read-only subagent of the session.

The checklists a lane pastes live with the skill that owns them (ADR-0044): `review-security/references/checklist.md`,
`review-architecture/references/smell.md`.

| Lane | Answered by | Instructions pasted into the prompt | Id prefix | KIND it reports |
| --- | --- | --- | --- | --- |
| security | the session alone (small scope), Codex via a read-only collab turn, or a read-only subagent (step 4) | `review-security/references/checklist.md` | SEC | vulnerability |
| bug | the session alone (small scope), or a read-only subagent (Claude when the base is collab; step 4) | `find-bug/SKILL.md` (with "No SPEC.md for the file") | BUG | bug |
| architecture | the session alone (small scope), or a read-only subagent (Claude when the base is collab; step 4) | `architecture-reviewer.md`, front matter removed; reports finding blocks instead of one line per violation | ARC | architecture |
| smell | the session alone (small scope), Codex via a read-only collab turn, or a read-only subagent (step 4) | `review-architecture/references/smell.md` | SML | smell |
| ui | the session alone (small scope), or a read-only subagent (Claude when the base is collab; step 4) | web: `paperflow/review/ui-web.md`, then `skills/impeccable/reference/critique.md`, `skills/impeccable/reference/audit.md`, `skills/impeccable/reference/craft-floor.md`; desktop or no web: `design-critique/SKILL.md`, then `accessibility-review/SKILL.md`; mixed: web then desktop | UI | ux |

The UI lane chooses instructions from the files of each batch, case-insensitively. Web extensions are
`.html .htm .css .scss .less .vue .svelte .tsx .jsx .cshtml .razor`; desktop extensions are `.xaml .axaml .dcl`.
A batch with both reads web first, then desktop; a batch with no web (including screenshots only) keeps the two
desktop skills. Paths in the table are relative to `.claude`, with impeccable's references under `skills/impeccable`.
The [web preface](ui-web.md) overrides writing, launcher, browser and sub-agent steps in those references: one
read-only reader performs Assessment A then B and uses this review's finding format, with closing questions skipped.
The manual skills are read because the user typed `/review-ui` and agreed to the estimate. For a project review the
reader also reads the project's UI style skill under `.claude/skills` if it has one.

With more than one batch, the prefix carries the batch number from the second batch on: `BUG2-1`.

## The prompt of one batch

`review.ps1 prompt -Run <run> -Lane <lane> -Batch <b> [-Retry]` builds it from [prompts.md](prompts.md) and writes it to
`<run>/prompts/<lane>-<b>.md` (a retry: `<lane>-<b>.2.md`): the opening, the output of `review.ps1 files` for that batch
word for word, the finding format of [findings.md](findings.md) and the instruction files above pasted word for word
(front matter removed) - so an agent that cannot read the kit's folders, such as Codex in another repository, still gets
every rule. The `specs:` line names the SPEC.md files of units in the batch; when SPEC.md files live apart from the code (featureDocs), it names them all (up to 30) and the reader picks. The list starts the review; the reader may trace calls, implementations and tests anywhere inside the repository and run read-only commands that write nothing to prove findings. It prints `prompt:` (the file), `answer:` (where the answer goes) and `task:` (the name of the collab question).
A reader's prompt: `review.ps1 prompt -Run <run> -Verify <id>`. Nobody pastes a prompt by hand.

When the static lane ran, the list of the smell lane ends with `Already reported by analyzers (do not
repeat): <rule> x<count>, ...` - the ten biggest static smell groups in scope - and the security lane's
with the static vulnerabilities the same way; the agent does not report those again. The bug lane's list ends with
`Analyzer hints (check, do not copy)`: the analyzers' bug diagnostics in scope, as places to look at - the lane reads them
like any other line of code, and they are never a finding by themselves.

## An external repository

With `-Out` (ADR-0034) the lanes read a repository that is not this project, by its own rules:

- **Rule files.** The `rules` key of `review.profile.json` names them (paths or globs, each must match a file).
  Without the key, the defaults: `CLAUDE.md`, `AGENTS.md`, `ARCHITECTURE.md` and `BUILD.md` at the root; the
  Markdown files under a `docs/adr...` folder, `docs/decisions` and `specs`; and the `CLAUDE.md` or
  `AGENTS.md` of any folder - never the agent config folders (`.claude`, `.agents`, `.codex`) or a generated
  folder. `"rules": []` is no rule file: the architecture lane is not applicable (row 6e).
- **A folder's own rules.** A `CLAUDE.md` or `AGENTS.md` below the root goes only to a batch with a file
  under that folder; every other rule file goes to every architecture batch. The estimate counts them.
- **The prompt** of every batch gains these two lines (`review.ps1 prompt` writes them, with the repository's path):

```
The repository is <repo> (read only): every path of the list is relative to it; read <repo>\<path>.
Judge against its own rules - for the architecture lane exactly the files on the rules: line - never against this project's ADRs or profile.
```

- **A branch scope.** The list is the kit's own read-only git (ADR-0037), not review-files; the static lane still builds the whole project and keeps only findings in the files of the scope - the rest is counted `outside scope`.
