# The I/O half of the API lookup check (SPEC "Tra API truoc khi goi", F17): read one plan and the branch's
# change, and report. Every decision is in api-check-plan.ps1, which is pure and fully tested.
#
# Entry point: `paperflow.ps1 api-check -Path <plan file> [-Base <branch>]`. To call it directly:
#   powershell -Command ". .\api-check.ps1; exit (Invoke-PaperApiCheck -Path <plan> -RepoRoot <root>)"
#
# THIS FILE DECLARES NO param() BLOCK, ON PURPOSE: it is dot-sourced by paperflow.ps1, and a param block
# would run in the runner's scope and blank its own -Path (see tasks-gate.ps1).
#
# The change = the branch against the point where it left its base branch, plus uncommitted work
# (git diff -U0 <merge-base> compares the working tree, so both arrive in one diff) plus untracked source
# files, whole. The added lines decide WHICH files are this task's; for each of them this reads the whole
# file as it is now and, once per project folder (the nearest .csproj/.vbproj/.fsproj above it), the
# project's own imports - its project file, Directory.Build.props/.targets up to the repository root, and
# the global usings of its .cs/.vb files - so a file that uses the host through an old using or a global
# using is still a host use (api-check-plan.ps1 decides).
#
# The base branch and the merge-base come from change-set.ps1, which review-files reads too: -Base, then
# branch.<current>.paperflowBase, then main, master, origin/main, origin/master; none of them is exit 2, not
# verifiable, and so is a base with no merge-base (a shallow clone). Working straight on the base branch:
# origin/<branch> when it exists, otherwise only uncommitted and untracked work counts. Paths are from the
# top of the working tree (git rev-parse --show-toplevel), and so are the untracked files.
#
# The host namespaces: the profile's api.namespaces, and nothing else - no lock, no kit folder. A project set
# up from a host template gets the key in its profile; a project set up before 1.1.0 declares it by hand,
# because setup never rewrites a profile. No key: exit 5, and the message says to declare api.namespaces.
#
# Exit: 0 pass | 1 host used and the API table is empty (F17) | 2 bad request or not verifiable (no plan,
#       not a git repository, an unknown -Base, no base branch, no merge-base) | 5 not applicable
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'api-check-plan.ps1')
. (Join-Path $PSScriptRoot 'change-set.ps1')

# Native git, run the one way the kit runs it (change-set.ps1).
function Invoke-PaperApiGit { Invoke-PaperChangeGit @args }

function Get-PaperApiRelative([string] $RepoRoot, [string] $FullPath) {
    $root = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($FullPath)
    if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { $full = $full.Substring($root.Length) }
    return ($full -replace '\\', '/')
}

function Get-PaperApiProjectFiles([string] $Dir) {
    try { return @([IO.Directory]::GetFiles($Dir) | Where-Object { $_ -match '\.(?:cs|vb|fs)proj$' }) }
    catch { return @() }
}

# The project a file belongs to: the nearest folder at or above it, inside the repository, that holds a
# .csproj/.vbproj/.fsproj. Every folder walked is remembered, so the next file of the project costs nothing.
function Find-PaperApiProjectDir([string] $RepoRoot, [string] $FullPath, [hashtable] $DirCache) {
    $root = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\')
    $dir = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($FullPath))
    $walked = New-Object System.Collections.Generic.List[string]
    $found = $null
    while ($dir -and $dir.Length -ge $root.Length) {
        if ($DirCache.ContainsKey($dir)) { $found = $DirCache[$dir]; break }
        $walked.Add($dir)
        if (@(Get-PaperApiProjectFiles $dir).Count -gt 0) { $found = $dir; break }
        if ($dir.Length -eq $root.Length) { break }
        $dir = [IO.Path]::GetDirectoryName($dir)
    }
    foreach ($w in $walked) { $DirCache[$w] = $found }
    return $found
}

# Whether a project imports a host namespace for all its files (Get-PaperProjectHostImport decides), read
# once per project folder: its project file, the Directory.Build.props/.targets from it up to the
# repository root, and its .cs/.vb files outside bin and obj that mention "global using" at all.
function Get-PaperApiProjectImport([string] $RepoRoot, [string] $ProjectDir, [string[]] $Namespaces, [hashtable] $ImportCache) {
    if ($ImportCache.ContainsKey($ProjectDir)) { return $ImportCache[$ProjectDir] }
    $root = [IO.Path]::GetFullPath($RepoRoot).TrimEnd('\')
    $read = { param($full) [pscustomobject]@{ Path = (Get-PaperApiRelative $RepoRoot $full); Text = [IO.File]::ReadAllText($full, [Text.Encoding]::UTF8) } }

    $projectFiles = @(Get-PaperApiProjectFiles $ProjectDir | ForEach-Object { & $read $_ })
    $dir = $ProjectDir
    while ($dir -and $dir.Length -ge $root.Length) {
        foreach ($name in @('Directory.Build.props', 'Directory.Build.targets')) {
            $candidate = Join-Path $dir $name
            if ([IO.File]::Exists($candidate)) { $projectFiles += & $read $candidate }
        }
        if ($dir.Length -eq $root.Length) { break }
        $dir = [IO.Path]::GetDirectoryName($dir)
    }

    $sources = @()
    try { $all = @([IO.Directory]::EnumerateFiles($ProjectDir, '*', [IO.SearchOption]::AllDirectories)) } catch { $all = @() }
    foreach ($full in $all) {
        if ($full -notmatch '\.(?:cs|vb)$') { continue }
        if ($full.Substring($ProjectDir.Length) -match '\\(?:bin|obj|\.git|\.vs|node_modules)\\') { continue }
        $text = [IO.File]::ReadAllText($full, [Text.Encoding]::UTF8)
        if ($text -notmatch 'global\s+using') { continue }
        $sources += [pscustomobject]@{ Path = (Get-PaperApiRelative $RepoRoot $full); Text = $text }
    }

    $import = Get-PaperProjectHostImport -SourceFiles $sources -ProjectFiles $projectFiles -Namespaces $Namespaces
    $ImportCache[$ProjectDir] = $import
    return $import
}

function Invoke-PaperApiCheck {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $RepoRoot,
        $ProjectProfile,
        [string] $Base
    )

    try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }
    $say = { param($line) [Console]::Out.WriteLine($line) }

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        [Console]::Error.WriteLine("api-check: no such plan file: $Path")
        return 2
    }
    # -Encoding UTF8: 5.1 defaults to the ANSI codepage, and the heading carries diacritics.
    $planLines = @(Get-Content -LiteralPath $Path -Encoding UTF8)

    # The profile is the only source: no lock, no kit folder (an installed project has neither).
    $namespaces = @(Get-PaperApiNamespaces -ProjectProfile $ProjectProfile)
    if ($namespaces.Count -eq 0) {
        $v = Get-PaperApiCheckVerdict -Rows @() -Usage @() -Namespaces @() -Declared (Test-PaperApiNamespacesDeclared $ProjectProfile)
        & $say "api-check: NOT APPLICABLE - $($v.Reason)"
        return 5
    }

    # ---- the base branch and the merge-base: change-set.ps1, shared with review-files ------------------
    $change = Get-PaperChangeBase -RepoRoot $RepoRoot -Base $Base -Tool 'api-check'
    if ($change.Code -ne 0) {
        [Console]::Error.WriteLine($change.Message)
        return $change.Code
    }
    # Every path from the top of the working tree, whatever folder the verb was started from.
    $RepoRoot = $change.RepoRoot
    $mergeBase = $change.MergeBase
    $hasHead = $change.HasHead

    # ---- the change: diff against the merge-base (committed + uncommitted), plus untracked files ---------
    $extensions = @(Get-PaperApiStrings (Get-PaperApiValue (Get-PaperApiValue $ProjectProfile 'codeMap').Value 'extensions').Value)
    if ($extensions.Count -eq 0) { $extensions = $script:PaperApiCodeExtensions }

    $files = @()
    if ($hasHead) {
        $diff = Invoke-PaperApiGit -C $RepoRoot diff -U0 --no-color --no-ext-diff $mergeBase
        if ($diff.Code -ne 0) {
            [Console]::Error.WriteLine("api-check: git diff against $mergeBase failed")
            return 2
        }
        $files = @(ConvertFrom-PaperUnifiedDiff -Lines $diff.Lines)
    }
    foreach ($rel in @(Get-PaperChangeUntracked $RepoRoot)) {
        if (-not $rel -or -not (Test-PaperApiCodePath -Path $rel -Extensions $extensions)) { continue }
        $full = Join-Path $RepoRoot ($rel -replace '/', '\')
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
        $files += [pscustomobject]@{ Path = $rel; Added = @([IO.File]::ReadAllLines($full, [Text.Encoding]::UTF8)) }
    }

    # ---- each code file with added lines: the whole file as it is now, and its project's host import -----
    $dirCache = @{}
    $importCache = @{}
    $withContext = @()
    foreach ($f in $files) {
        $rel = [string] $f.Path
        $full = Join-Path $RepoRoot ($rel -replace '/', '\')
        $whole = $null
        $import = $null
        if (@($f.Added).Count -gt 0 -and (Test-PaperApiCodePath -Path $rel -Extensions $extensions) -and (Test-Path -LiteralPath $full -PathType Leaf)) {
            $whole = @([IO.File]::ReadAllLines($full, [Text.Encoding]::UTF8))
            $projectDir = Find-PaperApiProjectDir $RepoRoot $full $dirCache
            if ($projectDir) { $import = Get-PaperApiProjectImport $RepoRoot $projectDir $namespaces $importCache }
        }
        $withContext += [pscustomobject]@{ Path = $rel; Added = @($f.Added); Lines = $whole; Import = $import }
    }
    $files = $withContext

    $rows = @(Get-PaperApiTableRows -Lines $planLines)
    $usage = @(Get-PaperHostUsage -Files $files -Namespaces $namespaces -Extensions $extensions)
    $verdict = Get-PaperApiCheckVerdict -Rows $rows -Usage $usage -Namespaces $namespaces

    $label = switch ($verdict.ExitCode) { 0 { 'PASS' } 1 { 'FAIL' } 5 { 'NOT APPLICABLE' } default { "EXIT $($verdict.ExitCode)" } }
    & $say "api-check: $label - $($verdict.Reason)"
    foreach ($u in $usage) { & $say ("  file  {0}    {1}" -f $u.Path, $u.Why) }
    $unlisted = @($verdict.Unlisted)
    if ($verdict.ExitCode -ne 5 -and $unlisted.Count -gt 0) {
        & $say 'not in the API table - review each one, this is a list and not a failure:'
        foreach ($m in $unlisted) { & $say ("  {0}    {1}" -f $m.Member, $m.Path) }
    }
    $from = Format-PaperChangeFrom $change -WithWorkingTree
    & $say "       change: $from"
    & $say "       namespaces: $($namespaces -join ', ')"
    & $say "       $Path"
    return $verdict.ExitCode
}
