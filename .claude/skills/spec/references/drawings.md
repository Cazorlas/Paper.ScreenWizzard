# Hình và sơ đồ

The detail behind "Hình và sơ đồ" in `SKILL.md`, kept here so the skill stays under its length limit.

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

- **In a split requirement** (`split-spec.md`) a part links the drawing one folder up:
  `![the case in words](../shapes/<case>.png)`. The drawings stay in the feature's one `shapes/` folder.
