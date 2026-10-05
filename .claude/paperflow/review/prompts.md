# Prompts of the reviews

The two prompt templates that review.ps1 prompt fills for the four reviews (ADR-0043, SPEC F213). the script review-collab-plan.ps1 reads this file at run time:
the first fenced block under each heading is the template, nothing else in this file is read as a template.

- A line that starts with `{retry}`, `{external}`, `{architecture}`, `{bug}`, `{ui}{project}` or `{static}` is kept only when
  that condition holds (the tag is removed). A part between `{external}` and `{/external}`, or `{static}` and `{/static}`
  inside a line is kept or dropped the same way. `{project}` means: not an external repository.
- Text in angle brackets is filled by `review.ps1 prompt`; the prompt asks the agent for nothing else.

## Lane prompt

```
/qa lane <lane>, batch <b> of <n>, run <run>
You review read only: read files, never edit, fix or run anything that writes. A separate reader verifies findings later.
{retry}This batch comes back once. Answer the whole list below again, fixing exactly these errors of your first answer:
{retry}<one line per error, as review.ps1 check printed it: <lane>-<b>: <id>: <error>>
{external}The repository is <repo> (read only): every path of the list is relative to it; read <repo>\<path>.
{external}Judge against its own rules - for the architecture lane exactly the files on the rules: line - never against this project's ADRs or profile.
Read every file in the list below - no more, no fewer - and nothing outside it except the documents the instructions
name (SPEC.md, ADRs, CODEMAP.md, the project's CLAUDE.md){external}, and on this repository only its own rule files{/external}.

<the lines of: review.ps1 files -Run <run> -Lane <lane> -Batch <b> [-Retry], unchanged>

Report each finding as one block in the format of the section "Finding format" below, ids <PREFIX>-1, <PREFIX>-2, ...
{architecture}This replaces the report of the instructions: never one line per violation, always a finding block.
{bug}For a file no SPEC.md covers, follow the instructions' section "No SPEC.md for the file".
{ui}{project}If the project has a UI style skill under .claude/skills, read it too.
Do not fix anything and do not verify your own findings. End with the line "seen N/N" and, if any file was not read,
"not read: <path>, <path>". Your final message is only the finding blocks and those lines.

## Finding format (<absolute path of references/findings.md>)

<the section "## A finding" of references/findings.md, from its first line after the heading to the line before
"## What check does with an answer", unchanged>

## Instructions for the <lane> lane (<absolute path of the instruction file>)

<the instruction file, its YAML front matter (from the first "---" line to the next) removed, unchanged otherwise;
the ui lane has two such sections: design-critique, then accessibility-review>
```

## Verify prompt

```
/qa verify <id>, run <run>
You are an independent reader: read files only, never edit. Decide whether the rule below is broken for the input below.
{external}The repository is <repo> (read only): look for the code there; read <repo>\<path>.
RULE      <RULE of the finding, verbatim>
INPUT     <INPUT of the finding, verbatim>
{static}WHERE     <path:line>   (a static finding only: its rule id, title and link are the RULE line)
Find the code the rule is about yourself{static}, starting at WHERE{/static}. Answer with exactly one block:

FOR          <id>
VERDICT      confirmed | rejected | needs_validation
INPUT        <the input that gives the wrong result>
EVIDENCE     <what you read and what that code does with the input>
FINGERPRINT  <the broken spot you located: the file and what is wrong there>

confirmed needs that INPUT and a FINGERPRINT; EVIDENCE is never empty. Your final message is only this block.

## How to verify (<absolute path of find-bug/SKILL.md>)

<the section "## Verifying a finding before it becomes a task" of find-bug/SKILL.md, up to the next "## " heading,
unchanged>
```
