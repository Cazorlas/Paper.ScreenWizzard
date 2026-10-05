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

function ConvertTo-PaperReviewSonarList {
    <#
    .SYNOPSIS
    A profile value that is a text or a non-empty list of texts to a list (F251, D6): Kind path - every item cut at both ends, a backslash
    becomes a slash, the last slash goes, and an item with a drive, a leading slash, a .. segment or a comma is an error; Kind pattern - only a
    comma or an empty item is an error. Errors is empty when the list is good.
    #>
    param($Value, [string] $Key, [string] $Prefix, [string] $Kind)
    $bad = [pscustomobject]@{ Items = @(); Errors = @("${Prefix}.${Key}: not a text or a non-empty list of texts") }
    $raw = @()
    if ($Value -is [string]) { $raw = @($Value) }
    elseif ($Value -is [System.Array] -and @($Value).Count -gt 0) { $raw = @($Value) }
    else { return $bad }
    foreach ($r in $raw) { if ($r -isnot [string]) { return $bad } }
    $items = @(); $errors = @()
    foreach ($r in $raw) {
        $t = $r.Trim()
        if ($Kind -eq 'path') {
            $n = $t.Replace('\', '/').TrimEnd('/')
            if (-not $n -or $n -match '^[A-Za-z]:' -or $n.StartsWith('/') -or (@($n -split '/') -contains '..') -or $n.Contains(',')) {
                $errors += "${Prefix}.${Key}: `"$t`" is not a path inside the repository (no drive, no leading slash, no .., no comma)"
            }
            else { $items += $n }
        }
        else {
            if (-not $t) { $errors += "${Prefix}.${Key}: an empty item - one pattern per item" }
            elseif ($t.Contains(',')) { $errors += "${Prefix}.${Key}: `"$t`" has a comma - one pattern per item" }
            else { $items += $t }
        }
    }
    return [pscustomobject]@{ Items = $items; Errors = $errors }
}

function ConvertFrom-PaperReviewSonarConfig {
    <#
    .SYNOPSIS
    The review.sonarqube section of the profile (M.1, F251): projectKey (a text; without it the layer is off), url (http or https; empty = the
    environment, then http://localhost:9000, decided by sonarqube.ps1), hotspots (1-500, default 20), timeoutSec (60-3600, default 600),
    scanner (auto, dotnet or cli; default auto), sources, tests and exclusions (a text or a list of texts, for the SonarScanner CLI; empty =
    not declared). A token is never a profile key. Errors are named by Prefix ("review.sonarqube").
    #>
    param($Section, [string] $Prefix = 'review.sonarqube')
    $cfg = [pscustomobject]@{ ProjectKey = ''; Url = ''; Hotspots = $script:PaperReviewSonarHotspots; TimeoutSec = $script:PaperReviewSonarTimeoutSec; Scanner = 'auto'; Sources = @(); Tests = @(); Exclusions = @() }
    $errors = @()
    if ($Section -isnot [System.Collections.IDictionary]) { return [pscustomobject]@{ Sonar = $cfg; Errors = @("${Prefix}: not an object") } }
    foreach ($key in @($Section.Keys)) {
        $v = $Section[$key]
        switch -CaseSensitive ($key) {
            'projectKey' { if ($v -isnot [string] -or -not $v.Trim()) { $errors += "${Prefix}.projectKey: not a text" } else { $cfg.ProjectKey = $v.Trim() } }
            'url' { if ($v -isnot [string] -or $v -notmatch '^https?://[^\s/]+') { $errors += "${Prefix}.url: not an http or https address" } else { $cfg.Url = $v.Trim().TrimEnd('/') } }
            'hotspots' { if (-not (Test-PaperReviewWholeRange $v 1 500)) { $errors += "${Prefix}.hotspots: not a whole number from 1 to 500" } else { $cfg.Hotspots = [int] $v } }
            'timeoutSec' { if (-not (Test-PaperReviewWholeRange $v 60 3600)) { $errors += "${Prefix}.timeoutSec: not a whole number from 60 to 3600" } else { $cfg.TimeoutSec = [int] $v } }
            'scanner' { if ($v -isnot [string] -or @('auto', 'dotnet', 'cli') -cnotcontains $v) { $errors += "${Prefix}.scanner: not auto, dotnet or cli" } else { $cfg.Scanner = $v } }
            'sources' { $l = ConvertTo-PaperReviewSonarList -Value $v -Key 'sources' -Prefix $Prefix -Kind 'path'; $errors += @($l.Errors); if (@($l.Errors).Count -eq 0) { $cfg.Sources = @($l.Items) } }
            'tests' { $l = ConvertTo-PaperReviewSonarList -Value $v -Key 'tests' -Prefix $Prefix -Kind 'path'; $errors += @($l.Errors); if (@($l.Errors).Count -eq 0) { $cfg.Tests = @($l.Items) } }
            'exclusions' { $l = ConvertTo-PaperReviewSonarList -Value $v -Key 'exclusions' -Prefix $Prefix -Kind 'pattern'; $errors += @($l.Errors); if (@($l.Errors).Count -eq 0) { $cfg.Exclusions = @($l.Items) } }
            default { $errors += "${Prefix}.${key}: unknown key (projectKey, url, hotspots, timeoutSec, scanner, sources, tests, exclusions)" }
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
    impacts), Impacts, Path (without the project key), Line, Column (the server's startOffset plus one, 0 when it gave no range - F252), Message, Type. IssuesRead is what the pages hold, IssuesTotal what the server
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
            $column = 0
            $range = Get-PaperReviewSonarProp $i 'textRange'
            if ($null -ne $range -and $null -ne (Get-PaperReviewSonarProp $range 'startOffset')) { $column = [int] (Get-PaperReviewSonarProp $range 'startOffset') + 1 }
            $issues += [pscustomobject]@{
                Rule = $rule; Severity = "$(Get-PaperReviewSonarProp $i 'severity')"; Impacts = @(Get-PaperReviewSonarProp $i 'impacts' | Where-Object { $null -ne $_ })
                Path = $comp.Replace('\', '/'); Line = $line; Column = $column; Message = "$(Get-PaperReviewSonarProp $i 'message')"; Type = "$(Get-PaperReviewSonarProp $i 'type')"
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
    is left out before the ranking. Ranked is every file in scope; Top the first -Top of them; InScope how many; Measured the lower-case paths of Ranked (F253).
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
    $measured = @{}
    foreach ($r in $ranked) { $measured["$($r.Path)".ToLowerInvariant()] = $true }
    return [pscustomobject]@{ Ranked = $ranked; Top = @($ranked | Select-Object -First $take); InScope = $ranked.Count; Measured = $measured }
}

# ------------------------------------------------------------------ M.2, M.3, M.4 when, with which server, how far

function Get-PaperReviewSonarApplicability {
    <#
    .SYNOPSIS
    Whether the SonarQube layer applies to this run (M.2, F245): Enabled when the profile declares a project key; Applies when nothing is in
    the way, else Line says the first thing in the way - an external repository, a branch scope (Community keeps one branch per project), a
    files scope, a branch that is not the base, uncommitted work (the analysis would publish it as the base), a scanner folder git does not
    ignore, no access key, no scanner. Scanner dotnet (the default, as before) or cli (F249-F251, D3): ScannerProblem comes first after the
    scope reasons; the CLI needs no ignored folder, and says CliProblem when it is not found and SourcesProblem when a folder is wrong.
    #>
    param([string] $ProjectKey, [bool] $External, [string] $Mode, [string] $Branch, [string] $BaseBranch, [int] $Dirty, [bool] $Ignored, [bool] $TokenSet, [bool] $ScannerFound,
        [string] $Scanner = 'dotnet', [string] $ScannerProblem = '', [string] $CliProblem = '', [string] $SourcesProblem = '')
    $na = { param($enabled, $why) [pscustomobject]@{ Enabled = $enabled; Applies = $false; Line = "review: sonarqube - not applicable: $why" } }
    if (-not "$ProjectKey".Trim()) { return & $na $false 'no review.sonarqube.projectKey in the profile' }
    if ($External) { return & $na $true 'external repository (read only)' }
    if ($Mode -eq 'branch') { return & $na $true 'branch scope - SonarQube Community keeps one branch per project' }
    if ($Mode -eq 'files') { return & $na $true 'files scope - SonarQube reads the whole project: name the project or a folder' }
    if ($BaseBranch -and $Branch -ne $BaseBranch) { return & $na $true "branch $Branch is not the base branch $BaseBranch" }
    if ($Dirty -gt 0) { return & $na $true "uncommitted work - the analysis would publish it as $BaseBranch" }
    if ($ScannerProblem) { return & $na $true $ScannerProblem }
    if ($Scanner -eq 'cli') {
        # The CLI keeps its work folder inside the run folder, which git always ignores: no .sonarqube/ question.
        if (-not $TokenSet) { return & $na $true 'PAPER_SONARQUBE_TOKEN is not set' }
        if (-not $ScannerFound) { return & $na $true $(if ($CliProblem) { $CliProblem } else { 'SonarScanner CLI not found - set PAPER_SONARQUBE_SCANNER or put sonar-scanner on PATH' }) }
        if ($SourcesProblem) { return & $na $true $SourcesProblem }
    }
    else {
        if (-not $Ignored) { return & $na $true '.sonarqube/ is not ignored by git - add it to .gitignore' }
        if (-not $TokenSet) { return & $na $true 'PAPER_SONARQUBE_TOKEN is not set' }
        if (-not $ScannerFound) { return & $na $true 'dotnet-sonarscanner not on PATH' }
    }
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
# ------------------------------------------------------------------ F249-F253 the SonarScanner CLI for a repository with no .NET code (plan 2026-10-05-sonar-scanner-cli)

$script:PaperReviewSonarCliWorkDir = '.paper/review/sonarqube/cli-work'

function Get-PaperReviewSonarScanner {
    <#
    .SYNOPSIS
    Which scanner runs (F249, D2): Setting is review.sonarqube.scanner - auto (dotnet-sonarscanner when the repository has .NET code, else the
    SonarScanner CLI), dotnet or cli. Problem is not empty when dotnet was asked for and there is no .NET code to build.
    #>
    param([string] $Setting, [bool] $HasDotnetCode)
    $res = { param($scanner, $why, $problem) [pscustomobject]@{ Scanner = $scanner; Why = $why; Problem = $problem } }
    switch ("$Setting") {
        'cli' { return & $res 'cli' 'review.sonarqube.scanner' '' }
        'dotnet' {
            if ($HasDotnetCode) { return & $res 'dotnet' 'review.sonarqube.scanner' '' }
            return & $res 'dotnet' 'review.sonarqube.scanner' 'review.sonarqube.scanner is dotnet but the repository has no .NET code'
        }
        default {
            if ($HasDotnetCode) { return & $res 'dotnet' '.NET code' '' }
            return & $res 'cli' 'no .NET code' ''
        }
    }
}

function Resolve-PaperReviewSonarCli {
    <#
    .SYNOPSIS
    Where the SonarScanner CLI is (F250, D4): PAPER_SONARQUBE_SCANNER wins - a file, or a folder with bin\sonar-scanner.bat; a wrong value is
    a problem and the PATH is not tried. No variable: the hit of the PATH, else a problem.
    #>
    param([string] $Variable, [bool] $VariableIsFile, [bool] $VariableHasBinBat, [string] $PathHit)
    $res = { param($path, $problem) [pscustomobject]@{ Path = $path; Problem = $problem } }
    $v = "$Variable".Trim()
    if ($v) {
        if ($VariableIsFile) { return & $res $v '' }
        if ($VariableHasBinBat) { return & $res ($v.TrimEnd('\') + '\bin\sonar-scanner.bat') '' }
        return & $res '' "PAPER_SONARQUBE_SCANNER is set but $v is not a SonarScanner CLI (sonar-scanner.bat, or a folder with bin\sonar-scanner.bat)"
    }
    if ("$PathHit".Trim()) { return & $res "$PathHit".Trim() '' }
    return & $res '' 'SonarScanner CLI not found - set PAPER_SONARQUBE_SCANNER or put sonar-scanner on PATH'
}

function Get-PaperReviewSonarJavaHome {
    <#
    .SYNOPSIS
    The JAVA_HOME of the CLI process from PAPER_SONARQUBE_JAVA (F250, D5): a java.exe gives the folder two levels up, a folder itself;
    nothing gives nothing (the machine's own JAVA_HOME, or the JRE the zip carries, stays).
    #>
    param([string] $Java)
    $v = "$Java".Trim()
    if (-not $v) { return '' }
    if ($v -match '(?i)\.exe$') {
        $bin = [IO.Path]::GetDirectoryName($v)
        if ($bin) { $up = [IO.Path]::GetDirectoryName($bin); if ($up) { return $up } }
        return $v
    }
    return $v.TrimEnd('\')
}

function Resolve-PaperReviewSonarSources {
    <#
    .SYNOPSIS
    The folders the CLI analyses and the folders of the tests (F251, D7), checked before the scan. Existing: the candidate folders that are on
    disk. Not declared: sources src (else the whole repository), tests tests then test when the sources are not the whole repository. A
    declared folder that is not there, or a test folder inside the sources (SonarQube indexes a file once), is a Problem.
    #>
    param([string[]] $Sources, [string[]] $Tests, [string[]] $Existing)
    $has = @{}
    foreach ($e in @($Existing)) { if ("$e") { $has["$e".ToLowerInvariant()] = $true } }
    $res = { param($s, $t, $problem) [pscustomobject]@{ Sources = @($s); Tests = @($t); Problem = $problem } }
    $src = @($Sources | Where-Object { $_ })
    $tst = @($Tests | Where-Object { $_ })
    if ($src.Count -gt 0) {
        foreach ($s in $src) { if ($s -ne '.' -and -not $has.ContainsKey($s.ToLowerInvariant())) { return & $res @() @() "review.sonarqube.sources: $s does not exist" } }
    }
    else { $src = $(if ($has.ContainsKey('src')) { @('src') } else { @('.') }) }
    if ($tst.Count -gt 0) {
        foreach ($t in $tst) { if (-not $has.ContainsKey($t.ToLowerInvariant())) { return & $res @() @() "review.sonarqube.tests: $t does not exist" } }
    }
    elseif ($src -notcontains '.') {
        foreach ($name in @('tests', 'test')) { if ($has.ContainsKey($name)) { $tst = @($name); break } }
    }
    foreach ($t in $tst) {
        foreach ($s in $src) {
            $lt = $t.ToLowerInvariant(); $ls = $s.ToLowerInvariant()
            if ($ls -eq '.' -or $lt -eq $ls -or $lt.StartsWith("$ls/") -or $ls.StartsWith("$lt/")) { return & $res @() @() "tests $t overlap sources $s - SonarQube indexes a file once" }
        }
    }
    return & $res $src $tst ''
}

function Get-PaperReviewSonarCliArgs {
    <#
    .SYNOPSIS
    The command line of the SonarScanner CLI (F251, D8): the project key, the address, the sources, the tests and the exclusions when there are
    any, and the work folder. The access key is never here - it is the SONAR_TOKEN of the process.
    #>
    param([string] $ProjectKey, [string] $Url, [string[]] $Sources, [string[]] $Tests, [string[]] $Exclusions, [string] $WorkDir)
    $a = @("-Dsonar.projectKey=$ProjectKey", "-Dsonar.host.url=$Url", "-Dsonar.sources=$(@($Sources) -join ',')")
    if (@($Tests | Where-Object { $_ }).Count -gt 0) { $a += "-Dsonar.tests=$(@($Tests) -join ',')" }
    if (@($Exclusions | Where-Object { $_ }).Count -gt 0) { $a += "-Dsonar.exclusions=$(@($Exclusions) -join ',')" }
    $a += "-Dsonar.working.directory=$WorkDir"
    return $a
}

function Format-PaperReviewSonarScannerLine {
    param([string] $Scanner, [string] $Why, [string[]] $Sources, [string[]] $Tests, [string[]] $Exclusions)
    if ($Scanner -ne 'cli') { return "review: sonarqube - scanner: dotnet-sonarscanner ($Why)" }
    $list = { param($l) $x = @($l | Where-Object { $_ }); if ($x.Count -gt 0) { $x -join ',' } else { 'none' } }
    return "review: sonarqube - scanner: SonarScanner CLI ($Why); sources $(@($Sources) -join ','); tests $(& $list $Tests); exclusions $(& $list $Exclusions)"
}

function Format-PaperReviewSonarCliKeysNote { return 'review: sonarqube - review.sonarqube.sources, tests and exclusions are for the SonarScanner CLI; dotnet-sonarscanner reads the projects' }
function Format-PaperReviewSonarCliFailedLine { param([int] $Code) return "review: sonarqube not verifiable - the SonarScanner CLI failed (exit $Code)" }
function Format-PaperReviewSonarNoTaskLine { param([string] $Path) return "review: sonarqube not verifiable - the scanner left no ceTaskId in $Path" }

function ConvertTo-PaperReviewSonarComplexityResults {
    <#
    .SYNOPSIS
    The three complexity rules (S3776, S1541, S1067) of the server's issues as the flat results of the SARIF reader (F252, D11), so
    Get-PaperReviewComplexity ranks them like a build's: Uri inside RepoRoot, Line, Column (the server's offset plus one, 0 when it gave none),
    File sonarqube.
    #>
    param($Issues, [string] $RepoRoot)
    $root = "$RepoRoot".Replace('\', '/').TrimEnd('/')
    $out = @()
    foreach ($i in @($Issues | Where-Object { $null -ne $_ })) {
        if ($script:PaperReviewComplexityRules -cnotcontains "$($i.Rule)") { continue }
        $col = 0
        if ($null -ne $i.PSObject.Properties['Column']) { $col = [int] $i.Column }
        $out += [pscustomobject]@{ RuleId = "$($i.Rule)"; Level = 'warning'; Message = "$($i.Message)"; Uri = "file:///$root/$($i.Path)"; Line = [int] $i.Line; Column = $col; Category = ''; Title = ''; Suppressed = $false; File = 'sonarqube' }
    }
    return $out
}

function Format-PaperReviewSonarComplexitySource {
    param([string] $Head, [string] $State, [string] $Reason)
    return "Source: the SonarQube analysis of $(Get-PaperReviewSonarSha7 $Head) only - the static lane did not run ($($State): $Reason)."
}

function Get-PaperReviewSonarExcludedReason {
    <#
    .SYNOPSIS
    Why a file of the scope is left out of the lanes (F253, D13): not in the top N when the analysis measured it, else it has no number at all.
    Measured is the lower-case paths of Get-PaperReviewHotspots.
    #>
    param([string] $Path, $Measured, [int] $Top)
    $k = "$Path".Replace('\', '/').ToLowerInvariant()
    if ($null -ne $Measured -and $Measured.ContainsKey($k)) { return "not in the top $Top SonarQube hotspots" }
    return 'not measured by the SonarQube analysis'
}
