# The plans a new session should know about: approved, with a task still open (ADR-0023).
#
# One plan per session is the cheapest cut of context the kit can make: a session that runs from the spec
# through the approval into /task-do and on to the next job reads its whole history on every call. The plan
# is already the hand-over - status, ticked tasks, evidence rows - so a fresh session loses nothing, as long
# as it is told which plan to pick up. session-anchor says it, from this.
#
# Get-PaperOpenPlanNotice -Plans <objects with Path, Written, State, Tasks> [-Max 3]
#   Path     relative, / separators - the form the command is typed in
#   Written  last write time; newest first
#   State    Approved | Done | Pending (Get-PaperPlanStatusLine)
#   Tasks    Get-PaperPlanTasks of an approved plan; any other plan may carry none
# Returns '' when no approved plan has an open task.
#
# Dot-sourced; declares no param() block at script level. ASCII only: PowerShell 5.1 reads a .ps1 without a
# BOM as ANSI.

function Get-PaperOpenPlanNotice {
    param(
        [object[]] $Plans = @(),
        [int] $Max = 3
    )
    $open = @($Plans | Where-Object { $null -ne $_ -and $_.State -eq 'Approved' -and @($_.Tasks | Where-Object { -not $_.Ticked }).Count -gt 0 } |
            Sort-Object -Property Written -Descending)
    if ($open.Count -eq 0) { return '' }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('paper-kit: approved plans with open tasks - one plan per session: run /clear (or open a new session), then:')
    foreach ($plan in @($open | Select-Object -First $Max)) {
        $ids = @($plan.Tasks | Where-Object { -not $_.Ticked } | ForEach-Object { $_.Id })
        $lines.Add("  /task-do $($plan.Path)   (open: $($ids -join ', '))")
    }
    if ($open.Count -gt $Max) { $lines.Add("  (+$($open.Count - $Max) more)") }
    return ($lines -join [Environment]::NewLine)
}
