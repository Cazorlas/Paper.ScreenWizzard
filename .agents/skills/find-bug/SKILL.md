---
name: find-bug
description: Read a feature's code adversarially against its SPEC.md and report rules the implementation does not satisfy, each with the concrete input that breaks it. Use before reporting a task done (inside /paperflow when the project has it), when a user report contradicts the spec, or before shipping.
---

# find-bug

**The spec is right and the code is guilty until it proves otherwise.** One caution: a spec freshly
written by `spec-backfill` is the code's behaviour copied down, so a rule nobody agreed can sit in it.

**A finding is a concrete input that produces the wrong answer.** "This looks fragile" is not a
finding. If you cannot name the element, the size, the value or the sequence that breaks it, it is a
suspicion - list it separately as one, and do not pad the findings with it.

**Scope.** Handed the output of `review-files` (as `/task-verify` does), the hunt covers exactly that
list - no more, no fewer - and a group rule printed beside a file applies to it on top of the spec. The
report ends with `đã xem N/N` and names every file not read; one missing file leaves the pass unfinished.

## How

1. **Read the whole `SPEC.md`.** Every acceptance line is a claim the code must satisfy - a line under a
   `chờ kiểm` marker or a `Bản nháp chờ duyệt` banner included: it is what the open plan promised, and
   the reason `/task-verify` runs this before it closes the spec.
2. **For each rule, find the code that implements it** - not the code that looks related. A rule that
   nothing implements is the strongest finding there is.
3. **Walk the acceptance line's `given` through the real path**: the entry point, what the caller
   passes, every method it reaches, every early return.
4. **Where the path needs a live model, measure it** - the live lane in the host session already
   connected (a disposable copy only with the user's OK) - and read the model back. A return
   value is not evidence.
5. **Report what fails, most severe first.**

## Nine ways a rule is broken without the diff showing it

**A rule enforced at some call sites and not others.** Count the call sites, not the rule. Two
commands that both create a tap, or a preview and an apply that walk the same layout, each have to
honour it.

**Two implementations of one question.** A perpendicularity test written three times under three
names, and one lookup growing a fallback its extension-method twin never got. **Search
by behaviour, not by name** - the second copy rarely shares the first one's name.

**A host API call that reports success without doing the job.** A connect call returned success
without creating the segment the spec required. Check the state the rule describes, not the call's
result. A host pack may keep the traps already met in its API lookup skill's references.

**A hand-rolled version of something the host already decides.** A reimplemented rule took the first
entry of a preference list where the host applies size bands - identical on one model, wrong on the
next. Where the host has the rule, compare against the host's answer.

**A bounding box used as a shape.** Direction, "nearest" or "touches" taken from an axis-aligned box: its
diagonal always reads as ascending, and a long diagonal element's box centre sits far from the element.
Take the result from the geometry; a host pack's geometry skill lists the forms it takes there.

**A point in the wrong coordinate space.** An instance's own coordinates against the world's, a nested or
linked instance's transform, a mirrored instance, a view or sheet against the model. The rule holds in the
space the test used; check the others.

**An early return that skips a later check.** Follow every `return` and `continue`, and ask what sits
downstream of it for that input - and whether the user is told.

**A rule stated for one side and not its mirror.** Start and end, left and right, above and below the
main, upstream and downstream, mirrored and not, rotated by 90 and by 270 degrees. If the rule mentions
one, check the other explicitly.

**A test that proves the leaf and not the plumbing.** A test called the leaf with its flag
already set and stayed green while the feature was broken. For each acceptance line, find the test
that drives it from the entry point; a line covered only below the decision is unproven.

## What a finding looks like

```
RULE      A widened local segment gets a transition at each side        (SPEC, "Grilles on ducts")
CODE      <where it is implemented, or "nothing implements this">
FAILS     Given a grille 300 mm from the duct's open end, only the inner transition is
          placed - the outer side is skipped because the segment reaches the end.
SEVERITY  An open 1200 mm duct end in the installed system.
```

**Severity is what reaches the site, not how deep in the code it sits.** A wrong size or a free
connector in the model outranks anything that only costs time.

## Verifying a finding before it becomes a task

A finding with a concrete input does not go straight to a repair task. A second agent - who did not
hunt it and is told nothing about who found it or why - reads it back first.

**The candidate that agent gets is only the rule, quoted verbatim, and the input.** Not the code path,
not the finder's explanation, not the severity. The reader relocates the code from the rule and the
input alone; being handed the spot to check turns this into confirming a claim, not testing one.

**Three verdicts, no others:**

- **confirmed** - the reader reproduced the same wrong answer on that input, independently (against
  the source, or against the connected host under the rule already in force). Say what was run and
  what it showed.
- **rejected** - the reader tried the input and got the right answer: the source was already correct,
  the rule was read wrong, or the input never reaches the branch the finding named. Say which, and why
  - a rejection is a line in the report, never a silent drop.
- **needs_validation** - the fact that would decide it is out of reach right now (the host is not
  open, the data does not exist). Not a rejection; name exactly what is missing, and do not let it
  block the other findings verifying beside it.

**Fingerprint by the broken spot the reader independently located, not by the input.** Two inputs
that land on the same cause are one task, once a reader has traced both to it.

Verify findings in parallel, one reader per finding - a reader only reads, never fixes.

A suspicion (no concrete input) skips all of this: it stays a line in the report, as before, and is
never chased into a verdict.

### What a verdict looks like

```
RULE          <the rule, quoted verbatim - same text the candidate carried>
INPUT         <the same input>
VERDICT       confirmed | rejected | needs_validation
EVIDENCE      <what the reader ran or read, and what it showed>
FINGERPRINT   <where confirmed: the real broken spot the reader located, not the input -
              two findings with the same fingerprint are one task>
```

## Before reporting

- Every finding names the input that fails. No input, no finding.
- Check the acceptance line is what the code should do. A rule quietly changed on purpose is a spec
  gap for the user to decide, not a defect - say which it is. A spec gap is proposed as a new
  `SPEC.md` line in numbers; the plan goes back to `chờ duyệt`.
- **Do not fix anything here.** If the project has `/paperflow` and this runs inside it, a finding
  verified **confirmed** goes back to its repair loop with a regression test written from its input;
  **rejected** and **needs_validation** stay in the report. Otherwise report and stop.
- Say what you could not check and why. An unchecked rule reported as passing is worse than one
  reported as unchecked.
