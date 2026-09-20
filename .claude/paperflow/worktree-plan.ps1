# The decision half of worktree.ps1, with no git, no file system and no process: where a task's worktree
# goes, which arguments make sense, whether two files carry the same content, and what `done` may do.
# Dot-source it; tests/worktree.tests.ps1 covers it.
#
# THIS FILE STAYS ASCII (PowerShell 5.1 reads a .ps1 without a BOM in the ANSI codepage).
#
# Exit codes it plans (the same as worktree.ps1):
#   0  ok
#   1  refused, a carried file that does not match, or a red test
#   2  bad arguments
#   5  not applicable - the project declares no verb for the step

$script:PaperWorktreeCommands = @('create', 'carry', 'baseline', 'done')

function New-PaperWorktreeVerdict([int] $code, [string] $reason, [string] $action = '') {
    return [pscustomobject]@{ ExitCode = $code; Reason = $reason; Action = $action }
}

function Test-PaperWorktreeSlug([string] $Slug) {
    # Kebab-case only. The slug becomes a folder name and a branch name, so anything a shell or git would
    # need quoting for is refused rather than silently rewritten into a name the user did not type.
    return ($Slug -cmatch '^[a-z0-9]+(-[a-z0-9]+)*$') -and $Slug.Length -le 50
}

function Get-PaperWorktreeBranch([string] $Slug) { return "task/$Slug" }

function Get-PaperWorktreePath {
    <#
    .SYNOPSIS
    Where the worktree of one task goes: <parent>\_worktrees\<repo>-<slug> when <parent>\_worktrees
    exists, else the sibling <parent>\<repo>-<slug>.
    .DESCRIPTION
    Never below the repository itself: a project whose build reaches a dependency by a relative path
    counted from the repository resolves it the same way from a folder at the same depth, and from
    nowhere else.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $MainRoot,
        [Parameter(Mandatory = $true)][string] $Slug,
        [bool] $WorktreesDirExists
    )
    $main = $MainRoot.TrimEnd('\', '/')
    $parent = Split-Path $main -Parent
    $name = Split-Path $main -Leaf
    if ($WorktreesDirExists) { return (Join-Path (Join-Path $parent '_worktrees') "$name-$Slug") }
    return (Join-Path $parent "$name-$Slug")
}

function Test-PaperWorktreeCopyPath([string] $Path) {
    # A profile's worktree.copyFiles entry: relative to the repository, and never out of it.
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ([System.IO.Path]::IsPathRooted($Path) -or $Path -match '^[a-zA-Z]:') { return $false }
    foreach ($part in ($Path -split '[\\/]')) { if ($part -eq '..') { return $false } }
    return $true
}

function Get-PaperWorktreeArgsVerdict {
    <#
    .SYNOPSIS
    Checks the arguments of one worktree.ps1 call before anything touches git.
    #>
    param(
        [string] $Command,
        [string] $Slug,
        [bool] $Merge,
        [bool] $Discard,
        [bool] $Check,
        [string] $From
    )
    if ($script:PaperWorktreeCommands -notcontains $Command) {
        return New-PaperWorktreeVerdict 2 "unknown command '$Command'. Commands: $($script:PaperWorktreeCommands -join ', ')"
    }
    # baseline may run without a slug: then it runs in -Repo itself.
    if ($Command -ne 'baseline' -or $Slug) {
        if (-not $Slug) { return New-PaperWorktreeVerdict 2 "$Command needs a slug: worktree.ps1 $Command <slug>" }
        if (-not (Test-PaperWorktreeSlug $Slug)) {
            return New-PaperWorktreeVerdict 2 "slug '$Slug' is not kebab-case (a-z, 0-9 and single dashes, at most 50 characters)"
        }
    }
    if ($From -and $Command -ne 'create') { return New-PaperWorktreeVerdict 2 '-From belongs to create' }
    if ($Check -and $Command -ne 'carry') { return New-PaperWorktreeVerdict 2 '-Check belongs to carry' }
    if (($Merge -or $Discard) -and $Command -ne 'done') { return New-PaperWorktreeVerdict 2 '-Merge and -Discard belong to done' }
    if ($Merge -and $Discard) {
        return New-PaperWorktreeVerdict 2 'done takes -Merge or -Discard, not both: merge keeps the work, discard throws it away'
    }
    return New-PaperWorktreeVerdict 0 'ok'
}

function Test-PaperSameIgnoringCr {
    <#
    .SYNOPSIS
    True when two files hold the same bytes once every CR (0x0D) is removed from both.
    .DESCRIPTION
    `git apply` into a checkout with core.autocrlf=true writes CRLF where the source had LF, so a byte
    compare would call every carried text file a mismatch.
    #>
    param([byte[]] $Left, [byte[]] $Right)
    if ($null -eq $Left) { $Left = [byte[]]@() }
    if ($null -eq $Right) { $Right = [byte[]]@() }
    $i = 0; $j = 0
    while ($true) {
        while ($i -lt $Left.Length -and $Left[$i] -eq 13) { $i++ }
        while ($j -lt $Right.Length -and $Right[$j] -eq 13) { $j++ }
        $endL = $i -ge $Left.Length
        $endR = $j -ge $Right.Length
        if ($endL -or $endR) { return ($endL -and $endR) }
        if ($Left[$i] -ne $Right[$j]) { return $false }
        $i++; $j++
    }
}

function Get-PaperCarryVerdict {
    param([int] $Checked, [AllowEmptyCollection()][string[]] $Mismatches = @())
    $bad = @($Mismatches | Where-Object { $_ })
    if ($bad.Count -gt 0) {
        return New-PaperWorktreeVerdict 1 ("carry: $($bad.Count) mismatch(es) of $Checked file(s), stop and look: " + ($bad -join ', '))
    }
    return New-PaperWorktreeVerdict 0 "carry: $Checked file(s) compared ignoring CR, 0 mismatches"
}

function Get-PaperWorktreeDoneVerdict {
    <#
    .SYNOPSIS
    What `done` may do with one task's worktree: refuse (1), remove it, or merge first.
    .PARAMETER Unmerged
    Commits on the task branch that are not in its base (git rev-list --count base..branch).
    .PARAMETER Uncommitted
    Lines of `git status --porcelain` in the worktree: changed, staged or untracked files.
    #>
    param(
        [Parameter(Mandatory = $true)][string] $Branch,
        [Parameter(Mandatory = $true)][string] $Base,
        [int] $Unmerged,
        [int] $Uncommitted,
        [bool] $Merge,
        [bool] $Discard
    )
    if ($Discard) {
        return New-PaperWorktreeVerdict 0 "discard: $Branch and its worktree go, with $Unmerged unmerged commit(s) and $Uncommitted uncommitted change(s)" 'remove'
    }
    if ($Uncommitted -gt 0) {
        return New-PaperWorktreeVerdict 1 "refused: $Branch has $Uncommitted uncommitted change(s) in its worktree. Commit them, or run done -Discard to throw them away"
    }
    if ($Unmerged -gt 0 -and -not $Merge) {
        return New-PaperWorktreeVerdict 1 "refused: $Branch has $Unmerged commit(s) not merged into $Base. Run done -Merge when the user asked to merge, or done -Discard to throw them away"
    }
    if ($Unmerged -gt 0) {
        return New-PaperWorktreeVerdict 0 "merge: $Unmerged commit(s) of $Branch into $Base, then test $Base" 'merge'
    }
    return New-PaperWorktreeVerdict 0 "$Branch has nothing left to merge into $Base" 'remove'
}

function Get-PaperTestCountLine {
    <#
    .SYNOPSIS
    The last line of a test run's output that states how many tests ran, or '' when none does.
    .DESCRIPTION
    Shapes it knows: "Total: 12" / "Total tests: 12" (dotnet), "Passed: 12", "Tests run: 12", and
    "12 run" / "12 passed" / "12 tests". An exit 0 with no such line is not a pass: it may have run nothing.
    #>
    param([AllowEmptyString()][AllowEmptyCollection()][string[]] $Lines = @())
    $patterns = @(
        '(?i)\b(total( tests)?|passed|failed|tests run)\s*[:=]\s*\d+',
        '(?i)\b\d+\s+(tests?|passed|failed|run)\b'
    )
    $found = ''
    foreach ($line in $Lines) {
        foreach ($p in $patterns) {
            if ($line -match $p) { $found = $line.Trim(); break }
        }
    }
    return $found
}
