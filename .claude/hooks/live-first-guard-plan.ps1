# The live-first order as a pure function over what the PreToolUse hook read: the file about to be edited,
# the approved plans with their lines, and three profile values. No file system, no clock. Dot-sourced;
# declares no param() block.
#
# The order, for a code task on a host that can check the change itself (the profile declares live.loop):
#
#     ensure the host session -> measure live, recorded as a baseline evidence row -> red unit test written
#     from that number -> green -> verify live again, and loop until it holds
#
# A red test written before anyone measured the host is a test of what the author believed the host does.
# The project owner who asked for this order had watched tests go green on numbers the host never produced.
#
# When the edit is on the order:
#   - the profile declares no live.loop -> quiet: nothing on this project can measure live, and "red first"
#     alone is the rule (the task gate keeps it)
#   - the file is not source code (codeMap.extensions), or lives under the feature docs or .claude/ (plans,
#     harness scripts - writing the measuring script IS the measure step) -> quiet
#   - no approved code plan has an unticked unit or live task whose {files:} covers the file -> quiet: the
#     file scope has its own gate, and a reminder about work no task claims is a false alarm
#   - the covering task is itself the live baseline task (a live task whose text says baseline) -> quiet
#   - the plan's evidence has a baseline row for that task's group or an earlier one -> quiet
# Otherwise: remind (context, the edit goes ahead), or block when the profile sets live.enforceOrder true.
#
# A baseline row is a table row under the plan's evidence heading whose text says "baseline" (or the
# Vietnamese "moc do", diacritics ignored); its first task id places it in a group.
#
# Needs Get-PaperPlanTasks, Get-PaperPlanOutline, Get-PaperPlainText (paperflow/tasks-gate-plan.ps1) and
# Test-PaperTaskCoversChange (plan-nag-plan.ps1): one reader of a plan for every hook.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperLiveFirstLanes = @('unit', 'live')

function Test-PaperBaselineText([string] $Text) {
    return ((Get-PaperPlainText $Text) -match '\bbaseline\b|\bmoc do\b')
}

function Get-PaperPlanWorkType([string[]] $Lines) {
    foreach ($line in @($Lines | Select-Object -First 20)) {
        $plain = (Get-PaperPlainText ([string] $line)).Trim()
        if ($plain -match '^\*\*(loai viec|work type|loai):?\*\*:?\s*(\w+)') { return $Matches[2] }
    }
    return 'code'
}

function Get-PaperLiveFirstVerdict {
    <#
    .SYNOPSIS
    Action (quiet | remind | block), Text and Key (plan|task, for the once-per-session check) for one edit.
    .PARAMETER File
    The edited file relative to the project root, / separators - the form {files:} globs are written in.
    .PARAMETER Plans
    One object per APPROVED plan: Path (relative) and Lines.
    .PARAMETER LiveLoop
    live.loop of the profile; empty means the host cannot measure live and the order does not apply.
    .PARAMETER Enforce
    live.enforceOrder of the profile.
    #>
    param(
        [string] $File,
        $Plans = @(),
        [string] $LiveLoop = '',
        [bool] $Enforce = $false,
        [string[]] $Extensions = @('.cs', '.xaml', '.ps1', '.py', '.ts'),
        [string] $DocsRel = 'docs/features',
        $LaneAliases
    )
    $quiet = [pscustomobject]@{ Action = 'quiet'; Text = ''; Key = '' }
    if ([string]::IsNullOrWhiteSpace($LiveLoop) -or [string]::IsNullOrWhiteSpace($File)) { return $quiet }
    $rel = $File.Replace('\', '/').TrimStart('/')
    $ext = [IO.Path]::GetExtension($rel).ToLowerInvariant()
    if (@($Extensions) -notcontains $ext) { return $quiet }
    $docs = $DocsRel.Replace('\', '/').Trim('/') + '/'
    if ($rel.StartsWith($docs, [StringComparison]::OrdinalIgnoreCase) -or $rel.StartsWith('.claude/', [StringComparison]::OrdinalIgnoreCase)) { return $quiet }

    foreach ($plan in @($Plans)) {
        if ($null -eq $plan) { continue }
        $lines = [string[]] @($plan.Lines)
        if ((Get-PaperPlanWorkType $lines) -ne 'code') { continue }
        $tasks = @(Get-PaperPlanTasks $lines $LaneAliases)
        $bareCoversAll = @($tasks | Where-Object { @($_.Files).Count -gt 0 }).Count -eq 0
        $owner = $null
        foreach ($t in $tasks) {
            if ($t.Ticked -or $script:PaperLiveFirstLanes -notcontains $t.Lane) { continue }
            if (Test-PaperTaskCoversChange @($rel) $t.Files $bareCoversAll) { $owner = $t; break }
        }
        if ($null -eq $owner) { continue }
        if ($owner.Lane -eq 'live' -and (Test-PaperBaselineText $owner.Text)) { continue }

        $groupOf = @{}
        foreach ($t in $tasks) { if (-not $groupOf.ContainsKey($t.Id)) { $groupOf[$t.Id] = $t.Group } }
        $hasBaseline = $false
        foreach ($row in (Get-PaperPlanOutline $lines)) {
            if ($row.Section -ne 'evidence' -or $row.Heading -or $row.Fenced) { continue }
            if (-not $row.Line.TrimStart().StartsWith('|')) { continue }
            if (-not (Test-PaperBaselineText $row.Line)) { continue }
            $id = [regex]::Match($row.Line, '\bT\d+[a-z]?\b').Value
            if (-not $id -or -not $groupOf.ContainsKey($id) -or $groupOf[$id] -le $owner.Group) { $hasBaseline = $true; break }
        }
        if ($hasBaseline) { continue }

        $text = @"
Live first ($($plan.Path), $($owner.Id), editing $rel): this project can check code in its host (live.loop in the profile), and the plan's evidence has no live baseline for this task's group yet. The order for a code task here is:

  1. ensure - the host session and its MCP server are on (switch the server on yourself; starting the host still needs the user's OK)
  2. measure - run the behaviour live and add an evidence row that says "baseline", with the number
  3. red - write the unit test FROM that number, seen failing on its assertion
  4. green - the smallest code that passes it
  5. verify - live again, and loop until the number holds

Each step: $LiveLoop <step>
If this task's behaviour cannot be checked in the host, say so in one line in the plan's Decisions and write a baseline row that says "baseline: not checkable - <why>".
"@
        if ($Enforce) {
            return [pscustomobject]@{ Action = 'block'; Text = $text + "`nlive.enforceOrder is true in the profile, so this edit is refused until that row exists."; Key = "$($plan.Path)|$($owner.Id)" }
        }
        return [pscustomobject]@{ Action = 'remind'; Text = $text; Key = "$($plan.Path)|$($owner.Id)" }
    }
    return $quiet
}
