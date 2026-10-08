---
name: check-spec
description: Use whenever a SPEC.md or one of its spec/ part files was written or changed - in /task-spec, when closing it in /task-verify, in /spec-backfill or /sync-docs - or to see whether the specs have drifted.
---

# check-spec

```
python .claude/skills/check-spec/check_spec.py
```

Exit `0` when clean, `1` when any check finds a problem. Runs from anywhere in the
repository, in any kind of project - it reads markdown and file times only.

## The code-name check

**`SPEC` — a requirement written in the code's words.** A `SPEC.md` names nothing from the code: no class,
method, property, and no test. It is written in the user's words, which is what keeps it from drifting —
a contract that promised `StairWidth` drifted the day the code renamed it to `Width`, while *"the user types
the stair width, default 1000 mm"* could not have. A test name is no exception: the acceptance line **is**
the test case.

What counts as a name: a backticked word with a lowercase letter and either two capitals
(`NamingAndExportCommand`, `AStationLosesItsBar`) or an underscore between letters
(`Plan_Station_LosesItsBar`). Two things pass on purpose:

- **A value the user picks** — `Always`, `None`: one word, one capital.
- **A name the user sees in capitals** — a view, a parameter, a setting key such as `PAPER_PLAN_VIEW`: no
  lowercase.

It is the mirror of `check-code-map`, which **requires** a code map to name code so it can be checked
against it. The two scripts share the backtick pattern and the list of framework names that are not ours.

## Beside the spec - F6 and F7

**`F6` - a drawing whose picture is stale.** Every `shapes/*.svg` in a spec's folder needs the PNG rendered
from it, no older than the SVG (two seconds of slack, because a checkout writes the pair a moment apart).
The spec links the PNG, so a newer SVG means the reader sees a shape the source no longer says. Fix: run
`.claude/paperflow/render-shapes.ps1` on the folder and look at the picture.

**`F7` - a requirement never closed.** A quoted line in `SPEC.md` carrying the draft banner (Bản nháp chờ
duyệt) or a change marker (chờ kiểm) while its plan's status is `xong` or `done`. The marker is judged by
the brief or plan it names (a marker naming `YYYY-MM-DD-<task>` is judged by `YYYY-MM-DD-<task>-plan.md` in the same folder); one that
names neither is left over when no plan in the folder is still open. A rule that merely mentions the marker
words is not a quoted line and is never reported. Fix: close `SPEC.md` as skill `spec` says.

## Two languages - F26

**`F26` - a requirement in one language only.** A `SPEC.md` carrying the draft banner or a change marker
needs both parts in the one file: a `# English` heading and a `# Tiếng Việt` heading, each over the whole
spec (skill `spec`, "Two languages"). When both are there they must keep one shape: the same number of `##`
sections, the same number of acceptance lines (`Given` / `Cho`), the same `F` codes; the report names what
differs. A spec nobody is changing is not asked for the second part, so old specs get it on their next
change.

## A requirement split into a tree - F30, F31

Over 300 lines a `SPEC.md` becomes an index and one `spec/<part>.md` per part (skill `spec`,
`spec/references/split-spec.md`). Every check above runs on each part as on `SPEC.md`, and two more on the tree:

- **`F30`** - a file in `spec/` the index does not link, or an index link to a part that is not there. The
  agent reading the index would never find the one, and would look for the other in vain.
- **`F31`** - an F code on two rows of the index (per language part), or an F table in a part. The F table
  lives only in the index; a test or a bug that names a code must mean one case.

An unsplit `SPEC.md` over 300 lines is listed under **Long** with its line count - a reminder, never a
problem and never a non-zero exit, because splitting is its own task.

## What it cannot catch

A brief (`YYYY-MM-DD-<task>.md`) - it names no code either, but it is not a `SPEC.md`, so only reading
checks it. The plan beside it names code on purpose and is never checked.

A spec written in plain words that describes the implementation anyway — a data structure, a layout, a
step of the algorithm. Only a reader can see that; skill `spec` says what a spec does not contain, and
`spec-backfill` is the session that reads a spec against its code.
