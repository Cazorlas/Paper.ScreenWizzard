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
python .claude/skills/spec-changes/spec_changes.py --view             # the review page of every changed feature
python .claude/skills/spec-changes/spec_changes.py --view docs/features/<slug> main   # one feature, changed or not
```

Both compare the **working copy** (uncommitted edits included) with the merge base of the branch and the
base, so a preview mid-task shows what the spec says now. Fetch the base first (`git fetch origin main`).
Exit `0` done - "No SPEC.md changed" included - and `2` when the base or the repository cannot be read.

| The person asks | Run | Then |
| --- | --- | --- |
| to see what the spec changes are | `--preview` | show the output; nothing is written |
| to record them, usually when the pull request is ready | no flag | show the table; commit only if asked |
| to read the requirement, or review it on a page | `--view` | give the path of the page; publish it only if asked |

## What it writes

For each feature whose `SPEC.md` or any `spec/<part>.md` of a split one the branch changed, one file beside
`SPEC.md` - a split requirement is one feature, its rules pooled by section name across the index and the
parts, so a rule moved word for word from `SPEC.md` into a part is no change, and the index's table of
parts is navigation, not a rule:
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

## The review page - `--view`

One self-contained HTML page per feature, built from `SPEC.md` and its parts: a side column with the index and
every part, each F row linked to the part that covers it, and the rules this branch added (green), changed (new
words bold, the old rule struck under it) and removed (struck at the end of their section) - marked with the
same comparison as the record, so the two never disagree. Drawings are embedded, so the page travels as one
file; a Mermaid block is drawn when the browser is online and stays readable text when not.

- Written to `.paper/spec-view/<slug>.html` - outside git, rebuilt on every run; never commit it and never
  edit it: the text is the source.
- A folder with no `SPEC.md` exits `2` and writes nothing (F32); with no base the named feature's page is
  still built, nothing marked.
- Open it in a browser. In Claude Code the person may ask to publish it as an artifact; the page is a single
  HTML file, so it publishes as it is.
