---
name: check-code-map
description: Check every CODEMAP.md in the Paper solution in a few seconds - which claims name a code symbol that no longer exists, which maps a session cannot enter cheaply, which have grown past a map, and whether a retired INSTRUCTION.md is back. Use at the end of a task that touched a CODEMAP.md, after renaming, moving or deleting a public type or file, or to see how far the maps have drifted.
---

# check-code-map

```
python .claude/skills/check-code-map/check_code_map.py
```

Runs from any directory. Exit `0` when clean, `1` when a map is stale, cannot be entered cheaply, or an
`INSTRUCTION.md` has reappeared.

## Why this exists

**Nobody reads a `CODEMAP.md` routinely - Claude writes them and Claude reads them back.** That loop has
nothing checking it, so a drifted claim sends the next session confidently to the wrong file, which is
worse than no map. This script is the check, and it only works because **every claim in a map names a
code symbol**. A claim naming nothing cannot be verified by anyone.

## The checks

| Report | Means | Blocks exit 0 |
| --- | --- | --- |
| `STALE` | a `` `CamelCase` `` in a map is not an identifier, file or folder name anywhere in the repository (bin, obj, .git and build output skipped) | yes |
| `MAP` | fewer than two opening lines before the first heading, or a 250-line block with no heading | yes |
| `INSTRUCTION` | an `INSTRUCTION.md` / `INSTRUCTIONS.md` exists again - that layer was retired into `CODEMAP.md`, `docs/progress/` handovers and comments | yes |
| `MORE THAN A MAP` | a map past 80 lines, so it still carries rules or traps | no - backlog |

The inverse check - a `SPEC.md` must name **no** code - is skill `check-spec`, which shares this script's
backtick pattern and its list of framework names.

## The output is a list to triage

`STALE` over-reports on purpose. Three kinds come back:

| Kind | Example | Do |
| --- | --- | --- |
| A framework or host API type | `FormattedText`, `HttpContext` | add it to a names file in `external/` beside the script, one per line - a host pack ships its own, a project adds another file there |
| Named because it is gone | *"`OldService` was removed"* | already filtered by `ABSENCE`; add a word if one slips through |
| **Genuinely drifted** | `RouteSolver` where the code has `NetworkRouteSolver` | **fix the map**, or delete the row if the code is gone |

Fix the map to match the code - **never the code to match the map**. A spec is the other way round: the
spec wins, and a disagreement is a bug for `find-bug`.

## What it cannot catch

A claim whose symbols all still exist but whose statement is now false. That surfaces only when a session
reads that code again - so correct a drifted claim in the same task that finds it.
