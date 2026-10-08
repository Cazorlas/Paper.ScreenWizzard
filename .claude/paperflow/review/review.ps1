# The review engine of /review-architecture, /review-bugs, /review-security and /review-ui, the I/O part (plan 2026-10-02-qa-command, table K): git, the disk, `dotnet restore`, the build verb.
# Every decision lives in review-plan.ps1, review-sarif-plan.ps1, review-external-plan.ps1 and review-lanes-plan.ps1, which are pure and fully
# tested: each command reads what it needs, hands it to one of their functions, prints its lines and exits
# with its code.
#
#   review.ps1 plan    -Kind architecture|bugs|security|ui [-Scope project|branch|path|files] [-Path <folder>] [-Files <a>,<b>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes] [-Executor auto|collab|subagents]
#   review.ps1 lanes   -Kind architecture|bugs|security|ui [-Scope project|branch|path|files] [-Path <folder>] [-Files <a>,<b>] [-Base <branch>] [-Pick <numbers|lanes|all>] [-Executor auto|collab|subagents]
#   review.ps1 approve -Run <run>
#   review.ps1 static  -Run <run>
#   review.ps1 files   -Run <run> -Lane <lane> [-Batch <n>] [-Retry]
#   review.ps1 check   -Run <run>
#   review.ps1 prompt  -Run <run> -Lane <lane> [-Batch <n>] [-Retry]      (the prompt of one lane batch, written to the run folder)
#   review.ps1 prompt  -Run <run> -Verify <id>                            (the prompt of the reader of one finding)
#   review.ps1 report  -Run <run>
#   review.ps1 init    -Repo <repository> -Out <folder>
#   (every command takes -Repo <project root>; default: the current folder. -Kind is the command that asked - /review-architecture,
#    /review-bugs, /review-security or /review-ui - and is needed by plan and lanes; the commands after plan read it from plan.json
#    and refuse a -Kind that does not match.)
#
# A run lives in <root>\.paper\review\<run>\ (plan.json, lanes\, verdicts\, sarif\, static.json, findings.json);
# <root>\.paper\review\.gitignore holds one line "*", so git sees nothing there. The one other file a run
# writes is its report under the profile's qa.report folder.
#
# An external repository (-Out <folder> with -Repo, ADR-0034): nothing is written into the repository. The
# review profile is <Out>\review.profile.json (review.ps1 init writes its skeleton; the old qa.profile.json is read when it is alone), runs live in <Out>\runs\<run>\ and
# reports in <Out>\reports\. No script of the repository runs (paperflow is the kit's own), git runs with
# GIT_OPTIONAL_LOCKS=0 so it never refreshes the repository's index, and the static lane runs the owner's
# declared build line only when it carries every declared no-deploy property; a build that changes a file
# git does not ignore leaves the static lane not verifiable (review-external-plan.ps1). A branch scope (ADR-0037)
# lists its files with the kit's own read-only git (Invoke-ReviewGit: core.fsmonitor off, no lazy fetch), never
# review-files, and never fetches.
#
# -Executor (ADR-0043): who answers the agent lanes - Codex through a read-only collab session when collab and codex are here, else
# Claude subagents; the choice is in plan.json and in the estimate, and PAPER_REVIEW_COLLAB_DIR replaces the collab folder.
#
# Exit codes:
#   0  done
#   1  check found answers to send back
#   2  invalid request, broken profile, unknown run (F66, F67); "not verifiable:" when the branch scope has
#      no base (F69); a wrong review.profile.json or -Out (external repository, F91-F95, F98); a branch scope
#      with no base or no merge-base (F121, F122)
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
    [string[]] $Pick,
    [string] $Executor = 'auto',
    [string] $Verify,
    [string] $Session = 'claude',
    [string] $Kind,
    [string[]] $Files,
    [switch] $AllFiles
)

$ErrorActionPreference = 'Stop'
# git prints paths as UTF-8 bytes; read them, and write ours, as UTF-8 whatever the console codepage.
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

. (Join-Path $PSScriptRoot 'review-sarif-plan.ps1')
. (Join-Path $PSScriptRoot 'review-plan.ps1')
. (Join-Path $PSScriptRoot 'review-external-plan.ps1')
. (Join-Path $PSScriptRoot 'review-branch-plan.ps1')
. (Join-Path $PSScriptRoot 'review-lanes-plan.ps1')
. (Join-Path $PSScriptRoot 'review-collab-plan.ps1')
. (Join-Path $PSScriptRoot 'sonarqube-plan.ps1')
. (Join-Path $PSScriptRoot 'sonarqube.ps1')

$script:Utf8 = New-Object System.Text.UTF8Encoding $false
$script:PowerShellExe = Join-Path $PSHOME 'powershell.exe'

function Say([string] $Line) { [Console]::Out.WriteLine($Line) }
function Stop-Review([int] $Code, [string] $Line) { if ($Line) { Say $Line }; exit $Code }

function Show-Help {
    Say 'review - on-demand review of a project, a branch, a folder or a few files'
    Say '  review.ps1 plan -Kind architecture|bugs|security|ui [-Scope project|branch|path|files] [-Path <folder>] [-Files <a>,<b>] [-Base <branch>] [-Only a,b] [-Skip a,b] [-StaticOnly] [-Yes]'
    Say '  review.ps1 lanes -Kind <kind> [-Scope ...] [-Path <folder>] [-Files <a>,<b>] [-Base <branch>] [-Pick 1,3|all|<lanes>]   (the lanes that apply and their cost; writes nothing)'
    Say '  review.ps1 approve|static|check|report -Run <run>'
    Say '  review.ps1 files -Run <run> -Lane <lane> [-Batch <n>] [-Retry]'
    Say '  review.ps1 prompt -Run <run> -Lane <lane> [-Batch <n>] [-Retry]   |   review.ps1 prompt -Run <run> -Verify <id>   (writes the prompt, prints where)'
    Say '  plan and lanes take -Executor auto|collab|subagents (who answers the agent lanes)'
    Say '  review.ps1 init -Repo <repository> -Out <folder>   (an external repository: nothing is written into it)'
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
if ($Command -notin @('init', 'lanes', 'plan', 'approve', 'static', 'files', 'prompt', 'check', 'report', 'sonar')) { Stop-Review 2 "review: unknown command '$Command' (init, lanes, plan, approve, static, files, prompt, check, report, sonar)" }
# The lane question (ADR-0035): -Pick belongs to lanes, and lanes lists every lane with no choice applied.
$pickGiven = $PSBoundParameters.ContainsKey('Pick')
if ($pickGiven -and $Command -ne 'lanes') { Stop-Review 2 'review: -Pick goes with review.ps1 lanes' }
if (@('claude', 'codex') -cnotcontains "$Session") { Stop-Review 2 'review: -Session must be claude or codex' }
if ($Command -eq 'lanes' -and (@('Only', 'Skip', 'StaticOnly', 'Yes') | Where-Object { $PSBoundParameters.ContainsKey($_) })) { Stop-Review 2 'review: lanes lists every lane that applies - -Only, -Skip, -StaticOnly and -Yes go with plan' }
# The scope parameters as typed, in plan's order, for the plan line of lanes -Pick (F101).
$planArgs = [ordered]@{}
foreach ($k in @('Kind', 'Scope', 'Path', 'Files', 'Base', 'Repo', 'Out', 'Executor')) {
    if ($PSBoundParameters.ContainsKey($k)) { $planArgs[$k] = (@($PSBoundParameters[$k]) -join ',') }
}
# F126: lanes with no scope typed may ask the scope first on an external repository.
$scopeGiven = [bool] (@('Scope', 'Path', 'Base', 'Files') | Where-Object { $PSBoundParameters.ContainsKey($_) })
# ADR-0044, F230: plan and lanes are asked by one of the four commands; the others read the kind from the run.
$reviewKind = ''
if ($Command -in @('plan', 'lanes')) {
    $ki = Get-PaperReviewKindLanes -Kind $Kind
    if ($ki.Error) { Stop-Review 2 "review: $($ki.Error)" }
    $reviewKind = $ki.Kind
}
if ($Command -eq 'sonar') {
    $ki = Get-PaperReviewKindLanes -Kind $Kind
    if ($ki.Kind -ne 'architecture') { Stop-Review 2 'review: sonar goes with -Kind architecture - the SonarQube layer feeds /review-architecture only' }
    $reviewKind = $ki.Kind
}
$external = [bool] $Out
# powershell -File hands -Files a,b over as one string; a comma splits it (a path with a comma is not a path of this command).
$Files = @(@($Files) | ForEach-Object { "$_" -split ',' } | Where-Object { "$_".Trim() })
if ($Command -eq 'init' -and -not $external) { Stop-Review 2 'review: init needs -Out <folder outside the repository>' }
if ($external) {
    # A2: before the first git command, so no git of this run, nor of a process it starts, takes the
    # repository's index lock to refresh it.
    if (-not $PSBoundParameters.ContainsKey('Repo')) { Stop-Review 2 'review: -Out needs -Repo <repository> - the repository to read' }
    [Environment]::SetEnvironmentVariable('GIT_OPTIONAL_LOCKS', '0')
    # ADR-0037: a partial clone never fetches a missing object for this run's git.
    [Environment]::SetEnvironmentVariable('GIT_NO_LAZY_FETCH', '1')
}
if (-not (Test-Path -LiteralPath $Repo -PathType Container)) { Stop-Review 2 "review: project folder not found: $Repo" }
$top = Invoke-Native 'git.exe' @('-C', $Repo, 'rev-parse', '--show-toplevel')
if ($top.Code -ne 0 -or -not $top.Lines) { Stop-Review 2 "review: not a git repository: $Repo" }
$root = ([string] $top.Lines[0]).Trim().Replace('/', '\').TrimEnd('\')

if ($external) {
    # A4 (F91): -Out outside the repository, by its text against the git root and -Repo as typed, and by git
    # itself from the nearest folder of -Out that exists (an 8.3 path and its long form are one folder).
    $outFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out).TrimEnd('\')
    $repoGiven = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Repo).TrimEnd('\')
    if (Test-Path -LiteralPath $outFull -PathType Leaf) { Stop-Review 2 "review: -Out is a file, not a folder: $outFull" }
    # Spec bo sung 2026-10-03 (find-bug T8 FB4): the repository's git folder too - inside it --show-toplevel
    # fails, so a junction or an 8.3 name into .git is caught by --absolute-git-dir.
    $norm = { param($r) if ($r.Code -eq 0 -and $r.Lines) { ([string] $r.Lines[0]).Trim().Replace('/', '\').TrimEnd('\') } else { '' } }
    $gitDir = & $norm (Invoke-Native 'git.exe' @('-C', $root, 'rev-parse', '--absolute-git-dir'))
    $inside = -not (Test-PaperReviewOutsideRepo -Out $outFull -RepoRoots @(@($root, $repoGiven, $gitDir) | Where-Object { $_ }))
    if (-not $inside) {
        $probe = $outFull
        while ($probe -and -not (Test-Path -LiteralPath $probe -PathType Container)) { $probe = Split-Path -Parent $probe }
        if ($probe) {
            if ((& $norm (Invoke-Native 'git.exe' @('-C', $probe, 'rev-parse', '--show-toplevel'))) -ieq $root) { $inside = $true }
            elseif ($gitDir -and (& $norm (Invoke-Native 'git.exe' @('-C', $probe, 'rev-parse', '--absolute-git-dir'))) -ieq $gitDir) { $inside = $true }
        }
    }
    if ($inside) { Stop-Review 2 "review: -Out must be a folder outside the repository: $outFull is inside $root" }
}

# paperflow (F97, D7): the folder above the engine - the kit's own paperflow, vendored or in the plugin - in every mode;
# the repository never supplies one.
$paperflow = $null
foreach ($candidate in @(Get-PaperReviewPaperflowCandidates -ScriptRoot $PSScriptRoot -RepoRoot $root -External $external)) {
    if (Test-Path -LiteralPath (Join-Path $candidate 'paperflow.ps1') -PathType Leaf) { $paperflow = (Resolve-Path -LiteralPath $candidate).ProviderPath; break }
}
if (-not $paperflow) { Stop-Review 2 'review: paperflow not found (.claude/paperflow/paperflow.ps1) - run paper-kit setup' }
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
    # A6: the review profile of the repository lives under -Out; the repository's own profile is never read.
    # review.profile.json; the old qa.profile.json is read when it is the only one there (F238).
    $profileFile = Join-Path $outFull 'review.profile.json'
    $usingOldProfile = $false
    $oldProfileFile = Join-Path $outFull 'qa.profile.json'
    if (-not (Test-Path -LiteralPath $profileFile -PathType Leaf) -and (Test-Path -LiteralPath $oldProfileFile -PathType Leaf)) { $profileFile = $oldProfileFile; $usingOldProfile = $true }
    if (Test-Path -LiteralPath $profileFile -PathType Leaf) {
        try { $json = [IO.File]::ReadAllText($profileFile, [Text.Encoding]::UTF8) | ConvertFrom-Json }
        catch { Stop-Review 2 "review: $profileFile is not valid JSON - $($_.Exception.Message)" }
        $config = ConvertFrom-PaperReviewExternalProfile -Profile (ConvertTo-PaperMap $json -SkipDocKeys) -RepoRoot $root
        if (@($config.Errors).Count -gt 0) {
            foreach ($e in $config.Errors) { Say "review: $e" }
            exit 2
        }
        if ($usingOldProfile) { $config.Notes = @(@($config.Notes) + 'review: reading qa.profile.json - rename it to review.profile.json') }
    }
    elseif ($Command -ne 'init') { Stop-Review 2 "review: no review.profile.json in $outFull - run review.ps1 init -Repo $root -Out $outFull first" }
    $runsDir = Join-Path $outFull 'runs'
    $reportsDir = Join-Path $outFull 'reports'
    if ($null -ne $config) { $config.Report = $reportsDir }
}
else {
    $read = Read-PaperProfileFile $root
    if ($read.Error) { Stop-Review 2 "review: .claude/paper.profile.json is not valid JSON - $($read.Error)" }
    $profileMap = $read.Map
    $config = ConvertFrom-PaperReviewProfile -Profile $profileMap
    if (@($config.Errors).Count -gt 0) {
        foreach ($e in $config.Errors) { Say "review: $e" }
        exit 2
    }
    $runsDir = Join-Path $root '.paper\review'
}
$dataDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'data')).ProviderPath

function Initialize-ReviewRunsDir {
    if (-not (Test-Path -LiteralPath $runsDir -PathType Container)) { New-Item -ItemType Directory -Force -Path $runsDir | Out-Null }
    $ignore = Join-Path $runsDir '.gitignore'
    if (-not (Test-Path -LiteralPath $ignore -PathType Leaf)) { Write-Text $ignore "*`n" }
}

function Get-ReviewRunDir {
    if (-not $Run) { Stop-Review 2 "review: $Command needs -Run <run>" }
    $dir = Join-Path $runsDir $Run
    if (-not (Test-Path -LiteralPath (Join-Path $dir 'plan.json') -PathType Leaf)) {
        if ($external) { Stop-Review 2 "review: no run $Run (looked in $runsDir)" }
        Stop-Review 2 "review: no run $Run (looked in .paper/review)"
    }
    if ($Kind) {
        $runKind = ''
        try { $runKind = "$((Read-Json (Join-Path $dir 'plan.json')).Kind)" } catch { $runKind = '' }
        if ($runKind -and $runKind -ne "$Kind".Trim().ToLowerInvariant()) { Stop-Review 2 "review: run $Run is a $runKind review - -Kind $Kind does not match" }
    }
    return $dir
}

# The kit's git runner (change-set.ps1): stdout only, so a git warning never reads as a path. On an external
# repository with core.fsmonitor off (ADR-0037): no hook and no daemon of the repository runs.
function Invoke-ReviewGit {
    if ($external) { return Invoke-PaperChangeGit -c core.fsmonitor=false @args }
    return Invoke-PaperChangeGit @args
}

# Every file of the repository git knows: tracked plus untracked-not-ignored, '/' separated.
function Get-ReviewRepoFiles {
    $t = Invoke-ReviewGit -C $root ls-files -z
    $u = Invoke-ReviewGit -C $root ls-files -z --others --exclude-standard
    if ($t.Code -ne 0 -or $u.Code -ne 0) { Stop-Review 2 "review: git ls-files failed: $(@($t.Lines + $u.Lines) -join ' / ')" }
    $split = { param($lines) @((@($lines) -join "`n") -split "`0" | ForEach-Object { $_.Trim("`n") } | Where-Object { $_ }) }
    return [pscustomobject]@{ Tracked = @(& $split $t.Lines); Untracked = @(& $split $u.Lines) }
}

# Runs a block with environment variables set for the processes it starts, and puts them back after.
function Invoke-ReviewWithEnvironment($Variables, [scriptblock] $Block) {
    $saved = @{}
    foreach ($k in @($Variables.Keys)) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $Variables[$k]) }
    try { return (& $Block) }
    finally { foreach ($k in @($Variables.Keys)) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) } }
}

function Get-ReviewFullPath([string] $Rel) { return (Join-Path $root ($Rel.Replace('/', '\'))) }

# Sizes of the files that are on disk: path -> bytes (a missing key = not on disk).
function Get-ReviewSizes([string[]] $Paths) {
    $sizes = @{}
    foreach ($p in @($Paths)) {
        $full = Get-ReviewFullPath $p
        if (Test-Path -LiteralPath $full -PathType Leaf) { $sizes[$p] = (Get-Item -LiteralPath $full).Length }
    }
    return $sizes
}

# The repository as git sees it, for F94: every path `git status` lists -> "XY|length|ticks" ("XY|missing").
function Get-ReviewRepoSnapshot {
    $st = Invoke-ReviewGit -C $root -c core.quotepath=false status --porcelain --untracked-files=all
    if ($st.Code -ne 0) { return $null }
    $snap = @{}
    foreach ($line in @($st.Lines)) {
        $p = @(Get-PaperReviewStatusPaths -Lines @($line))
        if ($p.Count -eq 0) { continue }
        $xy = "$line".Substring(0, 2)
        $full = Get-ReviewFullPath $p[0]
        if (Test-Path -LiteralPath $full -PathType Leaf) { $i = Get-Item -LiteralPath $full; $snap[$p[0]] = "$xy|$($i.Length)|$($i.LastWriteTimeUtc.Ticks)" }
        else { $snap[$p[0]] = "$xy|missing" }
    }
    return $snap
}

# C (F95): the rule files of an external repository with their size; exit 2 naming each wrong rules entry.
function Get-ReviewExternalRules([string[]] $Paths) {
    $rr = Resolve-PaperReviewRules -Declared $config.DeclaredRules -Paths $Paths
    if (@($rr.Errors).Count -gt 0) {
        foreach ($e in $rr.Errors) { Say "review: $e" }
        exit 2
    }
    $sizes = Get-ReviewSizes @($rr.Rules)
    return @($rr.Rules | Where-Object { $_ } | ForEach-Object { [pscustomobject]@{ Path = $_; Bytes = $(if ($sizes.Contains($_)) { [long] $sizes[$_] } else { 0 }) } })
}

# ------------------------------------------------------------------ init

function Invoke-ReviewInit {
    if (-not (Test-Path -LiteralPath $outFull -PathType Container)) { New-Item -ItemType Directory -Force -Path $outFull | Out-Null }
    $all = Get-ReviewRepoFiles
    $paths = @($all.Tracked) + @($all.Untracked)
    $created = ($null -eq $config)
    $declared = $null
    if (-not $created) { $declared = $config.DeclaredRules }
    $rr = Resolve-PaperReviewRules -Declared $declared -Paths $paths
    if (@($rr.Errors).Count -gt 0) {
        foreach ($e in $rr.Errors) { Say "review: $e" }
        exit 2
    }
    $buildConfig = $config
    if ($created) {
        $json = (New-PaperReviewExternalProfile -RepoRoot $root -Rules @($rr.Rules)) | ConvertTo-Json -Depth 6
        # PowerShell 5.1 writes ' < > & as \u escapes; valid JSON either way, but the owner reads this file.
        $json = [regex]::Replace($json, '(?<!\\)((?:\\\\)*)\\u00(27|3[cCeE]|26)', { param($m) $m.Groups[1].Value + [string] [char] [Convert]::ToInt32($m.Groups[2].Value, 16) })
        Write-Text $profileFile ($json + "`n")
        $buildConfig = [pscustomobject]@{ BuildCommand = ''; NoDeploy = @() }
    }
    $b = Get-PaperReviewExternalBuild -Config $buildConfig
    foreach ($line in (Format-PaperReviewInitLines -ProfilePath $profileFile -RepoRoot $root -Out $outFull -Rules @($rr.Rules) -Created $created -Build $b -Command "$($buildConfig.BuildCommand)")) { Say $line }
    if (-not $created) { foreach ($note in @($config.Notes)) { Say $note } }
    exit 0
}

# ------------------------------------------------------------------ plan, lanes

# The branch of an external repository (ADR-0037, D): its base, how old that base is here, and the files it
# changed - committed since the merge-base, then modified, staged or new on disk - all with read-only git,
# never a fetch. Error (the text after "review: ") at the first step that fails.
function Get-ReviewExternalBranch {
    $fail = { param($e) [pscustomobject]@{ Error = $e } }
    $first = { param($r) if ($r.Code -eq 0 -and @($r.Lines).Count -gt 0) { "$(@($r.Lines)[0])".Trim() } else { '' } }
    $verify = { param($ref) (Invoke-ReviewGit -C $root rev-parse --verify -q "$ref^{commit}").Code -eq 0 }
    if (-not (& $verify 'HEAD')) { return & $fail 'not verifiable: the repository has no commit yet - use -Scope project' }
    $label = Get-PaperReviewBranchLabel -Branch (& $first (Invoke-ReviewGit -C $root symbolic-ref --short -q HEAD)) -Head (& $first (Invoke-ReviewGit -C $root rev-parse HEAD))
    $found = @(@('origin/HEAD', 'origin/main', 'main', 'master') | Where-Object { & $verify $_ })
    $originHead = & $first (Invoke-ReviewGit -C $root symbolic-ref -q --short refs/remotes/origin/HEAD)
    $givenFound = $false
    if ($Base) { $givenFound = & $verify $Base }
    $b = Select-PaperReviewExternalBase -Given $Base -GivenFound $givenFound -OriginHead $originHead -Found $found
    if ($b.Error) { return & $fail "not verifiable: $($b.Error)" }
    $ref = $b.Ref
    $mb = & $first (Invoke-ReviewGit -C $root merge-base HEAD $ref)
    if (-not $mb) { return & $fail "not verifiable: no merge-base with $ref (shallow clone or unrelated history) - review does not fetch: deepen the clone yourself (git fetch --deepen=<n>) or use -Scope project" }
    $ahead = [int] (& $first (Invoke-ReviewGit -C $root rev-list --count "$mb..HEAD"))
    $sha = & $first (Invoke-ReviewGit -C $root rev-parse $ref)
    $kind = ConvertTo-PaperReviewBaseKind -FullName (& $first (Invoke-ReviewGit -C $root rev-parse --symbolic-full-name $ref))
    $cpFound = $false; $behind = 0
    if ($kind.Kind -eq 'local') {
        $cpFound = & $verify $kind.Counterpart
        if ($cpFound) { $behind = [int] (& $first (Invoke-ReviewGit -C $root rev-list --count "$ref..$($kind.Counterpart)")) }
    }
    # The last fetch of this machine: FETCH_HEAD in the common git folder (a worktree shares it).
    $age = -1; $date = ''
    $common = & $first (Invoke-ReviewGit -C $root rev-parse --path-format=absolute --git-common-dir)
    if ($common) {
        $fetchHead = Join-Path $common.Replace('/', '\') 'FETCH_HEAD'
        if (Test-Path -LiteralPath $fetchHead -PathType Leaf) {
            $item = Get-Item -LiteralPath $fetchHead
            $age = ((Get-Date).ToUniversalTime() - $item.LastWriteTimeUtc).TotalDays
            $date = $item.LastWriteTime.ToString('yyyy-MM-dd')
        }
    }
    $shallow = (& $first (Invoke-ReviewGit -C $root rev-parse --is-shallow-repository)) -eq 'true'
    $line = Format-PaperReviewBaseLine -Ref $ref -Why $b.Why -Sha $sha -MergeBase $mb -Ahead $ahead -Kind $kind.Kind -Counterpart $kind.Counterpart -CounterpartFound $cpFound -Behind $behind -FetchAgeDays $age -FetchDate $date -Shallow $shallow
    # --no-renames: a rename is read as a new file, so git never reads a blob (a partial clone would fetch it).
    $d = Invoke-ReviewGit -C $root diff --name-only --no-renames --no-ext-diff --no-textconv -z --diff-filter=d $mb HEAD
    if ($d.Code -ne 0) { return & $fail "not verifiable: git diff against $mb failed" }
    $committed = @((@($d.Lines) -join "`n") -split "`0" | ForEach-Object { $_.Trim("`n") } | Where-Object { $_ })
    $st = Invoke-ReviewGit -C $root status --porcelain --untracked-files=all
    if ($st.Code -ne 0) { return & $fail 'not verifiable: git status failed' }
    $split = Split-PaperReviewStatusEntries -Lines @($st.Lines)
    $counts = Get-PaperReviewExternalBranchPaths -Committed $committed -Changed @($split.Changed) -Untracked @($split.Untracked)
    return [pscustomobject]@{
        Error = ''; Label = $label; Ref = $ref; Why = $b.Why; MergeBase = $mb; Ahead = $ahead; BaseLine = $line.Line; Stale = $line.Stale
        Committed = $committed; Changed = @($split.Changed); Untracked = @($split.Untracked); Counts = $counts
    }
}

# ADR-0043, F205: who answers the agent lanes. The collab folder is PAPER_REVIEW_COLLAB_DIR (tests), else the user's .claude\collab;
# codex is "on PATH" when an application of that name is; the session that runs it is -Session claude (default) or codex (D6, ADR-0044).
function Get-ReviewCollabDir {
    if ($env:PAPER_REVIEW_COLLAB_DIR) { return $env:PAPER_REVIEW_COLLAB_DIR }
    return (Join-Path $env:USERPROFILE '.claude\collab')
}
function Get-ReviewExecutor($Solo) {
    $dir = Get-ReviewCollabDir
    $found = Test-Path -LiteralPath (Join-Path $dir 'scripts\collab.ps1') -PathType Leaf
    $codex = [bool] (Get-Command codex -CommandType Application -ErrorAction SilentlyContinue)
    $copy = ($Session -eq 'codex')
    $e = Get-PaperReviewExecutor -Requested $Executor -CollabDir $dir -CollabFound $found -CodexFound $codex -CollabRole $env:COLLAB_ROLE -CodexCopy $copy -SoloTokens ([long] $Solo.Tokens) -SoloLimit ([long] $config.SoloTokens)
    if ($e.Error) { Stop-Review 2 $e.Error }
    return $e
}

function Get-ReviewChangesLine($Branch) {
    return (Format-PaperReviewChangesLine -Committed $Branch.Counts.CommittedCount -Uncommitted $Branch.Counts.UncommittedCount -Untracked $Branch.Counts.UntrackedCount)
}

# The scope, chosen and checked; writes nothing. An external repository: the project by default; a branch
# scope reads its branch with the kit's own git (Get-ReviewExternalBranch), never review-files (F98).
function Select-ReviewScope {
    # -Files (C): relative to the repository root or absolute inside it; one that climbs out is refused before anything else.
    $fileList = @()
    if (@($Files | Where-Object { "$_".Trim() }).Count -gt 0) {
        $fq = ConvertTo-PaperReviewFilesRequest -Requested $Files -RepoRoot $root
        if ($fq.Error) { Stop-Review 2 "review: $($fq.Error)" }
        $fileList = @($fq.Paths)
    }
    $script:ReviewFilesAsked = $fileList
    if ($external) {
        $sel = Select-PaperReviewScope -Requested $Scope -Path $Path -Base $Base -External -Files $fileList
        if ($sel.Error) { Stop-Review 2 "review: $($sel.Error)" }
        if ($sel.Mode -eq 'branch') {
            $script:ReviewBranch = Get-ReviewExternalBranch
            if ($script:ReviewBranch.Error) { Stop-Review 2 "review: $($script:ReviewBranch.Error)" }
            $sel.Label = $script:ReviewBranch.Label
            $sel.Reason = "branch $($script:ReviewBranch.Label) against $($script:ReviewBranch.Ref)"
        }
        return $sel
    }
    $branch = "$((Invoke-PaperChangeGit -C $root rev-parse --abbrev-ref HEAD).Lines | Select-Object -First 1)".Trim()
    # The base and merge-base are the kit's one answer (change-set.ps1, as review-files reads them): -Base,
    # the base worktree create recorded, main/master/origin, or the branch's own remote when on the base.
    $change = Get-PaperChangeBase -RepoRoot $root -Base $Base -Tool 'review'
    $ahead = 0
    if ($change.Code -eq 0 -and "$($change.MergeBase)" -ne 'HEAD') {
        $c = Invoke-PaperChangeGit -C $root rev-list --count "$($change.MergeBase)..HEAD"
        if ($c.Code -eq 0) { $ahead = [int] ("$($c.Lines[0])".Trim()) }
    }
    $baseBranch = "$($change.BaseRef)"
    $dirty = Get-PaperReviewDirtyCount -StatusLines (Invoke-PaperChangeGit -C $root status --porcelain --untracked-files=all).Lines -ReportDir $config.Report -SarifDir $config.SarifDir

    $sel = Select-PaperReviewScope -Requested $Scope -Path $Path -Base $Base -CurrentBranch $branch -BaseBranch $baseBranch -BaseError "$($change.Message)" -Ahead $ahead -Dirty $dirty -Files $fileList
    if ($sel.Error) { Stop-Review 2 "review: $($sel.Error)" }
    return $sel
}

# The files of a scope with what is left out and why; ExitCode 5 (no file) or 2 (not verifiable) with its
# Line - never stops. A branch of an external repository: its own git list (ADR-0037); of a project:
# review-files.
function Get-ReviewScopeFiles($all, [string[]] $allPaths, $sel) {
    if ($sel.Mode -eq 'files') {
        # F231: the asked files, through the filters of the folder scope; a file git does not know and the disk does not have is "not found".
        $asked = @($script:ReviewFilesAsked)
        $bytes = Get-ReviewSizes $asked
        $onDisk = @($asked | Where-Object { $bytes.ContainsKey($_) })
        $known = @{}
        foreach ($t in (@($all.Tracked) + @($all.Untracked))) { $known[$t.ToLowerInvariant()] = $true }
        $binary = @{}
        foreach ($p in @($bytes.Keys)) { $binary[$p] = Test-PaperBinaryFile (Get-ReviewFullPath $p) }
        $submodules = @($asked | Where-Object { -not $bytes.ContainsKey($_) -and (Test-Path -LiteralPath (Get-ReviewFullPath $_) -PathType Container) })
        $missing = @($asked | Where-Object { -not $bytes.ContainsKey($_) -and $submodules -notcontains $_ -and $known.ContainsKey($_.ToLowerInvariant()) })
        $fs = Get-PaperReviewFilesScope -Paths $asked -Tracked @($all.Tracked) -Untracked @($all.Untracked) -OnDisk $onDisk -Bytes $bytes -Binary $binary -Missing $missing -Submodules $submodules -Exclude $config.Exclude
        return [pscustomobject]@{ ExitCode = $fs.ExitCode; Line = $fs.Line; Files = @($fs.Files); Excluded = @($fs.Excluded) }
    }
    if ($sel.Mode -eq 'branch' -and -not $external) {
        $args2 = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'review-files', '-Repo', $root)
        if ($Base) { $args2 += @('-Base', $Base) }
        $rf = Invoke-Native $script:PowerShellExe $args2
        $parsed = ConvertFrom-PaperReviewFilesOutput -Lines $rf.Lines -ExitCode $rf.Code
        return (Resolve-PaperReviewBranchScope -ReviewFiles $parsed -Sizes (Get-ReviewSizes @($parsed.Files | ForEach-Object { $_.Path })) -Label $sel.Label)
    }
    $tracked = @($all.Tracked); $untracked = @($all.Untracked); $folder = $sel.Folder
    if ($sel.Mode -eq 'branch') {
        $bp = $script:ReviewBranch.Counts
        $tracked = @($bp.Tracked); $untracked = @($bp.Untracked); $folder = ''
    }
    $paths = @($tracked) + @($untracked)
    $bytes = Get-ReviewSizes $paths
    $binary = @{}
    foreach ($p in @($bytes.Keys)) { $binary[$p] = Test-PaperBinaryFile (Get-ReviewFullPath $p) }
    $submodules = @($paths | Where-Object { -not $bytes.ContainsKey($_) -and (Test-Path -LiteralPath (Get-ReviewFullPath $_) -PathType Container) })
    $missing = @($paths | Where-Object { -not $bytes.ContainsKey($_) -and $submodules -notcontains $_ })
    $r = Get-PaperReviewScopeFiles -Tracked $tracked -Untracked $untracked -Bytes $bytes -Binary $binary -Missing $missing -Submodules $submodules -Folder $folder -Exclude $config.Exclude
    $line = ''
    if ($r.ExitCode -eq 5) { $line = Format-PaperReviewNoFileLine $sel.Mode $sel.Label }
    return [pscustomobject]@{ ExitCode = $r.ExitCode; Line = $line; Files = @($r.Files); Excluded = @($r.Excluded) }
}

# Everything plan decides once the scope is chosen - files, lanes, estimate, batches - for plan and for
# lanes alike; writes nothing. Exit 2 or 5 as plan.
function Get-ReviewPlanParts($all, [string[]] $allPaths, $sel, [string[]] $OnlyLanes, [string[]] $SkipLanes, [bool] $StaticOnlyRun) {
    $ruleObjs = @()
    if ($external) { $ruleObjs = @(Get-ReviewExternalRules $allPaths) }

    $scopeResult = Get-ReviewScopeFiles $all $allPaths $sel
    if ($scopeResult.ExitCode -ne 0) { Stop-Review $scopeResult.ExitCode $scopeResult.Line }
    $files = @($scopeResult.Files); $excluded = @($scopeResult.Excluded)
    # The base: and changes: lines of an external branch scope (ADR-0037).
    $scopeLines = @()
    if ($external -and $sel.Mode -eq 'branch') { $scopeLines = @($script:ReviewBranch.BaseLine, (Get-ReviewChangesLine $script:ReviewBranch)) }
    # F231: every file asked for that is left out is named with its reason, right after the scope line.
    if ($sel.Mode -eq 'files') { $scopeLines += @($excluded | ForEach-Object { "excluded: $($_.Path) ($($_.Reason))" }) }

    $screens = @(Get-PaperReviewScreens -Paths $allPaths -Glob $config.UiScreens -Folder $sel.Folder)
    $laneFiles = @{}
    foreach ($l in $script:PaperReviewLaneNames) { $laneFiles[$l] = @(Get-PaperReviewLaneFiles -Files $files -Lane $l -Screens $screens) }
    # M.7: when an analysis of this commit is saved, the architecture and smell lanes of the architecture review read only the hot spots.
    $sonar = $null
    if ($reviewKind -eq 'architecture' -and -not $AllFiles) { $sonar = Get-ReviewSonarForPlan $sel $allPaths $files }
    if ($null -ne $sonar) {
        $scopeLines += @($sonar.Lines)
        if ($null -ne $sonar.Allowed) {
            $dropped = @{}
            foreach ($ln in @('architecture', 'smell')) {
                $keep = @()
                foreach ($f in @($laneFiles[$ln])) { if ($sonar.Allowed.ContainsKey("$($f.Path)".ToLowerInvariant())) { $keep += $f } else { $dropped["$($f.Path)"] = $true } }
                $laneFiles[$ln] = $keep
            }
            $names = [string[]] @($dropped.Keys)
            [Array]::Sort($names, [StringComparer]::Ordinal)
            # F253: a file the analysis measured is "not in the top N"; one it has no number for says so.
            $scopeLines += @($names | ForEach-Object { "excluded: $_ ($(Get-PaperReviewSonarExcludedReason -Path $_ -Measured $sonar.Measured -Top $sonar.TopCount))" })
        }
    }
    $hasDotnet = Test-PaperReviewDotnetProject -Paths $allPaths
    if ($external) {
        $b = Get-PaperReviewExternalBuild -Config $config
        $lanes = Get-PaperReviewLanes -Hosts $config.Hosts -Config $config -LaneFiles $laneFiles -HasDotnetProject $hasDotnet -HasBuildVerb $b.Run -NoBuildReason $b.Reason -RuleCount $ruleObjs.Count -Only $OnlyLanes -Skip $SkipLanes -StaticOnly:$StaticOnlyRun -Kind $reviewKind
    }
    else {
        $hasBuild = (Get-PaperVerbPlan -ProjectProfile $profileMap -Verb 'build').ExitCode -eq 0
        $lanes = Get-PaperReviewLanes -Hosts $config.Hosts -Config $config -LaneFiles $laneFiles -HasDotnetProject $hasDotnet -HasBuildVerb $hasBuild -Only $OnlyLanes -Skip $SkipLanes -StaticOnly:$StaticOnlyRun -Kind $reviewKind
    }
    if ($lanes.Error) { Stop-Review 2 "review: $($lanes.Error)" }
    # D: what a session would read alone - the files the reading lanes read, each once - decides who answers.
    $soloContent = Get-PaperReviewSoloContent -Lanes $lanes.Lanes -LaneFiles $laneFiles
    $exec = Get-ReviewExecutor $soloContent
    $maxTokens = [int] $config.BatchTokens
    if ($exec.Kind -eq 'collab') { $maxTokens = [int] $config.CodexBatchTokens }
    if ($exec.Kind -eq 'solo') { $maxTokens = [int]::MaxValue }
    $specPaths = @($allPaths | Where-Object { $_ -match '(?:^|/)SPEC\.md$' })
    $unitRoots = @($allPaths | Where-Object { $_ -match '(?:^|/)(SPEC|CODEMAP)\.md$' } | ForEach-Object {
        $p = ConvertTo-PaperReviewRelPath $_
        $slash = $p.LastIndexOf('/')
        if ($slash -lt 0) { '' } else { $p.Substring(0, $slash) }
    } | Select-Object -Unique)
    $estimate = Get-PaperReviewEstimate -Lanes $lanes.Lanes -LaneFiles $laneFiles -Config $config -StaticOnly:$StaticOnlyRun -Rules $ruleObjs -Executor $exec.Kind -UnitRoots $unitRoots
    $batches = @(Get-PaperReviewBatchList -Lanes $lanes.Lanes -LaneFiles $laneFiles -MaxTokens $maxTokens -Rules $ruleObjs -UnitRoots $unitRoots -Specs $specPaths -Executor $exec.Kind -SubagentMaxTokens ([int] $config.BatchTokens))
    $collab = $null
    if ($exec.Kind -eq 'collab') {
        $modelsFile = Join-Path (Get-ReviewCollabDir) 'models.json'
        $modelsText = ''
        if (Test-Path -LiteralPath $modelsFile -PathType Leaf) { try { $modelsText = [IO.File]::ReadAllText($modelsFile, [Text.Encoding]::UTF8) } catch { $modelsText = '' } }
        $caps = Get-PaperReviewCollabCaps -ModelsText $modelsText
        $collab = [pscustomobject]@{ CollabScript = $exec.CollabScript; Parallel = $caps.Parallel; GapSec = $caps.GapSec; TurnTokens = @($estimate.TurnTokens); LaneExecutors = @($lanes.Lanes | Where-Object { $_.State -eq 'run' -and $_.Name -ne 'static' } | ForEach-Object {
            $kind = Get-PaperReviewLaneExecutor -Lane $_.Name -Base $exec.Kind
            [pscustomobject]@{ Lane = $_.Name; Reader = $(if ($kind -eq 'subagents') { 'claude subagents' } else { 'codex via collab' }) }
        }); Root = $root; VerifyCap = [int] $config.VerifyCap }
    }
    $solo = $null
    if ($exec.Kind -eq 'solo') {
        $solo = [pscustomobject]@{ Files = $soloContent.Files; Tokens = $soloContent.Tokens; Limit = [long] $config.SoloTokens }
        # A finding is read back by Codex through a read-only collab session that opens only then: its line, when the machine has it.
        if ($exec.CollabScript) { $collab = [pscustomobject]@{ CollabScript = $exec.CollabScript; Parallel = 1; GapSec = 0; TurnTokens = @(); Root = $root; VerifyCap = [int] $config.VerifyCap } }
    }
    return [pscustomobject]@{ Files = $files; Excluded = $excluded; Screens = $screens; LaneFiles = $laneFiles; Lanes = $lanes.Lanes; Estimate = $estimate; Batches = $batches; Rules = $ruleObjs; ScopeLines = $scopeLines; Executor = $exec; Collab = $collab; Solo = $solo; Sonar = $(if ($null -ne $sonar) { $sonar.Plan } else { $null }) }
}

function Invoke-ReviewPlan {
    $all = Get-ReviewRepoFiles
    $allPaths = @($all.Tracked) + @($all.Untracked)
    $sel = Select-ReviewScope
    $parts = Get-ReviewPlanParts $all $allPaths $sel $Only $Skip $StaticOnly.IsPresent
    $files = @($parts.Files); $excluded = @($parts.Excluded); $estimate = $parts.Estimate; $ruleObjs = @($parts.Rules)
    $approved = [bool] (Test-PaperReviewApprovedAtPlan -Lanes $parts.Lanes -Yes $Yes.IsPresent)

    Initialize-ReviewRunsDir
    $existingRuns = @(Get-ChildItem -LiteralPath $runsDir -Directory | ForEach-Object { $_.Name })
    $runId = Get-PaperReviewRunId -Stamp (Get-Date).ToString('yyyyMMdd-HHmmss') -Existing $existingRuns
    $dir = Join-Path $runsDir $runId
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $plan = [pscustomobject]@{
        Run = $runId; Mode = $sel.Mode; Label = $sel.Label; Reason = $sel.Reason; Folder = $sel.Folder; Base = $Base
        StaticOnly = $StaticOnly.IsPresent; Files = $files; Excluded = $excluded; Lanes = $parts.Lanes; Batches = @($parts.Batches)
        Screens = @($parts.Screens); Estimate = $estimate; Approved = $approved; Created = (Get-Date).ToString('s')
        ScopeLines = @($parts.ScopeLines); Kind = $reviewKind
    }
    $plan | Add-Member -NotePropertyName Executor -NotePropertyValue $parts.Executor.Kind
    $plan | Add-Member -NotePropertyName ExecutorReason -NotePropertyValue $parts.Executor.Reason
    $plan | Add-Member -NotePropertyName CollabScript -NotePropertyValue $parts.Executor.CollabScript
    $plan | Add-Member -NotePropertyName SoloReader -NotePropertyValue "$($parts.Executor.SoloReader)"
    $plan | Add-Member -NotePropertyName Session -NotePropertyValue $Session
    if ($null -ne $parts.Sonar) { $plan | Add-Member -NotePropertyName Sonar -NotePropertyValue $parts.Sonar }
    $repoLine = ''
    if ($external) {
        $plan | Add-Member -NotePropertyName External -NotePropertyValue $true
        $plan | Add-Member -NotePropertyName RepoRoot -NotePropertyValue $root
        $plan | Add-Member -NotePropertyName ProfilePath -NotePropertyValue $profileFile
        $plan | Add-Member -NotePropertyName Rules -NotePropertyValue $ruleObjs
        $repoLine = $root
    }
    Write-Json (Join-Path $dir 'plan.json') $plan
    foreach ($line in (Format-PaperReviewEstimate -Run $runId -Mode $sel.Mode -Reason $sel.Reason -FileCount $files.Count -ExcludedCount $excluded.Count -Estimate $estimate -Approved $approved -RepoRoot $repoLine -RunDir $dir -ScopeLines @($parts.ScopeLines) -Executor $parts.Executor.Kind -ExecutorReason $parts.Executor.Reason -Collab $parts.Collab -Solo $parts.Solo)) { Say $line }
    foreach ($note in @($config.Notes)) { Say $note }
    exit 0
}

# The lane question (ADR-0035, F99-F105): the lanes that apply and their cost, with no choice applied; with
# -Pick, the answer turned into the exact plan command line. Writes nothing, creates no run folder.
function Invoke-ReviewLanes {
    $all = Get-ReviewRepoFiles
    $allPaths = @($all.Tracked) + @($all.Untracked)
    # F126: an external repository, no scope typed, the branch off its base - ask the scope first, alone.
    # No base or no merge-base: no question, the project as before.
    if ($external -and -not $pickGiven -and -not $scopeGiven) {
        $br = Get-ReviewExternalBranch
        if (-not $br.Error) {
            $script:ReviewBranch = $br
            $bs = Get-ReviewScopeFiles $all $allPaths ([pscustomobject]@{ Mode = 'branch'; Label = $br.Label; Folder = '' })
            $bc = 0
            if ($bs.ExitCode -eq 0) { $bc = @($bs.Files).Count }
            if (Test-PaperReviewAskScope -Ahead $br.Ahead -Uncommitted $br.Counts.UncommittedCount -Untracked $br.Counts.UntrackedCount -BranchCount $bc) {
                $ps = Get-ReviewScopeFiles $all $allPaths ([pscustomobject]@{ Mode = 'project'; Label = ''; Folder = '' })
                foreach ($line in @(Format-PaperReviewScopeChoice -Label $br.Label -RepoRoot $root -BaseLine $br.BaseLine -ChangesLine (Get-ReviewChangesLine $br) -ProjectCount @($ps.Files).Count -ProjectExcluded @($ps.Excluded).Count -BranchCount $bc -BranchExcluded @($bs.Excluded).Count)) { Say $line }
                exit 0
            }
        }
    }
    $sel = Select-ReviewScope
    $parts = Get-ReviewPlanParts $all $allPaths $sel @() @() $false
    if (-not $pickGiven) {
        $repoLine = ''
        if ($external) { $repoLine = $root }
        $list = Format-PaperReviewLaneList -Mode $sel.Mode -Reason $sel.Reason -FileCount @($parts.Files).Count -ExcludedCount @($parts.Excluded).Count -Estimate $parts.Estimate -RepoRoot $repoLine -ScopeLines @($parts.ScopeLines) -Executor $parts.Executor.Kind -ExecutorReason $parts.Executor.Reason -Collab $parts.Collab -Solo $parts.Solo
        foreach ($line in @($list.Lines)) { Say $line }
        if ($list.ExitCode -eq 0) { foreach ($note in @($config.Notes)) { Say $note } }
        exit $list.ExitCode
    }
    $r = Resolve-PaperReviewLanePick -Pick $Pick -Lanes $parts.Lanes
    if ($r.Error -eq 'no lane applies to this scope') { Stop-Review 5 'review: NOT APPLICABLE - no lane applies to this scope' }
    if ($r.Error) { Stop-Review 2 "review: $($r.Error)" }
    foreach ($line in @(Format-PaperReviewPickLines -Pick $r -Mode $sel.Mode -Reason $sel.Reason -Arguments $planArgs)) { Say $line }
    exit 0
}

# ------------------------------------------------------------------ approve, files

function Invoke-ReviewApprove {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $plan.Approved = $true
    $plan | Add-Member -NotePropertyName ApprovedAt -NotePropertyValue (Get-Date).ToString('s') -Force
    Write-Json (Join-Path $dir 'plan.json') $plan
    Say "review: run $Run approved - lanes may start"
    exit 0
}

function Read-ReviewStatic([string] $Dir) {
    $file = Join-Path $Dir 'static.json'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
    return Read-Json $file
}

function Invoke-ReviewFiles {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $answer = $null
    $answerFile = Join-Path $dir "lanes\$Lane-$Batch.md"
    if ($Retry -and (Test-Path -LiteralPath $answerFile -PathType Leaf)) { $answer = Read-Lines $answerFile }
    $static = Read-ReviewStatic $dir
    $groups = @(); $hints = @()
    if ($null -ne $static) { $groups = @($static.Groups); if ($null -ne $static.PSObject.Properties['Hints']) { $hints = @($static.Hints | Where-Object { $null -ne $_ }) } }
    $r = Get-PaperReviewFilesList -Plan $plan -Lane $Lane -Batch $Batch -Run $Run -Retry $Retry.IsPresent -RetryAnswer $answer -StaticGroups $groups -StaticHints $hints
    foreach ($line in @($r.Lines)) { Say $line }
    exit $r.ExitCode
}

# ------------------------------------------------------------------ prompt (ADR-0043, F213)

# The instruction files of a lane, pasted word for word into its prompt: paths from the engine folder, the checklists in the skills that own them (ADR-0044).
function Get-ReviewInstructionPaths([string] $LaneName, [string[]] $Files) {
    $skill = $PSScriptRoot
    $rel = @()
    switch ($LaneName) {
        'security' { $rel = @('..\..\skills\review-security\references\checklist.md') }
        'bug' { $rel = @('..\..\skills\find-bug\SKILL.md') }
        'smell' { $rel = @('..\..\skills\review-architecture\references\smell.md') }
        'ui' { $rel = @(Get-PaperReviewUiInstructions -Files $Files | ForEach-Object { Join-Path '..\..' $_ }) }
    }
    if ($LaneName -eq 'architecture') {
        $cands = @('..\..\agents\architecture-reviewer.md', '..\..\..\.claude\agents\architecture-reviewer.md') | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $skill $_)) }
        foreach ($c in $cands) { if (Test-Path -LiteralPath $c -PathType Leaf) { return @($c) } }
        Stop-Review 2 "review: instruction file not found: $($cands[0])"
    }
    $paths = @($rel | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $skill $_)) })
    foreach ($p in $paths) { if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { Stop-Review 2 "review: instruction file not found: $p" } }
    return $paths
}

function Get-ReviewPromptTemplate([string] $Name) {
    $file = Join-Path $PSScriptRoot 'prompts.md'
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { Stop-Review 2 "review: instruction file not found: $file" }
    $t = Get-PaperReviewPromptTemplate -PromptsText ([IO.File]::ReadAllText($file, [Text.Encoding]::UTF8)) -Name $Name
    if (-not $t) { Stop-Review 2 "review: no '$Name' template in $file" }
    return $t
}

function Invoke-ReviewPrompt {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $external = ($plan.External -eq $true)
    $repoText = ''
    if ($external) { $repoText = "$($plan.RepoRoot)" }
    if ($Verify) {
        $fjson = Join-Path $dir 'findings.json'
        $found = $null
        if (Test-Path -LiteralPath $fjson -PathType Leaf) { $found = @((Read-Json $fjson).Findings | Where-Object { $null -ne $_ -and "$($_.Id)" -ieq $Verify -and "$($_.Status)" -eq 'queued' })[0] }
        if ($null -eq $found) { Stop-Review 2 "review: $Verify is not queued for verification - run review.ps1 check" }
        $howTo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\skills\find-bug\SKILL.md'))
        if (-not (Test-Path -LiteralPath $howTo -PathType Leaf)) { Stop-Review 2 "review: instruction file not found: $howTo" }
        $text = New-PaperReviewVerifyPrompt -Template (Get-ReviewPromptTemplate 'Verify prompt') -Finding $found -Run $Run -External $external -Repo $repoText -HowToPath $howTo -HowToText ([IO.File]::ReadAllText($howTo, [Text.Encoding]::UTF8))
        $id = "$($found.Id)"
        $promptFile = Join-Path $dir "prompts\verify-$id.md"
        Write-Text $promptFile ($text + "`n")
        $reader = "$($found.Reader)"; if (-not $reader) { $reader = 'claude' }
        Say "review-prompt: verify $id, run $Run - reader $reader"
        Say "prompt: $promptFile"
        Say "answer: $(Join-Path $dir "verdicts\$id.md")"
        Say "task: verify-$id"
        exit 0
    }
    if (-not $Lane) { Stop-Review 2 'review: prompt needs -Lane <lane> [-Batch <n>] or -Verify <id>' }
    $tag = "$Lane-$Batch"
    $answerFile = Join-Path $dir "lanes\$tag.md"
    $answer = $null
    if ($Retry) {
        if (-not (Test-Path -LiteralPath $answerFile -PathType Leaf)) { Stop-Review 2 "review: $tag has no answer to send back - save it first" }
        $answer = Read-Lines $answerFile
    }
    $static = Read-ReviewStatic $dir
    $groups = @(); $hints = @()
    if ($null -ne $static) { $groups = @($static.Groups); if ($null -ne $static.PSObject.Properties['Hints']) { $hints = @($static.Hints | Where-Object { $null -ne $_ }) } }
    $r = Get-PaperReviewFilesList -Plan $plan -Lane $Lane -Batch $Batch -Run $Run -Retry $Retry.IsPresent -RetryAnswer $answer -StaticGroups $groups -StaticHints $hints
    if ($r.ExitCode -ne 0) { foreach ($line in @($r.Lines)) { Say $line }; exit $r.ExitCode }
    $all = @(Get-PaperReviewPlanBatches $plan $Lane)
    $paths = @($all[$Batch - 1].Files)
    $prefix = Get-PaperReviewPrefix $Lane $Batch $all.Count
    $errors = @()
    if ($Retry) {
        $a = Test-PaperReviewLaneAnswer -Lines $answer -Lane $Lane -Prefix $prefix -ListPaths $paths -FileCount $paths.Count
        foreach ($e in @($a.Errors)) { $errors += "${tag}: $e" }
        if (-not $a.Complete -and @($a.Errors).Count -eq 0) { $errors += "${tag}: not read: $(@($a.NotRead) -join ', ')" }
    }
    $instructions = @(Get-ReviewInstructionPaths -LaneName $Lane -Files $paths | ForEach-Object { [pscustomobject]@{ Path = $_; Text = [IO.File]::ReadAllText($_, [Text.Encoding]::UTF8) } })
    $findingsFile = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'findings.md'))
    $text = New-PaperReviewLanePrompt -Template (Get-ReviewPromptTemplate 'Lane prompt') -Lane $Lane -Batch $Batch -BatchCount $all.Count -Run $Run -Prefix $prefix -FilesLines @($r.Lines) `
        -External $external -Repo $repoText -Retry $Retry.IsPresent -RetryErrors $errors -FindingsPath $findingsFile -FindingsText ([IO.File]::ReadAllText($findingsFile, [Text.Encoding]::UTF8)) -Instructions $instructions
    $name = $tag; $retryText = ''
    if ($Retry) { $name = "$tag.2"; $retryText = ' - retry' }
    $promptFile = Join-Path $dir "prompts\$name.md"
    Write-Text $promptFile ($text + "`n")
    Say "review-prompt: lane $Lane batch $Batch of $($all.Count), run $Run$retryText"
    Say "prompt: $promptFile"
    Say "answer: $(Join-Path $dir "lanes\$name.md")"
    Say "task: $name"
    $laneExecutor = Get-PaperReviewLaneExecutor -Lane $Lane -Base "$($plan.Executor)"
    Say "executor: $laneExecutor"
    exit 0
}

# ------------------------------------------------------------------ static

# What git sees of the repository that is not committed, for the key of a build that can be used again (F242): "<XY> <path> <sha256 of the
# content | deleted>", the reports and the SARIF copies of earlier runs left out (they are written on purpose, like Get-PaperReviewDirtyCount).
function Get-ReviewStaticChanges {
    $st = Invoke-ReviewGit -C $root -c core.quotepath=false status --porcelain=v1 --untracked-files=all
    if ($st.Code -ne 0) { return $null }
    $skip = @(@($config.Report, $config.SarifDir) | Where-Object { $_ } | ForEach-Object { (ConvertTo-PaperReviewRelPath $_) + '/' })
    $rows = @()
    foreach ($line in @($st.Lines)) {
        $paths = @(Get-PaperReviewStatusPaths -Lines @($line))
        if ($paths.Count -eq 0) { continue }
        $p = $paths[0]
        if (@($skip | Where-Object { $p.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) { continue }
        $full = Get-ReviewFullPath $p
        $hash = 'deleted'
        if (Test-Path -LiteralPath $full -PathType Leaf) { $hash = Get-ReviewFileSha256 $full }
        $rows += "$("$line".Substring(0, 2)) $p $hash"
    }
    return , $rows
}

function Get-ReviewFileSha256([string] $Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (-join ($sha.ComputeHash([IO.File]::ReadAllBytes($Path)) | ForEach-Object { $_.ToString('x2') })) } finally { $sha.Dispose() }
}

function Get-ReviewTextSha256([string] $Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return (-join ($sha.ComputeHash($script:Utf8.GetBytes($Text)) | ForEach-Object { $_.ToString('x2') })) } finally { $sha.Dispose() }
}

function Invoke-ReviewStatic {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $kind = "$($plan.Kind)"
    if ($kind -eq 'ui') { Stop-Review 5 'review: static NOT APPLICABLE - /review-ui has no static lane' }
    $gate = Get-PaperReviewStaticGate -Row @($plan.Lanes | Where-Object { $_.Name -eq 'static' })[0]
    if ($gate.ExitCode -ne 0) { Stop-Review $gate.ExitCode $gate.Line }

    $staticJson = Join-Path $dir 'static.json'
    $fail = {
        param([string] $reason, [string[]] $tail)
        Write-Json $staticJson ([pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = ''; Build = ''; Kind = $kind; Findings = @(); Groups = @(); Hints = @(); Counts = [ordered]@{} })
        Say "review: static not verifiable - $reason"
        foreach ($t in @($tail)) { Say "  $t" }
        exit 4
    }

    if (-not (Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue)) { & $fail 'dotnet SDK not found' @() }
    $ver = Invoke-Native 'dotnet.exe' @('--version')
    $tfm = Get-PaperReviewTargetFramework -VersionText "$(@($ver.Lines)[0])"
    if ($ver.Code -ne 0 -or -not $tfm) { & $fail 'dotnet SDK not found' @() }

    $all = Get-ReviewRepoFiles
    $styles = @{}
    foreach ($p in @(@($all.Tracked) + @($all.Untracked) | Where-Object { $_ -match '\.(csproj|vbproj)$' })) {
        $full = Get-ReviewFullPath $p
        if (Test-Path -LiteralPath $full -PathType Leaf) { $styles[$p] = Get-PaperReviewProjectStyle -Text ([IO.File]::ReadAllText($full)) }
    }
    $analyzers = $config.Analyzers
    $pk = Get-PaperReviewStaticPackages -Analyzers $analyzers -Styles $styles

    $globalConfig = Join-Path $dataDir 'review.globalconfig'
    if ($config.GlobalConfig) {
        $globalConfig = Get-ReviewFullPath $config.GlobalConfig
        if (-not (Test-Path -LiteralPath $globalConfig -PathType Leaf)) { Stop-Review 2 "review: review.staticAnalysis.globalconfig: $($config.GlobalConfig) does not exist" }
    }
    # F236: the limits of the profile become a SonarLint.xml of this run, an additional file of every project of the build.
    $sonarLintXml = ''
    $sonarLintText = New-PaperReviewSonarLintXml -Limits $config.ComplexityLimits
    if ($sonarLintText) {
        $sonarLintXml = Join-Path $dir 'SonarLint.xml'
        Write-Text $sonarLintXml ($sonarLintText + "`r`n")
    }

    # F242: a build of the same commit, the same uncommitted content, the same analyzers, configuration and build line is not made again.
    if ($external) { $buildLine = "$($config.BuildCommand)" } else { $buildLine = "$((Get-PaperVerbPlan -ProjectProfile $profileMap -Verb 'build').Command)" }
    $headLine = Invoke-ReviewGit -C $root rev-parse HEAD
    $head = ''
    if ($headLine.Code -eq 0 -and @($headLine.Lines).Count -gt 0) { $head = "$(@($headLine.Lines)[0])".Trim() }
    $changes = Get-ReviewStaticChanges
    $key = ''
    if ($null -ne $changes) {
        $key = Get-PaperReviewStaticKey -Head $head -Changes @($changes) -Analyzers $analyzers -GlobalConfigHash (Get-ReviewFileSha256 $globalConfig) `
            -SonarLintHash $(if ($sonarLintText) { Get-ReviewTextSha256 $sonarLintText } else { '' }) -BuildLine $buildLine
    }
    $cacheRoot = Join-Path $runsDir 'static-cache'
    $cacheDir = ''
    $cache = $null
    if ($key) {
        $cacheDir = Join-Path $cacheRoot $key.Substring(0, 16)
        $cacheFile = Join-Path $cacheDir 'cache.json'
        if (Test-Path -LiteralPath $cacheFile -PathType Leaf) {
            try { $cache = Read-Json $cacheFile } catch { $cache = $null }
            if ($null -ne $cache -and "$($cache.Key)" -ne $key) { $cache = $null }
        }
    }

    $targets = Join-Path $dir 'Paper.Review.targets'
    $envs = Get-PaperReviewBuildEnvironment -Targets $targets -Analyzers $analyzers
    $sarifDir = Join-Path $dir 'sarif'
    New-Item -ItemType Directory -Force -Path $sarifDir | Out-Null
    $dllsById = [ordered]@{}
    $expected = $null
    $reused = ($null -ne $cache)
    $analyzerText = ''
    $buildText = ''
    $buildCode = 0
    if ($reused) {
        [IO.Directory]::SetLastWriteTimeUtc($cacheDir, [DateTime]::UtcNow)
        foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $cacheDir 'sarif') -Filter '*.sarif' -File -ErrorAction SilentlyContinue)) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $sarifDir $f.Name) -Force }
        foreach ($f in @(Get-ChildItem -LiteralPath $cacheDir -Filter 'analyzers-*.txt' -File -ErrorAction SilentlyContinue)) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $dir $f.Name) -Force }
        $expected = [ordered]@{}
        foreach ($p in @($cache.Expected.PSObject.Properties)) { $expected[$p.Name] = [string[]] @($p.Value) }
        $analyzerText = "$($cache.AnalyzerText)"
        $buildText = "$($cache.BuildText) - build of run $($cache.Run), reused"
    }
    else {
        if ($pk.Packages.Count -gt 0) {
            $restoreDir = Join-Path $dir 'restore'
            $restoreProj = Join-Path $restoreDir 'review-restore.csproj'
            Write-Text $restoreProj ((New-PaperReviewRestoreProject -Packages $pk.Packages -TargetFramework $tfm) + "`r`n")
            # The repository's own Directory.*.props/targets would be imported by a project under it; these empty
            # ones stop that search, so the restore reads only what this run wrote (NuGet.config still applies).
            foreach ($stop in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) { Write-Text (Join-Path $restoreDir $stop) "<Project />`r`n" }
            $rs = Invoke-ReviewWithEnvironment @{ 'MSBUILDDISABLENODEREUSE' = $envs['MSBUILDDISABLENODEREUSE'] } { Invoke-Native 'dotnet.exe' @('restore', $restoreProj, '--nologo') }
            Write-Text (Join-Path $dir 'restore.log') ((@($rs.Lines) -join "`r`n") + "`r`n")
            if ($rs.Code -ne 0) { & $fail (Get-PaperReviewRestoreFailure -Lines $rs.Lines -Packages $pk.Packages -ExitCode $rs.Code) @() }
            $locals = Invoke-Native 'dotnet.exe' @('nuget', 'locals', 'global-packages', '--list')
            $cacheNuget = ConvertFrom-PaperNugetLocals -Lines $locals.Lines
            if (-not $cacheNuget) { & $fail 'the NuGet global-packages folder is unknown (dotnet nuget locals global-packages --list)' @() }
            foreach ($id in @($pk.Packages.Keys)) {
                $pkgDir = Join-Path $cacheNuget ("$($id.ToLowerInvariant())\$(ConvertTo-PaperReviewNugetVersion $pk.Packages[$id])")
                $rel = @()
                if (Test-Path -LiteralPath $pkgDir -PathType Container) {
                    $rel = @(Get-ChildItem -LiteralPath $pkgDir -Recurse -File | ForEach-Object { $_.FullName.Substring($pkgDir.TrimEnd('\').Length + 1).Replace('\', '/') })
                }
                $dlls = @(Get-PaperReviewAnalyzerDlls -Files $rel)
                if ($dlls.Count -eq 0) { & $fail "$id $($pk.Packages[$id]) has no analyzer assembly" @() }
                $dllsById[$id] = @($dlls | ForEach-Object { Join-Path $pkgDir ($_.Replace('/', '\')) })
            }
        }
        $plug = Get-PaperReviewAnalyzerPlug -DllsById $dllsById
        $expected = Get-PaperReviewExpectedDlls -Analyzers $analyzers -HasSdk $pk.HasSdk -DllsById $dllsById
        # Written right before the build: its time stamp, newer than every output, makes CoreCompile run again.
        Write-Text $targets ((New-PaperReviewTargets -Run $Run -SarifDir $sarifDir -AnalyzerListDir $dir -GlobalConfig $globalConfig -Analyzers $plug.Analyzers -LegacyOnly $plug.LegacyOnly -SonarLintXml $sonarLintXml) + "`r`n")

        if ($external) {
            # E: the owner's build line, run directly from the repository root (no run lock, no profile of the
            # repository); git's view of the repository before and after it (F94).
            $eb = Get-PaperReviewExternalBuild -Config $config
            if (-not $eb.Run) { & $fail $eb.Reason @() }
            . (Join-Path $paperflow 'project-command.ps1')
            $snapBefore = Get-ReviewRepoSnapshot
            if ($null -eq $snapBefore) { & $fail 'git status failed before the build' @() }
            $res = Invoke-ReviewWithEnvironment $envs { Invoke-PaperProjectCommand -Directory $root -Command $config.BuildCommand }
            $buildCode = if ($null -eq $res.Code) { 1 } else { [int] $res.Code }
            $buildLines = @("review: build -> $($config.BuildCommand)") + @($res.Lines)
            $snapAfter = Get-ReviewRepoSnapshot
            Write-Text (Join-Path $dir 'build.log') ((@($buildLines) -join "`r`n") + "`r`n")
            if ($null -eq $snapAfter) { & $fail 'git status failed after the build' @() }
            $changed = @(Compare-PaperReviewRepoSnapshot -Before $snapBefore -After $snapAfter)
            if ($changed.Count -gt 0) { & $fail (Format-PaperReviewRepoChange -Paths $changed) @() }
        }
        else {
            $build = Invoke-ReviewWithEnvironment $envs { Invoke-Native $script:PowerShellExe @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'build', '-Repo', $root, '-Full') }
            $buildCode = $build.Code
            $buildLines = @($build.Lines)
            Write-Text (Join-Path $dir 'build.log') ((@($buildLines) -join "`r`n") + "`r`n")
        }
        $sarifNow = @(Get-ChildItem -LiteralPath $sarifDir -Filter '*.sarif' -File -ErrorAction SilentlyContinue)
        $bf = Get-PaperReviewBuildFailure -ExitCode $buildCode -Lines $buildLines -SarifCount $sarifNow.Count -External:$external
        if ($null -ne $bf) { & $fail $bf.Reason $bf.Tail }
        $analyzerText = Format-PaperReviewAnalyzerText -Analyzers $analyzers -HasLegacy $pk.HasLegacy
    }
    $sarifFiles = @(Get-ChildItem -LiteralPath $sarifDir -Filter '*.sarif' -File -ErrorAction SilentlyContinue | Sort-Object Name)
    if (-not $reused) {
        $buildText = $(if ($external) { "build.command of review.profile.json (exit $buildCode), $($sarifFiles.Count) SARIF file(s)" } else { "paperflow.ps1 build -Full (exit $buildCode), $($sarifFiles.Count) SARIF file(s)" })
    }
    $results = @(); $problems = @()
    foreach ($f in $sarifFiles) {
        try { $obj = Read-Json $f.FullName }
        catch { $problems += "$($f.Name): not JSON - $($_.Exception.Message)"; continue }
        $c = ConvertFrom-PaperReviewSarif -Sarif $obj -File $f.Name
        $results += @($c.Results); $problems += @($c.Problems)
    }
    $listed = @()
    foreach ($t in @(Get-ChildItem -LiteralPath $dir -Filter 'analyzers-*.txt' -File -ErrorAction SilentlyContinue)) { foreach ($line in (Read-Lines $t.FullName)) { $listed += $line } }
    $sonarFile = Join-Path $dataDir 'sonar-cs-rules.tsv'
    $sonar = @{}
    if (Test-Path -LiteralPath $sonarFile -PathType Leaf) { $sonar = Read-PaperReviewSonarTable -Lines (Read-Lines $sonarFile) }
    $scopePaths = @($plan.Files | ForEach-Object { $_.Path })
    $grouped = Group-PaperReviewStatic -Results $results -RepoRoot $root -ScopePaths $scopePaths -ScopeMode $plan.Mode -ProfileKinds $config.Kinds -SonarTable $sonar
    $proof = @(Get-PaperReviewAnalyzerProof -Listed $listed -Expected $expected -LoadProblems $grouped.LoadProblems)
    $outcome = Get-PaperReviewStaticOutcome -Grouped $grouped -Proof $proof -AnalyzerText $analyzerText -BuildText $buildText -Problems $problems -SarifDir $config.SarifDir -SarifCount $sarifFiles.Count -Kind $kind
    $complexity = $null
    if ($outcome.ExitCode -eq 0) {
        # E: the source lines of the places the three complexity rules reported, read from the working tree.
        $lineMap = @{}
        foreach ($r in @($results | Where-Object { $script:PaperReviewComplexityRules -ccontains "$($_.RuleId)" })) {
            $loc = ConvertTo-PaperReviewRepoPath -Uri $r.Uri -RepoRoot $root
            if ($loc.Reason -or $lineMap.ContainsKey($loc.Path)) { continue }
            $full = Get-ReviewFullPath $loc.Path
            if (Test-Path -LiteralPath $full -PathType Leaf) { try { $lineMap[$loc.Path] = [string[]] [IO.File]::ReadAllLines($full) } catch { } }
        }
        # F243: the three rules the server reported for this commit (a clean tree only) join the members and expressions of the table.
        $cxResults = @($results)
        if ($kind -eq 'architecture' -and -not $external -and $config.Sonar.ProjectKey -and @($changes).Count -eq 0) {
            $sres = Read-ReviewSonarResult $config.Sonar.ProjectKey $head
            if ($null -ne $sres) {
                $cxResults += @(ConvertTo-PaperReviewSonarComplexityResults -Issues @($sres.Issues) -RepoRoot $root)
                foreach ($i in @($sres.Issues | Where-Object { $script:PaperReviewComplexityRules -ccontains "$($_.Rule)" })) {
                    $full = Get-ReviewFullPath "$($i.Path)"
                    if (-not $lineMap.ContainsKey("$($i.Path)") -and (Test-Path -LiteralPath $full -PathType Leaf)) { try { $lineMap["$($i.Path)"] = [string[]] [IO.File]::ReadAllLines($full) } catch { } }
                }
            }
        }
        $complexity = Get-PaperReviewComplexity -Results $cxResults -RepoRoot $root -ScopePaths $scopePaths -ScopeMode $plan.Mode -Lines $lineMap
        $outcome.Json | Add-Member -NotePropertyName Complexity -NotePropertyValue $complexity -Force
    }
    Write-Json $staticJson $outcome.Json
    if ($outcome.CopySarifTo) {
        $keep = Get-ReviewFullPath $outcome.CopySarifTo
        New-Item -ItemType Directory -Force -Path $keep | Out-Null
        foreach ($f in $sarifFiles) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $keep $f.Name) -Force }
    }
    # F242: only a build that ran is kept, the three newest.
    if ($outcome.ExitCode -eq 0 -and -not $reused -and $key) {
        New-Item -ItemType Directory -Force -Path (Join-Path $cacheDir 'sarif') | Out-Null
        foreach ($f in $sarifFiles) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $cacheDir "sarif\$($f.Name)") -Force }
        foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter 'analyzers-*.txt' -File)) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $cacheDir $f.Name) -Force }
        Write-Json (Join-Path $cacheDir 'cache.json') ([pscustomobject]@{ Key = $key; Run = $Run; Date = (Get-Date).ToString('s'); SarifCount = $sarifFiles.Count; AnalyzerText = $analyzerText; BuildText = $buildText; Expected = $expected })
        $dirs = @(Get-ChildItem -LiteralPath $cacheRoot -Directory | Sort-Object LastWriteTimeUtc -Descending)
        for ($i = 3; $i -lt $dirs.Count; $i++) { Remove-Item -LiteralPath $dirs[$i].FullName -Recurse -Force -ErrorAction SilentlyContinue }
    }
    if ($reused) { Say "review: static - reused the build of run $($cache.Run) (same commit, changes, analyzers and build line)" }
    foreach ($line in @($outcome.Lines)) { Say $line }
    if ($outcome.ExitCode -eq 0 -and $kind -eq 'architecture' -and $null -ne $complexity) { Say (Format-PaperReviewComplexityLine -Complexity $complexity) }
    exit $outcome.ExitCode
}

# ------------------------------------------------------------------ check, report

# Every saved answer of the run: "<lane>-<b>" and "<lane>-<b>.2" -> its lines.
function Read-ReviewAnswers([string] $Dir) {
    $answers = @{}
    foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Dir 'lanes') -Filter '*.md' -File -ErrorAction SilentlyContinue)) {
        $answers[[IO.Path]::GetFileNameWithoutExtension($f.Name)] = Read-Lines $f.FullName
    }
    return $answers
}

# The records collab wrote beside the answers (F212): "bug-1" for lanes\bug-1.turn.json, "BUG-1" for verdicts\BUG-1.turn.json.
function Read-ReviewRecords([string] $Dir, [string] $Folder) {
    $records = @{}
    foreach ($f in @(Get-ChildItem -LiteralPath (Join-Path $Dir $Folder) -Filter '*.turn.json' -File -ErrorAction SilentlyContinue)) {
        try { $records[$f.Name.Substring(0, $f.Name.Length - '.turn.json'.Length)] = Read-Json $f.FullName } catch { }
    }
    return $records
}

function Invoke-ReviewCheck {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $c = Get-PaperReviewCheck -Plan $plan -Answers (Read-ReviewAnswers $dir) -Static (Read-ReviewStatic $dir) -VerifyCap $config.VerifyCap -Records (Read-ReviewRecords $dir 'lanes')
    Write-Json (Join-Path $dir 'findings.json') ([pscustomobject]@{ Findings = $c.Queue; LaneStates = $c.LaneStates; Seen = $c.Seen })
    if ($external) { Say "review: repository $root (external, read only) - verify readers read the code there" }
    foreach ($line in @($c.Lines)) { Say $line }
    exit $c.ExitCode
}

function Invoke-ReviewReport {
    $dir = Get-ReviewRunDir
    $plan = Read-Json (Join-Path $dir 'plan.json')
    $static = Read-ReviewStatic $dir
    $laneRecords = Read-ReviewRecords $dir 'lanes'
    $c = Get-PaperReviewCheck -Plan $plan -Answers (Read-ReviewAnswers $dir) -Static $static -VerifyCap $config.VerifyCap -Records $laneRecords
    $verdictLines = @()
    foreach ($v in @(Get-ChildItem -LiteralPath (Join-Path $dir 'verdicts') -Filter '*.md' -File -ErrorAction SilentlyContinue | Sort-Object Name)) { $verdictLines += , ([string[]] (Read-Lines $v.FullName)) }
    if ($external) { $reportDir = $reportsDir; $reportArg = $reportsDir }
    else { $reportDir = Get-ReviewFullPath $config.Report; $reportArg = $config.Report }
    $existing = @()
    if (Test-Path -LiteralPath $reportDir -PathType Container) { $existing = @(Get-ChildItem -LiteralPath $reportDir -File | ForEach-Object { $_.Name }) }
    $rep = New-PaperReviewReport -Plan $plan -Check $c -VerdictLines $verdictLines -Static $static -Run $Run -Date (Get-Date).ToString('yyyy-MM-dd') -ReportDir $reportArg -Existing $existing -Records $laneRecords -VerdictRecords (Read-ReviewRecords $dir 'verdicts') -ComplexityTop ([int] $config.ComplexityTop)
    Write-Text (Join-Path $reportDir $rep.Name) $rep.Text
    foreach ($line in @($rep.Lines)) { Say $line }
    exit 0
}

switch ($Command) {
    'init' { Invoke-ReviewInit }
    'lanes' { Invoke-ReviewLanes }
    'plan' { Invoke-ReviewPlan }
    'approve' { Invoke-ReviewApprove }
    'static' { Invoke-ReviewStatic }
    'files' { Invoke-ReviewFiles }
    'prompt' { Invoke-ReviewPrompt }
    'check' { Invoke-ReviewCheck }
    'report' { Invoke-ReviewReport }
    'sonar' { $sonarCode = @(Invoke-ReviewSonar); exit ([int] $sonarCode[-1]) }
}
