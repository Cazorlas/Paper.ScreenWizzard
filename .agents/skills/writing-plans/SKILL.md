---
name: writing-plans
description: Create a durable implementation plan only when the user explicitly requests one or Paper work genuinely requires coordination across people or sessions beyond one feature's task plan. Ordinary feature work uses the plan /task-spec writes beside SPEC.md and never invokes this skill.
disable-model-invocation: true
---

# Writing Plans

## Overview

Write durable coordination plans for work that cannot remain in one working session. Document the
files, independently verifiable increments, evidence boundaries, and handoff points needed by another
person or session. Do not create a plan file for ordinary work: every task already has one, written by `/task-spec` as
`<featureDocs>/<slug>/YYYY-MM-DD-<task>-plan.md` (template `spec/plan-template.md`) beside its brief
and the feature's `SPEC.md`, with the approval status the task gate reads. This skill is only for work
that spans several features or several people.

Assume they are a skilled developer, but know almost nothing about our toolset or problem domain. Assume they don't know good test design very well.

**Context:** If working in an isolated worktree, it should have been created with the `task-worktree` skill at execution time.

**Save plans to:** `<featureDocs>/<area>/YYYY-MM-DD-<slug>-plan.md`, beside a brief of the same date.
- `featureDocs` comes from `.claude/paper.profile.json` (default `docs/features`), and `<area>` is the
  folder naming what the plan spans, not one feature - `architecture`, `platform`, and so on.
- Do not write plans under `docs/superpowers/plans/`: the `tasks` gate and `/task-verify` read only `featureDocs`. Leave files already there; they are records, not open work.
- The requirement is never written here: it is each feature's `SPEC.md`, written before any code
  (skill `spec`). A plan links the `SPEC.md` files it serves and never repeats a rule from them.
- Unfinished work that another session continues gets a `docs/progress/` handover beside it; the plan's
  checkboxes are its status.
- (User preferences for plan location override this default)

## Scope Check

If the spec covers multiple independent subsystems, it should have been broken into sub-project specs in its SPEC.md. If it wasn't, suggest breaking this into separate plans — one per subsystem. Each plan should produce working, testable software on its own.

## File Structure

Before defining tasks, map out which files will be created or modified and what each one is responsible for. This is where decomposition decisions get locked in.

- Design units with clear boundaries and well-defined interfaces. Each file should have one clear responsibility.
- You reason best about code you can hold in context at once, and your edits are more reliable when files are focused. Prefer smaller, focused files over large ones that do too much.
- Files that change together should live together. Split by responsibility, not by technical layer.
- In existing codebases, follow established patterns. If the codebase uses large files, don't unilaterally restructure - but if a file you're modifying has grown unwieldy, including a split in the plan is reasonable.

This structure informs the task decomposition. Each task should produce self-contained changes that make sense independently.

## Task Right-Sizing

A task is the smallest unit that carries its own test cycle and is worth a
fresh reviewer's gate. When drawing task boundaries: fold setup,
configuration, scaffolding, and documentation steps into the task whose
deliverable needs them; split only where a reviewer could meaningfully
reject one task while approving its neighbor. Each task ends with an
independently testable deliverable.

## Plan Document Header

Use the header and sections of `spec/plan-template.md`, including `**Trạng thái:**` and
`**Loại việc:**`, links to the brief and SPEC.md, Context, Rules that apply, Decisions and evidence.
Link the spec's constraints rather than copying them into the plan.

## Task Structure

Tasks follow `spec/plan-template.md` (`### n.` groups, `- [ ] T<n> [lane] … {files:}`); keep an Interfaces line
per task where later tasks consume its names. Name consumed and produced signatures exactly.

Use `spec/plan-template.md`: the planner's test-contract table, a `[red]` task for the red-turn worker, then a
plain lane task for the green-turn worker; the red task's test files are locked (planner-worker.md).

## No Placeholders

Every step must contain the actual content an engineer needs. These are **plan failures** — never write them:
- "TBD", "TODO", "implement later", "fill in details"
- "Add appropriate error handling" / "add validation" / "handle edge cases"
- "Write tests for the above" (without decisive inputs and expected outputs in the test contract)
- "Similar to Task N" (state the task's own inputs, interfaces and checks)
- Steps without allowed files, a concrete success criterion and the command that proves it
- References to types, functions, or methods not defined in any task

## Remember
- Exact file paths always
- The planner writes the test contract; the red-turn worker writes the tests and the green-turn worker implements
- Exact commands with expected output
- DRY, YAGNI, TDD; commit only when the user asks

## Self-Review

After writing the complete plan, look at the spec with fresh eyes and check the plan against it. This is a checklist you run yourself — not a subagent dispatch.

**1. Spec coverage:** Skim each `Cho … →` line of every `SPEC.md` the plan serves. Can you point to a task that implements it? List any gaps.

**2. Placeholder scan:** Search your plan for red flags — any of the patterns from the "No Placeholders" section above. Fix them.

**3. Type consistency:** Do the types, method signatures, and property names you used in later tasks match what you defined in earlier tasks? A function called `clearLayers()` in Task 3 but `clearFullLayers()` in Task 7 is a bug.

If you find issues, fix them inline. No need to re-review — just fix and move on. If you find a spec requirement with no task, add the task.

## Execution Handoff

After saving the plan, give its link and stop. Execution follows the project's planner/worker flow (`task-do/references/planner-worker.md`).
