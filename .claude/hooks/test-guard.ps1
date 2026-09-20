# PreToolUse on Bash|PowerShell: a bare test command where the project runs its tests another way.
#
# A rule in a document binds only the reader who happened to read it. When a project's test runner is not
# the obvious one - an executable instead of `dotnet test`, a script that sets up the host - the obvious
# command fails with an error about tooling that reads like a broken test project, and the conclusion drawn
# from it is confidently wrong. The project names each trap in .claude/paper.profile.json -> guards.test:
#   { "pattern": regex over one command segment, "use": the command that works, "escape": a token that
#     lets a deliberate run through (reproducing the error, testing this hook) }
#
# Exit 2 sends stderr to the agent. Unreadable input, no profile or no rules: exit 0.
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
    $rules = Get-PaperProfileValue $profileMap @('guards', 'test')
    if ($null -eq $rules) { exit 0 }

    $hit = Get-PaperTestGuardHit $command $rules
    if ($null -eq $hit) { exit 0 }

    $escapeLine = if ($hit.Escape) { "To run it anyway on purpose, put the literal token $($hit.Escape) anywhere in the command." } else { '' }
    Write-PaperHookText -ToError @"
BLOCKED: this project does not run its tests with this command.

    $($hit.Segment)

Run instead:

    $($hit.Use)

Read the runner's own count of tests run, not only its exit code: a run that matched no test can exit 0.
$escapeLine
(Rule: .claude/paper.profile.json -> guards.test. Wrong for this project? Change the profile, not the hook.)
"@
    exit 2
}
catch {
    exit 0
}
