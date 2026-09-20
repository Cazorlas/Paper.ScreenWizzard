---
name: code-map
description: Write or revise a CODEMAP.md - the code map and code flow of one project or feature folder, so a session finds the code without reading the folder. Use when a folder has no CODEMAP.md, when files were added, moved or renamed, or when the flow through them changed.
---

# code-map

**A `CODEMAP.md` is the code map and code flow of its folder, and nothing else.** Its job is to save
tokens: a session reads it and knows which file to open and the path a call takes, without reading the
folder. It is written in the code's words and names files, types and methods - that is what lets
`check-code-map` catch a rename. A code graph, when the project has one, finds names; the map is the
curated route a graph cannot rank.

It goes stale only when a file or method is renamed, moved or deleted. Keep it that way:

| Not in a map | Goes to |
| --- | --- |
| a trap, an ordering constraint, a measured number, an API that lies | a comment at the code - `comment-code` |
| a silent rule true across the whole project | that project's `CLAUDE.md` |
| what the feature must do, in the user's words | `docs/features/<slug>/SPEC.md` - `spec` |
| a build or deploy fact whose error misdescribes it | `BUILD.md` |
| what one task was asked and why | the brief `docs/features/<slug>/YYYY-MM-DD-<task>.md` |
| a task's decisions, status, validation runs and evidence | its plan `docs/features/<slug>/YYYY-MM-DD-<task>-plan.md`; unfinished cross-session work also gets a `docs/progress/` handover |
| element ids, model names, values read off one model | nowhere |

## The shape

```markdown
# Orders.Export

Writes the selected orders to the CSV file the accounting import reads. Reached from the Export button
on the Orders page (`OrdersToolbar`) or the `export-orders` command of the CLI.

## Map

| File | Open it for |
| --- | --- |
| `ExportModule.cs` | the only type the application registers |
| `Services/OrderExportService.cs` | which orders go out and in which column order |
| `Services/CsvWriter.cs` | quoting, separator and the encoding the import expects |
| `Adapters/OrderReaderAdapter.cs` | reading the orders and their lines in one query |

## Flow

`ExportModule.Register` -> `ExportCommand.Execute` -> `OrderExportService.Export` ->
`OrderReaderAdapter.ReadSelected` (one query) -> `CsvWriter.Write` -> `ExportCommand` shows the saved path
```

- **At least two opening lines before the first heading** - what this is in the code's words, and where
  the user reaches it. `check-code-map` enforces it.
- **A row says what would send a reader to the file**, never the class name again.
- **Group rows by folder** when the folders mean something.
- **The flow is the entry point and the order**, each step by type and method, one line per path. A one-
  or two-file folder needs no table.
- **Verify the entry point in the code** - the command registration, `App.cs` or `Program.cs`, the DI
  registration, a `GetRequiredService<...>` caller - rather than trusting a name.
- **A pointer out of the folder is map too**: *the CSV quoting duplicates Reports'
  `CsvFormatter` - a module cannot reference another*. Grep cannot find that.
- **Name only symbols that exist.** Grep each one.
- **Keep it short.** Tens of lines; `check-code-map` lists a map past 80 as backlog.

## Revising one

Update it in the same change that adds, moves or renames a file or reroutes the flow - the rows that point
at the old name, and the flow step that went through it. If the project has a Stop hook that
reminds when a project's source changed and its map did not, and the map is still true, say so in one line.

## Finish

Run `check-code-map`. A new project map also gets a row in the solution's root `CODEMAP.md`, if the
repository has one.
