# PreToolUse Edit|Write: source code written before the host was measured.
#
# For a code task on a host that can check the change itself (the profile declares live.loop), the lane
# order is: ensure the host -> measure live, recorded as a baseline evidence row -> red unit test written
# from that number -> green -> verify live again. The decision is Get-PaperLiveFirstVerdict in
# live-first-guard-plan.ps1; this file only reads the edited path, the profile and the approved plans.
#
# Non-blocking by default: the reminder goes to the agent as additionalContext and the edit goes ahead,
# once per plan task in a session. live.enforceOrder true in the profile turns it into a block (exit 2)
# until the baseline row exists.
#
# Quiet for: no live.loop, a file that is not source code, a file under the feature docs or .claude/, no
# approved code plan claiming the file, a baseline already recorded. Unreadable input, any error: exit 0.
# PAPER_SKIP_LIVE_FIRST=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    if ($env:PAPER_SKIP_LIVE_FIRST -eq '1') { exit 0 }
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')
    $gatePlan = Join-Path $PSScriptRoot '..\paperflow\tasks-gate-plan.ps1'
    if (-not (Test-Path -LiteralPath $gatePlan -PathType Leaf)) { exit 0 }
    . $gatePlan
    . (Join-Path $PSScriptRoot 'plan-nag-plan.ps1')
    . (Join-Path $PSScriptRoot 'live-first-guard-plan.ps1')

    $payload = Read-PaperHookPayload
    if ($null -eq $payload) { exit 0 }
    $path = [string] $payload.tool_input.file_path
    if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $profileMap = Read-PaperProfile $root
    if ($null -eq $profileMap) { exit 0 }
    $liveLoop = [string] (Get-PaperProfileValue $profileMap @('live', 'loop'))
    if (-not $liveLoop) { exit 0 }

    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
    $rel = Get-PaperRelativePath $root $path
    if (-not $rel) { exit 0 }

    $docsRel = [string] (Get-PaperProfileValue $profileMap @('featureDocs'))
    if (-not $docsRel) { $docsRel = 'docs/features' }
    $docs = Join-Path $root ($docsRel.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $docs -PathType Container)) { exit 0 }

    $plans = New-Object System.Collections.Generic.List[psobject]
    foreach ($plan in @(Get-ChildItem -LiteralPath $docs -Recurse -File -Filter '*-plan.md' -ErrorAction SilentlyContinue)) {
        if ((Get-PaperPlanStatus $plan.FullName) -ne 'Approved') { continue }
        $plans.Add([pscustomobject]@{
                Path  = (Get-PaperRelativePath $root $plan.FullName)
                Lines = @(Get-Content -LiteralPath $plan.FullName -Encoding UTF8)
            })
    }
    if ($plans.Count -eq 0) { exit 0 }

    $extensions = ConvertTo-PaperExtensions (Get-PaperProfileValue $profileMap @('codeMap', 'extensions')) @('.cs', '.xaml', '.ps1', '.py', '.ts')
    $enforce = [bool] (Get-PaperProfileValue $profileMap @('live', 'enforceOrder'))
    $verdict = Get-PaperLiveFirstVerdict -File $rel -Plans $plans.ToArray() -LiveLoop $liveLoop -Enforce $enforce `
        -Extensions $extensions -DocsRel $docsRel -LaneAliases (Get-PaperProfileValue $profileMap @('laneAliases'))
    if ($null -eq $verdict -or $verdict.Action -eq 'quiet') { exit 0 }

    if ($verdict.Action -eq 'block') {
        Write-PaperHookText -ToError $verdict.Text
        exit 2
    }
    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'live-first' $verdict.Key) { exit 0 }
    $json = @{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; additionalContext = $verdict.Text } } | ConvertTo-Json -Compress -Depth 4
    Write-PaperHookText $json
    exit 0
}
catch {
    exit 0
}
