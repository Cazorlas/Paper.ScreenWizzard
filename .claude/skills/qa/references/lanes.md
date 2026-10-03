# /qa lanes

Read by the main session when it hands out the agent lanes (step 4 of `/qa`). `qa.ps1 plan` already
decided which lanes run; this page says why, which files each lane reads, and what each agent is told.
`qa.ps1 lanes` prints the same decision before any run, with no choice applied, for the lane question of the skill.

## Which lanes run

The lanes are `static`, `security`, `bug`, `architecture`, `smell`, `ui`, in that order. For each, the
first rule that matches decides:

| # | When | State | Reason printed |
| --- | --- | --- | --- |
| 1 | a name in `-Only`/`-Skip` is not a lane | refused (exit 2) | `unknown lane 'x' (static, security, bug, architecture, smell, ui)` |
| 2 | a lane is in both `-Only` and `-Skip` | refused (exit 2) | `lane 'x' is in both -Only and -Skip` |
| 3 | static: no .csproj, .vbproj, .fsproj or .sln in the repository | not applicable | `no .NET project` |
| 4 | static: the profile has no `build` verb | not applicable | `no build verb` |
| 4e | static, external repository: `build.command` blank, or `build.noDeploy` empty | not applicable | `no build command in qa.profile.json (build.command)` / `build.noDeploy names no property - deploying is not ruled out` |
| 5 | the lane has no file in scope | not applicable | static `no C# or VB files in scope`, ui `no UI files in scope`, the others `no code files in scope` |
| 6 | architecture: the profile declares neither `architecture.platformFree` nor `architecture.reviewers`, and `qa.lanes.architecture` is not true | not applicable | `profile declares no architecture` |
| 6e | architecture, external repository: no rule file (replaces 6; `qa.lanes.architecture` cannot force it) | not applicable | `no rule files in the repository (qa.profile.json rules)` |
| 7 | ui: no host but `cli` and `ai` - every other host has a user interface - and `qa.lanes.ui` is not true; never on an external repository, where the UI files of the scope decide | not applicable | `no host with a user interface (hosts: <hosts, or none>)` |
| 8 | profile `qa.lanes.<lane>` is false | skipped | `profile turns it off` |
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
screenshots `qa.ui.screens` names.

A lane bigger than `qa.batchTokens` content tokens (default 120000) is split into batches, files in path
order; one file over the cap is a batch alone. Each batch is one agent.

## Who runs each lane

| Lane | Agent | Instructions it reads | Id prefix | KIND it reports |
| --- | --- | --- | --- | --- |
| security | read-only subagent (Claude: general-purpose; Codex: a sub-agent) | `.claude/skills/qa/references/security.md` | SEC | vulnerability |
| bug | read-only subagent | `.claude/skills/find-bug/SKILL.md` (with "No SPEC.md for the file") | BUG | bug |
| architecture | the `architecture-reviewer` agent | its own definition; it reports in the finding format instead of one line per violation | ARC | architecture |
| smell | read-only subagent | `.claude/skills/qa/references/smell.md` | SML | smell |
| ui | read-only subagent | `.claude/skills/design-critique/SKILL.md`, `.claude/skills/accessibility-review/SKILL.md`, and the project's UI style skill if it has one | UI | ux |

The UI lane reads the `SKILL.md` of `design-critique` and `accessibility-review` as instructions because
those skills run only when the user types them - here the user typed `/qa` and agreed to the estimate.

With more than one batch, the prefix carries the batch number from the second batch on: `BUG2-1`.

## The prompt of one batch

The main session pastes this, filling the `<...>`:

```
/qa lane <lane>, batch <b> of <n>, run <run>
Instructions: read <instruction file> and follow it for this lane.
Read every file in the list below - no more, no fewer - and nothing outside it except the documents the
instructions name (SPEC.md, ADRs, CODEMAP.md, the project's CLAUDE.md).
<output of: qa.ps1 files -Run <run> -Lane <lane> -Batch <b>>
Report each finding as one block in the format of .claude/skills/qa/references/findings.md, ids <PREFIX>-1,
<PREFIX>-2, ... Do not fix anything. End with the line "seen N/N" and, if any file was not read,
"not read: <path>, <path>".
```

When the static lane ran, the list of the smell lane ends with `Already reported by analyzers (do not
repeat): <rule> x<count>, ...` - the ten biggest static smell groups in scope - and the security lane's
with the static vulnerabilities the same way; the agent does not report those again.

## An external repository

With `-Out` (ADR-0034) the lanes read a repository that is not this project, by its own rules:

- **Rule files.** The `rules` key of `qa.profile.json` names them (paths or globs, each must match a file).
  Without the key, the defaults: `CLAUDE.md`, `AGENTS.md`, `ARCHITECTURE.md` and `BUILD.md` at the root; the
  Markdown files under a `docs/adr...` folder, `docs/decisions` and `specs`; and the `CLAUDE.md` or
  `AGENTS.md` of any folder - never the agent config folders (`.claude`, `.agents`, `.codex`) or a generated
  folder. `"rules": []` is no rule file: the architecture lane is not applicable (row 6e).
- **A folder's own rules.** A `CLAUDE.md` or `AGENTS.md` below the root goes only to a batch with a file
  under that folder; every other rule file goes to every architecture batch. The estimate counts them.
- **The prompt** of every batch gains two lines after the instructions line:

```
The repository is <repo> (read only): every path of the list is relative to it; read <repo>\<path>.
Judge against its own rules - for the architecture lane exactly the files on the rules: line - never against this project's ADRs or profile.
```

- **A branch scope.** The list is the kit's own read-only git (ADR-0037), not review-files; the static lane still builds the whole project and keeps only findings in the files of the scope - the rest is counted `outside scope`.
