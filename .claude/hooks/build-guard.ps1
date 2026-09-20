# PreToolUse on Bash|PowerShell: a build that does not name the configuration the project needs.
#
# In a project that builds one configuration per host year, a build with no configuration picks the
# default - which may deploy into the running host, build a year nobody tests, or overwrite the installed
# product - and it succeeds, so nothing says it was the wrong build. The project declares the rule in
# .claude/paper.profile.json -> guards.build:
#   { "pattern": regex that identifies a build segment, "requireConfiguration": regex that segment must
#     also match, "message": what to build instead }
# A list of such objects works too.
#
# Exit 2 sends stderr to the agent. Unreadable input, no profile or no rule: exit 0.
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

    $profileMap = Read-PaperProfile (Get-PaperHookProjectDir $payload)
    $rules = Get-PaperProfileValue $profileMap @('guards', 'build')
    if ($null -eq $rules) { exit 0 }

    $hit = Get-PaperBuildGuardHit $command $rules
    if ($null -eq $hit) { exit 0 }

    $message = if ($hit.Message) { $hit.Message } else { 'Name the configuration this project builds.' }
    Write-PaperHookText -ToError @"
BLOCKED: this build does not name a configuration.

    $($hit.Segment)

$message
The build segment must match: $($hit.Require)

(Rule: .claude/paper.profile.json -> guards.build. Wrong for this project? Change the profile, not the hook.)
"@
    exit 2
}
catch {
    exit 0
}
