# /qa static lane, the pure part (plan 2026-10-02-qa-command, table G): reading SARIF 2.1, repository paths,
# the kind and severity of a rule, grouping into findings, rule links, the generated .targets and restore
# project, which DLLs of an analyzer package go to the compiler, project style, the NuGet cache line, the
# proof that each analyzer ran, and the Sonar rule table. No I/O: review.ps1 reads files and starts processes.
# Dot-sourced by review.ps1 and by review-plan.ps1's callers; declares no param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Rules the kit calls a bug whatever the analyzer's category says. Titles checked against each rule's page
# (Get-PaperReviewRuleLink) on 2026-10-02; a code whose page title changes leaves this table, it is not swapped.
$script:PaperReviewKitKinds = [ordered]@{
    'CA1065'  = 'Do not raise exceptions in unexpected locations'
    'CA1508'  = 'Avoid dead conditional code'
    'CA2200'  = 'Rethrow to preserve stack details'
    'CA2208'  = 'Instantiate argument exceptions correctly'
    'CA2241'  = 'Provide correct arguments to formatting methods'
    'CA2245'  = 'Do not assign a property to itself'
    'CA2246'  = 'Do not assign a symbol and its member in the same statement'
    'CA2248'  = 'Provide correct enum argument to Enum.HasFlag'
    'RCS1059' = 'Avoid locking on publicly accessible instance'
    'RCS1202' = 'Avoid NullReferenceException'
    'RCS1210' = 'Return completed task instead of returning null'
    'RCS1215' = 'Expression is always equal to true/false'
    'RCS1229' = 'Use async/await when necessary'
}

$script:PaperReviewSeverityRank = @{ 'critical' = 0; 'major' = 1; 'minor' = 2; 'info' = 3 }
$script:PaperReviewKindRank = @{ 'vulnerability' = 0; 'bug' = 1; 'smell' = 2 }
# CS9057: an analyzer built for a newer compiler than the one running is dropped (measured risk, plan T16).
$script:PaperReviewLoadProblemCodes = @('CS8032', 'CS8034', 'CS9057', 'AD0001')
$script:PaperReviewStaticCountNames = @('outside scope', 'generated', 'suppressed', 'compiler warnings', 'ignored by profile', 'outside repo', 'project-level', 'analyzer problems', 'complexity table')

# Sorts by a string key in ordinal order; culture order ignores '-' and folds case, which reorders paths.
function Sort-PaperReviewByKey($Items, [scriptblock] $Key) {
    $arr = [object[]] @($Items | Where-Object { $null -ne $_ })
    if ($arr.Count -lt 2) { return , $arr }
    # [Array]::Sort(keys, items, comparer) leaves both unsorted when PowerShell binds it, so the keys carry
    # their index and are sorted alone.
    $keys = New-Object 'System.Collections.Generic.List[string]'
    for ($i = 0; $i -lt $arr.Count; $i++) { $keys.Add(([string] (& $Key $arr[$i])) + "`0" + $i.ToString('D9')) }
    $keys.Sort([StringComparer]::Ordinal)
    return , [object[]] @(foreach ($k in $keys) { $arr[[int] $k.Substring($k.LastIndexOf("`0") + 1)] })
}

# A file the build wrote (table C row 4): never a finding, never a file a reviewer is handed.
function Test-PaperReviewGeneratedPath([string] $Path) {
    $parts = @("$Path".Replace('\', '/') -split '/' | Where-Object { $_ -ne '' -and $_ -ne '.' })
    if ($parts.Count -eq 0) { return $false }
    foreach ($p in $parts) { if (@('obj', 'bin', 'node_modules') -contains $p.ToLowerInvariant()) { return $true } }
    if ($parts[0].ToLowerInvariant() -eq 'packages') { return $true }
    $leaf = $parts[-1].ToLowerInvariant()
    foreach ($end in @('.g.cs', '.g.i.cs', '.designer.cs', '.assemblyattributes.cs')) { if ($leaf.EndsWith($end)) { return $true } }
    return $false
}

function Get-PaperReviewProp($Object, [string] $Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) { return $Object[$Name] }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function ConvertFrom-PaperReviewSarif {
    <#
    .SYNOPSIS
    One SARIF log (ConvertFrom-Json output) to flat results: RuleId, Level, Message, Uri, Line, Column,
    Category, Title, Suppressed, File. A log that is not SARIF 2.1 gives a problem and no result.
    #>
    param($Sarif, [string] $File)
    $results = @()
    $problems = @()
    $version = "$(Get-PaperReviewProp $Sarif 'version')"
    if ($version -ne '2.1.0') {
        $problems += "${File}: not SARIF 2.1 (version $version)"
        return [pscustomobject]@{ Results = $results; Problems = $problems }
    }
    foreach ($run in @(Get-PaperReviewProp $Sarif 'runs')) {
        if ($null -eq $run) { continue }
        $rules = @(Get-PaperReviewProp (Get-PaperReviewProp (Get-PaperReviewProp $run 'tool') 'driver') 'rules' | Where-Object { $null -ne $_ })
        $byId = @{}
        foreach ($rule in $rules) { $rid = "$(Get-PaperReviewProp $rule 'id')"; if ($rid -and -not $byId.ContainsKey($rid)) { $byId[$rid] = $rule } }
        foreach ($r in @(Get-PaperReviewProp $run 'results')) {
            if ($null -eq $r) { continue }
            $ruleId = "$(Get-PaperReviewProp $r 'ruleId')"
            $index = Get-PaperReviewProp $r 'ruleIndex'
            $rule = $null
            if ($null -ne $index -and [int] $index -ge 0 -and [int] $index -lt $rules.Count) {
                $candidate = $rules[[int] $index]
                if (-not $ruleId) { $ruleId = "$(Get-PaperReviewProp $candidate 'id')" }
                if ("$(Get-PaperReviewProp $candidate 'id')" -eq $ruleId) { $rule = $candidate }
            }
            if ($null -eq $rule -and $byId.ContainsKey($ruleId)) { $rule = $byId[$ruleId] }
            $level = "$(Get-PaperReviewProp $r 'level')"
            if (-not $level) { $level = 'warning' }
            $message = Get-PaperReviewProp $r 'message'
            if ($message -isnot [string]) { $message = "$(Get-PaperReviewProp $message 'text')" }
            $physical = Get-PaperReviewProp (@(Get-PaperReviewProp $r 'locations')[0]) 'physicalLocation'
            $uri = "$(Get-PaperReviewProp (Get-PaperReviewProp $physical 'artifactLocation') 'uri')"
            $region = Get-PaperReviewProp $physical 'region'
            $line = 0; $column = 0
            if ($null -ne (Get-PaperReviewProp $region 'startLine')) { $line = [int] (Get-PaperReviewProp $region 'startLine') }
            if ($null -ne (Get-PaperReviewProp $region 'startColumn')) { $column = [int] (Get-PaperReviewProp $region 'startColumn') }
            $suppressions = Get-PaperReviewProp $r 'suppressions'
            $results += [pscustomobject]@{
                RuleId     = $ruleId
                Level      = $level
                Message    = "$message"
                Uri        = $uri
                Line       = $line
                Column     = $column
                Category   = "$(Get-PaperReviewProp (Get-PaperReviewProp $rule 'properties') 'category')"
                Title      = "$(Get-PaperReviewProp (Get-PaperReviewProp $rule 'shortDescription') 'text')"
                Suppressed = ($null -ne $suppressions -and @($suppressions | Where-Object { $null -ne $_ }).Count -gt 0)
                File       = $File
            }
        }
    }
    return [pscustomobject]@{ Results = $results; Problems = $problems }
}

function ConvertTo-PaperReviewRepoPath {
    <#
    .SYNOPSIS
    A SARIF uri to a repository path with '/': Reason '' inside the repository, 'no-location' with no uri,
    'outside-repo' outside it, 'generated' for a file the build wrote (Test-PaperReviewGeneratedPath).
    #>
    param([string] $Uri, [string] $RepoRoot)
    if (-not $Uri) { return [pscustomobject]@{ Path = ''; Reason = 'no-location' } }
    $u = $Uri
    if ($u -match '^file:///') { $u = $u.Substring(8) }
    elseif ($u -match '^file://') { $u = '//' + $u.Substring(7) }
    $u = [Uri]::UnescapeDataString($u).Replace('/', '\')
    $rootN = "$RepoRoot".Replace('/', '\').TrimEnd('\') + '\'
    if ($u -match '^[A-Za-z]:\\' -or $u.StartsWith('\\')) { $abs = $u }
    else { $abs = $rootN + $u.TrimStart('\') }
    if (-not $abs.StartsWith($rootN, [StringComparison]::OrdinalIgnoreCase)) {
        return [pscustomobject]@{ Path = $abs.Replace('\', '/'); Reason = 'outside-repo' }
    }
    $rel = $abs.Substring($rootN.Length).Replace('\', '/')
    $reason = ''
    if (Test-PaperReviewGeneratedPath $rel) { $reason = 'generated' }
    return [pscustomobject]@{ Path = $rel; Reason = $reason }
}

function Get-PaperReviewFallbackSeverity([string] $Kind, [string] $Level) {
    if ($Kind -eq 'vulnerability' -or $Kind -eq 'bug') { return 'major' }
    if (@('note', 'none') -contains "$Level".ToLowerInvariant()) { return 'info' }
    return 'minor'
}

function Get-PaperReviewRuleKind {
    <#
    .SYNOPSIS
    The kind (bug, vulnerability, smell, ignore) and severity of one rule, first match wins: the profile line,
    the Sonar table, the kit's CA/RCS table, the kind the SARIF category names, then smell (G.8).
    #>
    param([string] $RuleId, [string] $Category, [string] $Level, $ProfileKinds, $SonarTable)
    $kind = $null; $hotspot = $false; $source = ''
    $isSonar = $RuleId -match '^S\d+$'
    $sonarRow = $null
    if ($isSonar -and $null -ne $SonarTable -and $SonarTable.Contains($RuleId)) { $sonarRow = $SonarTable[$RuleId] }
    $catMatch = [regex]::Match("$Category", '^(Blocker|Critical|Major|Minor|Info)\s+(Bug|Vulnerability|Code Smell|Security Hotspot)$')

    if ($null -ne $ProfileKinds -and $ProfileKinds.Contains($RuleId)) { $kind = "$($ProfileKinds[$RuleId])"; $source = 'profile' }
    elseif ($null -ne $sonarRow) {
        $kind = $sonarRow.Kind; $source = 'table'
        if ($kind -eq 'hotspot') { $kind = 'vulnerability'; $hotspot = $true }
    }
    elseif ($script:PaperReviewKitKinds.Contains($RuleId)) { $kind = 'bug'; $source = 'table' }
    elseif ($catMatch.Success) {
        $source = 'sarif'
        switch ($catMatch.Groups[2].Value) {
            'Bug' { $kind = 'bug' }
            'Vulnerability' { $kind = 'vulnerability' }
            'Code Smell' { $kind = 'smell' }
            'Security Hotspot' { $kind = 'vulnerability'; $hotspot = $true }
        }
    }
    elseif ($Category -eq 'Security') { $kind = 'vulnerability'; $source = 'sarif' }
    elseif ($Category -eq 'Reliability') { $kind = 'bug'; $source = 'sarif' }
    else { $kind = 'smell'; $source = 'default' }

    $severity = ''
    if ($kind -ne 'ignore') {
        if ($isSonar -and $null -ne $sonarRow -and $sonarRow.Severity) { $severity = $sonarRow.Severity }
        elseif ($isSonar -and $catMatch.Success) {
            $severity = switch ($catMatch.Groups[1].Value) { 'Blocker' { 'critical' } 'Critical' { 'critical' } 'Major' { 'major' } 'Minor' { 'minor' } default { 'info' } }
        }
        else { $severity = Get-PaperReviewFallbackSeverity $kind $Level }
    }
    return [pscustomobject]@{ Kind = $kind; Severity = $severity; Hotspot = $hotspot; Source = $source }
}

function Get-PaperReviewRuleLink {
    param([string] $RuleId)
    if ($RuleId -match '^S(\d+)$') { return "https://rules.sonarsource.com/csharp/RSPEC-$($Matches[1])" }
    if ($RuleId -match '^CA(\d+)$') { return "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca$($Matches[1])" }
    if ($RuleId -match '^IDE(\d+)$') { return "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide$($Matches[1])" }
    if ($RuleId -match '^RCS(\d+)$') { return "https://josefpihrt.github.io/docs/roslynator/analyzers/RCS$($Matches[1])" }
    return ''
}

function Group-PaperReviewStatic {
    <#
    .SYNOPSIS
    Every SARIF result of a run to findings (G.8, G.9): results not listed are only counted; one finding per
    rule, path, line and column whatever SARIF file it came from (Sources names them); ids ST-<n> in the
    order of the report: vulnerability, bug, smell; groups by highest severity, count, rule.
    .OUTPUTS
    Findings, Groups, Counts (ordered), LoadProblems (CS8032, CS8034, CS9057, AD0001 results for the proof).
    #>
    param($Results, [string] $RepoRoot, [string[]] $ScopePaths, [string] $ScopeMode, $ProfileKinds, $SonarTable)
    $counts = [ordered]@{}
    foreach ($n in $script:PaperReviewStaticCountNames) { $counts[$n] = 0 }
    $load = @()
    $scope = @{}
    foreach ($p in @($ScopePaths)) { if ($p) { $scope[$p.Replace('\', '/').ToLowerInvariant()] = $true } }
    $byKey = [ordered]@{}
    $complexSeen = @{}

    foreach ($r in @($Results)) {
        if ($null -eq $r) { continue }
        $id = "$($r.RuleId)"
        if ($script:PaperReviewLoadProblemCodes -contains $id) {
            $counts['analyzer problems']++
            $load += [pscustomobject]@{ RuleId = $id; Message = "$($r.Message)" }
            continue
        }
        if ($id -match '^CS\d+$') { $counts['compiler warnings']++; continue }
        if ($r.Suppressed) { $counts['suppressed']++; continue }
        $loc = ConvertTo-PaperReviewRepoPath -Uri $r.Uri -RepoRoot $RepoRoot
        $path = $loc.Path
        $where = ''
        if ($loc.Reason -eq 'generated') { $counts['generated']++; continue }
        if ($loc.Reason -eq 'outside-repo') { $counts['outside repo']++; continue }
        if ($loc.Reason -eq 'no-location') {
            $counts['project-level']++
            if ($ScopeMode -ne 'project') { continue }
            $where = "(project) $($r.File)"
        }
        else {
            if ($ScopeMode -ne 'project' -and -not $scope.ContainsKey($path.ToLowerInvariant())) { $counts['outside scope']++; continue }
            $where = "${path}:$($r.Line)"
        }
        # S3776, S1541 and S1067 are measured, not listed: they go to the complexity table (F235), once for each place.
        if ($script:PaperReviewComplexityRules -ccontains $id) {
            $ck = "$id|$path|$($r.Line)|$($r.Column)".ToLowerInvariant()
            if (-not $complexSeen.ContainsKey($ck)) { $complexSeen[$ck] = $true; $counts['complexity table']++ }
            continue
        }
        $kind = Get-PaperReviewRuleKind -RuleId $id -Category $r.Category -Level $r.Level -ProfileKinds $ProfileKinds -SonarTable $SonarTable
        if ($kind.Kind -eq 'ignore') { $counts['ignored by profile']++; continue }
        $key = "$id|$path|$($r.Line)|$($r.Column)".ToLowerInvariant()
        if ($byKey.Contains($key)) {
            $f = $byKey[$key]
            if (@($f.Sources) -notcontains $r.File) { $f.Sources = @($f.Sources) + $r.File }
            continue
        }
        $title = "$($r.Title)"
        if (-not $title -and $null -ne $SonarTable -and $SonarTable.Contains($id)) { $title = $SonarTable[$id].Title }
        $byKey[$key] = [pscustomobject]@{
            Id = ''; RuleId = $id; Kind = $kind.Kind; Severity = $kind.Severity; Hotspot = $kind.Hotspot; Source = $kind.Source
            Path = $path; Line = [int] $r.Line; Column = [int] $r.Column; Where = $where; Message = "$($r.Message)"
            Title = $title; Level = $r.Level; Sources = @($r.File); Link = (Get-PaperReviewRuleLink -RuleId $id); Status = 'machine'
        }
    }

    $groupMap = [ordered]@{}
    foreach ($f in $byKey.Values) {
        $gk = "$($f.Kind)|$($f.RuleId)".ToLowerInvariant()
        if (-not $groupMap.Contains($gk)) { $groupMap[$gk] = New-Object System.Collections.ArrayList }
        [void] $groupMap[$gk].Add($f)
    }
    $groups = @()
    foreach ($gk in $groupMap.Keys) {
        $members = Sort-PaperReviewByKey $groupMap[$gk] { param($x) "$($x.Path.ToLowerInvariant())`t$(([int] $x.Line).ToString('D9'))`t$(([int] $x.Column).ToString('D9'))" }
        $best = @($members | ForEach-Object { $script:PaperReviewSeverityRank[$_.Severity] } | Sort-Object)[0]
        $first = $members[0]
        $title = @($members | Where-Object { $_.Title } | Select-Object -First 1 | ForEach-Object { $_.Title })
        $groupTitle = if ($title.Count -gt 0) { $title[0] } else { $first.Message }
        $severity = @($script:PaperReviewSeverityRank.Keys | Where-Object { $script:PaperReviewSeverityRank[$_] -eq $best })[0]
        $groups += [pscustomobject]@{
            RuleId = $first.RuleId; Kind = $first.Kind; Severity = $severity; SeverityRank = [int] $best; Count = $members.Count
            Title = $groupTitle; Link = $first.Link; Source = $first.Source; Hotspot = [bool] $first.Hotspot; Findings = $members
        }
    }
    $groups = Sort-PaperReviewByKey $groups { param($g) "$($script:PaperReviewKindRank[$g.Kind])`t$($g.SeverityRank)`t$((1000000000 - $g.Count).ToString('D10'))`t$($g.RuleId)" }
    $findings = @()
    $n = 0
    foreach ($g in $groups) {
        foreach ($f in $g.Findings) { $n++; $f.Id = "ST-$n"; $findings += $f }
    }
    return [pscustomobject]@{ Findings = $findings; Groups = $groups; Counts = $counts; LoadProblems = $load }
}

function Format-PaperReviewStaticSummary {
    param($Static)
    $f = @($Static.Findings)
    $v = @($f | Where-Object { $_.Kind -eq 'vulnerability' }).Count
    $b = @($f | Where-Object { $_.Kind -eq 'bug' }).Count
    $s = @($f | Where-Object { $_.Kind -eq 'smell' }).Count
    $c = $Static.Counts
    return ("review: static - {0} vulnerability, {1} bug, {2} smell in scope; not listed: outside scope {3}, generated {4}, suppressed {5}, compiler warnings {6}, ignored by profile {7}, outside repo {8}" -f `
            $v, $b, $s, $c['outside scope'], $c['generated'], $c['suppressed'], $c['compiler warnings'], $c['ignored by profile'], $c['outside repo'])
}

# MSBuild reads % ; $ @ ' in an item or property as syntax; XML then needs & < > ".
function ConvertTo-PaperReviewMsbuildText([string] $Text) {
    $t = "$Text".Replace('%', '%25').Replace(';', '%3B').Replace('$', '%24').Replace('@', '%40').Replace("'", '%27')
    return $t.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function New-PaperReviewTargets {
    <#
    .SYNOPSIS
    The .targets a review run imports into every project of the build through CustomAfterMicrosoftCommonTargets
    (G.2): analyzers on, warnings never errors, SARIF per project and framework, the kit's .globalconfig, the
    analyzer DLLs (a LegacyOnly one only where the SDK does not bring its own), the analyzer list after
    CoreCompile, and the file itself as a compile input so the compiler runs again.
    #>
    param([string] $Run, [string] $SarifDir, [string] $AnalyzerListDir, [string] $GlobalConfig, [string[]] $Analyzers, [string[]] $LegacyOnly, [string] $SonarLintXml = '')
    $legacy = @{}
    foreach ($l in @($LegacyOnly)) { if ($l) { $legacy[$l.ToLowerInvariant()] = $true } }
    $sarif = (ConvertTo-PaperReviewMsbuildText $SarifDir.TrimEnd('\'))
    $list = (ConvertTo-PaperReviewMsbuildText $AnalyzerListDir.TrimEnd('\'))
    $lines = @(
        '<Project>',
        "  <!-- paper-kit review run ${Run}: imported through CustomAfterMicrosoftCommonTargets. Generated; never commit. -->",
        '  <PropertyGroup>',
        '    <RunAnalyzers>true</RunAnalyzers>',
        '    <RunAnalyzersDuringBuild>true</RunAnalyzersDuringBuild>',
        '    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>',
        '    <CodeAnalysisTreatWarningsAsErrors>false</CodeAnalysisTreatWarningsAsErrors>',
        '    <MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors>',
        '    <WarningsAsErrors></WarningsAsErrors>',
        '    <PaperReviewSuffix Condition="''$(TargetFramework)'' != ''''">-$(TargetFramework)</PaperReviewSuffix>',
        ('    <ErrorLog>' + $sarif + '\$(MSBuildProjectName)$(PaperReviewSuffix).sarif,version=2.1</ErrorLog>'),
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <CustomAdditionalCompileInputs Include="$(MSBuildThisFileFullPath)" />'
    )
    if ($GlobalConfig) {
        $lines += ('    <GlobalAnalyzerConfigFiles Include="' + (ConvertTo-PaperReviewMsbuildText $GlobalConfig) + '" />')
        # Measured (plan T8, X01): Microsoft.Managed.Core.targets copies GlobalAnalyzerConfigFiles into
        # EditorConfigFiles - what Csc reads - while it is evaluated, before this file is imported, so the item
        # above alone never reaches the compiler. EditorConfigFiles does.
        $lines += ('    <EditorConfigFiles Include="' + (ConvertTo-PaperReviewMsbuildText $GlobalConfig) + '" />')
    }
    if ($SonarLintXml) { $lines += ('    <AdditionalFiles Include="' + (ConvertTo-PaperReviewMsbuildText $SonarLintXml) + '" />') }
    foreach ($a in @($Analyzers)) {
        if (-not $a) { continue }
        $line = '    <Analyzer Include="' + (ConvertTo-PaperReviewMsbuildText $a) + '"'
        if ($legacy.ContainsKey($a.ToLowerInvariant())) { $line += ' Condition="''$(UsingMicrosoftNETSdk)'' != ''true''"' }
        $lines += ($line + ' />')
    }
    $lines += @(
        '  </ItemGroup>',
        '  <Target Name="PaperReviewAnalyzerList" AfterTargets="CoreCompile">',
        ('    <WriteLinesToFile File="' + $list + '\analyzers-$(MSBuildProjectName)$(PaperReviewSuffix).txt" Lines="@(Analyzer)" Overwrite="true" />'),
        '  </Target>',
        '</Project>'
    )
    return ($lines -join "`r`n")
}

function New-PaperReviewRestoreProject {
    <#
    .SYNOPSIS
    The throwaway project `dotnet restore` reads to put the analyzer packages in the NuGet cache (G.3):
    PackageDownload references nothing and writes nothing into the project under review.
    #>
    param($Packages, [string] $TargetFramework)
    $lines = @(
        '<Project Sdk="Microsoft.NET.Sdk">',
        '  <PropertyGroup>',
        "    <TargetFramework>$TargetFramework</TargetFramework>",
        '  </PropertyGroup>',
        '  <ItemGroup>'
    )
    foreach ($id in @($Packages.Keys)) {
        $lines += ('    <PackageDownload Include="' + (ConvertTo-PaperReviewMsbuildText $id) + '" Version="[' + (ConvertTo-PaperReviewMsbuildText "$($Packages[$id])") + ']" />')
    }
    $lines += @('  </ItemGroup>', '</Project>')
    return ($lines -join "`r`n")
}

function Get-PaperReviewAnalyzerDlls {
    <#
    .SYNOPSIS
    Which DLLs of an analyzer package go to the compiler (G.4), from the package's files relative to its
    folder: the C# folder of the highest roslyn<X.Y>, else analyzers/dotnet/cs, analyzers/cs, analyzers;
    plus the language-neutral DLLs beside a cs folder under analyzers/dotnet. No resources. Ordinal order.
    #>
    param([string[]] $Files)
    $all = @($Files | Where-Object { $_ } | ForEach-Object { $_.Replace('\', '/') })
    $direct = {
        param([string] $dir)
        @($all | Where-Object {
                $_.StartsWith($dir, [StringComparison]::OrdinalIgnoreCase) -and
                $_.Substring($dir.Length) -notmatch '/' -and $_ -match '\.dll$' -and $_ -notmatch '\.resources\.dll$'
            })
    }
    $main = $null
    $best = $null
    foreach ($f in $all) {
        $m = [regex]::Match($f, '^(analyzers/dotnet/roslyn(\d+(?:\.\d+){0,3})/cs/)[^/]+\.dll$', 'IgnoreCase')
        if (-not $m.Success) { continue }
        $vText = $m.Groups[2].Value
        if ($vText -notmatch '\.') { $vText += '.0' }
        $v = [version] $vText
        if ($null -eq $best -or $v -gt $best) { $best = $v; $main = $m.Groups[1].Value }
    }
    if (-not $main) {
        foreach ($candidate in @('analyzers/dotnet/cs/', 'analyzers/cs/', 'analyzers/')) {
            if (@(& $direct $candidate).Count -gt 0) { $main = $candidate; break }
        }
    }
    if (-not $main) { return @() }
    $picked = @(& $direct $main)
    if ($main -match '/cs/$') {
        $parent = $main.Substring(0, $main.Length - 3)
        if ($parent.StartsWith('analyzers/dotnet/', [StringComparison]::OrdinalIgnoreCase)) { $picked += @(& $direct $parent) }
    }
    $list = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in $picked) { if (-not $list.Contains($p)) { $list.Add($p) } }
    $list.Sort([StringComparer]::Ordinal)
    return $list.ToArray()
}

function Get-PaperReviewProjectStyle {
    <#
    .SYNOPSIS
    'sdk' when the project's root element names an Sdk or an Import does; 'legacy' otherwise - the projects
    whose compiler gets NetAnalyzers from the package instead of the SDK.
    #>
    param([string] $Text)
    $root = [regex]::Match("$Text", '<Project\b[^>]*>')
    if ($root.Success -and $root.Value -match '\sSdk\s*=') { return 'sdk' }
    if ("$Text" -match '<Import\b[^>]*\sSdk\s*=') { return 'sdk' }
    if ("$Text" -match '<Sdk\s+Name\s*=') { return 'sdk' }
    return 'legacy'
}

function ConvertFrom-PaperNugetLocals {
    param([string[]] $Lines)
    foreach ($l in @($Lines)) {
        $m = [regex]::Match("$l", '^\s*global-packages:\s*(.+?)\s*$')
        if ($m.Success) { return $m.Groups[1].Value }
    }
    return ''
}

function Get-PaperReviewAnalyzerProof {
    <#
    .SYNOPSIS
    Which analyzers did NOT run (G.5): one whose expected DLL never reached the compiler's analyzer list, or
    whose name a load problem (CS8032, CS8034, CS9057, AD0001) carries. No list at all: no project compiled.
    #>
    param([string[]] $Listed, $Expected, $LoadProblems)
    $leaves = @{}
    foreach ($l in @($Listed)) {
        $t = "$l".Trim()
        if (-not $t) { continue }
        $leaves[(($t -split '[\\/]')[-1]).ToLowerInvariant()] = $true
    }
    if ($leaves.Count -eq 0) { return [pscustomobject]@{ Id = '*'; Reason = 'no project compiled' } }
    $fails = @()
    foreach ($id in @($Expected.Keys)) {
        $names = @($Expected[$id] | Where-Object { $_ })
        $found = @($names | Where-Object { $leaves.ContainsKey($_.ToLowerInvariant()) }).Count -gt 0
        if (-not $found) { $fails += [pscustomobject]@{ Id = $id; Reason = 'not passed to the compiler' }; continue }
        foreach ($p in @($LoadProblems)) {
            if ($null -eq $p) { continue }
            $hit = @($names | Where-Object { "$($p.Message)".IndexOf(($_ -replace '\.dll$', ''), [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count -gt 0
            if ($hit) { $fails += [pscustomobject]@{ Id = $id; Reason = "$($p.RuleId): $($p.Message)" }; break }
        }
    }
    return $fails
}

function ConvertFrom-PaperSonarRspec {
    <#
    .SYNOPSIS
    One rule of sonar-dotnet's analyzers/rspec/cs/<id>.json to a line of data/sonar-cs-rules.tsv (G.11).
    A host product named in a title becomes "host": the kit's core names no host.
    #>
    param([string] $Id, $Rspec)
    $kind = switch ("$(Get-PaperReviewProp $Rspec 'type')") {
        'BUG' { 'bug' } 'VULNERABILITY' { 'vulnerability' } 'CODE_SMELL' { 'smell' } 'SECURITY_HOTSPOT' { 'hotspot' } default { 'smell' }
    }
    $severity = switch ("$(Get-PaperReviewProp $Rspec 'defaultSeverity')") {
        'Blocker' { 'critical' } 'Critical' { 'critical' } 'Major' { 'major' } 'Minor' { 'minor' } default { 'info' }
    }
    $title = ("$(Get-PaperReviewProp $Rspec 'title')" -replace '[\t\r\n]', ' ').Trim()
    # Written so this file does not itself name the hosts it removes.
    $title = [regex]::Replace($title, '(?<![a-z0-9])(r[e]vit|autoc[a]d|ac[a]d)(?![a-z0-9])', 'host', 'IgnoreCase')
    $standards = Get-PaperReviewProp $Rspec 'securityStandards'
    $cwe = @(@(Get-PaperReviewProp $standards 'CWE') | Where-Object { $null -ne $_ -and "$_" -ne '' } | ForEach-Object { "CWE-$_" }) -join ';'
    $owasp = @(@(Get-PaperReviewProp $standards 'OWASP') | Where-Object { $null -ne $_ -and "$_" -ne '' } | ForEach-Object { "$_" }) -join ';'
    return (@($Id, $kind, $severity, $title, $cwe, $owasp) -join "`t")
}

function Read-PaperReviewSonarTable {
    param([string[]] $Lines)
    $table = @{}
    foreach ($l in @($Lines)) {
        if (-not $l -or $l.StartsWith('#') -or $l.StartsWith("id`t")) { continue }
        $c = @($l -split "`t")
        while ($c.Count -lt 6) { $c += '' }
        $table[$c[0]] = [pscustomobject]@{ Kind = $c[1]; Severity = $c[2]; Title = $c[3]; Cwe = $c[4]; Owasp = $c[5] }
    }
    return $table
}

# ------------------------------------------------------------------ G.1: the decisions of `review.ps1 static`
# review.ps1 reads files and starts processes; what it does with what it read is decided here.

$script:PaperReviewNetAnalyzersId = 'Microsoft.CodeAnalysis.NetAnalyzers'

# Step 1: a static lane that does not run exits 5 with its reason; 0 goes on.
function Get-PaperReviewStaticGate {
    param($Row)
    if ($null -eq $Row -or $Row.State -eq 'not applicable') { return [pscustomobject]@{ ExitCode = 5; Line = "review: static NOT APPLICABLE - $($Row.Reason)" } }
    if ($Row.State -ne 'run') { return [pscustomobject]@{ ExitCode = 5; Line = "review: static skipped - $($Row.Reason)" } }
    return [pscustomobject]@{ ExitCode = 0; Line = '' }
}

# The folder NuGet keeps a version in (find-bug FB16): lower case, at least three parts, a fourth part of 0
# dropped - 4.12 -> 4.12.0, 10.30.0.0 -> 10.30.0 - so a pin written either way finds its package.
function ConvertTo-PaperReviewNugetVersion([string] $Version) {
    $v = "$Version".Trim().ToLowerInvariant()
    $m = [regex]::Match($v, '^(\d+(?:\.\d+)*)(-.*)?$')
    if (-not $m.Success) { return $v }
    $parts = @($m.Groups[1].Value -split '\.' | ForEach-Object { [string] [long] $_ })
    while ($parts.Count -lt 3) { $parts += '0' }
    if ($parts.Count -eq 4 -and $parts[3] -eq '0') { $parts = @($parts[0..2]) }
    return (($parts -join '.') + $m.Groups[2].Value)
}

# The restore project targets net<major>.0 of the running SDK: its targeting pack ships with the SDK.
function Get-PaperReviewTargetFramework {
    param([string] $VersionText)
    $m = [regex]::Match("$VersionText".Trim(), '^(\d+)\.')
    if (-not $m.Success) { return '' }
    return "net$($m.Groups[1].Value).0"
}

# Step 3: the packages to download - every analyzer not "off", except NetAnalyzers, which SDK-style projects
# get from the SDK; it is downloaded only when an old-format project needs it. Styles: project path -> sdk|legacy.
function Get-PaperReviewStaticPackages {
    param($Analyzers, $Styles)
    $values = @()
    if ($null -ne $Styles) { $values = @($Styles.Values) }
    $hasLegacy = @($values | Where-Object { $_ -eq 'legacy' }).Count -gt 0
    $hasSdk = @($values | Where-Object { $_ -eq 'sdk' }).Count -gt 0
    $packages = [ordered]@{}
    foreach ($id in @($Analyzers.Keys)) {
        if ($Analyzers[$id] -eq 'off') { continue }
        if ($id -eq $script:PaperReviewNetAnalyzersId -and -not $hasLegacy) { continue }
        $packages[$id] = $Analyzers[$id]
    }
    return [pscustomobject]@{ Packages = $packages; HasLegacy = $hasLegacy; HasSdk = $hasSdk }
}

# A failed `dotnet restore`: the package its first NU line names (else the first package), with that line.
function Get-PaperReviewRestoreFailure {
    param([string[]] $Lines, $Packages, [int] $ExitCode)
    $nu = @($Lines | Where-Object { "$_" -match '\bNU\d{4}\b' } | Select-Object -First 1)
    $line = if ($nu.Count -gt 0) { "$($nu[0])".Trim() } else { "dotnet restore exit $ExitCode" }
    $ids = @($Packages.Keys)
    $culprit = @($ids | Where-Object { $line.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 1)
    $id = if ($culprit.Count -gt 0) { $culprit[0] } else { $ids[0] }
    return "could not download $id $($Packages[$id]): $line"
}

# Every downloaded DLL goes to the compiler; the NetAnalyzers package's only where the SDK brings none.
function Get-PaperReviewAnalyzerPlug {
    param($DllsById)
    $all = @(); $legacy = @()
    foreach ($id in @($DllsById.Keys)) {
        $all += @($DllsById[$id])
        if ($id -eq $script:PaperReviewNetAnalyzersId) { $legacy += @($DllsById[$id]) }
    }
    return [pscustomobject]@{ Analyzers = $all; LegacyOnly = $legacy }
}

# What the proof (G.5) expects in the compiler's analyzer list, per analyzer that is not "off".
function Get-PaperReviewExpectedDlls {
    param($Analyzers, [bool] $HasSdk, $DllsById)
    $expected = [ordered]@{}
    foreach ($id in @($Analyzers.Keys)) {
        if ($Analyzers[$id] -eq 'off') { continue }
        $names = @()
        if ($id -eq $script:PaperReviewNetAnalyzersId -and $HasSdk) { $names += @('Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll', 'Microsoft.CodeAnalysis.NetAnalyzers.dll') }
        if ($null -ne $DllsById -and $DllsById.Contains($id)) { $names += @($DllsById[$id] | ForEach-Object { ("$_" -split '[\\/]')[-1] }) }
        if ($names.Count -gt 0) { $expected[$id] = @($names | Select-Object -Unique) }
    }
    return $expected
}

# Step 6: the environment of the build verb's process - MSBuild reads environment variables as properties;
# PowerShell 5.1 cannot pass MSBuild switches through the verb.
function Get-PaperReviewBuildEnvironment {
    param([string] $Targets, $Analyzers)
    # F239: a machine that deploys every debug build into the shared add-in folder (PAPER_DEPLOY_DEBUG) is told not to; MSBuild reads an
    # environment variable as a property, and a -p: switch or a project that assigns the property unconditionally still wins.
    $envs = [ordered]@{ 'CustomAfterMicrosoftCommonTargets' = $Targets; 'MSBUILDDISABLENODEREUSE' = '1'; 'PaperDeployDebug' = 'false' }
    # Off is set false: left out, the SDK runs its NetAnalyzers anyway (measured, find-bug FB22).
    if ($Analyzers.Contains($script:PaperReviewNetAnalyzersId)) { $envs['EnableNETAnalyzers'] = $(if ($Analyzers[$script:PaperReviewNetAnalyzersId] -eq 'off') { 'false' } else { 'true' }) }
    return $envs
}

# Steps 6-7: $null for a green build that wrote SARIF; else the reason (F58) with the command the verb ran
# (paperflow's "build -> <command>" line) and the last five lines - a red build and one with no SARIF alike.
function Get-PaperReviewBuildFailure {
    param([int] $ExitCode, [string[]] $Lines, [int] $SarifCount = 1, [switch] $External)
    # An external repository's build is the owner's build line, run directly: its exit 4 is a failed build.
    if ($ExitCode -eq 4 -and -not $External) {
        $held = @($Lines | Where-Object { "$_" -match 'another run holds' } | Select-Object -First 1)
        $what = if ($held.Count -gt 0) { "$($held[0])".Trim() } else { 'paperflow exit 4' }
        return [pscustomobject]@{ Reason = "another build holds the project: $what"; Tail = @() }
    }
    if ($ExitCode -eq 0 -and $SarifCount -gt 0) { return $null }
    $reason = if ($ExitCode -ne 0) { "build failed (exit $ExitCode)" } else { 'the build wrote no SARIF file' }
    $all = @($Lines)
    $last = @($all | Select-Object -Last 5)
    $pattern = if ($External) { '^review: build -> ' } else { '^paperflow: build -> ' }
    $command = @($all | Where-Object { "$_" -match $pattern } | Select-Object -First 1)
    $tail = @()
    if ($command.Count -gt 0 -and $last -notcontains $command[0]) { $tail += $command[0] }
    $tail += $last
    return [pscustomobject]@{ Reason = $reason; Tail = $tail }
}

# The "Analyzers:" line of the report.
function Format-PaperReviewAnalyzerText {
    param($Analyzers, [bool] $HasLegacy)
    $parts = @()
    foreach ($id in @($Analyzers.Keys)) {
        $v = $Analyzers[$id]
        if ($id -eq $script:PaperReviewNetAnalyzersId -and $v -ne 'off') {
            $t = "$id SDK built-in for SDK-style projects"
            if ($HasLegacy) { $t += ", $v for the others" }
            $parts += $t
        }
        else { $parts += "$id $v" }
    }
    return ($parts -join ', ')
}

# The three complexity rules of Sonar (F235): they are measured, not listed - the architecture review ranks them in its
# complexity table. Until the table exists they simply stay out of the smell listing.
$script:PaperReviewComplexityRules = @('S3776', 'S1541', 'S1067')

function Get-PaperReviewKindView {
    <#
    .SYNOPSIS
    What one command lists from the one analyzer build (F237, F.2): architecture - smell (not the three complexity
    rules); security - vulnerability (hotspots included); bugs - nothing, the bug-kind diagnostics are hints (HINT-<n>,
    never findings: no reader, no ledger proposal). Findings and Groups keep their ST ids; the counts of the other
    kinds are for the summary line. Kind '' lists everything, as before.
    #>
    param($Grouped, [string] $Kind)
    $all = @($Grouped.Findings | Where-Object { $null -ne $_ })
    $isComplexity = { param($x) $script:PaperReviewComplexityRules -contains "$($x.RuleId)" }
    $complexity = @($all | Where-Object { & $isComplexity $_ })
    $rest = @($all | Where-Object { -not (& $isComplexity $_) })
    $inTable = $complexity.Count
    if ($null -ne $Grouped.Counts -and $Grouped.Counts.Contains('complexity table')) { $inTable += [int] $Grouped.Counts['complexity table'] }
    $n = { param($k, $list) @($list | Where-Object { $_.Kind -eq $k }).Count }
    $view = [ordered]@{
        Vulnerability = (& $n 'vulnerability' $rest); Bug = (& $n 'bug' $rest); Smell = (& $n 'smell' $rest); InTable = $inTable
        Findings = $all; Groups = @($Grouped.Groups | Where-Object { $null -ne $_ }); Hints = @()
    }
    $keep = $null
    switch ($Kind) {
        'architecture' { $keep = 'smell' }
        'security' { $keep = 'vulnerability' }
        'bugs' { $keep = '' }
    }
    if ($Kind -and $null -ne $keep) {
        $listed = @()
        if ($keep) { $listed = @($rest | Where-Object { $_.Kind -eq $keep }) }
        $ids = @{}
        foreach ($f in $listed) { $ids["$($f.Id)"] = $true }
        $view.Findings = $listed
        $view.Groups = @($Grouped.Groups | Where-Object { $null -ne $_ -and $keep -and $_.Kind -eq $keep -and $script:PaperReviewComplexityRules -notcontains "$($_.RuleId)" })
        if ($Kind -eq 'bugs') {
            $rank = @{ 'critical' = 0; 'major' = 1; 'minor' = 2; 'info' = 3 }
            $bugs = @($rest | Where-Object { $_.Kind -eq 'bug' })
            $sorted = Sort-PaperReviewByKey $bugs { param($x) $sr = 4; if ($rank.ContainsKey("$($x.Severity)")) { $sr = $rank["$($x.Severity)"] }; "$sr`t$("$($x.Path)".ToLowerInvariant())`t$(([int] $x.Line).ToString('D9'))`t$($x.RuleId)" }
            $h = 0
            $hints = @()
            foreach ($b in @($sorted)) {
                $h++
                $hints += [pscustomobject]@{
                    Id = "HINT-$h"; RuleId = $b.RuleId; Kind = 'bug'; Severity = $b.Severity; Where = $b.Where; Path = $b.Path; Line = $b.Line
                    Title = $b.Title; Message = $b.Message; Link = $b.Link; Status = 'hint'
                }
            }
            $view.Hints = $hints
        }
    }
    return [pscustomobject]$view
}

# The summary line of one command's listing (F237, F.2): what it lists, what the others will, and what was not listed.
function Format-PaperReviewKindSummary {
    param($Static, $View, [string] $Kind)
    $c = $Static.Counts
    $not = ("not listed: outside scope {0}, generated {1}, suppressed {2}, compiler warnings {3}, ignored by profile {4}, outside repo {5}" -f `
            $c['outside scope'], $c['generated'], $c['suppressed'], $c['compiler warnings'], $c['ignored by profile'], $c['outside repo'])
    switch ($Kind) {
        'architecture' { return "review: static - $($View.Smell) smell listed, $($View.InTable) in the complexity table; for other reviews: $($View.Vulnerability) vulnerability (/review-security), $($View.Bug) bug (/review-bugs); $not" }
        'bugs' { return "review: static - $($View.Bug) bug hint(s) for the bug lane; for other reviews: $($View.Vulnerability) vulnerability (/review-security), $($View.Smell) smell (/review-architecture); $not" }
        'security' { return "review: static - $($View.Vulnerability) vulnerability listed; for other reviews: $($View.Bug) bug (/review-bugs), $($View.Smell) smell (/review-architecture); $not" }
    }
    return (Format-PaperReviewStaticSummary -Static $Static)
}

# Steps 8-9: static.json, the lines to print and the exit code, from the grouped results and the proof.
# Kind (F237): the command that asked - what is listed, what is only a hint; '' lists everything (pure callers).
function Get-PaperReviewStaticOutcome {
    param($Grouped, $Proof, [string] $AnalyzerText, [string] $BuildText, [string[]] $Problems, [string] $SarifDir, [int] $SarifCount = -1, [string] $Kind = '')
    $proofs = @($Proof | Where-Object { $null -ne $_ })
    $problemList = @($Problems | Where-Object { $_ })
    # Every SARIF file unreadable (not JSON, not 2.1) is no result at all: not verifiable (F58, find-bug FB15).
    if ($SarifCount -ge 0 -and $problemList.Count -ge [math]::Max(1, $SarifCount)) {
        $reason = "no SARIF file could be read: $($problemList[0])"
        $json = [pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = $AnalyzerText; Build = $BuildText; Kind = $Kind; Findings = @(); Groups = @(); Hints = @(); Counts = $Grouped.Counts; Problems = $problemList }
        return [pscustomobject]@{ ExitCode = 4; Json = $json; Lines = @("review: static not verifiable - $reason"); CopySarifTo = '' }
    }
    if ($proofs.Count -gt 0) {
        $first = $proofs[0]
        $reason = if ($first.Id -eq '*') { "analyzer did not run: $($first.Reason)" } else { "analyzer $($first.Id) did not run: $($first.Reason)" }
        $json = [pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = $AnalyzerText; Build = $BuildText; Kind = $Kind; Findings = @(); Groups = @(); Hints = @(); Counts = $Grouped.Counts; Problems = $problemList }
        return [pscustomobject]@{ ExitCode = 4; Json = $json; Lines = @("review: static not verifiable - $reason"); CopySarifTo = '' }
    }
    $view = Get-PaperReviewKindView -Grouped $Grouped -Kind $Kind
    $groups = @($view.Groups | ForEach-Object {
            $ids = @($_.Findings | Where-Object { $null -ne $_ } | ForEach-Object { $_.Id })
            [pscustomobject]@{ RuleId = $_.RuleId; Kind = $_.Kind; Severity = $_.Severity; SeverityRank = $_.SeverityRank; Count = $_.Count; Title = $_.Title; Link = $_.Link; Source = $_.Source; Hotspot = $_.Hotspot; Ids = $ids }
        })
    $json = [pscustomobject]@{ Verdict = 'ran'; Reason = ''; Analyzers = $AnalyzerText; Build = $BuildText; Kind = $Kind; Findings = @($view.Findings); Groups = $groups; Hints = @($view.Hints); Counts = $Grouped.Counts; Problems = $problemList }
    $lines = @($problemList | ForEach-Object { "review: static warning - $_" }) + @(Format-PaperReviewKindSummary -Static $Grouped -View $view -Kind $Kind)
    # review.staticAnalysis.sarifDir keeps the SARIF of a run that passed the proof only.
    return [pscustomobject]@{ ExitCode = 0; Json = $json; Lines = $lines; CopySarifTo = "$SarifDir" }
}

# ------------------------------------------------------------------ E. the complexity table (F235, F236)
# Sonar's three complexity rules are measured, not listed: the architecture review ranks the biggest. S3776 (cognitive), S1541 (cyclomatic)
# and S1067 (conditional operators in one expression) say the number and the limit in their message; SonarAnalyzer.CSharp 10.35 measured
# 2026-10-05 (plan W8): "... Cognitive Complexity from 48 to the 15 allowed.", "... Complexity of this method is 31 which is greater than 10
# authorized.", "... conditional operators (5) used in the expression (maximum allowed 3)." - the first whole number is the value, the second the limit.
$script:PaperReviewStaticRecipe = 1
$script:PaperReviewComplexityDefaults = [ordered]@{ cognitive = 15; cyclomatic = 10; expression = 3 }

function Get-PaperReviewSourceToken {
    <#
    .SYNOPSIS
    The text of a result in its source line: the identifier that starts at Column (a method name, get, a property), else the first 40
    characters of the line; with -Expression the rest of the line from Column, trimmed, 80 characters at most ("..." when cut).
    #>
    param($Lines, [string] $Path, [int] $Line, [int] $Column, [switch] $Expression)
    $arr = $null
    if ($null -ne $Lines -and $Lines.Contains($Path)) { $arr = @($Lines[$Path]) }
    if ($null -eq $arr -or $Line -lt 1 -or $Line -gt $arr.Count) { return '' }
    $text = "$($arr[$Line - 1])"
    $from = $text
    if ($Column -ge 1 -and ($Column - 1) -lt $text.Length) { $from = $text.Substring($Column - 1) }
    if ($Expression) {
        $e = $from.Trim()
        if ($e.Length -gt 80) { return $e.Substring(0, 80) + '...' }
        return $e
    }
    $m = [regex]::Match($from, '^@?[A-Za-z_][A-Za-z0-9_]*')
    if ($m.Success) { return $m.Value }
    $t = $text.Trim()
    if ($t.Length -gt 40) { $t = $t.Substring(0, 40) }
    return $t
}

function Get-PaperReviewComplexity {
    <#
    .SYNOPSIS
    The members, files and expressions over a Sonar complexity limit, from the flat results of ConvertFrom-PaperReviewSarif (E, F235).
    One result per rule, path, line and column whatever the SARIF file it came from; the two member rules merge by path and line. Members
    and expressions are ranked by the biggest excess (value / limit), then the cognitive value, the cyclomatic value, path, line; files by
    the sum of cognitive, the members, the path. A result whose message carries fewer than two numbers is NotRead, with its words.
    Counts: generated, outside scope, suppressed, outside repo (a result in none of them is a row). Lines: path -> the source lines.
    #>
    param($Results, [string] $RepoRoot, [string[]] $ScopePaths, [string] $ScopeMode, $Lines)
    $counts = [ordered]@{ 'generated' = 0; 'outside scope' = 0; 'suppressed' = 0; 'outside repo' = 0 }
    $scope = @{}
    foreach ($p in @($ScopePaths)) { if ($p) { $scope[$p.Replace('\', '/').ToLowerInvariant()] = $true } }
    $seen = @{}
    $items = @(); $notRead = @()
    foreach ($r in @($Results)) {
        if ($null -eq $r -or $script:PaperReviewComplexityRules -cnotcontains "$($r.RuleId)") { continue }
        if ($r.Suppressed) { $counts['suppressed']++; continue }
        $loc = ConvertTo-PaperReviewRepoPath -Uri $r.Uri -RepoRoot $RepoRoot
        if ($loc.Reason -eq 'generated') { $counts['generated']++; continue }
        if ($loc.Reason -eq 'outside-repo') { $counts['outside repo']++; continue }
        if ($loc.Reason -eq 'no-location') { continue }
        if ($ScopeMode -ne 'project' -and -not $scope.ContainsKey($loc.Path.ToLowerInvariant())) { $counts['outside scope']++; continue }
        $key = "$($r.RuleId)|$($loc.Path)|$($r.Line)|$($r.Column)".ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $nums = @([regex]::Matches("$($r.Message)", '\d+') | ForEach-Object { [long] $_.Value })
        if ($nums.Count -lt 2 -or $nums[1] -le 0) {
            $notRead += [pscustomobject]@{ RuleId = "$($r.RuleId)"; Path = $loc.Path; Line = [int] $r.Line; Where = "$($loc.Path):$($r.Line)"; Message = "$($r.Message)" }
            continue
        }
        $items += [pscustomobject]@{ RuleId = "$($r.RuleId)"; Path = $loc.Path; Line = [int] $r.Line; Column = [int] $r.Column; Value = $nums[0]; Limit = $nums[1] }
    }
    # The limits of this build: the first one each rule reported, else Sonar's default.
    $limits = [ordered]@{}
    foreach ($k in $script:PaperReviewComplexityDefaults.Keys) { $limits[$k] = [long] $script:PaperReviewComplexityDefaults[$k] }
    foreach ($pair in @(@('S3776', 'cognitive'), @('S1541', 'cyclomatic'), @('S1067', 'expression'))) {
        $first = @($items | Where-Object { $_.RuleId -ceq $pair[0] } | Select-Object -First 1)
        if ($first.Count -gt 0) { $limits[$pair[1]] = [long] $first[0].Limit }
    }
    $rankKey = {
        param($over, $cog, $cyc, $path, $line)
        $ov = (10000000000 - [long] [math]::Round($over * 1000000)).ToString('D12')
        "$ov`t$((1000000000 - [long] $cog).ToString('D10'))`t$((1000000000 - [long] $cyc).ToString('D10'))`t$path`t$(([int] $line).ToString('D9'))"
    }
    # Members: S3776 and S1541 of one place are one row.
    $byPlace = [ordered]@{}
    foreach ($i in @($items | Where-Object { $_.RuleId -ceq 'S3776' -or $_.RuleId -ceq 'S1541' })) {
        $pk = "$($i.Path)|$($i.Line)".ToLowerInvariant()
        if (-not $byPlace.Contains($pk)) {
            $byPlace[$pk] = [pscustomobject]@{ Name = (Get-PaperReviewSourceToken -Lines $Lines -Path $i.Path -Line $i.Line -Column $i.Column); Path = $i.Path; Line = $i.Line; Where = "$($i.Path):$($i.Line)"
                Cognitive = $null; CognitiveLimit = $null; Cyclomatic = $null; CyclomaticLimit = $null; Over = 0.0; WorstMetric = ''; WorstValue = 0; WorstLimit = 0 }
        }
        $m = $byPlace[$pk]
        if ($i.RuleId -ceq 'S3776') { $m.Cognitive = $i.Value; $m.CognitiveLimit = $i.Limit } else { $m.Cyclomatic = $i.Value; $m.CyclomaticLimit = $i.Limit }
    }
    foreach ($m in @($byPlace.Values)) {
        $oc = 0.0; $oy = 0.0
        if ($null -ne $m.Cognitive) { $oc = [double] $m.Cognitive / [double] $m.CognitiveLimit }
        if ($null -ne $m.Cyclomatic) { $oy = [double] $m.Cyclomatic / [double] $m.CyclomaticLimit }
        if ($oc -ge $oy) { $m.Over = $oc; $m.WorstMetric = 'cognitive'; $m.WorstValue = $m.Cognitive; $m.WorstLimit = $m.CognitiveLimit }
        else { $m.Over = $oy; $m.WorstMetric = 'cyclomatic'; $m.WorstValue = $m.Cyclomatic; $m.WorstLimit = $m.CyclomaticLimit }
    }
    $members = Sort-PaperReviewByKey @($byPlace.Values) { param($m) & $rankKey $m.Over $(if ($null -ne $m.Cognitive) { $m.Cognitive } else { 0 }) $(if ($null -ne $m.Cyclomatic) { $m.Cyclomatic } else { 0 }) $m.Path $m.Line }
    $members = @($members)
    # Files: the members of one path.
    $filePaths = [ordered]@{}
    foreach ($m in $members) {
        $fk = "$($m.Path)".ToLowerInvariant()
        if (-not $filePaths.Contains($fk)) { $filePaths[$fk] = [pscustomobject]@{ Path = $m.Path; Members = 0; CognitiveSum = [long] 0; Worst = $m.Name; WorstOver = $m.Over } }
        $f = $filePaths[$fk]
        $f.Members++
        if ($null -ne $m.Cognitive) { $f.CognitiveSum += [long] $m.Cognitive }
    }
    $files = Sort-PaperReviewByKey @($filePaths.Values) { param($f) "$((1000000000000 - [long] $f.CognitiveSum).ToString('D13'))`t$((1000000 - [int] $f.Members).ToString('D8'))`t$($f.Path)" }
    # Expressions.
    $exprs = @()
    foreach ($i in @($items | Where-Object { $_.RuleId -ceq 'S1067' })) {
        $exprs += [pscustomobject]@{ Path = $i.Path; Line = $i.Line; Where = "$($i.Path):$($i.Line)"; Operators = $i.Value; Limit = $i.Limit; Over = ([double] $i.Value / [double] $i.Limit)
            Text = (Get-PaperReviewSourceToken -Lines $Lines -Path $i.Path -Line $i.Line -Column $i.Column -Expression) }
    }
    $expressions = Sort-PaperReviewByKey @($exprs) { param($e) & $rankKey $e.Over 0 0 $e.Path $e.Line }
    return [pscustomobject]@{ Members = $members; Files = @($files); Expressions = @($expressions); NotRead = $notRead; Counts = $counts; Limits = $limits }
}

function Format-PaperReviewOver([double] $Over) {
    return 'x' + ([math]::Round($Over, 1, [MidpointRounding]::AwayFromZero)).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
}

function Format-PaperReviewComplexity {
    <#
    .SYNOPSIS
    The "## Complexity" section of the architecture report (E): the intro with the limits and what was not listed, then the three tables
    (members, files, expressions - the first Top rows of each, "top N of M") and the results whose numbers could not be read. Nothing
    over a limit: the section says so and never "clean". NotVerifiable: the section says why and has no table. Source (F252): a line, when the table comes from the
    SonarQube issues and not from a build, right after the heading.
    #>
    param($Complexity, [int] $Top, [string] $ReportDir, [string] $RepoRoot = '', [string] $NotVerifiable = '', [string] $Source = '')
    $out = @('## Complexity (Sonar S3776, S1541, S1067)', '')
    if ($NotVerifiable) { return @($out + "Not verifiable: $NotVerifiable.") }
    if ($Source) { $out += @($Source, '') }
    $lim = $Complexity.Limits
    $defaults = $script:PaperReviewComplexityDefaults
    $changed = $false
    foreach ($k in $defaults.Keys) { if ($null -ne $lim -and $lim.Contains($k) -and [long] $lim[$k] -ne [long] $defaults[$k]) { $changed = $true } }
    $c = $Complexity.Counts
    $cog = if ($null -ne $lim) { $lim['cognitive'] } else { $defaults['cognitive'] }
    $cyc = if ($null -ne $lim) { $lim['cyclomatic'] } else { $defaults['cyclomatic'] }
    $exp = if ($null -ne $lim) { $lim['expression'] } else { $defaults['expression'] }
    $why = if ($changed) { "the limits of this build, set by the project's configuration" } else { "Sonar's defaults; the project's own configuration can change a limit or turn a rule off" }
    $out += "Only what is over a limit is reported: cognitive complexity over $cog (S3776), cyclomatic complexity over $cyc (S1541), more than $exp conditional operators in one expression (S1067) - $why. Not listed: generated $($c['generated']), outside scope $($c['outside scope']), suppressed $($c['suppressed']), outside repo $($c['outside repo'])."
    $members = @($Complexity.Members); $files = @($Complexity.Files); $exprs = @($Complexity.Expressions); $notRead = @($Complexity.NotRead)
    if ($members.Count -eq 0 -and $exprs.Count -eq 0 -and $notRead.Count -eq 0) {
        return @($out + @('', 'No member or expression is over a limit - or the project''s configuration turned the rules off.'))
    }
    $link = { param($path, $text) "[$text]($(Get-PaperReviewReportLink $ReportDir $path $RepoRoot))" }
    $cell = { param($v, $l) if ($null -eq $v) { '-' } else { "$v / $l" } }
    $code = { param($t) '`' + ("$t".Replace('|', '\|')) + '`' }
    if ($members.Count -gt 0) {
        $n = [math]::Min($Top, $members.Count)
        $out += @('', "### Members - top $n of $($members.Count)", '', '| # | Member | Where | Cognitive | Cyclomatic | Over the limit |', '| --- | --- | --- | --- | --- | --- |')
        for ($i = 0; $i -lt $n; $i++) {
            $m = $members[$i]
            $out += "| $($i + 1) | $(& $code $m.Name) | $(& $link $m.Path $m.Where) | $(& $cell $m.Cognitive $m.CognitiveLimit) | $(& $cell $m.Cyclomatic $m.CyclomaticLimit) | $(Format-PaperReviewOver $m.Over) |"
        }
        $nf = [math]::Min($Top, $files.Count)
        $out += @('', "### Files - top $nf of $($files.Count)", '', '| # | File | Members over a limit | Cognitive sum | Worst member |', '| --- | --- | --- | --- | --- |')
        for ($i = 0; $i -lt $nf; $i++) {
            $f = $files[$i]
            $out += "| $($i + 1) | $(& $link $f.Path $f.Path) | $($f.Members) | $($f.CognitiveSum) | $(& $code $f.Worst) $(Format-PaperReviewOver $f.WorstOver) |"
        }
    }
    if ($exprs.Count -gt 0) {
        $ne = [math]::Min($Top, $exprs.Count)
        $out += @('', "### Expressions - top $ne of $($exprs.Count)", '', '| # | Where | Conditional operators | Over the limit | Expression |', '| --- | --- | --- | --- | --- |')
        for ($i = 0; $i -lt $ne; $i++) {
            $e = $exprs[$i]
            $out += "| $($i + 1) | $(& $link $e.Path $e.Where) | $($e.Operators) / $($e.Limit) | $(Format-PaperReviewOver $e.Over) | $(& $code $e.Text) |"
        }
    }
    if ($notRead.Count -gt 0) {
        $out += @('', '### Not read', '', '| Rule | Where | Message |', '| --- | --- | --- |')
        foreach ($r in $notRead) { $out += "| $($r.RuleId) | $(& $link $r.Path $r.Where) | $(ConvertTo-PaperReviewCell $r.Message) |" }
    }
    return $out
}

function Format-PaperReviewComplexityLine {
    <#
    .SYNOPSIS
    The line the static lane prints about the table (E): how many members and expressions are over a limit, how many results could not
    be read, and the worst; "none over a limit" says the project may have turned the rules off, never "clean"; not verifiable says why.
    #>
    param($Complexity, [string] $NotVerifiable = '')
    if ($NotVerifiable) { return "review: complexity not verifiable - $NotVerifiable" }
    $members = @($Complexity.Members); $exprs = @($Complexity.Expressions)
    if ($members.Count -eq 0 -and $exprs.Count -eq 0) {
        $l = $Complexity.Limits
        $cog = if ($null -ne $l) { $l['cognitive'] } else { 15 }
        $cyc = if ($null -ne $l) { $l['cyclomatic'] } else { 10 }
        $exp = if ($null -ne $l) { $l['expression'] } else { 3 }
        return "review: complexity - none over a limit (S3776 $cog, S1541 $cyc, S1067 $exp), or the project turned the rules off"
    }
    $worst = ''
    if ($members.Count -gt 0) { $m = $members[0]; $worst = "$($m.Name) $($m.Where) $($m.WorstMetric) $($m.WorstValue)/$($m.WorstLimit)" }
    else { $e = $exprs[0]; $worst = "expression $($e.Where) $($e.Operators)/$($e.Limit) operators" }
    return "review: complexity - $($members.Count) members, $($exprs.Count) expressions over a limit ($(@($Complexity.NotRead).Count) not read); worst: $worst"
}

function New-PaperReviewSonarLintXml {
    <#
    .SYNOPSIS
    The SonarLint.xml of a run (E, F236): only the rules the profile gives a limit for (review.complexity.limits), with the parameter names
    measured on SonarAnalyzer.CSharp 10.35 (plan W8): S3776 threshold and propertyThreshold, S1541 maximumFunctionComplexityThreshold,
    S1067 max. '' when no limit is given.
    #>
    param($Limits)
    $has = { param($k) $null -ne $Limits -and $Limits.Contains($k) }
    $rules = @()
    if (& $has 'cognitive') { $rules += , @('S3776', @(@('threshold', $Limits['cognitive']), @('propertyThreshold', $Limits['cognitive']))) }
    if (& $has 'cyclomatic') { $rules += , @('S1541', @(, @('maximumFunctionComplexityThreshold', $Limits['cyclomatic']))) }
    if (& $has 'expression') { $rules += , @('S1067', @(, @('max', $Limits['expression']))) }
    if ($rules.Count -eq 0) { return '' }
    $l = @('<?xml version="1.0" encoding="utf-8"?>', '<AnalysisInput>', '  <Rules>')
    foreach ($r in $rules) {
        $l += @('    <Rule>', "      <Key>$($r[0])</Key>", '      <Parameters>')
        foreach ($p in $r[1]) { $l += @('        <Parameter>', "          <Key>$($p[0])</Key>", "          <Value>$([int] $p[1])</Value>", '        </Parameter>') }
        $l += @('      </Parameters>', '    </Rule>')
    }
    $l += @('  </Rules>', '</AnalysisInput>')
    return ($l -join "`r`n")
}

# ------------------------------------------------------------------ F.3 the key of a build that can be used again (F242)
function Get-PaperReviewStaticKey {
    <#
    .SYNOPSIS
    The SHA-256 (hex) of what makes one analyzer build the same as another (F.3): the recipe, the commit, every change that is not
    committed (Changes: "<XY> <path> <sha256 of the content | deleted>", sorted here), the analyzers and their versions (sorted), the
    globalconfig, the SonarLint.xml ("none" when there is not one) and the build line. Order in the input never matters.
    #>
    param([string] $Head, [string[]] $Changes, $Analyzers, [string] $GlobalConfigHash, [string] $SonarLintHash, [string] $BuildLine)
    $lines = @("recipe $($script:PaperReviewStaticRecipe)", "head $Head")
    $ch = [string[]] @($Changes | Where-Object { $_ })
    [Array]::Sort($ch, [StringComparer]::Ordinal)
    $lines += $ch
    $an = [string[]] @($Analyzers.Keys | ForEach-Object { "analyzer $_=$($Analyzers[$_])" })
    [Array]::Sort($an, [StringComparer]::Ordinal)
    $lines += $an
    $lines += "globalconfig $GlobalConfigHash"
    $lines += "sonarlint $(if ($SonarLintHash) { $SonarLintHash } else { 'none' })"
    $lines += "build $BuildLine"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))) } finally { $sha.Dispose() }
    return (-join ($bytes | ForEach-Object { $_.ToString('x2') }))
}
