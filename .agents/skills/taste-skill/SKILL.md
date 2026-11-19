---
name: taste-skill
description: Manual only - run when the user types /taste-skill or names it; never on its own while UI is being built, reviewed or fixed, where the task's plan and the host's style skill own the work. Art direction for web pages and app screens, with a dated tell registry and render critique.
disable-model-invocation: true
---

> Adapted from leonxlnx/taste-skill, `skills/taste-skill`, commit 12af471f627f866411b4bd785b19646d3672784c (v2.1.5), MIT License - [LICENSE](LICENSE). Changed for paper-kit: the front matter (name and manual-only description rewritten, disable-model-invocation added); links and commands to omitted scripts/, library/, assets/ and references other than tells.md and critique.md made plain text marked "(not shipped in Paper)"; the upstream body from "The run" onward moved verbatim into references/run.md, and an "In a Paper project" section with links added. SKILL.md, LICENSE, references/tells.md, references/critique.md and the Paper split references/run.md ship. Everything else is upstream text.

# tasteskill v2

A page built by a model usually looks like the average of every page the model has seen: a centred
headline over three lines, a little label above it, a row of icon cards, a soft gradient, a few thin
rules and a monospace caption. Nothing in it is wrong and nothing in it was decided.

This skill replaces those defaults with decisions, in four ways:

1. **A script deals the directions, you choose among them.** Asked to invent a look, a model picks
   the category default every time. scripts/draw.mjs (not shipped in Paper) deals three of twenty researched directions;
   you choose the one that carries the subject's idea, and the script then deals its type system,
   palette, hero and section compositions from a library distilled from 247 real sites.
2. **The idea comes from the subject.** In the study, what separated the best sites from the forgettable
   ones was a concept taken from the thing itself, not the styling.
3. **A house style with no ornaments,** measured in a real browser by scripts/inspect.mjs (not shipped in Paper), so the
   rules are checked rather than hoped for.
4. **You look at what you made.** Screenshots at three widths, a juror's rubric, fix, look again.

One warning learned in testing. Taking the ornaments away is only half the job; a page that has lost its
labels, rules and cards and gained nothing in their place reads as thin, and blind jurors rightly prefer
a richer page with tells over an empty page without them. The other half is substance: type set
larger, whole fields of colour, real pictures of this subject's world (found or drawn), and every
section carrying its full content. The skill is built to give you that; do not trade it for restraint.

In the commands below `<skill>` is this skill's folder. Run them from the project's root, so the
working files land in `.taste-skill/` there. The scripts need Node 22+ and an installed Chrome, Edge
or Chromium, and install nothing. assets.mjs (not shipped in Paper) also needs network access; it uses no API keys.

## Page or app screen

Decide first which of two kinds of work this is.

- **A page** is read and scrolled: a landing page, a marketing or product site, a portfolio, an
  event, a restaurant, a one-pager. It follows **the run** below.
- **An app screen** is worked in: a dashboard, an admin or analytics view, settings, billing, an inbox
  or queue to work through, a wallet, a list with filters, an editor or builder. It follows
  **references/app-screens.md (not shipped in Paper)**: its own run, its own draw (draw.mjs --app (not shipped in Paper)), its own house rules
  (quiet borders, small labels and repeated panels are normal there; gradients, glows, icons in tinted
  squares, rainbow charts and invented vanity numbers are not) and inspect.mjs --app (not shipped in Paper). Read that file
  whole before you start; the page run below does not apply.

A product site that shows its app is a page; the interface fragments on it follow the app-screen
rules for their insides. Deal the product's app style once, in its own folder, with the page's colours
and type kept:

node <skill>/scripts/draw.mjs --app --kind <kind> --brief "<the product>" --keep palette,type --out .taste-skill/app (not shipped in Paper)

and build every fragment the page shows (the hero's live screen, a feature's crop, a chart) as a real
piece of that app: assets/app.css (not shipped in Paper) for its panels, tables and controls, assets/charts.js (not shipped in Paper) for its
charts, real data that adds up, one finding stated, cropped and scaled the way the page's blueprint
treats a picture. A fragment that looks like a working product is the strongest picture a software page
has; grey bars and lorem numbers are the weakest. When a request mixes both (a landing page and the
dashboard behind it), build each with its own run and one shared palette and typeface.

## In a Paper project

`/review-ui` reads [references/tells.md](references/tells.md) for web batches and
[references/critique.md](references/critique.md) only when screenshots are supplied.
The build run needs scripts not shipped in Paper; its upstream instructions continue in
[references/run.md](references/run.md). The review lane never reads that build run.
