---
name: impeccable
description: Manual only - run when the user types /impeccable or names it; never on its own while a window, page or component is being built, reviewed or fixed, where the task's plan and the host's style skill own the work. Design craft for web interfaces (HTML, CSS, JS, TSX, Razor, Vue, Svelte, WebView pages) with sub-commands shape, init, document, extract, critique, audit, polish, bolder, quieter, distill, harden, onboard, animate, colorize, typeset, layout, delight, overdrive, clarify, adapt, optimize. Not for XAML, WPF, WinForms or DCL.
disable-model-invocation: true
argument-hint: "[shape · audit|critique · animate|bolder|colorize|delight|layout|overdrive|quieter|typeset · adapt|clarify|distill · harden|onboard|optimize|polish · init|document|extract] [target]"
---

> Adapted from pbakaus/impeccable, `.claude/skills/impeccable`, commit ece38d9904b8a619b3f77cab476eacad09c4fb11 (v4.5.0), Apache License 2.0 - [LICENSE](LICENSE), [NOTICE.md](NOTICE.md). Changed for paper-kit: the front matter (rewritten description, manual-only invocation, no version, license or user-invocable keys, no live or generate in the argument hint), the section "In a Paper project" added, Setup step 1 (the launcher run) replaced by a pointer to that section, the Pin, Hooks and Doctor paragraphs and the live and generate rows removed, and the `scripts/` folder (launcher, binary download, live-browser assets, font index) and the reference files hooks.md, doctor.md, live.md, live-setup.md and generate.md left out; links to those files in reference/ are plain text marked "(not shipped in Paper)", and five dead file names in reference/ (responsive-design.md, cognitive-load.md, heuristics-scoring.md, design-tokens.json, raw-report.json) are unquoted. Everything else is upstream text.

Approach each design task as a design director: production-grade code, a clear point of view, and the needs of the client and users first. The named defaults to avoid are in reference/craft-floor.md.

Core principles:
- The deliverable is complete (except assets the user must provide).
- Aim for a distinct visual world that fits the brief, not the category's usual look.
- Verify in bounded passes, not a loop, and the ceiling covers the whole cycle: screenshots, defect scans, micro-edits, and rebuilds alike. Build fully, inspect once with a batched round (desktop and mobile together on the web; the shipped device classes on a native platform), fix everything it shows in one batch, confirm with at most one more round, and stop polishing. Open-ended self-QA burns the user's money doing worse what the finish handoffs do better.

## In a Paper project

This copy runs inside Paper projects, where a task's plan, a planner and workers own the work. These rules replace the upstream steps that conflict with them.

- **No launcher.** Paper projects never run the impeccable launcher: the binary is not shipped, and downloading one is the owner's choice, not a step of a session. In every session follow Launcher unavailable below, without its announcement message (nothing failed): read PRODUCT.md and DESIGN.md directly when they exist, do not invent missing context, and skip every step that needs a detector, a browser run or a live session.
- **No project state of its own.** Never write the .impeccable folder (critique storage, build, review and mock folders, design.json). Report findings in the answer instead. The skill writes PRODUCT.md or DESIGN.md only when the task or the owner asks; a plan that names them as files counts as the task asking.
- **Who asks and who decides.** Shape and init run in the main session with the owner, as part of the task's spec, before a wireframe or plan is frozen. A worker asks the planner, never the user: where the upstream text says to ask the user, a worker sends the question to the planner and waits. Do not run the interview or the unconditional comp approval in a worker.
- **Finish.** The planner's review replaces the finish review: do not start finish-reviewer or documenter (the files under reference/degraded stay as text to read). Write the documenter's output (DESIGN.md) only when the task asks for it.
- **Where it applies.** Web surfaces: HTML, CSS, JS, TSX, Razor, Vue, Svelte, WebView pages, Remotion scenes. Not for XAML, WPF, WinForms or DCL: use paper-wpf-style (the project's WPF style skill), design-critique, accessibility-review, ux-copy and design-system there. The ui-ux-pro-max skill is a manual lookup of style, palette and font data; run it only when the user asks, and let this skill's reference win where they disagree on a web screen.

## Setup

1. Load context without the launcher: follow [In a Paper project](#in-a-paper-project) and Launcher unavailable below. Do not run any `scripts/` command; none ships.
2. Load the request's playbook: its Commands-table reference for an explicit/implied sub-command, or [reference/new-work.md](reference/new-work.md) for a new surface or replacement visual world. Inspect target and incumbent visual truth before editing. When the app cannot run, start with committed visual-regression goldens or screenshot fixtures; verify target and freshness against current tokens, CSS, components, or assets, resolve conflicts, and compare theme/variant captures.
3. After resolving analysis and direction, read [reference/craft-floor.md](reference/craft-floor.md) immediately before any UI edit, including small refinements. It carries the quality floor, the absolute bans, and the reflexes no detector catches. Do not load it for planning-only work.

**Launcher unavailable:** Paper does not ship it. Read existing PRODUCT.md and DESIGN.md without inventing
missing context, follow applicable steps 2–3, and continue through permitted tools. Report missing context
when it limits the result; there is no launcher notice to send in every session.

## How to design

- **The brief wins.** Honor pinned aesthetics, eras, materials, fonts, and palettes even when they conflict with a saturated-pattern warning. Redirecting a clear brief toward your taste is failure.
- **Refinement preserves; redesign replaces.** Refinement keeps the incumbent identity, behavior, copy, and everything outside scope. Ask before replacing factual copy or adding claims. Redesign keeps product truth, content, function, native affordances, and constraints, but treats the old look as evidence and anti-reference; choose a replacement world in new-work and replace DESIGN.md. Never split the difference into polish on the discarded look.
- **Loaded symbols stay out of the decoration.** A subject's world does not license emblems tied to militarism, supremacy, or hate movements as motifs, badges, or ornament, such as the Rising Sun flag's rays, the Confederate battle flag, or Nazi-era insignia and their stylised variants; reach for that world's neutral forms instead. Content that documents such a symbol as fact stays as it is.
- **Visual authority is evidence, not a filename.** Missing DESIGN.md alone does not make a project greenfield; new-work decides whether to preserve, expand, or replace the incumbent world.

## Modes

The mode names what the visitor's success looks like on this surface.

- **Persuade:** the visitor decides and acts; design is the product. Landing pages, marketing, campaigns, pricing. Earn attention and action. Ship real imagery when the brief needs it; follow the committed world, not category habit.
- **Operate:** the visitor completes a task. App UI, dashboards, editors, admin, settings, tools. Scanability, consistency, native expectations, and the real usage scene outrank expression. Brand lives in precise details.
- **Read:** the visitor understands something. Docs, articles, guides, help, changelogs. Structure for comprehension, then make the reading experience worth staying in.
- **Experience:** the visitor is inside the work itself. Portfolios, galleries, showcases. Let the artifact lead from the first viewport; the interface recedes.

Choose the mode from the requested surface, not the product, and persist it only in that surface brief. A tool's landing page is still Persuade; a fashion house's documentation is still Read; a docs index is Read, not Persuade. See [new-work.md](reference/new-work.md) for new surfaces and [operate.md](reference/operate.md) for deeper Operate/Read guidance.

## Commands

| Command | Category | Description | Reference |
|---|---|---|---|
| `craft [feature]` | Build | Deprecated alias for an ordinary new-work request | [reference/craft.md](reference/craft.md) |
| `shape [feature]` | Build | Plan UX/UI before writing code | [reference/shape.md](reference/shape.md) |
| `init` | Build | Capture durable product context in PRODUCT.md | [reference/init.md](reference/init.md) |
| `document` | Build | Generate DESIGN.md from existing project code | [reference/document.md](reference/document.md) |
| `extract [target]` | Build | Pull reusable tokens and components into design system | [reference/extract.md](reference/extract.md) |
| `critique [target]` | Evaluate | UX design review with heuristic scoring | [reference/critique.md](reference/critique.md) |
| `audit [target]` | Evaluate | Technical quality checks (a11y, perf, responsive) | [reference/audit.md](reference/audit.md) · native: [reference/audit.native.md](reference/audit.native.md) |
| `polish [target]` | Refine | Final quality pass before shipping | [reference/polish.md](reference/polish.md) |
| `bolder [target]` | Refine | Amplify safe or bland designs | [reference/bolder.md](reference/bolder.md) |
| `quieter [target]` | Refine | Tone down aggressive or overstimulating designs | [reference/quieter.md](reference/quieter.md) |
| `distill [target]` | Refine | Strip to essence, remove complexity | [reference/distill.md](reference/distill.md) |
| `harden [target]` | Refine | Production-ready: errors, i18n, edge cases | [reference/harden.md](reference/harden.md) |
| `onboard [target]` | Refine | Design first-run flows, empty states, activation | [reference/onboard.md](reference/onboard.md) |
| `animate [target]` | Enhance | Add purposeful animations and motion | [reference/animate.md](reference/animate.md) |
| `colorize [target]` | Enhance | Add strategic color to monochromatic UIs | [reference/colorize.md](reference/colorize.md) |
| `typeset [target]` | Enhance | Improve typography hierarchy and fonts | [reference/typeset.md](reference/typeset.md) |
| `layout [target]` | Enhance | Fix spacing, rhythm, and visual hierarchy | [reference/layout.md](reference/layout.md) |
| `delight [target]` | Enhance | Add personality and memorable touches | [reference/delight.md](reference/delight.md) |
| `overdrive [target]` | Enhance | Push past conventional limits | [reference/overdrive.md](reference/overdrive.md) |
| `clarify [target]` | Fix | Improve UX copy, labels, and error messages | [reference/clarify.md](reference/clarify.md) |
| `adapt [target]` | Fix | Adapt for different devices and screen sizes | [reference/adapt.md](reference/adapt.md) · native: [reference/adapt.native.md](reference/adapt.native.md) |
| `optimize [target]` | Fix | Diagnose and fix UI performance | [reference/optimize.md](reference/optimize.md) |

Routing:

- **No argument:** read [routing.md](reference/routing.md) and present its context-aware menu; never auto-run a command.
- **Explicit or clearly implied request to run a command:** load its reference (native variant on native platforms) and follow it. Ask once if two commands fit.
- **Workflow or command-selection question:** read [Workflow questions](reference/routing.md#workflow-questions).
- **Otherwise:** treat the request as general design work. Missing PRODUCT.md routes a new surface or replacement world through init, then new-work; a narrow refinement of existing code proceeds on the incumbent implementation as the context loading in Setup directs, offering init afterward rather than blocking on it.
- `teach` aliases `init`. `craft` is a deprecated alias for ordinary new-work and adds nothing. `shape` owns task discovery, then enters new-work only for visual-world and surface-concept decisions.

After init writes PRODUCT.md, resume without repeating Setup's context loading; init loads the native platform reference itself when the platform it recorded is `ios`, `android`, or `adaptive`.

**Never repair drift as a side effect of a design task.** A `CONTEXT_STALE` finding is reported, not acted on, unless the user asks. The one exception is a finding marked `auto`, which the next write to that file performs anyway.
