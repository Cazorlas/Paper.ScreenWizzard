# Stop: the reply names a plan, a task or a markdown file without a link that opens it.
#
# Paperflow rule 9 says a reply that names a document gives the reader a clickable link to it. It was
# broken twice in one session by the same author - once with links that did not open (a path with a space
# written %20), once with no links at all, the second time after a whole session spent on the first. A rule
# with no gate is a wish; this is the gate.
#
# The decision is Get-PaperLinkNagVerdict in link-nag-plan.ps1; this file only reads. It looks at the final
# message of the turn (last_assistant_message, which Claude Code puts in the Stop payload) and, when the
# reply names a document with no link that opens it, holds the stop once with what to fix.
#
# Quiet on purpose for:
#   - stop_hook_active: the reply was already sent back once, and a hook that can hold a stop forever is a
#     loop. Known limit: a second reply that is still wrong goes through.
#   - a subagent (agent_id in the payload): its final message goes to the session that called it, which
#     writes the reply the user actually reads.
#   - the same complaint twice in a session.
# Unreadable input, no message, any error: exit 0. PAPER_SKIP_LINK_NAG=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    if ($env:PAPER_SKIP_LINK_NAG -eq '1') { exit 0 }
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')
    . (Join-Path $PSScriptRoot 'link-nag-plan.ps1')

    $payload = Read-PaperHookPayload
    if ($null -eq $payload -or $payload.stop_hook_active) { exit 0 }
    if ($payload.agent_id) { exit 0 }

    $verdict = Get-PaperLinkNagVerdict -Text ([string] $payload.last_assistant_message)
    if ($null -eq $verdict -or $verdict.ExitCode -eq 0) { exit 0 }

    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'link-nag' $verdict.Text) { exit 0 }
    Write-PaperHookText -ToError $verdict.Text
    exit 2
}
catch {
    exit 0
}
