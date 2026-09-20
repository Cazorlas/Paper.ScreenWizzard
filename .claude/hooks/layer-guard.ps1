# PostToolUse on Edit|Write: a platform-free layer reaching the host.
#
# The layers are the project's, declared in .claude/paper.profile.json -> architecture.platformFree. A use
# case or a Domain project that names the host builds green in every configuration, and the split that
# lets its logic run in a unit test with no host application or web server is quietly gone. Nothing else
# notices until someone tries to test it.
#
# KNOWN GAP: fires on Edit/Write only. A file rewritten through a shell command carries no file_path.
# Exit 2 shows stderr to the agent so it fixes the edit it just made. PAPER_SKIP_LAYER_GUARD=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'hook-input.ps1')
. (Join-Path $PSScriptRoot 'architecture-plan.ps1')

if ($env:PAPER_SKIP_LAYER_GUARD -eq '1') { exit 0 }

$payload = Read-PaperHookPayload
if ($null -eq $payload) { exit 0 }
$path = [string] $payload.tool_input.file_path
if ([string]::IsNullOrWhiteSpace($path) -or $path -notmatch '\.(cs|csproj)$') { exit 0 }
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }

$projectDir = $env:CLAUDE_PROJECT_DIR
$profileMap = Read-PaperProfile $projectDir
if ($null -eq $profileMap -or -not $profileMap['architecture']) { exit 0 }
$rules = @($profileMap['architecture']['platformFree'])
if ($rules.Count -eq 0) { exit 0 }

$rel = Get-PaperRelativePath $projectDir $path
if (-not $rel) { exit 0 }

$lines = @(Get-Content -LiteralPath $path -Encoding UTF8)
$hits = @(Get-PaperLayerHits -RelPath $rel -Lines $lines -Rules $rules)
if ($hits.Count -eq 0) { exit 0 }

$report = ($hits | ForEach-Object { "  line {0}: {1}   (forbidden here: {2}, rule {3})" -f $_.Line, $_.Text, $_.Forbidden, $_.Rule }) -join "`n"
[Console]::Error.WriteLine(@"
The host reached a platform-free layer: $rel

$report

This layer is declared host-free in .claude/paper.profile.json (architecture.platformFree). Its logic
runs in unit tests with no host installed; a host type here undoes that, and the build will not say so.

Fix with whichever fits:
  - the host call belongs in an adapter outside this layer, behind a port (an interface) declared here
  - data crossing the port is plain: numbers, strings, records - never a host object or host id
  - a command builds the adapter, calls the use case and hands the result to the view model; it decides nothing

If the rule itself is wrong for this project, change the profile - not this file. A genuine false positive:
say so and move on.
"@)
exit 2
