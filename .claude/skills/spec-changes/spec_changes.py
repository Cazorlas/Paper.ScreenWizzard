"""Record what this branch changed in each SPEC.md, section by section.

    python .claude/skills/spec-changes/spec_changes.py [--preview] [base]      base defaults to origin/main

Compares every SPEC.md of the working copy - uncommitted edits included - with the merge base of the
branch and `base`. Only rules are compared: a bullet, a numbered step, a table row, a drawing. A paragraph
explains a rule and is left out, reworded or not.

--preview prints the record and writes nothing. Without it, each changed SPEC.md gets
    <folder of SPEC.md>/spec-changes/<date of the branch's first commit>-<last part of the branch name>.md
The name is the same on every run, so a second run rewrites the file instead of adding one; then the
summary table for the pull request description is printed.

Exit code 0 done (nothing changed included), 2 when the base or the repository cannot be read.
"""

import datetime
import difflib
import io
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")
_HERE = os.path.dirname(os.path.abspath(__file__))
# Vendored at <project>/.claude/skills/spec-changes, so the project root is three folders up.
os.chdir(os.path.join(_HERE, "..", "..", ".."))

RULE_START = re.compile(r"^(?:[-*+] |\d+[.)] |\||!\[)")
HEADING = re.compile(r"^(#{1,4}) +(.+?)\s*#*\s*$")
TABLE_SEPARATOR = re.compile(r"^\|[\s:|-]+\|?$")
SAME_RULE = 0.6


def git(*args, check=True):
    done = subprocess.run(["git", "-c", "core.quotepath=off", *args], capture_output=True,
                          text=True, encoding="utf-8", errors="replace")
    if check and done.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)}: {done.stderr.strip()}")
    return done.stdout


# ------------------------------------------------------------------ reading a spec into rules


def rules_by_section(text):
    """Section heading -> the rules under it, each one line of text.

    A rule is a bullet or numbered step with its indented continuation lines, a table row other than the
    header and the separator, or an image. Fenced blocks and paragraphs are not rules."""
    sections = {}
    order = []
    heading = "(before the first heading)"
    rules = []
    current = None
    fenced = False
    table = []

    def close_table():
        # The row above the separator names the columns; it is not a rule.
        rows = [r for i, r in enumerate(table)
                if not TABLE_SEPARATOR.match(r) and not (i + 1 < len(table) and TABLE_SEPARATOR.match(table[i + 1]))]
        rules.extend(rows)
        table.clear()

    def close_section():
        nonlocal current
        if current:
            rules.append(" ".join(current))
            current = None
        close_table()
        if heading not in sections:
            order.append(heading)
            sections[heading] = []
        sections[heading].extend(rules)
        rules.clear()

    for line in text.replace("\r\n", "\n").split("\n"):
        stripped = line.strip()
        if stripped.startswith("```") or stripped.startswith("~~~"):
            fenced = not fenced
            continue
        if fenced:
            continue
        match = HEADING.match(line)
        if match:
            close_section()
            heading = match.group(2)
            continue
        if stripped.startswith("|"):
            if current:
                rules.append(" ".join(current))
                current = None
            table.append(stripped)
            continue
        close_table()
        if not stripped:
            if current:
                rules.append(" ".join(current))
                current = None
            continue
        if RULE_START.match(stripped) and not line.startswith("  "):
            if current:
                rules.append(" ".join(current))
            current = [stripped]
        elif current and line.startswith((" ", "\t")):
            current.append(stripped)
        elif current:
            # A paragraph right under a bullet with no indent still continues it in markdown.
            current.append(stripped)
    close_section()
    return order, {h: [re.sub(r"\s+", " ", r) for r in sections[h]] for h in order}


# ------------------------------------------------------------------ comparing


def words(text):
    return text.replace("**", "").replace("~~", "").split()


def likeness(a, b):
    """How alike two rules are; a rule that only grew or shrank still counts as the same rule."""
    wa, wb = words(a), words(b)
    matcher = difflib.SequenceMatcher(None, wa, wb, autojunk=False)
    kept = sum(block.size for block in matcher.get_matching_blocks())
    shorter = min(len(wa), len(wb))
    return max(matcher.ratio(), kept / shorter if shorter >= 4 else 0.0)


def compare(old, new):
    """(added, changed as (old, new), removed) between two lists of rules in order."""
    added, changed, removed = [], [], []
    matcher = difflib.SequenceMatcher(None, old, new, autojunk=False)
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag == "equal":
            continue
        gone, came = list(old[i1:i2]), list(new[j1:j2])
        for rule in list(gone):
            if not came:
                break
            best = max(came, key=lambda c: likeness(rule, c))
            if likeness(rule, best) >= SAME_RULE:
                changed.append((rule, best))
                gone.remove(rule)
                came.remove(best)
        removed += gone
        added += came
    return added, changed, removed


# ------------------------------------------------------------------ writing the record


def as_sentence(rule):
    """A bullet loses its dash, a numbered step keeps its number, a table row reads 'first: rest - rest'."""
    if rule.startswith("|"):
        cells = [c.strip() for c in rule.strip().strip("|").split("|")]
        rest = " - ".join(c for c in cells[1:] if c)
        return f"{cells[0]}: {rest}" if rest else cells[0]
    return re.sub(r"^[-*+] ", "", rule)


def from_record_folder(text):
    """Relative links in a spec are relative to its folder; the record sits one folder below it."""
    return re.sub(r"\]\((?![a-zA-Z][a-zA-Z0-9+.-]*:|/|#)", "](../", text)


def plain(text):
    return " ".join(words(text))


def now_and_before(old, new):
    """The changed rule twice: the new words bold in Now, the dropped words struck in Before."""
    a, b = words(as_sentence(old)), words(as_sentence(new))
    now, before = [], []
    for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, a, b, autojunk=False).get_opcodes():
        if tag == "equal":
            now += b[j1:j2]
            before += a[i1:i2]
            continue
        if j2 > j1:
            now.append("**" + " ".join(b[j1:j2]) + "**")
        if i2 > i1:
            before.append("~~" + " ".join(a[i1:i2]) + "~~")
    return [f"- *Now:* {from_record_folder(' '.join(now))}",
            f"  - *Before:* {from_record_folder(' '.join(before))}"]


def section_lines(heading, note, added, changed, removed):
    out = [f"## {heading}{note}", ""]
    if added:
        out += ["**Added**", ""]
        for rule in added:
            image = re.match(r"!\[(.*?)\]\((.*?)\)", rule)
            # A drawing is shown, not described - on its own lines, or markdown folds it into the bullet above.
            out += ["", from_record_folder(rule), ""] if image else [f"- {from_record_folder(plain(as_sentence(rule)))}"]
        out.append("")
    if changed:
        out += ["**Changed**", ""]
        for old, new in changed:
            out += now_and_before(old, new)
        out.append("")
    if removed:
        out += ["**Removed**", ""]
        for rule in removed:
            image = re.match(r"!\[(.*?)\]\((.*?)\)", rule)
            out.append(f"- ~~drawing: {image.group(1) or image.group(2)}~~" if image
                       else f"- ~~{plain(as_sentence(rule))}~~")
        out.append("")
    return out


# ------------------------------------------------------------------ the branch


def changed_specs(base):
    tracked = git("diff", "--name-only", base, "--").split("\n")
    untracked = git("ls-files", "--others", "--exclude-standard").split("\n")
    return sorted({p for p in tracked + untracked if p == "SPEC.md" or p.endswith("/SPEC.md")})


def read_working(path):
    return io.open(path, encoding="utf-8-sig").read() if os.path.isfile(path) else ""


def main(argv):
    preview = "--preview" in argv
    names = [a for a in argv if not a.startswith("--")]
    base_ref = names[0] if names else "origin/main"

    try:
        root = git("rev-parse", "--show-toplevel").strip()
        os.chdir(root)
        base = git("merge-base", base_ref, "HEAD").strip()
    except RuntimeError as error:
        print(f"spec-changes: cannot compare with '{base_ref}' - {error}. "
              "Fetch it (git fetch origin main) or name the base as the last argument.", file=sys.stderr)
        return 2

    branch = git("rev-parse", "--abbrev-ref", "HEAD").strip()
    history = [l for l in git("log", "--reverse", "--format=%cs|%s", f"{base}..HEAD").split("\n") if l]
    started, _, subject = (history[0] if history else "").partition("|")
    started = started or datetime.date.today().isoformat()
    subject = subject or branch
    short = re.sub(r"[^a-z0-9]+", "-", branch.split("/")[-1].lower()).strip("-") or "branch"
    file_name = f"{started}-{short}.md"

    summary = []
    for path in changed_specs(base):
        old_order, old = rules_by_section(git("show", f"{base}:{path}", check=False))
        new_order, new = rules_by_section(read_working(path))
        body, headings, totals = [], [], [0, 0, 0]
        for heading in new_order + [h for h in old_order if h not in new]:
            added, changed, removed = compare(old.get(heading, []), new.get(heading, []))
            if not (added or changed or removed):
                continue
            note = " (new section)" if heading not in old else " (section removed)" if heading not in new else ""
            body += section_lines(heading, note, added, changed, removed)
            headings.append(heading)
            totals = [totals[0] + len(added), totals[1] + len(changed), totals[2] + len(removed)]
        if not headings:
            continue

        folder = os.path.dirname(path)
        target = "/".join(p for p in (folder, "spec-changes", file_name) if p)
        feature = os.path.basename(folder) or "(root)"
        counts = f"{totals[0]} added, {totals[1]} changed, {totals[2]} removed"
        text = "\n".join([f"# Spec changes - {feature}", "", f"**{subject}**", "",
                          f"{started} - branch `{branch}` - {len(headings)} sections: {counts}.", "",
                          "*Written by the spec-changes script from SPEC.md itself - never edit it by hand.*", ""]
                         + body)
        text = re.sub(r"\n{3,}", "\n\n", text).rstrip("\n") + "\n"
        summary.append(f"| [{feature}]({target}) | {', '.join(headings)} | {totals[0]} | {totals[1]} | {totals[2]} |")

        if preview:
            print(f"===== {target} (preview)")
            print(text)
            continue
        os.makedirs(os.path.dirname(target), exist_ok=True)
        with io.open(target, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print(f"wrote {target}")

    if not summary:
        print(f"No SPEC.md changed on this branch since {base_ref} - nothing to record.")
        return 0
    if preview:
        print("Preview only - nothing was written.")
        return 0
    print("\n### Spec changes\n")
    print("| Feature | Sections | Added | Changed | Removed |")
    print("| --- | --- | --: | --: | --: |")
    print("\n".join(summary))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
