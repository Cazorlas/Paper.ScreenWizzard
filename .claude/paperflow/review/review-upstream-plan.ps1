# F382: pure upstream selection, refresh decisions and cache locations. ASCII, PowerShell 5.1.
function Get-PaperReviewUpstreamCommit([string[]] $Lines, [string] $TagPattern) {
    $refs = @()
    # An annotated tag lists the tag object, then its commit as "<tag>^{}": the pin and the raw URL use the commit.
    $peeled = @{}
    foreach ($line in @($Lines)) {
        if ($line -match '^([a-fA-F0-9]{40})\s+refs/tags/(.+)\^\{\}\s*$') { $peeled[$Matches[2]] = $Matches[1].ToLowerInvariant() }
    }
    foreach ($line in @($Lines)) {
        if ($line -notmatch '^([a-fA-F0-9]{40})\s+(\S+)\s*$') { continue }
        $sha = $Matches[1].ToLowerInvariant(); $ref = $Matches[2]
        if (-not $TagPattern) { if ($ref -eq 'HEAD') { return $sha }; continue }
        if ($ref.EndsWith('^{}') -or $ref -notmatch '^refs/tags/(.+)$') { continue }
        $tag = $Matches[1]
        if ($peeled.ContainsKey($tag)) { $sha = $peeled[$tag] }
        if ($tag -notlike $TagPattern) { continue }
        $number = $tag -replace '^[^0-9]+', ''
        $version = $null
        if ([version]::TryParse($number, [ref] $version)) { $refs += [pscustomobject]@{ Version = $version; Tag = $tag; Commit = $sha } }
    }
    $latest = @($refs | Sort-Object Version, Tag -Descending | Select-Object -First 1)
    if ($latest.Count) { return $latest[0].Commit }
    return ''
}

function Get-PaperReviewUpstreamDecision($Skill, [string] $Commit, [bool] $Enabled = $true) {
    if (-not $Enabled) { return 'skipped' }
    if (-not $Commit) { return 'skipped' }
    if ($Commit -eq $Skill.commit) { return 'current' }
    if (@($Skill.replace).Count) { return 'fetch' }
    return 'check-only'
}

function Get-PaperReviewUpstreamCachePath([string] $Cache, [string] $Skill, [string] $Commit, [string] $File) {
    return [IO.Path]::Combine($Cache, $Skill, $Commit, $File.Replace('/', [IO.Path]::DirectorySeparatorChar))
}

function Get-PaperReviewUpstreamSource([string] $Skill, [string] $Commit, [string] $Source, [string] $Prefix = 'source') {
    return "${Prefix}: $Skill@$($Commit.Substring(0, 7)) ($Source)"
}
