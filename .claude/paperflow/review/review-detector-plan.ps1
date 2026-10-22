# F383: pure detector scope, command chunks and evidence formatting. ASCII, PowerShell 5.1.
function ConvertFrom-PaperReviewDetectorJson([string] $Json) {
    if (-not $Json.TrimStart().StartsWith('[')) { throw 'stdout is not a JSON array' }
    try {
        # ConvertFrom-Json emits an array as one pipeline object on PowerShell 5.1.
        # Enumerate explicitly before passing findings to the scope and batch filters.
        foreach ($item in ($Json | ConvertFrom-Json)) { $item }
    } catch { throw 'stdout is not a JSON array' }
}

function ConvertTo-PaperReviewDetectorPath([string] $File, [string] $RepoRoot) {
    try {
        $rootPath = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/')
        $candidate = $File.Replace('/', '\')
        if (-not [IO.Path]::IsPathRooted($candidate)) { $candidate = [IO.Path]::Combine($rootPath, $candidate) }
        $full = [IO.Path]::GetFullPath($candidate)
        if ($full.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
            return $full.Substring($rootPath.Length + 1).Replace('\', '/')
        }
    } catch { }
    return ''
}

function Get-PaperReviewDetectorChunks([string[]] $Files, [string] $RepoRoot, [string] $Executable) {
    $dirs = @($Files | Where-Object { $_ -match '\.(html?|css|scss|less|vue|svelte|tsx|jsx|cshtml|razor|astro)$' } |
        ForEach-Object { [IO.Path]::GetDirectoryName([IO.Path]::Combine($RepoRoot, $_.Replace('/', '\'))) } | Sort-Object -Unique)
    $prefixLength = $Executable.Length + 2 + ' -y impeccable@latest detect --json --no-config'.Length + 16
    $chunk = @(); $length = $prefixLength
    foreach ($dir in $dirs) {
        $cost = $dir.Length + 3
        if ($prefixLength + $cost -gt 7000) { throw 'detector directory exceeds command line limit' }
        if ($length + $cost -gt 7000) { [pscustomobject]@{ Directories = @($chunk) }; $chunk = @(); $length = $prefixLength }
        $chunk += $dir; $length += $cost
    }
    if ($chunk.Count) { [pscustomobject]@{ Directories = @($chunk) } }
}

function ConvertFrom-PaperReviewDetectorItems($Items, [string[]] $Files, [string] $RepoRoot) {
    $scope = @{}
    foreach ($file in $Files) { $rel = ConvertTo-PaperReviewDetectorPath $file $RepoRoot; if ($rel) { $scope[$rel] = $rel } }
    foreach ($item in @($Items)) {
        $rel = ConvertTo-PaperReviewDetectorPath "$($item.file)" $RepoRoot
        if (-not $rel -or -not $scope.ContainsKey($rel)) { continue }
        [pscustomobject]@{ rule = "$($item.antipattern)"; name = "$($item.name)"; severity = "$($item.severity)"
            file = $scope[$rel]; line = $item.line; snippet = "$($item.snippet)" }
    }
}

function Format-PaperReviewDetectorLine($Items) {
    $count = @($Items).Count
    $files = @($Items | ForEach-Object { $_.file } | Sort-Object -Unique).Count
    return "detector pass: $count findings in $files files (impeccable latest)"
}

function Format-PaperReviewDetectorExitReason([int] $Code, [string] $Stderr) {
    $lastLine = @($Stderr -split '\r?\n' | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Last 1)
    if (-not $lastLine.Count) { return "exit $Code" }
    $detail = $lastLine[0]
    if ($detail.Length -gt 200) { $detail = $detail.Substring(0, 200) }
    return "exit ${Code}: $detail"
}

function Get-PaperReviewDetectorStatus([string] $Recorded) {
    if ($Recorded) { return $Recorded.Trim() }
    return 'detector pass: not run - detect was not called'
}

function Format-PaperReviewDetectorSection($Items, [string[]] $Files, [bool] $HasResults, [string] $Status) {
    $lines = @('## Detector findings')
    if (-not $HasResults) { $lines += Get-PaperReviewDetectorStatus $Status }
    else {
        $selected = @($Items | Where-Object { $Files -contains $_.file })
        foreach ($item in $selected) { $lines += "$($item.file):$($item.line) $($item.rule) ($($item.severity)) $($item.snippet)" }
        if (-not $selected.Count) { $lines += 'none in this batch' }
    }
    return ($lines -join "`n")
}
