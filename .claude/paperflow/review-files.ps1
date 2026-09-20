# The I/O half of the list of files to review (SPEC "Danh sach file phai xem", F20): find the branch's
# change, ask ocr or git for its files, and print the list. Every decision is in review-files-plan.ps1,
# which is pure and fully tested; the repository root, base branch and merge-base come from change-set.ps1.
#
# Entry point: `paperflow.ps1 review-files [-Base <branch>]`. To call it directly:
#   powershell -Command ". .\review-files.ps1; exit (Invoke-PaperReviewFiles -RepoRoot <root>)"
#
# THIS FILE DECLARES NO param() BLOCK, ON PURPOSE: it is dot-sourced by paperflow.ps1, and a param block
# would run in the runner's scope and blank its own parameters.
#
# The change = the branch against its merge-base with the base branch, plus uncommitted work, plus untracked
# files.
#   ocr (1.9.0 or newer): never through cmd.exe. npm installs ocr as an ocr.cmd shim, and cmd.exe re-reads
#     every argument of a .cmd - a file named src/R&D.cs makes it RUN D.cs, %VAR% expands, -x.cs becomes a
#     flag. So the shim's ocr.js is found and started as `node <ocr.js> ...` through ProcessStartInfo, each
#     argument escaped, with `--` before the paths; a shim with no .js behind it is not used (git, and the
#     first line says why). `delegate preview --format json` twice - the range from the merge-base to HEAD,
#     which sees commits only, and the working tree (no --from/--to), which sees staged, unstaged and
#     untracked work only (measured on v1.12.5) - merged, the working tree having the last word. Then
#     `delegate rule --format json -- <paths>` in batches cut by length.
#   git otherwise: `diff --name-status` and `--numstat` from the merge-base to the working tree, plus
#     untracked files; an untracked file with a NUL byte in its first 8000 is binary.
#   ocr failing, or printing JSON this cannot read: the list comes from git, and the first line says
#     `source git (ocr failed: <why>)` - the step is never skipped because of the tool.
# Then, whichever the source: a rename not committed yet folds into its new path (`git diff --name-status -M
# HEAD`), a file not on disk is out as missing, a gitlink or nested repository as submodule, and for git an
# obvious secret by its name as "secret_exclude (name)".
#
# Exit: 0 the list is made | 2 not verifiable: not a git repository, unknown -Base, no base branch, no
#       merge-base, or any unexpected error (with its message) - never 1 | 5 not applicable: no file changed
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'review-files-plan.ps1')
. (Join-Path $PSScriptRoot 'change-set.ps1')

# The empty tree: what a repository with no commit yet is measured from.
$script:PaperEmptyTree = '4b825dc642cb6eb9a060e54bf8d69288fbee4904'
# The characters of paths one `ocr delegate rule` call carries.
$script:PaperOcrRuleMaxChars = 6000

# How to start ocr without cmd.exe: a native ocr.exe as it is, or the ocr.js an npm .cmd shim runs, with the
# node.exe next to the shim or the one on PATH. Problem says why no ocr can be used ('' = none on PATH).
function Find-PaperOcr {
    $problem = ''
    foreach ($c in @(Get-Command ocr -CommandType Application -All -ErrorAction SilentlyContinue)) {
        $path = $c.Source
        $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
        if (@('.exe', '.com') -contains $ext) {
            return [pscustomobject]@{ FileName = $path; Prefix = @(); Problem = '' }
        }
        if (@('.cmd', '.bat') -notcontains $ext) { continue }
        $dir = Split-Path $path -Parent
        $rel = Get-PaperOcrShimScript -ShimText ([IO.File]::ReadAllText($path))
        $js = if ($rel) { [IO.Path]::GetFullPath((Join-Path $dir $rel)) } else { '' }
        if (-not $js -or -not [IO.File]::Exists($js)) {
            $problem = "$path runs no ocr.js to start with node, and ocr is never run through cmd.exe"
            continue
        }
        $node = Join-Path $dir 'node.exe'
        if (-not [IO.File]::Exists($node)) {
            $node = @(Get-Command node.exe -CommandType Application -ErrorAction SilentlyContinue | ForEach-Object { $_.Source })[0]
        }
        if (-not $node) { $problem = "no node.exe to run $js"; continue }
        return [pscustomobject]@{ FileName = $node; Prefix = @($js); Problem = '' }
    }
    return [pscustomobject]@{ FileName = ''; Prefix = @(); Problem = $problem }
}

# One ocr call: no shell in between, every argument escaped (ConvertTo-PaperProcessArgument), stdout read as
# UTF-8 (rule text is not ASCII), stderr kept apart.
function Invoke-PaperOcr($Ocr, [string[]] $Arguments, [string] $WorkingDirectory) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Ocr.FileName
    $psi.Arguments = (@(@($Ocr.Prefix) + @($Arguments) | ForEach-Object { ConvertTo-PaperProcessArgument "$_" }) -join ' ')
    if ($WorkingDirectory) { $psi.WorkingDirectory = $WorkingDirectory }
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = New-Object System.Text.UTF8Encoding $false
    $psi.StandardErrorEncoding = New-Object System.Text.UTF8Encoding $false
    try {
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Close()
        $errTask = $p.StandardError.ReadToEndAsync()
        $out = $p.StandardOutput.ReadToEnd()
        $p.WaitForExit()
        $err = $errTask.Result
        $code = $p.ExitCode
    }
    catch { return [pscustomobject]@{ Code = 1; Text = ''; Error = $_.Exception.Message } }
    $errLines = @("$err" -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    return [pscustomobject]@{ Code = $code; Text = "$out"; Error = ($errLines -join ' / ') }
}

function Test-PaperBinaryFile([string] $FullPath) {
    try {
        $stream = [IO.File]::OpenRead($FullPath)
        try {
            $buffer = New-Object byte[] 8000
            $n = $stream.Read($buffer, 0, $buffer.Length)
            for ($i = 0; $i -lt $n; $i++) { if ($buffer[$i] -eq 0) { return $true } }
            return $false
        }
        finally { $stream.Dispose() }
    }
    catch { return $false }
}

function Get-PaperReviewFullPath([string] $RepoRoot, [string] $Rel) {
    return (Join-Path $RepoRoot (("$Rel".TrimEnd('/')) -replace '/', '\'))
}

function Get-PaperReviewFilesFromGit([string] $RepoRoot, $Change) {
    $from = if ($Change.HasHead) { $Change.MergeBase } else { $script:PaperEmptyTree }
    $nameStatus = Invoke-PaperChangeGit -C $RepoRoot diff --name-status -M --no-color --no-ext-diff $from
    $numStat = Invoke-PaperChangeGit -C $RepoRoot diff --numstat -M --no-color --no-ext-diff $from
    if ($nameStatus.Code -ne 0 -or $numStat.Code -ne 0) { throw "git diff against $from failed" }
    $files = @(ConvertFrom-PaperGitNameStatus -NameStatus $nameStatus.Lines -NumStat $numStat.Lines)

    $untracked = @(Get-PaperChangeUntracked $RepoRoot)
    $binary = @()
    $lineCounts = @{}
    foreach ($rel in $untracked) {
        $full = Get-PaperReviewFullPath $RepoRoot $rel
        if (-not [IO.File]::Exists($full)) { continue }
        if (Test-PaperBinaryFile $full) { $binary += $rel; continue }
        try { $lineCounts[$rel] = @([IO.File]::ReadAllLines($full)).Count } catch { }
    }
    return @(Merge-PaperReviewFiles -First $files -Then @(ConvertFrom-PaperGitUntracked -Paths $untracked -BinaryPaths $binary -Insertions $lineCounts))
}

# The files from ocr, or Failed with the reason when ocr exits non-zero or prints JSON this cannot read.
function Get-PaperReviewFilesFromOcr([string] $RepoRoot, $Change, $Ocr) {
    $sets = @()
    $head = (Invoke-PaperChangeGit -C $RepoRoot rev-parse HEAD).Text.Trim()
    if (Test-PaperReviewNeedsRange -HasHead ([bool] $Change.HasHead) -MergeBase $Change.MergeBase -Head $head) {
        $sets += , @('delegate', 'preview', '--format', 'json', '--repo', $RepoRoot, '--from', $Change.MergeBase, '--to', 'HEAD')
    }
    $sets += , @('delegate', 'preview', '--format', 'json', '--repo', $RepoRoot)

    $files = @()
    foreach ($a in $sets) {
        $run = Invoke-PaperOcr $Ocr $a $RepoRoot
        if ($run.Code -ne 0) {
            return [pscustomobject]@{ Failed = "ocr delegate preview failed (exit $($run.Code)): $($run.Error)"; Files = @() }
        }
        $p = ConvertFrom-PaperOcrPreview -Json $run.Text
        if ($p.ExitCode -ne 0) { return [pscustomobject]@{ Failed = $p.Reason; Files = @() } }
        $files = @(Merge-PaperReviewFiles -First $files -Then $p.Files)
    }
    return [pscustomobject]@{ Failed = ''; Files = $files }
}

function Get-PaperReviewRuleGroups([string] $RepoRoot, $Files, $Ocr) {
    $groups = @()
    foreach ($batch in @(Split-PaperOcrRuleBatches -Paths @(Get-PaperOcrRulePaths -Files $Files) -MaxChars $script:PaperOcrRuleMaxChars)) {
        $run = Invoke-PaperOcr $Ocr (@('delegate', 'rule', '--format', 'json', '--repo', $RepoRoot, '--') + @($batch)) $RepoRoot
        if ($run.Code -ne 0) {
            return [pscustomobject]@{ Failed = "ocr delegate rule failed (exit $($run.Code)): $($run.Error)"; Groups = @() }
        }
        $g = ConvertFrom-PaperOcrRules -Json $run.Text
        if ($g.ExitCode -ne 0) { return [pscustomobject]@{ Failed = $g.Reason; Groups = @() } }
        $groups += @($g.Groups)
    }
    return [pscustomobject]@{ Failed = ''; Groups = @(Merge-PaperRuleGroups $groups) }
}

# The merged list checked against the working tree (Resolve-PaperReviewFiles decides): renames not committed
# yet, files not on disk, gitlinks and nested repositories.
function Resolve-PaperReviewOnDisk([string] $RepoRoot, $Change, $Files, [bool] $SecretNames) {
    $renames = @()
    if ($Change.HasHead) {
        $renames = @(ConvertFrom-PaperGitRenames -NameStatus @((Invoke-PaperChangeGit -C $RepoRoot diff --name-status -M --no-color --no-ext-diff HEAD).Lines))
    }
    $submodules = @(ConvertFrom-PaperGitGitlinks -StageLines @((Invoke-PaperChangeGit -C $RepoRoot ls-files -s).Lines))
    $missing = @()
    foreach ($f in @($Files)) {
        if ($null -eq $f) { continue }
        $full = Get-PaperReviewFullPath $RepoRoot $f.Path
        if ([IO.Directory]::Exists($full)) { $submodules += $f.Path; continue }
        if (-not [IO.File]::Exists($full)) { $missing += $f.Path }
    }
    return @(Resolve-PaperReviewFiles -Files $Files -Renames $renames -Missing $missing -Submodules $submodules -SecretNames:$SecretNames)
}

function Invoke-PaperReviewFiles {
    param(
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        [string] $Base
    )

    # UTF-8 both ways: ocr prints UTF-8 (rule text is not ASCII), and so does this report.
    try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }
    try {
        $change = Get-PaperChangeBase -RepoRoot $RepoRoot -Base $Base -Tool 'review-files'
        if ($change.Code -ne 0) {
            [Console]::Error.WriteLine($change.Message)
            return $change.Code
        }
        $RepoRoot = $change.RepoRoot

        $ocr = Find-PaperOcr
        $versionText = ''
        $versionExit = 0
        if ($ocr.FileName) {
            $v = Invoke-PaperOcr $ocr @('--version') $RepoRoot
            if ($v.Code -eq 0) { $versionText = $v.Text } else { $versionExit = $v.Code }
        }
        $source = Select-PaperReviewSource -OcrVersionText $versionText -VersionExitCode $versionExit -NotRunnable $ocr.Problem

        $files = $null
        $groups = @()
        $failed = ''
        if ($source.Source -eq 'ocr') {
            $found = Get-PaperReviewFilesFromOcr $RepoRoot $change $ocr
            if ($found.Failed) { $failed = $found.Failed }
            else {
                $pick = Get-PaperReviewSourceLabel -Selected $source
                $files = @(Resolve-PaperReviewOnDisk $RepoRoot $change $found.Files $pick.SecretNames)
                $rules = Get-PaperReviewRuleGroups $RepoRoot $files $ocr
                if ($rules.Failed) { $files = $null; $failed = $rules.Failed }
                else { $groups = @($rules.Groups) }
            }
        }
        $pick = Get-PaperReviewSourceLabel -Selected $source -OcrFailed $failed
        if ($null -eq $files) {
            $files = @(Resolve-PaperReviewOnDisk $RepoRoot $change @(Get-PaperReviewFilesFromGit $RepoRoot $change) $pick.SecretNames)
            $groups = @()
        }
        $label = $pick.Label

        $list = Get-PaperReviewList -Files $files -Groups $groups
        foreach ($line in @(Format-PaperReviewList -List $list -From (Format-PaperChangeFrom $change) -Source $label)) {
            [Console]::Out.WriteLine($line)
        }
        return $list.ExitCode
    }
    catch {
        [Console]::Error.WriteLine("review-files: not verifiable: $($_.Exception.Message)")
        return 2
    }
}
