# The shared body of the tool-event and Stop dispatchers - pre-tool.ps1, post-tool.ps1 and stop.ps1
# (ADR-0021). Dot-sourced by them after they set $DispatchEvent; declares no param() block.
#
# Reads stdin once, hands it to every script of the event's row in hook-dispatch-plan.ps1 through
# $global:PaperHookRaw (Read-PaperHookPayload takes it from there), and runs each script with & in this
# same process. `exit` inside a script called with & ends that script only and leaves its code in
# $LASTEXITCODE; a script that ends without exit left 0. Every script runs, as when each was a hook of its
# own started in parallel: a blocking script's message is already on stderr, and this process exits 2 when
# any of them exited 2, 0 otherwise - a script that threw lets the tool call through (F10).
#
# Pipeline output of a script goes to stdout as UTF-8. At most one script per row prints JSON there
# (live-first-guard); the payload-contract test keeps it that way, because two JSON objects on one stdout
# are neither.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'hook-dispatch-plan.ps1')

    $stream = [Console]::OpenStandardInput()
    $reader = New-Object System.IO.StreamReader($stream, (New-Object System.Text.UTF8Encoding $false))
    $raw = $reader.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $toolName = ''
    try { $toolName = [string] ($raw | ConvertFrom-Json).tool_name } catch { exit 0 }
    $global:PaperHookRaw = $raw

    $codes = New-Object System.Collections.Generic.List[int]
    foreach ($name in (Get-PaperHookDispatch $DispatchEvent $toolName)) {
        $path = Join-Path $PSScriptRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $global:LASTEXITCODE = 0
        try {
            & $path | ForEach-Object { Write-PaperHookText ([string] $_) }
            $codes.Add([int] $global:LASTEXITCODE)
        }
        catch { $codes.Add(1) }
    }
    exit (Get-PaperDispatchExit $codes.ToArray())
}
catch {
    exit 0
}
