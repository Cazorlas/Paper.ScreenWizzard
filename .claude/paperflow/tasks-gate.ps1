# The I/O half of the task gate: read one plan file and report the verdict. Every decision is
# Get-PaperTaskGateVerdict in tasks-gate-plan.ps1, which is pure and fully tested.
#
# THIS FILE DECLARES NO param() BLOCK, ON PURPOSE. It is meant to be dot-sourced, and dot-sourcing runs
# the param block in the CALLER's scope with no arguments - which silently blanked paperflow.ps1's own
# -Path, so `paperflow tasks -Path <file>` answered "tasks needs -Path" while holding the path.
# Entry point: `paperflow.ps1 tasks -Path <plan file>` (docs/features/<slug>/YYYY-MM-DD-<task>-plan.md). To call it directly:
#   powershell -Command ". .\tasks-gate.ps1; exit (Invoke-PaperTasksGate -Path <plan file>)"
#
# Exit: 0 done | 1 open task or a tick with no evidence | 2 malformed | 4 not approved

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tasks-gate-plan.ps1')

function Invoke-PaperTasksGate {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        $ProjectProfile,
        [string[]] $Lanes = @(),
        # Both default to the profile's own keys; pass them to judge a plan against a profile you hold in hand.
        $WorkTypes,
        $LaneAliases
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        [Console]::Error.WriteLine("tasks: no such plan file: $Path")
        return 2
    }

    # -Encoding UTF8 is not optional: PowerShell 5.1's Get-Content defaults to the ANSI codepage, and a
    # plan whose status line reads "da duyet" in Vietnamese would arrive as mojibake and never match.
    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)

    $lanes = @($Lanes)
    if ($lanes.Count -eq 0 -and $null -ne $ProjectProfile) {
        $lanes = @($ProjectProfile['lanes']) | Where-Object { $_ }
    }

    if ($null -eq $WorkTypes -and $null -ne $ProjectProfile) { $WorkTypes = Get-PaperMapValue $ProjectProfile 'workTypes' }
    if ($null -eq $LaneAliases -and $null -ne $ProjectProfile) { $LaneAliases = Get-PaperMapValue $ProjectProfile 'laneAliases' }

    $verdict = Get-PaperTaskGateVerdict -Lines $lines -Lanes $lanes -WorkTypes $WorkTypes -LaneAliases $LaneAliases
    $label = switch ($verdict.ExitCode) {
        0 { 'DONE' }
        1 { 'OPEN' }
        2 { 'MALFORMED' }
        4 { 'NOT APPROVED' }
        default { "EXIT $($verdict.ExitCode)" }
    }
    # [Console]::Out, not Write-Output: the caller runs this as `exit (Invoke-PaperTasksGate ...)`, and
    # anything written to the success stream would be collected into that return value instead of printed.
    [Console]::Out.WriteLine("tasks: $label - $($verdict.Reason)")
    [Console]::Out.WriteLine("       $Path")
    return $verdict.ExitCode
}

