# Stop: code written this session inside the files of a task nobody has ticked.
#
# A task is ticked, with its evidence row, the moment it passes. Nothing enforced "the moment": the task
# gate speaks only when someone runs it, so ticks were batched at the end or forgotten, and code was written
# with no plan at all - a real project's user caught both. The first version of this hook then reminded on
# the clock alone, "code is newer than the approved plan", and fired four times in one session on work no
# open task covered. So the reminder now follows the file scope the plan already declares.
#
# When a source file (codeMap.extensions, default .cs .xaml .ps1 .py .ts) was written this session, under
# the profile's featureDocs folder (default docs/features) it reminds once per state:
#   - an approved plan has an unticked task whose {files:} covers one of those files, and the plan itself
#     was last written before that code: tick that task, by its id, and add its evidence
#   - none approved, one waiting for approval written within two days of the code: the code waits for the OK
#   - none at all: every code change has a plan, or say in one line it was tooling asked for directly
# Nothing else is said: a changed file no open task claims is left to the gate that owns the file scope.
#
# The decision is Get-PaperPlanNagVerdict in plan-nag-plan.ps1; this file only reads. The tasks are read
# by the gate's own Get-PaperPlanTasks (paperflow/tasks-gate-plan.ps1) - under the Tasks heading, outside
# a code block - so the reminder and the gate cannot drift apart; without that file there is no reading a
# plan, and the hook exits 0.
#
# A project with no featureDocs folder keeps no plans: quiet.
# stop_hook_active exits 0. Unreadable input, no stamp, any error: exit 0. PAPER_SKIP_PLAN_NAG=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')
    $gatePlan = Join-Path $PSScriptRoot '..\paperflow\tasks-gate-plan.ps1'
    if (-not (Test-Path -LiteralPath $gatePlan -PathType Leaf)) { exit 0 }
    . $gatePlan
    . (Join-Path $PSScriptRoot 'plan-nag-plan.ps1')

    if ($env:PAPER_SKIP_PLAN_NAG -eq '1') { exit 0 }
    $payload = Read-PaperHookPayload
    if ($null -eq $payload -or $payload.stop_hook_active) { exit 0 }

    $since = Get-PaperSessionStart ([string] $payload.session_id) -Create
    if ($null -eq $since) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $profileMap = Read-PaperProfile $root
    if ($null -eq $profileMap) { exit 0 }
    $extensions = ConvertTo-PaperExtensions (Get-PaperProfileValue $profileMap @('codeMap', 'extensions')) @('.cs', '.xaml', '.ps1', '.py', '.ts')

    # Relative with / separators: the form a {files:} glob is written in.
    $changed = New-Object System.Collections.Generic.List[string]
    $lastCode = $null
    foreach ($file in (Get-PaperChangedFiles $root $since $extensions)) {
        $changed.Add((Get-PaperRelative $root $file.FullName))
        if ($null -eq $lastCode -or $file.LastWriteTime -gt $lastCode) { $lastCode = $file.LastWriteTime }
    }
    if ($null -eq $lastCode) { exit 0 }

    $docsRel = [string] (Get-PaperProfileValue $profileMap @('featureDocs'))
    if (-not $docsRel) { $docsRel = 'docs/features' }
    $docs = Join-Path $root ($docsRel.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $docs -PathType Container)) { exit 0 }

    # Only an approved plan is read line by line: the others are judged on their status line and their
    # date, and a Stop hook has no time to open every plan a repository ever wrote.
    $plans = New-Object System.Collections.Generic.List[psobject]
    foreach ($plan in @(Get-ChildItem -LiteralPath $docs -Recurse -File -Filter '*-plan.md' -ErrorAction SilentlyContinue)) {
        $status = Get-PaperPlanStatus $plan.FullName
        if (-not $status) { continue }
        $lines = @()
        if ($status -eq 'Approved') { $lines = @(Get-Content -LiteralPath $plan.FullName -Encoding UTF8) }
        $plans.Add([pscustomobject]@{
                Path    = (Get-PaperRelative $root $plan.FullName)
                Status  = $status
                Written = $plan.LastWriteTime
                Lines   = $lines
            })
    }

    $verdict = Get-PaperPlanNagVerdict -ChangedFiles $changed.ToArray() -LastCodeWrite $lastCode `
        -Plans $plans.ToArray() -DocsRel $docsRel -LaneAliases (Get-PaperProfileValue $profileMap @('laneAliases'))
    if ($null -eq $verdict -or $verdict.ExitCode -eq 0) { exit 0 }

    # The newest code write is part of the key: more code with the same task still unticked is news, a bare
    # re-stop is not.
    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'plan-nag' ($verdict.Text + '|' + $lastCode.ToString('o'))) { exit 0 }
    Write-PaperHookText -ToError $verdict.Text
    exit 2
}
catch {
    exit 0
}
