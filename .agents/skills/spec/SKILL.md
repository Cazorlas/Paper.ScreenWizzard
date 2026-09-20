---
name: spec
description: Write a Paper feature's requirement first - SPEC.md in the user's words with measurable acceptance, before any code - then a frozen brief and a checkbox plan the user approves, and close SPEC.md (draft banner and change markers out) once verification passes. Use when starting or finishing feature behavior work; use spec-backfill for an existing undocumented feature.
---

# Paper feature spec

`docs/features/<slug>/SPEC.md` answers **what we build and the rules it follows**, in the user's words.
The code answers how. Where the two disagree, the spec is right and the code has a bug - unless the
user agrees the rule itself changes.

**The test of a spec: could somebody rebuild the feature from it?** Not pixel for pixel, but every
input, every domain rule and the whole user flow. A spec that carries only rules describes a library.

**Write it the way the user would explain the tool to a colleague.** Short sentences, the everyday
words of the user's trade, no jargon from software. If a sentence needs a programmer to understand it, rewrite it.

## Three documents per task - SPEC.md first

| File | For | Words |
| --- | --- | --- |
| `SPEC.md` | the requirement, living; **written or changed before any code** | the user's; no code |
| `YYYY-MM-DD-<task>.md` - the **brief** | what was asked this time and why, the source material, which SPEC sections change, the model and ids allowed; frozen the day work starts | the user's; no code, no tasks |
| `YYYY-MM-DD-<task>-plan.md` - the **plan** | approval status, context, decisions, UI wireframe, test-first tasks, API lookups, evidence | the code's |

All three sit in `docs/features/<slug>/` (the profile's `featureDocs`), with `input/` (what the user
handed over, verbatim, never edited) and `shapes/` (SVG + rendered PNG). The brief comes from
[brief-template.md](brief-template.md), the plan from [plan-template.md](plan-template.md). A question
that changes no code needs none of them.

Beside `<featureDocs>`, in its parent folder `<docs>` (default `docs`), two ledgers outlive every task:
`<docs>/bugs/` - one file per bug report, kept by `/task-bug` and never deleted - and
`<docs>/retro/README.md` - the lesson queue `/task-verify` fills and the project owner empties in
`/sync-docs`.

## The shape of SPEC.md

```markdown
# <feature> - SPEC

<3-5 lines: what the user gets out of it>

## User story              one paragraph: As a <who>, I want <what>, so that <why>
## What the user does      the flow, numbered, including what the user sees at the end
## Inputs                  a table: what the user picks or sets, its default, and what it DECIDES
## Outputs                 what the user gets: elements created or changed, reports, messages
## Key entities            the domain nouns (duct, branch, air terminal, transition...)
## <group of rules>        a bold claim, why it exists, then its acceptance lines
## Edge cases              boundaries already decided
## When it does not do the job   a table: # | case | what the user sees - F1, F2 ..., never renumbered
## Assumptions             what was settled without asking, and why
## Clarifications          ### Session YYYY-MM-DD - Q: ... -> A: ...  (open: **Chưa trả lời**)
## What it does not do yet deliberately deferred; code can never say this
```

**What the user does** is numbered and user-level: where it is started, what is picked, what appears,
what is clicked, what the user sees at the end.

| Write | Never write |
| --- | --- |
| "picks ducts in the model" | a control's name, a layout, a mockup |
| "fittings cannot be picked" | the name of the selection filter |
| "the settings dialog opens" | whether it is modal or modeless |
| "terminals that could not connect are listed" | how the list is built |

**Inputs**: a field nobody can change is not an input; it is a rule or an assumption. **Outputs**: a
result the user cannot see or check is not an output.

**When it does not do the job** is every way the feature stops short, and what the user sees. Each row
is an acceptance line - given that case, exactly that is seen.

| # | Case | What the user sees |
| --- | --- | --- |
| F1 | a grille with no duct above it within 3000 mm | it stays selected, and the report names its level |
| F2 | the picked duct is in a linked model | **silent**: nothing is reported and the run looks successful - open bug, see What it does not do yet |

- **Every row has a code, `F1`, `F2` …, and a code is never renumbered.** A removed row keeps its gap:
  `F4` gone means the next new row is still the next number, so a test, a plan or a bug that names `F5`
  keeps meaning the same case.
- **Each row is one test, and the test's name starts with its code** (`F3_…`). The plan's tasks name
  the codes they cover; `/task-verify` reports one verdict per code.
- **A silent skip is its own row, never omitted.** "Nothing" is never an acceptable design - a silent
  skip is the costliest bug, because the user believes the run worked - but where the feature does skip
  silently today, write the row plainly (**silent**: what the user actually sees) and link the open bug
  from `<docs>/bugs/` or the reason it is kept. A silent skip left out of the table is the one nobody
  will ever test.

## Rules, with acceptance

**Acceptance is the part with authority.** A rule without it can only be believed. Write it in real
numbers; **each line is one test case**, and the tests are written from these lines, not from the code.

```markdown
**A grille sits under a widened local duct segment.** A grille tapped into the narrow duct leaves
its neck outside the duct.

- Cho một miệng gió ngang dưới ống 900 x 400 mm → đoạn ống cục bộ rộng **1200 mm**
- Cho đoạn đã mở rộng → **một côn thu mỗi bên** về lại 900 mm, phẳng đáy
```

(`Given … →` in a spec written in English is the same line.)

- **The result is measurable.** "Routed sensibly" cannot be checked; "two 45-degree elbows, 150 mm apart" can.
- **The `Cho` is a shape, never an element of somebody's model.** No element id, no model or file
  name, no level of one project, no count of its elements. Say what makes the case, in millimetres and
  degrees. The model that exposed it belongs in the brief.
- **Too hard to say in a sentence? Draw it** - see Hình và sơ đồ.
- **A lookup is not a rule until its values are in it.** "The configured size" cannot become a test;
  carry the rows with their default figures, marked as defaults.

## Hình và sơ đồ

**A shape is drawn; a flow is written as a text diagram.** Neither is a screenshot.

- **A shape**: `shapes/<case>.svg` beside SPEC.md, in millimetres - the outline as given, and dashed,
  where the rule puts the result - so one drawing states the case and its expected result.
  Render it to the PNG beside it and link the PNG (`![the case in words](shapes/<case>.png)`); many
  markdown viewers, pull requests included, will not show an SVG.
- **Keep the SVG** - it is the source. Moving a label is a one-line diff in it; a PNG alone means
  redrawing, and a reviewer sees only a changed binary.
- **Render after every edit**: `.claude/paperflow/render-shapes.ps1 -Path docs/features/<slug>` sizes a
  headless browser to the drawing; exit 4 means no browser, so the picture is not verifiable - say so.
  **Open the PNG and look at it before committing**: a label over an outline or a cut-off caption shows
  only there. Skill `check-spec` reports an SVG newer than its PNG, or one with no PNG.
- **A flow or a sequence** is a Mermaid block in the document itself. In SPEC.md it draws only what the
  user sees happen - steps, choices, messages, never a class, method or file. Plans, code maps and
  decision records may draw the code with it.

## Never name the code

**No code, no programming-language syntax, no class, method, property, file path, command, test or
assembly name - in SPEC.md and in the brief.** That is how, it is the session's decision, and it is
what makes a spec drift: "the user types the branch offset, default 150 mm" survives every rename. A
value the user types or picks (`Tee`, `Wye`, a command name they type themselves, a layer they choose) is
allowed. A default figure is not a code identifier - write it and
say *default*. The plan is the one document that names code, and a UI wireframe lives there too.

Skill `check-spec` fails when a `SPEC.md` names code in backticks, so a slip is caught in under a
second. It cannot catch a class name written as plain prose, nor read the brief; reading can.

## Before the code - write the requirement

**A bug report is one of two things, and they are fixed in opposite orders.**

| | What happens |
| --- | --- |
| **Code bug** - the code does not do what the spec says | the spec is already right; fix the code with a test from its line |
| **Spec gap** - the code does what the spec says and the result is still wrong | propose the new rule in numbers; change SPEC.md, then the code |

A spec gap is never closed by changing the code first and writing the spec to match.

1. Read the whole `SPEC.md` (or run `spec-backfill` when the feature has none), the request, the
   applicable instructions, source, tests and live evidence.
2. **Write or edit `SPEC.md`**: the story, the flow, inputs, outputs, and a `Cho … →` line for every rule
   the task touches.
   - A **new** `SPEC.md` opens with the banner
     `> Bản nháp chờ duyệt <date>. Đây là yêu cầu, viết trước khi có code. Việc làm và bằng chứng: [<plan>](<plan>)`.
   - A **change** to an existing one edits the rule lines in place and puts
     `> Đổi bởi <brief>, chờ kiểm` directly under each changed section heading. Strike the lines the
     change contradicts.
3. **Ask only where the answer changes what gets built or how it is checked.** Each question is a
   choice of two to five, or one word. At most five in a session. Record the exchange under
   `## Clarifications`; a question still open is a line marked **Chưa trả lời**.
4. **Write the brief, then the plan** with status `chờ duyệt` and `- [ ] T<n> [lane] … — done when …`
   tasks in test-first order, grouped `### n.` and ending with a Close group. Every rule line and every
   `F<n>` row the task touches is reached by a task, and the task names the codes it covers.
5. **Stop and show the user the SPEC rules and the task list - end the turn.** Every time - the first
   presentation and the second, third and later re-approvals alike - the message **carries an openable link**
   to each document awaiting approval (SPEC.md, brief, plan: one markdown link per line, path relative to the
   project root, the changed ones marked); never "as before" and never a bare file name. Write
   `đã duyệt <date> ("<their words>")` on the plan's status line only after they say OK.
   `.claude/paperflow/paperflow.ps1 tasks -Path <plan>` exits 4 until then.
6. Tick each task the moment its proof has run and add its evidence row; the work is done when the same
   command exits 0.

**A result that shows SPEC.md is wrong stops the work**: edit SPEC.md (a spec gap, proposed in numbers,
under a `chờ kiểm` marker), add `Spec bổ sung <date>` under the plan's Decisions, set the status back
to `chờ duyệt`, end the turn. **The brief is never edited, and a ticked task is never rewritten** - add a
correcting task and a Decisions line.

## After verification - close SPEC.md

1. Remove the `Bản nháp chờ duyệt` banner and every `chờ kiểm` marker this task put in.
2. **Delete the lines a verified line replaced**, and any line the work proved wrong.
3. Update `Inputs`, `When it does not do the job` and `What it does not do yet` if they moved. A new
   failure row takes the next unused `F` number; a row that no longer exists is deleted, its number
   never reused. An open bug stays linked from its `F` row or from `What it does not do yet`; a fixed one
   is unlinked there and kept in `<docs>/bugs/`.
4. Run skill `check-spec`.
5. For the pull request, a person may ask for skill `spec-changes`: the rules this branch added, changed
   and removed, section by section. Offer it once; never run it unasked.

`SPEC.md` then describes what the feature does - verified behaviour, not intended or build-only
behaviour. A banner or marker still there once the plan is `xong` is drift: `check-spec` and `/sync-docs`
list it.
Architecture and project rules stay in the `CLAUDE.md` files, where the code is in the `CODEMAP.md`,
and traps in a comment at the code - never in the spec.
