---
name: parity-refactor
description: Refactor a feature without changing what it does - freeze the old code as a Baseline, run old and new on the same inputs and compare every result, delete the Baseline only when they match. Use for any refactor, split or "clean up without changing behaviour"; a host pack may add its own parity-compare skill with the mechanics of comparing inside that host.
---

# Refactor under a parity check

**A refactor moves and splits code; it never changes what the feature produces.** Green unit tests
do not show that - they cover the cases somebody thought of. So the old code is kept alive beside the
new, both run on the same inputs, and the results must match. Only then does the old copy go.

```text
freeze Baseline -> build compare entry -> prove the compare -> refactor in small steps
     (compare after each) -> all identical: delete Baseline  |  a difference: read the Baseline
```

**This technique has a name**: the frozen copy is a *golden master*, and each compare run is a
*characterization test* - a test that records what the code does today rather than what anyone meant it to
do. Say the name when you use it: it is the standard answer to "how do I change code whose tests do not
cover it", and recognising it saves re-deriving the procedure from scratch.

In the task lifecycle this replaces the logic lane for a refactor, and `SPEC.md` does not change: a
refactor adds no acceptance line and leaves no `chờ kiểm` marker. The brief says what moves and why;
the plan carries the Baseline, compare and delete-Baseline tasks with their compare counts as evidence.

## 0. Before anything

- A branch that starts after any pending fix: the Baseline is whatever the branch starts from.
- Read the feature's `CODEMAP.md` and `CLAUDE.md`, its callers (grep the entry types), and its tests.
  Every outside caller is an API the refactor keeps or updates in the same step.
- Ask the user once: which settings the comparison runs with (usually the saved state of the dialog or
  the request), and how much real data a compare run may touch.

## 1. Freeze the Baseline

Copy the feature's **logic** files into `<Feature>/Baseline/`, changing only the namespace to end in
`.Baseline`. Logic only - no window, page, view or view model: a second copy of a UI type breaks the
build or, loaded at runtime, the UI.

```bash
mkdir -p Baseline && for f in Logic/*.cs; do
  sed 's/^namespace \(.*\);\?$/namespace \1.Baseline;/' "$f" > "Baseline/$(basename "$f")"; done
grep -L "\.Baseline" Baseline/*.cs        # must print nothing
```

- Shared types outside the copied folder (settings, DTOs) are shared by both sides and the refactor
  must not change them, or the Baseline silently changes with it.
- Keep the Baseline in an assembly the running host can reload, if the host pins some assemblies.
- Add `Baseline/BASELINE.md`: a frozen copy, never edited, deleted with the compare entry. **A fix made in
  the Baseline makes the comparison agree with a behaviour change instead of catching it.**

## 1b. What cannot be Baselined gets inventoried instead

The rule above forbids a second copy of a window, page or view model - rightly, it breaks the build or
the running UI. **So the UI half of a move has no golden master, and nothing checks it.** That is not a
small gap: on a module split measured 2026-09-20 the logic half compared clean while the UI half
silently lost four grid columns, a select-all tick box and eighteen bindings. Every one compiled.

What replaces the Baseline there is a list taken **before** the move and checked after. The two halves
come from different places, and mixing them up is the trap:

```bash
# The bindings are text, and XAML has no generator - grep is the whole answer.
grep -rho '{Binding [^},]*' Views/ | sort -u > /tmp/before-bindings.txt   # measured: 116 on one module
```

**The members are not text.** Read them from the built assembly's metadata, not the source - and not
with grep, which under-reports so badly it is worse than nothing: measured on the same module, a
`public .* (get|=>)` pattern found **13 members** because a property whose `get` sits on the next line
does not match, and a property a generator emits is not in the file at all.

- **Measured, for scale:** a source scan of one product returned **179** findings of which nearly all
  were wrong, because that product generates most of its properties and commands from an attribute.
  Reading the compiled metadata for the same question returned **9**, and every one was real.
- **After the move, every entry on the list still resolves**, or it is a column somebody stopped seeing.
  Turn the check into a test rather than a one-off: a binding that names nothing is not a compile error
  and not an exception - WPF and its kin leave the control blank and log to a trace nobody reads.
- **A rename is the usual cause**, so compare by what the entry *means*, not by its spelling:
  `SheetNumber` becoming `Number` is fine, `SheetNumber` becoming nothing is the finding.

## 2. The compare entry

One public entry that runs **both** sides on the same input and returns the differences.

- **Both sides start from the same state**, and neither leaves anything behind: each side's changes are
  undone after its result is read (a rolled-back transaction, a throwaway database, a copied file).
- **Compare results, not ids.** Reduce every output to what the user would see - kind, size, position
  rounded to a stated tolerance, the values the feature writes - and match the two sides as multisets.
  A throw is a result: the same message on both sides agrees.
- **Also compare a read-only path** that walks the same logic - a preview, a check, a dry run.
- **Log each input's line as it finishes**, tagged with the branch the Baseline took. A call can time
  out while the host carries on; the log survives, and coverage is a count over it.
- **Resolve cached services afresh per side.** Something created inside a rolled-back run is gone after
  the rollback; a cache handing it to the next side makes both fail and read as equal.

## 3. Choose the comparison set

Classify the data first with one cheap query, then pick a handful of inputs **covering every path the
data offers**. Name the paths it does not cover; those need unit-test cases or a captured fixture.
Split a heavy run into several calls in the host session already connected; a disposable copy (verb
`live`) only once the user has said yes - starting or stopping a host is their call.

## 4. Prove the compare before refactoring

1. **Baseline against the untouched live code: every result identical.** Anything else means the logic
   is not repeatable, and every later difference would be noise.
2. **A deliberate change must be caught**: alter one constant in the live code, build, compare - it must
   report differences. Revert, rebuild.

Both hold, or there is no refactor yet.

## 5. The loop - one small unit per step

1. **Pin** the unit's current behaviour in the project's unit test project - characterization tests
   asserting what it does today, even where that looks odd. Deciding logic only.
2. **Refactor** the unit; update outside callers in the same step.
3. **Build** every configuration the unit reaches (verb `build`); run verb `test`.
4. **Diff the code lines** against the Baseline - only plumbing may differ:

   ```bash
   norm() { cat "$@" | sed 's/\r//; s#//.*$##; s/^[ \t]*//; s/[ \t]*$//' | grep -v '^$' | grep -v '^using ' | grep -v '^namespace ' | sort; }
   norm Baseline/*.cs > /tmp/base.txt; norm Logic/*.cs > /tmp/live.txt
   comm -23 /tmp/base.txt /tmp/live.txt   # only in Baseline
   comm -13 /tmp/base.txt /tmp/live.txt   # only in live
   ```

   A changed condition, number, string or read order in that list is a logic change, however green the
   compare.
5. **Publish and compare** (verb `publish`, then the compare entry on the host). Every result identical,
   or stop.
6. Commit the step only if the user has asked for commits.

**Simple code first.** Before adding a delegate, interface or lazy wrapper just to move code, stop and
ask: say what it costs in readability and what it buys in tests. A bug found on the way is described,
not fixed - or fixed in its own step, where the compare shows exactly the difference it was meant to make.

## 6. Finish

**All identical, on every path the set covers:** delete `Baseline/` and the compare entry in one step,
build, test, and update the `CODEMAP.md` of every folder that moved (`code-map`, then `check-code-map`).

**A difference:** the Baseline is the authority. Read the Baseline's path for that input beside the live
one, line by line, and name the first divergence - a moved read, a lost branch, a changed default. Fix the
live code and compare again. Never edit the Baseline to make them agree, and never delete it while a
difference is unexplained.

Report: the comparison set and the paths it covered, the paths it did not, the per-step compare counts,
and whether the Baseline is deleted.
