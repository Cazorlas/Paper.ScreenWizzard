# Two languages

The detail behind "Two languages" in `SKILL.md`, kept here so the skill stays under its length limit.

**Every requirement is written in English and in Vietnamese, in one `SPEC.md`.** The owner approves in
Vietnamese; the people who read the code read English. One language only means one of them approves a
text they half-read (asked by the project owner, 2026-09-22: one file, two parts with the same content).

```markdown
# <feature> - SPEC

<the 3-5 lines of what the user gets, and the draft banner while there is one>

English · Tiếng Việt - the same requirement twice; change both together.

# English

## User story
...
## <group of rules>
- Given … → …

# Tiếng Việt

## User story
...
## <nhóm luật>
- Cho … → …
```

- **Two level-1 parts, `# English` then `# Tiếng Việt`**, each carrying the whole shape of a spec below
  it (`## User story` to `## What it does not do yet`). Nothing but the title, the summary and the banner
  sits above them.
- **Same shape, line for line**: the same number of `##` sections in the same order, one acceptance line
  for each (`Given … →` / `Cho … →`), the same `F<n>` rows, a `chờ kiểm` marker under the same sections.
  Numbers, units and values the user types stay as they are in both.
- **Both parts change in the same edit**, and are closed together. A spec nobody touches keeps its one
  language until its next change; that change writes the missing part.
- Skill `check-spec` reports `F26` for a `SPEC.md` carrying a banner or marker without both parts, and for
  two parts whose sections, acceptance lines or `F` codes differ. `spec-changes` keys a section by its part
  (`English · History`, `Tiếng Việt · History`), so the pull-request record shows each language's change.
- Never a second file: tools, tests and links all point at `SPEC.md`.
