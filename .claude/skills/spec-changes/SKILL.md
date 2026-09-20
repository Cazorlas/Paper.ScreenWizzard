---
name: spec-changes
description: Show or record what a branch changed in each SPEC.md - section by section, the rules added, the rules changed (now and before, changed words marked) and the rules removed, with added drawings shown - as a preview in the session or as one generated file per feature per branch, plus a summary table for the pull request. Run only when a person asks for it.
disable-model-invocation: true
---

# spec-changes

**A person starts this, never the session on its own.** At the end of a task that changed a `SPEC.md`,
ask once - "show the spec changes?" - and wait. Never run the script, write a `spec-changes/` file or commit
one unasked.

```
python .claude/skills/spec-changes/spec_changes.py --preview          # print, write nothing
python .claude/skills/spec-changes/spec_changes.py                    # write the files, print the PR table
python .claude/skills/spec-changes/spec_changes.py --preview main     # another base; default origin/main
```

Both compare the **working copy** (uncommitted edits included) with the merge base of the branch and the
base, so a preview mid-task shows what the spec says now. Fetch the base first (`git fetch origin main`).
Exit `0` done - "No SPEC.md changed" included - and `2` when the base or the repository cannot be read.

| The person asks | Run | Then |
| --- | --- | --- |
| to see what the spec changes are | `--preview` | show the output; nothing is written |
| to record them, usually when the pull request is ready | no flag | show the table; commit only if asked |

## What it writes

For each `SPEC.md` the branch changed, one file beside it:
`docs/features/<slug>/spec-changes/<date of the branch's first commit>-<last part of the branch name>.md`
(a branch `task/stair-width` whose first commit is dated 2026-09-17 writes a file named
2026-09-17-stair-width.md). The name is the same on every run, so a second run rewrites the file. In name order the folder is the feature's requirement history,
readable in any markdown viewer.

**The file is generated - never edit it by hand, never write one without the script.** A hand-edited
record is a second copy of `SPEC.md`, and it drifts.

Each section whose rules changed lists:

| Under | Shows |
| --- | --- |
| **Added** | the rule in full; a drawing is the picture itself |
| **Changed** | *Now:* the new rule with the new words **bold**; under it *Before:* the old rule with the dropped words ~~struck~~ |
| **Removed** | the old rule, struck through |

**Only rules count**: a bullet, a numbered step, a table row (read as `first cell: the rest`) and a
drawing. A paragraph explains a rule and is left out, reworded or not, and so are quoted lines - the draft
banner and the change markers. That is safe only because skill `spec` gives every rule its own acceptance
line; a rule stated only in a paragraph is a spec to fix, not a reason to list paragraphs.

Two rules count as one rule changed when most of their words match; a rule rewritten beyond that shows as
one removed and one added. **Reword a rule only when the rule changes**, so every listed change is one a
reviewer should look for in the code.

A relative link is re-pointed one folder up, so a drawing shows from `spec-changes/`. It shows the picture
as it is now: **give a changed drawing a new file name** when old records must keep showing the old one.

The printed table goes into the pull request description - **the person posts it**; this skill never
writes to the pull request.
