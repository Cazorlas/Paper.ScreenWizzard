param(
    [Parameter(Position = 0, Mandatory = $true)][ValidateSet('start', 'event', 'finish', 'recover', 'summarize')][string] $Command,
    [Parameter(Mandatory = $true)][string] $ProjectRoot,
    [string] $RunId,
    [string] $Actor = 'script',
    [string] $SessionId = '',
    [string] $TaskId = '',
    [string] $ParentTaskId = '',
    [string] $Lane = '',
    [string] $Status = 'succeeded',
    [string] $PayloadJson = ''
)

$ErrorActionPreference = 'Stop'
$script:Utf8 = New-Object System.Text.UTF8Encoding $false
$script:MaxPreview = 240

function Get-PaperHarnessRoot([string] $Root) {
    $resolved = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).ProviderPath.TrimEnd('\')
    $harness = Join-Path $resolved '.paper\harness'
    New-Item -ItemType Directory -Force -Path (Join-Path $harness 'runs') | Out-Null
    return $harness
}

function Get-PaperHarnessNow() { return [DateTime]::UtcNow.ToString('o') }

function Get-PaperHarnessRunId() {
    return ('{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'), [guid]::NewGuid().ToString('N').Substring(0, 8))
}

function Get-PaperHarnessJson($Value) {
    return ($Value | ConvertTo-Json -Depth 32 -Compress)
}

function Get-PaperHarnessMutexName([string] $Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $script:Utf8.GetBytes($Path.ToLowerInvariant())
        return 'Local\PaperHarness-' + (-join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })).Substring(0, 24)
    }
    finally { $sha.Dispose() }
}

function Use-PaperHarnessLock([string] $Path, [scriptblock] $Action) {
    $mutex = New-Object System.Threading.Mutex($false, (Get-PaperHarnessMutexName $Path))
    $held = $false
    try {
        $held = $mutex.WaitOne([TimeSpan]::FromSeconds(15))
        if (-not $held) { throw "harness lock timed out: $Path" }
        & $Action
    }
    finally {
        if ($held) { $mutex.ReleaseMutex() | Out-Null }
        $mutex.Dispose()
    }
}

function Write-PaperHarnessText([string] $Path, [string] $Text) {
    [IO.File]::WriteAllText($Path, $Text, $script:Utf8)
}

function Add-PaperHarnessLine([string] $EventsPath, $Event) {
    Use-PaperHarnessLock $EventsPath {
        $line = (Get-PaperHarnessJson $Event) + "`n"
        [IO.File]::AppendAllText($EventsPath, $line, $script:Utf8)
    }
}

function Read-PaperHarnessRun([string] $RunDir) {
    $path = Join-Path $RunDir 'run.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "unknown harness run: $RunDir" }
    return (Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Limit-PaperHarnessText([string] $Value) {
    if ([string]::IsNullOrEmpty($Value)) { return $null }
    if ($Value.Length -le $script:MaxPreview) { return $Value }
    return $Value.Substring(0, $script:MaxPreview) + '...'
}

function Get-PaperHarnessStatus($Payload) {
    if ($null -ne $Payload.tool_response -and $null -ne $Payload.tool_response.exit_code) {
        if ([int]$Payload.tool_response.exit_code -eq 0) { return 'succeeded' }
        return 'failed'
    }
    if ($Payload.hook_event_name -in @('PermissionDenied', 'StopFailure')) { return 'failed' }
    return 'observed'
}

function Get-PaperHarnessEventType($Payload) {
    switch ([string]$Payload.hook_event_name) {
        'SessionStart' { return 'session_started' }
        'SessionEnd' { return 'session_finished' }
        'UserPromptSubmit' { return 'prompt_submitted' }
        'PreToolUse' { return 'tool_started' }
        'PostToolUse' { return 'tool_finished' }
        'SubagentStart' { return 'agent_started' }
        'SubagentStop' { return 'agent_finished' }
        'TaskCreated' { return 'task_started' }
        'TaskCompleted' { return 'task_finished' }
        'Stop' { return 'turn_finished' }
        'PermissionDenied' { return 'permission_denied' }
        default { return 'host_event' }
    }
}

function Get-PaperHarnessEvent([string] $RunDir, $Payload) {
    $run = Read-PaperHarnessRun $RunDir
    $toolName = if ($Payload.tool_name) { [string]$Payload.tool_name } elseif ($Payload.commandOrTool) { [string]$Payload.commandOrTool } else { $null }
    $duration = $null
    if ($Payload.duration_ms) { $duration = [int]$Payload.duration_ms }
    elseif ($Payload.tool_response -and $Payload.tool_response.duration_ms) { $duration = [int]$Payload.tool_response.duration_ms }
    $extensions = [ordered]@{}
    foreach ($name in @('hook_event_name', 'tool_use_id', 'agent_id', 'agent_type', 'reason', 'permission_mode', 'turn_id')) {
        if ($null -ne $Payload.$name -and "$($Payload.$name)" -ne '') { $extensions[$name] = Limit-PaperHarnessText "$($Payload.$name)" }
    }
    $changed = @()
    if ($Payload.tool_input -and $Payload.tool_input.file_path) { $changed = @([string]$Payload.tool_input.file_path) }
    return [ordered]@{
        schemaVersion = '1'
        eventId = [guid]::NewGuid().ToString()
        timestamp = Get-PaperHarnessNow
        runId = $RunDir | Split-Path -Leaf
        sessionId = if ($Payload.session_id) { [string]$Payload.session_id } else { [string]$run.sessionId }
        taskId = if ($Payload.task_id) { [string]$Payload.task_id } else { [string]$run.taskId }
        parentTaskId = [string]$run.parentTaskId
        lane = [string]$run.lane
        actor = [string]$run.actor
        eventType = Get-PaperHarnessEventType $Payload
        status = Get-PaperHarnessStatus $Payload
        cwd = if ($Payload.cwd) { [string]$Payload.cwd } else { [string]$run.projectRoot }
        worktree = if ($Payload.cwd) { [string]$Payload.cwd } else { [string]$run.projectRoot }
        commandOrTool = $toolName
        exitCode = if ($Payload.tool_response -and $null -ne $Payload.tool_response.exit_code) { [int]$Payload.tool_response.exit_code } else { $null }
        durationMs = $duration
        evidenceRefs = @()
        changedFiles = $changed
        redactionStatus = if ($Payload.tool_input.command -or $Payload.tool_input.content -or $Payload.tool_response.output) { 'redacted' } else { 'clean' }
        extensions = $extensions
    }
}

function Write-PaperHarnessSummary([string] $RunDir, [string] $StatusOverride = '') {
    $run = Read-PaperHarnessRun $RunDir
    $eventsPath = Join-Path $RunDir 'events.jsonl'
    $events = @(Get-Content -LiteralPath $eventsPath -Encoding UTF8 | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
    $existing = $null
    $summaryPath = Join-Path $RunDir 'summary.json'
    if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
        try { $existing = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { $existing = $null }
    }
    $eventCounts = [ordered]@{}
    $changed = New-Object System.Collections.Generic.List[string]
    $agents = New-Object System.Collections.Generic.List[string]
    $tasks = New-Object System.Collections.Generic.List[string]
    $lanes = New-Object System.Collections.Generic.List[string]
    $failureCount = 0; $permissionDeniedCount = 0; $toolFinishedCount = 0; $recovered = $false; $redacted = $false
    $lastEvent = $null; $finishedAt = $null
    foreach ($event in $events) {
        $kind = [string]$event.eventType
        if (-not $eventCounts.Contains($kind)) { $eventCounts[$kind] = 0 }
        $eventCounts[$kind] = [int]$eventCounts[$kind] + 1
        $lastEvent = $kind
        if ($kind -eq 'run_finished') { $finishedAt = [string]$event.timestamp }
        if ($kind -eq 'tool_finished') { $toolFinishedCount++ }
        if ($kind -eq 'permission_denied') { $permissionDeniedCount++; $failureCount++ }
        if ([string]$event.status -eq 'failed') { $failureCount++ }
        if ($kind -eq 'run_recovered') { $recovered = $true }
        if ([string]$event.redactionStatus -eq 'redacted') { $redacted = $true }
        foreach ($file in @($event.changedFiles)) { if ($file -and -not $changed.Contains([string]$file)) { $changed.Add([string]$file) } }
        if ($event.extensions -and $event.extensions.agent_id -and -not $agents.Contains([string]$event.extensions.agent_id)) { $agents.Add([string]$event.extensions.agent_id) }
        if ($event.taskId -and -not $tasks.Contains([string]$event.taskId)) { $tasks.Add([string]$event.taskId) }
        if ($event.lane -and -not $lanes.Contains([string]$event.lane)) { $lanes.Add([string]$event.lane) }
    }
    $status = if ($StatusOverride) { $StatusOverride } elseif ($existing -and $existing.status) { [string]$existing.status } elseif ($eventCounts.Contains('run_finished')) { [string]($events | Where-Object { $_.eventType -eq 'run_finished' } | Select-Object -Last 1).status } else { 'incomplete' }
    $summary = [ordered]@{
        schemaVersion = '1'; runId = (Split-Path $RunDir -Leaf); actor = [string]$run.actor; sessionId = [string]$run.sessionId
        taskId = [string]$run.taskId; status = $status; startedAt = [string]$run.startedAt; finishedAt = $finishedAt
        eventCount = $events.Count; eventsPath = 'events.jsonl'; lastEventType = $lastEvent; redactionStatus = if ($redacted) { 'redacted' } else { 'clean' }
        quality = [ordered]@{
            eventCounts = $eventCounts; toolFinishedCount = $toolFinishedCount; failureCount = $failureCount
            permissionDeniedCount = $permissionDeniedCount; changedFileCount = $changed.Count; changedFiles = @($changed)
            agentIds = @($agents); taskIds = @($tasks); lanes = @($lanes); recovered = $recovered
        }
    }
    $temp = Join-Path $RunDir 'summary.json.tmp'
    Write-PaperHarnessText $temp ((Get-PaperHarnessJson $summary) + "`n")
    Move-Item -LiteralPath $temp -Destination $summaryPath -Force
    return (Get-PaperHarnessJson $summary)
}

function Start-PaperHarnessRun([string] $HarnessRoot) {
    $id = Get-PaperHarnessRunId
    $dir = Join-Path $HarnessRoot (Join-Path 'runs' $id)
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $run = [ordered]@{
        runId = $id; projectRoot = (Split-Path $HarnessRoot -Parent | Split-Path -Parent)
        actor = $Actor; sessionId = if ($SessionId) { $SessionId } else { $id }; taskId = $TaskId
        parentTaskId = $ParentTaskId; lane = $Lane; startedAt = Get-PaperHarnessNow
    }
    Write-PaperHarnessText (Join-Path $dir 'run.json') ((Get-PaperHarnessJson $run) + "`n")
    Add-PaperHarnessLine (Join-Path $dir 'events.jsonl') ([ordered]@{
            schemaVersion = '1'; eventId = [guid]::NewGuid().ToString(); timestamp = $run.startedAt; runId = $id
            sessionId = $run.sessionId; taskId = $TaskId; parentTaskId = $ParentTaskId; lane = $Lane
            actor = $Actor; eventType = 'run_started'; status = 'running'; cwd = $run.projectRoot; worktree = $run.projectRoot
            commandOrTool = $null; exitCode = $null; durationMs = $null; evidenceRefs = @(); changedFiles = @()
            redactionStatus = 'clean'; extensions = @{}
        })
    return $id
}

function Finish-PaperHarnessRun([string] $RunDir) {
    $run = Read-PaperHarnessRun $RunDir
    $eventsPath = Join-Path $RunDir 'events.jsonl'
    $event = [ordered]@{
        schemaVersion = '1'; eventId = [guid]::NewGuid().ToString(); timestamp = Get-PaperHarnessNow
        runId = (Split-Path $RunDir -Leaf); sessionId = [string]$run.sessionId; taskId = [string]$run.taskId
        parentTaskId = [string]$run.parentTaskId; lane = [string]$run.lane; actor = [string]$run.actor
        eventType = 'run_finished'; status = $Status; cwd = [string]$run.projectRoot; worktree = [string]$run.projectRoot
        commandOrTool = $null; exitCode = if ($Status -eq 'succeeded') { 0 } elseif ($Status -eq 'failed') { 1 } else { $null }; durationMs = $null
        evidenceRefs = @(); changedFiles = @(); redactionStatus = 'clean'; extensions = @{}
    }
    Use-PaperHarnessLock $eventsPath {
        [IO.File]::AppendAllText($eventsPath, (Get-PaperHarnessJson $event) + "`n", $script:Utf8)
        Write-PaperHarnessSummary $RunDir $Status | Out-Null
    }
    return (Get-PaperHarnessJson $event)
}

function Recover-PaperHarnessRun([string] $RunDir) {
    $eventsPath = Join-Path $RunDir 'events.jsonl'
    if (-not (Test-Path -LiteralPath $eventsPath -PathType Leaf)) { throw "run has no events.jsonl: $RunDir" }
    $valid = New-Object System.Collections.Generic.List[string]
    foreach ($line in [IO.File]::ReadAllLines($eventsPath, $script:Utf8)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $null = $line | ConvertFrom-Json; $valid.Add($line) }
        catch { break }
    }
    Use-PaperHarnessLock $eventsPath {
        [IO.File]::WriteAllText($eventsPath, (($valid -join "`n") + "`n"), $script:Utf8)
        $run = Read-PaperHarnessRun $RunDir
        $recovery = [ordered]@{ schemaVersion = '1'; eventId = [guid]::NewGuid().ToString(); timestamp = Get-PaperHarnessNow; runId = (Split-Path $RunDir -Leaf); sessionId = [string]$run.sessionId; taskId = [string]$run.taskId; parentTaskId = [string]$run.parentTaskId; lane = [string]$run.lane; actor = [string]$run.actor; eventType = 'run_recovered'; status = 'recovered'; cwd = [string]$run.projectRoot; worktree = [string]$run.projectRoot; commandOrTool = $null; exitCode = $null; durationMs = $null; evidenceRefs = @(); changedFiles = @(); redactionStatus = 'clean'; extensions = @{} }
        [IO.File]::AppendAllText($eventsPath, (Get-PaperHarnessJson $recovery) + "`n", $script:Utf8)
    }
    return 'recovered'
}

try {
    $root = Get-PaperHarnessRoot $ProjectRoot
    switch ($Command) {
        'start' { Write-Output (Start-PaperHarnessRun $root) }
        'event' {
            if (-not $RunId) { throw 'event requires -RunId' }
            if (-not $PayloadJson) { $PayloadJson = [Console]::In.ReadToEnd() }
            if (-not $PayloadJson) { throw 'event requires -PayloadJson or JSON on stdin' }
            $dir = Join-Path $root (Join-Path 'runs' $RunId)
            $payload = $PayloadJson | ConvertFrom-Json
            Add-PaperHarnessLine (Join-Path $dir 'events.jsonl') (Get-PaperHarnessEvent $dir $payload)
            Write-Output 'recorded'
        }
        'finish' {
            if (-not $RunId) { throw 'finish requires -RunId' }
            Write-Output (Finish-PaperHarnessRun (Join-Path $root (Join-Path 'runs' $RunId)))
        }
        'recover' {
            if (-not $RunId) { throw 'recover requires -RunId' }
            Write-Output (Recover-PaperHarnessRun (Join-Path $root (Join-Path 'runs' $RunId)))
        }
        'summarize' {
            if (-not $RunId) { throw 'summarize requires -RunId' }
            $dir = Join-Path $root (Join-Path 'runs' $RunId)
            Use-PaperHarnessLock (Join-Path $dir 'events.jsonl') { Write-PaperHarnessSummary $dir | Out-Null }
            Write-Output 'summarized'
        }
    }
    exit 0
}
catch {
    [Console]::Error.WriteLine("paper-harness: $($_.Exception.Message)")
    exit 2
}
