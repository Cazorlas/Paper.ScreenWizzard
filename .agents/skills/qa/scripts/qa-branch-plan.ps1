# The branch scope of an external repository (plan 2026-10-03-qa-external-branch, ADR-0037): which base,
# how old that base is on this machine, the files the branch changed - committed since the merge-base, then
# modified, staged or new on disk - and the scope question lanes asks before the lane question when the
# branch is off its base. qa.ps1 reads git with the kit's own read-only commands and never fetches.
# Pure, no I/O. The caller dot-sources qa-plan.ps1 and qa-external-plan.ps1 first (Get-PaperQaStatusPaths);
# declares no param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Get-PaperQaSha7([string] $Sha) {
    if ("$Sha".Length -gt 7) { return "$Sha".Substring(0, 7) }
    return "$Sha"
}

function Select-PaperQaExternalBase {
    <#
    .SYNOPSIS
    The base of a branch scope (A.1): -Base when it is there, else the first of origin/HEAD (shown as the
    branch it points to), origin/main, main, master that this machine has. Error when none is.
    #>
    param([string] $Given, [bool] $GivenFound, [string] $OriginHead, [string[]] $Found)
    $new = { param($ref, $why, $err) [pscustomobject]@{ Ref = $ref; Why = $why; Error = $err } }
    if ($Given) {
        if ($GivenFound) { return & $new $Given '-Base' '' }
        return & $new '' '' "-Base $Given is not a branch or commit of this repository"
    }
    $has = { param($name) @($Found | Where-Object { "$_" -eq $name }).Count -gt 0 }
    if (& $has 'origin/HEAD') {
        $ref = if ($OriginHead) { $OriginHead } else { 'origin/HEAD' }
        return & $new $ref 'origin/HEAD' ''
    }
    foreach ($c in @('origin/main', 'main', 'master')) { if (& $has $c) { return & $new $c 'default' '' } }
    return & $new '' '' 'no base branch - none of origin/HEAD, origin/main, main, master exists on this machine (/qa does not fetch) - pass -Base <branch>'
}

function ConvertTo-PaperQaBaseKind {
    <#
    .SYNOPSIS
    What the base is (A.2), from `git rev-parse --symbolic-full-name <base>`: a remote branch, a local branch
    (with origin/<name> to compare it with), or a commit.
    #>
    param([string] $FullName)
    $n = "$FullName"
    if ($n.StartsWith('refs/remotes/')) { return [pscustomobject]@{ Kind = 'remote'; Counterpart = '' } }
    if ($n.StartsWith('refs/heads/') -and $n.Length -gt 'refs/heads/'.Length) { return [pscustomobject]@{ Kind = 'local'; Counterpart = 'origin/' + $n.Substring('refs/heads/'.Length) } }
    return [pscustomobject]@{ Kind = 'commit'; Counterpart = '' }
}

function Format-PaperQaBaseLine {
    <#
    .SYNOPSIS
    The base: line (A.3): the base, the merge-base, the commits ahead and how old the base is on this machine.
    STALE when a local base is behind its origin counterpart; MAY BE STALE when the last fetch (FETCH_HEAD) is
    more than StaleDays whole days old or there is none. /qa never fetches.
    #>
    param([string] $Ref, [string] $Why, [string] $Sha, [string] $MergeBase, [int] $Ahead, [string] $Kind, [string] $Counterpart, [bool] $CounterpartFound, [int] $Behind, [double] $FetchAgeDays, [string] $FetchDate, [bool] $Shallow, [int] $StaleDays = 7)
    $head = "base: $Ref ($Why) at $(Get-PaperQaSha7 $Sha), merge-base $(Get-PaperQaSha7 $MergeBase), $Ahead commit(s) ahead - "
    $never = $FetchAgeDays -lt 0
    if ($never) { $fetch = 'no fetch recorded on this machine (no FETCH_HEAD)' }
    else { $fetch = "last fetch $FetchDate ($([Math]::Floor($FetchAgeDays)) day(s) ago)" }
    $old = $never -or ([Math]::Floor($FetchAgeDays) -gt $StaleDays)
    $fresh = ' - run git fetch yourself for a fresh base'
    $stale = $false
    switch ($Kind) {
        'remote' {
            if ($old) { $tail = "MAY BE STALE: $fetch; /qa never fetches$fresh"; $stale = $true }
            else { $tail = "$fetch; /qa never fetches" }
        }
        'local' {
            if (-not $CounterpartFound) { $tail = "a local branch with no $Counterpart to compare; /qa never fetches" }
            elseif ($Behind -gt 0) { $tail = "STALE: $Ref is $Behind commit(s) behind $Counterpart on this machine; /qa never fetches"; $stale = $true }
            elseif ($old) { $tail = "MAY BE STALE: even with $Counterpart, $fetch; /qa never fetches$fresh"; $stale = $true }
            else { $tail = "even with $Counterpart, $fetch; /qa never fetches" }
        }
        default { $tail = 'a commit, not a branch: nothing to compare' }
    }
    if ($Shallow) { $tail += '; shallow clone' }
    return [pscustomobject]@{ Line = $head + $tail; Stale = $stale }
}

function Split-PaperQaStatusEntries {
    <#
    .SYNOPSIS
    `git status --porcelain --untracked-files=all` lines (A.4) split into Changed (modified, staged, deleted,
    the new name of a rename) and Untracked (??), in the order given.
    #>
    param([string[]] $Lines)
    $changed = @(); $untracked = @()
    foreach ($l in @($Lines)) {
        $s = "$l"
        if ($s.Length -le 3) { continue }
        $p = @(Get-PaperQaStatusPaths -Lines @($s))
        if ($p.Count -eq 0) { continue }
        if ($s.Substring(0, 2) -eq '??') { $untracked += $p[0] } else { $changed += $p[0] }
    }
    return [pscustomobject]@{ Changed = $changed; Untracked = $untracked }
}

function Get-PaperQaDistinctCount([string[]] $Paths) {
    $seen = @{}
    foreach ($p in @($Paths)) { if ($p) { $seen[$p.ToLowerInvariant()] = $true } }
    return $seen.Count
}

function Get-PaperQaExternalBranchPaths {
    <#
    .SYNOPSIS
    The paths of a branch scope (A.5): Tracked = committed since the merge-base, then uncommitted, one entry
    per path whatever its case (the first spelling kept); Untracked = the new files not already in Tracked.
    Each group counted apart.
    #>
    param([string[]] $Committed, [string[]] $Changed, [string[]] $Untracked)
    $seen = @{}
    $tracked = @()
    foreach ($p in (@($Committed) + @($Changed))) {
        if (-not $p) { continue }
        $k = $p.ToLowerInvariant()
        if ($seen.ContainsKey($k)) { continue }
        $seen[$k] = $true
        $tracked += $p
    }
    $new = @()
    foreach ($p in @($Untracked)) {
        if (-not $p) { continue }
        $k = $p.ToLowerInvariant()
        if ($seen.ContainsKey($k)) { continue }
        $seen[$k] = $true
        $new += $p
    }
    return [pscustomobject]@{
        Tracked = $tracked; Untracked = $new
        CommittedCount = (Get-PaperQaDistinctCount $Committed); UncommittedCount = (Get-PaperQaDistinctCount $Changed); UntrackedCount = (Get-PaperQaDistinctCount $Untracked)
    }
}

function Format-PaperQaChangesLine {
    param([int] $Committed, [int] $Uncommitted, [int] $Untracked)
    return "changes: $Committed committed file(s) since the merge-base, $Uncommitted uncommitted (modified, staged or deleted), $Untracked untracked - read as they are on disk"
}

# A.7: the branch name, or HEAD-<sha7> when HEAD is detached.
function Get-PaperQaBranchLabel {
    param([string] $Branch, [string] $Head)
    if ($Branch) { return $Branch }
    return "HEAD-$(Get-PaperQaSha7 $Head)"
}

# A.8 (F126): the branch is off its base - commits ahead or work not committed - and its scope keeps a file.
function Test-PaperQaAskScope {
    param([int] $Ahead, [int] $Uncommitted, [int] $Untracked, [int] $BranchCount)
    return [bool] (($Ahead -gt 0 -or $Uncommitted -gt 0 -or $Untracked -gt 0) -and $BranchCount -gt 0)
}

function Format-PaperQaScopeChoice {
    <#
    .SYNOPSIS
    What lanes prints instead of the lane list when the scope must be asked first (A.9, F126).
    #>
    param([string] $Label, [string] $RepoRoot, [string] $BaseLine, [string] $ChangesLine, [int] $ProjectCount, [int] $ProjectExcluded, [int] $BranchCount, [int] $BranchExcluded)
    return @(
        "qa-scope: choose the scope first - $Label is off its base",
        "repo: $RepoRoot (external, read only)",
        $BaseLine,
        $ChangesLine,
        "1. project - $ProjectCount file(s), $ProjectExcluded excluded",
        "2. branch $Label - $BranchCount file(s), $BranchExcluded excluded",
        'ask: scope'
    )
}
