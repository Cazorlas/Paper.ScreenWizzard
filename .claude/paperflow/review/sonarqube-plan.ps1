# The optional self-hosted SonarQube layer of the architecture review, the pure part (plan 2026-10-05-qa-split, section M; SPEC F243-F248;
# ADR-0044): the pages of the Web API read into rows, the weight of an issue, the ranking of the files and the hot spots the lanes read, when
# the layer applies, when the server is started and stopped, the state of the analysis task, the lines it prints, and the access key hidden
# in every line. No I/O: sonarqube.ps1 talks to the server and the scanner and hands the answers to these. The caller dot-sources
# review-sarif-plan.ps1 (Sort-PaperReviewByKey) and review-plan.ps1 first; declares no param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperReviewSonarHotspots = 20
$script:PaperReviewSonarTimeoutSec = 600
$script:PaperReviewSonarPollSec = 5
$script:PaperReviewIssueWeights = @{ BLOCKER = 5; CRITICAL = 3; MAJOR = 2; MINOR = 1; INFO = 0 }
$script:PaperReviewImpactWeights = @{ HIGH = 3; MEDIUM = 2; LOW = 1 }
$script:PaperReviewSonarPageSize = 500
$script:PaperReviewSonarIssueLimit = 10000

function Get-PaperReviewSonarProp($Object, [string] $Name) {
    if ($null -eq $Object) { return $null }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function ConvertFrom-PaperReviewSonarConfig {
    <#
    .SYNOPSIS
    The review.sonarqube section of the profile (M.1): projectKey (a text; without it the layer is off), url (http or https; empty = the
    environment, then http://localhost:9000, decided by sonarqube.ps1), hotspots (1-500, default 20), timeoutSec (60-3600, default 600).
    A token is never a profile key. Errors are named by Prefix ("review.sonarqube").
    #>
    param($Section, [string] $Prefix = 'review.sonarqube')
    $cfg = [pscustomobject]@{ ProjectKey = ''; Url = ''; Hotspots = $script:PaperReviewSonarHotspots; TimeoutSec = $script:PaperReviewSonarTimeoutSec }
    $errors = @()
    if ($Section -isnot [System.Collections.IDictionary]) { return [pscustomobject]@{ Sonar = $cfg; Errors = @("${Prefix}: not an object") } }
    foreach ($key in @($Section.Keys)) {
        $v = $Section[$key]
        switch -CaseSensitive ($key) {
            'projectKey' { if ($v -isnot [string] -or -not $v.Trim()) { $errors += "${Prefix}.projectKey: not a text" } else { $cfg.ProjectKey = $v.Trim() } }
            'url' { if ($v -isnot [string] -or $v -notmatch '^https?://[^\s/]+') { $errors += "${Prefix}.url: not an http or https address" } else { $cfg.Url = $v.Trim().TrimEnd('/') } }
            'hotspots' { if (-not (Test-PaperReviewWholeRange $v 1 500)) { $errors += "${Prefix}.hotspots: not a whole number from 1 to 500" } else { $cfg.Hotspots = [int] $v } }
            'timeoutSec' { if (-not (Test-PaperReviewWholeRange $v 60 3600)) { $errors += "${Prefix}.timeoutSec: not a whole number from 60 to 3600" } else { $cfg.TimeoutSec = [int] $v } }
            default { $errors += "${Prefix}.${key}: unknown key (projectKey, url, hotspots, timeoutSec)" }
        }
    }
    return [pscustomobject]@{ Sonar = $cfg; Errors = $errors }
}

# ------------------------------------------------------------------ M.5 the pages of the Web API

function ConvertFrom-PaperReviewSonarMeasures {
    <#
    .SYNOPSIS
    The pages of /api/measures/component_tree (qualifiers FIL) to one row per file: Path, Cognitive, Complexity, DuplicatedDensity, Ncloc
    (a measure the file does not have is 0). Total is the number of files the server says it has.
    #>
    param($Pages)
    $files = @(); $seen = @{}
    $total = 0
    foreach ($page in @($Pages)) {
        if ($null -eq $page) { continue }
        $paging = Get-PaperReviewSonarProp $page 'paging'
        if ($null -ne $paging -and $null -ne (Get-PaperReviewSonarProp $paging 'total')) { $total = [long] (Get-PaperReviewSonarProp $paging 'total') }
        foreach ($c in @(Get-PaperReviewSonarProp $page 'components' | Where-Object { $null -ne $_ })) {
            $path = "$(Get-PaperReviewSonarProp $c 'path')"
            if (-not $path) { $k = "$(Get-PaperReviewSonarProp $c 'key')"; $cut = $k.IndexOf(':'); $path = $(if ($cut -ge 0) { $k.Substring($cut + 1) } else { $k }) }
            $path = $path.Replace('\', '/')
            if ($seen.ContainsKey($path.ToLowerInvariant())) { continue }
            $seen[$path.ToLowerInvariant()] = $true
            $m = @{}
            foreach ($x in @(Get-PaperReviewSonarProp $c 'measures' | Where-Object { $null -ne $_ })) { $m["$(Get-PaperReviewSonarProp $x 'metric')"] = "$(Get-PaperReviewSonarProp $x 'value')" }
            $num = { param($name) $v = 0.0; if ($m.ContainsKey($name) -and [double]::TryParse($m[$name], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref] $v)) { return $v }; return 0.0 }
            $files += [pscustomobject]@{
                Path = $path; Cognitive = [long] (& $num 'cognitive_complexity'); Complexity = [long] (& $num 'complexity')
                DuplicatedDensity = [double] (& $num 'duplicated_lines_density'); Ncloc = [long] (& $num 'ncloc')
            }
        }
    }
    if ($total -lt $files.Count) { $total = $files.Count }
    return [pscustomobject]@{ Files = $files; Total = $total }
}

function ConvertFrom-PaperReviewSonarIssues {
    <#
    .SYNOPSIS
    The pages of /api/issues/search to rows: Rule (without the repository: S3776), Severity (the old scale, '' when the issue has only
    impacts), Impacts, Path (without the project key), Line, Message, Type. IssuesRead is what the pages hold, IssuesTotal what the server
    says it has; Truncated when the server stopped giving at its limit of 10,000 (F243).
    #>
    param($Pages)
    $issues = @()
    $total = 0
    foreach ($page in @($Pages)) {
        if ($null -eq $page) { continue }
        $paging = Get-PaperReviewSonarProp $page 'paging'
        if ($null -ne $paging -and $null -ne (Get-PaperReviewSonarProp $paging 'total')) { $total = [long] (Get-PaperReviewSonarProp $paging 'total') }
        foreach ($i in @(Get-PaperReviewSonarProp $page 'issues' | Where-Object { $null -ne $_ })) {
            $rule = "$(Get-PaperReviewSonarProp $i 'rule')"
            $cut = $rule.IndexOf(':'); if ($cut -ge 0) { $rule = $rule.Substring($cut + 1) }
            $comp = "$(Get-PaperReviewSonarProp $i 'component')"
            $cut = $comp.IndexOf(':'); if ($cut -ge 0) { $comp = $comp.Substring($cut + 1) }
            $line = 0
            if ($null -ne (Get-PaperReviewSonarProp $i 'line')) { $line = [int] (Get-PaperReviewSonarProp $i 'line') }
            $issues += [pscustomobject]@{
                Rule = $rule; Severity = "$(Get-PaperReviewSonarProp $i 'severity')"; Impacts = @(Get-PaperReviewSonarProp $i 'impacts' | Where-Object { $null -ne $_ })
                Path = $comp.Replace('\', '/'); Line = $line; Message = "$(Get-PaperReviewSonarProp $i 'message')"; Type = "$(Get-PaperReviewSonarProp $i 'type')"
            }
        }
    }
    if ($total -lt $issues.Count) { $total = $issues.Count }
    return [pscustomobject]@{ Issues = $issues; IssuesRead = $issues.Count; IssuesTotal = $total; Truncated = ($issues.Count -lt $total) }
}

function Get-PaperReviewSonarIssuePageCount {
    # The pages of 500 to ask for: all of them, but the server gives no issue past the 10,000th.
    param([long] $Total)
    if ($Total -le 0) { return 1 }
    $pages = [int] [math]::Ceiling($Total / [double] $script:PaperReviewSonarPageSize)
    return [math]::Min($pages, [int] ($script:PaperReviewSonarIssueLimit / $script:PaperReviewSonarPageSize))
}

function Get-PaperReviewIssueWeight {
    <#
    .SYNOPSIS
    The weight of one issue (F248): its severity - blocker 5, critical 3, major 2, minor 1, info 0 - or, when it has only impacts, the highest
    impact - high 3, medium 2, low 1; nothing known is 0.
    #>
    param($Issue)
    $sev = "$(Get-PaperReviewSonarProp $Issue 'Severity')".ToUpperInvariant()
    if ($sev -and $script:PaperReviewIssueWeights.ContainsKey($sev)) { return [int] $script:PaperReviewIssueWeights[$sev] }
    $best = 0
    foreach ($imp in @(Get-PaperReviewSonarProp $Issue 'Impacts' | Where-Object { $null -ne $_ })) {
        $s = "$(Get-PaperReviewSonarProp $imp 'severity')".ToUpperInvariant()
        if ($s -and $script:PaperReviewImpactWeights.ContainsKey($s) -and [int] $script:PaperReviewImpactWeights[$s] -gt $best) { $best = [int] $script:PaperReviewImpactWeights[$s] }
    }
    return $best
}

function Get-PaperReviewHotspots {
    <#
    .SYNOPSIS
    The files of the scope ranked for the lanes (F248): cognitive complexity descending, then the weighted issues, then the duplicated lines
    (round(density x ncloc / 100)), then the path. ScopePaths: the folders or files of the scope, none = the whole project; a file outside it
    is left out before the ranking. Ranked is every file in scope; Top the first -Top of them; InScope how many.
    #>
    param($Files, $Issues, [string[]] $ScopePaths, [int] $Top)
    $scope = @(@($ScopePaths) | Where-Object { $_ } | ForEach-Object { (ConvertTo-PaperReviewRelPath $_).ToLowerInvariant() } | Where-Object { $_ })
    $weights = @{}; $counts = @{}
    foreach ($i in @($Issues | Where-Object { $null -ne $_ })) {
        $k = "$($i.Path)".ToLowerInvariant()
        if (-not $weights.ContainsKey($k)) { $weights[$k] = 0; $counts[$k] = 0 }
        $weights[$k] += (Get-PaperReviewIssueWeight -Issue $i)
        $counts[$k]++
    }
    $rows = @()
    foreach ($f in @($Files | Where-Object { $null -ne $_ })) {
        $lp = "$($f.Path)".ToLowerInvariant()
        if ($scope.Count -gt 0 -and @($scope | Where-Object { $lp -eq $_ -or $lp.StartsWith("$_/") }).Count -eq 0) { continue }
        $w = 0; $n = 0
        if ($weights.ContainsKey($lp)) { $w = [int] $weights[$lp]; $n = [int] $counts[$lp] }
        $dup = [long] [math]::Round([double] $f.DuplicatedDensity * [double] $f.Ncloc / 100.0, [MidpointRounding]::AwayFromZero)
        $rows += [pscustomobject]@{
            Path = "$($f.Path)"; Cognitive = [long] $f.Cognitive; Complexity = [long] $f.Complexity; DuplicatedDensity = [double] $f.DuplicatedDensity; Ncloc = [long] $f.Ncloc
            Weighted = $w; IssueCount = $n; DuplicatedLines = $dup
        }
    }
    $ranked = Sort-PaperReviewByKey @($rows) { param($r) "$((1000000000000 - [long] $r.Cognitive).ToString('D13'))`t$((1000000000 - [long] $r.Weighted).ToString('D10'))`t$((1000000000 - [long] $r.DuplicatedLines).ToString('D10'))`t$($r.Path)" }
    $ranked = @($ranked)
    $take = [math]::Min([math]::Max($Top, 0), $ranked.Count)
    return [pscustomobject]@{ Ranked = $ranked; Top = @($ranked | Select-Object -First $take); InScope = $ranked.Count }
}

# ------------------------------------------------------------------ M.2, M.3, M.4 when, with which server, how far

function Get-PaperReviewSonarApplicability {
    <#
    .SYNOPSIS
    Whether the SonarQube layer applies to this run (M.2, F245): Enabled when the profile declares a project key; Applies when nothing is in
    the way, else Line says the first thing in the way - an external repository, a branch scope (Community keeps one branch per project), a
    files scope, a branch that is not the base, uncommitted work (the analysis would publish it as the base), a scanner folder git does not
    ignore, no access key, no scanner.
    #>
    param([string] $ProjectKey, [bool] $External, [string] $Mode, [string] $Branch, [string] $BaseBranch, [int] $Dirty, [bool] $Ignored, [bool] $TokenSet, [bool] $ScannerFound)
    $na = { param($enabled, $why) [pscustomobject]@{ Enabled = $enabled; Applies = $false; Line = "review: sonarqube - not applicable: $why" } }
    if (-not "$ProjectKey".Trim()) { return & $na $false 'no review.sonarqube.projectKey in the profile' }
    if ($External) { return & $na $true 'external repository (read only)' }
    if ($Mode -eq 'branch') { return & $na $true 'branch scope - SonarQube Community keeps one branch per project' }
    if ($Mode -eq 'files') { return & $na $true 'files scope - SonarQube reads the whole project: name the project or a folder' }
    if ($BaseBranch -and $Branch -ne $BaseBranch) { return & $na $true "branch $Branch is not the base branch $BaseBranch" }
    if ($Dirty -gt 0) { return & $na $true "uncommitted work - the analysis would publish it as $BaseBranch" }
    if (-not $Ignored) { return & $na $true '.sonarqube/ is not ignored by git - add it to .gitignore' }
    if (-not $TokenSet) { return & $na $true 'PAPER_SONARQUBE_TOKEN is not set' }
    if (-not $ScannerFound) { return & $na $true 'dotnet-sonarscanner not on PATH' }
    return [pscustomobject]@{ Enabled = $true; Applies = $true; Line = '' }
}

function Get-PaperReviewSonarStartDecision {
    <#
    .SYNOPSIS
    What to do about the server (M.3, F244): use it when it says UP; else start it from the unzipped folder (StopAfter unless the machine keeps
    it running) when the address is this machine's and the folder has StartSonar.bat; else not verifiable, naming what it said.
    Action use | start | fail.
    #>
    param([string] $Url, [string] $Status, [string] $StatusError, [string] $SonarHome, [bool] $HomeHasStart, [bool] $Keep, [int] $TimeoutSec)
    $res = { param($action, $stop, $line) [pscustomobject]@{ Action = $action; StopAfter = $stop; Line = $line } }
    if ("$Status".ToUpperInvariant() -eq 'UP') { return & $res 'use' $false '' }
    $isLocal = $false
    try { $h = ([Uri] $Url).Host.ToLowerInvariant(); $isLocal = (@('localhost', '127.0.0.1', '::1', '[::1]') -contains $h) } catch { $isLocal = $false }
    if ($isLocal -and $SonarHome -and $HomeHasStart) {
        return & $res 'start' (-not $Keep) "review: sonarqube - starting $SonarHome (waiting for UP, at most $TimeoutSec s)"
    }
    $why = if ($StatusError) { $StatusError } elseif ($Status) { "status $Status" } else { 'no answer' }
    return & $res 'fail' $false "review: sonarqube not verifiable - $Url does not answer ($why)"
}

function Format-PaperReviewSonarTimeoutLine { param([int] $TimeoutSec) return "review: sonarqube not verifiable - the server did not come up in $TimeoutSec s" }
function Format-PaperReviewSonarStoppedLine { return 'review: sonarqube - stopped (this run started it)' }

function Get-PaperReviewSonarTaskState {
    <#
    .SYNOPSIS
    The state of the analysis task on the server (M.4, F247): SUCCESS - read; FAILED or CANCELED - not verifiable, and no old result of the
    server stands in; anything else - wait, until the time is up.
    #>
    param([string] $Status, [int] $ElapsedSec, [int] $TimeoutSec)
    $st = "$Status".ToUpperInvariant()
    if ($st -eq 'SUCCESS') { return [pscustomobject]@{ State = 'success'; Line = '' } }
    if ($st -eq 'FAILED' -or $st -eq 'CANCELED') { return [pscustomobject]@{ State = 'fail'; Line = "review: sonarqube not verifiable - analysis $st on the server" } }
    if ($ElapsedSec -ge $TimeoutSec) { return [pscustomobject]@{ State = 'timeout'; Line = "review: sonarqube not verifiable - analysis still $st after $TimeoutSec s" } }
    return [pscustomobject]@{ State = 'wait'; Line = '' }
}

# ------------------------------------------------------------------ the lines, and the key hidden

function Get-PaperReviewSonarSha7([string] $Head) {
    $h = "$Head".Trim()
    if ($h.Length -gt 7) { return $h.Substring(0, 7) }
    return $h
}

function Format-PaperReviewSonarReuseLine { param([string] $Head) return "review: sonarqube - the server already has commit $(Get-PaperReviewSonarSha7 $Head) - no scan" }

function Format-PaperReviewSonarEstimateLine {
    param([string] $Head, [string] $Url, [string] $ProjectKey, [int] $Read, [int] $Total)
    return "review: sonarqube - analysis of $(Get-PaperReviewSonarSha7 $Head) read from $Url (project $ProjectKey); hotspots: the lanes read $Read of $Total files in scope"
}

function Format-PaperReviewSonarNoAnalysisLine {
    param([string] $Head)
    return "review: sonarqube - no analysis of $(Get-PaperReviewSonarSha7 $Head) read yet: run review.ps1 sonar first for hotspots; this plan reads every file"
}

function Protect-PaperReviewSecret {
    <#
    .SYNOPSIS
    Every line with the access key replaced by *** (F246): everything the layer saves or prints goes through this. No key to hide: the lines.
    #>
    param([string[]] $Lines, [string] $Secret)
    $out = @()
    foreach ($l in @($Lines)) {
        $t = "$l"
        if ($Secret) { $t = $t.Replace($Secret, '***') }
        $out += $t
    }
    return $out
}

function Format-PaperReviewSonarSection {
    <#
    .SYNOPSIS
    The "## SonarQube" section of the architecture report (M.7): the analysis the lanes were narrowed by, and the ranked files they read -
    cognitive and cyclomatic complexity, duplicated lines, lines of code, and the open issues with their weight. Sonar is plan.json's Sonar.
    #>
    param($Sonar, [string] $ReportDir, [string] $RepoRoot = '')
    $out = @('## SonarQube', '')
    $line = "Analysis of $(Get-PaperReviewSonarSha7 $Sonar.Head) read from $($Sonar.Url) (project $($Sonar.ProjectKey)); the lanes read the top $($Sonar.Top) of $($Sonar.InScope) files in scope."
    if ([long] $Sonar.IssuesTotal -gt [long] $Sonar.IssuesRead) { $line += " Issues: read $($Sonar.IssuesRead) of $($Sonar.IssuesTotal) (the server gives at most 10,000)." }
    $out += $line
    $out += @('', '| # | File | Cognitive | Cyclomatic | Duplicated lines | Lines | Issues (weighted) |', '| --- | --- | --- | --- | --- | --- | --- |')
    $i = 0
    foreach ($f in @($Sonar.Files | Where-Object { $null -ne $_ })) {
        $i++
        $out += "| $i | [$($f.Path)]($(Get-PaperReviewReportLink $ReportDir $f.Path $RepoRoot)) | $($f.Cognitive) | $($f.Complexity) | $($f.DuplicatedLines) | $($f.Ncloc) | $($f.IssueCount) ($($f.Weighted)) |"
    }
    return $out
}