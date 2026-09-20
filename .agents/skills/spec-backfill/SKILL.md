---
name: spec-backfill
description: Build or audit a Paper feature's living SPEC.md by reading its complete user flow, inputs, rules, tests, and runtime evidence. Use for an existing feature whose product contract is missing or may have drifted.
---

# Backfill a Paper feature spec

This workflow recovers observable behavior from an existing feature. It cannot prove that current
behavior is what the user originally wanted, so mark a newly backfilled contract as current-behavior
evidence until a domain owner confirms it.

## Read the whole behavior path

Read the command/executor, UI and code-behind, settings/defaults, domain or geometry rules, tests,
the `CODEMAP.md` to find all of them, comments at the deciding code, and relevant live evidence from the host. Do not derive the contract from notes alone
and do not sample only the first matching file.

Map evidence into the sections defined by the `spec` skill:

- a one-paragraph user story: who uses it, what for, and why;
- user flow from entry point through success and failure;
- every user input, default, and resulting decision;
- every output the user can see: elements created or changed, reports, messages;
- rules from domain/geometry code and measured runtime behavior;
- acceptance cases from tests and uncovered reproducible boundaries;
- deliberate gaps from current instructions and verified handovers.

Read through to concrete numbers and outcomes. Replace phrases such as "configured size" with the
actual default or lookup values users receive.

## Resolve disagreement explicitly

- Backfill mode: source and verified runtime behavior describe current behavior; update `SPEC.md` and
  report any requirement that still needs domain confirmation.
- Spec-first mode: an already accepted `SPEC.md` is authority; a mismatch is a product finding and the
  implementation must be repaired or the user must explicitly change the contract.

A backfill that comes before a change is the first step of `/task-spec`, not a separate write-up: the
recovered `SPEC.md` carries the `Bản nháp chờ duyệt` banner until the user confirms it, and the change
then edits it in place with `chờ kiểm` markers, before the brief and the plan are written.

Before finishing, verify that the flow includes failure reporting, all inputs include defaults, every
rule has a measurable acceptance case or is labeled an assumption, and no code symbol appears in the
contract.
