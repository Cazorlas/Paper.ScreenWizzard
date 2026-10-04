# /qa findings, verdicts and proposals

One format for every lane, checked by `qa.ps1 check`; one verdict format for every reader. Machine-read:
a block that does not fit is sent back, never guessed at.

## A finding

```
FINDING   BUG-1
LANE      bug
KIND      bug
SEVERITY  major
WHERE     src/Ducts/CapPolicy.cs:42
RULE      "Every open duct end gets a cap" (docs/features/ducts/SPEC.md, Caps)
INPUT     a duct 0 mm long between two fittings
WHY       The cap is skipped when the length is 0, so the end stays open.
FIX       Treat length 0 as an end in the early return at line 40.
```

- A key in capitals at the start of a line, then at least one space, then the value. A line indented by
  two spaces or a tab continues the value before it. Other lines are ignored.
- A block starts at `FINDING` and ends at the next `FINDING`, the `seen` line, or the end.
- FINDING: `<PREFIX>-<n>` of your lane (`BUG-1`, `SEC-3`, `BUG2-1` in a second batch), each once.
- LANE: the lane you were given. KIND: security `vulnerability`, bug `bug`, architecture `architecture`,
  smell `smell`, ui `ux`. SEVERITY: `critical`, `major`, `minor` or `info`.
- WHERE: `path:line`, line 1 or more, a path of your list.
- RULE: the rule broken, with its source - a SPEC line, an ADR, the project's CLAUDE.md, or the code's
  own contract when no SPEC covers the file (find-bug, "No SPEC.md for the file").
- INPUT: the value that gives the wrong result, or `-` for a suspicion. Never left out.
- WHY and FIX: what goes wrong, and the smallest change that repairs it. Never empty.
- No STATUS and no VERDICT: a lane reports; verifying is a separate reader.

After the last block: `seen N/N` (`đã xem N/N` reads the same), N the number of files in your list, and
`not read: <path>, <path>` for any file you did not read. No finding and `seen N/N` is a valid answer.

## What check does with an answer

Each error is printed as `<lane>-<b>: <id>: <error>`. A wrong block stays out of the report; the right
ones in the same answer count. The batch is sent back **once** with exactly those errors, or with only
the files not read (`qa.ps1 files ... -Retry`); the second answer is saved as `<lane>-<b>.2.md`. Still
wrong the second time: that batch is not verifiable, named with its first error, and the report says so.

## Which findings a reader verifies

| Finding | Status before any reader |
| --- | --- |
| KIND bug or vulnerability from an agent lane, with an INPUT | candidate |
| bug or vulnerability from the static lane, `critical` or `major`, not a `--static-only` run | candidate |
| an agent bug or vulnerability with INPUT `-` | `suspicion` |
| any other static finding | `machine` |
| architecture, smell, ux | `proposal` (work to plan, never the bug ledger) |

Candidates are ordered by severity, then lane (security, bug, static), then id number; the first
`qa.verifyCap` (default 10) are `queued` - `check` prints `verify: <id> (<lane>, <severity>)` - and the
rest stay `needs_validation (cap N reached)`.

A reader gets, for an agent finding, only the RULE verbatim and the INPUT - not WHERE, WHY, FIX, the
lane or who found it (find-bug, "Verifying a finding"); for a static finding, the rule id, its title, its
link and `path:line`, with: decide whether this code gives a wrong result or an exploitable path for some
input; confirmed needs that INPUT.

On an external repository, also give the reader the repository line check prints: it locates the code there, read only.

**Who reads.** `qa.ps1 prompt -Run <run> -Verify <id>` builds the reader's prompt (RULE verbatim, INPUT, and for a static
finding WHERE; nothing else). `check` names the reader on every `verify:` line: `reader claude` or `reader codex`. A
finding Codex found is read by Claude; a finding of the Claude worker, or of a batch with no record, is read by Codex; a run
whose lanes were answered by subagents, a static finding, and a run planned before this rule go to a Claude subagent, as
before. collab writes `<lane>-<b>.turn.json` beside each answer and `verdicts/<id>.turn.json` beside each verdict it answered
(agent, role, model, tokens, time); the report's `Who answered` and `Readers` sections come from them, and say `same agent
family` when the reader is of the finder's family because the other was not available.

## A verdict

Saved by the main session as `.paper/qa/<run>/verdicts/<id>.md`:

```
FOR          BUG-1
VERDICT      confirmed
INPUT        a duct 0 mm long between two fittings
EVIDENCE     Read CapPolicy.Decide: length 0 returns before the end check; the test input gives no cap.
FINGERPRINT  src/Ducts/CapPolicy.cs early return on zero length
```

VERDICT is `confirmed`, `rejected` or `needs_validation`; EVIDENCE is never empty; `confirmed` needs a
FINGERPRINT (the broken spot the reader located) and an INPUT. A verdict for an id nobody queued, or in
the wrong format, leaves the finding `needs_validation` with the error; a queued finding with no verdict
is `needs_validation (no verdict returned)`.

## Proposals

A bug ledger proposal comes only from a confirmed finding with an input, never from a suspicion, a
rejected or an unverified one. Confirmed findings with the same fingerprint (compared trimmed, lower
case, `\` as `/`, spaces folded) are one proposal. Each carries the command the main session runs once
the owner approves it: `/task-bug <WHY> (qa <run>: <ids>)`. Architecture, smell and UI findings are work
proposals for `/task-spec`, never ledger entries.
