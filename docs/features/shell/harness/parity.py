#!/usr/bin/env python3
"""Parity check for the folders-by-domain refactor (plan 2026-09-26-thu-muc-theo-domain-plan.md, T2).

Compares every line of code in src/ and tests/ (*.cs, *.xaml) at the base commit with the working tree, as a multiset:
using directives and namespace declarations are dropped, and the base side is rewritten through the moves (a type's
old namespace -> the namespace it is declared in now) and the renames of the plan's port table. What is left must be
identical, apart from the lines in ALLOWED, each with its reason. Exit 0 when identical, 1 when not.

    python3 docs/features/shell/harness/parity.py [base-commit]
"""
import collections
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[4]
BASE = sys.argv[1] if len(sys.argv) > 1 else "1c9d5c8"
ROOTS = ("src/", "tests/")
EXTENSIONS = (".cs", ".xaml")

# The plan's port table: old type name -> new type name.
TYPE_RENAMES = {
    "IScreenSourcePort": "IScreenSource",
    "IWindowCatalogPort": "IWindowCatalog",
    "IMonitorCatalogPort": "IMonitorCatalog",
    "ISettingsStorePort": "ISettingsStore",
    "IHotkeyPort": "IHotkeys",
    "IAutostartPort": "IAutostart",
    "ISingleInstancePort": "ISingleInstance",
    "IClockPort": "IClock",
    "IDelayPort": "IDelay",
    "ILogPort": "ILog",
    "INotificationPort": "INotifications",
    "IClipboardPort": "IClipboard",
    "IFileStorePort": "IFileStore",
    "IImageCodecPort": "IImageCodec",
    "FakeDelayPort": "FakeDelay",
    "FakeHotkeyPort": "FakeHotkeys",
    "FakeAutostartPort": "FakeAutostart",
}

# Files the refactor adds or rewrites on purpose; compared by review, not by this script.
NEW_FILES = {
    "tests/Paper.ScreenWizzard.UnitTests/Architecture/FolderShapeTests.cs",
}

# (base line after the rewrite, working line) pairs that are allowed to differ, and why.
ALLOWED = [
    # Tests that read source folders by path: the folders moved.
    ('var views = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Views", "Capture");',
     'var views = Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Capture", "Views");'),
    # ViewModels/Editor/ held Commands/ as well; the editor's commands now sit beside it, so the scan names them.
    ('var files = new[] { "Views", "ViewModels" }', 'var files = new[] { "Views", "ViewModels", "Commands" }'),
    ('.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder, "Editor"), "*.cs", SearchOption.AllDirectories))',
     '.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, "Editor", folder), "*.cs", SearchOption.AllDirectories))'),
    ('Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Views", "Editor"), "*.xaml", SearchOption.AllDirectories);',
     'Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Paper.ScreenWizzard.Presentation", "Editor", "Views"), "*.xaml", SearchOption.AllDirectories);'),
    # A comment that names the moved namespaces.
    ("// The namespace is not ...UiTests.Editor on purpose: a namespace of that name would hide the Presentation.Views.Editor and",
     "// The namespace is not ...UiTests.Editor on purpose: a namespace of that name would hide the short name of the Presentation.Editor"),
    ("// ViewModels.Editor namespaces' short names, the same trap the capture lane met with FlaUI's Capture class.",
     "// namespace (its Views and ViewModels), the same trap the capture lane met with FlaUI's Capture class."),
    # Converters that two domains use moved to Shared/Views; the XAML that names them gains a prefix for it.
    ('xmlns:local="clr-namespace:Paper.ScreenWizzard.Presentation.Capture.Views"', 'xmlns:shared="clr-namespace:Paper.ScreenWizzard.Presentation.Shared.Views"'),
    ('<local:PixelImageConverter x:Key="PixelImage" />', '<shared:PixelImageConverter x:Key="PixelImage" />'),
    ('xmlns:shell="clr-namespace:Paper.ScreenWizzard.Presentation.Shell.Views"', 'xmlns:shared="clr-namespace:Paper.ScreenWizzard.Presentation.Shared.Views"'),
    ('<shell:ChoiceConverter x:Key="Choice" />', '<shared:ChoiceConverter x:Key="Choice" />'),
    ('<local:ChoiceConverter x:Key="Choice" />', '<shared:ChoiceConverter x:Key="Choice" />'),
    (None, 'xmlns:shared="clr-namespace:Paper.ScreenWizzard.Presentation.Shared.Views"'),
    # The entry host's string dictionaries left Startup/ with the rest of it.
    ('Source = new Uri($"pack://application:,,,/Paper.ScreenWizzard;component/Startup/AppStrings.{code}.xaml"),',
     'Source = new Uri($"pack://application:,,,/Paper.ScreenWizzard;component/AppStrings.{code}.xaml"),'),
]

USING = re.compile(r"^using\s+(static\s+)?(\w+\s*=\s*)?[\w.]+\s*;$")
NAMESPACE = re.compile(r"^namespace\s+[\w.]+\s*;$")
TOP_LEVEL_TYPE = re.compile(
    r"^(?:public|internal)(?:\s+(?:static|sealed|abstract|partial|readonly|unsafe|file))*\s+"
    r"(?:class|record\s+struct|record|struct|enum|interface)\s+(\w+)", re.M)
NAMESPACE_DECL = re.compile(r"^namespace\s+([\w.]+)\s*;", re.M)


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True, text=True).stdout


def base_files():
    names = [n for n in git("ls-tree", "-r", "--name-only", BASE).splitlines()
             if n.startswith(ROOTS) and n.endswith(EXTENSIONS)]
    return {n: git("show", f"{BASE}:{n}") for n in names}


def work_files():
    files = {}
    for root in ROOTS:
        for path in (ROOT / root).rglob("*"):
            rel = path.relative_to(ROOT).as_posix()
            if path.is_file() and rel.endswith(EXTENSIONS) and not re.search(r"/(bin|obj)/", rel):
                files[rel] = path.read_text(encoding="utf-8")
    return files


def declarations(files):
    """type name -> namespace, for top-level types of src/ (tests keep their namespaces)."""
    found = {}
    for name, text in files.items():
        if not name.startswith("src/") or not name.endswith(".cs"):
            continue
        ns = NAMESPACE_DECL.search(text)
        if ns:
            for match in TOP_LEVEL_TYPE.finditer(text):
                found.setdefault(match.group(1), ns.group(1))
    return found


def short(namespace):
    return namespace[len("Paper.ScreenWizzard."):]


def rewrite_rules(base, work):
    old, new = declarations(base), declarations(work)
    rules = []
    namespace_votes = collections.defaultdict(collections.Counter)
    for type_name, old_ns in old.items():
        new_name = TYPE_RENAMES.get(type_name, type_name)
        new_ns = new.get(new_name) or new.get(type_name)
        if new_name not in new:
            new_name = type_name
        if new_ns is None:
            continue
        namespace_votes[old_ns][new_ns] += 1
        if (old_ns, type_name) != (new_ns, new_name):
            rules.append((re.compile(rf"\b{re.escape(old_ns)}\.{type_name}\b"), f"{new_ns}.{new_name}"))
            # The same name qualified from inside Paper.ScreenWizzard ("UseCases.Editor.Models.X").
            rules.append((re.compile(rf"(?<![\w.]){re.escape(short(old_ns))}\.{type_name}\b"), f"{short(new_ns)}.{new_name}"))
    # A rename counts once it has happened: the old name is gone from the working tree.
    work_text = "\n".join(text for name, text in work.items() if name not in NEW_FILES)
    for type_name, new_name in TYPE_RENAMES.items():
        if not re.search(rf"\b{type_name}\b", work_text):
            rules.append((re.compile(rf"\b{type_name}\b"), new_name))
    # A namespace named on its own (clr-namespace in XAML): where most of its types went. Longest first.
    for old_ns in sorted(namespace_votes, key=len, reverse=True):
        target = namespace_votes[old_ns].most_common(1)[0][0]
        if target != old_ns:
            rules.append((re.compile(rf"\b{re.escape(old_ns)}\b(?!\.\w)"), target))
    return rules


def normalise(text, rules=()):
    lines = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line or USING.match(line) or NAMESPACE.match(line):
            continue
        for pattern, replacement in rules:
            line = pattern.sub(replacement, line)
        lines.append(line)
    return lines


def main():
    base, work = base_files(), work_files()
    rules = rewrite_rules(base, work)
    before = collections.Counter(l for n, t in base.items() for l in normalise(t, rules))
    after = collections.Counter(l for n, t in work.items() if n not in NEW_FILES for l in normalise(t))
    only_before, only_after = before - after, after - before
    allowed = 0
    for old_line, new_line in ALLOWED:
        if old_line is None:  # a line only added
            while only_after[new_line]:
                only_after[new_line] -= 1
                allowed += 1
            continue
        while only_before[old_line] and only_after[new_line]:
            only_before[old_line] -= 1
            only_after[new_line] -= 1
            allowed += 1
    only_before, only_after = +only_before, +only_after
    moved = sum(1 for n in work if n not in base) + sum(1 for n in base if n not in work)
    print(f"parity: base {BASE}, {len(base)} files before, {len(work)} now ({moved} paths differ), "
          f"{sum(before.values())} code lines compared, {allowed} allowed differences")
    for line, count in sorted(only_before.items()):
        print(f"  - only before (x{count}): {line}")
    for line, count in sorted(only_after.items()):
        print(f"  + only after  (x{count}): {line}")
    differences = sum(only_before.values()) + sum(only_after.values())
    print(f"parity: {differences} difference(s)")
    return 0 if differences == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
