# PostToolUse on Edit|Write: a Claude Code memory file that grew into a diary, cites a path that is gone,
# or an index (MEMORY.md) that is costly or out of step with its folder.
#
# The decision is Get-PaperMemoryNagVerdict in memory-nag-plan.ps1, with the measurements and the source of
# the rules (hindsight's consolidation, ADR-0028); this file only reads: the file as the agent left it, the
# .md names beside it, and whether each backticked absolute path it cites exists.
#
# Exit 2 shows stderr to the agent after the write; nothing is blocked or undone - memory is the user's.
# Quiet for any file outside ~/.claude/projects/<slug>/memory/, for the same complaint twice in a session,
# and on any error or unreadable input (F10). PAPER_SKIP_MEMORY_NAG=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    if ($env:PAPER_SKIP_MEMORY_NAG -eq '1') { exit 0 }
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')
    . (Join-Path $PSScriptRoot 'memory-nag-plan.ps1')

    $payload = Read-PaperHookPayload
    if ($null -eq $payload) { exit 0 }
    $path = [string] $payload.tool_input.file_path
    if (-not (Get-PaperMemoryFileName $path)) { exit 0 }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }

    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    $dir = Split-Path -Parent $path
    $files = @(Get-ChildItem -LiteralPath $dir -Filter '*.md' -File | Where-Object { $_.Name -ne 'MEMORY.md' } | ForEach-Object { $_.Name })
    $missing = @(Get-PaperMemoryPaths -Text $text | Where-Object { -not (Test-Path -LiteralPath $_) })

    $verdict = Get-PaperMemoryNagVerdict -Path $path -Text $text -Files $files -MissingPaths $missing
    if ($verdict.ExitCode -eq 0) { exit 0 }
    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'memory-nag' $verdict.Text) { exit 0 }
    Write-PaperHookText -ToError $verdict.Text
    exit 2
}
catch {
    exit 0
}
