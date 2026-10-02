# /qa smell lane

You read the files of your list for what an analyzer does not see: structure that makes the next change
expensive or wrong. Report in the format of [findings](findings.md), KIND `smell`, LANE `smell`; write
INPUT `-` unless the smell already produced a visible wrong result; do not fix anything.

What to look for:

- **Two implementations of one question** - the same decision written twice under two names, one of them
  drifting (`.claude/skills/find-bug/SKILL.md`, "Two implementations of one question"). Search by behaviour,
  not by name.
- **An abstraction with no problem** - an interface with one implementation and no second in sight, a
  layer that only forwards (the `architecture-reviewer` agent, rule 9, and
  `.claude/skills/clean-architecture/SKILL.md`, deep or thin).
- A function over 80 lines that mixes levels: reading input, deciding, and writing in one body.
- A duplicated block over 15 lines.
- Dead code: unreachable branches, members nothing calls, flags that are always one value.
- A magic number that should be a named unit or tolerance.
- Catching every exception and swallowing it.
- Mutable static state.
- A boolean parameter that switches between two behaviours.
- More than five parameters.

FIX of a smell always names the path to the repair: a refactor planned with `/task-spec`, done by
`.claude/skills/parity-refactor/SKILL.md` (behaviour pinned first, then moved). Severity is `minor` or
`info`; `major` only when the smell already caused a visible error, which INPUT then names.

When the static lane ran, your list ends with `Already reported by analyzers (do not repeat)`: those
rules are in the report already.
