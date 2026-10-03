# /qa static lane, the pure part (plan 2026-10-02-qa-command, table G): reading SARIF 2.1, repository paths,
# the kind and severity of a rule, grouping into findings, rule links, the generated .targets and restore
# project, which DLLs of an analyzer package go to the compiler, project style, the NuGet cache line, the
# proof that each analyzer ran, and the Sonar rule table. No I/O: qa.ps1 reads files and starts processes.
# Dot-sourced by qa.ps1 and by qa-plan.ps1's callers; declares no param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Rules the kit calls a bug whatever the analyzer's category says. Titles checked against each rule's page
# (Get-PaperQaRuleLink) on 2026-10-02; a code whose page title changes leaves this table, it is not swapped.
$script:PaperQaKitKinds = [ordered]@{
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

$script:PaperQaSeverityRank = @{ 'critical' = 0; 'major' = 1; 'minor' = 2; 'info' = 3 }
$script:PaperQaKindRank = @{ 'vulnerability' = 0; 'bug' = 1; 'smell' = 2 }
# CS9057: an analyzer built for a newer compiler than the one running is dropped (measured risk, plan T16).
$script:PaperQaLoadProblemCodes = @('CS8032', 'CS8034', 'CS9057', 'AD0001')
$script:PaperQaStaticCountNames = @('outside scope', 'generated', 'suppressed', 'compiler warnings', 'ignored by profile', 'outside repo', 'project-level', 'analyzer problems')

# Sorts by a string key in ordinal order; culture order ignores '-' and folds case, which reorders paths.
function Sort-PaperQaByKey($Items, [scriptblock] $Key) {
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
function Test-PaperQaGeneratedPath([string] $Path) {
    $parts = @("$Path".Replace('\', '/') -split '/' | Where-Object { $_ -ne '' -and $_ -ne '.' })
    if ($parts.Count -eq 0) { return $false }
    foreach ($p in $parts) { if (@('obj', 'bin', 'node_modules') -contains $p.ToLowerInvariant()) { return $true } }
    if ($parts[0].ToLowerInvariant() -eq 'packages') { return $true }
    $leaf = $parts[-1].ToLowerInvariant()
    foreach ($end in @('.g.cs', '.g.i.cs', '.designer.cs', '.assemblyattributes.cs')) { if ($leaf.EndsWith($end)) { return $true } }
    return $false
}

function Get-PaperQaProp($Object, [string] $Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) { return $Object[$Name] }
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function ConvertFrom-PaperQaSarif {
    <#
    .SYNOPSIS
    One SARIF log (ConvertFrom-Json output) to flat results: RuleId, Level, Message, Uri, Line, Column,
    Category, Title, Suppressed, File. A log that is not SARIF 2.1 gives a problem and no result.
    #>
    param($Sarif, [string] $File)
    $results = @()
    $problems = @()
    $version = "$(Get-PaperQaProp $Sarif 'version')"
    if ($version -ne '2.1.0') {
        $problems += "${File}: not SARIF 2.1 (version $version)"
        return [pscustomobject]@{ Results = $results; Problems = $problems }
    }
    foreach ($run in @(Get-PaperQaProp $Sarif 'runs')) {
        if ($null -eq $run) { continue }
        $rules = @(Get-PaperQaProp (Get-PaperQaProp (Get-PaperQaProp $run 'tool') 'driver') 'rules' | Where-Object { $null -ne $_ })
        $byId = @{}
        foreach ($rule in $rules) { $rid = "$(Get-PaperQaProp $rule 'id')"; if ($rid -and -not $byId.ContainsKey($rid)) { $byId[$rid] = $rule } }
        foreach ($r in @(Get-PaperQaProp $run 'results')) {
            if ($null -eq $r) { continue }
            $ruleId = "$(Get-PaperQaProp $r 'ruleId')"
            $index = Get-PaperQaProp $r 'ruleIndex'
            $rule = $null
            if ($null -ne $index -and [int] $index -ge 0 -and [int] $index -lt $rules.Count) {
                $candidate = $rules[[int] $index]
                if (-not $ruleId) { $ruleId = "$(Get-PaperQaProp $candidate 'id')" }
                if ("$(Get-PaperQaProp $candidate 'id')" -eq $ruleId) { $rule = $candidate }
            }
            if ($null -eq $rule -and $byId.ContainsKey($ruleId)) { $rule = $byId[$ruleId] }
            $level = "$(Get-PaperQaProp $r 'level')"
            if (-not $level) { $level = 'warning' }
            $message = Get-PaperQaProp $r 'message'
            if ($message -isnot [string]) { $message = "$(Get-PaperQaProp $message 'text')" }
            $physical = Get-PaperQaProp (@(Get-PaperQaProp $r 'locations')[0]) 'physicalLocation'
            $uri = "$(Get-PaperQaProp (Get-PaperQaProp $physical 'artifactLocation') 'uri')"
            $region = Get-PaperQaProp $physical 'region'
            $line = 0; $column = 0
            if ($null -ne (Get-PaperQaProp $region 'startLine')) { $line = [int] (Get-PaperQaProp $region 'startLine') }
            if ($null -ne (Get-PaperQaProp $region 'startColumn')) { $column = [int] (Get-PaperQaProp $region 'startColumn') }
            $suppressions = Get-PaperQaProp $r 'suppressions'
            $results += [pscustomobject]@{
                RuleId     = $ruleId
                Level      = $level
                Message    = "$message"
                Uri        = $uri
                Line       = $line
                Column     = $column
                Category   = "$(Get-PaperQaProp (Get-PaperQaProp $rule 'properties') 'category')"
                Title      = "$(Get-PaperQaProp (Get-PaperQaProp $rule 'shortDescription') 'text')"
                Suppressed = ($null -ne $suppressions -and @($suppressions | Where-Object { $null -ne $_ }).Count -gt 0)
                File       = $File
            }
        }
    }
    return [pscustomobject]@{ Results = $results; Problems = $problems }
}

function ConvertTo-PaperQaRepoPath {
    <#
    .SYNOPSIS
    A SARIF uri to a repository path with '/': Reason '' inside the repository, 'no-location' with no uri,
    'outside-repo' outside it, 'generated' for a file the build wrote (Test-PaperQaGeneratedPath).
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
    if (Test-PaperQaGeneratedPath $rel) { $reason = 'generated' }
    return [pscustomobject]@{ Path = $rel; Reason = $reason }
}

function Get-PaperQaFallbackSeverity([string] $Kind, [string] $Level) {
    if ($Kind -eq 'vulnerability' -or $Kind -eq 'bug') { return 'major' }
    if (@('note', 'none') -contains "$Level".ToLowerInvariant()) { return 'info' }
    return 'minor'
}

function Get-PaperQaRuleKind {
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
    elseif ($script:PaperQaKitKinds.Contains($RuleId)) { $kind = 'bug'; $source = 'table' }
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
        else { $severity = Get-PaperQaFallbackSeverity $kind $Level }
    }
    return [pscustomobject]@{ Kind = $kind; Severity = $severity; Hotspot = $hotspot; Source = $source }
}

function Get-PaperQaRuleLink {
    param([string] $RuleId)
    if ($RuleId -match '^S(\d+)$') { return "https://rules.sonarsource.com/csharp/RSPEC-$($Matches[1])" }
    if ($RuleId -match '^CA(\d+)$') { return "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca$($Matches[1])" }
    if ($RuleId -match '^IDE(\d+)$') { return "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide$($Matches[1])" }
    if ($RuleId -match '^RCS(\d+)$') { return "https://josefpihrt.github.io/docs/roslynator/analyzers/RCS$($Matches[1])" }
    return ''
}

function Group-PaperQaStatic {
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
    foreach ($n in $script:PaperQaStaticCountNames) { $counts[$n] = 0 }
    $load = @()
    $scope = @{}
    foreach ($p in @($ScopePaths)) { if ($p) { $scope[$p.Replace('\', '/').ToLowerInvariant()] = $true } }
    $byKey = [ordered]@{}

    foreach ($r in @($Results)) {
        if ($null -eq $r) { continue }
        $id = "$($r.RuleId)"
        if ($script:PaperQaLoadProblemCodes -contains $id) {
            $counts['analyzer problems']++
            $load += [pscustomobject]@{ RuleId = $id; Message = "$($r.Message)" }
            continue
        }
        if ($id -match '^CS\d+$') { $counts['compiler warnings']++; continue }
        if ($r.Suppressed) { $counts['suppressed']++; continue }
        $loc = ConvertTo-PaperQaRepoPath -Uri $r.Uri -RepoRoot $RepoRoot
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
        $kind = Get-PaperQaRuleKind -RuleId $id -Category $r.Category -Level $r.Level -ProfileKinds $ProfileKinds -SonarTable $SonarTable
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
            Title = $title; Level = $r.Level; Sources = @($r.File); Link = (Get-PaperQaRuleLink -RuleId $id); Status = 'machine'
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
        $members = Sort-PaperQaByKey $groupMap[$gk] { param($x) "$($x.Path.ToLowerInvariant())`t$(([int] $x.Line).ToString('D9'))`t$(([int] $x.Column).ToString('D9'))" }
        $best = @($members | ForEach-Object { $script:PaperQaSeverityRank[$_.Severity] } | Sort-Object)[0]
        $first = $members[0]
        $title = @($members | Where-Object { $_.Title } | Select-Object -First 1 | ForEach-Object { $_.Title })
        $groupTitle = if ($title.Count -gt 0) { $title[0] } else { $first.Message }
        $severity = @($script:PaperQaSeverityRank.Keys | Where-Object { $script:PaperQaSeverityRank[$_] -eq $best })[0]
        $groups += [pscustomobject]@{
            RuleId = $first.RuleId; Kind = $first.Kind; Severity = $severity; SeverityRank = [int] $best; Count = $members.Count
            Title = $groupTitle; Link = $first.Link; Source = $first.Source; Hotspot = [bool] $first.Hotspot; Findings = $members
        }
    }
    $groups = Sort-PaperQaByKey $groups { param($g) "$($script:PaperQaKindRank[$g.Kind])`t$($g.SeverityRank)`t$((1000000000 - $g.Count).ToString('D10'))`t$($g.RuleId)" }
    $findings = @()
    $n = 0
    foreach ($g in $groups) {
        foreach ($f in $g.Findings) { $n++; $f.Id = "ST-$n"; $findings += $f }
    }
    return [pscustomobject]@{ Findings = $findings; Groups = $groups; Counts = $counts; LoadProblems = $load }
}

function Format-PaperQaStaticSummary {
    param($Static)
    $f = @($Static.Findings)
    $v = @($f | Where-Object { $_.Kind -eq 'vulnerability' }).Count
    $b = @($f | Where-Object { $_.Kind -eq 'bug' }).Count
    $s = @($f | Where-Object { $_.Kind -eq 'smell' }).Count
    $c = $Static.Counts
    return ("qa: static - {0} vulnerability, {1} bug, {2} smell in scope; not listed: outside scope {3}, generated {4}, suppressed {5}, compiler warnings {6}, ignored by profile {7}, outside repo {8}" -f `
            $v, $b, $s, $c['outside scope'], $c['generated'], $c['suppressed'], $c['compiler warnings'], $c['ignored by profile'], $c['outside repo'])
}

# MSBuild reads % ; $ @ ' in an item or property as syntax; XML then needs & < > ".
function ConvertTo-PaperQaMsbuildText([string] $Text) {
    $t = "$Text".Replace('%', '%25').Replace(';', '%3B').Replace('$', '%24').Replace('@', '%40').Replace("'", '%27')
    return $t.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
}

function New-PaperQaTargets {
    <#
    .SYNOPSIS
    The .targets a /qa run imports into every project of the build through CustomAfterMicrosoftCommonTargets
    (G.2): analyzers on, warnings never errors, SARIF per project and framework, the kit's .globalconfig, the
    analyzer DLLs (a LegacyOnly one only where the SDK does not bring its own), the analyzer list after
    CoreCompile, and the file itself as a compile input so the compiler runs again.
    #>
    param([string] $Run, [string] $SarifDir, [string] $AnalyzerListDir, [string] $GlobalConfig, [string[]] $Analyzers, [string[]] $LegacyOnly)
    $legacy = @{}
    foreach ($l in @($LegacyOnly)) { if ($l) { $legacy[$l.ToLowerInvariant()] = $true } }
    $sarif = (ConvertTo-PaperQaMsbuildText $SarifDir.TrimEnd('\'))
    $list = (ConvertTo-PaperQaMsbuildText $AnalyzerListDir.TrimEnd('\'))
    $lines = @(
        '<Project>',
        "  <!-- paper-kit /qa run ${Run}: imported through CustomAfterMicrosoftCommonTargets. Generated; never commit. -->",
        '  <PropertyGroup>',
        '    <RunAnalyzers>true</RunAnalyzers>',
        '    <RunAnalyzersDuringBuild>true</RunAnalyzersDuringBuild>',
        '    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>',
        '    <CodeAnalysisTreatWarningsAsErrors>false</CodeAnalysisTreatWarningsAsErrors>',
        '    <MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors>',
        '    <WarningsAsErrors></WarningsAsErrors>',
        '    <PaperQaSuffix Condition="''$(TargetFramework)'' != ''''">-$(TargetFramework)</PaperQaSuffix>',
        ('    <ErrorLog>' + $sarif + '\$(MSBuildProjectName)$(PaperQaSuffix).sarif,version=2.1</ErrorLog>'),
        '  </PropertyGroup>',
        '  <ItemGroup>',
        '    <CustomAdditionalCompileInputs Include="$(MSBuildThisFileFullPath)" />'
    )
    if ($GlobalConfig) {
        $lines += ('    <GlobalAnalyzerConfigFiles Include="' + (ConvertTo-PaperQaMsbuildText $GlobalConfig) + '" />')
        # Measured (plan T8, X01): Microsoft.Managed.Core.targets copies GlobalAnalyzerConfigFiles into
        # EditorConfigFiles - what Csc reads - while it is evaluated, before this file is imported, so the item
        # above alone never reaches the compiler. EditorConfigFiles does.
        $lines += ('    <EditorConfigFiles Include="' + (ConvertTo-PaperQaMsbuildText $GlobalConfig) + '" />')
    }
    foreach ($a in @($Analyzers)) {
        if (-not $a) { continue }
        $line = '    <Analyzer Include="' + (ConvertTo-PaperQaMsbuildText $a) + '"'
        if ($legacy.ContainsKey($a.ToLowerInvariant())) { $line += ' Condition="''$(UsingMicrosoftNETSdk)'' != ''true''"' }
        $lines += ($line + ' />')
    }
    $lines += @(
        '  </ItemGroup>',
        '  <Target Name="PaperQaAnalyzerList" AfterTargets="CoreCompile">',
        ('    <WriteLinesToFile File="' + $list + '\analyzers-$(MSBuildProjectName)$(PaperQaSuffix).txt" Lines="@(Analyzer)" Overwrite="true" />'),
        '  </Target>',
        '</Project>'
    )
    return ($lines -join "`r`n")
}

function New-PaperQaRestoreProject {
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
        $lines += ('    <PackageDownload Include="' + (ConvertTo-PaperQaMsbuildText $id) + '" Version="[' + (ConvertTo-PaperQaMsbuildText "$($Packages[$id])") + ']" />')
    }
    $lines += @('  </ItemGroup>', '</Project>')
    return ($lines -join "`r`n")
}

function Get-PaperQaAnalyzerDlls {
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

function Get-PaperQaProjectStyle {
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

function Get-PaperQaAnalyzerProof {
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
    $kind = switch ("$(Get-PaperQaProp $Rspec 'type')") {
        'BUG' { 'bug' } 'VULNERABILITY' { 'vulnerability' } 'CODE_SMELL' { 'smell' } 'SECURITY_HOTSPOT' { 'hotspot' } default { 'smell' }
    }
    $severity = switch ("$(Get-PaperQaProp $Rspec 'defaultSeverity')") {
        'Blocker' { 'critical' } 'Critical' { 'critical' } 'Major' { 'major' } 'Minor' { 'minor' } default { 'info' }
    }
    $title = ("$(Get-PaperQaProp $Rspec 'title')" -replace '[\t\r\n]', ' ').Trim()
    # Written so this file does not itself name the hosts it removes.
    $title = [regex]::Replace($title, '(?<![a-z0-9])(r[e]vit|autoc[a]d|ac[a]d)(?![a-z0-9])', 'host', 'IgnoreCase')
    $standards = Get-PaperQaProp $Rspec 'securityStandards'
    $cwe = @(@(Get-PaperQaProp $standards 'CWE') | Where-Object { $null -ne $_ -and "$_" -ne '' } | ForEach-Object { "CWE-$_" }) -join ';'
    $owasp = @(@(Get-PaperQaProp $standards 'OWASP') | Where-Object { $null -ne $_ -and "$_" -ne '' } | ForEach-Object { "$_" }) -join ';'
    return (@($Id, $kind, $severity, $title, $cwe, $owasp) -join "`t")
}

function Read-PaperQaSonarTable {
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

# ------------------------------------------------------------------ G.1: the decisions of `qa.ps1 static`
# qa.ps1 reads files and starts processes; what it does with what it read is decided here.

$script:PaperQaNetAnalyzersId = 'Microsoft.CodeAnalysis.NetAnalyzers'

# Step 1: a static lane that does not run exits 5 with its reason; 0 goes on.
function Get-PaperQaStaticGate {
    param($Row)
    if ($null -eq $Row -or $Row.State -eq 'not applicable') { return [pscustomobject]@{ ExitCode = 5; Line = "qa: static NOT APPLICABLE - $($Row.Reason)" } }
    if ($Row.State -ne 'run') { return [pscustomobject]@{ ExitCode = 5; Line = "qa: static skipped - $($Row.Reason)" } }
    return [pscustomobject]@{ ExitCode = 0; Line = '' }
}

# The folder NuGet keeps a version in (find-bug FB16): lower case, at least three parts, a fourth part of 0
# dropped - 4.12 -> 4.12.0, 10.30.0.0 -> 10.30.0 - so a pin written either way finds its package.
function ConvertTo-PaperQaNugetVersion([string] $Version) {
    $v = "$Version".Trim().ToLowerInvariant()
    $m = [regex]::Match($v, '^(\d+(?:\.\d+)*)(-.*)?$')
    if (-not $m.Success) { return $v }
    $parts = @($m.Groups[1].Value -split '\.' | ForEach-Object { [string] [long] $_ })
    while ($parts.Count -lt 3) { $parts += '0' }
    if ($parts.Count -eq 4 -and $parts[3] -eq '0') { $parts = @($parts[0..2]) }
    return (($parts -join '.') + $m.Groups[2].Value)
}

# The restore project targets net<major>.0 of the running SDK: its targeting pack ships with the SDK.
function Get-PaperQaTargetFramework {
    param([string] $VersionText)
    $m = [regex]::Match("$VersionText".Trim(), '^(\d+)\.')
    if (-not $m.Success) { return '' }
    return "net$($m.Groups[1].Value).0"
}

# Step 3: the packages to download - every analyzer not "off", except NetAnalyzers, which SDK-style projects
# get from the SDK; it is downloaded only when an old-format project needs it. Styles: project path -> sdk|legacy.
function Get-PaperQaStaticPackages {
    param($Analyzers, $Styles)
    $values = @()
    if ($null -ne $Styles) { $values = @($Styles.Values) }
    $hasLegacy = @($values | Where-Object { $_ -eq 'legacy' }).Count -gt 0
    $hasSdk = @($values | Where-Object { $_ -eq 'sdk' }).Count -gt 0
    $packages = [ordered]@{}
    foreach ($id in @($Analyzers.Keys)) {
        if ($Analyzers[$id] -eq 'off') { continue }
        if ($id -eq $script:PaperQaNetAnalyzersId -and -not $hasLegacy) { continue }
        $packages[$id] = $Analyzers[$id]
    }
    return [pscustomobject]@{ Packages = $packages; HasLegacy = $hasLegacy; HasSdk = $hasSdk }
}

# A failed `dotnet restore`: the package its first NU line names (else the first package), with that line.
function Get-PaperQaRestoreFailure {
    param([string[]] $Lines, $Packages, [int] $ExitCode)
    $nu = @($Lines | Where-Object { "$_" -match '\bNU\d{4}\b' } | Select-Object -First 1)
    $line = if ($nu.Count -gt 0) { "$($nu[0])".Trim() } else { "dotnet restore exit $ExitCode" }
    $ids = @($Packages.Keys)
    $culprit = @($ids | Where-Object { $line.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object -First 1)
    $id = if ($culprit.Count -gt 0) { $culprit[0] } else { $ids[0] }
    return "could not download $id $($Packages[$id]): $line"
}

# Every downloaded DLL goes to the compiler; the NetAnalyzers package's only where the SDK brings none.
function Get-PaperQaAnalyzerPlug {
    param($DllsById)
    $all = @(); $legacy = @()
    foreach ($id in @($DllsById.Keys)) {
        $all += @($DllsById[$id])
        if ($id -eq $script:PaperQaNetAnalyzersId) { $legacy += @($DllsById[$id]) }
    }
    return [pscustomobject]@{ Analyzers = $all; LegacyOnly = $legacy }
}

# What the proof (G.5) expects in the compiler's analyzer list, per analyzer that is not "off".
function Get-PaperQaExpectedDlls {
    param($Analyzers, [bool] $HasSdk, $DllsById)
    $expected = [ordered]@{}
    foreach ($id in @($Analyzers.Keys)) {
        if ($Analyzers[$id] -eq 'off') { continue }
        $names = @()
        if ($id -eq $script:PaperQaNetAnalyzersId -and $HasSdk) { $names += @('Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll', 'Microsoft.CodeAnalysis.NetAnalyzers.dll') }
        if ($null -ne $DllsById -and $DllsById.Contains($id)) { $names += @($DllsById[$id] | ForEach-Object { ("$_" -split '[\\/]')[-1] }) }
        if ($names.Count -gt 0) { $expected[$id] = @($names | Select-Object -Unique) }
    }
    return $expected
}

# Step 6: the environment of the build verb's process - MSBuild reads environment variables as properties;
# PowerShell 5.1 cannot pass MSBuild switches through the verb.
function Get-PaperQaBuildEnvironment {
    param([string] $Targets, $Analyzers)
    $envs = [ordered]@{ 'CustomAfterMicrosoftCommonTargets' = $Targets; 'MSBUILDDISABLENODEREUSE' = '1' }
    # Off is set false: left out, the SDK runs its NetAnalyzers anyway (measured, find-bug FB22).
    if ($Analyzers.Contains($script:PaperQaNetAnalyzersId)) { $envs['EnableNETAnalyzers'] = $(if ($Analyzers[$script:PaperQaNetAnalyzersId] -eq 'off') { 'false' } else { 'true' }) }
    return $envs
}

# Steps 6-7: $null for a green build that wrote SARIF; else the reason (F58) with the command the verb ran
# (paperflow's "build -> <command>" line) and the last five lines - a red build and one with no SARIF alike.
function Get-PaperQaBuildFailure {
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
    $pattern = if ($External) { '^qa: build -> ' } else { '^paperflow: build -> ' }
    $command = @($all | Where-Object { "$_" -match $pattern } | Select-Object -First 1)
    $tail = @()
    if ($command.Count -gt 0 -and $last -notcontains $command[0]) { $tail += $command[0] }
    $tail += $last
    return [pscustomobject]@{ Reason = $reason; Tail = $tail }
}

# The "Analyzers:" line of the report.
function Format-PaperQaAnalyzerText {
    param($Analyzers, [bool] $HasLegacy)
    $parts = @()
    foreach ($id in @($Analyzers.Keys)) {
        $v = $Analyzers[$id]
        if ($id -eq $script:PaperQaNetAnalyzersId -and $v -ne 'off') {
            $t = "$id SDK built-in for SDK-style projects"
            if ($HasLegacy) { $t += ", $v for the others" }
            $parts += $t
        }
        else { $parts += "$id $v" }
    }
    return ($parts -join ', ')
}

# Steps 8-9: static.json, the lines to print and the exit code, from the grouped results and the proof.
function Get-PaperQaStaticOutcome {
    param($Grouped, $Proof, [string] $AnalyzerText, [string] $BuildText, [string[]] $Problems, [string] $SarifDir, [int] $SarifCount = -1)
    $proofs = @($Proof | Where-Object { $null -ne $_ })
    $problemList = @($Problems | Where-Object { $_ })
    # Every SARIF file unreadable (not JSON, not 2.1) is no result at all: not verifiable (F58, find-bug FB15).
    if ($SarifCount -ge 0 -and $problemList.Count -ge [math]::Max(1, $SarifCount)) {
        $reason = "no SARIF file could be read: $($problemList[0])"
        $json = [pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = $AnalyzerText; Build = $BuildText; Findings = @(); Groups = @(); Counts = $Grouped.Counts; Problems = $problemList }
        return [pscustomobject]@{ ExitCode = 4; Json = $json; Lines = @("qa: static not verifiable - $reason"); CopySarifTo = '' }
    }
    if ($proofs.Count -gt 0) {
        $first = $proofs[0]
        $reason = if ($first.Id -eq '*') { "analyzer did not run: $($first.Reason)" } else { "analyzer $($first.Id) did not run: $($first.Reason)" }
        $json = [pscustomobject]@{ Verdict = 'not verifiable'; Reason = $reason; Analyzers = $AnalyzerText; Build = $BuildText; Findings = @(); Groups = @(); Counts = $Grouped.Counts; Problems = $problemList }
        return [pscustomobject]@{ ExitCode = 4; Json = $json; Lines = @("qa: static not verifiable - $reason"); CopySarifTo = '' }
    }
    $groups = @($Grouped.Groups | ForEach-Object {
            [pscustomobject]@{ RuleId = $_.RuleId; Kind = $_.Kind; Severity = $_.Severity; SeverityRank = $_.SeverityRank; Count = $_.Count; Title = $_.Title; Link = $_.Link; Source = $_.Source; Hotspot = $_.Hotspot; Ids = @($_.Findings | ForEach-Object { $_.Id }) }
        })
    $json = [pscustomobject]@{ Verdict = 'ran'; Reason = ''; Analyzers = $AnalyzerText; Build = $BuildText; Findings = @($Grouped.Findings); Groups = $groups; Counts = $Grouped.Counts; Problems = $problemList }
    $lines = @($problemList | ForEach-Object { "qa: static warning - $_" }) + @(Format-PaperQaStaticSummary -Static $Grouped)
    # qa.staticAnalysis.sarifDir keeps the SARIF of a run that passed the proof only.
    return [pscustomobject]@{ ExitCode = 0; Json = $json; Lines = $lines; CopySarifTo = "$SarifDir" }
}
