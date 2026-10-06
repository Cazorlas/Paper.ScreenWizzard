# Which hook scripts one event runs, as a pure function over the event and tool names. Dot-sourced;
# declares no param() block.
#
# ADR-0021: every PowerShell 5.1 hook is a cold start of 0.7-1.1 s, and four of them started together for
# one shell command took 2.5 s (median, 2026-09-27). So PreToolUse registers one dispatcher (pre-tool.ps1,
# matcher Bash|PowerShell|Edit|Write), PostToolUse one (post-tool.ps1, matcher Edit|Write), and Stop one
# (stop.ps1, no matcher: Stop has no tool_name); this table says what each runs in its own process. A new
# guard for a tool is a row here, not a new entry in settings.fragment.json. harness-event no longer runs
# on a tool event, so Read and Grep open no process at all.
#
# The tool lists are the matchers the guards were registered with before, plus apply_patch - the name
# Codex gives a file edit, which Codex also accepted under the Edit|Write matcher.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperDispatchTable = @(
    @{ Event = 'PreToolUse'; Tools = @('Bash', 'PowerShell'); Scripts = @('destructive-guard.ps1', 'build-guard.ps1', 'test-guard.ps1') },
    @{ Event = 'PreToolUse'; Tools = @('Edit', 'Write', 'apply_patch'); Scripts = @('live-first-guard.ps1') },
    @{ Event = 'PostToolUse'; Tools = @('Edit', 'Write', 'apply_patch'); Scripts = @('layer-guard.ps1', 'no-static-host-state.ps1', 'memory-nag.ps1') },
    @{ Event = 'Stop'; Tools = @(); Scripts = @('code-map-nag.ps1', 'plan-nag.ps1', 'link-nag.ps1', 'verify-on-stop.ps1', 'harness-event.ps1') }
)

# The scripts one event runs for one tool, in table order. An empty Tools list means every tool.
function Get-PaperHookDispatch([string] $EventName, [string] $ToolName) {
    $scripts = New-Object System.Collections.Generic.List[string]
    foreach ($row in $script:PaperDispatchTable) {
        if ($row.Event -ne $EventName) { continue }
        if (@($row.Tools).Count -gt 0 -and @($row.Tools) -notcontains $ToolName) { continue }
        foreach ($s in $row.Scripts) { if (-not $scripts.Contains($s)) { $scripts.Add($s) } }
    }
    return , @($scripts)
}

# The dispatcher's own exit code from the codes its scripts left: 2 when any of them blocked, 0 for every
# other outcome - a script that failed or threw lets the tool call through (F10).
function Get-PaperDispatchExit([int[]] $Codes) {
    if (@($Codes) -contains 2) { return 2 }
    return 0
}

# The scripts that dispatchers replaced under every event, as registered in kit 1.33.0 and before.
# settings.fragment.json lists them under retiredHooks.anyEvent so setup takes the old entries out of a
# project's settings; the hooks test holds the two lists equal.
$script:PaperRetiredHooks = @('destructive-guard.ps1', 'build-guard.ps1', 'test-guard.ps1', 'live-first-guard.ps1', 'layer-guard.ps1', 'no-static-host-state.ps1')
