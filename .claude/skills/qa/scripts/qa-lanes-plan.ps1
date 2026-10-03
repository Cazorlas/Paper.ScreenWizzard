# The lane question of /qa (plan 2026-10-03-qa-lane-picker, ADR-0035): the list of the lanes that apply
# and their cost (qa.ps1 lanes), the option groups of the choice box, and an answer turned into the exact
# qa.ps1 plan command line (qa.ps1 lanes -Pick). Pure: no I/O.
# The caller dot-sources qa-plan.ps1 first (Format-PaperQaNumber, $script:PaperQaLaneNames); declares no
# param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Get-PaperQaLaneAsk {
    <#
    .SYNOPSIS
    The option groups of the choice box, one string per question (B.1): up to three lanes fit one question
    with "all"; more go on in a second question that ends with "none". Fewer than two lanes: no question.
    #>
    param([string[]] $Names)
    $n = @($Names | Where-Object { $_ })
    if ($n.Count -lt 2) { return @() }
    if ($n.Count -le 3) { return @('all,' + ($n -join ',')) }
    return @(('all,' + ($n[0..2] -join ',')), ((@($n[3..($n.Count - 1)]) + 'none') -join ','))
}

function Format-PaperQaLaneList {
    <#
    .SYNOPSIS
    What qa.ps1 lanes prints (B.2): the scope, one numbered line per lane that applies with its cost, one
    line per lane that does not apply or is turned off, the verify readers, the total and the option groups.
    ExitCode 5 when no lane applies.
    #>
    param([string] $Mode, [string] $Reason, [int] $FileCount, [int] $ExcludedCount, $Estimate, [string] $RepoRoot = '', [string[]] $ScopeLines = @())
    $out = @("qa-lanes: scope $Mode ($Reason) - $FileCount file(s), $ExcludedCount excluded")
    if ($RepoRoot) { $out += "repo: $RepoRoot (external, read only)" }
    # The base: and changes: lines of an external branch scope (ADR-0037), right after the repo line.
    $out += @($ScopeLines | Where-Object { $_ })
    $rows = @($Estimate.Rows | Where-Object { $null -ne $_ -and $_.Name -ne 'verify' })
    $run = @($rows | Where-Object { $_.State -eq 'run' })
    $i = 0
    foreach ($r in $run) {
        $i++
        if ($r.Name -eq 'static') { $out += "$i. static - free - $($r.Files) file(s), a build with the analyzers, no agent"; continue }
        $line = "$i. $($r.Name) - $(Format-PaperQaNumber $r.Tokens) tokens - $($r.Files) file(s), $($r.Agents) agent(s)"
        if ($r.Reason) { $line += ", $($r.Reason)" }
        $out += $line
    }
    foreach ($r in @($rows | Where-Object { $_.State -ne 'run' })) { $out += "$($r.State): $($r.Name) - $($r.Reason)" }
    $verify = @($Estimate.Rows | Where-Object { $null -ne $_ -and $_.Name -eq 'verify' })
    if ($verify.Count -gt 0 -and "$($verify[0].State)" -ne '0') {
        $out += "verify: $($verify[0].State) reader(s), $(Format-PaperQaNumber $verify[0].Tokens) tokens - once, when an agent lane runs"
    }
    if ($run.Count -eq 0) {
        $out += 'qa: NOT APPLICABLE - no lane applies to this scope'
        return [pscustomobject]@{ Lines = $out; ExitCode = 5 }
    }
    $names = @($run | ForEach-Object { "$($_.Name)" })
    if ([long] $Estimate.Total -gt 0) { $out += "all: $(Format-PaperQaNumber $Estimate.Total) tokens - $($names -join ', ')" }
    else { $out += "all: free - $($names -join ', ')" }
    $ask = @(Get-PaperQaLaneAsk -Names $names)
    if ($ask.Count -gt 0) { $out += 'ask: ' + (@($ask | ForEach-Object { $_ -replace ',', ', ' }) -join ' | ') }
    else { $out += 'ask: none - one lane applies, nothing to choose' }
    return [pscustomobject]@{ Lines = $out; ExitCode = 0 }
}

function Resolve-PaperQaLanePick {
    <#
    .SYNOPSIS
    An answer to the lane question (B.3): numbers of the list, lane names or all, separated by commas or
    spaces, to the lanes in lane order and the plan parameter that runs them. The first wrong token is the
    Error, named (F100); numbers, names and all pick the same lanes (F105).
    #>
    param([string[]] $Pick, $Lanes)
    $fail = { param([string] $e) return [pscustomobject]@{ Error = $e; Lanes = @(); Param = '' } }
    $all = @($Lanes | Where-Object { $null -ne $_ -and $_.Name -ne 'verify' })
    $run = @($all | Where-Object { $_.State -eq 'run' } | ForEach-Object { "$($_.Name)" })
    $count = $run.Count
    if ($count -eq 0) { return & $fail 'no lane applies to this scope' }
    $tokens = @(@($Pick) | ForEach-Object { "$_" -split '[,\s]+' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($tokens.Count -eq 0) { return & $fail '-Pick names no lane - give numbers from the list, lane names or all' }
    $picked = @()
    foreach ($t in $tokens) {
        if ($t -eq 'all') { $picked += $run; continue }
        if ($t -match '^\d+$') {
            $n = 0
            if (-not [int]::TryParse($t, [ref] $n) -or $n -lt 1 -or $n -gt $count) { return & $fail "$t is not on the list (1-$count)" }
            $picked += $run[$n - 1]
            continue
        }
        if ($script:PaperQaLaneNames -contains $t) {
            if ($run -contains $t) { $picked += $t; continue }
            $row = @($all | Where-Object { $_.Name -eq $t })[0]
            return & $fail "$t is $($row.State) here - $($row.Reason)"
        }
        return & $fail "'$t' is not a number on the list, a lane or all"
    }
    $lanes = @($script:PaperQaLaneNames | Where-Object { $picked -contains $_ })
    $param = ''
    if ($lanes.Count -eq $count) { $param = '' }
    elseif ($lanes.Count -eq 1 -and $lanes[0] -eq 'static') { $param = '-StaticOnly' }
    else { $param = '-Only ' + ($lanes -join ',') }
    return [pscustomobject]@{ Error = ''; Lanes = $lanes; Param = $param }
}

# One argument of a command line: kept as it is when it needs no quotes, else in single quotes.
function ConvertTo-PaperQaArg {
    param([string] $Value)
    if ($Value -match '^[A-Za-z0-9_./\\:=,+-]+$') { return $Value }
    return "'" + $Value.Replace("'", "''") + "'"
}

function Format-PaperQaPickLines {
    <#
    .SYNOPSIS
    The two lines of qa.ps1 lanes -Pick (B.4): the lanes and the scope, and the exact plan command line -
    the scope parameters as typed, in order, then the lane parameter (F101).
    #>
    param($Pick, [string] $Mode, [string] $Reason, $Arguments)
    $line = 'plan: qa.ps1 plan'
    if ($null -ne $Arguments) {
        foreach ($k in @($Arguments.Keys)) { $line += " -$k $(ConvertTo-PaperQaArg ([string] $Arguments[$k]))" }
    }
    if ($Pick.Param) { $line += " $($Pick.Param)" }
    return @("qa-pick: $(@($Pick.Lanes) -join ', ') (scope $Mode - $Reason)", $line)
}
