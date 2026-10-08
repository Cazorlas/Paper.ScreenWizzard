---
name: review-security
description: On-demand security review of a folder, a branch, some files or the whole project - the free static-analysis build keeps only its vulnerabilities, then a read-only pass over the security checklist; one report, findings verified by a second reader, nothing fixed. Manual only - run when the user types /review-security; never on its own during a task.
disable-model-invocation: true
argument-hint: "[project | branch [base] | <folder> | <file> <file> ...] [--skip static] [--static-only] [--yes] [--repo <path> --out <folder>]"
---

# /review-security

**One command, one report, nothing fixed.** `/review-security` looks for places where a value an attacker controls
reaches something that trusts it. Two lanes: `static` (the shared analyzer build, free in tokens - one build serves every
review of the same checkout state; this review lists only its vulnerabilities and former security hotspots, its bugs and
smells are counted and left to `/review-bugs` and `/review-architecture`) and
`security`, read only, against [the checklist](references/checklist.md). It does not replace Claude Code's built-in
`/security-review` of pending changes, nor the global `security-audit` skill for a full audit (ADR-0044).

Follow [the steps](../../paperflow/review/steps.md) (`.claude/paperflow/review/steps.md` from the project root; from the
plugin, under its `kit:` folder) with `-Kind security`. Scope words are those of `/review-architecture`:

| The user says | Parameters of `plan` |
| --- | --- |
| nothing | none: the branch when it is ahead of its base or has uncommitted work, else the project |
| `project` / `branch [base]` / a folder / files | `-Scope project` / `-Scope branch [-Base <base>]` / `-Scope path -Path <folder>` / `-Scope files -Files <file>,<file>` |
| `--skip static` / `--static-only` / `--yes` | `-Skip static` / `-StaticOnly` / `-Yes` |
| `--repo <path> --out <folder>` | `-Repo <path> -Out <folder>` on every command |

No lane question: one lane reads, the static lane is free. The estimate is still the stop before paid reading.

With the collab base, security stays with Codex through collab. Batches keep files together by their nearest
ancestor with SPEC.md or CODEMAP.md, splitting only oversized units. Start from the list and read other
repository files needed to trace a finding; read-only commands that write nothing may prove it. `seen N/N`
is coverage information. A valid WHERE outside the list is marked `(ngoài lô)` in the report.

- A finding is a place, an attacker-controlled INPUT and the rule it breaks; without an input it is a suspicion
  (INPUT `-`), never a bug ledger proposal.
- Every vulnerability with an input is read again by the other model before it can become a proposal (the steps,
  "Verify"). Give `WHERE <path:line>` as a starting point; the verifier independently traces and reproduces it.
  The static lane's critical and major vulnerabilities are verified the same way.
- The security lane's list ends with the vulnerabilities the analyzers already reported; it looks for what an analyzer
  cannot see.

## Never

- Fix code, write the bug ledger, commit, or change the project's configuration.
- Start the security lane before the user saw the estimate and agreed (`approve`).
- Put a finding that is not confirmed, or has no input, among the bug ledger proposals.
- Report a static lane that was not verifiable as "0 vulnerabilities".
- Send a secret it found anywhere: the report names the file and line, never the value.
