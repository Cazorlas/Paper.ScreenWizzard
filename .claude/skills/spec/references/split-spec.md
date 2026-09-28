# A long requirement is a tree

The detail behind "A long requirement is a tree" in `SKILL.md`, kept here so the skill stays under its length
limit. Decision: ADR-0025 of the kit.

**A task reads the part of the requirement it touches, not the whole feature.** A large feature carries
several smaller ones; reading all of it for one small task pays for every line it does not use.

## When

- **Over 300 lines, split.** Skill `check-spec` reminds every unsplit `SPEC.md` longer than that; it is a
  reminder, never a problem.
- **Splitting is its own task**, opened by the user - never done inside another task, whose diff would then
  carry a move of hundreds of lines.
- Under 300 lines, keep one file: the index plus one part costs about as much as the whole.

## The shape

```text
docs/features/<slug>/
  SPEC.md            the index
  spec/<part>.md     one file per part - its rules, acceptance lines and edge cases
  shapes/  input/  spec-changes/   as before, one of each for the whole feature
```

`SPEC.md`, the index, keeps what every task needs:

```markdown
# <feature> - SPEC

<3-5 lines: what the user gets out of it>

## User story
## What the user does
## Inputs
## Outputs
## Key entities
## Parts

| Part | File | What it covers | F |
| --- | --- | --- | --- |
| Worktree | [spec/worktree.md](spec/worktree.md) | one worktree and branch per task | F8, F13 |
| Hooks | [spec/hooks.md](spec/hooks.md) | what a hook blocks, reminds and lets pass | F10, F27 |

## Edge cases                  only those that span parts
## When it does not do the job   the whole F table, every code
## Assumptions
## Clarifications
## What it does not do yet
```

A part, `spec/<part>.md`:

```markdown
# <feature> - <part>

Part of [SPEC.md](../SPEC.md).

## <group of rules>      a bold claim, why it exists, then its acceptance lines
## Edge cases            the boundaries of this part
```

- **The F table lives only in the index**, one series for the whole feature. A part names the codes it
  covers in its rules, never in a table of its own. The `brief` verb and `/task-verify` look a code up in
  one place, and no feature ever has two `F5`.
- **Every part is linked from the table of parts**, and every link there is a file.
- **A part is a requirement too**: no code names, both language parts while it is written or changed
  (`two-languages.md`), a draft banner or a `chờ kiểm` marker under the changed section of the part it is in,
  drawings linked as `../shapes/<case>.png`.
- A section name is unique across the tree - `spec-changes` matches rules by section name, so two parts
  both with "Edge cases" read as one section. Name it after the part: "Edge cases - worktree".

## Reading and writing

| Step | Reads |
| --- | --- |
| writing or changing the requirement | the index, then only the parts the task touches; a new rule that fits no part is a new part and a new row |
| the brief | names the parts that change, as it names the sections |
| `find-bug`, `/task-verify`, closing | the index and the parts the task touched |

## How to split

1. One part per group of rules a task would change together - usually each `## <group of rules>` of the
   old file, or a few small ones together. Aim for parts of 30-150 lines.
2. **Move the words unchanged, word for word.** `spec-changes` pools the rules of the tree by section name,
   so a rule moved as it was is no change and a reviewer sees nothing to review. Rewording in the same task
   makes every moved rule look new.
3. The F table stays whole in the index, the codes as they are - never renumbered.
4. Links from other documents to `SPEC.md` keep working; point a link at a part only where it names a rule.
5. Run skill `check-spec`: F30 (a part the index does not link, or a link to no part) and F31 (an F code on
   two rows, or an F table in a part) are the tree's own checks.
