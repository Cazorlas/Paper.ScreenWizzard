# The memory gate as pure functions over the path and text of the file just written. No file system: the
# hook hands in the names of the files beside it and which cited paths are gone, so every branch below is
# a test with plain strings. Dot-sourced; declares no param() block.
#
# Claude Code's memory is one fact per file under ~/.claude/projects/<slug>/memory/, and MEMORY.md is the
# index loaded into EVERY session of that project. Measured on one machine 2026-09-29 (113 files): 12 bodies
# over 4000 characters (the longest 18.6k), one index of 13.7k characters, 19 index lines over 200
# characters, 6 of 46 backticked absolute paths gone. A memory that grows into a diary costs every session
# and is read less. hindsight (vectorize-io/hindsight, consolidation/prompts.py) keeps its observations
# usable with a few rules - update rather than create, one facet per entry, a changed state written as
# "was X, now Y" - and retracts what cites a source that is gone (reflect/retractions.py). ADR-0028.
#
# What it says, never blocking and never editing (memory is the user's):
#   - a memory body (after the frontmatter) over 4000 characters
#   - a backticked absolute path in it that no longer exists - maybe stale, maybe only an example
#   - MEMORY.md: a line over 200 characters, a link to a file that is not there, a file it does not list
# Only whole backticked spans count as paths: a bare path with a space in it cannot be told from prose
# (a bare scan found 22 "gone" paths, most of them `D:\Program` cut at the space; backticked: 6).
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperMemoryBodyLimit = 4000
$script:PaperMemoryIndexLineLimit = 200
$script:PaperMemoryFolder = '(?i)[\\/]\.claude[\\/]projects[\\/][^\\/]+[\\/]memory[\\/]([^\\/]+\.md)$'

function Get-PaperMemoryFileName([string] $Path) {
    <# The file name when Path is a memory file, else $null. #>
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $m = [regex]::Match($Path, $script:PaperMemoryFolder)
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Get-PaperMemoryPaths([AllowNull()][AllowEmptyString()][string] $Text) {
    <# Every whole backticked absolute path (drive or UNC), once, in order. `...` and `<name>` are examples. #>
    $seen = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches([string] $Text, '`([^`\r\n]+)`')) {
        $p = $m.Groups[1].Value.Trim()
        if ($p -notmatch '^([A-Za-z]:[\\/]|\\\\[^\\])') { continue }
        if ($p -match '\.\.\.|[<>*?|"]') { continue }
        if (-not $seen.Contains($p)) { $seen.Add($p) }
    }
    return $seen.ToArray()
}

function Get-PaperMemoryBody([string] $Text) {
    $t = ([string] $Text).Replace("`r", '')
    $m = [regex]::Match($t, '(?s)\A---\n.*?\n---\n?')
    if ($m.Success) { return $t.Substring($m.Length) }
    return $t
}

function Get-PaperMemoryNagVerdict {
    <#
    .SYNOPSIS
    ExitCode (2 = say Text to the agent, 0 = quiet) and Text for one memory file just written.
    .PARAMETER Files
    The .md names in the same folder, MEMORY.md left out; read only for MEMORY.md.
    .PARAMETER MissingPaths
    The backticked absolute paths of Text that do not exist.
    #>
    param(
        [AllowNull()][AllowEmptyString()][string] $Path,
        [AllowNull()][AllowEmptyString()][string] $Text,
        [AllowNull()][string[]] $Files = @(),
        [AllowNull()][string[]] $MissingPaths = @()
    )
    $quiet = [pscustomobject]@{ ExitCode = 0; Text = '' }
    $name = Get-PaperMemoryFileName $Path
    if (-not $name) { return $quiet }

    $problems = New-Object System.Collections.Generic.List[string]
    if ($name -ieq 'MEMORY.md') {
        $lines = ([string] $Text).Replace("`r", '') -split "`n"
        $long = @(for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i].Length -gt $script:PaperMemoryIndexLineLimit) { $i + 1 } })
        if ($long.Count -gt 0) {
            $problems.Add(("{0} line(s) over {1} characters - line {2}: the index is loaded into every session, so each line is a" -f $long.Count, $script:PaperMemoryIndexLineLimit, ($long -join ', ')) + " hook of about 150 characters and the detail goes in the file")
        }
        $listed = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
        foreach ($m in [regex]::Matches([string] $Text, '\]\(<?([^)<>\s]+\.md)>?\)')) {
            $target = ($m.Groups[1].Value -split '[\\/]')[-1]
            [void] $listed.Add($target)
            if (@($Files) -notcontains $target) { $problems.Add("links $target, which is not in the folder: remove the line or restore the file") }
        }
        foreach ($f in @($Files)) {
            if ($f -and -not $listed.Contains($f) -and ([string] $Text).IndexOf($f, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
                $problems.Add("does not list ${f}: add a one-line pointer, or delete the file if it is no longer true")
            }
        }
    }
    else {
        $body = Get-PaperMemoryBody $Text
        if ($body.Length -gt $script:PaperMemoryBodyLimit) {
            $problems.Add(("{0} has a body of {1} characters (over {2})" -f $name, $body.Length, $script:PaperMemoryBodyLimit))
        }
        foreach ($p in @($MissingPaths)) {
            if ($p) { $problems.Add("$name cites $p, which does not exist: stale (rewrite or delete the line), or only an example") }
        }
    }
    if ($problems.Count -eq 0) { return $quiet }

    $text = "Memory hygiene ($name):`n" + (($problems | ForEach-Object { "  - $_" }) -join "`n") + @"


Keep memory usable (hindsight's consolidation rules):
  - one fact per file; a file carrying several topics splits into several files
  - update the memory that covers it rather than create a near-duplicate
  - a state that changed is rewritten as it is now - "was X, now Y (date)" - not appended as a diary
  - what is no longer true, or whose subject is gone, is deleted from the file and the index
Say so and move on if this is deliberate (a reference note is allowed to be long). PAPER_SKIP_MEMORY_NAG=1 turns this off.
"@
    return [pscustomobject]@{ ExitCode = 2; Text = $text }
}
