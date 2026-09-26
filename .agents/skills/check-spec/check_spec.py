"""Check every SPEC.md in the repository for the code's vocabulary, stale drawings and unclosed markers.

A SPEC.md is written in the user's words and names nothing from the code - no class,
method, property or test - so it cannot drift when the code is renamed. A code map is
the opposite: it must name code so it can be checked against it
(.claude/skills/check-code-map). The backtick pattern and the list of framework names
that are not ours are imported from that script, so both agree on what counts as a name.

First written for one project, where it searched that project's docs/ folder. Here it searches
the whole repository, because the kit does not know where a project keeps its features.

Two checks on the folder a SPEC.md sits in ride along, because they are about the same document:
  F6  a drawing under shapes/ whose SVG is newer than its rendered PNG, or that has no PNG at all -
      the picture the spec links no longer shows what its source says;
  F7  a draft banner or a change marker still in SPEC.md while the plan it belongs to is finished -
      the work is done and the requirement was never closed;
  F26 a SPEC.md being written or changed (it carries a banner or a marker) without both language
      parts - a "# English" part and a "# Tieng Viet" part in the one file - or with two parts whose
      shape differs: other sections, other acceptance lines, other F codes. The owner reads Vietnamese
      and the code's people read English, so a requirement is approved in both.

Exit code 0 when clean, 1 when any check finds a problem.
"""

import glob
import io
import os
import re
import sys
import time
import unicodedata

sys.stdout.reconfigure(encoding="utf-8")
_HERE = os.path.dirname(os.path.abspath(__file__))
os.chdir(os.path.join(_HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(_HERE, "..", "check-code-map"))

from check_code_map import EXTERNAL, TICKED, walk  # noqa: E402

# What a SPEC may not name - wider than CAMEL on purpose. A test called
# `AColumnStandingOnTheSlabIsNotPartOfItsBoundary` starts with two capitals, and
# `Given_x_Then_y` is joined by underscores; CAMEL misses both, and both got into a
# spec past it. A name needs a lowercase letter, so a view or parameter the user
# sees in capitals (`PAPER_PLAN_VIEW`) is still the user's word, not the code's.
SPEC_NAME = re.compile(r"^(?=\w*[a-z])(?=(?:\w*[A-Z]){2}|\w*[A-Za-z]_[A-Za-z])\w+$")

# A git checkout writes a.png and then a.svg a few milliseconds apart, so a fresh clone would report every
# drawing as stale without some slack. A real edit after a render is seconds later at the very least.
RENDER_SLACK_SECONDS = 2.0

# Marker words compared without diacritics: skill `spec` writes them in Vietnamese, and an editor may
# store the same letters decomposed.
DRAFT_BANNER = "ban nhap cho duyet"
CHANGE_MARKER = "cho kiem"
FINISHED = re.compile(r"^(xong|done)\b")
# The brief or plan a marker names: 2026-09-17-task, 2026-09-17-task.md or 2026-09-17-task-plan.md.
DATED_NAME = re.compile(r"(\d{4}-\d{2}-\d{2}-[\w.-]+?)(?:-plan)?(?:\.md)?(?=[)\]\s,;]|$)")
STROKED_D = chr(0x111)


# The two language parts of one SPEC.md: a level-1 heading each, compared without diacritics.
LANGUAGE_PARTS = {"english": "English", "tieng viet": "Tiếng Việt"}
# One line of acceptance: "- Cho ... ->" in Vietnamese, "- Given ... ->" in English (a Vietnamese part may
# write Given too). Counted, never compared word for word - the two languages share only the shape.
ACCEPTANCE = re.compile(r"^\s*[-*]\s+(?:cho|given)\b", re.IGNORECASE)
F_CODE = re.compile(r"^\s*\|\s*(F\d+)\s*\|")


def find_specs():
    out = []
    for dirpath, filenames in walk("."):
        if "SPEC.md" in filenames:
            out.append(os.path.join(dirpath, "SPEC.md").replace("\\", "/"))
    return sorted(out)


def language_parts(text):
    """Part name -> its lines, for every "# English" / "# Tieng Viet" heading; a part runs to the next level-1
    heading. Lines inside a fenced block never start a part."""
    parts, current, fenced = {}, None, False
    for line in text.split("\n"):
        if line.strip().startswith("```"):
            fenced = not fenced
        if not fenced and re.match(r"^# \S", line):
            key = plain_words(line[2:]).strip(" #")
            current = LANGUAGE_PARTS.get(key)
            if current:
                parts[current] = []
                continue
        if current:
            parts[current].append(line)
    return {name: "\n".join(lines) for name, lines in parts.items()}


def shape(text):
    """What both languages must share: the section count, the acceptance line count and the F codes."""
    lines = text.split("\n")
    return (
        sum(1 for line in lines if line.startswith("## ")),
        sum(1 for line in lines if ACCEPTANCE.match(line)),
        sorted({m.group(1) for m in map(F_CODE.match, lines) if m}, key=lambda c: int(c[1:])),
    )


def has_marker(text):
    return any(
        line.lstrip().startswith(">") and (DRAFT_BANNER in plain_words(line) or CHANGE_MARKER in plain_words(line))
        for line in text.split("\n")
    )


def check_languages(text):
    """F26 - a SPEC under change has an English part and a Vietnamese part, and the two keep one shape."""
    parts = language_parts(text)
    missing = [name for name in LANGUAGE_PARTS.values() if name not in parts]
    if missing:
        if not has_marker(text):
            return []
        wanted = " and ".join(f'"# {name}"' for name in missing)
        return [(0, f"SPEC.md is being written or changed and has no {wanted} part - one file, both languages")]
    en, vi = shape(parts["English"]), shape(parts["Tiếng Việt"])
    problems = []
    if en[0] != vi[0]:
        problems.append((0, f"English has {en[0]} sections, Tiếng Việt {vi[0]}"))
    if en[1] != vi[1]:
        problems.append((0, f"English has {en[1]} acceptance lines, Tiếng Việt {vi[1]}"))
    only_en = [c for c in en[2] if c not in vi[2]]
    only_vi = [c for c in vi[2] if c not in en[2]]
    if only_en:
        problems.append((0, f"{', '.join(only_en)} in English and not in Tiếng Việt"))
    if only_vi:
        problems.append((0, f"{', '.join(only_vi)} in Tiếng Việt and not in English"))
    return problems


def check_spec(text):
    """A SPEC is written in the customer's words, so it names nothing from the code -
    no class, no method, and no test. A test name is no exception: the acceptance line
    IS the test case, and the test is written from it."""
    problems = []
    for lineno, line in enumerate(text.split("\n"), 1):
        for raw in TICKED.findall(line):
            head = re.split(r"[.(<\[\s/]", raw.strip())[0]
            if not SPEC_NAME.match(head) or head in EXTERNAL:
                continue
            problems.append((lineno, f"names {head} - a spec names nothing from the code, tests included"))
    return problems


def check_shapes(folder):
    """F6 - every shapes/*.svg beside the spec has a PNG rendered from it, and the PNG is not older."""
    problems = []
    for svg in sorted(glob.glob(os.path.join(folder, "shapes", "*.svg"))):
        name = os.path.basename(svg)
        png = svg[:-4] + ".png"
        if not os.path.isfile(png):
            problems.append((0, f"shapes/{name} has no rendered {name[:-4]}.png - run render-shapes"))
        elif os.path.getmtime(svg) - os.path.getmtime(png) > RENDER_SLACK_SECONDS:
            problems.append((0, f"shapes/{name} is newer than {name[:-4]}.png - render it again and look at the picture"))
    return problems


def plain_words(text):
    """Lowercase, without diacritics; the stroked d has no decomposition and is mapped by hand."""
    text = unicodedata.normalize("NFD", text.lower()).replace(STROKED_D, "d")
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


def plan_status(path):
    """The plan's status value in plain words, or None when it has no status line."""
    for line in io.open(path, encoding="utf-8-sig").read().split("\n"):
        match = re.match(r"^(?:trang thai|status)\s*:\s*(.*)$", plain_words(line).replace("*", "").strip())
        if match:
            return match.group(1).strip()
    return None


def check_markers(folder, text):
    """F7 - a draft banner or change marker whose plan is finished.

    Only a quoted line (> ...) is a banner or a marker; a rule that talks about markers is not one. A marker
    naming its brief or plan is judged by that plan. One naming neither is judged by the folder: it is left
    over when no plan there is still open and at least one is finished."""
    plans = {os.path.basename(p): p for p in glob.glob(os.path.join(folder, "*-plan.md"))}
    statuses = {name: plan_status(path) for name, path in plans.items()}
    finished = sorted(n for n, s in statuses.items() if s is not None and FINISHED.match(s))
    still_open = [n for n, s in statuses.items() if s is not None and not FINISHED.match(s)]

    problems = []
    fenced = False
    for lineno, line in enumerate(text.split("\n"), 1):
        if line.strip().startswith("```"):
            fenced = not fenced
        if fenced or not line.lstrip().startswith(">"):
            continue
        words = plain_words(line)
        kind = "draft banner" if DRAFT_BANNER in words else "change marker" if CHANGE_MARKER in words else None
        if not kind:
            continue
        owners = [n for n in (m + "-plan.md" for m in DATED_NAME.findall(line)) if n in plans]
        done = [n for n in owners if n in finished] if owners else (finished if not still_open else [])
        if done:
            problems.append((lineno, f"{kind} still here while plan {', '.join(done)} is finished - close SPEC.md"))
    return problems


def main():
    started = time.time()
    specs = find_specs()
    groups = [
        ("SPEC - a requirement written in the code's words (a test name counts):", []),
        ("F6 - a drawing whose rendered picture is missing or older than its source:", []),
        ("F7 - a draft banner or change marker left behind by a finished plan:", []),
        ("F26 - a SPEC under change without both language parts, or with two parts of another shape:", []),
    ]
    for path in specs:
        text = io.open(path, encoding="utf-8-sig").read()
        folder = os.path.dirname(path)
        found = (check_spec(text), check_shapes(folder), check_markers(folder, text), check_languages(text))
        for (_, bad), problems in zip(groups, found):
            if problems:
                bad.append((path, problems))

    count = 0
    for title, bad in groups:
        if not bad:
            continue
        print(title + "\n")
        for path, problems in bad:
            print(f"  {path}")
            for lineno, what in problems:
                print(f"      line {lineno:>5}  {what}" if lineno else f"      {what}")
                count += 1
        print()

    print(f"{len(specs)} SPEC.md, {count} problems - {time.time() - started:.1f}s")
    return 1 if count else 0


if __name__ == "__main__":
    sys.exit(main())
