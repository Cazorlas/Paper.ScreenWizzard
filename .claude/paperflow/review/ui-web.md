# Web UI in a read-only review lane

These rules take precedence over the impeccable references pasted after them. Review read only: inspect the batch's
files and supplied screenshots, tracing other repository files when needed; never edit or fix them. Apply impeccable only to web files; desktop files use
design-critique and accessibility-review when those instructions are also present.

- Never run the impeccable launcher or any detector command. Use the references' Launcher unavailable path; read
  PRODUCT.md and DESIGN.md if present as context, without creating or changing them. Skip the detector step and state
  that detector evidence is unavailable; source inspection and supplied screenshots do not prove live behavior.
- No browser: do not launch a browser, navigate, capture screenshots or run live checks. Use only supplied evidence.
- No sub-agents: perform critique's Assessment A, then Assessment B yourself in the same turn. Do not delegate either
  assessment; no DEGRADED banner is needed.
- Follow critique.md for usability and hierarchy, audit.md for accessibility, and craft-floor.md for web quality.
  Report only findings supported by repository evidence or supplied screenshots; label anything requiring runtime validation accordingly.
- Read-only commands that write nothing may prove a finding; ui-ux-pro-max's search script may support FIX.
  For each supplied screen, include an info finding with RULE `overall`, first impression and repair priorities.
  Without captured screens, the main session records `visual pass: not run - <reason>`; source inspection alone
  leaves visual behavior unverified.
- Never write the .impeccable folder. Skip critique-storage, saving critiques, init, hooks, pinning and all other
  writing steps. Do not create or change PRODUCT.md, DESIGN.md, reports or configuration.
- Skip the closing questions and owner interview. Include the line `Questions skipped: read-only review lane` in the
  answer, before the coverage lines; do not ask the user or initiate follow-up work.
- Use the finding format of this review command, including its UI id prefix, RULE, INPUT, WHERE, WHY and FIX fields.
  This replaces impeccable's report and scorecard format. Nielsen scores belong only as context in WHY, never as a
  separate scorecard. End with the required `seen N/N` and any `not read:` coverage lines.
