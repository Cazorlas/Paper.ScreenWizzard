# The lane question of the review commands (plan 2026-10-03-qa-lane-picker, ADR-0035; narrowed by ADR-0044, F234): the list of the
# lanes that apply and their cost (review.ps1 lanes), the one option group of the choice box, and an answer turned into the exact
# review.ps1 plan command line (review.ps1 lanes -Pick). Static is never a choice: it runs first and is free. Pure: no I/O.
# The caller dot-sources review-plan.ps1 first (Format-PaperReviewNumber, $script:PaperReviewLaneNames); declares no
# param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Get-PaperReviewLaneAsk {
    <#
    .SYNOPSIS
    The option group of the choice box (B.3, F234): one question, "all" and the reading lanes that apply. Static is
    never a choice - it runs first and is free. Fewer than two reading lanes: no question. Only the architecture
    command has two (architecture, smell); a caller with more lanes (the six of a pure test) gets them all in the one group.
    #>
    param([string[]] $Names)
    $n = @($Names | Where-Object { $_ -and $_ -ne 'static' })
    if ($n.Count -lt 2) { return @() }
    return @('all,' + ($n -join ','))
}

function Format-PaperReviewLaneList {
    <#
    .SYNOPSIS
    What review.ps1 lanes prints (B.2, B.3): the scope, the static line (it runs first, free) or the reason it does not
    apply, one numbered line per reading lane that applies with its cost, one line per lane that does not apply or is
    turned off, the verify readers, the total and the option group. ExitCode 5 when no lane applies.
    #>
    param([string] $Mode, [string] $Reason, [int] $FileCount, [int] $ExcludedCount, $Estimate, [string] $RepoRoot = '', [string[]] $ScopeLines = @(),
        [string] $Executor = '', [string] $ExecutorReason = '', $Collab = $null, $Solo = $null)
    $out = @("review-lanes: scope $Mode ($Reason) - $FileCount file(s), $ExcludedCount excluded")
    if ($RepoRoot) { $out += "repo: $RepoRoot (external, read only)" }
    # The base: and changes: lines of an external branch scope (ADR-0037), right after the repo line.
    $out += @($ScopeLines | Where-Object { $_ })
    $rows = @($Estimate.Rows | Where-Object { $null -ne $_ -and $_.Name -ne 'verify' })
    $run = @($rows | Where-Object { $_.State -eq 'run' })
    $static = @($run | Where-Object { $_.Name -eq 'static' })
    if ($static.Count -gt 0) { $out += "static: runs first, free ($($static[0].Files) C#/VB files)" }
    $i = 0
    foreach ($r in @($run | Where-Object { $_.Name -ne 'static' })) {
        $i++
        $line = "$i. $($r.Name) - $(Format-PaperReviewNumber $r.Tokens) tokens - $($r.Files) file(s), $($r.Agents) agent(s)"
        if ($Executor -eq 'collab') { $line = "$i. $($r.Name) - $(Format-PaperReviewNumber $r.Tokens) tokens - $($r.Files) file(s), $($r.Agents) turn(s) on codex" }
        if ($r.Reason) { $line += ", $($r.Reason)" }
        $out += $line
    }
    foreach ($r in @($rows | Where-Object { $_.State -ne 'run' })) { $out += "$($r.State): $($r.Name) - $($r.Reason)" }
    $verify = @($Estimate.Rows | Where-Object { $null -ne $_ -and $_.Name -eq 'verify' })
    if ($verify.Count -gt 0 -and "$($verify[0].State)" -ne '0') {
        $out += "verify: $($verify[0].State) reader(s), $(Format-PaperReviewNumber $verify[0].Tokens) tokens - once, when an agent lane runs"
    }
    if ($run.Count -eq 0) {
        $out += 'review: NOT APPLICABLE - no lane applies to this scope'
        return [pscustomobject]@{ Lines = $out; ExitCode = 5 }
    }
    $names = @($run | ForEach-Object { "$($_.Name)" })
    # F205, F210: who answers the agent lanes, and what the whole run takes on Codex.
    $wallText = ''
    if ($Executor -and @($run | Where-Object { $script:PaperReviewLlmLanes -contains $_.Name }).Count -gt 0) {
        $out += @(Get-PaperReviewExecutorLines -Executor $Executor -Reason $ExecutorReason -Collab $Collab -Run '' -Brief -Solo $Solo)
        if ($Executor -eq 'collab') {
            $secs = @(@($Collab.TurnTokens) | ForEach-Object { Get-PaperReviewCodexTurnSec -Tokens ([long] $_) })
            $wallText = ', about ' + (Format-PaperReviewDuration -Seconds (Get-PaperReviewWallClock -Durations ([int[]] $secs) -Parallel ([int] $Collab.Parallel) -GapSec ([int] $Collab.GapSec)))
        }
    }
    if ([long] $Estimate.Total -gt 0) { $out += "all: $(Format-PaperReviewNumber $Estimate.Total) tokens$wallText - $($names -join ', ')" }
    else { $out += "all: free - $($names -join ', ')" }
    $ask = @(Get-PaperReviewLaneAsk -Names $names)
    if ($ask.Count -gt 0) { $out += 'ask: ' + (@($ask | ForEach-Object { $_ -replace ',', ', ' }) -join ' | ') }
    else { $out += 'ask: none - one lane applies, nothing to choose' }
    return [pscustomobject]@{ Lines = $out; ExitCode = 0 }
}

function Resolve-PaperReviewLanePick {
    <#
    .SYNOPSIS
    An answer to the lane question (B.3): numbers of the list (the reading lanes), lane names or all, separated by commas
    or spaces, to the lanes in lane order and the plan parameter that runs them. Static is not a choice: it is added when
    it runs, and picking it by name is refused. The first wrong token is the Error, named (F100, F234); numbers, names
    and all pick the same lanes (F105).
    #>
    param([string[]] $Pick, $Lanes)
    $fail = { param([string] $e) return [pscustomobject]@{ Error = $e; Lanes = @(); Param = '' } }
    $all = @($Lanes | Where-Object { $null -ne $_ -and $_.Name -ne 'verify' })
    $run = @($all | Where-Object { $_.State -eq 'run' } | ForEach-Object { "$($_.Name)" })
    $reading = @($run | Where-Object { $_ -ne 'static' })
    $count = $reading.Count
    if ($run.Count -eq 0) { return & $fail 'no lane applies to this scope' }
    $tokens = @(@($Pick) | ForEach-Object { "$_" -split '[,\s]+' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($tokens.Count -eq 0) { return & $fail '-Pick names no lane - give numbers from the list, lane names or all' }
    $picked = @()
    foreach ($t in $tokens) {
        if ($t -eq 'all') { $picked += $run; continue }
        if ($t -match '^\d+$') {
            $n = 0
            if (-not [int]::TryParse($t, [ref] $n) -or $n -lt 1 -or $n -gt $count) { return & $fail "$t is not on the list (1-$count)" }
            $picked += $reading[$n - 1]
            continue
        }
        if ($t -eq 'static' -and $run -contains 'static') { return & $fail 'static is not a choice - it runs first; say --skip static to leave it out' }
        if ($script:PaperReviewLaneNames -contains $t) {
            if ($run -contains $t) { $picked += $t; continue }
            $row = @($all | Where-Object { $_.Name -eq $t })[0]
            if ($null -eq $row) { return & $fail "'$t' is not a lane of this command" }
            return & $fail "$t is $($row.State) here - $($row.Reason)"
        }
        return & $fail "'$t' is not a number on the list, a lane or all"
    }
    if ($run -contains 'static') { $picked += 'static' }
    $lanes = @($script:PaperReviewLaneNames | Where-Object { $picked -contains $_ })
    $param = ''
    if ($lanes.Count -eq $run.Count) { $param = '' }
    else { $param = '-Only ' + ($lanes -join ',') }
    return [pscustomobject]@{ Error = ''; Lanes = $lanes; Param = $param }
}

# One argument of a command line: kept as it is when it needs no quotes, else in single quotes.
function ConvertTo-PaperReviewArg {
    param([string] $Value)
    if ($Value -match '^[A-Za-z0-9_./\\:=,+-]+$') { return $Value }
    return "'" + $Value.Replace("'", "''") + "'"
}

function Format-PaperReviewPickLines {
    <#
    .SYNOPSIS
    The two lines of review.ps1 lanes -Pick (B.4): the lanes and the scope, and the exact plan command line -
    the scope parameters as typed, in order, then the lane parameter (F101).
    #>
    param($Pick, [string] $Mode, [string] $Reason, $Arguments)
    $line = 'plan: review.ps1 plan'
    if ($null -ne $Arguments) {
        foreach ($k in @($Arguments.Keys)) { $line += " -$k $(ConvertTo-PaperReviewArg ([string] $Arguments[$k]))" }
    }
    if ($Pick.Param) { $line += " $($Pick.Param)" }
    return @("review-pick: $(@($Pick.Lanes) -join ', ') (scope $Mode - $Reason)", $line)
}
