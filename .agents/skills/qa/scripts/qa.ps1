# /qa, the I/O part (plan 2026-10-02-qa-command, table K): git, the disk, `dotnet restore`, the build verb.
# Every decision lives in qa-plan.ps1, qa-sarif-plan.ps1, qa-external-plan.ps1 and qa-lanes-plan.ps1, which are pure and fully
# tested: each command reads what it needs, hands it to one of their functions, prints its lines and exits
# with its code.
#
#   qa.ps1 plan    [-Scope project|branch|path] [-Path <folder>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes]
#   qa.ps1 lanes   [-Scope project|branch|path] [-Path <folder>] [-Base <branch>] [-Pick <numbers|lanes|all>]
#   qa.ps1 approve -Run <run>
#   qa.ps1 static  -Run <run>
#   qa.ps1 files   -Run <run> -Lane <lane> [-Batch <n>] [-Retry]
#   qa.ps1 check   -Run <run>
#   qa.ps1 report  -Run <run>
#   qa.ps1 init    -Repo <repository> -Out <folder>
#   (every command takes -Repo <project root>; default: the current folder)
#
# A run lives in <root>\.paper\qa\<run>\ (plan.json, lanes\, verdicts\, sarif\, static.json, findings.json);
# <root>\.paper\qa\.gitignore holds one line "*", so git sees nothing there. The one other file a run
# writes is its report under the profile's qa.report folder.
#
# An external repository (-Out <folder> with -Repo, ADR-0034): nothing is written into the repository. The
# qa profile is <Out>\qa.profile.json (qa.ps1 init writes its skeleton), runs live in <Out>\runs\<run>\ and
# reports in <Out>\reports\. No script of the repository runs (paperflow is the kit's own), git runs with
# GIT_OPTIONAL_LOCKS=0 so it never refreshes the repository's index, and the static lane runs the owner's
# declared build line only when it carries every declared no-deploy property; a build that changes a file
# git does not ignore leaves the static lane not verifiable (qa-external-plan.ps1).
#
# Exit codes:
#   0  done
#   1  check found answers to send back
#   2  invalid request, broken profile, unknown run (F66, F67); "not verifiable:" when the branch scope has
#      no base (F69); a wrong qa.profile.json or -Out (external repository, F91-F95, F98)
#   4  not verifiable: the run is not approved (F63), or the static lane could not run (F57, F58)
#   5  NOT APPLICABLE: no file in scope (F65), or the lane does not run
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string] $Command = 'help',
    [string] $Repo = (Get-Location).Path,
    [string] $Run,
    [string] $Scope,
    [string] $Path,
    [string] $Base,
    [string[]] $Only,
    [string[]] $Skip,
    [switch] $StaticOnly,
    [switch] $Yes,
    [string] $Lane,
    [int] $Batch = 1,
    [switch] $Retry,
    [string] $Out,
    [string[]] $Pick
)

$ErrorActionPreference = 'Stop'
# git prints paths as UTF-8 bytes; read them, and write ours, as UTF-8 whatever the console codepage.
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

. (Join-Path $PSScriptRoot 'qa-sarif-plan.ps1')
. (Join-Path $PSScriptRoot 'qa-plan.ps1')
. (Join-Path $PSScriptRoot 'qa-external-plan.ps1')
. (Join-Path $PSScriptRoot 'qa-lanes-plan.ps1')

$script:Utf8 = New-Object System.Text.UTF8Encoding $false
$script:PowerShellExe = Join-Path $PSHOME 'powershell.exe'

function Say([string] $Line) { [Console]::Out.WriteLine($Line) }
function Stop-Qa([int] $Code, [string] $Line) { if ($Line) { Say $Line }; exit $Code }

function Show-Help {
    Say 'qa - on-demand QA sweep of a project, a branch or a folder'
    Say '  qa.ps1 plan [-Scope project|branch|path] [-Path <folder>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes]'
    Say '  qa.ps1 lanes [-Scope ...] [-Path <folder>] [-Base <branch>] [-Pick 1,3|all|<lanes>]   (the lanes that apply and their cost; writes nothing)'
    Say '  qa.ps1 approve|static|check|report -Run <run>'
    Say '  qa.ps1 files -Run <run> -Lane <lane> [-Batch <n>] [-Retry]'
    Say '  qa.ps1 init -Repo <repository> -Out <folder>   (an external repository: nothing is written into it)'
    Say '  every command takes -Out <folder> with -Repo for an external repository'
    Say 'Exit: 0 done | 1 answers to send back | 2 invalid request | 4 not verifiable | 5 not applicable'
}

# Runs a native command; returns @{ Code; Lines }. Stderr is folded into Lines.
function Invoke-Native([string] $Exe, [string[]] $Arguments) {
    $ErrorActionPreference = 'Continue'
    $lines = @(& $Exe @Arguments 2>&1 | ForEach-Object { "$_" })
    $code = $LASTEXITCODE
    return [pscustomobject]@{ Code = $code; Lines = $lines }
}

function Write-Text([string] $File, [string] $Text) {
    $dir = Split-Path -Parent $File
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText($File, $Text, $script:Utf8)
}
function Write-Json([string] $File, $Object) { Write-Text $File (($Object | ConvertTo-Json -Depth 12) + "`n") }
function Read-Json([string] $File) { return ([IO.File]::ReadAllText($File, [Text.Encoding]::UTF8) | ConvertFrom-Json) }
function Read-Lines([string] $File) { return , [IO.File]::ReadAllLines($File, [Text.Encoding]::UTF8) }

# ------------------------------------------------------------------ where things are

if ($Command -in @('help', '-h', '--help', '/?')) { Show-Help; exit 0 }
if ($Command -notin @('init', 'lanes', 'plan', 'approve', 'static', 'files', 'check', 'report')) { Stop-Qa 2 "qa: unknown command '$Command' (init, lanes, plan, approve, static, files, check, report)" }
# The lane question (ADR-0035): -Pick belongs to lanes, and lanes lists every lane with no choice applied.
$pickGiven = $PSBoundParameters.ContainsKey('Pick')
if ($pickGiven -and $Command -ne 'lanes') { Stop-Qa 2 'qa: -Pick goes with qa.ps1 lanes' }
if ($Command -eq 'lanes' -and (@('Only', 'Skip', 'StaticOnly', 'Yes') | Where-Object { $PSBoundParameters.ContainsKey($_) })) { Stop-Qa 2 'qa: lanes lists every lane that applies - -Only, -Skip, -StaticOnly and -Yes go with plan' }
# The scope parameters as typed, in plan's order, for the plan line of lanes -Pick (F101).
$planArgs = [ordered]@{}
foreach ($k in @('Scope', 'Path', 'Base', 'Repo', 'Out')) { if ($PSBoundParameters.ContainsKey($k)) { $planArgs[$k] = [string] $PSBoundParameters[$k] } }
$external = [bool] $Out
if ($Command -eq 'init' -and -not $external) { Stop-Qa 2 'qa: init needs -Out <folder outside the repository>' }
if ($external) {
    # A2: before the first git command, so no git of this run, nor of a process it starts, takes the
    # repository's index lock to refresh it.
    if (-not $PSBoundParameters.ContainsKey('Repo')) { Stop-Qa 2 'qa: -Out needs -Repo <repository> - the repository to read' }
    [Environment]::SetEnvironmentVariable('GIT_OPTIONAL_LOCKS', '0')
}
if (-not (Test-Path -LiteralPath $Repo -PathType Container)) { Stop-Qa 2 "qa: project folder not found: $Repo" }
$top = Invoke-Native 'git.exe' @('-C', $Repo, 'rev-parse', '--show-toplevel')
if ($top.Code -ne 0 -or -not $top.Lines) { Stop-Qa 2 "qa: not a git repository: $Repo" }
$root = ([string] $top.Lines[0]).Trim().Replace('/', '\').TrimEnd('\')

if ($external) {
    # A4 (F91): -Out outside the repository, by its text against the git root and -Repo as typed, and by git
    # itself from the nearest folder of -Out that exists (an 8.3 path and its long form are one folder).
    $outFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out).TrimEnd('\')
    $repoGiven = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Repo).TrimEnd('\')
    if (Test-Path -LiteralPath $outFull -PathType Leaf) { Stop-Qa 2 "qa: -Out is a file, not a folder: $outFull" }
    # Spec bo sung 2026-10-03 (find-bug T8 FB4): the repository's git folder too - inside it --show-toplevel
    # fails, so a junction or an 8.3 name into .git is caught by --absolute-git-dir.
    $norm = { param($r) if ($r.Code -eq 0 -and $r.Lines) { ([string] $r.Lines[0]).Trim().Replace('/', '\').TrimEnd('\') } else { '' } }
    $gitDir = & $norm (Invoke-Native 'git.exe' @('-C', $root, 'rev-parse', '--absolute-git-dir'))
    $inside = -not (Test-PaperQaOutsideRepo -Out $outFull -RepoRoots @(@($root, $repoGiven, $gitDir) | Where-Object { $_ }))
    if (-not $inside) {
        $probe = $outFull
        while ($probe -and -not (Test-Path -LiteralPath $probe -PathType Container)) { $probe = Split-Path -Parent $probe }
        if ($probe) {
            if ((& $norm (Invoke-Native 'git.exe' @('-C', $probe, 'rev-parse', '--show-toplevel'))) -ieq $root) { $inside = $true }
            elseif ($gitDir -and (& $norm (Invoke-Native 'git.exe' @('-C', $probe, 'rev-parse', '--absolute-git-dir'))) -ieq $gitDir) { $inside = $true }
        }
    }
    if ($inside) { Stop-Qa 2 "qa: -Out must be a folder outside the repository: $outFull is inside $root" }
}

# paperflow (F97): a project looks beside the skill (a Claude project, the kit source), then in the project;
# an external repository only in the kit (beside the skill, or the .claude beside the Codex .agents).
$paperflow = $null
foreach ($candidate in @(Get-PaperQaPaperflowCandidates -ScriptRoot $PSScriptRoot -RepoRoot $root -External $external)) {
    if (Test-Path -LiteralPath (Join-Path $candidate 'paperflow.ps1') -PathType Leaf) { $paperflow = (Resolve-Path -LiteralPath $candidate).ProviderPath; break }
}
if (-not $paperflow) { Stop-Qa 2 'qa: paperflow not found (.claude/paperflow/paperflow.ps1) - run paper-kit setup' }
# The kit's own readers, reused rather than copied: review-files.ps1 (Test-PaperBinaryFile, and with it
# review-files-plan.ps1's Test-PaperSecretName), tasks-gate-plan.ps1 (the glob rules), profile-map.ps1,
# verb-plan.ps1.
. (Join-Path $paperflow 'review-files.ps1')
. (Join-Path $paperflow 'tasks-gate-plan.ps1')
. (Join-Path $paperflow 'profile-map.ps1')
. (Join-Path $paperflow 'verb-plan.ps1')

$profileMap = $null
$config = $null
$profileFile = ''
$reportsDir = ''
if ($external) {
    # A6: the qa profile of the repository lives under -Out; the repository's own profile is never read.
    $profileFile = Join-Path $outFull 'qa.profile.json'
    if (Test-Path -LiteralPath $profileFile -PathType Leaf) {
        try { $json = [IO.File]::ReadAllText($profileFile, [Text.Encoding]::UTF8) | ConvertFrom-Json }
        catch { Stop-Qa 2 "qa: $profileFile is not valid JSON - $($_.Exception.Message)" }
        $config = ConvertFrom-PaperQaExternalProfile -Profile (ConvertTo-PaperMap $json -SkipDocKeys) -RepoRoot $root
        if (@($config.Errors).Count -gt 0) {
            foreach ($e in $config.Errors) { Say "qa: $e" }
            exit 2
        }
    }
    elseif ($Command -ne 'init') { Stop-Qa 2 "qa: no qa.profile.json in $outFull - run qa.ps1 init -Repo $root -Out $outFull first" }
    $runsDir = Join-Path $outFull 'runs'
    $reportsDir = Join-Path $outFull 'reports'
    if ($null -ne $config) { $config.Report = $reportsDir }
}
else {
    $read = Read-PaperProfileFile $root
    if ($read.Error) { Stop-Qa 2 "qa: .claude/paper.profile.json is not valid JSON - $($read.Error)" }
    $profileMap = $read.Map
    $config = ConvertFrom-PaperQaProfile -Profile $profileMap
    if (@($config.Errors).Count -gt 0) {
        foreach ($e in $config.Errors) { Say "qa: $e" }
        exit 2
    }
    $runsDir = Join-Path $root '.paper\qa'
}
$dataDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\data')).ProviderPath

function Initialize-QaRunsDir {
    if (-not (Test-Path -LiteralPath $runsDir -PathType Container)) { New-Item -ItemType Directory -Force -Path $runsDir | Out-Null }
    $ignore = Join-Path $runsDir '.gitignore'
    if (-not (Test-Path -LiteralPath $ignore -PathType Leaf)) { Write-Text $ignore "*`n" }
}

function Get-QaRunDir {
    if (-not $Run) { Stop-Qa 2 "qa: $Command needs -Run <run>" }
    $dir = Join-Path $runsDir $Run
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'plan.json') -PathType Leaf)) {
        if ($external) { Stop-Qa 2 "qa: no run $Run (looked in $runsDir)" }
        Stop-Qa 2 "qa: no run $Run (looked in .paper/qa)"
    }
    return $dir
}

# Every file of the repository git knows: tracked plus untracked-not-ignored, '/' separated.
function Get-QaRepoFiles {
    # The kit's git runner (change-set.ps1): stdout only, so a git warning never reads as a path.
    $t = Invoke-PaperChangeGit -C $root ls-files -z
    $u = Invoke-PaperChangeGit -C $root ls-files -z --others --exclude-standard
    if ($t.Code -ne 0 -or $u.Code -ne 0) { Stop-Qa 2 "qa: git ls-files failed: $(@($t.Lines + $u.Lines) -join ' / ')" }
    $split = { param($lines) @((@($lines) -join "`n") -split "`0" | ForEach-Object { $_.Trim("`n") } | Where-Object { $_ }) }
    return [pscustomobject]@{ Tracked = @(& $split $t.Lines); Untracked = @(& $split $u.Lines) }
}

# Runs a block with environment variables set for the processes it starts, and puts them back after.
function Invoke-QaWithEnvironment($Variables, [scriptblock] $Block) {
    $saved = @{}
    foreach ($k in @($Variables.Keys)) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $Variables[$k]) }
    try { return (& $Block) }
    finally { foreach ($k in @($Variables.Keys)) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) } }
}

function Get-QaFullPath([string] $Rel) { return (Join-Path $root ($Rel.Replace('/', '\'))) }

# Sizes of the files that are on disk: path -> bytes (a missing key = not on disk).
function Get-QaSizes([string[]] $Paths) {
    $sizes = @{}
    foreach ($p in @($Paths)) {
        $full = Get-QaFullPath $p
        if (Test-Path -LiteralPath $full -PathType Leaf) { $sizes[$p] = (Get-Item -LiteralPath $full).Length }
    }
    return $sizes
}

# The repository as git sees it, for F94: every path `git status` lists -> "XY|length|ticks" ("XY|missing").
function Get-QaRepoSnapshot {
    $st = Invoke-PaperChangeGit -C $root -c core.quotepath=false status --porcelain --untracked-files=all
    if ($st.Code -ne 0) { return $null }
    $snap = @{}
    foreach ($line in @($st.Lines)) {
        $p = @(Get-PaperQaStatusPaths -Lines @($line))
        if ($p.Count -eq 0) { continue }
        $xy = "$line".Substring(0, 2)
        $full = Get-QaFullPath $p[0]
        if (Test-Path -LiteralPath $full -PathType Leaf) { $i = Get-Item -LiteralPath $full; $snap[$p[0]] = "$xy|$($i.Length)|$($i.LastWriteTimeUtc.Ticks)" }
        else { $snap[$p[0]] = "$xy|missing" }
    }
    return $snap
}

# C (F95): the rule files of an external repository with their size; exit 2 naming each wrong rules entry.
function Get-QaExternalRules([string[]] $Paths) {
    $rr = Resolve-PaperQaRules -Declared $config.DeclaredRules -Paths $Paths
    if (@($rr.Errors).Count -gt 0) {
        foreach ($e in $rr.Errors) { Say "qa: $e" }
        exit 2
    }
    $sizes = Get-QaSizes @($rr.Rules)
    return @($rr.Rules | Where-Object { $_ } | ForEach-Object { [pscustomobject]@{ Path = $_; Bytes = $(if ($sizes.Contains($_)) { [long] $sizes[$_] } else { 0 }) } })
}

# ------------------------------------------------------------------ init

function Invoke-QaInit {
    if (-not (Test-Path -LiteralPath $outFull -PathType Container)) { New-Item -ItemType Directory -Force -Path $outFull | Out-Null }
    $all = Get-QaRepoFiles
    $paths = @($all.Tracked) + @($all.Untracked)
    $created = ($null -eq $config)
    $declared = $null
    if (-not $created) { $declared = $config.DeclaredRules }
    $rr = Resolve-PaperQaRules -Declared $declared -Paths $paths
    if (@($rr.Errors).Count -gt 0) {
        foreach ($e in $rr.Errors) { Say "qa: $e" }
        exit 2
    }
    $buildConfig = $config
    if ($created) {
        $json = (New-PaperQaExternalProfile -RepoRoot $root -Rules @($rr.Rules)) | ConvertTo-Json -Depth 6
        # PowerShell 5.1 writes ' < > & as \u escapes; valid JSON either way, but the owner reads this file.
        $json = [regex]::Replace($json, '(?<!\\)((?:\\\\)*)\\u00(27|3[cCeE]|26)', { param($m) $m.Groups[1].Value + [string] [char] [Convert]::ToInt32($m.Groups[2].Value, 16) })
        Write-Text $profileFile ($json + "`n")
        $buildConfig = [pscustomobject]@{ BuildCommand = ''; NoDeploy = @() }
    }
    $b = Get-PaperQaExternalBuild -Config $buildConfig
    foreach ($line in (Format-PaperQaInitLines -ProfilePath $profileFile -RepoRoot $root -Out $outFull -Rules @($rr.Rules) -Created $created -Build $b -Command "$($buildConfig.BuildCommand)")) { Say $line }
    exit 0
}

# ------------------------------------------------------------------ plan, lanes

# The scope, chosen and checked; writes nothing. An external repository chooses it with no branch scope
# (F98), so no branch, base or uncommitted work is read.
function Select-QaScope {
    if ($external) {
        $sel = Select-PaperQaScope -Requested $Scope -Path $Path -Base $Base -External
        if ($sel.Error) { Stop-Qa 2 "qa: $($sel.Error)" }
        return $sel
    }
    $branch = "$((Invoke-PaperChangeGit -C $root rev-parse --abbrev-ref HEAD).Lines | Select-Object -First 1)".Trim()
    # The base and merge-base are the kit's one answer (change-set.ps1, as review-files reads them): -Base,
    # the base worktree create recorded, main/master/origin, or the branch's own remote when on the base.
    $change = Get-PaperChangeBase -RepoRoot $root -Base $Base -Tool 'qa'
    $ahead = 0
    if ($change.Code -eq 0 -and "$($change.MergeBase)" -ne 'HEAD') {
        $c = Invoke-PaperChangeGit -C $root rev-list --count "$($change.MergeBase)..HEAD"
        if ($c.Code -eq 0) { $ahead = [int] ("$($c.Lines[0])".Trim()) }
    }
    $baseBranch = "$($change.BaseRef)"
    $dirty = Get-PaperQaDirtyCount -StatusLines (Invoke-PaperChangeGit -C $root status --porcelain --untracked-files=all).Lines -ReportDir $config.Report -SarifDir $config.SarifDir

    $sel = Select-PaperQaScope -Requested $Scope -Path $Path -Base $Base -CurrentBranch $branch -BaseBranch $baseBranch -BaseError "$($change.Message)" -Ahead $ahead -Dirty $dirty
    if ($sel.Error) { Stop-Qa 2 "qa: $($sel.Error)" }
    return $sel
}

# Everything plan decides once the scope is chosen - files, lanes, estimate, batches - for plan and for
# lanes alike; writes nothing. Exit 2 or 5 as plan.
function Get-QaPlanParts($all, [string[]] $allPaths, $sel, [string[]] $OnlyLanes, [string[]] $SkipLanes, [bool] $StaticOnlyRun) {
    $ruleObjs = @()
    if ($external) { $ruleObjs = @(Get-QaExternalRules $allPaths) }

    if ($sel.Mode -eq 'branch') {
        $args2 = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'review-files', '-Repo', $root)
        if ($Base) { $args2 += @('-Base', $Base) }
        $rf = Invoke-Native $script:PowerShellExe $args2
        $parsed = ConvertFrom-PaperReviewFilesOutput -Lines $rf.Lines -ExitCode $rf.Code
        $scopeResult = Resolve-PaperQaBranchScope -ReviewFiles $parsed -Sizes (Get-QaSizes @($parsed.Files | ForEach-Object { $_.Path })) -Label $sel.Label
        if ($scopeResult.ExitCode -ne 0) { Stop-Qa $scopeResult.ExitCode $scopeResult.Line }
    }
    else {
        $bytes = Get-QaSizes $allPaths
        $binary = @{}
        foreach ($p in @($bytes.Keys)) { $binary[$p] = Test-PaperBinaryFile (Get-QaFullPath $p) }
        $submodules = @($allPaths | Where-Object { -not $bytes.ContainsKey($_) -and (Test-Path -LiteralPath (Get-QaFullPath $_) -PathType Container) })
        $missing = @($allPaths | Where-Object { -not $bytes.ContainsKey($_) -and $submodules -notcontains $_ })
        $scopeResult = Get-PaperQaScopeFiles -Tracked $all.Tracked -Untracked $all.Untracked -Bytes $bytes -Binary $binary -Missing $missing -Submodules $submodules -Folder $sel.Folder -Exclude $config.Exclude
        if ($scopeResult.ExitCode -eq 5) { Stop-Qa 5 (Format-PaperQaNoFileLine $sel.Mode $sel.Label) }
    }
    $files = @($scopeResult.Files); $excluded = @($scopeResult.Excluded)

    $screens = @(Get-PaperQaScreens -Paths $allPaths -Glob $config.UiScreens -Folder $sel.Folder)
    $laneFiles = @{}
    foreach ($l in $script:PaperQaLaneNames) { $laneFiles[$l] = @(Get-PaperQaLaneFiles -Files $files -Lane $l -Screens $screens) }
    $hasDotnet = Test-PaperQaDotnetProject -Paths $allPaths
    if ($external) {
        $b = Get-PaperQaExternalBuild -Config $config
        $lanes = Get-PaperQaLanes -Hosts $config.Hosts -Config $config -LaneFiles $laneFiles -HasDotnetProject $hasDotnet -HasBuildVerb $b.Run -NoBuildReason $b.Reason -RuleCount $ruleObjs.Count -Only $OnlyLanes -Skip $SkipLanes -StaticOnly:$StaticOnlyRun
    }
    else {
        $hasBuild = (Get-PaperVerbPlan -ProjectProfile $profileMap -Verb 'build').ExitCode -eq 0
        $lanes = Get-PaperQaLanes -Hosts $config.Hosts -Config $config -LaneFiles $laneFiles -HasDotnetProject $hasDotnet -HasBuildVerb $hasBuild -Only $OnlyLanes -Skip $SkipLanes -StaticOnly:$StaticOnlyRun
    }
    if ($lanes.Error) { Stop-Qa 2 "qa: $($lanes.Error)" }
    $estimate = Get-PaperQaEstimate -Lanes $lanes.Lanes -LaneFiles $laneFiles -Config $config -StaticOnly:$StaticOnlyRun -Rules $ruleObjs
    $batches = @(Get-PaperQaBatchList -Lanes $lanes.Lanes -LaneFiles $laneFiles -MaxTokens $config.BatchTokens -Rules $ruleObjs)
    return [pscustomobject]@{ Files = $files; Excluded = $excluded; Screens = $screens; LaneFiles = $laneFiles; Lanes = $lanes.Lanes; Estimate = $estimate; Batches = $batches; Rules = $ruleObjs }
}

function Invoke-QaPlan {
    $all = Get-QaRepoFiles
    $allPaths = @($all.Tracked) + @($all.Untracked)
    $sel = Select-QaScope
    $parts = Get-QaPlanParts $all $allPaths $sel $Only $Skip $StaticOnly.IsPresent
    $files = @($parts.Files); $excluded = @($parts.Excluded); $estimate = $parts.Estimate; $ruleObjs = @($parts.Rules)
    $approved = [bool] (Test-PaperQaApprovedAtPlan -Lanes $parts.Lanes -Yes $Yes.IsPresent)

    Initialize-QaRunsDir
    $existingRuns = @(Get-ChildItem -LiteralPath $runsDir -Directory | ForEach-Object { $_.Name })
    $runId = Get-PaperQaRunId -Stamp (Get-Date).ToString('yyyyMMdd-HHmmss') -Existing $existingRuns
    $dir = Join-Path $runsDir $runId
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $plan = [pscustomobject]@{
        Run = $runId; Mode = $sel.Mode; Label = $sel.Label; Reason = $sel.Reason; Folder = $sel.Folder; Base = $Base
        StaticOnly = $StaticOnly.IsPresent; Files = $files; Excluded = $excluded; Lanes = $parts.Lanes; Batches = @($parts.Batches)
        Screens = @($parts.Screens); Estimate = $estimate; Approved = $approved; Created = (Get-Date).ToString('s')
    }
    $repoLine = ''
    if ($external) {
        $plan | Add-Member -NotePropertyName External -NotePropertyValue $true
        $plan | Add-Member -NotePropertyName RepoRoot -NotePropertyValue $root
        $plan | Add-Member -NotePropertyName ProfilePath -NotePropertyValue $profileFile
        $plan | Add-Member -NotePropertyName Rules -NotePropertyValue $ruleObjs
        $repoLine = $root
    }
    Write-Json (Join-Path $dir 'plan.json') $plan
    foreach ($line in (Format-PaperQaEstimate -Run $runId -Mode $sel.Mode -Reason $sel.Reason -FileCount $files.Count -ExcludedCount $excluded.Count -Estimate $estimate -Approved $approved -RepoRoot $repoLine -RunDir $dir)) { Say $line }
    exit 0
}

# The lane question (ADR-0035, F99-F105): the lanes that apply and their cost, with no choice applied; with
# -Pick, the answer turned into the exact plan command line. Writes nothing, creates no run folder.
function Invoke-QaLanes {
    $all = Get-QaRepoFiles
    $allPaths = @($all.Tracked) + @($all.Untracked)
    $sel = Select-QaScope
    $parts = Get-QaPlanParts $all $allPaths $sel @() @() $false
    if (-not $pickGiven) {
        $repoLine = ''
        if ($external) { $repoLine = $root }
        $list = Format-PaperQaLaneList -Mode $sel.Mode -Reason $sel.Reason -FileCount @($parts.Files).Count -ExcludedCount @($parts.Excluded).Count -Estimate $parts.Estimate -RepoRoot $repoLine
        foreach ($line in @($list.Lines)) { Say $line }
        exit $list.ExitCode
    }
    $r = Resolve-PaperQaLanePick -Pick $Pick -Lanes $parts.Lanes
    if ($r.Error -eq 'no lane applies to this scope') { Stop-Qa 5 'qa: NOT APPLICABLE - no lane applies to this scope' }
    if ($r.Error) { Stop-Qa 2 "qa: $($r.Error)" }
    foreach ($line in @(Format-PaperQaPickLines -Pick $r -Mode $sel.Mode -Reason $sel.Reason -Arguments $planArgs)) { Say $line }
    exit 0
}

# ------------------------------------------------------------------ approve, files

function Invoke-QaApprove {
    $dir = Get-QaRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $plan.Approved = $true
    $plan | Add-Member -NotePropertyName ApprovedAt -NotePropertyValue (Get-Date).ToString('s') -Force
    Write-Json (Join-Path $dir 'plan.json') $plan
    Say "qa: run $Run approved - lanes may start"
    exit 0
}

function Read-QaStatic([string] $Dir) {
    $file = Join-Path $Dir 'static.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
    return Read-Json $file
}

function Invoke-QaFiles {
    $dir = Get-QaRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $answer = $null
    $answerFile = Join-Path $dir "lanes\$Lane-$Batch.md"
    if ($Retry -and (Test-Path -LiteralPath $answerFile -PathType Leaf)) { $answer = Read-Lines $answerFile }
    $static = Read-QaStatic $dir
    $groups = @()
    if ($null -ne $static) { $groups = @($static.Groups) }
    $r = Get-PaperQaFilesList -Plan $plan -Lane $Lane -Batch $Batch -Run $Run -Retry $Retry.IsPresent -RetryAnswer $answer -StaticGroups $groups
    foreach ($line in @($r.Lines)) { Say $line }
    exit $r.ExitCode
}

# ------------------------------------------------------------------ static

function Invoke-QaStatic {
    $dir = Get-QaRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $gate = Get-PaperQaStaticGate -Row @($plan.Lanes | Where-Object { $_.Name -eq 'static' })[0]
    if ($gate.ExitCode -ne 0) { Stop-Qa $gate.ExitCode $gate.Line }

    $staticJson = Join-Path $dir 'static.json'
    $fail = {
        param([string] $reason, [string[]] $tail)
        Write-Json $staticJson ([pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = ''; Build = ''; Findings = @(); Groups = @(); Counts = [ordered]@{} })
        Say "qa: static not verifiable - $reason"
        foreach ($t in @($tail)) { Say "  $t" }
        exit 4
    }

    if (-not (Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue)) { & $fail 'dotnet SDK not found' @() }
    $ver = Invoke-Native 'dotnet.exe' @('--version')
    $tfm = Get-PaperQaTargetFramework -VersionText "$(@($ver.Lines)[0])"
    if ($ver.Code -ne 0 -or -not $tfm) { & $fail 'dotnet SDK not found' @() }

    $all = Get-QaRepoFiles
    $styles = @{}
    foreach ($p in @(@($all.Tracked) + @($all.Untracked) | Where-Object { $_ -match '\.(csproj|vbproj)$' })) {
        $full = Get-QaFullPath $p
        if (Test-Path -LiteralPath $full -PathType Leaf) { $styles[$p] = Get-PaperQaProjectStyle -Text ([IO.File]::ReadAllText($full)) }
    }
    $analyzers = $config.Analyzers
    $pk = Get-PaperQaStaticPackages -Analyzers $analyzers -Styles $styles

    $targets = Join-Path $dir 'Paper.Qa.targets'
    $envs = Get-PaperQaBuildEnvironment -Targets $targets -Analyzers $analyzers
    $dllsById = [ordered]@{}
    if ($pk.Packages.Count -gt 0) {
        $restoreDir = Join-Path $dir 'restore'
        $restoreProj = Join-Path $restoreDir 'qa-restore.csproj'
        Write-Text $restoreProj ((New-PaperQaRestoreProject -Packages $pk.Packages -TargetFramework $tfm) + "`r`n")
        # The repository's own Directory.*.props/targets would be imported by a project under it; these empty
        # ones stop that search, so the restore reads only what this run wrote (NuGet.config still applies).
        foreach ($stop in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) { Write-Text (Join-Path $restoreDir $stop) "<Project />`r`n" }
        $rs = Invoke-QaWithEnvironment @{ 'MSBUILDDISABLENODEREUSE' = $envs['MSBUILDDISABLENODEREUSE'] } { Invoke-Native 'dotnet.exe' @('restore', $restoreProj, '--nologo') }
        Write-Text (Join-Path $dir 'restore.log') ((@($rs.Lines) -join "`r`n") + "`r`n")
        if ($rs.Code -ne 0) { & $fail (Get-PaperQaRestoreFailure -Lines $rs.Lines -Packages $pk.Packages -ExitCode $rs.Code) @() }
        $locals = Invoke-Native 'dotnet.exe' @('nuget', 'locals', 'global-packages', '--list')
        $cache = ConvertFrom-PaperNugetLocals -Lines $locals.Lines
        if (-not $cache) { & $fail 'the NuGet global-packages folder is unknown (dotnet nuget locals global-packages --list)' @() }
        foreach ($id in @($pk.Packages.Keys)) {
            $pkgDir = Join-Path $cache ("$($id.ToLowerInvariant())\$(ConvertTo-PaperQaNugetVersion $pk.Packages[$id])")
            $rel = @()
            if (Test-Path -LiteralPath $pkgDir -PathType Container) {
                $rel = @(Get-ChildItem -LiteralPath $pkgDir -Recurse -File | ForEach-Object { $_.FullName.Substring($pkgDir.TrimEnd('\').Length + 1).Replace('\', '/') })
            }
            $dlls = @(Get-PaperQaAnalyzerDlls -Files $rel)
            if ($dlls.Count -eq 0) { & $fail "$id $($pk.Packages[$id]) has no analyzer assembly" @() }
            $dllsById[$id] = @($dlls | ForEach-Object { Join-Path $pkgDir ($_.Replace('/', '\')) })
        }
    }

    $globalConfig = Join-Path $dataDir 'qa.globalconfig'
    if ($config.GlobalConfig) {
        $globalConfig = Get-QaFullPath $config.GlobalConfig
        if (-not (Test-Path -LiteralPath $globalConfig -PathType Leaf)) { Stop-Qa 2 "qa: qa.staticAnalysis.globalconfig: $($config.GlobalConfig) does not exist" }
    }
    $plug = Get-PaperQaAnalyzerPlug -DllsById $dllsById
    $expected = Get-PaperQaExpectedDlls -Analyzers $analyzers -HasSdk $pk.HasSdk -DllsById $dllsById

    $sarifDir = Join-Path $dir 'sarif'
    New-Item -ItemType Directory -Force -Path $sarifDir | Out-Null
    # Written right before the build: its time stamp, newer than every output, makes CoreCompile run again.
    Write-Text $targets ((New-PaperQaTargets -Run $Run -SarifDir $sarifDir -AnalyzerListDir $dir -GlobalConfig $globalConfig -Analyzers $plug.Analyzers -LegacyOnly $plug.LegacyOnly) + "`r`n")

    if ($external) {
        # E: the owner's build line, run directly from the repository root (no run lock, no profile of the
        # repository); git's view of the repository before and after it (F94).
        $eb = Get-PaperQaExternalBuild -Config $config
        if (-not $eb.Run) { & $fail $eb.Reason @() }
        . (Join-Path $paperflow 'project-command.ps1')
        $snapBefore = Get-QaRepoSnapshot
        if ($null -eq $snapBefore) { & $fail 'git status failed before the build' @() }
        $res = Invoke-QaWithEnvironment $envs { Invoke-PaperProjectCommand -Directory $root -Command $config.BuildCommand }
        $buildCode = if ($null -eq $res.Code) { 1 } else { [int] $res.Code }
        $buildLines = @("qa: build -> $($config.BuildCommand)") + @($res.Lines)
        $snapAfter = Get-QaRepoSnapshot
        Write-Text (Join-Path $dir 'build.log') ((@($buildLines) -join "`r`n") + "`r`n")
        if ($null -eq $snapAfter) { & $fail 'git status failed after the build' @() }
        $changed = @(Compare-PaperQaRepoSnapshot -Before $snapBefore -After $snapAfter)
        if ($changed.Count -gt 0) { & $fail (Format-PaperQaRepoChange -Paths $changed) @() }
    }
    else {
        $build = Invoke-QaWithEnvironment $envs { Invoke-Native $script:PowerShellExe @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'build', '-Repo', $root, '-Full') }
        $buildCode = $build.Code
        $buildLines = @($build.Lines)
        Write-Text (Join-Path $dir 'build.log') ((@($buildLines) -join "`r`n") + "`r`n")
    }
    $sarifFiles = @(Get-ChildItem -LiteralPath $sarifDir -Filter '*.sarif' -File -ErrorAction SilentlyContinue | Sort-Object Name)
    $bf = Get-PaperQaBuildFailure -ExitCode $buildCode -Lines $buildLines -SarifCount $sarifFiles.Count -External:$external
    if ($null -ne $bf) { & $fail $bf.Reason $bf.Tail }
    $results = @(); $problems = @()
    foreach ($f in $sarifFiles) {
        try { $obj = Read-Json $f.FullName }
        catch { $problems += "$($f.Name): not JSON - $($_.Exception.Message)"; continue }
        $c = ConvertFrom-PaperQaSarif -Sarif $obj -File $f.Name
        $results += @($c.Results); $problems += @($c.Problems)
    }
    $listed = @()
    foreach ($t in @(Get-ChildItem -LiteralPath $dir -Filter 'analyzers-*.txt' -File -ErrorAction SilentlyContinue)) { foreach ($line in (Read-Lines $t.FullName)) { $listed += $line } }
    $sonarFile = Join-Path $dataDir 'sonar-cs-rules.tsv'
    $sonar = @{}
    if (Test-Path -LiteralPath $sonarFile -PathType Leaf) { $sonar = Read-PaperQaSonarTable -Lines (Read-Lines $sonarFile) }
    $grouped = Group-PaperQaStatic -Results $results -RepoRoot $root -ScopePaths @($plan.Files | ForEach-Object { $_.Path }) -ScopeMode $plan.Mode -ProfileKinds $config.Kinds -SonarTable $sonar
    $proof = @(Get-PaperQaAnalyzerProof -Listed $listed -Expected $expected -LoadProblems $grouped.LoadProblems)
    $outcome = Get-PaperQaStaticOutcome -Grouped $grouped -Proof $proof -AnalyzerText (Format-PaperQaAnalyzerText -Analyzers $analyzers -HasLegacy $pk.HasLegacy) `
        -BuildText $(if ($external) { "build.command of qa.profile.json (exit $buildCode), $($sarifFiles.Count) SARIF file(s)" } else { "paperflow.ps1 build -Full (exit $buildCode), $($sarifFiles.Count) SARIF file(s)" }) -Problems $problems -SarifDir $config.SarifDir -SarifCount $sarifFiles.Count
    Write-Json $staticJson $outcome.Json
    if ($outcome.CopySarifTo) {
        $keep = Get-QaFullPath $outcome.CopySarifTo
        New-Item -ItemType Directory -Force -Path $keep | Out-Null
        foreach ($f in $sarifFiles) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $keep $f.Name) -Force }
    }
    foreach ($line in @($outcome.Lines)) { Say $line }
    exit $outcome.ExitCode
}

# ------------------------------------------------------------------ check, report

# Every saved answer of the run: "<lane>-<b>" and "<lane>-<b>.2" -> its lines.
function Read-QaAnswers([string] $Dir) {
    $answers = @{}
    foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Dir 'lanes') -Filter '*.md' -File -ErrorAction SilentlyContinue)) {
        $answers[[IO.Path]::GetFileNameWithoutExtension($f.Name)] = Read-Lines $f.FullName
    }
    return $answers
}

function Invoke-QaCheck {
    $dir = Get-QaRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $c = Get-PaperQaCheck -Plan $plan -Answers (Read-QaAnswers $dir) -Static (Read-QaStatic $dir) -VerifyCap $config.VerifyCap
    Write-Json (Join-Path $dir 'findings.json') ([pscustomobject]@{ Findings = $c.Queue; LaneStates = $c.LaneStates; Seen = $c.Seen })
    if ($external) { Say "qa: repository $root (external, read only) - verify readers read the code there" }
    foreach ($line in @($c.Lines)) { Say $line }
    exit $c.ExitCode
}

function Invoke-QaReport {
    $dir = Get-QaRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $static = Read-QaStatic $dir
    $c = Get-PaperQaCheck -Plan $plan -Answers (Read-QaAnswers $dir) -Static $static -VerifyCap $config.VerifyCap
    $verdictLines = @()
    foreach ($v in @(Get-ChildItem -LiteralPath (Join-Path $dir 'verdicts') -Filter '*.md' -File -ErrorAction SilentlyContinue | Sort-Object Name)) { $verdictLines += , ([string[]] (Read-Lines $v.FullName)) }
    if ($external) { $reportDir = $reportsDir; $reportArg = $reportsDir }
    else { $reportDir = Get-QaFullPath $config.Report; $reportArg = $config.Report }
    $existing = @()
    if (Test-Path -LiteralPath $reportDir -PathType Container) { $existing = @(Get-ChildItem -LiteralPath $reportDir -File | ForEach-Object { $_.Name }) }
    $rep = New-PaperQaReport -Plan $plan -Check $c -VerdictLines $verdictLines -Static $static -Run $Run -Date (Get-Date).ToString('yyyy-MM-dd') -ReportDir $reportArg -Existing $existing
    Write-Text (Join-Path $reportDir $rep.Name) $rep.Text
    foreach ($line in @($rep.Lines)) { Say $line }
    exit 0
}

switch ($Command) {
    'init' { Invoke-QaInit }
    'lanes' { Invoke-QaLanes }
    'plan' { Invoke-QaPlan }
    'approve' { Invoke-QaApprove }
    'static' { Invoke-QaStatic }
    'files' { Invoke-QaFiles }
    'check' { Invoke-QaCheck }
    'report' { Invoke-QaReport }
}
