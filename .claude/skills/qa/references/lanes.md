# /qa lanes

Read by the main session when it hands out the agent lanes (step 4 of `/qa`). `qa.ps1 plan` already
decided which lanes run; this page says why, which files each lane reads, and what each agent is told.

## Which lanes run

The lanes are `static`, `security`, `bug`, `architecture`, `smell`, `ui`, in that order. For each, the
first rule that matches decides:

| # | When | State | Reason printed |
| --- | --- | --- | --- |
| 1 | a name in `-Only`/`-Skip` is not a lane | refused (exit 2) | `unknown lane 'x' (static, security, bug, architecture, smell, ui)` |
| 2 | a lane is in both `-Only` and `-Skip` | refused (exit 2) | `lane 'x' is in both -Only and -Skip` |
| 3 | `-StaticOnly`, any lane but static | skipped | `-StaticOnly` |
| 4 | `-Only` given and the lane not in it | skipped | `-Only` |
| 5 | the lane in `-Skip` | skipped | `-Skip` |
| 6 | profile `qa.lanes.<lane>` is false | skipped | `profile turns it off` |
| 7 | static: no .csproj, .vbproj, .fsproj or .sln in the repository | not applicable | `no .NET project` |
| 8 | static: the profile has no `build` verb | not applicable | `no build verb` |
| 9 | the lane has no file in scope | not applicable | static `no C# or VB files in scope`, ui `no UI files in scope`, the others `no code files in scope` |
| 10 | architecture: the profile declares neither `architecture.platformFree` nor `architecture.reviewers`, and `qa.lanes.architecture` is not true | not applicable | `profile declares no architecture` |
| 11 | ui: hosts have neither desktop nor web, and `qa.lanes.ui` is not true | not applicable | `no desktop or web host` |
| 12 | otherwise | run | |

## Files of each lane

| Group | Extensions |
| --- | --- |
| code | .cs .vb .fs .ps1 .psm1 .psd1 .py .ts .tsx .js .jsx .mjs .cjs .go .java .c .cc .cpp .h .hpp .rs .sql .sh |
| config | .json .config .xml .yml .yaml .props .targets .csproj .vbproj .fsproj .sln .addin .manifest .toml .ini |
| ui | .xaml .axaml .cshtml .razor .html .htm .css .scss .less .vue .svelte .tsx .jsx |

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
