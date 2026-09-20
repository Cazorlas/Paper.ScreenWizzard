# PostToolUse on Edit|Write: a static field holding a live host object.
#
# Which types are host state is the project's, declared in .claude/paper.profile.json ->
# architecture.hostStateTypes (a document, a database, an element... - the host pack's profile.json lists
# them). A static field captures the FIRST document of the host process; close and reopen the file and
# every call on it throws, long after the edit that caused it. Unit tests never open a second document, so
# they cannot see it. It has shipped as a production bug in a real add-in.
#
# Exit 2 shows stderr to the agent so it fixes the edit it just made. PAPER_SKIP_STATIC_HOST_STATE=1 turns
# it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'hook-input.ps1')
. (Join-Path $PSScriptRoot 'architecture-plan.ps1')

if ($env:PAPER_SKIP_STATIC_HOST_STATE -eq '1') { exit 0 }

$payload = Read-PaperHookPayload
if ($null -eq $payload) { exit 0 }
$path = [string] $payload.tool_input.file_path
if ([string]::IsNullOrWhiteSpace($path) -or $path -notmatch '\.cs$') { exit 0 }
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }

$profileMap = Read-PaperProfile $env:CLAUDE_PROJECT_DIR
if ($null -eq $profileMap -or -not $profileMap['architecture']) { exit 0 }
$types = @($profileMap['architecture']['hostStateTypes'])
if ($types.Count -eq 0) { exit 0 }

$hits = @(Get-PaperStaticStateHits -Lines @(Get-Content -LiteralPath $path -Encoding UTF8) -Types $types)
if ($hits.Count -eq 0) { exit 0 }

$report = ($hits | ForEach-Object { "  line {0}: {1}" -f $_.Line, $_.Text }) -join "`n"
[Console]::Error.WriteLine(@"
Static host state in $path

$report

A static field captures the FIRST document of the host process. Close and reopen the file and every call
on it throws - in a session nobody is debugging, invisible to unit tests.

Fix with whichever fits:
  - a static property that reads fresh on every call:  static Document Doc => Context.ActiveDocument;
  - an instance field, set for each command run
  - pass the document in as a parameter

If this line was already there before your edit, or is a genuine false positive, say so and move on.
"@)
exit 2
