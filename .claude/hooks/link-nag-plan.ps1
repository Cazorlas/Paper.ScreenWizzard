# The link gate as a pure function over the text of the final message of a turn. No file system, no
# session: the hook hands the text in, so every branch below is a test with a plain string. Dot-sourced;
# declares no param() block.
#
# Paperflow rule 9: when a reply names a plan, a task or any markdown file, the reader gets a link that
# OPENS it. The rule was written, then broken twice in one session by the same author - once with links
# that did not open, once with none at all, the second time after a whole session spent on that lesson. A
# rule with no gate is a wish, so this is the gate.
#
# What counts as a mention of a document:
#   - a markdown file name (anything ending .md, an optional #L<line> after it) that is not the target or
#     the text of a link in the same reply. A backtick is not a link, so `SPEC.md` still counts.
#   - a task id (T8, T8c) in a reply that links no plan at all. The plan is where a task lives; a reply
#     naming tasks and pointing nowhere makes the reader go and look.
# What does not: anything inside a fenced block (content being shown, not a reference), a web URL, a file
# that is not markdown, and a rule code such as F20.
#
# And what it refuses to pass: a link that looks right and does not open. Measured 2026-09-20 in a
# repository with a space in its path - the link went out with the space written %20 and did not open,
# for a whole session, with nothing to say so until the user did. That is worse than a bare name, which at
# least tells the reader to go and look. The form that opened is angle brackets around the path.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Get-PaperLinkNagVerdict {
    <#
    .SYNOPSIS
    ExitCode (2 = say Text and hold the stop, 0 = quiet) and Text for one reply.
    .PARAMETER Text
    The final message of the turn, as written.
    #>
    param([AllowNull()][AllowEmptyString()][string] $Text)

    $quiet = [pscustomobject]@{ ExitCode = 0; Text = '' }
    if ([string]::IsNullOrWhiteSpace($Text)) { return $quiet }

    # Fenced blocks are content being shown. An unclosed fence runs to the end of the reply, which is what a
    # markdown reader does with it too.
    $body = [regex]::Replace($Text, '(?ms)^[ \t]{0,3}(`{3,}|~{3,})[^\r\n]*\r?\n.*?(?:^[ \t]{0,3}\1[ \t]*$|\z)', '')

    # Links, in the two shapes markdown accepts: a target with no space, or any target wrapped in <>. A raw
    # space in the target is not a link at all, so it stays in the text and its file counts as named.
    $linkPattern = '\[([^\]]*)\]\((<[^>\r\n]*>|[^)\s]*)\)'
    $linked = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    $linksAPlan = $false
    $brokenLinks = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($body, $linkPattern)) {
        $label = $m.Groups[1].Value
        $target = $m.Groups[2].Value.Trim('<', '>')
        if ($target -match '(?i)%20') { $brokenLinks.Add($m.Value) }
        if ($target -match '(?i)plan\.md') { $linksAPlan = $true }
        foreach ($name in @($target, $label)) {
            $leaf = ($name -replace '#.*$', '') -split '[\\/]' | Select-Object -Last 1
            if ($leaf -match '(?i)\.md$') { [void] $linked.Add($leaf) }
        }
    }
    $rest = [regex]::Replace($body, $linkPattern, ' ')
    $rest = [regex]::Replace($rest, 'https?://\S+', ' ')

    $bare = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($rest, '[^\s`''"()\[\]<>,;:!?]+\.md\b(?:#L\d+)?')) {
        $leaf = ($m.Value -replace '#.*$', '') -split '[\\/]' | Select-Object -Last 1
        if ($linked.Contains($leaf)) { continue }
        if (-not $bare.Contains($m.Value)) { $bare.Add($m.Value) }
    }

    $tasks = New-Object System.Collections.Generic.List[string]
    if (-not $linksAPlan) {
        foreach ($m in [regex]::Matches($rest, '(?<![\w-])T\d+[a-z]?(?!\w)')) {
            if (-not $tasks.Contains($m.Value)) { $tasks.Add($m.Value) }
        }
    }

    if ($bare.Count -eq 0 -and $tasks.Count -eq 0 -and $brokenLinks.Count -eq 0) { return $quiet }

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($b in @($bare | Select-Object -First 8)) { $lines.Add("  - $b - named, with no link that opens it") }
    if ($tasks.Count -gt 0) {
        $lines.Add('  - ' + (@($tasks | Select-Object -First 8) -join ', ') + ' - task ids, and this reply links no plan for the reader to find them in')
    }
    foreach ($b in @($brokenLinks | Select-Object -First 4)) { $lines.Add("  - $b - %20 in the target does not open") }

    $text = "This reply names documents without a link that opens them (paperflow rule 9):`n`n" + ($lines -join "`n") + @"


Resend it with one clickable link per document, measured now rather than remembered:
  [name](path from the working directory), plus #L<line> when it points at a line - take the number from
  grep -n right before sending.
  A path with a space goes in angle brackets: [name](<My Project/docs/x.md>). Never %20: that was measured
  broken. A file outside the working directory is written with a leading two-dot prefix.
  A task is a line in a plan, so link the plan (with #L<line> of the task) whenever you name a task.
"@
    return [pscustomobject]@{ ExitCode = 2; Text = $text }
}
