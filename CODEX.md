# Codex entry point

Codex reads `AGENTS.md` on its own. The paper-kit block there gives the reading order. This file repeats
no rule: `CLAUDE.md` holds them.

- Skills: `.agents/skills/<name>/SKILL.md` has the same text as `.claude/skills/<name>/SKILL.md`. Codex
  has no slash commands. When a skill says `/task-do`, open `.agents/skills/task-do/SKILL.md` and follow it.
- Gate: `.claude/paperflow/paperflow.ps1 tasks -Path <plan>`. Do not implement a plan the user has not
  approved (exit 4).
- Hooks: `.codex/hooks.json`. The `AGENTS.md` block lists the hooks Codex cannot run.
- The kit owns both skill copies. Do not edit either one here, because setup refuses to run over a copy
  that was edited by hand.
