# One task, one git worktree beside the repository: its build output and its red tests stay there, so
# other sessions working in the main checkout are not blocked by them.
#
#   worktree.ps1 create   <slug> [-From <branch>]            [-Repo <path>]
#   worktree.ps1 carry    <slug> [-Check]                    [-Repo <path>]
#   worktree.ps1 baseline [<slug>]                           [-Repo <path>]
#   worktree.ps1 done     <slug> [-Merge [-Test <command line>] | -Discard] [-Into <branch>] [-Repo <path>]
#
# create    branch task/<slug> from the current branch (or -From), in <parent>\_worktrees\<repo>-<slug> when
#           that folder exists, else <parent>\<repo>-<slug>; copy the profile's worktree.copyFiles that exist.
#           The base branch is recorded in git config (branch.task/<slug>.paperflowBase) for `done`.
# carry     the main checkout's uncommitted work (git diff HEAD --binary, plus untracked files) into the
#           worktree, then compare every carried file ignoring CR. -Check compares only.
# baseline  the profile's build verb, then its test verb, in the worktree (or in -Repo without a slug);
#           prints the line of the test output that says how many tests ran, and exits with that run's
#           verdict: 0 tests ran and passed, 1 tests ran and failed, 4 no test ran - not verifiable (F5).
# done      refuses while the worktree has uncommitted changes, or the branch has commits not in its base
#           and neither -Merge nor -Discard was given. -Merge merges into the base IN THE MAIN CHECKOUT, runs
#           -Test (default: the profile's test verb) there, and removes the worktree only when it is green.
#
# Never Claude Code's built-in worktree tool: it nests the checkout below the repository, where relative
# paths counted from the repository root no longer resolve.
#
# Every decision is in worktree-plan.ps1 (pure, tested); this file runs git, copies and compares.
# Exit: 0 ok | 1 refused, mismatch, merge failed or test red | 2 bad arguments |
#       4 baseline ran no test: not verifiable | 5 not applicable
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string] $Command,
    [Parameter(Position = 1)][string] $Slug,
    [string] $Repo = (Get-Location).Path,
    [string] $From,
    [string] $Into,
    [string] $Test,
    [switch] $Check,
    [switch] $Merge,
    [switch] $Discard
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'worktree-plan.ps1')
. (Join-Path $PSScriptRoot 'verb-plan.ps1')

# Paths git prints may be non-ASCII; without this a redirected child reads them in the OEM codepage.
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

function Say([string] $line) { [Console]::Out.WriteLine($line) }
function Fail([int] $code, [string] $line) {
    if ($code -eq 5) { Say $line } else { [Console]::Error.WriteLine($line) }
    exit $code
}

# Native git. ErrorActionPreference must be Continue around it: git writes progress to stderr, and
# PowerShell 5.1 turns each stderr line into an error that Stop makes fatal. The exit code decides.
# No param block on purpose: with one, the function becomes advanced and gains the common parameters,
# so `branch -D` bound -D to -Debug and git received `branch task/x` - which CREATES the branch.
function Invoke-PaperGit {
    $gitArgs = @($args)
    $ErrorActionPreference = 'Continue'
    $lines = @(& git.exe -c core.quotepath=off @gitArgs 2>&1 | ForEach-Object { "$_" })
    return [pscustomobject]@{ Code = $LASTEXITCODE; Lines = $lines; Text = ($lines -join "`n") }
}

function ConvertTo-PaperWorktreeMap($Value) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) { return $Value }
    if ($Value -is [System.Array]) { return @($Value | ForEach-Object { ConvertTo-PaperWorktreeMap $_ }) }
    if ($Value -isnot [System.Management.Automation.PSCustomObject]) { return $Value }
    $map = @{}
    foreach ($prop in $Value.PSObject.Properties) {
        if ($prop.Name.StartsWith('$')) { continue }
        $map[$prop.Name] = ConvertTo-PaperWorktreeMap $prop.Value
    }
    return $map
}

# $null when the folder has no .claude/paper.profile.json; exit 2 when it is not valid JSON.
function Read-PaperWorktreeProfile([string] $Dir) {
    $file = Join-Path $Dir '.claude\paper.profile.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
    try { return ConvertTo-PaperWorktreeMap (Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json) }
    catch { Fail 2 "worktree: $file is not valid JSON - $($_.Exception.Message)" }
}

# Runs one of the project's command lines in a folder, echoing each line as it comes and keeping them.
#
# Through project-command.ps1, the one way the kit runs a project's line, and never a bare -Command:
# -Command takes the line as process ARGUMENTS, and PowerShell 5.1 hands a native process an argument without
# escaping the double quotes inside it, so the child re-splits what it receives. Measured 2026-09-18: a
# profile declaring `& "C:\a b\build step.ps1"` made the child look for a program called `C:\a`, and one
# declaring `Write-Output "x y: Total: 4"` made it print `x` and `y` on separate lines.
. (Join-Path $PSScriptRoot 'project-command.ps1')
function Invoke-PaperCommandLine([string] $Dir, [string] $CommandLine) {
    $run = Invoke-PaperProjectCommand -Directory $Dir -Command $CommandLine -Echo
    return [pscustomobject]@{ Code = $run.Code; Lines = @($run.Lines) }
}

function Get-PaperMainRoot([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { Fail 2 "worktree: folder not found: $Path" }
    $r = Invoke-PaperGit -C $Path rev-parse --path-format=absolute --git-common-dir
    if ($r.Code -ne 0) { Fail 2 "worktree: not a git repository: $Path" }
    # The common dir is the main checkout's .git even when -Repo points inside another worktree.
    $common = [System.IO.Path]::GetFullPath(($r.Lines[-1]).Trim())
    return (Split-Path $common -Parent)
}

# The worktree currently checked out on a branch, or $null.
function Find-PaperWorktree([string] $MainRoot, [string] $Branch) {
    $r = Invoke-PaperGit -C $MainRoot worktree list --porcelain
    $path = $null
    foreach ($line in $r.Lines) {
        if ($line -like 'worktree *') { $path = $line.Substring(9) }
        elseif ($line -eq "branch refs/heads/$Branch" -and $path) { return [System.IO.Path]::GetFullPath($path) }
    }
    return $null
}

function Test-PaperBranch([string] $MainRoot, [string] $Branch) {
    return ((Invoke-PaperGit -C $MainRoot show-ref --verify --quiet "refs/heads/$Branch").Code -eq 0)
}

# ---- arguments -----------------------------------------------------------------------------------------
$verdict = Get-PaperWorktreeArgsVerdict -Command $Command -Slug $Slug -Merge $Merge.IsPresent -Discard $Discard.IsPresent -Check $Check.IsPresent -From $From
if ($verdict.ExitCode -ne 0) { Fail $verdict.ExitCode "worktree: $($verdict.Reason)" }
$branch = if ($Slug) { Get-PaperWorktreeBranch $Slug } else { '' }

switch ($Command) {

    'create' {
        $main = Get-PaperMainRoot $Repo
        if (Test-PaperBranch $main $branch) { Fail 2 "worktree: branch $branch already exists - pick another slug, or finish it with done $Slug" }
        $base = $From
        if ($base) {
            if (-not (Test-PaperBranch $main $base)) { Fail 2 "worktree: -From $base is not a local branch" }
        }
        else {
            $head = Invoke-PaperGit -C $Repo symbolic-ref --short -q HEAD
            if ($head.Code -ne 0 -or -not $head.Text) { Fail 2 'worktree: HEAD is detached - pass -From <branch>' }
            $base = $head.Text.Trim()
        }
        $worktreesDir = Join-Path (Split-Path $main -Parent) '_worktrees'
        $path = Get-PaperWorktreePath -MainRoot $main -Slug $Slug -WorktreesDirExists (Test-Path -LiteralPath $worktreesDir -PathType Container)
        if (Test-Path -LiteralPath $path) { Fail 2 "worktree: $path already exists" }

        $add = Invoke-PaperGit -C $main worktree add -b $branch $path $base
        if ($add.Code -ne 0) { Fail 1 "worktree: git worktree add failed - $($add.Text)" }
        [void] (Invoke-PaperGit -C $main config "branch.$branch.paperflowBase" $base)

        # Local files git does not carry (personal settings and the like), declared by the project.
        $projectProfile = Read-PaperWorktreeProfile $main
        $copyFiles = @()
        if ($projectProfile -and $projectProfile['worktree']) { $copyFiles = @($projectProfile['worktree']['copyFiles']) | Where-Object { $_ } }
        foreach ($rel in $copyFiles) {
            if (-not (Test-PaperWorktreeCopyPath $rel)) { Say "worktree: skipped copyFiles entry '$rel' - it must be relative and stay inside the repository"; continue }
            $src = Join-Path $main $rel
            if (-not (Test-Path -LiteralPath $src)) { continue }
            $dst = Join-Path $path $rel
            $dstDir = Split-Path $dst -Parent
            if (-not (Test-Path -LiteralPath $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
            Copy-Item -LiteralPath $src -Destination $dst -Recurse -Force
            Say "worktree: copied $rel"
        }

        Say "worktree: $path"
        Say "branch:   $branch (from $base)"
        Say "next:     worktree.ps1 carry $Slug (only if the main checkout holds uncommitted work the task needs), then worktree.ps1 baseline $Slug"
        exit 0
    }

    'carry' {
        $main = Get-PaperMainRoot $Repo
        $path = Find-PaperWorktree $main $branch
        if (-not $path) { Fail 2 "worktree: no worktree on $branch - create it first" }

        $changed = Invoke-PaperGit -C $main diff HEAD --name-only --no-renames
        $untracked = Invoke-PaperGit -C $main ls-files --others --exclude-standard
        if ($changed.Code -ne 0 -or $untracked.Code -ne 0) { Fail 1 "worktree: git could not list the uncommitted work - $($changed.Text) $($untracked.Text)" }
        $files = @(@($changed.Lines) + @($untracked.Lines) | Where-Object { $_ } | Select-Object -Unique)

        if (-not $Check) {
            $dirty = Invoke-PaperGit -C $path status --porcelain
            if (@($dirty.Lines | Where-Object { $_ }).Count -gt 0) {
                Fail 1 "worktree: $path already has uncommitted changes - carry only into a clean worktree, or use -Check to compare"
            }
            $patch = Join-Path $env:TEMP ("paperflow-carry-" + [guid]::NewGuid().ToString('N') + '.patch')
            try {
                # --output, not a pipe: PowerShell would re-encode a binary patch line by line.
                $diff = Invoke-PaperGit -C $main diff HEAD --binary --no-renames "--output=$patch"
                if ($diff.Code -ne 0) { Fail 1 "worktree: git diff failed - $($diff.Text)" }
                if ((Test-Path -LiteralPath $patch) -and (Get-Item -LiteralPath $patch).Length -gt 0) {
                    $apply = Invoke-PaperGit -C $path apply --binary $patch
                    if ($apply.Code -ne 0) { Fail 1 "worktree: git apply failed in $path (was it created from another base?) - $($apply.Text)" }
                }
            }
            finally { Remove-Item -LiteralPath $patch -Force -ErrorAction SilentlyContinue }
            foreach ($rel in @($untracked.Lines | Where-Object { $_ })) {
                $dst = Join-Path $path $rel
                $dstDir = Split-Path $dst -Parent
                if (-not (Test-Path -LiteralPath $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
                Copy-Item -LiteralPath (Join-Path $main $rel) -Destination $dst -Force
            }
        }

        $mismatches = New-Object System.Collections.Generic.List[string]
        foreach ($rel in $files) {
            $src = Join-Path $main $rel
            $dst = Join-Path $path $rel
            $srcThere = Test-Path -LiteralPath $src -PathType Leaf
            $dstThere = Test-Path -LiteralPath $dst -PathType Leaf
            if ($srcThere -ne $dstThere) { $mismatches.Add($rel); continue }
            if (-not $srcThere) { continue }   # deleted on both sides
            if (-not (Test-PaperSameIgnoringCr ([System.IO.File]::ReadAllBytes($src)) ([System.IO.File]::ReadAllBytes($dst)))) { $mismatches.Add($rel) }
        }
        $carry = Get-PaperCarryVerdict -Checked $files.Count -Mismatches $mismatches.ToArray()
        if ($carry.ExitCode -ne 0) { Fail $carry.ExitCode "worktree: $($carry.Reason)" }
        Say "worktree: $($carry.Reason)"
        exit 0
    }

    'baseline' {
        $dir = $Repo
        if ($Slug) {
            $main = Get-PaperMainRoot $Repo
            $dir = Find-PaperWorktree $main $branch
            if (-not $dir) { Fail 2 "worktree: no worktree on $branch - create it first" }
        }
        elseif (-not (Test-Path -LiteralPath $dir -PathType Container)) { Fail 2 "worktree: folder not found: $dir" }

        $projectProfile = Read-PaperWorktreeProfile $dir
        if ($null -eq $projectProfile) { Fail 5 "worktree: baseline not applicable - no .claude/paper.profile.json in $dir" }
        $plans = [ordered]@{}
        foreach ($verb in @('build', 'test')) {
            $plans[$verb] = Get-PaperVerbPlan -ProjectProfile $projectProfile -Verb $verb
            if ($plans[$verb].ExitCode -eq 2) { Fail 2 "worktree: $($plans[$verb].Reason)" }
        }
        if ($plans['build'].ExitCode -ne 0 -and $plans['test'].ExitCode -ne 0) {
            Fail 5 'worktree: baseline not applicable - the profile declares neither build nor test'
        }

        if ($plans['build'].ExitCode -eq 0) {
            Say "baseline: build -> $($plans['build'].Command)"
            $build = Invoke-PaperCommandLine $dir $plans['build'].Command
            if ($build.Code -ne 0) { Fail 1 "baseline: build failed (exit $($build.Code)) in $dir" }
        }
        else { Say "baseline: build not applicable - $($plans['build'].Reason)" }

        if ($plans['test'].ExitCode -ne 0) {
            Say "baseline: test not applicable - $($plans['test'].Reason)"
            exit 0
        }
        Say "baseline: test -> $($plans['test'].Command)"
        $run = Invoke-PaperCommandLine $dir $plans['test'].Command
        $count = Get-PaperTestCountLine -Lines $run.Lines
        if ($count) { Say "baseline: tests: $count" }
        # F5: a run that executed no test has proved nothing, whatever it exited - and the caller reads the
        # exit code, not the wording, so the verdict has to reach it. 0 pass, 1 red, 4 not verifiable.
        $testVerdict = Get-PaperTestRunVerdict -ExitCode $run.Code -Output $run.Lines
        if ($testVerdict.ExitCode -eq 4) { Fail 4 "baseline: not verifiable in $dir - $($testVerdict.Reason)" }
        if ($testVerdict.ExitCode -ne 0) { Fail 1 "baseline: test is red in $dir ($($testVerdict.Reason)) - record the red tests as this task's baseline" }
        Say "baseline: green in $dir"
        exit 0
    }

    'done' {
        $main = Get-PaperMainRoot $Repo
        $path = Find-PaperWorktree $main $branch
        $hasBranch = Test-PaperBranch $main $branch
        if (-not $path -and -not $hasBranch) { Fail 2 "worktree: no worktree and no branch for '$Slug'" }
        if ($path) {
            $here = [System.IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') + '\'
            if ($here.StartsWith($path.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
                Fail 2 "worktree: run done from outside $path - a folder in use cannot be removed"
            }
        }

        $base = $Into
        if (-not $base) {
            $cfg = Invoke-PaperGit -C $main config --get "branch.$branch.paperflowBase"
            if ($cfg.Code -eq 0) { $base = $cfg.Text.Trim() }
        }
        if (-not $base) { Fail 2 "worktree: no base recorded for $branch - pass -Into <branch>" }
        if (-not (Test-PaperBranch $main $base)) { Fail 2 "worktree: base branch $base does not exist" }

        $uncommitted = 0
        if ($path) { $uncommitted = @((Invoke-PaperGit -C $path status --porcelain).Lines | Where-Object { $_ }).Count }
        $unmerged = 0
        if ($hasBranch) {
            $count = Invoke-PaperGit -C $main rev-list --count "$base..$branch"
            if ($count.Code -ne 0) { Fail 1 "worktree: git rev-list failed - $($count.Text)" }
            $unmerged = [int]($count.Text.Trim())
        }

        $done = Get-PaperWorktreeDoneVerdict -Branch $branch -Base $base -Unmerged $unmerged -Uncommitted $uncommitted -Merge $Merge.IsPresent -Discard $Discard.IsPresent
        if ($done.ExitCode -ne 0) { Fail $done.ExitCode "worktree: $($done.Reason)" }
        Say "worktree: $($done.Reason)"

        if ($done.Action -eq 'merge') {
            $head = Invoke-PaperGit -C $main symbolic-ref --short -q HEAD
            if ($head.Text.Trim() -ne $base) {
                Fail 1 "worktree: the main checkout $main is on '$($head.Text.Trim())', not on $base - switch it to $base, then run done -Merge again. Worktree kept."
            }
            $merged = Invoke-PaperGit -C $main merge --no-edit $branch
            if ($merged.Code -ne 0) {
                if (Test-Path -LiteralPath (Join-Path $main '.git\MERGE_HEAD')) { [void] (Invoke-PaperGit -C $main merge --abort) }
                Fail 1 "worktree: merging $branch into $base failed and was aborted - $($merged.Text). Worktree kept."
            }
            Say "worktree: merged $branch into $base"

            $testLine = $Test
            if (-not $testLine) {
                $plan = Get-PaperVerbPlan -ProjectProfile (Read-PaperWorktreeProfile $main) -Verb 'test'
                if ($plan.ExitCode -ne 0) {
                    Fail 5 "worktree: merged, but no test can run on $base ($($plan.Reason)) - not verifiable, worktree kept at $path. Verify, then run done $Slug"
                }
                $testLine = $plan.Command
            }
            Say "worktree: test on $base -> $testLine"
            $run = Invoke-PaperCommandLine $main $testLine
            $countLine = Get-PaperTestCountLine -Lines $run.Lines
            if ($countLine) { Say "worktree: tests: $countLine" }
            # F5, as in baseline: a run that executed no test proved nothing, and the worktree must not be
            # thrown away on it.
            $mergeVerdict = Get-PaperTestRunVerdict -ExitCode $run.Code -Output $run.Lines
            if ($mergeVerdict.ExitCode -eq 4) {
                Fail 4 "worktree: merged, but the test on $base is not verifiable ($($mergeVerdict.Reason)) - worktree kept at $path, branch $branch kept"
            }
            if ($mergeVerdict.ExitCode -ne 0) {
                Fail 1 "worktree: test is red on $base after the merge (exit $($run.Code)) - worktree kept at $path, branch $branch kept"
            }
        }

        if ($path) {
            $removeArgs = @('-C', $main, 'worktree', 'remove')
            if ($Discard) { $removeArgs += '--force' }
            $removed = Invoke-PaperGit @removeArgs $path
            if ($removed.Code -ne 0) { Fail 1 "worktree: could not remove $path - $($removed.Text)" }
            Say "worktree: removed $path"
        }
        if ($hasBranch) {
            $deleted = Invoke-PaperGit -C $main branch -D $branch
            if ($deleted.Code -ne 0) { Fail 1 "worktree: could not delete $branch - $($deleted.Text)" }
            Say "worktree: deleted $branch"
        }
        [void] (Invoke-PaperGit -C $main worktree prune)
        exit 0
    }
}
