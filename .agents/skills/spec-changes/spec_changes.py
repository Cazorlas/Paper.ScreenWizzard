"""Record what this branch changed in each SPEC.md, section by section.

    python .claude/skills/spec-changes/spec_changes.py [--preview] [base]      base defaults to origin/main
    python .claude/skills/spec-changes/spec_changes.py --view [docs/features/<slug>] [base]

Compares every SPEC.md of the working copy - uncommitted edits included - with the merge base of the
branch and `base`. Only rules are compared: a bullet, a numbered step, a table row, a drawing. A paragraph
explains a rule and is left out, reworded or not.

A long requirement is a tree (ADR-0025): SPEC.md is the index and each part a file spec/<part>.md beside it.
The tree is one feature: it changed when SPEC.md or any part changed, and its rules are pooled by section
heading across the files, so a rule moved word for word from SPEC.md into a part is no change. The index's
table of parts is navigation, not a rule. Links in a part are read relative to the feature folder, so a
drawing shows from spec-changes/ wherever it was written.

--view builds the review page instead of the record - spec_view.py, written to .paper/spec-view/<slug>.html -
for the named feature, or for every feature the branch changed.

--preview prints the record and writes nothing. Without it, each changed SPEC.md gets
    <folder of SPEC.md>/spec-changes/<date of the branch's first commit>-<last part of the branch name>.md
The name is the same on every run, so a second run rewrites the file instead of adding one; then the
summary table for the pull request description is printed.

Exit code 0 done (nothing changed included), 2 when the base or the repository cannot be read.
"""

import datetime
import difflib
import glob
import io
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")
_HERE = os.path.dirname(os.path.abspath(__file__))
# Vendored at <project>/.claude/skills/spec-changes, so the project root is three folders up. main() moves
# there before asking git anything; importing this file for one function changes nothing.
PROJECT_ROOT = os.path.abspath(os.path.join(_HERE, "..", "..", ".."))

RULE_START = re.compile(r"^(?:[-*+] |\d+[.)] |\||!\[)")
HEADING = re.compile(r"^(#{1,4}) +(.+?)\s*#*\s*$")
TABLE_SEPARATOR = re.compile(r"^\|[\s:|-]+\|?$")
SAME_RULE = 0.6
# A SPEC.md carries an English part and a Vietnamese part under level-1 headings (skill spec, "Two
# languages"). Both have a "History" or a "Log"; without the part in the key, the rules of the two would
# be pooled under one section and a change in one language would read as a change in both.
LANGUAGE_PART = re.compile(r"^(english|ti[eế]ng vi[eệ]t)$", re.IGNORECASE)
# The tree's parts, beside SPEC.md; a row of the index that links one is navigation, not a rule.
PARTS_DIR = "spec"
PART_LINK = re.compile(r"\]\(\s*(?:\./)?" + PARTS_DIR + r"/[^)]+\.md\s*\)")
RELATIVE_LINK = re.compile(r"\]\((?![a-zA-Z][a-zA-Z0-9+.-]*:|/|#)([^)\s]+)")


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
    part = None
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
            if len(match.group(1)) == 1:
                part = heading if LANGUAGE_PART.match(heading) else None
            elif part:
                heading = f"{part} · {heading}"
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


def from_part_folder(rule, part_dir):
    """A link in a part is relative to its folder under spec/; rewrite it relative to the feature folder,
    like SPEC.md's."""
    def feature_relative(match):
        return "](" + os.path.normpath(os.path.join(part_dir, match.group(1))).replace("\\", "/")
    return RELATIVE_LINK.sub(feature_relative, rule)


def rules_of_tree(index_text, parts):
    """rules_by_section over SPEC.md and its parts - (path under spec/, text) - as one requirement:
    sections pooled by heading, part links made feature-relative, the index's links to its parts dropped."""
    order, sections = [], {}
    for part_path, text in [(None, index_text)] + list(parts):
        file_order, file_rules = rules_by_section(text)
        part_dir = os.path.dirname(os.path.join(PARTS_DIR, part_path)) if part_path else None
        for heading in file_order:
            rules = [from_part_folder(r, part_dir) if part_dir else r
                     for r in file_rules[heading] if not PART_LINK.search(r)]
            if heading not in sections:
                order.append(heading)
                sections[heading] = []
            sections[heading].extend(rules)
    return order, sections


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


def spec_of(path):
    """The SPEC.md a changed file belongs to: itself, or the index of the part it is; None otherwise."""
    if path == "SPEC.md" or path.endswith("/SPEC.md"):
        return path
    if not path.endswith(".md"):
        return None
    pieces = path.split("/")[:-1]
    # The nearest folder called spec above the file; its parent holds the SPEC.md.
    for at in range(len(pieces) - 1, -1, -1):
        if pieces[at] == PARTS_DIR:
            return "/".join(pieces[:at] + ["SPEC.md"])
    return None


def changed_specs(base):
    tracked = git("diff", "--name-only", base, "--").split("\n")
    untracked = git("ls-files", "--others", "--exclude-standard").split("\n")
    # A folder called spec holds a requirement's parts only beside a SPEC.md, now or at the base; a skill
    # named spec is not one.
    def is_spec(path):
        return os.path.isfile(path) or subprocess.run(
            ["git", "cat-file", "-e", f"{base}:{path}"], capture_output=True).returncode == 0
    return sorted({s for s in map(spec_of, tracked + untracked) if s and is_spec(s)})


def parts_folder(spec_path):
    return "/".join(p for p in (os.path.dirname(spec_path), PARTS_DIR) if p)


def base_tree(base, spec_path):
    """SPEC.md and its part files as they were at the base."""
    folder = parts_folder(spec_path)
    listed = git("ls-tree", "-r", "--name-only", f"{base}:{folder}", check=False).split("\n")
    parts = [(n, git("show", f"{base}:{folder}/{n}", check=False)) for n in sorted(listed) if n.endswith(".md")]
    return rules_of_tree(git("show", f"{base}:{spec_path}", check=False), parts)


def working_parts(spec_path):
    """(path under spec/, text) of every part file in the working copy, subfolders included, sorted."""
    folder = parts_folder(spec_path)
    names = sorted(os.path.relpath(p, folder).replace("\\", "/")
                   for p in glob.glob(os.path.join(folder, "**", "*.md"), recursive=True))
    return [(n, read_working(f"{folder}/{n}")) for n in names]


def working_tree(spec_path):
    """SPEC.md and its part files in the working copy, uncommitted edits included."""
    return rules_of_tree(read_working(spec_path), working_parts(spec_path))


def write_view(spec_path, base, note):
    """--view: the review page of one requirement, written to .paper/spec-view/<slug>.html (spec_view.py)."""
    import spec_view
    folder = os.path.dirname(spec_path) or "."
    old_order, old = base_tree(base, spec_path) if base else working_tree(spec_path)
    new_order, new = working_tree(spec_path)
    page, counts = spec_view.build(folder, read_working(spec_path), working_parts(spec_path),
                                   old_order, old, new_order, new, note)
    slug = os.path.basename(os.path.abspath(folder))
    target = os.path.join(".paper", "spec-view", slug + ".html")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with io.open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write(page)
    print(f"wrote {target.replace(os.sep, '/')} - {counts[0]} added, {counts[1]} changed, {counts[2]} removed")


def read_working(path):
    return io.open(path, encoding="utf-8-sig").read() if os.path.isfile(path) else ""


def main(argv):
    preview = "--preview" in argv
    view = "--view" in argv
    names = [a for a in argv if not a.startswith("--")]
    # --view takes a feature folder, read from where the command was typed; any other name is the base.
    started_in = os.getcwd()
    feature = None
    if view:
        # A folder only when it is one: a base such as origin/main or release/main has a slash too.
        folders = [a for a in names if os.path.isdir(os.path.join(started_in, a))]
        names = [a for a in names if a not in folders]
        if folders:
            feature = os.path.abspath(os.path.join(started_in, folders[0]))
            if not os.path.isfile(os.path.join(feature, "SPEC.md")):
                # F32: never a blank page that reads like a requirement with no rules.
                print(f"spec-changes: {folders[0]} has no SPEC.md - no page written.", file=sys.stderr)
                return 2
    base_ref = names[0] if names else "origin/main"

    os.chdir(PROJECT_ROOT)
    try:
        root = git("rev-parse", "--show-toplevel").strip()
        os.chdir(root)
        base = git("merge-base", base_ref, "HEAD").strip()
    except RuntimeError as error:
        if feature:
            # One named feature is still worth a page with no base: shown as it is, nothing marked.
            write_view(os.path.relpath(os.path.join(feature, "SPEC.md")).replace("\\", "/"), None,
                       f"no base '{base_ref}' - nothing marked")
            return 0
        print(f"spec-changes: cannot compare with '{base_ref}' - {error}. "
              "Fetch it (git fetch origin main) or name the base as the last argument.", file=sys.stderr)
        return 2

    if view:
        branch = git("rev-parse", "--abbrev-ref", "HEAD").strip()
        note = f"branch {branch} against {base_ref} ({base[:9]})"
        specs = [os.path.relpath(os.path.join(feature, "SPEC.md")).replace("\\", "/")] if feature else changed_specs(base)
        if not specs:
            print(f"No SPEC.md changed on this branch since {base_ref} - no page to build.")
            return 0
        for spec_path in specs:
            write_view(spec_path, base, note)
        return 0

    branch = git("rev-parse", "--abbrev-ref", "HEAD").strip()
    history = [l for l in git("log", "--reverse", "--format=%cs|%s", f"{base}..HEAD").split("\n") if l]
    started, _, subject = (history[0] if history else "").partition("|")
    started = started or datetime.date.today().isoformat()
    subject = subject or branch
    short = re.sub(r"[^a-z0-9]+", "-", branch.split("/")[-1].lower()).strip("-") or "branch"
    file_name = f"{started}-{short}.md"

    summary = []
    for path in changed_specs(base):
        old_order, old = base_tree(base, path)
        new_order, new = working_tree(path)
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
                          "*Written by the spec-changes script from SPEC.md and its parts - never edit it by hand.*", ""]
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
