---
name: wiki
description: Use only when the user names a knowledge wiki - types /wiki, or asks to look something up in a wiki, add a source or an answer to one, promote memory into one, create one, or lint one. Not for a project's own docs, its SPEC.md, the reference index or memory alone.
argument-hint: "[which | init <folder> | ingest <source> | query <question> | promote | lint] [--wiki <name>]"
---

# /wiki

A knowledge wiki is a git repository of its own, kept by an agent for several projects, after Andrej
Karpathy's "LLM Wiki" pattern. Three layers: `raw/` holds the sources exactly as they came, a folder per
kind under the wiki's `wiki` folder holds the pages the agent writes, and the wiki's `AGENTS.md` holds
its rules. `index.md` lists every page on one line and is read first; `log.md` records every operation,
append-only. The kit keeps the mechanism - this skill, the templates in `template/`, the lint, the
machine's registry of wikis; the knowledge itself lives in the wiki repository, never in the kit or in a
project (ADR-0036 in the Paper-skills repository).

## Which wiki

The script is `scripts/wiki.ps1` (in a project: `.claude/skills/wiki/scripts/wiki.ps1`; Codex:
`.agents/skills/wiki/scripts/wiki.ps1`), always run as
`powershell -NoProfile -ExecutionPolicy Bypass -File <script> <command> ...`.

Start with `scripts/wiki.ps1 which`: it reads the machine's registry, `%LOCALAPPDATA%\paper-kit\wikis.json`,
and prints one line per wiki serving the project open here (a worktree counts as its main checkout):

- `wiki: <name> -> <path> (serves <names>) - <about>` - a wiki to use; the one naming this project comes
  first, wikis serving every project (`*`) after it.
- `wiki: <name> -> <path> (folder missing - ...)` - the registry names a folder that is gone: tell the user,
  with the registry path, and do not work on it.
- Exit 5, `wiki: no wiki serves <project>; <registry> lists <n>: <names>` (or `no registry at ...`): say no
  wiki serves this project, name the registry and the wikis it lists. **Never guess** a folder, and never
  create a wiki the user did not ask for.

When the user names a wiki (`--wiki <name>`), use that one; `lint -Wiki <name>` also takes a registered name.

## Read the wiki's own rules first

Open `<wiki>/AGENTS.md` before any write to the wiki and follow it: its layers, kinds, page names, link
style, log entries and operations. It wins over this skill on the wiki's own conventions; this skill wins
on cost and on approval.

## Creating a wiki

`wiki.ps1 init -Wiki <folder> [-Name <kebab>] [-Serves a,b] [-About "<one line>"] [-DryRun] [-NoRegister]`

- Run `-DryRun` first whenever the folder already holds files: it lists what would be created and kept.
- It never overwrites: every file already there is kept byte for byte and named as kept.
- It refuses a folder inside another git repository. The wiki of a read-only repository sits beside that
  repository, never inside it; such a repository is a source by pointer (`repo:` and `sha:`, see the
  wiki's `AGENTS.md`).
- `-Serves` names the repository folders this wiki is for, comma separated; `*` (the default) is every
  project. `-Name` defaults to the folder name in kebab-case.
- On a new machine, `init` on a clone of an existing wiki only registers it: every file is kept.
- It does not run `git init` and commits nothing: `git init` and commits only when the user says so.

## Working on a wiki

ingest, query, lint and promote follow the wiki's `AGENTS.md`. Session rules:

- Write only the wiki's files. **Never write wiki pages into** the project open in this session - not its
  docs, not its memory, not its `SPEC.md`.
- Codex: the wiki is outside the project, so each write needs approval, and goes through a patch only.
- After an ingest, a query answer kept as a page, or a promote, run
  `wiki.ps1 lint -Wiki <path>` and fix every error before saying done.
- Commit in the wiki repository only when the user says so.

**Cost.** A step that reads many files - a content lint (contradictions, stale claims, missing pages), a
batch of sources, a promote - is said before it runs: the number of files, the number of characters, and
the **estimate** in tokens (characters / 4). Then **stop** and wait for the user to agree. `wiki.ps1 lint`
itself is free (no model) and needs no approval.

## Memory and the wiki

| Agent memory keeps | The wiki keeps |
| --- | --- |
| what holds in one project: its paths, its decisions, its traps | what holds beyond one project: a technique, a trap of a library, a decision that recurs |
| loaded into every session of that project | read on demand, through `index.md` |

Find the memory folder with `.claude/paperflow/paperflow.ps1 memory` (Claude Code's and Codex's shared
memory of the project).

**promote** moves a fact from memory to the wiki. First list, for each memory file: the file, the wiki page
it becomes (new or updated), and the part that stays in memory. Then stop and wait for the user's
**approval** of that list - never run promote on a list the user has not approved. After approval: copy
each memory file unchanged to `raw/memory/<project folder>/` in the wiki, ingest it, leave in the memory
file only its project-specific part plus one line pointing to the page, and append a `promote` entry to
`log.md`.

## Lint output

`wiki.ps1 lint` prints one line per finding, `<ERROR|WARNING> <rule> <file>[:<line>] <message>`, then
`wiki-lint: <n> page(s), <e> error(s), <w> warning(s)`.

| Rule | Level | Means |
| --- | --- | --- |
| `missing-file` | error | a root file (`AGENTS.md`, `CLAUDE.md`, `index.md`, `log.md`) is missing - `init` adds it |
| `bad-name` | error | a page name is not lowercase ascii words joined by `-` |
| `outside-folder` | error | a page sits directly in the pages folder instead of a kind folder |
| `duplicate-name` | error | two pages share a name, so a link cannot tell them apart |
| `dead-link` | error | a link of the index or a page resolves to no file |
| `ambiguous-link` | error | a link resolves to two files - put the folder in it |
| `index-missing` | error | a page has no line in `index.md` |
| `index-duplicate` | error | a page is listed twice in `index.md` |
| `index-section` | error | an index line is under the wrong `## <kind>` section, or under none |
| `index-summary` | error | an index line has no one-line summary |
| `orphan` | warning | no other page links the page |
| `log-format` | error | a `## ` line of `log.md` is not `## [YYYY-MM-DD] <op> \| <title>` |
| `log-order` | error | a log entry is dated before the one above it |
| `secret` | error | something shaped like a key, token or password - named by kind and line, never by value |
| `source-pin` | error | a source by pointer names `repo:` without a valid `sha:` |
| `source-drift` | warning | the clone has moved past the pinned commit - read its paths again |
| `source-missing` | warning | the pinned repository is not a git clone on this machine |

Exit codes: `lint` 0 clean (warnings allowed), 1 errors, 2 cannot run (not a wiki, unknown name, broken
registry); `which` 0 found, 5 no wiki serves the project, 2 broken registry; `init` 0 done, 2 refused
(nothing written).
