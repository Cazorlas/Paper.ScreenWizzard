---
name: systematic-debugging
description: Use when encountering any bug, test failure, or unexpected behavior, before proposing fixes
---

# Systematic Debugging

Random fixes waste time and create new bugs. Quick patches mask underlying issues.

**Core principle:** ALWAYS find root cause before attempting fixes. Symptom fixes are failure.
**Violating the letter of this process is violating the spirit of debugging.**

## The Iron Law

```text
NO FIXES WITHOUT ROOT CAUSE INVESTIGATION FIRST
```

If you haven't completed Phase 1, you cannot propose fixes.

## When to Use

Any technical issue: test failures, production bugs, unexpected behavior, performance problems, build
failures, integration issues.

**ESPECIALLY when** under time pressure, when "just one quick fix" seems obvious, when you've already
tried several fixes, or when you don't fully understand the issue. **Don't skip** because it seems simple
or because someone wants it fixed now - systematic is faster than thrashing.

## Before anything in a live host: is the code you think is running, running?

The most common wasted hour is debugging a change that never reached the host. A host that already loaded
an assembly usually answers a second load of the same identity with the copy it has - and reports success.
Rule it out first: evidence that the host runs the new build (a reload result, the loaded assembly's hash,
a behaviour only the new build has). The host pack's live skill says how to get it.

## The Four Phases

You MUST complete each phase before proceeding to the next.

### Phase 1: Root Cause Investigation

1. **Read error messages carefully** - don't skip past errors or warnings; read stack traces completely;
   note line numbers, file paths, error codes. They often contain the exact solution.
2. **Reproduce consistently** - the exact steps, every time? If not reproducible, gather more data; don't
   guess.
3. **Check recent changes** - git diff, recent commits, new dependencies, config and environment changes,
   the build configuration, and what is actually deployed or loaded.
4. **Gather evidence across component boundaries** - when the system has several components, log what
   enters and leaves each boundary and run once to see WHERE it breaks, before proposing any fix. Worked
   example: `references/multi-component-evidence.md`.
5. **Trace data flow** - when the error is deep in the call stack: where does the bad value originate,
   what called this with it? Keep tracing up; fix at the source, not the symptom. Full technique:
   `root-cause-tracing.md`.

### Phase 2: Pattern Analysis

1. **Find working examples** - similar working code in the same codebase.
2. **Compare against references** - read the reference implementation COMPLETELY, every line.
3. **Identify differences** - list every one, however small; don't assume "that can't matter".
4. **Understand dependencies** - components, settings, config, environment, assumptions, the host version
   and the document open in it.

### Phase 3: Hypothesis and Testing

1. **Form a single hypothesis** - "I think X is the root cause because Y". Write it down; be specific.
2. **Test minimally** - the SMALLEST change that tests it, one variable at a time.
3. **Verify before continuing** - worked → Phase 4; didn't → form a NEW hypothesis. DON'T add more fixes
   on top.
4. **When you don't know** - say "I don't understand X". Don't pretend; ask, research more.

### Phase 4: Implementation

1. **Create a failing test case** - simplest possible reproduction, automated if possible, a one-off
   script if there is no framework, a recorded read-back from the host when only the host shows the
   behaviour. MUST exist before fixing. Use the repo test conventions (the tests
   folder's `CODEMAP.md` for which project, that project's `CLAUDE.md` for how). When the behaviour is
   a rule in the feature's `SPEC.md`, the test case is its `Cho … →` line with its numbers; when the code
   already does what that line says and the result is still wrong, it is a spec gap - stop and take it
   through `/task-bug`, which changes `SPEC.md` before any code.
2. **Implement a single fix** - the root cause, ONE change, no "while I'm here" improvements, no bundled
   refactoring.
3. **Verify the fix** - the test passes, no other test broke, the issue is actually resolved.
4. **If the fix doesn't work** - STOP and count the fixes tried. Fewer than 3: back to Phase 1 with the new
   information. **3 or more: step 5** - don't attempt fix #4 without an architectural discussion.
5. **If 3+ fixes failed: question the architecture.** Signs: each fix reveals new shared state or
   coupling somewhere else; fixes need "massive refactoring"; each fix creates new symptoms. Ask whether
   the pattern is fundamentally sound, or kept through inertia. **Discuss with your human partner before
   attempting more fixes.** This is not a failed hypothesis - it is a wrong architecture.

## Red Flags - STOP and Follow Process

If you catch yourself thinking:

- "Quick fix for now, investigate later"
- "Just try changing X and see if it works"
- "Add multiple changes, run tests"
- "Skip the test, I'll manually verify"
- "It's probably X, let me fix that"
- "I don't fully understand but this might work"
- "Pattern says X but I'll adapt it differently"
- "Here are the main problems: [lists fixes without investigation]"
- Proposing solutions before tracing data flow
- **"One more fix attempt" (when already tried 2+)**
- **Each fix reveals a new problem in a different place**

**ALL of these mean: STOP. Return to Phase 1.** If 3+ fixes failed, question the architecture (Phase 4,
step 5). Tempted to argue your way past the process? Read `references/rationalizations.md` first - it
also lists the phrases a human partner uses when you are guessing.

## Quick Reference

| Phase | Key Activities | Success Criteria |
|-------|---------------|------------------|
| **1. Root Cause** | Read errors, reproduce, check changes, gather evidence | Understand WHAT and WHY |
| **2. Pattern** | Find working examples, compare | Identify differences |
| **3. Hypothesis** | Form theory, test minimally | Confirmed or new hypothesis |
| **4. Implementation** | Create test, fix, verify | Bug resolved, tests pass |

## When Process Reveals "No Root Cause"

If investigation shows the issue is truly environmental, timing-dependent or external: document what you
investigated, implement appropriate handling (retry, timeout, error message), and add monitoring or
logging for next time. **But** 95% of "no root cause" cases are incomplete investigation.

## Supporting Techniques

In this directory:

- **`root-cause-tracing.md`** - trace bugs backward through the call stack to the original trigger
- **`defense-in-depth.md`** - add validation at several layers after finding the root cause
- **`condition-based-waiting.md`** - replace arbitrary timeouts with condition polling
- **`references/multi-component-evidence.md`** - instrumenting component boundaries, worked example
- **`references/rationalizations.md`** - excuses and their answers, partner signals, measured impact

**Related skills:** the repo test conventions (tests folder `CODEMAP.md`, test project `CLAUDE.md`) for
the failing test in Phase 4; **verification-before-completion** before claiming the fix works.
