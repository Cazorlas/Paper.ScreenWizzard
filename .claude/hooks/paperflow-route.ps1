# UserPromptSubmit: route a request for code or model work into the paperflow skill.
#
# The decision is Get-PaperRoute in paperflow-route-plan.ps1; this file only reads - the prompt from stdin
# as UTF-8 bytes, the project profile, the route file each host pack vendors into .claude/paperflow/routes/
# (<host>.json: keywords, loop, liveFirst), and the RULE/SAMPLE/REFERENCE index under the feature docs.
# The context names the work type it matched, the host, and that host's loop order.
#
# Never blocks: a routed prompt gets one paragraph of context, anything else exits 0 with no output.
# Slash commands are skipped - the user chose the flow themselves. No profile, unreadable input, any
# error: exit 0 and say nothing, because a hook that fails must not stand between the user and their
# prompt. PAPER_SKIP_PAPERFLOW_ROUTE=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    if ($env:PAPER_SKIP_PAPERFLOW_ROUTE -eq '1') { exit 0 }
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'paperflow-route-plan.ps1')

    $payload = Read-PaperHookPayload
    if ($null -eq $payload) { exit 0 }
    $prompt = [string] $payload.prompt
    if ([string]::IsNullOrWhiteSpace($prompt) -or $prompt.TrimStart().StartsWith('/')) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $profileMap = Read-PaperProfile $root
    if ($null -eq $profileMap) { exit 0 }

    # One route file per host the project declares or names under routing.hosts; a host whose pack ships
    # none routes on the project's own words only.
    $routes = @{}
    $routeDir = Join-Path $PSScriptRoot '..\paperflow\routes'
    $names = @(@(Get-PaperProfileValue $profileMap @('hosts')) + @(Get-PaperRouteKeys (Get-PaperProfileValue $profileMap @('routing', 'hosts')))) |
        Where-Object { $_ -and $_ -ne '$comment' } | Select-Object -Unique
    foreach ($h in $names) {
        $file = Join-Path $routeDir "$h.json"
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
        try { $routes[[string] $h] = ConvertTo-PaperHookMap ([IO.File]::ReadAllText($file, [Text.Encoding]::UTF8) | ConvertFrom-Json) } catch { }
    }

    $docsRel = [string] (Get-PaperProfileValue $profileMap @('featureDocs'))
    if (-not $docsRel) { $docsRel = 'docs/features' }
    $index = @()
    try { $index = @(Get-PaperModelRuleIndex (Join-Path $root ($docsRel.Replace('/', '\')))) } catch { }

    $route = Get-PaperRoute -Prompt $prompt -ProjectProfile $profileMap -HostRoutes $routes -ModelIndex $index -DocsRel $docsRel
    if ($null -eq $route) { exit 0 }

    $json = @{ hookSpecificOutput = @{ hookEventName = 'UserPromptSubmit'; additionalContext = $route.Context } } | ConvertTo-Json -Compress -Depth 4
    Write-PaperHookText $json
    exit 0
}
catch {
    exit 0
}
