# The optional self-hosted SonarQube layer of the architecture review, the I/O part (plan 2026-10-05-qa-split, section M; SPEC F243-F248; ADR-0044):
# `review.ps1 sonar` talks to the server (a Web API call with the access key in an Authorization header), starts and stops the unzipped server
# when the machine declares one, runs the scanner - dotnet-sonarscanner around a build that never deploys when the repository has .NET code, else
# the SonarScanner CLI with no build (F249-F251) - waits for the analysis, reads the numbers of the files and
# the open issues, and saves them under the run folder of the project; `plan` then reads only the hot spots. Every decision is in
# sonarqube-plan.ps1 (pure, tested); this file only reads and starts things. Dot-sourced by review.ps1, which defines what it uses ($root,
# $config, $runsDir, $paperflow, Say, Write-Text, Write-Json, Invoke-Native, Invoke-ReviewGit, Invoke-ReviewWithEnvironment, Select-ReviewScope).
#
# The access key (PAPER_SONARQUBE_TOKEN) goes only into the environment of the scanner process (SONAR_TOKEN) and the Authorization header of a
# call; nothing is saved or printed without Protect-PaperReviewSecret first. The machine's own settings are environment variables of the user:
# PAPER_SONARQUBE_TOKEN (needed), PAPER_SONARQUBE_URL (default http://localhost:9000), PAPER_SONARQUBE_HOME (the unzipped folder),
# PAPER_SONARQUBE_JAVA (java.exe, or the folder of a JRE: it starts the server and is the JAVA_HOME of the CLI), PAPER_SONARQUBE_KEEP=1 (do not
# stop a server this run started), PAPER_SONARQUBE_SCANNER (the SonarScanner CLI: sonar-scanner.bat or its unzipped folder; else sonar-scanner on PATH).
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Get-ReviewSonarSettings {
    $url = "$($config.Sonar.Url)"
    if (-not $url) { $url = "$env:PAPER_SONARQUBE_URL" }
    if (-not $url) { $url = 'http://localhost:9000' }
    return [pscustomobject]@{
        Url = $url.Trim().TrimEnd('/'); Token = "$env:PAPER_SONARQUBE_TOKEN"; Home = "$env:PAPER_SONARQUBE_HOME"; Java = "$env:PAPER_SONARQUBE_JAVA"
        Keep = ("$env:PAPER_SONARQUBE_KEEP" -eq '1'); ProjectKey = "$($config.Sonar.ProjectKey)"; ScannerSetting = "$($config.Sonar.Scanner)"
        CliVariable = "$env:PAPER_SONARQUBE_SCANNER"
    }
}

# One GET of the Web API. Ok with the parsed Json, or not Ok with the Error (a status code or the exception text).
function Invoke-ReviewSonarRest([string] $Url, [string] $PathAndQuery, [string] $Token) {
    try {
        $req = [System.Net.HttpWebRequest]::Create($Url.TrimEnd('/') + $PathAndQuery)
        $req.Method = 'GET'; $req.Timeout = 10000; $req.ReadWriteTimeout = 10000; $req.Proxy = $null; $req.Accept = 'application/json'
        if ($Token) { $req.Headers.Add('Authorization', "Bearer $Token") }
        $resp = $req.GetResponse()
        try {
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
            $text = $reader.ReadToEnd()
        }
        finally { $resp.Close() }
        return [pscustomobject]@{ Ok = $true; Json = ($text | ConvertFrom-Json); Error = '' }
    }
    catch [System.Net.WebException] {
        $code = ''
        if ($null -ne $_.Exception.Response) { $code = "HTTP $([int] $_.Exception.Response.StatusCode)" }
        $msg = if ($code) { $code } else { "$($_.Exception.Message)" }
        return [pscustomobject]@{ Ok = $false; Json = $null; Error = $msg }
    }
    catch { return [pscustomobject]@{ Ok = $false; Json = $null; Error = "$($_.Exception.Message)" } }
}

# What git says about the project, for the question "does the layer apply": the branch, the base branch (origin/HEAD, origin/main, main,
# master - the first this machine has), the uncommitted work, whether .sonarqube/ is ignored, and the commit.
function Get-ReviewSonarGit {
    $first = { param($r) if ($r.Code -eq 0 -and @($r.Lines).Count -gt 0) { "$(@($r.Lines)[0])".Trim() } else { '' } }
    $verify = { param($ref) (Invoke-ReviewGit -C $root rev-parse --verify -q "$ref^{commit}").Code -eq 0 }
    $branch = & $first (Invoke-ReviewGit -C $root rev-parse --abbrev-ref HEAD)
    $head = & $first (Invoke-ReviewGit -C $root rev-parse HEAD)
    $found = @(@('origin/HEAD', 'origin/main', 'main', 'master') | Where-Object { & $verify $_ })
    $originHead = & $first (Invoke-ReviewGit -C $root symbolic-ref -q --short refs/remotes/origin/HEAD)
    $b = Select-PaperReviewExternalBase -Given '' -GivenFound $false -OriginHead $originHead -Found $found
    $base = ''
    if (-not $b.Error) { $base = "$($b.Ref)" -replace '^origin/', '' }
    $st = Invoke-ReviewGit -C $root status --porcelain --untracked-files=all
    $dirty = 0
    if ($st.Code -eq 0) { $dirty = Get-PaperReviewDirtyCount -StatusLines $st.Lines -ReportDir $config.Report -SarifDir $config.SarifDir }
    # A folder that does not exist yet is ignored by `.sonarqube/` only through a path inside it.
    $ignored = ((Invoke-ReviewGit -C $root check-ignore -q '.sonarqube/out/.sonar/report-task.txt').Code -eq 0)
    return [pscustomobject]@{ Branch = $branch; Head = $head; BaseBranch = $base; Dirty = $dirty; Ignored = $ignored }
}

# Does the layer apply, with which scanner (F249): .NET code in the repository (a project file or a C#/VB source) -> dotnet-sonarscanner, else the
# SonarScanner CLI, unless review.sonarqube.scanner says. The CLI: where it is (F250), the folders it analyses and the tests (F251), checked here so
# plan and sonar give the same answer. AllPaths: the files of the repository when the caller has them (plan), else asked of git.
function Get-ReviewSonarApplicabilityNow($Selection, [string[]] $AllPaths = $null) {
    $s = Get-ReviewSonarSettings
    $g = Get-ReviewSonarGit
    $paths = $AllPaths
    if ($null -eq $paths) { $listing = Get-ReviewRepoFiles; $paths = @($listing.Tracked) + @($listing.Untracked) }
    $sc = Get-PaperReviewSonarScanner -Setting $s.ScannerSetting -HasDotnetCode (Test-PaperReviewDotnetCode -Paths $paths)
    $cliPath = ''; $cliProblem = ''; $found = $false; $srcs = @(); $tsts = @(); $srcProblem = ''
    if ($sc.Scanner -eq 'cli') {
        $var = $s.CliVariable.Trim()
        $isFile = $false; $hasBin = $false
        if ($var) {
            $isFile = (Test-Path -LiteralPath $var -PathType Leaf)
            $hasBin = (-not $isFile) -and (Test-Path -LiteralPath (Join-Path $var 'bin\sonar-scanner.bat') -PathType Leaf)
        }
        $hit = ''
        $cmd = @(Get-Command sonar-scanner -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1)
        if ($cmd.Count -gt 0) { $hit = "$($cmd[0].Source)" }
        $cr = Resolve-PaperReviewSonarCli -Variable $var -VariableIsFile $isFile -VariableHasBinBat $hasBin -PathHit $hit
        $cliPath = "$($cr.Path)"; $cliProblem = "$($cr.Problem)"; $found = (-not $cliProblem)
        $declared = @($config.Sonar.Sources) + @($config.Sonar.Tests)
        $existing = @(@('src', 'tests', 'test') + $declared | Where-Object { $_ -and $_ -ne '.' } | Select-Object -Unique | Where-Object { Test-Path -LiteralPath (Join-Path $root ($_.Replace('/', '\'))) -PathType Container })
        $rs = Resolve-PaperReviewSonarSources -Sources @($config.Sonar.Sources) -Tests @($config.Sonar.Tests) -Existing $existing
        $srcProblem = "$($rs.Problem)"; $srcs = @($rs.Sources); $tsts = @($rs.Tests)
    }
    else { $found = [bool] (Get-Command dotnet-sonarscanner -CommandType Application -ErrorAction SilentlyContinue) }
    $a = Get-PaperReviewSonarApplicability -ProjectKey $s.ProjectKey -External $external -Mode "$($Selection.Mode)" -Branch $g.Branch -BaseBranch $g.BaseBranch -Dirty $g.Dirty `
        -Ignored $g.Ignored -TokenSet ([bool] $s.Token) -ScannerFound $found -Scanner $sc.Scanner -ScannerProblem $sc.Problem -CliProblem $cliProblem -SourcesProblem $srcProblem
    return [pscustomobject]@{ Result = $a; Settings = $s; Git = $g; Scanner = $sc; CliPath = $cliPath; Sources = $srcs; Tests = $tsts }
}

function Get-ReviewSonarResultFile([string] $ProjectKey, [string] $Head) {
    $name = ($ProjectKey -replace '[^A-Za-z0-9._-]', '-') + '-' + $Head.Substring(0, [math]::Min(12, $Head.Length)) + '.json'
    return (Join-Path (Join-Path $runsDir 'sonarqube') $name)
}

function Read-ReviewSonarResult([string] $ProjectKey, [string] $Head) {
    if (-not $ProjectKey -or -not $Head) { return $null }
    $file = Get-ReviewSonarResultFile $ProjectKey $Head
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $null }
    try { $r = Read-Json $file } catch { return $null }
    if ("$($r.Head)" -ne $Head) { return $null }
    return $r
}

# The hot spots of a saved result for a scope: the scope paths of a folder scope, none for the project.
function Get-ReviewSonarHotspots($Result, $Selection) {
    $scope = @()
    if ("$($Selection.Mode)" -eq 'path' -and "$($Selection.Folder)") { $scope = @("$($Selection.Folder)") }
    return (Get-PaperReviewHotspots -Files @($Result.Files) -Issues @($Result.Issues) -ScopePaths $scope -Top ([int] $config.Sonar.Hotspots))
}

# plan and lanes (M.7): the lanes of the architecture review read only the hot spots when an analysis of this commit is saved. Lines say so (and
# which files are left out and why); Allowed is the set of paths the architecture and smell lanes keep, $null when they keep all.
function Get-ReviewSonarForPlan($Selection, [string[]] $AllPaths = $null, $ScopeFiles = @()) {
    $none = [pscustomobject]@{ Lines = @(); Allowed = $null; Plan = $null; Measured = $null }
    if ($external -or -not "$($config.Sonar.ProjectKey)") { return $none }
    $now = Get-ReviewSonarApplicabilityNow $Selection $AllPaths
    if (-not $now.Result.Applies) { return $none }
    $res = Read-ReviewSonarResult $now.Settings.ProjectKey $now.Git.Head
    if ($null -eq $res) { return [pscustomobject]@{ Lines = @(Format-PaperReviewSonarNoAnalysisLine -Head $now.Git.Head); Allowed = $null; Plan = $null; Measured = $null } }
    $h = Get-ReviewSonarHotspots $res $Selection
    $allowed = @{}
    foreach ($row in @($h.Top)) { $allowed["$($row.Path)".ToLowerInvariant()] = $true }
    $take = @($h.Top).Count
    $line = Format-PaperReviewSonarEstimateLine -Head $now.Git.Head -Url $now.Settings.Url -ProjectKey $now.Settings.ProjectKey -Read $take -Total ([int] $h.InScope)
    # F252: the complexity table from the issues of the server, kept in plan.json for the report of a repository whose static lane does not run.
    $cxResults = @(ConvertTo-PaperReviewSonarComplexityResults -Issues @($res.Issues) -RepoRoot $root)
    $lineMap = @{}
    foreach ($i in @($res.Issues | Where-Object { $script:PaperReviewComplexityRules -ccontains "$($_.Rule)" })) {
        $full = Get-ReviewFullPath "$($i.Path)"
        if (-not $lineMap.ContainsKey("$($i.Path)") -and (Test-Path -LiteralPath $full -PathType Leaf)) { try { $lineMap["$($i.Path)"] = [string[]] [IO.File]::ReadAllLines($full) } catch { } }
    }
    $scopePaths = @(@($ScopeFiles) | ForEach-Object { "$($_.Path)" })
    $complexity = Get-PaperReviewComplexity -Results $cxResults -RepoRoot $root -ScopePaths $scopePaths -ScopeMode "$($Selection.Mode)" -Lines $lineMap
    $planObj = [pscustomobject]@{
        Head = $now.Git.Head; Url = $now.Settings.Url; ProjectKey = $now.Settings.ProjectKey; Top = $take; InScope = [int] $h.InScope
        IssuesRead = [long] $res.IssuesRead; IssuesTotal = [long] $res.IssuesTotal; Files = @($h.Top); Complexity = $complexity
    }
    return [pscustomobject]@{ Lines = @($line); Allowed = $allowed; Plan = $planObj; TopCount = $take; Measured = $h.Measured }
}

function Stop-ReviewSonarServer([string] $SonarHome) {
    $bat = Join-Path $SonarHome 'bin\windows-x86-64\StopSonar.bat'
    if (Test-Path -LiteralPath $bat -PathType Leaf) {
        $p = Start-Process -FilePath $env:ComSpec -ArgumentList @('/c', "`"$bat`"") -WindowStyle Hidden -PassThru
        [void] $p.WaitForExit(60000)
    }
}

# `review.ps1 sonar -Kind architecture [scope]` (M.2-M.5). Exit 0 results saved, 4 not verifiable, 5 not applicable.
function Invoke-ReviewSonar {
    $sel = Select-ReviewScope
    $now = Get-ReviewSonarApplicabilityNow $sel
    $s = $now.Settings
    if (-not $now.Result.Applies) { Say $now.Result.Line; exit 5 }
    $head = $now.Git.Head
    $secret = $s.Token
    $say = { param([string] $line) foreach ($l in @(Protect-PaperReviewSecret -Lines @($line) -Secret $secret)) { Say $l } }
    # F249, F251: which scanner, and why - before the first question to the server.
    $isCli = ($now.Scanner.Scanner -eq 'cli')
    & $say (Format-PaperReviewSonarScannerLine -Scanner $now.Scanner.Scanner -Why $now.Scanner.Why -Sources $now.Sources -Tests $now.Tests -Exclusions @($config.Sonar.Exclusions))
    if (-not $isCli -and (@($config.Sonar.Sources).Count + @($config.Sonar.Tests).Count + @($config.Sonar.Exclusions).Count) -gt 0) { & $say (Format-PaperReviewSonarCliKeysNote) }
    $code = 0
    $started = $false
    # The folder of the results is inside the runs folder, which git is told to ignore.
    Initialize-ReviewRunsDir
    $sqDir = Join-Path $runsDir 'sonarqube'
    try {
        # --- the server (M.3)
        $st = Invoke-ReviewSonarRest $s.Url '/api/system/status' $s.Token
        $status = ''
        if ($st.Ok) { $status = "$($st.Json.status)" }
        $homeStart = ($s.Home -and (Test-Path -LiteralPath (Join-Path $s.Home 'bin\windows-x86-64\StartSonar.bat') -PathType Leaf))
        $dec = Get-PaperReviewSonarStartDecision -Url $s.Url -Status $status -StatusError $st.Error -SonarHome $s.Home -HomeHasStart $homeStart -Keep $s.Keep -TimeoutSec ([int] $config.Sonar.TimeoutSec)
        if ($dec.Action -eq 'fail') { & $say $dec.Line; return 4 }
        if ($dec.Action -eq 'start') {
            & $say $dec.Line
            $javaExe = ''
            if ($s.Java) { $javaExe = $(if ($s.Java -match '\.exe$') { $s.Java } else { Join-Path $s.Java 'bin\java.exe' }) }
            $savedJava = $env:SONAR_JAVA_PATH
            try {
                if ($javaExe) { $env:SONAR_JAVA_PATH = $javaExe }
                [void] (Start-Process -FilePath $env:ComSpec -ArgumentList @('/c', "`"$(Join-Path $s.Home 'bin\windows-x86-64\StartSonar.bat')`"") -WindowStyle Hidden -PassThru)
            }
            finally { $env:SONAR_JAVA_PATH = $savedJava }
            $started = $true
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $up = $false
            while ($clock.Elapsed.TotalSeconds -lt [int] $config.Sonar.TimeoutSec) {
                Start-Sleep -Seconds 2
                $again = Invoke-ReviewSonarRest $s.Url '/api/system/status' $s.Token
                if ($again.Ok -and "$($again.Json.status)" -eq 'UP') { $up = $true; break }
            }
            if (-not $up) { & $say (Format-PaperReviewSonarTimeoutLine -TimeoutSec ([int] $config.Sonar.TimeoutSec)); return 4 }
        }
        $key = [Uri]::EscapeDataString($s.ProjectKey)
        # --- the latest analysis of the project (M.4)
        $an = Invoke-ReviewSonarRest $s.Url "/api/project_analyses/search?project=$key&ps=1" $s.Token
        $revision = ''
        if ($an.Ok -and @($an.Json.analyses).Count -gt 0) { $revision = "$(@($an.Json.analyses)[0].revision)" }
        if ($revision -and $revision -ieq $head) { & $say (Format-PaperReviewSonarReuseLine -Head $head) }
        else {
            New-Item -ItemType Directory -Force -Path $sqDir | Out-Null
            $scanLog = Join-Path $sqDir "scan-$($head.Substring(0, 12)).log"
            $logLines = @()
            $scanEnv = @{ SONAR_TOKEN = $s.Token }
            $savedCwd = (Get-Location).Path
            # F251: the CLI keeps its work folder inside the run folder (git ignores it) and no build runs; an old report-task.txt is never taken
            # for this scan's. dotnet-sonarscanner keeps its own under .sonarqube.
            $reportTaskRel = '.sonarqube/out/.sonar/report-task.txt'
            if ($isCli) { $reportTaskRel = "$($script:PaperReviewSonarCliWorkDir)/report-task.txt" }
            $reportTask = Join-Path $root ($reportTaskRel.Replace('/', '\'))
            if ($isCli) { Remove-Item -LiteralPath $reportTask -Force -ErrorAction SilentlyContinue }
            Set-Location -LiteralPath $root
            try {
                if ($isCli) {
                    $cliArgs = Get-PaperReviewSonarCliArgs -ProjectKey $s.ProjectKey -Url $s.Url -Sources $now.Sources -Tests $now.Tests -Exclusions @($config.Sonar.Exclusions) -WorkDir $script:PaperReviewSonarCliWorkDir
                    $cliEnv = @{ SONAR_TOKEN = $s.Token }
                    $javaHome = Get-PaperReviewSonarJavaHome -Java $s.Java
                    if ($javaHome) { $cliEnv['JAVA_HOME'] = $javaHome }
                    $cliPath = $now.CliPath
                    $c = Invoke-ReviewWithEnvironment $cliEnv { Invoke-Native $cliPath $cliArgs }
                    $logLines += @($c.Lines)
                    Write-Text $scanLog ((@(Protect-PaperReviewSecret -Lines $logLines -Secret $secret) -join "`r`n") + "`r`n")
                    if ($c.Code -ne 0) {
                        & $say (Format-PaperReviewSonarCliFailedLine -Code $c.Code)
                        foreach ($l in @($c.Lines | Select-Object -Last 5)) { & $say "  $l" }
                        return 4
                    }
                }
                else {
                    $b = Invoke-ReviewWithEnvironment $scanEnv { Invoke-Native 'dotnet-sonarscanner' @('begin', "/k:$($s.ProjectKey)", "/d:sonar.host.url=$($s.Url)") }
                    $logLines += @($b.Lines)
                    if ($b.Code -ne 0) {
                        Write-Text $scanLog ((@(Protect-PaperReviewSecret -Lines $logLines -Secret $secret) -join "`r`n") + "`r`n")
                        & $say "review: sonarqube not verifiable - the scanner could not begin (exit $($b.Code))"
                        foreach ($l in @($b.Lines | Select-Object -Last 5)) { & $say "  $l" }
                        return 4
                    }
                    # The build of the project, as the static lane runs it but without the kit's .targets: it never deploys.
                    $buildEnv = @{ PaperDeployDebug = 'false'; MSBUILDDISABLENODEREUSE = '1'; CustomAfterMicrosoftCommonTargets = '' }
                    $bd = Invoke-ReviewWithEnvironment $buildEnv { Invoke-Native $script:PowerShellExe @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $paperflow 'paperflow.ps1'), 'build', '-Repo', $root, '-Full') }
                    $logLines += @($bd.Lines)
                    if ($bd.Code -ne 0) {
                        Write-Text $scanLog ((@(Protect-PaperReviewSecret -Lines $logLines -Secret $secret) -join "`r`n") + "`r`n")
                        & $say "review: sonarqube not verifiable - build failed (exit $($bd.Code))"
                        foreach ($l in @($bd.Lines | Select-Object -Last 5)) { & $say "  $l" }
                        return 4
                    }
                    $e = Invoke-ReviewWithEnvironment $scanEnv { Invoke-Native 'dotnet-sonarscanner' @('end') }
                    $logLines += @($e.Lines)
                    Write-Text $scanLog ((@(Protect-PaperReviewSecret -Lines $logLines -Secret $secret) -join "`r`n") + "`r`n")
                    if ($e.Code -ne 0) {
                        & $say "review: sonarqube not verifiable - the scanner could not end (exit $($e.Code))"
                        foreach ($l in @($e.Lines | Select-Object -Last 5)) { & $say "  $l" }
                        return 4
                    }
                }
            }
            finally { Set-Location -LiteralPath $savedCwd }
            # --- wait for the server to finish the analysis
            $ceId = ''
            if (Test-Path -LiteralPath $reportTask -PathType Leaf) {
                $m = [regex]::Match([IO.File]::ReadAllText($reportTask), '(?m)^ceTaskId=(\S+)')
                if ($m.Success) { $ceId = $m.Groups[1].Value }
            }
            if (-not $ceId) {
                & $say $(if ($isCli) { Format-PaperReviewSonarNoTaskLine -Path $reportTaskRel } else { "review: sonarqube not verifiable - the scanner left no ceTaskId in $reportTaskRel" })
                return 4
            }
            $clock = [Diagnostics.Stopwatch]::StartNew()
            while ($true) {
                $ce = Invoke-ReviewSonarRest $s.Url "/api/ce/task?id=$([Uri]::EscapeDataString($ceId))" $s.Token
                $ceStatus = ''
                if ($ce.Ok) { $ceStatus = "$($ce.Json.task.status)" }
                $ts = Get-PaperReviewSonarTaskState -Status $ceStatus -ElapsedSec ([int] $clock.Elapsed.TotalSeconds) -TimeoutSec ([int] $config.Sonar.TimeoutSec)
                if ($ts.State -eq 'success') { break }
                if ($ts.State -ne 'wait') { & $say $ts.Line; return 4 }
                Start-Sleep -Seconds $script:PaperReviewSonarPollSec
            }
        }
        # --- read the numbers of the files and the open issues (M.5)
        $measures = @(); $mTotal = 1; $page = 1
        while ($page -le $mTotal) {
            $mr = Invoke-ReviewSonarRest $s.Url "/api/measures/component_tree?component=$key&metricKeys=cognitive_complexity,complexity,duplicated_lines_density,ncloc&qualifiers=FIL&ps=500&p=$page" $s.Token
            if (-not $mr.Ok) { & $say "review: sonarqube not verifiable - the measures could not be read ($($mr.Error))"; return 4 }
            $measures += , $mr.Json
            $mTotal = [int] [math]::Ceiling([double] $mr.Json.paging.total / 500.0)
            $page++
        }
        $issuePages = @(); $iPages = 1; $page = 1
        while ($page -le $iPages) {
            $ir = Invoke-ReviewSonarRest $s.Url "/api/issues/search?componentKeys=$key&resolved=false&ps=500&p=$page" $s.Token
            if (-not $ir.Ok) { & $say "review: sonarqube not verifiable - the issues could not be read ($($ir.Error))"; return 4 }
            $issuePages += , $ir.Json
            if ($page -eq 1) { $iPages = Get-PaperReviewSonarIssuePageCount -Total ([long] $ir.Json.paging.total) }
            $page++
        }
        $mm = ConvertFrom-PaperReviewSonarMeasures -Pages $measures
        $ii = ConvertFrom-PaperReviewSonarIssues -Pages $issuePages
        New-Item -ItemType Directory -Force -Path $sqDir | Out-Null
        $result = [pscustomobject]@{
            Key = $s.ProjectKey; Head = $head; Url = $s.Url; Date = (Get-Date).ToString('s'); Files = @($mm.Files); Issues = @($ii.Issues)
            IssuesRead = $ii.IssuesRead; IssuesTotal = $ii.IssuesTotal
        }
        Write-Text (Get-ReviewSonarResultFile $s.ProjectKey $head) ((Protect-PaperReviewSecret -Lines @($result | ConvertTo-Json -Depth 8) -Secret $secret) -join "`n")
        $h = Get-ReviewSonarHotspots $result $sel
        & $say (Format-PaperReviewSonarEstimateLine -Head $head -Url $s.Url -ProjectKey $s.ProjectKey -Read @($h.Top).Count -Total ([int] $h.InScope))
        if ($ii.Truncated) { & $say "review: sonarqube - read $($ii.IssuesRead) of $($ii.IssuesTotal) issues (the server gives at most 10,000)" }
    }
    finally {
        if ($started -and -not $s.Keep) {
            Stop-ReviewSonarServer $s.Home
            & $say (Format-PaperReviewSonarStoppedLine)
        }
    }
    return $code
}
