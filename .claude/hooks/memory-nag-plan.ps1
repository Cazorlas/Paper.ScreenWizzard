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
#   - MEMORY.md: a line over 200 characters, a link to a file that is not there, a file it does not list,
#     two lines linking one file (F78)
#   - any memory file: something shaped like a secret (F77), by file, kind and line - never the value,
#     since what the hook says goes into the transcript
# Only whole backticked spans count as paths: a bare path with a space in it cannot be told from prose
# (a bare scan found 22 "gone" paths, most of them `D:\Program` cut at the space; backticked: 6).
#
# The secret patterns start from agentmemory's src/functions/privacy.ts (rohitg00/agentmemory, b3d6cf5).
# Its patterns as they are, over 137 memory files of the owner's machine (305469 characters, 2026-10-02,
# ADR-0032), flagged 6 places in 5 files and all 6 were wrong: three compound names `lusk-block-...` (sk-
# with no word boundary) and three mentions of `<private>...</private>` in a note teaching the rule. So here
# every prefixed pattern has a left boundary, the generic ones (sk-style, bearer, a value assigned to a key
# name) need a value that looks random - a digit, a letter and a run of 16 letters or digits - and a
# <private> block counts only with words inside. The kit's own rules over the same files: 0.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

. (Join-Path $PSScriptRoot 'shared-memory-plan.ps1')

$script:PaperMemoryBodyLimit = 4000
$script:PaperSecretListLimit = 5
# Name, label, regex, and which part must look random ('' = none, 'tail3' = the match after its first
# three characters, 'group1' = the first group). First in this order wins on a line.
$script:PaperSecretPatterns = @(
    @('private-block', 'a <private> block', '(?is)<private>(.*?)</private>', 'private'),
    @('pem-private-key', 'a PEM private key', '-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----', ''),
    @('github-token', 'a GitHub token', '(?<![A-Za-z0-9_])(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{22,})', ''),
    @('aws-access-key', 'an AWS access key id', '(?-i)(?<![A-Z0-9])(?:AKIA|ASIA)[0-9A-Z]{16}(?![A-Z0-9])', ''),
    @('google-api-key', 'a Google API key', '(?<![A-Za-z0-9])AIza[A-Za-z0-9_-]{35}', ''),
    @('slack-token', 'a Slack token', '(?<![A-Za-z0-9])xox[abprs]-[A-Za-z0-9-]{10,}', ''),
    @('jwt', 'a JSON web token', '(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}', ''),
    @('npm-token', 'an npm token', '(?<![A-Za-z0-9])npm_[A-Za-z0-9]{36}', ''),
    @('gitlab-token', 'a GitLab token', '(?<![A-Za-z0-9])glpat-[A-Za-z0-9_-]{20,}', ''),
    @('digitalocean-token', 'a DigitalOcean token', '(?<![A-Za-z0-9])dop_v1_[a-f0-9]{64}', ''),
    @('huggingface-token', 'a Hugging Face token', '(?<![A-Za-z0-9])hf_[A-Za-z0-9]{34,}', ''),
    @('stripe-key', 'a Stripe live key', '(?<![A-Za-z0-9])(?:sk|rk)_live_[A-Za-z0-9]{20,}', ''),
    @('sk-style-key', 'an API key (sk-, rk-, pk-, ak-)', '(?<![A-Za-z0-9_-])(?:sk|rk|pk|ak)-[A-Za-z0-9_-]{20,}', 'tail3'),
    @('azure-account-key', 'an Azure storage account key', '(?i)AccountKey=[A-Za-z0-9+/=]{40,}', ''),
    @('url-password', 'a password inside a URL', '[A-Za-z][A-Za-z0-9+.-]*://[^\s/:@`]+:[^\s/@`]{6,}@', ''),
    @('bearer-token', 'a bearer token', 'Bearer\s+([A-Za-z0-9._~+/=-]{20,})', 'group1'),
    @('secret-assignment', 'a value assigned to a key, token or password name', '(?i)(?<![A-Za-z0-9])(?:api[_-]?key|secret|token|password|passwd|pwd|credential|auth)[A-Za-z0-9_-]*["'']?\s*[=:]\s*["'']?([A-Za-z0-9_\-/.+=]{20,})', 'group1')
)
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

function Test-PaperRandomLooking([string] $Value) {
    <# A digit, a letter, and a run of at least 16 letters or digits: a key, not a word or a path. #>
    if ($Value -notmatch '[0-9]' -or $Value -notmatch '[A-Za-z]') { return $false }
    foreach ($m in [regex]::Matches($Value, '[A-Za-z0-9]+')) { if ($m.Length -ge 16) { return $true } }
    return $false
}

function Get-PaperSecretHits([AllowNull()][AllowEmptyString()][string] $Text) {
    <#
    Name, Label and Line (from 1, CRLF as LF) of each line holding something shaped like a secret, in line
    order; a <private> block over several lines counts on the line it opens. One hit per line: the first
    pattern in table order. No field carries the value, so no caller can repeat it.
    #>
    if ([string]::IsNullOrEmpty($Text)) { return @() }
    $t = $Text.Replace("`r", '')
    $starts = New-Object System.Collections.Generic.List[int]
    $starts.Add(0)
    for ($i = 0; $i -lt $t.Length; $i++) { if ($t[$i] -eq "`n") { $starts.Add($i + 1) } }
    $best = @{}
    for ($p = 0; $p -lt $script:PaperSecretPatterns.Count; $p++) {
        $pat = $script:PaperSecretPatterns[$p]
        foreach ($m in [regex]::Matches($t, $pat[2])) {
            $ok = switch ($pat[3]) {
                'private' { ($m.Groups[1].Value -replace ('[\s`.' + [char]0x2026 + ']'), '').Length -gt 0 }
                'tail3' { Test-PaperRandomLooking $m.Value.Substring(3) }
                'group1' { Test-PaperRandomLooking $m.Groups[1].Value }
                default { $true }
            }
            if (-not $ok) { continue }
            $line = $starts.BinarySearch($m.Index)
            if ($line -lt 0) { $line = (-bnot $line) - 1 }
            $line++
            if (-not $best.ContainsKey($line) -or $best[$line] -gt $p) { $best[$line] = $p }
        }
    }
    $out = foreach ($line in ($best.Keys | Sort-Object)) {
        $pat = $script:PaperSecretPatterns[$best[$line]]
        [pscustomobject]@{ Name = $pat[0]; Label = $pat[1]; Line = [int] $line }
    }
    return @($out)
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
        # F78: two lines linking one file - a lost update re-adding a line, or a near-duplicate pointer.
        $byFile = [ordered]@{}
        foreach ($e in @(Get-PaperMemoryIndexEntries -Text $Text)) {
            $key = $e.File.ToLowerInvariant()
            if (-not $byFile.Contains($key)) { $byFile[$key] = [pscustomobject]@{ File = $e.File; Lines = (New-Object System.Collections.Generic.List[int]) } }
            if (-not $byFile[$key].Lines.Contains($e.Line)) { $byFile[$key].Lines.Add($e.Line) }
        }
        foreach ($d in $byFile.Values) {
            if ($d.Lines.Count -lt 2) { continue }
            $l = $d.Lines.ToArray()
            $which = if ($l.Count -eq 2) { "$($l[0]) and $($l[1])" } else { (($l[0..($l.Count - 2)]) -join ', ') + " and $($l[-1])" }
            $problems.Add("lines $which both link $($d.File) - keep one line per file")
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
    # F77: secrets first, by kind and line only.
    $secret = ''
    $hits = @(Get-PaperSecretHits -Text $Text)
    if ($hits.Count -gt 0) {
        $shown = @($hits | Select-Object -First $script:PaperSecretListLimit | ForEach-Object { "  - line $($_.Line): looks like $($_.Label)" })
        if ($hits.Count -gt $script:PaperSecretListLimit) { $shown += "  - and $($hits.Count - $script:PaperSecretListLimit) more" }
        $secret = "Possible secret in memory (${name}):`n" + ($shown -join "`n") + "`n" +
            'Delete each from the file now: memory is plain text, loaded into every session and read by every agent. Keep the value where it lives (environment, vault) and write only where it is kept. Not a secret (an example, a public id)? Say so and move on.'
    }
    if ($problems.Count -eq 0) {
        if ($secret) { return [pscustomobject]@{ ExitCode = 2; Text = $secret } }
        return $quiet
    }

    $text = "Memory hygiene ($name):`n" + (($problems | ForEach-Object { "  - $_" }) -join "`n") + @"


Keep memory usable (hindsight's consolidation rules):
  - one fact per file; a file carrying several topics splits into several files
  - update the memory that covers it rather than create a near-duplicate
  - a state that changed is rewritten as it is now - "was X, now Y (date)" - not appended as a diary
  - what is no longer true, or whose subject is gone, is deleted from the file and the index
Say so and move on if this is deliberate (a reference note is allowed to be long). PAPER_SKIP_MEMORY_NAG=1 turns this off.
"@
    if ($secret) { $text = $secret + "`n`n" + $text }
    return [pscustomobject]@{ ExitCode = 2; Text = $text }
}

