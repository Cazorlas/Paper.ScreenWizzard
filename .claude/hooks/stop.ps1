# Stop, no matcher: the one process that runs every Stop script - code-map-nag, plan-nag, link-nag,
# verify-on-stop, harness-event, in that order. Stop has no tool_name; the dispatcher reads it as empty.
# The table is hook-dispatch-plan.ps1; the body is hook-dispatch.ps1. Why one process: ADR-0021.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$DispatchEvent = 'Stop'
. (Join-Path $PSScriptRoot 'hook-dispatch.ps1')
# An exit inside a dot-sourced file may end only that file; carry its code out of this process.
exit $global:LASTEXITCODE
