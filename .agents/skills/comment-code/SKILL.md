---
name: comment-code
description: Write the comment that keeps the next editor out of a trap - at the code, in the code's words. Use when a task uncovered an edge case, an ordering constraint, a measured number or an API that lies, and the knowledge belongs to one place in the source rather than to the product's requirements or a project-wide rule.
---

# Commenting code

**A comment is the only layer that is in the diff.** Change the method and the comment above it is in
front of you; a paragraph in another file is not. That is why a trap lives here rather than in any
document.

## Which layer, before you write anything

| It says | It goes |
| --- | --- |
| what the product must do, in the user's words | `docs/features/<slug>/SPEC.md` - never names a class or method |
| what this code is, which file holds what, the path a call takes | the folder's `CODEMAP.md` - `code-map` |
| why one task chose this design over another, and what it measured | that task's plan, under `Decisions` - frozen once the plan is `xong` |
| a silent rule true across a whole project, with no single line to sit on | that project's `CLAUDE.md` |
| what bites **here**, in the code's words | **a comment, at the site** |

## What earns a comment

It does one of three things, and you can say which before you write it:

- **prevents a future mistake** - a trap, an ordering constraint, an API that lies;
- **preserves a non-obvious decision** - why it is this way when the obvious alternative looks better;
- **cuts future discovery time** - a fact that costs real digging to re-derive.

**Unsure? Write nothing.** A comment restating the line is one more thing to keep true.

## The shape: the claim, then the failure it prevents

The failure is what stops the comment being "improved" away.

```csharp
// Unsubscribe before disposing the cache, not after: the handler reads the cache, and an event raised
// between the two calls lands on a disposed object - an ObjectDisposedException that shows up only
// under load, never when stepping through in a debugger.
```

**A measured number beats an adjective** - nothing in the code holds it and nobody can re-derive it.
**Not an element of somebody's model**: an id or a model name goes stale when the element is redrawn;
describe what makes the case (*a branch square to the main, 300 mm below it*) and put ids in the commit.

## Describe the path, never the intent

Before writing a comment about behaviour, **read the path that produces it** - follow the early returns.
A comment describing what somebody meant to build, above a branch that cannot be reached, is worse than
none. If you cannot trace the line executing, say so, or say nothing. Mark anything not verified in the running
host as unverified.

## Where it goes

- **A member** - `///` above it; constants and tolerances especially, with their unit.
- **A line or block** - `//` directly above, indented with it.
- **A decision spanning a method** - `<remarks>` on the method: what it decides and what it deliberately
  does not touch.
- **Knowledge spanning files** - written at every site, each naming the others.

The project's own ceiling in its `CLAUDE.md` holds; without one: **at most 10 lines of prose in one block**. Longer means
it is either several traps (split them to their sites) or a project-wide rule (its `CLAUDE.md`).

## Revising one

- Correct it in the same edit that falsified it; delete a comment whose code is gone.
- Do not date it, sign it, or narrate the change - git has those.
