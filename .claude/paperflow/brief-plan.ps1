# What a lane agent is handed for its tasks, and nothing more (ADR-0023, F28).
#
# Measured 2026-09-28 on one project: the prompt a lane got was already small (~1.2k tokens), but the lane
# then read the whole plan, the whole SPEC.md and more on its own, and ran to 160-300k tokens of context.
# So the brief is built here, from the plan and SPEC.md, and the lane is told to work from it:
#   - the task lines as written, and each task's {files:}
#   - the rows of SPEC.md's "When it does not do the job" table for every F code the tasks name; a code the
#     table does not have is named as missing, so the lane reports F28 instead of reading SPEC.md whole
#   - the plan's wireframe section when a task is ui or e2e
#   - the evidence rows that say baseline when a task is [red]: the number its red test is written from
# The "Given ... ->" acceptance lines a task covers are chosen by the main session and pasted beside this:
# a task names its F codes, not its acceptance lines, so finding those here would be a guess.
#
# Get-PaperTaskBrief -PlanLines <string[]> -SpecLines <string[]> -TaskIds <string[]> [-PlanPath <text>]
#                    [-LaneAliases <map>]
# Returns ExitCode (0, or 2 for an id the plan does not have) and Text.
#
# Needs tasks-gate-plan.ps1 dot-sourced first (Get-PaperPlanOutline, Get-PaperPlanTasks, Get-PaperPlainText, $script:PaperNotVerifiableReasons).
# Dot-sourced; declares no param() block at script level. ASCII only: PowerShell 5.1 reads a .ps1 without a
# BOM as ANSI.

function Get-PaperTaskBrief {
    param(
        [AllowEmptyString()][string[]] $PlanLines = @(),
        [AllowEmptyString()][string[]] $SpecLines = @(),
        [string[]] $TaskIds = @(),
        [string] $PlanPath = '',
        $LaneAliases
    )
    $nl = [Environment]::NewLine
    $ids = @($TaskIds | ForEach-Object { ([string] $_).Trim() } | Where-Object { $_ })
    if ($ids.Count -eq 0) { return [pscustomobject]@{ ExitCode = 2; Text = 'brief: no task id given (-Task T1,T2)' } }

    $all = @(Get-PaperPlanTasks $PlanLines $LaneAliases)
    $tasks = New-Object System.Collections.Generic.List[psobject]
    foreach ($id in $ids) {
        $t = $all | Where-Object { $_.Id -eq $id } | Select-Object -First 1
        if ($null -eq $t) {
            return [pscustomobject]@{ ExitCode = 2; Text = "brief: task $id is not in the plan's Tasks ($(@($all | ForEach-Object { $_.Id }) -join ', '))" }
        }
        $tasks.Add($t)
    }

    # The task lines as written: the outline's own rows, so a fenced example is never taken for a task.
    $outline = @(Get-PaperPlanOutline $PlanLines)
    $taskLines = @{}
    foreach ($row in $outline) {
        if ($row.Section -ne 'tasks' -or $row.Heading -or $row.Fenced) { continue }
        if ($row.Line -match '^\s*[-*]\s*\[( |x|X)\]\s*(T\d+[a-z]?)\b' -and -not $taskLines.ContainsKey($Matches[2])) { $taskLines[$Matches[2]] = $row.Line.Trim() }
    }

    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("brief for $($ids -join ', ') - plan $PlanPath")
    $out.Add('Work from this brief. Do not read the whole plan or SPEC.md; a line you need that is not here: report "not verifiable (brief-lacks: <line>)" (F28).')
    $out.Add('')
    $out.Add('## Tasks')
    foreach ($t in $tasks) { $out.Add($taskLines[$t.Id]) }
    $out.Add('')
    $out.Add('## Files each task may write')
    foreach ($t in $tasks) {
        $files = if (@($t.Files).Count -gt 0) { @($t.Files) -join ', ' } else { '(none declared)' }
        $out.Add("$($t.Id): $files")
    }

    # F codes the tasks name, in order of first mention.
    $codes = New-Object System.Collections.Generic.List[string]
    foreach ($t in $tasks) {
        foreach ($m in [regex]::Matches([string] $t.Text, '\bF(\d+)\b')) {
            $code = 'F' + $m.Groups[1].Value
            if (-not $codes.Contains($code)) { $codes.Add($code) }
        }
    }
    if ($codes.Count -gt 0) {
        $out.Add('')
        $out.Add('## Failure modes (SPEC.md)')
        foreach ($code in $codes) {
            $row = @($SpecLines | Where-Object { ([string] $_) -match ('^\s*\|\s*' + $code + '\s*\|') }) | Select-Object -First 1
            if ($row) { $out.Add(([string] $row).Trim()) } else { $out.Add("$code - not in SPEC.md") }
        }
    }

    # The wireframe section, for a ui or e2e task: its heading down to the next heading of its level or
    # higher, fenced drawing included.
    if (@($tasks | Where-Object { $_.Lane -eq 'ui' -or $_.Lane -eq 'e2e' }).Count -gt 0) {
        $inside = $false; $level = 0
        $block = New-Object System.Collections.Generic.List[string]
        foreach ($row in $outline) {
            if ($row.Heading -and -not $row.Fenced) {
                $plain = Get-PaperPlainText $row.Line
                $hl = if ($plain -match '^(#+)') { $Matches[1].Length } else { 0 }
                if ($inside -and $hl -le $level) { break }
                if (-not $inside -and $plain -match 'wireframe') { $inside = $true; $level = $hl }
            }
            if ($inside) { $block.Add($row.Line) }
        }
        if ($block.Count -gt 0) {
            $out.Add('')
            foreach ($l in $block) { $out.Add($l) }
            while ($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count - 1])) { $out.RemoveAt($out.Count - 1) }
        }
    }

    # The baseline rows, for a [red] task: the measured number its red test is written from.
    if (@($tasks | Where-Object { $_.Red }).Count -gt 0) {
        $rows = @($outline | Where-Object { $_.Section -eq 'evidence' -and -not $_.Heading -and -not $_.Fenced -and $_.Line -match '^\s*\|' -and $_.Line -match '\bbaseline\b' } | ForEach-Object { $_.Line.Trim() })
        if ($rows.Count -gt 0) {
            $out.Add('')
            $out.Add('## Baseline rows (evidence)')
            foreach ($r in $rows) { $out.Add($r) }
        }
    }

    $out.Add('')
    $out.Add('Hand back only: exactly one evidence row per task (| T<n> | command / id / value read back | verdict |), your branch and commit, the files you changed. No logs. A task with no row is not verifiable (no-report) and is handed out again once (F34).')
    # The reason codes come from the gate's one list (tasks-gate-plan.ps1), so a lane and the gate cannot disagree.
    $out.Add("A verdict that is not pass or fail reads ""not verifiable (<code>: <detail>)"", one code of: $($script:PaperNotVerifiableReasons -join ', ').")
    $out.Add('A command or host call that times out twice at the same step: stop that task with "not verifiable (timeout: <step>)" - never a third try (F36).')
    return [pscustomobject]@{ ExitCode = 0; Text = ($out -join $nl) }
}
