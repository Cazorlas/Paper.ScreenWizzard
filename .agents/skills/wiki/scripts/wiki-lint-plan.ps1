# The wiki skill's lint (plan 2026-10-03-llm-wiki, tables D.1-D.5; ADR-0036): links, page names, the index
# against wiki/, the log, orphan pages, secrets and sources from read-only repositories, over texts already
# read. Free: no model, no file system, no git - wiki.ps1 reads the files and the clones' HEAD.
# pure; dot-sourced; declares no param(). The caller dot-sources hooks/memory-nag-plan.ps1 first
# (Get-PaperSecretHits). Paths are relative to the wiki's root, separated by '/'.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperWikiLogOps = @('init', 'ingest', 'query', 'promote', 'lint')
$script:PaperWikiRootFiles = @('AGENTS.md', 'CLAUDE.md', 'index.md', 'log.md')
$script:PaperWikiPageName = '^[a-z0-9]+(-[a-z0-9]+)*\.md$'
$script:PaperWikiFence = '^\s{0,3}(```|~~~)'
$script:PaperWikiCodeSpan = '(`+)(.+?)\1'
$script:PaperWikiLinkPattern = '\[\[([^\[\]\r\n]*)\]\]'
$script:PaperWikiLogLine = '^## \[(\d{4}-\d{2}-\d{2})\] (init|ingest|query|promote|lint) \| (\S.*)$'
$script:PaperWikiIndexLine = '^\s*[-*+]\s+!?\[\['

function Compare-PaperWikiText([string] $A, [string] $B) { return [string]::Compare($A, $B, [StringComparison]::OrdinalIgnoreCase) }

function Get-PaperWikiTextLines([AllowNull()][string] $Text) {
    <#
    Each line (Number from 1, CRLF as LF) outside fenced code: Raw as written, Plain with code spans blanked.
    The fence lines themselves and every line between them are left out.
    #>
    $out = New-Object System.Collections.Generic.List[object]
    $inCode = $false
    $n = 0
    foreach ($line in ([string] $Text).Replace("`r", '').Split("`n")) {
        $n++
        if ($line -match $script:PaperWikiFence) { $inCode = -not $inCode; continue }
        if ($inCode) { continue }
        $plain = [regex]::Replace($line, $script:PaperWikiCodeSpan, { param($m) ' ' * $m.Length })
        $out.Add([pscustomobject]@{ Number = $n; Raw = $line; Plain = $plain })
    }
    return $out.ToArray()
}

function ConvertTo-PaperWikiTarget([string] $Inner) {
    $t = $Inner
    $bar = $t.IndexOf('|')
    if ($bar -ge 0) { $t = $t.Substring(0, $bar) }
    $hash = $t.IndexOf('#')
    if ($hash -ge 0) { $t = $t.Substring(0, $hash) }
    $t = $t.Trim().Replace('\', '/')
    while ($t.StartsWith('./') -or $t.StartsWith('/')) { $t = if ($t.StartsWith('./')) { $t.Substring(2) } else { $t.Substring(1) } }
    return $t
}

function Get-PaperWikiLinks([AllowNull()][string] $Text) {
    <# Every [[...]] outside code: Line, Inner as written, Target (no alias, no heading, '/', no leading ./ or /). An empty Target is no link. #>
    $out = New-Object System.Collections.Generic.List[object]
    foreach ($l in @(Get-PaperWikiTextLines $Text)) {
        foreach ($m in [regex]::Matches($l.Plain, $script:PaperWikiLinkPattern)) {
            $inner = $m.Groups[1].Value
            $target = ConvertTo-PaperWikiTarget $inner
            if ($target -eq '') { continue }
            $out.Add([pscustomobject]@{ Line = $l.Number; Inner = $inner; Target = $target })
        }
    }
    return $out.ToArray()
}

function Sort-PaperWikiStrings([string[]] $Items) {
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($i in @($Items)) { $list.Add($i) }
    $list.Sort([StringComparer]::OrdinalIgnoreCase)
    return $list.ToArray()
}

function Resolve-PaperWikiLink([string] $Target, [AllowNull()][AllowEmptyCollection()][string[]] $Files) {
    <# The files a target names (plan D.2): the file's path without .md equals it, or ends with '/' + it; case ignored. #>
    $t = $Target
    $mdOnly = $false
    if ($t.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) { $t = $t.Substring(0, $t.Length - 3); $mdOnly = $true }
    if ($t -eq '') { return @() }
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($f in @($Files | Where-Object { $_ })) {
        $isMd = $f.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)
        if ($mdOnly -and -not $isMd) { continue }
        $key = if ($isMd) { $f.Substring(0, $f.Length - 3) } else { $f }
        if ($key.Equals($t, [StringComparison]::OrdinalIgnoreCase) -or $key.EndsWith('/' + $t, [StringComparison]::OrdinalIgnoreCase)) { $hits.Add($f) }
    }
    return @(Sort-PaperWikiStrings $hits.ToArray())
}

function Test-PaperWikiLogLine([string] $Line) {
    <# Ok and Date (yyyy-MM-dd, $null when not Ok) of a line that starts '## ' (plan D.3). #>
    $m = [regex]::Match($Line, $script:PaperWikiLogLine)
    if (-not $m.Success) { return [pscustomobject]@{ Ok = $false; Date = $null } }
    $d = [datetime]::MinValue
    $ok = [datetime]::TryParseExact($m.Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $d)
    if (-not $ok) { return [pscustomobject]@{ Ok = $false; Date = $null } }
    return [pscustomobject]@{ Ok = $true; Date = $m.Groups[1].Value }
}

function Remove-PaperWikiQuotes([string] $Value) {
    if ($Value.Length -ge 2 -and (($Value.StartsWith('"') -and $Value.EndsWith('"')) -or ($Value.StartsWith("'") -and $Value.EndsWith("'")))) {
        return $Value.Substring(1, $Value.Length - 2)
    }
    return $Value
}

function Get-PaperWikiPinnedSource([AllowNull()][string] $Text) {
    <# Repo and Sha ('' when absent) from a frontmatter opening the text; $null when there is none or it names no repo. #>
    $lines = ([string] $Text).Replace("`r", '').Split("`n")
    if ($lines.Count -lt 2 -or $lines[0].TrimEnd() -ne '---') { return $null }
    $repo = $null
    $sha = ''
    $closed = $false
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l.TrimEnd() -eq '---') { $closed = $true; break }
        $m = [regex]::Match($l, '^repo:\s*(.+?)\s*$')
        if ($m.Success) { $repo = Remove-PaperWikiQuotes $m.Groups[1].Value; continue }
        $m = [regex]::Match($l, '^sha:\s*(.+?)\s*$')
        if ($m.Success) { $sha = Remove-PaperWikiQuotes $m.Groups[1].Value }
    }
    if (-not $closed -or [string]::IsNullOrEmpty($repo)) { return $null }
    return [pscustomobject]@{ Repo = $repo; Sha = $sha }
}

function Get-PaperWikiText([hashtable] $Texts, [string] $Path) {
    foreach ($k in @($Texts.Keys)) { if ([string]::Equals([string] $k, $Path, [StringComparison]::OrdinalIgnoreCase)) { return [string] $Texts[$k] } }
    return $null
}

function Get-PaperWikiLintFindings([hashtable] $Texts, [AllowNull()][AllowEmptyCollection()][string[]] $Files, [hashtable] $Heads) {
    <#
    Every finding of plan table D.5: Severity (error, warning), Rule, Path, Line (0 = the whole file),
    Message; sorted by Path (ordinal, case ignored), then Line, then Rule.
    #>
    if ($null -eq $Heads) { $Heads = @{} }
    $files = @($Files | Where-Object { $_ })
    $found = New-Object System.Collections.Generic.List[object]
    $add = { param($sev, $rule, $path, $line, $msg) $found.Add([pscustomobject]@{ Severity = $sev; Rule = $rule; Path = $path; Line = [int] $line; Message = $msg }) }
    $leafOf = { param($p) $p.Substring($p.LastIndexOf('/') + 1) }
    $nameOf = { param($p) $leaf = & $leafOf $p; if ($leaf.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) { $leaf.Substring(0, $leaf.Length - 3) } else { $leaf } }

    foreach ($root in $script:PaperWikiRootFiles) {
        if ($null -eq (Get-PaperWikiText $Texts $root)) { & $add 'error' 'missing-file' $root 0 'missing - run wiki.ps1 init on this folder; it adds what is missing and keeps every file that is there' }
    }

    $pages = @($files | Where-Object { $_ -match '^wiki/.+\.md$' })
    $kindPages = New-Object System.Collections.Generic.List[string]
    foreach ($p in $pages) {
        $leaf = & $leafOf $p
        $badName = $leaf -cnotmatch $script:PaperWikiPageName
        $outside = $p -match '^wiki/[^/]+$'
        if ($badName) { & $add 'error' 'bad-name' $p 0 'page names are lowercase ascii words joined by "-" (example: transaction-scope.md)' }
        if ($outside) { & $add 'error' 'outside-folder' $p 0 'pages live in a kind folder - move it to wiki/<kind>/' }
        if (-not $badName -and -not $outside) { $kindPages.Add($p) }
    }
    $byLeaf = @{}
    foreach ($p in @(Sort-PaperWikiStrings $pages)) {
        $leaf = (& $leafOf $p).ToLowerInvariant()
        if ($byLeaf.ContainsKey($leaf)) { & $add 'error' 'duplicate-name' $p 0 "has the same name as $($byLeaf[$leaf]) - [[$(& $nameOf $p)]] cannot tell them apart; rename one" }
        else { $byLeaf[$leaf] = $p }
    }

    # Links of the index and of every page; the resolved set of each.
    $linksBy = @{}
    $checked = @('index.md') + $pages
    foreach ($src in $checked) {
        $text = Get-PaperWikiText $Texts $src
        if ($null -eq $text) { continue }
        $resolved = New-Object System.Collections.Generic.List[object]
        foreach ($l in @(Get-PaperWikiLinks $text)) {
            $hits = @(Resolve-PaperWikiLink $l.Target $files)
            if ($hits.Count -eq 0) { & $add 'error' 'dead-link' $src $l.Line "[[$($l.Inner)]] links to no page - write the page or remove the link" }
            elseif ($hits.Count -ge 2) { & $add 'error' 'ambiguous-link' $src $l.Line "[[$($l.Inner)]] matches $($hits -join ', ') - put the folder in the link to choose one" }
            $resolved.Add([pscustomobject]@{ Line = $l.Line; Hits = $hits })
        }
        $linksBy[$src] = $resolved.ToArray()
    }

    $indexText = Get-PaperWikiText $Texts 'index.md'
    if ($null -ne $indexText) {
        $listed = New-Object System.Collections.Generic.List[string]
        $firstLine = @{}
        $section = $null
        foreach ($l in @(Get-PaperWikiTextLines $indexText)) {
            if ($l.Raw.StartsWith('## ')) { $section = $l.Raw.Substring(3).Trim(); continue }
            if ($l.Plain -notmatch $script:PaperWikiIndexLine) { continue }
            $first = $null
            foreach ($m in [regex]::Matches($l.Plain, $script:PaperWikiLinkPattern)) {
                $t = ConvertTo-PaperWikiTarget $m.Groups[1].Value
                if ($t -ne '') { $first = $m; break }
            }
            if ($null -eq $first) { continue }
            $hits = @(Resolve-PaperWikiLink (ConvertTo-PaperWikiTarget $first.Groups[1].Value) $files)
            foreach ($h in $hits) { $listed.Add($h) }
            if ($hits.Count -ne 1 -or $hits[0] -notmatch '^wiki/[^/]+/') { continue }
            $page = $hits[0]
            $n = & $nameOf $page
            $kind = $page.Split('/')[1]
            $key = $page.ToLowerInvariant()
            if ($firstLine.ContainsKey($key)) {
                & $add 'error' 'index-duplicate' 'index.md' $l.Number "[[$n]] is listed again (first on line $($firstLine[$key])) - keep one line per page"
            }
            else { $firstLine[$key] = $l.Number }
            if ($null -eq $section) {
                & $add 'error' 'index-section' 'index.md' $l.Number "[[$n]] is under no section but the page is in wiki/$kind/ - move the line under `"## $kind`""
            }
            elseif ($section -ine $kind) {
                & $add 'error' 'index-section' 'index.md' $l.Number "[[$n]] is under `"## $section`" but the page is in wiki/$kind/ - move the line under `"## $kind`""
            }
            $close = $l.Plain.IndexOf(']]', $first.Index)
            $rest = $l.Plain.Substring($close + 2)
            $dashes = '^\s*(-|:|' + [char]0x2013 + '|' + [char]0x2014 + ')\s*\S'
            if ($rest -notmatch $dashes) {
                & $add 'error' 'index-summary' 'index.md' $l.Number "[[$n]] has no one-line summary - write `"- [[$n]] - <what the page holds>`""
            }
        }
        foreach ($p in $kindPages) {
            if (-not (Test-PaperWikiListed $listed.ToArray() $p)) {
                $n = & $nameOf $p
                $kind = $p.Split('/')[1]
                & $add 'error' 'index-missing' $p 0 "is not in index.md - add `"- [[$n]] - <one line>`" under `"## $kind`""
            }
        }
    }

    foreach ($p in $kindPages) {
        $linked = $false
        foreach ($src in $pages) {
            if ([string]::Equals($src, $p, [StringComparison]::OrdinalIgnoreCase) -or -not $linksBy.ContainsKey($src)) { continue }
            foreach ($r in @($linksBy[$src])) {
                if (@($r.Hits).Count -eq 1 -and [string]::Equals($r.Hits[0], $p, [StringComparison]::OrdinalIgnoreCase)) { $linked = $true; break }
            }
            if ($linked) { break }
        }
        if (-not $linked) { & $add 'warning' 'orphan' $p 0 "no other page links [[$(& $nameOf $p)]] - link it from a page it belongs to" }
    }

    $logText = Get-PaperWikiText $Texts 'log.md'
    if ($null -ne $logText) {
        $prev = $null
        foreach ($l in @(Get-PaperWikiTextLines $logText)) {
            if (-not $l.Raw.StartsWith('## ')) { continue }
            $v = Test-PaperWikiLogLine $l.Raw
            if (-not $v.Ok) {
                $shown = if ($l.Raw.Length -gt 80) { $l.Raw.Substring(0, 80) + '...' } else { $l.Raw }
                & $add 'error' 'log-format' 'log.md' $l.Number "`"$shown`" is not `"## [YYYY-MM-DD] <op> | <title>`" (op: init, ingest, query, promote, lint)"
                continue
            }
            if ($null -ne $prev -and [string]::CompareOrdinal($v.Date, $prev) -lt 0) {
                & $add 'error' 'log-order' 'log.md' $l.Number "[$($v.Date)] comes after [$prev] - the log is append-only: new entries go at the end"
            }
            $prev = $v.Date
        }
    }

    foreach ($k in @($Texts.Keys)) {
        foreach ($hit in @(Get-PaperSecretHits ([string] $Texts[$k]))) {
            & $add 'error' 'secret' $k $hit.Line "looks like $($hit.Label) - delete it now: the wiki is plain text, pushed to its remote and read by every agent; the value is not repeated here"
        }
        if ($k -notmatch '^raw/.+\.md$') { continue }
        $pin = Get-PaperWikiPinnedSource ([string] $Texts[$k])
        if ($null -eq $pin) { continue }
        if ($pin.Sha -notmatch '^[0-9a-fA-F]{7,40}$') {
            & $add 'error' 'source-pin' $k 0 "names repo $($pin.Repo) without a commit - add `"sha: <the commit read>`" (git -C $($pin.Repo) rev-parse --short HEAD when it was read)"
            continue
        }
        $head = if ($Heads.ContainsKey($pin.Repo)) { [string] $Heads[$pin.Repo] } else { '' }
        if ([string]::IsNullOrEmpty($head)) {
            & $add 'warning' 'source-missing' $k 0 "$($pin.Repo) is not a git clone on this machine - the source cannot be checked"
            continue
        }
        $len = [Math]::Min($head.Length, $pin.Sha.Length)
        if (-not [string]::Equals($head.Substring(0, $len), $pin.Sha.Substring(0, $len), [StringComparison]::OrdinalIgnoreCase)) {
            $short = $head.Substring(0, [Math]::Min(7, $head.Length))
            & $add 'warning' 'source-drift' $k 0 "$($pin.Repo) is at $short, this source read $($pin.Sha) - read its paths again, then add a new raw file with the new sha"
        }
    }

    $all = $found.ToArray()
    $sorted = New-Object System.Collections.Generic.List[object]
    foreach ($x in $all) { $sorted.Add($x) }
    $sorted.Sort([Comparison[object]] {
            param($a, $b)
            $c = Compare-PaperWikiText $a.Path $b.Path
            if ($c -ne 0) { return $c }
            if ($a.Line -ne $b.Line) { return $a.Line.CompareTo($b.Line) }
            return [string]::CompareOrdinal($a.Rule, $b.Rule)
        })
    return $sorted.ToArray()
}

function Format-PaperWikiFinding($Finding) {
    <# <SEVERITY> <rule> <path>[:<line>] <message> #>
    $at = if ([int] $Finding.Line -gt 0) { ":$($Finding.Line)" } else { '' }
    return ('{0} {1} {2}{3} {4}' -f ([string] $Finding.Severity).ToUpperInvariant(), $Finding.Rule, $Finding.Path, $at, $Finding.Message)
}

function Format-PaperWikiSummary($Findings, [int] $Pages) {
    $all = @($Findings | Where-Object { $null -ne $_ })
    $errors = @($all | Where-Object { $_.Severity -eq 'error' }).Count
    $warnings = @($all | Where-Object { $_.Severity -eq 'warning' }).Count
    return "wiki-lint: $Pages page(s), $errors error(s), $warnings warning(s)"
}

function Get-PaperWikiLintExitCode($Findings) {
    <# 1 when any finding is an error, else 0. #>
    if (@($Findings | Where-Object { $null -ne $_ -and $_.Severity -eq 'error' }).Count -gt 0) { return 1 }
    return 0
}
