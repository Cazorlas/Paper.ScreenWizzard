# Native UI in a read-only review lane

These rules take precedence over the impeccable references pasted after them. Review read only: inspect the batch's
files and supplied screenshots, tracing other repository files when needed; never edit or fix them. Apply these
references only to native UI files; web and desktop files follow their own instruction groups when present.

- Use the `## Detector findings` section the prompt supplies; never run the detector or the launcher yourself.
  Read PRODUCT.md and DESIGN.md if present as context,
  without creating or changing them. Source inspection and supplied screenshots do not prove live behavior.
- No device, simulator, emulator or app runs. Do not launch a browser, navigate, capture screenshots or run live checks.
  Use only supplied evidence; read-only commands that write nothing may prove a source finding.
- No sub-agents: perform the assessment yourself in the same turn, without delegation or a DEGRADED banner.
- Follow audit.native.md for native usability, accessibility and craft. SwiftUI also uses ios.md; Compose and Android
  layout XML also use android.md; Flutter uses both ios.md and android.md; QML uses audit.native.md only.
  Report only findings supported by repository evidence or supplied screenshots; label anything requiring runtime
  validation accordingly. Library proposals follow ui-libraries.md and the project's existing component set.
- For each supplied screen, include an info finding with RULE `overall`, first impression and repair priorities.
  Without captured screens, the main session records `visual pass: not run - <reason>`; source inspection alone
  leaves visual behavior unverified.
- A screen that runs an outside tool (an AI CLI such as Claude Code or Codex, a runtime) shows its version states:
  too old (blocks, with an update action) and a newer one available (says so, does not block). A missing state is a
  finding; INPUT names the tool and the state.
- Never write the .impeccable folder. Skip critique-storage, saving critiques, init, hooks, pinning and all other
  writing steps. Do not create or change PRODUCT.md, DESIGN.md, reports or configuration.
- Skip the closing questions and owner interview. Include the line `Questions skipped: read-only review lane` in the
  answer, before the coverage lines; do not ask the user or initiate follow-up work.
- Use the finding format of this review command, including its UI id prefix, RULE, INPUT, WHERE, WHY and FIX fields.
  This replaces impeccable's report and scorecard format. Scores belong only as context in WHY, never as a separate
  scorecard. End with the required `seen N/N` and any `not read:` coverage lines.
