# PostToolUse on Edit|Write|apply_patch: a Claude Code memory file that grew into a diary, cites a path that
# is gone, holds something shaped like a secret (F77), or an index (MEMORY.md) that is costly, out of step
# with its folder or lists one file twice (F78). Claude and Codex write the same memory (ADR-0032).
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
    # A Codex apply_patch can touch several files: each memory file in it is checked as its own write.
    $pathsProperty = $payload.tool_input.PSObject.Properties['file_paths']
    $paths = if ($pathsProperty -and $pathsProperty.Value) { @($pathsProperty.Value) } else { @([string] $payload.tool_input.file_path) }

    $said = New-Object System.Collections.Generic.List[string]
    foreach ($path in $paths) {
        $path = [string] $path
        if (-not (Get-PaperMemoryFileName $path)) { continue }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }

        $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
        $dir = Split-Path -Parent $path
        $files = @(Get-ChildItem -LiteralPath $dir -Filter '*.md' -File | Where-Object { $_.Name -ne 'MEMORY.md' } | ForEach-Object { $_.Name })
        $missing = @(Get-PaperMemoryPaths -Text $text | Where-Object { -not (Test-Path -LiteralPath $_) })

        $verdict = Get-PaperMemoryNagVerdict -Path $path -Text $text -Files $files -MissingPaths $missing
        if ($verdict.ExitCode -ne 0) { $said.Add($verdict.Text) }
    }
    if ($said.Count -eq 0) { exit 0 }
    $all = $said -join "`n`n"
    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'memory-nag' $all) { exit 0 }
    Write-PaperHookText -ToError $all
    exit 2
}
catch {
    exit 0
}
