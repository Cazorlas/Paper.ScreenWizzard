# The steps of the four review commands

Read by `/review-architecture`, `/review-bugs`, `/review-security` and `/review-ui` (ADR-0044). Each of them is a thin
skill that says its own question, its lanes and its scope words, then sends you here. Everything below is the same for the
four, except where a step names one of them. The engine is `review.ps1` in this folder; every command takes `-Kind
architecture|bugs|security|ui` on `lanes` and `plan` (the later commands read the kind from the run).

| Command | Question | Lanes |
| --- | --- | --- |
| `/review-architecture` | does the structure follow the rules; where is it too tangled | static (smells, complexity table), architecture, smell |
| `/review-bugs` | does the code do what its SPEC says, for every input | static (bug diagnostics are hints), bug |
| `/review-security` | can an attacker reach what trusts a value | static (vulnerabilities), security |
| `/review-ui` | can the screens be used | ui |

**One command, one report, nothing fixed.** Every finding lands in one report; a bug enters the ledger only after a
second reader confirmed it and the owner approved it.

## The reviews and /task-verify

`/task-verify` closes one plan: it reviews exactly the change of that plan against its SPEC, inside the task lifecycle, and
its verdict gates the merge. A review belongs to no plan and gates nothing: it is a look the owner asks for - before a
release, after a merge train, on code that never had a SPEC - and its output is proposals: bug ledger entries to approve one
by one, and work to plan with `/task-spec`. Never run one as a step of a task, and never let its report stand in for
`/task-verify` (ADR-0031). Claude Code's own `/code-review`, `/security-review` and `/simplify` and the global
`security-audit` skill are other tools; none of them replaces these.

## Running it

The script is `review.ps1` (in a project and in the plugin: `.claude/paperflow/review/review.ps1`; Codex runs the same
file, with `-Session codex`), always run as `powershell -NoProfile -ExecutionPolicy Bypass -File <script> <command> ...`.
The user's words map to its parameters:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project; with no base branch (or no merge-base) the project, saying why |
| `project` | `-Scope project` |
| `branch [base]` | `-Scope branch [-Base <base>]` |
| a folder | `-Scope path -Path <folder>` |
| one or more files | `-Scope files -Files <file>,<file>` (inside the repository; a file that is not there or is filtered out is named with its reason) |
| `--only a,b` / `--skip a,b` | `-Only a,b` / `-Skip a,b` (the lanes of the command that was typed; a lane of another command is refused, exit 2) |
| `--static-only` | `-StaticOnly` (not for `/review-ui`: it has no static lane) |
| `--all` | none: every lane that applies, without the lane question |
| `--all-files` | `-AllFiles` (read every file, not only the SonarQube hot spots) |
| `--yes` | `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command (`init` first, below) |
| Codex is the session | `-Session codex` on every command |

1. **Plan.** First the lane question - only `/review-architecture` asks one ("Which lanes: ask first", below); the other
   three ask nothing. If the project declares a SonarQube server, run `review.ps1 sonar -Kind architecture <scope>` before `plan`
   ("SonarQube", below). Run `review.ps1 plan -Kind <kind>` with the parameters. Show its output to the user as it is: the
   scope and why, one line per lane with its state, files, agents and tokens, the verify readers, who reads, the total.
2. **Stop at the estimate.** Unless the last line says `approved: lanes may start` (`--yes`, or nothing but static analysis
   runs), stop and ask one question: start these lanes, change them (the lane question again, or a narrower scope), or static
   only? A different choice is a new `plan`. Only after the user agreed, run `review.ps1 approve -Run <run>`. Until then
   `review.ps1 files` refuses (exit 4) - no lane gets a file list.
3. **Static lane.** `review.ps1 static -Run <run>` builds the project with the analyzers plugged in (see "Static", below).
   It is machine work: start it in the background and go on. Exit 4 is not verifiable with the reason - never "0 findings";
   exit 5 is not applicable.
4. **Agent lanes.** The estimate's `lanes run:` line says who answers the batches; never a lane before `approve`. Do not start a second big review on this machine while one runs its batches: Codex has a fixed number of places and a second sweep takes them.
   For `/review-ui`, before starting the UI lane, the main session captures screens in scope into the run folder
   when the host can run (desktop: `drive-wpf`; web: Playwright MCP), and supplies them in `screens`. If capture is
   unavailable, record `visual pass: not run - <reason>` in the report and identify the lane as a source-only review.
   For every supplied screen, request an info finding with RULE `overall`, first impression and repair priorities.
   The lane may use ui-ux-pro-max's search script to support FIX without writing anything.
   For `/review-ui`, each batch's file extensions select the instructions: web reads [ui-web.md](ui-web.md), then
   `skills/impeccable/reference/critique.md`, `skills/impeccable/reference/audit.md` and `skills/impeccable/reference/craft-floor.md`; desktop (`.xaml .axaml .dcl`) or no web reads
   `design-critique` then `accessibility-review`. Mixed batches read web first, then desktop. Exact extensions and
   paths: [lanes](lanes.md). The web preface requires read-only assessments in the same turn, no launcher, browser,
   sub-agents or writes, closing questions skipped, and this command's finding format.
   - `A alone - ...` (a small scope, "Who reads", below): read the files yourself. For each lane in state `run`:
     `review.ps1 prompt -Run <run> -Lane <lane> -Batch 1` writes the lane's prompt; follow it - read each file of its list
     once, answer in the format of [findings](findings.md) - and save the answer word for word to `answer:`.
   - `codex via collab - <collab> - ...`: run the `collab-start:` line of the estimate once - a read-only collab session on
     the real checkout (uncommitted work included) or the external repository; nothing is written there. For every lane
     in state `run` whose batch executor is `collab`, and every batch of it: `review.ps1 prompt -Run <run> -Lane <lane> -Batch <b>` writes the prompt and
     prints `prompt:` and `answer:`; then `<collab> ask -Session <id> -Task <lane>-<b> -Prompt <prompt> -Answer <answer>
     -WaitSec 0`. Exit 11: it runs. Exit 13 (the machine's turns are all taken): `<collab> wait -Session <id>`, then ask
     again. Exit 6 (Codex out of usage): ask that batch again with `-As takeover` (the Claude Sonnet worker), and the next
     batches too, with `<collab> probe -Session <id>` between asks - back to Codex once the probe says ok. Exit 7: the
     checkout changed during the turn - the answer is kept; name the printed paths to the user. Exit 1, 4 or 9: ask that
     batch once more; failing again, it has no answer. When every batch was asked, `wait` until no turn runs.
   - Batches with executor `subagents` (bug, architecture and UI when the base is collab) go to Claude subagents.
     The base `claude subagents - <reason>` (no codex on PATH, collab not installed, `-Executor subagents`, a collab turn runs the review):
     hand each batch's `prompt:` file to one read-only subagent (Codex: one sub-agent), all in one message, in the
     background.
5. **Save each answer.** collab writes the answer word for word to `answer:`, with `<lane>-<b>.turn.json` beside it
   (agent, role, model); never edit either. A subagent's answer, or your own, you save word for word to `answer:` yourself.
6. **Check.** `review.ps1 check -Run <run>`. Exit 1 lists what to send back, `<lane>-<b>: <error>`: run the same `prompt`
   command with `-Retry` and send that prompt once, the way the batch went first (collab: `-Task <lane>-<b>.2`); its
   answer is `<lane>-<b>.2.md`. Check again. There is no third time: a batch still wrong is reported not verifiable.
7. **Verify.** Each `verify: <id> (<lane>, <severity>) reader <agent>` line names a reader that is not the agent that
   found it. `review.ps1 prompt -Run <run> -Verify <id>` writes its prompt - the RULE verbatim and the INPUT, plus WHERE as a starting point, never
   WHY, FIX, the lane or who found it - and prints `prompt:` and `answer:`. `reader claude`: one read-only Claude
   subagent per finding, all in one message, given only that prompt; save its block word for word to `answer:`.
   `reader codex`: when the run was read alone, run the `collab-start:` line of the estimate now (it was kept for this), then `<collab> ask -Session <id> -Task verify-<id> -Prompt <prompt> -Answer <answer>`; Codex unavailable,
   a fresh read-only Claude subagent reads it instead (the report shows both are Claude). In a Codex session the reader is a fresh Codex sub-agent (the report says it is the same family). A reader that returns nothing
   leaves the finding not verified - do not invent a verdict. Then `<collab> report -Session <id> -Final` closes the
   session.
8. **Report.** `review.ps1 report -Run <run>` writes the report and prints its path. Give the user a link that opens
   (paperflow rule 9), a short summary in Vietnamese, and ask which bug ledger proposals to open. Each one approved becomes
   `/task-bug` with the command line the report gives; nothing else does.

The checklists the lanes read: security - `.claude/skills/review-security/references/checklist.md`; smell -
`.claude/skills/review-architecture/references/smell.md`; the bug lane reads `.claude/skills/find-bug/SKILL.md`; ui reads
`design-critique` then `accessibility-review`. Which lanes run, who answers each and how its prompt is built: [lanes](lanes.md);
the format of a finding and of a verdict: [findings](findings.md); the prompts: [prompts](prompts.md).

## Who reads

`review.ps1` decides, from the files the reading lanes read (each file once, plus 1,600 tokens a screenshot):

- At most `review.soloTokens` content tokens (default 40,000; 0 turns this off): you read alone.
  The estimate says `lanes run: A alone - <n> files, ~<tokens> content tokens (solo limit <limit>)`; one turn per lane, no
  Codex turn for a lane. Findings that need reading back are read by Codex through a read-only collab session that opens only then and closes after
  (when the machine has collab and codex - the estimate's `collab-start:` line, "only if check queues a finding"), else by a
  fresh subagent (the report says when it is the same family as you).
- More: the old way - the lanes go to Codex through collab, else to subagents.
- `-Executor solo` forces the first (refused over the limit, exit 2, with the tokens and how to narrow: a folder, a few
  files); `-Executor collab` or `subagents` force the others.

## Static

One analyzer build serves every review of the same checkout state. `review.ps1 static` lists only the findings of the
command's own kind: the architecture review - smells (the three complexity rules go to the complexity table), the security
review - vulnerabilities, the bugs review - nothing: its bug diagnostics are hints (`Analyzer hints (check, do not copy)` at
the end of the bug lane's file list, and a report section; never read again, never a bug ledger proposal). The other kinds
are counted, with the command that lists them. A later review on the same commit, the same uncommitted content, the same
analyzers, configuration and build line says `review: static - reused the build of run <run>` and does not build again; one
file edited or a commit later it builds again. Mechanics and the profile keys:
[static analysis](../../skills/review-architecture/references/static-analysis.md).

## SonarQube (optional, /review-architecture only)

With `review.sonarqube.projectKey` in the profile and `PAPER_SONARQUBE_TOKEN` on the machine, run
`review.ps1 sonar -Kind architecture <scope>` before `plan`. It asks the self-hosted server (starting and stopping a local
install when the machine declares one), scans only when the server has no analysis of the commit, and saves the numbers of
the files and the open issues. `plan` then hands the architecture and smell lanes only the top
`review.sonarqube.hotspots` files (default 20); `-AllFiles` reads them all. Exit 4: not verifiable (the server does not
answer, did not come up, the analysis failed) - say the reason and go on with `plan`, which reads every file; exit 5: not
applicable (a branch scope, not the base branch, uncommitted work, an external repository, no key, no scanner...) - say the
reason and go on. A repository without .NET code (a Python or TypeScript project) is scanned by the SonarScanner CLI, with no
build: the static lane is then not applicable, the report's `## Complexity` is built from the server's issues and says so, and
a file the analysis did not measure is left out with its own reason. Never print, write or pass the access key on a command
line. Mechanics: [SonarQube](../../skills/review-architecture/references/sonarqube.md).

## Which lanes: ask first

Only /review-architecture asks, and only when both of its reading lanes apply. `/review-bugs`, `/review-security` and
`/review-ui` have one reading lane: the estimate is their stop. Static is not a choice: it runs first and is free; the owner
leaves it out by saying `--skip static`.

1. **List.** `review.ps1 lanes -Kind architecture` with the scope parameters of `plan` (and `-Repo`/`-Out`) writes nothing.
   It prints the scope; the static line (`static: runs first, free (<n> C#/VB files)`, or why it does not apply); one
   numbered line per reading lane that applies, with its tokens, files and agents; a line per lane that does not apply or
   is turned off, with the reason; the verify readers; who reads; `all:`, the total; and `ask:`, the option group. Exit 5:
   nothing applies - show it and stop. On an external repository with no scope given, `lanes` may print `review-scope:`
   instead - the branch is off its base: ask the scope first ("A branch of an external repository", below), then run
   `lanes` again with it.
2. **Ask.** Claude (AskUserQuestion): one question, `multiSelect: true`, header `Lane`, text `Rà kiến trúc <scope>: đọc
   lane nào? Phân tích tĩnh và bảng độ phức tạp luôn chạy trước, miễn phí.` Options, with the numbers of the lanes' lines:

   | `ask:` | label | description |
   | --- | --- | --- |
   | all | Cả hai | Kiến trúc và smell - <total> token, gồm xác minh |
   | architecture | Kiến trúc | <tokens> token - <n> file, <a> agent |
   | smell | Smell | <tokens> token - <n> file, <a> agent |

   The box adds Other by itself: that is "Tự gõ". Codex, or any agent with no choice box: print the numbered lines as they
   are, then `all = cả hai (<total> token)` and `Hoặc gõ một câu, vd: chỉ kiến trúc`, and end the turn.
3. **Pick.** Run `review.ps1 lanes -Kind architecture <scope> -Pick <answer>`: the lanes ticked (Cả hai = `all`), or the
   numbers, `all` or lane names typed. `-Pick static` is refused: static is not a choice. A sentence - Other, a Codex answer,
   or the owner's own words after the command - you read: the lanes it names (kiến trúc/architecture, smell/refactor), "trừ
   X" as the other lane, and the scope it names (a folder: `-Scope path -Path <folder>`; nhánh, phần đổi, thay đổi, so với
   <x>: `-Scope branch`, with `-Base <x>` when it names one; cả dự án, cả kho: project; some files: `-Scope files -Files`).
   A sentence that names only a scope: run `lanes` with that scope and ask the lane question - never pick lanes for it. Once
   the scope changed, pick by lane names: the numbers belong to the first list. A sentence that names no lane: ask again -
   never guess one.
4. Exit 2 names a number not on the list, a lane that does not apply here and why, `static`, or a word that is no lane: say
   it in Vietnamese and ask again. Exit 0 prints `review-pick:` (the lanes and the scope) and `plan:`, the exact command line.
5. **Confirm a sentence.** Show the lanes, the scope and the `plan:` line and ask `Đúng chưa?` (Claude: header `Xác nhận`,
   options `Đúng, lập plan` and `Chọn lại`; Codex: `y`, or a new answer). Boxes or numbers need no confirmation: the
   estimate is the next stop. Then step 1 runs the `plan:` line.

No question when the owner's words already set the lanes (`--only`, `--skip`, `--static-only`, `--all`), with `--yes` (a
run nobody answers: every lane that applies), when `ask:` says `none` (one reading lane applies: plan it), or when `lanes`
exits 5.

## An external repository (read only)

A review also covers a repository it must never write into - another team's code, judged by its own rules (ADR-0034). Add
`-Repo <repository> -Out <folder outside it>` to every command.

1. **Once:** `review.ps1 init -Repo <repository> -Out <folder>` writes `<folder>/review.profile.json` (never over an
   existing one - and not beside an old `qa.profile.json`, which is still read, with a line to rename it; run again, it checks
   the file) and lists the rule files it found. The owner fills in `build.command` - the build line, run from the repository
   root - and `build.noDeploy`, every MSBuild property that turns deployment off (`DeployAddin=false`), each also written in
   the command as `-p:Name=Value`. Until both are there the static lane is not applicable. Never write or guess the build
   line yourself.
2. Then steps 1-8 above, every path under `<folder>`: runs in `<folder>/runs/<run>/` (save `lanes/` and `verdicts/` there),
   the report in `<folder>/reports/`. Scopes are `project` (the default), `path` and `branch [base]`, and `files`.
3. Every file list has a `repo:` line: its paths are relative to that folder - tell each agent to read them there, and each
   verify reader to look for the code there (`check` prints it). The architecture list ends with a `rules:` line: the
   repository's own rule files for that batch; the architecture batch carries them: `review.ps1 prompt` pastes them with
   architecture-reviewer's instructions, never this project's ADRs or profile.
4. Bug proposals carry no `/task-bug`: the repository is not this project's; the owner reports them to its owners.

### A branch of an external repository

`-Scope branch [-Base <branch>]` reads the files the open branch changed since it left its base - committed, then modified,
staged or new on disk - with the kit's own read-only git (ADR-0037); nothing is fetched. The base: `-Base`,
else `origin/HEAD`, `origin/main`, `main`, `master`, the first on this machine. `lanes`, the estimate and the report carry a
`base:` line - the base, the merge-base, the commits ahead and how old the base is here (`STALE` or
`MAY BE STALE`: the owner fetches if a fresh base matters; never fetch for them) - and a `changes:` line.

`review-scope:` (`lanes` with no scope while the branch is off its base): ask the scope first, alone. Claude: header
`Phạm vi`, one choice, text `Rà cả kho hay phần đổi của nhánh <branch>?`, options `Cả kho` (`<n> file`) and
`Nhánh <branch>` (`<m> file đổi so với <base>`); Codex: print the numbered lines and `Hoặc gõ: rà phần đổi so với
<nhánh gốc>`. Then run `lanes` with the scope picked (`-Scope project`, or `-Scope branch` with `-Base` when the answer names
a base) and ask the lane question, if the command asks one.

**From the plugin** (ADR-0038, ADR-0044): `/paper-kit:review-architecture`, `/paper-kit:review-bugs`,
`/paper-kit:review-security` or `/paper-kit:review-ui` in a session whose repository has no review engine of its own runs
this copy with `-Repo` the session's repository and `-Out` the folder it names (default
`%LOCALAPPDATA%\paper-kit\review\<name>-<code>`; an old folder under `paper-kit\qa` that holds a profile is kept and
named). Codex has no plugin command: run `paper-kit/scripts/review-entry.ps1 -Kind <kind> -Repo .` from a Paper-skills
checkout and follow its lines. Codex as the session: add `-Session codex` to every command - a sub-agent of Codex has no
network for the collab session.

Nothing is written into the repository - not `.paper/`, not its `.claude/`, not git's index - and none of its scripts runs
except the declared build. Files that build writes into folders git ignores are the build's own; a build that changes
anything else leaves the static lane not verifiable, naming the files.

## Never

- Fix code, write anything under the bug ledger, commit, or change the project's config.
- Start a reading lane before the user saw the estimate and agreed (`approve`), or restrict tracing to the batch list: readers may read any repository file needed to prove a finding.
- Let a lane verify its own findings, let a finding be read by the agent that found it while the other is available, or put
  a finding that is not `confirmed` with an INPUT in the bug ledger proposals.
- Report a lane that did not run as clean: not applicable, skipped and not verifiable each say why.
- Run a review on another repository without -Out, or edit its review.profile.json build line yourself.
- Plan before the owner picked the lanes (unless no question is asked), or plan the reading of a sentence the owner has not
  confirmed.
- Send a secret a lane found: the report names the file and line, never the value; and never print or pass the SonarQube key.

## Exit codes of review.ps1

| Exit | Meaning |
| --- | --- |
| 0 | done |
| 1 | `check` found answers to send back |
| 2 | invalid request, broken profile key, unknown lane or run, a lane of another command, `-Executor` that cannot be met, an instruction file or a queued finding that is not there (`prompt`); `not verifiable:` for a branch scope with no base branch, a wrong review.profile.json or -Out (external repository) |
| 4 | not verifiable: the run is not approved yet (`files`, `prompt`), the static lane could not run (no dotnet, a package not downloaded, the build red, an analyzer that did not load), or the SonarQube layer could not (`sonar`) |
| 5 | NOT APPLICABLE: no file in scope, or the lane does not run (`files`, `prompt`), or the SonarQube layer does not apply (`sonar`) |

## Paper-skills itself

Paper-skills runs the reviews from the payload, not from a vendored copy:
`powershell -NoProfile -ExecutionPolicy Bypass -File paper-kit/payload/.claude/paperflow/review/review.ps1 plan -Kind <kind> -Repo .`
(and the same path for every other command). Being PowerShell only, its static, architecture and UI lanes say not
applicable with the reason; security, bug and smell run. The three pointer skills in `.claude/skills/` (there is no
`/review-ui` here) send you to the payload skills.
