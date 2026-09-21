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

Codex runs the kit's hooks from `.codex/hooks.json`. These have no Codex equivalent, so check by hand what they would have checked:

- live-first-guard.ps1 (PreToolUse Edit|Write): Codex has no Edit or Write tool to run it on
- layer-guard.ps1 (PostToolUse Edit|Write): Codex has no Edit or Write tool to run it on
- no-static-host-state.ps1 (PostToolUse Edit|Write): Codex has no Edit or Write tool to run it on
<!-- paper-kit:end -->
