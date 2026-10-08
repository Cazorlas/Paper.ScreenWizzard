---
name: review-architecture
description: On-demand architecture review of a folder, a branch, some files or the whole project - the project's own architecture rules, code smells, and a ranked table of the most complex methods and files from the free analyzer build; one report of proposals, nothing fixed. Manual only - run when the user types /review-architecture; never on its own during a task, where /task-verify and the architecture-reviewer agent own the review of a plan's change.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder> | <file> <file> ...] [--only architecture,smell] [--skip lanes] [--static-only] [--all] [--all-files] [--yes] [--repo <path> --out <folder>]"
---

# /review-architecture

**One command, one report, nothing fixed.** `/review-architecture` asks one question of a scope: does its structure
follow the rules, and where is it too tangled to change safely? Three lanes: `static` (the shared analyzer build, free
in tokens: its smells and the complexity table), `architecture` and `smell`, read only. Behaviour is `/review-bugs`'s
question, attackers are `/review-security`'s, screens are `/review-ui`'s; none of the four replaces `/task-verify`
(ADR-0031, ADR-0044).

The engine is shared by the four reviews: `.claude/paperflow/review/review.ps1`, run as
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/paperflow/review/review.ps1 <command> -Kind architecture ...`.
Follow [the steps](../../paperflow/review/steps.md) (`.claude/paperflow/review/steps.md` from the project root; from the
plugin, under its `kit:` folder); this page says only what is particular to this review.

| The user says | Parameters of `lanes` and `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project |
| `project` | `-Scope project` |
| `branch [base]` | `-Scope branch [-Base <base>]` |
| a folder | `-Scope path -Path <folder>` |
| one or more files | `-Scope files -Files <file>,<file>` |
| `--only` / `--skip` | `-Only` / `-Skip` with lanes of this review: static, architecture, smell |
| `--static-only` | `-StaticOnly` (the complexity table and the analyzer smells only, no token spent) |
| `--all` / `--yes` | none (every lane that applies, no lane question) / `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command |

## What runs

1. **The analyzer build first, free.** The static lane is not a choice: it runs whenever it applies (a .NET project and
   the build verb), before any reading lane, unless the user said `--skip static`. One build serves every review of the
   same checkout state: a run whose commit, changes and analyzers match an earlier run's reuses that build (the steps,
   "Static"). This review lists the analyzers' smells; their bugs and vulnerabilities are counted, left to `/review-bugs`
   and `/review-security`. Mechanics: [static analysis](references/static-analysis.md).
2. **The lane question, one question**, only when both reading lanes apply and the owner named none (Claude:
   AskUserQuestion, `multiSelect: true`, header `Lane`, text `Rà kiến trúc <scope>: đọc lane nào? Phân tích tĩnh và bảng
   độ phức tạp luôn chạy trước, miễn phí.`), options `Cả hai`, `Kiến trúc`, `Smell`, each with its tokens. Codex: print
   the numbered lines and end the turn. A sentence ("chỉ kiến trúc") is read and confirmed as the steps say.
3. **The reading lanes** use, pasted by `review.ps1 prompt`: architecture - `.claude/agents/architecture-reviewer.md` with
   the project's ADRs and profile (on an external repository: that repository's own rule files, the `rules:` line); smell
   - [smell](references/smell.md). A finding cites the rule it breaks - an ADR line, a profile rule, the repository's own
   rule file - and is a proposal for `/task-spec`, never a bug ledger entry. Who reads - you alone, Codex through collab,
   or subagents - is the estimate's `lanes run:` line. With the collab base, architecture uses Claude subagents;
   smell stays with Codex through collab. Batches keep related files together by the nearest ancestor with
   SPEC.md or CODEMAP.md; only a unit larger than the batch limit is split.

Start from the batch list. Read any repository file needed to trace a call, another implementation or a test,
and run read-only commands that write nothing to prove a finding. `seen N/N` reports coverage of the list.
A valid WHERE outside that list remains a finding, marked `(ngoài lô)` in the report.

## The complexity table

The report's section `## Complexity` ranks what Sonar reported over its limits, from the static lane's own SARIF:
members (cognitive complexity S3776 and cyclomatic complexity S1541, one row per member), files (members over a limit
and their cognitive sum - one class per file in most C# code) and expressions (S1067, conditional operators), top 20 of
each by default (`review.complexityTop`). The kit's analyzer configuration turns the three rules on;
`review.complexity.limits` sets other limits. `review.ps1 static` prints one line with the worst member. No row means
nothing over a limit, or the project's own configuration turned a rule off: the section says so, never "clean". The
three rules appear only in this table. Format and ranking: [static analysis](references/static-analysis.md).

## SonarQube, when the project declares it

With `review.sonarqube.projectKey` in the profile and `PAPER_SONARQUBE_TOKEN` on the machine, run
`review.ps1 sonar -Kind architecture <scope>` before `plan`: it reads the self-hosted server's measures and open issues
for the commit you are on (scanning first only when the server has no analysis of it, starting and stopping a local
install when the machine declares one), and `plan` then hands the reading lanes only the top `review.sonarqube.hotspots`
files (default 20) - say `--all-files` (`-AllFiles`) to read every file. Exit 5 is not applicable with the reason (a branch
scope, not the base branch, uncommitted work, an external repository...), exit 4 not verifiable: either way go on with
`plan`, which then uses the analyzer build alone. On a repository without .NET code the static lane is not applicable; the
SonarScanner CLI scans it (no build) and the server's issues give the complexity table. Never print, write or pass the
token on a command line. Mechanics:
[SonarQube](references/sonarqube.md).

## Never

- Fix code, restructure, write the bug ledger, commit, or change the project's configuration.
- Start a reading lane before the user saw the estimate and agreed (`approve`).
- Report an architecture finding without the rule it breaks, or a lane that did not run as clean.
- Show a complexity table from a build that was not verifiable.
- Run on another repository without `-Out`, or write or guess its build line.
