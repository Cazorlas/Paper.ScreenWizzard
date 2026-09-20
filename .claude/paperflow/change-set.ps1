# The change of a branch, found once for every kit verb that reads it (api-check, review-files): the
# repository root, the base branch, the merge-base with it, and the untracked files. Each verb then asks git
# for the diff it needs.
#
# The repository root is `git rev-parse --show-toplevel`, whatever folder the verb was started from: every
# path is from the root, and untracked files at the root are not lost when a verb runs in a subfolder.
#
# The base branch, first that exists:
#   1. -Base <branch>;
#   2. branch.<current>.paperflowBase - what worktree.ps1 create recorded as the branch it cut this one from;
#   3. main, master, origin/main, origin/master.
# None of them while HEAD exists: exit 2, not verifiable - never "not applicable", which would read as
# "nothing changed". A base branch with no merge-base (a shallow clone that stops before it, or unrelated
# history): exit 2 too, for the same reason.
# Working straight on the base branch (found by 2 or 3, not named by -Base): origin/<branch> when it exists,
# so commits not pushed yet count; with no remote the merge-base is HEAD, only uncommitted and untracked
# work counts, and the "from" line says so. No commit yet: no base is needed, every file is new.
#
# THIS FILE DECLARES NO param() BLOCK: it is dot-sourced. ASCII only: 5.1 reads a .ps1 without a BOM as ANSI.

# Native git with stderr allowed: 5.1 turns each stderr line into an error that Stop makes fatal. No param
# block, so a git flag is never bound to a PowerShell common parameter.
function Invoke-PaperChangeGit {
    $gitArgs = @($args)
    $ErrorActionPreference = 'Continue'
    $lines = @(& git.exe -c core.quotepath=off @gitArgs 2>$null | ForEach-Object { "$_" })
    return [pscustomobject]@{ Code = $LASTEXITCODE; Lines = $lines; Text = ($lines -join "`n") }
}

function Test-PaperChangeRef([string] $RepoRoot, [string] $Ref) {
    if (-not $Ref) { return $false }
    return ((Invoke-PaperChangeGit -C $RepoRoot rev-parse --verify -q "$Ref^{commit}").Code -eq 0)
}

function Get-PaperChangeBase {
    <#
    .SYNOPSIS
    The repository root, the base branch and the merge-base of the repository's current branch.
    .OUTPUTS
    Code 0 found (or no commit yet), 2 not a repository / unknown -Base / no base branch / no merge-base -
    with Message, the line to print on stderr, prefixed with -Tool. RepoRoot is the top of the working tree.
    BaseRef is '' when there is no commit yet; MergeBase is 'HEAD' when there is no base to measure from.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [string] $Base,
        [string] $Tool = 'paperflow'
    )

    $result = { param($code, $message, $root, $ref, $why, $mb, $hasHead)
        [pscustomobject]@{ Code = $code; Message = $message; RepoRoot = $root; BaseRef = $ref; BaseWhy = $why; MergeBase = $mb; HasHead = $hasHead } }

    if ((Invoke-PaperChangeGit -C $RepoRoot rev-parse --git-dir).Code -ne 0) {
        return & $result 2 "${Tool}: $RepoRoot is not a git repository" $RepoRoot '' '' 'HEAD' $false
    }
    $top = (Invoke-PaperChangeGit -C $RepoRoot rev-parse --show-toplevel).Text.Trim()
    if ($top) { $RepoRoot = [IO.Path]::GetFullPath(($top -replace '/', '\')).TrimEnd('\') }

    $current = (Invoke-PaperChangeGit -C $RepoRoot symbolic-ref --short -q HEAD).Text.Trim()
    $baseRef = $null
    $baseWhy = ''
    if ($Base) {
        if (-not (Test-PaperChangeRef $RepoRoot $Base)) {
            return & $result 2 "${Tool}: -Base $Base is not a branch or commit of this repository" $RepoRoot '' '' 'HEAD' $false
        }
        $baseRef = $Base; $baseWhy = '-Base'
    }
    if (-not $baseRef -and $current) {
        $recorded = (Invoke-PaperChangeGit -C $RepoRoot config --get "branch.$current.paperflowBase").Text.Trim()
        if ($recorded -and (Test-PaperChangeRef $RepoRoot $recorded)) { $baseRef = $recorded; $baseWhy = 'recorded by worktree create' }
    }
    if (-not $baseRef) {
        foreach ($candidate in @('main', 'master', 'origin/main', 'origin/master')) {
            if (Test-PaperChangeRef $RepoRoot $candidate) { $baseRef = $candidate; $baseWhy = 'default'; break }
        }
    }
    $hasHead = (Test-PaperChangeRef $RepoRoot 'HEAD')
    if (-not $hasHead) {
        return & $result 0 '' $RepoRoot '' '' 'HEAD' $false
    }
    if (-not $baseRef) {
        return & $result 2 "${Tool}: not verifiable: no base branch - none of -Base, branch.<current>.paperflowBase, main, master, origin/main, origin/master exists. Pass -Base <branch>." $RepoRoot '' '' 'HEAD' $true
    }

    # Straight on the base branch: its remote, or the uncommitted work alone.
    if ($baseWhy -ne '-Base' -and $current -and $baseRef -eq $current) {
        if (Test-PaperChangeRef $RepoRoot "origin/$current") {
            $baseRef = "origin/$current"; $baseWhy = "current branch $current, its remote"
        }
        else {
            return & $result 0 '' $RepoRoot $baseRef "current branch, no origin/$current" 'HEAD' $true
        }
    }

    $mb = Invoke-PaperChangeGit -C $RepoRoot merge-base HEAD $baseRef
    if ($mb.Code -ne 0 -or -not $mb.Text.Trim()) {
        return & $result 2 "${Tool}: not verifiable: no merge-base with $baseRef (shallow clone or unrelated history): fetch deeper or pass -Base <branch>" $RepoRoot $baseRef $baseWhy 'HEAD' $true
    }
    return & $result 0 '' $RepoRoot $baseRef $baseWhy $mb.Text.Trim() $true
}

# Untracked files, repository-relative with forward slashes, as git lists them (.gitignore applies).
function Get-PaperChangeUntracked([string] $RepoRoot) {
    return @((Invoke-PaperChangeGit -C $RepoRoot ls-files --others --exclude-standard).Lines | Where-Object { $_ })
}

# The line naming where the change is measured from, the same in every verb's report. -WithWorkingTree adds
# " + uncommitted + untracked" after a merge-base, for a report that names the working tree apart.
function Format-PaperChangeFrom {
    param($Change, [switch] $WithWorkingTree)
    if (-not $Change.BaseRef) { return 'no commit yet: every file is new' }
    if ("$($Change.MergeBase)" -eq 'HEAD') { return "base $($Change.BaseRef) ($($Change.BaseWhy)): uncommitted work only" }
    $mb = "$($Change.MergeBase)"
    $line = "base $($Change.BaseRef) ($($Change.BaseWhy)), merge-base $($mb.Substring(0, [Math]::Min(10, $mb.Length)))"
    if ($WithWorkingTree) { $line += ' + uncommitted + untracked' }
    return $line
}
