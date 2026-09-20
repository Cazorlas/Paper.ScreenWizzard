---
name: diagram
description: Put a diagram in a Paper document - an ADR, a SPEC.md, a plan, a CODEMAP.md - as Mermaid inside the markdown, generated from the repository where the shape already exists in data. Use when a document explains a structure that is hard to hold in prose (module layers, a use case's ports and who implements them, the path a call takes, a state machine), when a reviewer asks what depends on what, or when a diagram already in a document may no longer match the code.
---

# A diagram that cannot quietly start lying

A picture drawn by hand is true on the day it is drawn. The code moves; the picture does not, and nothing
says so. **So a diagram here is Mermaid inside the markdown, and wherever the shape already exists in data,
it is generated and checked rather than drawn.**

Mermaid rather than an image file, for three reasons that are measurable rather than stylistic: it diffs,
so a reviewer sees which edge changed; every gate the kit has keeps reading the document as text
(`check-spec`, `check-code-map`, the `tasks` gate); and it cannot drift away from the document the way a
PNG in an assets folder does.

## The rule that comes before any of this

**Information must never live only in a picture.** If a value, a flag, a rule code or a step appears in the
diagram and nowhere else, the document is broken - for anyone who cannot see the image, and for anyone who
needs to copy that value. Put the same fact in the prose beside it. This is blocking, not advisory.

A diagram is for a **shape**: what points at what, what happens in which order, which state follows which.
A table is better for values, and a list is better for steps with detail. Reach for a diagram when the
prose has started saying "A references B which references C, while D also reaches B" - not before.

## Generated: module layers

The one shape the repository already holds as data is the `ProjectReference` graph. Do not draw it:

```powershell
.claude\paperflow\diagram.ps1 -Path "<document>.md" -Module <Module> -SourceRoot "<repo>\src"
.claude\paperflow\diagram.ps1 -Path "<document>.md" -Module <Module> -SourceRoot "<repo>\src" -Check
```

It writes between markers and touches nothing else:

```
<!-- paper-diagram: layers Paper.Structural -->
...generated mermaid...
<!-- /paper-diagram -->
```

- Only edges **inside** the group are drawn. A module that references a shared library does not drag the
  whole solution into the picture; that is what keeps it readable and what keeps the block stable.
- The output is sorted, so the same repository always produces the same block. A diagram that reorders
  itself makes every run look like a change, and after that nobody reads the diff.
- `-Check` writes nothing and exits 1 when the picture and the projects disagree. **The picture is the one
  that is wrong** - it is generated, the code is not. Regenerate; never edit inside the markers by hand.
- A group with no edges still draws its nodes. "Nothing references anything" is a finding worth seeing.

Put a layers diagram in the ADR that decided the layout and in the module's root `CODEMAP.md`. Do not put
the same one in a plan: a plan is about one change, and a plan's diagram goes stale the day it is merged.

## Drawn by hand: everything else

Sequence, state and use-case pictures have no generator yet, so they carry their own honesty rule:
**every node names a real file, type or command, and the document says where it came from.** A hand-drawn
diagram whose boxes are abstractions nobody can find in the repository is prose with rectangles.

| Shape | Mermaid | Use it for |
|---|---|---|
| what depends on what | `graph TD` | layers, ports and their implementations |
| what happens in order, across parts | `sequenceDiagram` | a call from a ribbon button to a host and back |
| what state follows what | `stateDiagram-v2` | a gate's verdicts, a plan's statuses |

Keep it under about fifteen nodes. Past that nobody reads it, and the thing to do is split the document,
not shrink the font.

## Before you commit one

1. **Does a fact live only here?** Move it to the prose. Blocking.
2. **Is this shape already data?** Then generate it; a hand-drawn layer graph is a future lie.
3. **Does every node name something real?** A box called "Service Layer" that matches no folder is noise.
4. **Does it render?** Open the file in the editor's preview and look at it. A Mermaid syntax error shows
   as a broken block on GitHub, and the author is the last person who will notice.
