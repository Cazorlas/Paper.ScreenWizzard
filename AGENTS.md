<!-- paper-kit:start -->
## Paper kit

Paper-kit setup writes this block and replaces it on every run, so edit outside the markers. The block
only points. Rules live in the `CLAUDE.md` files it names, which take precedence over this file.

Read in this order:

1. `CLAUDE.md` at the repository root.
2. The `CLAUDE.md` nearest the folder you change. Nested ones here: none yet.
3. `CODEMAP.md` at the root, the index of every folder's map, then the map nearest the code.
4. For a feature: `docs/features/<slug>/SPEC.md`, then its open brief and plan.

Lifecycle: task-spec (SPEC.md, brief, plan) -> stop for the user's approval -> task-do -> task-verify, end to end in paperflow; each step is `.agents/skills/<name>/SKILL.md`, and `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` is the gate.

Every task worktree must be attached to the checkout that created it as its immediate parent. Follow task-worktree to record and verify that parent; in Orca pass an explicit --parent-worktree and read it back. Use --no-parent only when the user explicitly requests independent work.

Memory is Claude Code's memory of this repository, shared: Claude and Codex read and write the same folder, and there is no second store - no memory or notes file in the repository. The session-start hook hands you the folder, its index (MEMORY.md) and the rules for writing it. If that block did not reach you, run `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/paperflow/paperflow.ps1 memory` and follow what it prints.

Codex runs the kit's hooks from `.codex/hooks.json`. These have no Codex equivalent, so check by hand what they would have checked:

- none: every kit hook runs in Codex too
<!-- paper-kit:end -->
