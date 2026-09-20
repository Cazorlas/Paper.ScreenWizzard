# The tick reminder as a pure function over what the Stop hook already read: the code files this session
# changed, and for each plan under the feature docs its status, when it was last written, and its lines.
# No file system, no clock of its own - the hook hands both in, so every branch below is a test with plain
# data. Dot-sourced; declares no param() block.
#
# Scope decides WHICH plan and WHICH task; the clock decides whether that reminder has been answered:
#   - an approved plan has an unticked task whose {files:} covers a file that changed -> name the plan and
#     that task. A task that declares no {files:} covers everything only in a plan where no task declares
#     one - a plan written before {files:} existed, which still behaves as it always did. In a plan that
#     does declare them, a task without one (the closing group every template ends in) claims no file.
#   - that plan was written after the last code change -> quiet: the tick just happened. Judged per plan,
#     not on the newest plan in the repository, so one busy plan cannot silence another.
#   - no open task claims any of the changed files -> quiet. The file scope already has a gate of its own
#     and the guard that holds it at write time; a second reminder here is only a false alarm, which is
#     what it was: four in one session.
#   - no approved plan at all -> the two older branches: a plan waiting for approval, or none anywhere.
#
# It needs Get-PaperPlanTasks and Test-PaperGlobOverlap from paperflow/tasks-gate-plan.ps1: one reader of
# the plan, the one the gate itself uses - a task counts only under the Tasks heading and outside a code
# block, and a glob compares the same way (leading /, Unicode NFC) on both sides.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Does one task cover any of the files that changed? A task with no {files:} covers everything when its
# plan declares no {files:} anywhere ($BareCoversAll): reading "no scope" as "no match" there would quietly
# stop reminding on every plan written before {files:} existed. In a plan that declares them, the bare
# tasks are the closing group of the template - find-bug, docs, commit - and counting them made every code
# change a reminder again (find-bug, measured).
function Test-PaperTaskCoversChange([string[]] $ChangedFiles, [string[]] $Files, [bool] $BareCoversAll = $true) {
    if (@($Files).Count -eq 0) { return $BareCoversAll }
    foreach ($file in @($ChangedFiles)) {
        foreach ($glob in @($Files)) {
            if (Test-PaperGlobOverlap $file $glob) { return $true }
        }
    }
    return $false
}

function Get-PaperPlanNagVerdict {
    <#
    .SYNOPSIS
    ExitCode (2 = say Text and hold the stop, 0 = quiet) and Text for one stop.
    .PARAMETER ChangedFiles
    Paths of the code files this session changed, relative to the project root, with / separators - the
    form {files:} globs are written in.
    .PARAMETER LastCodeWrite
    When the newest of those files was written.
    .PARAMETER Plans
    One object per plan found under the feature docs: Path (relative, / separators), Status
    (Approved | Pending | Done), Written (when the file was last written), Lines (the file, for an
    approved plan; the other statuses are judged on their status and date alone).
    .PARAMETER DocsRel
    The feature docs folder as the profile spells it, for the "no plan at all" line.
    .PARAMETER LaneAliases
    The profile's laneAliases, passed through to the task reader.
    #>
    param(
        [AllowEmptyString()][string[]] $ChangedFiles = @(),
        [datetime] $LastCodeWrite,
        $Plans = @(),
        [string] $DocsRel = 'docs/features',
        $LaneAliases
    )

    $hits = New-Object System.Collections.Generic.List[psobject]
    $pending = New-Object System.Collections.Generic.List[string]
    $anyApproved = $false

    foreach ($plan in @($Plans)) {
        if ($null -eq $plan) { continue }
        $status = [string] $plan.Status
        if ($status -eq 'Pending') {
            # A plan written days apart from the code is somebody else's work in progress, not this one's.
            if ($plan.Written -ge $LastCodeWrite.AddDays(-2)) { $pending.Add([string] $plan.Path) }
            continue
        }
        if ($status -ne 'Approved') { continue }
        $anyApproved = $true

        # The tasks the gate would read: under the Tasks heading, outside a code block. A task line quoted
        # as an example in Decisions is text to the gate, and was a false reminder here.
        $entries = @(Get-PaperPlanTasks ([string[]] @($plan.Lines)) $LaneAliases)
        $bareCoversAll = @($entries | Where-Object { @($_.Files).Count -gt 0 }).Count -eq 0
        $ids = New-Object System.Collections.Generic.List[string]
        foreach ($entry in $entries) {
            if ($entry.Ticked -or $ids.Contains($entry.Id)) { continue }
            if (Test-PaperTaskCoversChange $ChangedFiles $entry.Files $bareCoversAll) { $ids.Add($entry.Id) }
        }
        if ($ids.Count -eq 0) { continue }
        if ($plan.Written -ge $LastCodeWrite) { continue }
        $hits.Add([pscustomobject]@{ Path = [string] $plan.Path; Ids = @($ids) })
    }

    if ($hits.Count -gt 0) {
        $rows = @($hits | Sort-Object Path | ForEach-Object { '    ' + $_.Path + ' - ' + ($_.Ids -join ', ') })
        $first = @($hits | Sort-Object Path)[0].Ids[0]
        $text = "Code changed this session in the files of a task nobody has ticked:`n`n" + ($rows -join "`n") + @"


Tick that task (- [x] $first) and add its evidence row the moment it passes - not at the end.
If it is not finished yet, say which one is in progress in one line and stop.
"@
        return [pscustomobject]@{ ExitCode = 2; Text = $text }
    }

    if ($anyApproved) { return [pscustomobject]@{ ExitCode = 0; Text = '' } }

    if ($pending.Count -gt 0) {
        $text = "Code changed this session while its plan is not approved yet:`n`n    " + (($pending | Sort-Object) -join "`n    ") + @"


Code for that plan waits for the user's OK on SPEC.md and the plan (/task-do asks the gate first).
If this code is not part of it - tooling asked for directly - say what it was in one line and stop.
"@
        return [pscustomobject]@{ ExitCode = 2; Text = $text }
    }

    $text = @"
Code changed this session with no approved plan under $DocsRel.

Every code change has SPEC.md, a brief and a plan (/task-spec). If this change was asked for directly as
tooling with no plan, say that in one line and stop.
"@
    return [pscustomobject]@{ ExitCode = 2; Text = $text }
}
