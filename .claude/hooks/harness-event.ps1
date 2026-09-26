# Claude/Codex adapter for the Paper-owned harness. Observability is fail-open: a broken recorder must
# never block the host agent or change a Paper guard decision.
$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    $isClaude = [bool]$env:CLAUDE_PROJECT_DIR
    $payload = Read-PaperHookPayload
    if ($null -eq $payload) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $actor = if ($isClaude) { 'claude' } else { 'codex' }
    $session = [string]$payload.session_id
    if (-not $session) { $session = 'no-session' }
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $key = -join ($hash.ComputeHash((New-Object Text.UTF8Encoding $false).GetBytes("$actor`n$session")) | ForEach-Object { $_.ToString('x2') }) }
    finally { $hash.Dispose() }
    $stateDir = Join-Path $root '.paper\harness\sessions'
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $statePath = Join-Path $stateDir ($key.Substring(0, 32) + '.json')
    $harness = Join-Path $root '.paper\harness\harness.ps1'
    if (-not (Test-Path -LiteralPath $harness -PathType Leaf)) { exit 0 }

    $state = $null
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        try { $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $state = $null }
    }
    if ($null -eq $state -or -not $state.runId) {
        $startArgs = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $harness, 'start', '-ProjectRoot', $root, '-Actor', $actor, '-SessionId', $session)
        $runId = (& powershell.exe @startArgs 2>$null | ForEach-Object { "$_" } | Select-Object -Last 1)
        if (-not $runId) { exit 0 }
        $state = [ordered]@{ runId = $runId; actor = $actor; sessionId = $session }
        [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Compress) + "`n", (New-Object Text.UTF8Encoding $false))
    }

    $json = $payload | ConvertTo-Json -Depth 32 -Compress
    $eventArgs = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $harness, 'event', '-ProjectRoot', $root, '-RunId', ([string]$state.runId))
    $json | & powershell.exe @eventArgs *> $null

    if ([string]$payload.hook_event_name -eq 'SessionEnd') {
        $finishArgs = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $harness, 'finish', '-ProjectRoot', $root, '-RunId', ([string]$state.runId), '-Status', 'observed')
        & powershell.exe @finishArgs *> $null
        Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
    }
    exit 0
}
catch { exit 0 }
