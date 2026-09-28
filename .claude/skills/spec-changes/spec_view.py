"""The review page of one requirement: a self-contained HTML page built from SPEC.md and its parts.

    python .claude/skills/spec-changes/spec_changes.py --view [docs/features/<slug>] [base]

The text stays the only source (ADR-0025, point 6); the page is a way to look at it, written to
.paper/spec-view/<slug>.html, outside git, overwritten on every build. It is marked with the comparison the
record uses - compare() and now_and_before() of spec_changes.py - so the page and the record never disagree:
an added rule green, a changed one with its new words bold and the old rule struck under it, a removed one struck
at the end of its section.

Markdown is rendered here, in Python, for the small set a requirement uses (headings, bullets, numbered steps,
tables, quotes, fenced blocks, bold, code, links, images): no network and no package, and every mark is testable.
Drawings are embedded as data URIs so the page travels as one file; a mermaid block stays text inside
<pre class="mermaid">, drawn by mermaid from the CDN when the browser is online and readable as text when not.
"""

import base64
import html
import os
import re

import spec_changes as sc

IMAGE_TYPES = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif",
               ".svg": "image/svg+xml", ".webp": "image/webp"}
F_CELL = re.compile(r"^F\d+$")
MERMAID = "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs"


# ------------------------------------------------------------------ inline markdown


def inline(text, link_target=None):
    """Escape the text, then turn `code`, **bold**, ~~struck~~ and [links](...) into HTML. link_target maps a
    link's target to the one the page uses (a part becomes an anchor); None keeps it."""
    parts = re.split(r"(`[^`]+`)", text)
    out = []
    for piece in parts:
        if len(piece) > 1 and piece.startswith("`") and piece.endswith("`"):
            out.append("<code>" + html.escape(piece[1:-1]) + "</code>")
            continue
        s = html.escape(piece, quote=False)
        s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
        s = re.sub(r"~~(.+?)~~", r"<del>\1</del>", s)
        s = re.sub(r"(?<![*\w])\*(?!\s)(.+?)(?<!\s)\*(?![*\w])", r"<em>\1</em>", s)

        def link(match):
            target = html.unescape(match.group(2))
            if link_target:
                target = link_target(target)
            return f'<a href="{html.escape(target)}">{match.group(1)}</a>'
        s = re.sub(r"(?<!!)\[([^\]]+)\]\(([^)\s]+)\)", link, s)
        out.append(s)
    return "".join(out)


# ------------------------------------------------------------------ one file of the requirement


class Page:
    """What the files of one requirement share while they are rendered: the marks, the part anchors, the images."""

    def __init__(self, feature_dir, added, changed, removed, part_ids, f_parts):
        self.feature_dir = feature_dir
        self.added = set(added)            # rule strings, feature-relative, as rules_of_tree gives them
        self.changed = dict(changed)       # new rule -> old rule
        self.removed = removed             # heading -> [old rules]
        self.part_ids = part_ids           # "spec/x.md" -> "part-x"
        self.f_parts = f_parts             # "F3" -> "part-x"
        self.shown_removed = set()

    def anchor(self, target):
        clean = target.split("#")[0]
        if clean.startswith("./"):
            clean = clean[2:]
        if clean in ("SPEC.md", "../SPEC.md"):
            return "#part-index"
        return "#" + self.part_ids[clean] if clean in self.part_ids else target

    def image(self, alt, src, file_dir):
        path = os.path.normpath(os.path.join(file_dir, src))
        kind = IMAGE_TYPES.get(os.path.splitext(path)[1].lower())
        if kind and os.path.isfile(path):
            data = base64.b64encode(open(path, "rb").read()).decode("ascii")
            src = f"data:{kind};base64,{data}"
            return f'<figure><img src="{src}" alt="{html.escape(alt)}"><figcaption>{html.escape(alt)}</figcaption></figure>'
        return f'<figure class="missing"><figcaption>{html.escape(alt)} - {html.escape(src)} not found</figcaption></figure>'


def rule_key(raw, part_dir):
    """The rule as rules_of_tree keys it: whitespace folded, a part's links made feature-relative."""
    key = re.sub(r"\s+", " ", raw)
    return sc.from_part_folder(key, part_dir) if part_dir else key


def mark(page, key, body_html, tag, attrs=""):
    """One rule element, marked when the branch added or changed it."""
    if key in page.added:
        return f'<{tag} class="rule added"{attrs}>{body_html}</{tag}>'
    if key in page.changed:
        now, before = sc.now_and_before(page.changed[key], key)
        now = now.replace("- *Now:* ", "", 1).replace("](../", "](")
        before = before.replace("  - *Before:* ", "", 1).replace("](../", "](")
        if tag == "tr":
            # A row has no room for two versions: the next row, across the table, carries them.
            columns = body_html.count("<td")
            return (f'<tr class="rule changed"{attrs}>{body_html}</tr>'
                    f'<tr class="before-row"><td colspan="{columns}"><div>now: {inline(now)}</div>'
                    f'<div class="before">before: {inline(before)}</div></td></tr>')
        return (f'<{tag} class="rule changed"{attrs}>{inline(now)}'
                f'<div class="before">before: {inline(before)}</div></{tag}>')
    return f"<{tag}{attrs}>{body_html}</{tag}>"


def removed_block(page, heading):
    rules = page.removed.get(heading, [])
    if not rules or heading in page.shown_removed:
        return ""
    page.shown_removed.add(heading)
    items = "".join(f'<li class="rule removed"><del>{inline(sc.plain(sc.as_sentence(r)))}</del></li>' for r in rules)
    return f'<ul class="removed-list">{items}</ul>'


def render_file(page, text, part_dir, file_dir, part_id):
    """One markdown file of the requirement as HTML, its rules marked. part_dir is None for SPEC.md."""
    out = []
    lines = text.replace("\r\n", "\n").split("\n")
    heading = "(before the first heading)"
    language = None
    i = 0

    def flush_removed():
        block = removed_block(page, heading)
        if block:
            out.append(block)

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        if stripped.startswith("```") or stripped.startswith("~~~"):
            fence, lang = stripped[:3], stripped[3:].strip().lower()
            body = []
            i += 1
            while i < len(lines) and not lines[i].strip().startswith(fence):
                body.append(lines[i])
                i += 1
            i += 1
            code = html.escape("\n".join(body))
            out.append(f'<pre class="mermaid">{code}</pre>' if lang == "mermaid" else f"<pre><code>{code}</code></pre>")
            continue
        match = sc.HEADING.match(line)
        if match:
            flush_removed()
            level, title = len(match.group(1)), match.group(2)
            heading = title
            if level == 1:
                language = title if sc.LANGUAGE_PART.match(title) else None
            elif language:
                heading = f"{language} · {title}"
            anchor = part_id + "-" + re.sub(r"[^\w]+", "-", title.lower()).strip("-")
            out.append(f'<h{min(level + 1, 6)} id="{html.escape(anchor)}">{inline(title, page.anchor)}</h{min(level + 1, 6)}>')
            i += 1
            continue
        if stripped.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i].strip())
                i += 1
            out.append(render_table(page, rows, part_dir))
            continue
        if stripped.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quote.append(lines[i].strip()[1:].strip())
                i += 1
            out.append("<blockquote>" + inline(" ".join(quote), page.anchor) + "</blockquote>")
            continue
        image = re.match(r"^!\[(.*?)\]\((.*?)\)\s*$", stripped)
        if image:
            key = rule_key(stripped, part_dir)
            out.append(mark(page, key, page.image(image.group(1), image.group(2), file_dir), "div"))
            i += 1
            continue
        if sc.RULE_START.match(stripped) and not line.startswith("  "):
            ordered = bool(re.match(r"^\d+[.)] ", stripped))
            items = []
            while i < len(lines):
                item_line = lines[i]
                item = item_line.strip()
                if not item or item.startswith("|") or item.startswith("```") or sc.HEADING.match(item_line):
                    break
                starts = sc.RULE_START.match(item) and not item_line.startswith("  ")
                if starts and item.startswith("!["):
                    break
                if starts:
                    items.append([item])
                elif items:
                    items[-1].append(item)
                else:
                    break
                i += 1
            rendered = []
            for pieces in items:
                raw = " ".join(pieces)
                body = re.sub(r"^(?:[-*+]|\d+[.)]) ", "", raw)
                rendered.append(mark(page, rule_key(raw, part_dir), inline(body, page.anchor), "li"))
            tag = "ol" if ordered else "ul"
            out.append(f"<{tag}>" + "".join(rendered) + f"</{tag}>")
            continue
        if not stripped:
            i += 1
            continue
        paragraph = []
        while i < len(lines) and lines[i].strip() and not sc.HEADING.match(lines[i]) \
                and not sc.RULE_START.match(lines[i].strip()) and not lines[i].strip().startswith((">", "```", "~~~")):
            paragraph.append(lines[i].strip())
            i += 1
        out.append("<p>" + inline(" ".join(paragraph), page.anchor) + "</p>")
    flush_removed()
    return "\n".join(out)


def render_table(page, rows, part_dir):
    """A table; the row above the separator is its header, every other row a rule that can be marked. The code
    cell of an F row links to the part that covers the code."""
    header = None
    body = rows
    if len(rows) > 1 and sc.TABLE_SEPARATOR.match(rows[1]):
        header, body = rows[0], rows[2:]

    def cells(row):
        return [c.strip() for c in row.strip().strip("|").split("|")]
    out = ["<table>"]
    if header:
        out.append("<thead><tr>" + "".join(f"<th>{inline(c, page.anchor)}</th>" for c in cells(header)) + "</tr></thead>")
    out.append("<tbody>")
    for row in body:
        if sc.TABLE_SEPARATOR.match(row):
            continue
        tds = []
        for n, cell in enumerate(cells(row)):
            if n == 0 and F_CELL.match(cell) and cell in page.f_parts:
                tds.append(f'<td><a href="#{page.f_parts[cell]}">{cell}</a></td>')
            else:
                tds.append(f"<td>{inline(cell, page.anchor)}</td>")
        out.append(mark(page, rule_key(row, part_dir), "".join(tds), "tr"))
    out.append("</tbody></table>")
    return "".join(out)


# ------------------------------------------------------------------ the page


def part_id(name):
    return "part-" + re.sub(r"[^\w]+", "-", name[:-3].lower()).strip("-")


def parts_of_index(index_text):
    """From the index's table of parts, in its order: "spec/x.md" -> (the part's name, the F codes its row names)."""
    covered = {}
    for line in index_text.split("\n"):
        link = sc.PART_LINK.search(line)
        if not line.strip().startswith("|") or not link:
            continue
        target = re.search(r"\]\(\s*(?:\./)?(" + sc.PARTS_DIR + r"/[^)\s]+\.md)", line).group(1)
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        name = "" if sc.PART_LINK.search(cells[0]) else cells[0]
        covered.setdefault(target, (name, re.findall(r"\bF\d+\b", cells[-1])))
    return covered


def build(feature_dir, index_text, parts, old_order, old, new_order, new, title_note):
    """The whole page. parts: [(path under spec/, text)]; old/new: rules_of_tree of the base and the working copy."""
    added, changed, removed = [], [], {}
    counts = [0, 0, 0]
    for heading in new_order + [h for h in old_order if h not in new]:
        a, c, r = sc.compare(old.get(heading, []), new.get(heading, []))
        added += a
        changed += [(n, o) for o, n in c]
        if r:
            removed[heading] = r
        counts = [counts[0] + len(a), counts[1] + len(c), counts[2] + len(r)]

    part_ids = {f"{sc.PARTS_DIR}/{name}": part_id(name.replace("/", "-")) for name, _ in parts}
    listed = parts_of_index(index_text)
    f_parts = {code: part_ids[target] for target, (_, codes) in listed.items()
               if target in part_ids for code in codes}
    # The parts in the order of the index's table, then any it does not list.
    order = list(listed)

    def place(part):
        key = f"{sc.PARTS_DIR}/{part[0]}"
        return (order.index(key) if key in order else len(order), part[0])
    parts = sorted(parts, key=place)
    page = Page(feature_dir, added, changed, removed, part_ids, f_parts)

    slug = os.path.basename(os.path.normpath(feature_dir))
    sections, nav = [], []
    files = [(None, "SPEC.md", index_text)] + [(n, f"{sc.PARTS_DIR}/{n}", t) for n, t in parts]
    for name, shown, text in files:
        pid = "part-index" if name is None else part_ids[shown]
        part_dir = None if name is None else os.path.dirname(os.path.join(sc.PARTS_DIR, name))
        file_dir = feature_dir if name is None else os.path.join(feature_dir, os.path.dirname(os.path.join(sc.PARTS_DIR, name)))
        first = re.search(r"(?m)^# +(.+?)\s*$", text)
        named = listed.get(shown, ("", []))[0]
        label = "Index" if name is None else (named or (first.group(1) if first else shown))
        body = render_file(page, text, part_dir, file_dir, pid)
        marks = len(re.findall(r'class="rule (?:added|changed|removed)"', body))
        badge = f' <span class="badge">{marks}</span>' if marks else ""
        nav.append(f'<li><a href="#{pid}">{html.escape(label)}</a>{badge}<div class="file">{html.escape(shown)}</div></li>')
        sections.append(f'<section id="{pid}" class="file-section"><div class="file">{html.escape(shown)}</div>{body}</section>')

    leftover = [h for h in removed if h not in page.shown_removed]
    if leftover:
        blocks = "".join(f"<h3>{inline(h)}</h3>" + removed_block(page, h) for h in leftover)
        sections.append(f'<section id="removed-sections" class="file-section"><h2>Removed sections</h2>{blocks}</section>')

    summary = f"{counts[0]} added, {counts[1]} changed, {counts[2]} removed"
    return TEMPLATE.format(title=html.escape(slug), note=html.escape(title_note), summary=summary,
                           nav="".join(nav), sections="\n".join(sections), mermaid=MERMAID), counts


TEMPLATE = """<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title} - requirement</title>
<style>
:root {{ --bg:#fbfbfa; --fg:#1d1d1b; --muted:#6b6b66; --line:#e3e2dd; --side:#f2f1ec; --add:#e3f4e6; --add-edge:#2f8f46;
  --chg:#fff4d6; --chg-edge:#b7860b; --del:#fbe6e4; --del-edge:#b3402f; --link:#1f5fa8; --code:#efeee9; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#18181a; --fg:#e7e6e1; --muted:#9a9993; --line:#303033; --side:#202023;
  --add:#16301d; --add-edge:#58c178; --chg:#33290f; --chg-edge:#e0b340; --del:#3a1a17; --del-edge:#e0735f; --link:#7fb2ee; --code:#2a2a2e; }} }}
* {{ box-sizing:border-box; }}
body {{ margin:0; background:var(--bg); color:var(--fg); font:15px/1.55 system-ui,-apple-system,"Segoe UI",sans-serif; display:flex; }}
nav {{ position:sticky; top:0; height:100vh; overflow:auto; width:280px; flex:none; background:var(--side); border-right:1px solid var(--line); padding:20px 16px; }}
nav h1 {{ font-size:17px; margin:0 0 4px; }} nav .summary {{ color:var(--muted); font-size:13px; margin-bottom:6px; }}
nav .note {{ color:var(--muted); font-size:12px; margin-bottom:16px; }}
nav ul {{ list-style:none; margin:0; padding:0; }} nav li {{ margin:0 0 10px; }}
nav a {{ color:var(--fg); text-decoration:none; font-weight:600; }} nav a:hover {{ color:var(--link); }}
.file {{ color:var(--muted); font:12px ui-monospace,Consolas,monospace; }}
.badge {{ display:inline-block; min-width:20px; padding:0 6px; border-radius:10px; background:var(--chg-edge); color:#fff; font-size:12px; text-align:center; }}
main {{ flex:1; min-width:0; padding:24px 40px 80px; max-width:1000px; }}
.file-section {{ border-bottom:1px solid var(--line); padding:8px 0 28px; }}
h2 {{ font-size:24px; }} h3 {{ font-size:19px; margin-top:28px; }} h4 {{ font-size:16px; }}
a {{ color:var(--link); }} code {{ background:var(--code); padding:1px 4px; border-radius:4px; font-size:13px; }}
pre {{ background:var(--code); padding:12px; border-radius:6px; overflow:auto; }}
blockquote {{ margin:12px 0; padding:8px 14px; border-left:3px solid var(--chg-edge); background:var(--chg); }}
table {{ border-collapse:collapse; width:100%; margin:12px 0; font-size:14px; }}
th, td {{ border:1px solid var(--line); padding:6px 8px; text-align:left; vertical-align:top; }}
th {{ background:var(--side); }}
li {{ margin:4px 0; }}
.rule.added {{ background:var(--add); box-shadow:inset 3px 0 var(--add-edge); }}
.rule.changed {{ background:var(--chg); box-shadow:inset 3px 0 var(--chg-edge); }}
.rule.removed {{ background:var(--del); box-shadow:inset 3px 0 var(--del-edge); }}
li.rule, div.rule {{ padding:4px 8px; border-radius:4px; }}
.before {{ color:var(--muted); font-size:13px; margin-top:4px; }}
.removed-list {{ list-style:none; padding-left:0; }}
.removed-list::before {{ content:"Removed on this branch"; display:block; color:var(--del-edge); font-size:12px; font-weight:600; margin-bottom:4px; }}
figure {{ margin:12px 0; }} figure img {{ max-width:100%; background:#fff; border:1px solid var(--line); border-radius:4px; }}
figcaption {{ color:var(--muted); font-size:13px; }} figure.missing figcaption {{ color:var(--del-edge); }}
.legend span {{ display:inline-block; padding:0 6px; margin-right:4px; border-radius:3px; font-size:12px; }}
@media (max-width:760px) {{ body {{ display:block; }} nav {{ position:static; width:auto; height:auto; }} main {{ padding:16px; }} }}
</style></head>
<body>
<nav><h1>{title}</h1><div class="summary">{summary}</div><div class="note">{note}</div>
<div class="legend"><span class="rule added">added</span><span class="rule changed">changed</span><span class="rule removed">removed</span></div>
<ul>{nav}</ul></nav>
<main>
{sections}
</main>
<script type="module">
try {{ const m = await import("{mermaid}"); m.default.initialize({{ startOnLoad: false }}); await m.default.run({{ querySelector: "pre.mermaid" }}); }}
catch (e) {{ /* offline: the diagram stays readable as text */ }}
</script>
</body></html>
"""
