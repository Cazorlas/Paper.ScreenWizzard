# PreToolUse on Bash|PowerShell: a command that destroys what cannot be brought back.
#
# Every project gets the generic rules: a forced `git clean`, `git reset --hard`, and a recursive delete of
# the project root (or a folder above it) or of .git. Each throws away work the session may not have
# created, and git cannot restore it. A host adds its own in .claude/paper.profile.json ->
# guards.destructive: [ { "pattern": regex, "reason": why, and what to do } ] - closing the running host with
# unsaved documents, deleting a deployed bundle to clear a file lock.
#
# A profile pattern is tried on every segment and on every pipeline (segments joined by " | "), so
# `Get-Process hostapp | Stop-Process` can be caught as well as `Stop-Process -Name hostapp`; quoted text that is
# not one word or a path is emptied first, and a quoted command after powershell -Command / bash -c is checked
# on its own (conventions: guard-plan.ps1). Built-in rules read the segment with every quoted string emptied,
# so a commit message that mentions a reset is not a reset. Exit 2 sends the reason to the agent. Unreadable
# input: exit 0.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'guard-plan.ps1')

    $payload = Read-PaperHookPayload
    if ($null -eq $payload -or $null -eq $payload.tool_input) { exit 0 }
    $command = [string] $payload.tool_input.command
    if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

    $projectDir = Get-PaperHookProjectDir $payload
    $cwd = $null
    if ($payload.cwd -and (Test-Path -LiteralPath ([string] $payload.cwd) -PathType Container)) { $cwd = [string] $payload.cwd }
    $rules = Get-PaperProfileValue (Read-PaperProfile $projectDir) @('guards', 'destructive')

    $hit = Get-PaperDestructiveHit $command $rules $projectDir $cwd
    if ($null -eq $hit) { exit 0 }

    Write-PaperHookText -ToError @"
BLOCKED: this command destroys data that cannot be brought back.

    $($hit.Segment)

$($hit.Reason)

If the user asked for exactly this, ask them to run it themselves. (Project rules: .claude/paper.profile.json
-> guards.destructive.)
"@
    exit 2
}
catch {
    exit 0
}
