# /qa, the I/O part (plan 2026-10-02-qa-command, table K): git, the disk, `dotnet restore`, the build verb.
# Every decision lives in qa-plan.ps1 and qa-sarif-plan.ps1, which are pure and fully tested: each command
# reads what it needs, hands it to one of their functions, prints its lines and exits with its code.
#
#   qa.ps1 plan    [-Scope project|branch|path] [-Path <folder>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes]
#   qa.ps1 approve -Run <run>
#   qa.ps1 static  -Run <run>
#   qa.ps1 files   -Run <run> -Lane <lane> [-Batch <n>] [-Retry]
#   qa.ps1 check   -Run <run>
#   qa.ps1 report  -Run <run>
#   (every command takes -Repo <project root>; default: the current folder)
#
# A run lives in <root>\.paper\qa\<run>\ (plan.json, lanes\, verdicts\, sarif\, static.json, findings.json);
# <root>\.paper\qa\.gitignore holds one line "*", so git sees nothing there. The one other file a run
# writes is its report under the profile's qa.report folder.
#
# Exit codes:
#   0  done
#   1  check found answers to send back
#   2  invalid request, broken profile, unknown run (F66, F67); "not verifiable:" when the branch scope has
#      no base (F69)
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
    [switch] $Retry
)

$ErrorActionPreference = 'Stop'
# git prints paths as UTF-8 bytes; read them, and write ours, as UTF-8 whatever the console codepage.
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

. (Join-Path $PSScriptRoot 'qa-sarif-plan.ps1')
. (Join-Path $PSScriptRoot 'qa-plan.ps1')

$script:Utf8 = New-Object System.Text.UTF8Encoding $false
$script:PowerShellExe = Join-Path $PSHOME 'powershell.exe'

function Say([string] $Line) { [Console]::Out.WriteLine($Line) }
function Stop-Qa([int] $Code, [string] $Line) { if ($Line) { Say $Line }; exit $Code }

function Show-Help {
    Say 'qa - on-demand QA sweep of a project, a branch or a folder'
    Say '  qa.ps1 plan [-Scope project|branch|path] [-Path <folder>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes]'
    Say '  qa.ps1 approve|static|check|report -Run <run>'
    Say '  qa.ps1 files -Run <run> -Lane <lane> [-Batch <n>] [-Retry]'
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
if ($Command -notin @('plan', 'approve', 'static', 'files', 'check', 'report')) { Stop-Qa 2 "qa: unknown command '$Command' (plan, approve, static, files, check, report)" }
if (-not (Test-Path -LiteralPath $Repo -PathType Container)) { Stop-Qa 2 "qa: project folder not found: $Repo" }
$top = Invoke-Native 'git.exe' @('-C', $Repo, 'rev-parse', '--show-toplevel')
if ($top.Code -ne 0 -or -not $top.Lines) { Stop-Qa 2 "qa: not a git repository: $Repo" }
$root = ([string] $top.Lines[0]).Trim().Replace('/', '\').TrimEnd('\')

# paperflow: beside the skill in a Claude project and in the kit source; the project's own for Codex.
$paperflow = $null
foreach ($candidate in @((Join-Path $PSScriptRoot '..\..\..\paperflow'), (Join-Path $root '.claude\paperflow'))) {
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

$read = Read-PaperProfileFile $root
if ($read.Error) { Stop-Qa 2 "qa: .claude/paper.profile.json is not valid JSON - $($read.Error)" }
$profileMap = $read.Map
$config = ConvertFrom-PaperQaProfile -Profile $profileMap
if (@($config.Errors).Count -gt 0) {
    foreach ($e in $config.Errors) { Say "qa: $e" }
    exit 2
}

$runsDir = Join-Path $root '.paper\qa'
$dataDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\data')).ProviderPath

function Initialize-QaRunsDir {
    if (-not (Test-Path -LiteralPath $runsDir -PathType Container)) { New-Item -ItemType Directory -Force -Path $runsDir | Out-Null }
    $ignore = Join-Path $runsDir '.gitignore'
    if (-not (Test-Path -LiteralPath $ignore -PathType Leaf)) { Write-Text $ignore "*`n" }
}

function Get-QaRunDir {
    if (-not $Run) { Stop-Qa 2 "qa: $Command needs -Run <run>" }
    $dir = Join-Path $runsDir $Run
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'plan.json') -PathType Leaf)) { Stop-Qa 2 "qa: no run $Run (looked in .paper/qa)" }
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

# ------------------------------------------------------------------ plan

function Invoke-QaPlan {
    $all = Get-QaRepoFiles
    $allPaths = @($all.Tracked) + @($all.Untracked)
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

    $hasBuild = (Get-PaperVerbPlan -ProjectProfile $profileMap -Verb 'build').ExitCode -eq 0
    $screens = @(Get-PaperQaScreens -Paths $allPaths -Glob $config.UiScreens -Folder $sel.Folder)
    $laneFiles = @{}
    foreach ($l in $script:PaperQaLaneNames) { $laneFiles[$l] = @(Get-PaperQaLaneFiles -Files $files -Lane $l -Screens $screens) }
    $lanes = Get-PaperQaLanes -Hosts $config.Hosts -Config $config -LaneFiles $laneFiles -HasDotnetProject (Test-PaperQaDotnetProject -Paths $allPaths) -HasBuildVerb $hasBuild -Only $Only -Skip $Skip -StaticOnly:$StaticOnly
    if ($lanes.Error) { Stop-Qa 2 "qa: $($lanes.Error)" }
    $estimate = Get-PaperQaEstimate -Lanes $lanes.Lanes -LaneFiles $laneFiles -Config $config -StaticOnly:$StaticOnly
    $batches = @(Get-PaperQaBatchList -Lanes $lanes.Lanes -LaneFiles $laneFiles -MaxTokens $config.BatchTokens)
    $approved = [bool] (Test-PaperQaApprovedAtPlan -Lanes $lanes.Lanes -Yes $Yes.IsPresent)

    Initialize-QaRunsDir
    $existingRuns = @(Get-ChildItem -LiteralPath $runsDir -Directory | ForEach-Object { $_.Name })
    $runId = Get-PaperQaRunId -Stamp (Get-Date).ToString('yyyyMMdd-HHmmss') -Existing $existingRuns
    $dir = Join-Path $runsDir $runId
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $plan = [pscustomobject]@{
        Run = $runId; Mode = $sel.Mode; Label = $sel.Label; Reason = $sel.Reason; Folder = $sel.Folder; Base = $Base
        StaticOnly = $StaticOnly.IsPresent; Files = $files; Excluded = $excluded; Lanes = $lanes.Lanes; Batches = $batches
        Screens = $screens; Estimate = $estimate; Approved = $approved; Created = (Get-Date).ToString('s')
    }
    Write-Json (Join-Path $dir 'plan.json') $plan
    foreach ($line in (Format-PaperQaEstimate -Run $runId -Mode $sel.Mode -Reason $sel.Reason -FileCount $files.Count -ExcludedCount $excluded.Count -Estimate $estimate -Approved $approved)) { Say $line }
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

    $build = Invoke-QaWithEnvironment $envs { Invoke-Native $script:PowerShellExe @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'build', '-Repo', $root, '-Full') }
    Write-Text (Join-Path $dir 'build.log') ((@($build.Lines) -join "`r`n") + "`r`n")
    $sarifFiles = @(Get-ChildItem -LiteralPath $sarifDir -Filter '*.sarif' -File -ErrorAction SilentlyContinue | Sort-Object Name)
    $bf = Get-PaperQaBuildFailure -ExitCode $build.Code -Lines $build.Lines -SarifCount $sarifFiles.Count
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
        -BuildText "paperflow.ps1 build -Full (exit $($build.Code)), $($sarifFiles.Count) SARIF file(s)" -Problems $problems -SarifDir $config.SarifDir -SarifCount $sarifFiles.Count
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
    $reportDir = Get-QaFullPath $config.Report
    $existing = @()
    if (Test-Path -LiteralPath $reportDir -PathType Container) { $existing = @(Get-ChildItem -LiteralPath $reportDir -File | ForEach-Object { $_.Name }) }
    $rep = New-PaperQaReport -Plan $plan -Check $c -VerdictLines $verdictLines -Static $static -Run $Run -Date (Get-Date).ToString('yyyy-MM-dd') -ReportDir $config.Report -Existing $existing
    Write-Text (Join-Path $reportDir $rep.Name) $rep.Text
    foreach ($line in @($rep.Lines)) { Say $line }
    exit 0
}

switch ($Command) {
    'plan' { Invoke-QaPlan }
    'approve' { Invoke-QaApprove }
    'static' { Invoke-QaStatic }
    'files' { Invoke-QaFiles }
    'check' { Invoke-QaCheck }
    'report' { Invoke-QaReport }
}
