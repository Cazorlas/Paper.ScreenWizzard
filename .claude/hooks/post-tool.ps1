# PostToolUse, matcher Edit|Write: the one process that runs every PostToolUse script the tool needs - the
# architecture guards (layer-guard, no-static-host-state) and memory-nag for a file edit. The table is
# hook-dispatch-plan.ps1; the body is hook-dispatch.ps1. Why one process: ADR-0021.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$DispatchEvent = 'PostToolUse'
. (Join-Path $PSScriptRoot 'hook-dispatch.ps1')
# An exit inside a dot-sourced file may end only that file; carry its code out of this process.
exit $global:LASTEXITCODE
