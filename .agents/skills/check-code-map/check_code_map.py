"""Check every CODEMAP.md in the Paper solution against the code it describes.

  symbols    a claim naming `SomeType` that no longer exists in the source is
             stale, and nothing else in the repo will catch it - no human reads
             these files routinely, so this check is the review.

  structure  opening lines before the first heading, and no unheaded block so
             long that a session has to load the whole file to find one step.

  budget     a CODEMAP.md past 80 lines is still carrying rules or traps. Listed
             as backlog, not as a failure.

  retired    INSTRUCTION.md was the old state-and-traps layer. It is split into
             CODEMAP.md, docs/progress handovers and comments; one coming back
             is a failure.

The inverse check - a SPEC.md must NOT name code - is .claude/skills/check-spec, which
imports TICKED, EXTERNAL and walk from here so both agree on what a name is.

Exit code 0 when clean, 1 when a map is stale or cannot be entered cheaply.
"""

import io
import os
import re
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")

# The script lives at <repo>/.claude/skills/check-code-map/, so every path below is
# relative to the repo root whatever directory it was started from.
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
# The whole repository. This was once one project folder - the one the script was first written for - and
# in any other repository it found 0 maps and reported CLEAN: a silent pass nobody would ever notice.
SOLUTION = "."

SKIP_DIRS = {
    ".git", ".vs", ".idea", "bin", "obj", "graphify-out", ".codegraph", "artifacts", "packages",
    "node_modules", "TestResults", ".superpowers",
}
# .lsp and .scr: script files a host loads by name, so a map may name a routine defined only there.
SOURCE_EXT = (".cs", ".xaml", ".csproj", ".props", ".targets", ".json", ".ps1", ".sln", ".slnx",
              ".resx", ".xml", ".py", ".csx", ".fsx", ".txt", ".mjs", ".lsp", ".scr")

# Identifiers are indexed from the solution AND from .claude, because a map may name a hook
# script or a skill. Maps themselves are only searched for under the solution.
INDEX_ROOTS = (SOLUTION,)

# Names that legitimately appear in a CODEMAP.md without existing in our source: the BCL,
# WPF and MSBuild. Add to this list only for a name owned by something outside the repo -
# never to silence a map that has drifted. A host application's API names are not here: they
# come from external/*.txt beside this script (see load_external_names).
EXTERNAL = {
    "DllNotFoundException", "TypeInitializationException", "MissingMethodException",
    "InvalidOperationException", "ArgumentException", "TypeLoadException",
    "InvalidCastException", "FileLoadException", "AppDomain", "AssemblyLoadContext",
    "CancellationToken", "TaskCompletionSource", "PrintWindow",
    "FormattedText", "SystemColors", "AutomationPeer", "RenderTargetBitmap", "DataTemplate",
    "DependencyProperty", "ControlTemplate", "ResourceDictionary", "ObservableCollection",
    "ScrollViewer", "ScrollBar", "DataGrid", "TreeView", "TreeViewItem", "ComboBox", "ContextMenu",
    "MenuItem", "UserControl", "WebView2",
    "ProjectReference", "PackageReference", "TargetFramework",
}


def load_external_names(folder):
    """Names from every external/*.txt: one per line, '#' starts a comment.

    The script is the kit's and a hand edit to it is refused at the next setup, so the
    names a host or a project adds live in files beside it: a host pack ships
    external/<host>.txt, a project adds its own file next to it.
    """
    names = set()
    if not os.path.isdir(folder):
        return names
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".txt"):
            continue
        with open(os.path.join(folder, name), encoding="utf-8-sig") as fh:
            for line in fh:
                word = line.split("#", 1)[0].strip()
                if word:
                    names.add(word)
    return names


EXTERNAL |= load_external_names(os.path.join(os.path.dirname(os.path.abspath(__file__)), "external"))

# A note may name something on purpose *because* it is gone - "the services are gone:
# OpenAiService" is a correct claim, not a stale one. The sentence always says so, so a
# line carrying one of these words (or two lines either side of one) is not reported.
ABSENCE = (
    "gone", "removed", "deleted", "no longer", "does not exist", "never existed",
    "obsolete", "renamed", "used to", "would be", "dropped", "replaced", "reverted",
    "leftover", "do not re-add", "don't re-add", "should not come back",
    "not in source", "family parameter",
    "đã xóa", "đã xoá", "không còn", "bỏ đi", "thay bằng",
)

# `ForFront` / `ForPlan` written as the second half of a pair is the part that differs,
# not a name of its own - accept it when some real identifier ends with it.
ALTERNATION = re.compile(r"`([A-Za-z_][A-Za-z0-9_]*)`\s*/\s*`([A-Za-z_][A-Za-z0-9_]*)`")

IDENT = re.compile(rb"\b[A-Za-z_][A-Za-z0-9_]*\b")
TICKED = re.compile(r"`([^`\n]{2,80})`")
CAMEL = re.compile(r"^[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+$")
HEADING = re.compile(r"^(#{1,3}) +(.*)$")

# What a flagged block costs to read, not how long it looks: 250 lines is around 10 KB.
MAX_UNHEADED_BLOCK = 250
STRUCTURE_FROM_LINES = 300

# A map needs few lines, so a file much past this still carries something that belongs in
# a comment, a project CLAUDE.md or a SPEC.md. Backlog, not a fault.
MAP_BUDGET_LINES = 80


def walk(root):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        yield dirpath, filenames


def build_source_index():
    known, count = set(), 0
    for root in INDEX_ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, filenames in walk(root):
            # A project folder is named like an assembly (Orders.Export): a map names its parts too.
            base = os.path.basename(dirpath)
            known.add(base.encode("ascii", "ignore"))
            known.update(part.encode("ascii", "ignore") for part in base.split("."))
            for name in filenames:
                # A map names a file or folder as often as a type, and the file name is not
                # always a symbol inside it.
                stem = os.path.splitext(name)[0]
                known.add(stem.encode("ascii", "ignore"))
                known.update(part.encode("ascii", "ignore") for part in stem.split("."))
                if name.endswith(SOURCE_EXT):
                    count += 1
                    try:
                        with open(os.path.join(dirpath, name), "rb") as fh:
                            known.update(IDENT.findall(fh.read()))
                    except OSError:
                        pass
    return {b.decode("ascii", "ignore") for b in known}, count


def find_maps():
    out = []
    for dirpath, filenames in walk(SOLUTION):
        if "CODEMAP.md" in filenames:
            out.append(os.path.join(dirpath, "CODEMAP.md").replace("\\", "/"))
    return sorted(out)


def symbols_in(text, known=()):
    found = {}
    lines = text.split("\n")
    for lineno, line in enumerate(lines, 1):
        # "both are gone" usually lands a line or two after the names it refers to.
        window = " ".join(lines[max(0, lineno - 3):lineno + 2]).lower()
        if any(word in window for word in ABSENCE):
            continue
        tails = {b for _, b in ALTERNATION.findall(line) if any(k.endswith(b) for k in known)}
        for raw in TICKED.findall(line):
            head = re.split(r"[.(<\[\s/]", raw.strip())[0]
            if CAMEL.match(head) and head not in EXTERNAL and head not in tails:
                found.setdefault(head, lineno)
    return found


def structure_of(text):
    lines = text.split("\n")
    heads = [i for i, l in enumerate(lines) if HEADING.match(l)]
    positions = heads or [0]
    second = heads[1] if len(heads) > 1 else len(lines)
    entry = sum(1 for l in lines[1:second] if l.strip() and not HEADING.match(l))
    gap = max([b - a for a, b in zip(positions, positions[1:])] + [len(lines) - positions[-1]])
    return len(lines), entry, gap


def main():
    started = time.time()
    os.chdir(REPO)
    known, source_count = build_source_index()
    maps = find_maps()
    print(f"{source_count} source files -> {len(known)} identifiers;  {len(maps)} CODEMAP.md\n")

    leftovers = sorted(
        os.path.join(d, f).replace("\\", "/")
        for d, fs in walk(SOLUTION) for f in fs if re.fullmatch(r"INSTRUCTIONS?\.md", f))
    if leftovers:
        print("INSTRUCTION — a retired layer is back; split it into CODEMAP.md (/code-map), a docs/progress "
              "handover, and comments (/comment-code):\n")
        for p in leftovers:
            print(f"  {p}")
        print()

    stale, weak, lengths = [], [], {}
    total = 0
    for path in maps:
        text = io.open(path, encoding="utf-8-sig").read()
        found = symbols_in(text, known)
        total += len(found)
        missing = sorted((line, name) for name, line in found.items() if name not in known)
        if missing:
            stale.append((path, missing))

        n, entry, gap = structure_of(text)
        lengths[path] = n
        problems = []
        if entry < 2:
            problems.append("no opening summary (two lines at least) before the first section")
        if n >= STRUCTURE_FROM_LINES and gap > MAX_UNHEADED_BLOCK:
            problems.append(f"{gap}-line block with no heading")
        if problems:
            weak.append((path, n, problems))

    if stale:
        print("STALE — a claim names a symbol that is not in the source:\n")
        for path, missing in stale:
            print(f"  {path}")
            for line, name in missing:
                print(f"      line {line:>5}  {name}")
        print()

    over = sorted(((n, p) for p, n in lengths.items() if n > MAP_BUDGET_LINES), reverse=True)
    if over:
        print(f"MORE THAN A MAP — {len(over)} past the {MAP_BUDGET_LINES}-line budget. Traps go to "
              f"comments (/comment-code), project-wide silent rules to that project's CLAUDE.md:")
        for n, p in over:
            print(f"  {n:>5} lines  {p}")
        print()

    if weak:
        print("MAP — a map a session cannot enter cheaply:\n")
        for path, n, problems in weak:
            print(f"  {path}  ({n} lines)")
            for p in problems:
                print(f"      {p}")
        print()

    print(f"{len(maps)} CODEMAP.md, {total} symbols named, "
          f"{sum(len(m) for _, m in stale)} stale, {len(weak)} weak, {len(over)} over budget, "
          f"{len(leftovers)} INSTRUCTION left — {time.time() - started:.1f}s")
    return 1 if (stale or weak or leftovers) else 0


if __name__ == "__main__":
    sys.exit(main())
