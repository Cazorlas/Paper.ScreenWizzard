# Stop: source changed this session under a CODEMAP.md that did not.
#
# A code map that names a file which moved, or misses a flow that was rerouted, sends the next session to
# the wrong place with confidence. The map is cheapest to fix in the turn that changed the code, so this
# reminds once per state - the same reminder is not repeated on the next stop, a new file makes it news:
#   - only files written after this session's stamp (session-anchor writes it; the first Stop writes it
#     when SessionStart did not), never the whole dirty working tree
#   - only files whose nearest CODEMAP.md up the tree exists and was not written this session
#   - extensions from .claude/paper.profile.json -> codeMap.extensions (default .cs .xaml .ps1 .py .ts)
#
# stop_hook_active (the agent is already answering a reminder) exits 0, so it can never loop.
# Unreadable input, no session id, any error: exit 0. PAPER_SKIP_CODE_MAP_NAG=1 turns it off.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')

    if ($env:PAPER_SKIP_CODE_MAP_NAG -eq '1') { exit 0 }
    $payload = Read-PaperHookPayload
    if ($null -eq $payload -or $payload.stop_hook_active) { exit 0 }

    $since = Get-PaperSessionStart ([string] $payload.session_id) -Create
    if ($null -eq $since) { exit 0 }

    $root = Get-PaperHookProjectDir $payload
    if (-not $root) { exit 0 }
    $profileMap = Read-PaperProfile $root
    $extensions = ConvertTo-PaperExtensions (Get-PaperProfileValue $profileMap @('codeMap', 'extensions')) @('.cs', '.xaml', '.ps1', '.py', '.ts')

    $stale = [ordered]@{}
    foreach ($file in (Get-PaperChangedFiles $root $since $extensions)) {
        $map = Get-PaperNearestCodeMap $root $file.FullName
        if (-not $map) { continue }
        if ((Get-Item -LiteralPath $map).LastWriteTime -gt $since) { continue }
        if (-not $stale.Contains($map)) { $stale[$map] = New-Object System.Collections.Generic.List[string] }
        $stale[$map].Add((Get-PaperRelative $root $file.FullName))
    }
    if ($stale.Count -eq 0) { exit 0 }

    $sb = New-Object System.Text.StringBuilder
    [void] $sb.AppendLine('Source changed this session, but the CODEMAP.md that covers it did not:')
    [void] $sb.AppendLine()
    foreach ($map in $stale.Keys) {
        [void] $sb.AppendLine('  ' + (Get-PaperRelative $root $map))
        $files = @($stale[$map] | Sort-Object)
        foreach ($f in ($files | Select-Object -First 8)) { [void] $sb.AppendLine('      ' + $f) }
        if ($files.Count -gt 8) { [void] $sb.AppendLine("      ... and $($files.Count - 8) more") }
    }
    [void] $sb.AppendLine()
    [void] $sb.AppendLine('If this change added, moved or renamed a file or rerouted a flow, update that map (/code-map).')
    [void] $sb.AppendLine('If the map is still true, say so in one line and stop.')
    if (Test-PaperNudgeRepeated ([string] $payload.session_id) 'code-map-nag' $sb.ToString()) { exit 0 }
    Write-PaperHookText -ToError $sb.ToString()
    exit 2
}
catch {
    exit 0
}
