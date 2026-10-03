# /qa, the pure part (plan 2026-10-02-qa-command, tables B, C, D, E, F, H, I, J): the profile key, the scope,
# the lanes, the estimate and batches, approval, the finding format and coverage, the verification queue
# and verdicts, the bug ledger proposals and the report. No I/O: qa.ps1 reads git, the disk and processes.
# The caller dot-sources paperflow/review-files-plan.ps1 (Test-PaperSecretName), paperflow/tasks-gate-plan.ps1
# (ConvertTo-PaperGlobKey, ConvertTo-PaperGlobRegex) and qa-sarif-plan.ps1
# (Test-PaperQaGeneratedPath, Sort-PaperQaByKey) first, and qa-external-plan.ps1 (Get-PaperQaBatchRules, used
# only for an external repository) before calling with -Rules; declares no param() block.
# The lanes (D.1, ADR-0035): a lane that does not apply says so before any choice is applied, and the ui
# lane runs on every host with a user interface - all but $script:PaperQaNoUiHosts.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperQaLaneNames = @('static', 'security', 'bug', 'architecture', 'smell', 'ui')
$script:PaperQaLlmLanes = @('security', 'bug', 'architecture', 'smell', 'ui')
$script:PaperQaDefaultAnalyzers = [ordered]@{
    'Microsoft.CodeAnalysis.NetAnalyzers' = '10.0.401'
    'Roslynator.Analyzers'                = '5.0.0'
    'SonarAnalyzer.CSharp'                = '10.35.0.4138'
}
$script:PaperQaVersionPattern = '^\d+(\.\d+){1,3}(-[0-9A-Za-z.-]+)?$'
$script:PaperQaKindNames = @('bug', 'vulnerability', 'smell', 'ignore')
$script:PaperQaSeverities = @('critical', 'major', 'minor', 'info')

# Estimate constants (table E). Not calibrated yet: a real run's token count is what tunes them.
$script:PaperQaLaneOverhead = 25000
$script:PaperQaBytesPerToken = 4
$script:PaperQaReadFactor = 1.5
$script:PaperQaImageTokens = 1600
$script:PaperQaVerifyTokens = 20000

# What each lane reports and the id prefix of its findings (D.3).
$script:PaperQaLaneKinds = @{ 'security' = 'vulnerability'; 'bug' = 'bug'; 'architecture' = 'architecture'; 'smell' = 'smell'; 'ui' = 'ux' }
$script:PaperQaLanePrefix = @{ 'security' = 'SEC'; 'bug' = 'BUG'; 'architecture' = 'ARC'; 'smell' = 'SML'; 'ui' = 'UI' }

$script:PaperQaCodeExt = @('.cs', '.vb', '.fs', '.ps1', '.psm1', '.psd1', '.py', '.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs', '.go', '.java', '.c', '.cc', '.cpp', '.h', '.hpp', '.rs', '.sql', '.sh')
$script:PaperQaConfigExt = @('.json', '.config', '.xml', '.yml', '.yaml', '.props', '.targets', '.csproj', '.vbproj', '.fsproj', '.sln', '.addin', '.manifest', '.toml', '.ini')
$script:PaperQaUiExt = @('.xaml', '.axaml', '.cshtml', '.razor', '.html', '.htm', '.css', '.scss', '.less', '.vue', '.svelte', '.tsx', '.jsx', '.dcl')
# The hosts with no user interface of their own; every other host has one (ADR-0035).
$script:PaperQaNoUiHosts = @('cli', 'ai')
$script:PaperQaProjectExt = @('.csproj', '.vbproj', '.fsproj', '.sln', '.props', '.targets')

function Format-PaperQaNumber([double] $Value) { return $Value.ToString('N0', [Globalization.CultureInfo]::InvariantCulture) }

function Test-PaperQaWholeNumber($Value, [long] $Min) {
    if ($Value -is [bool] -or $Value -is [string] -or $null -eq $Value) { return $false }
    # At most [int]::MaxValue: the value is used as an [int] (find-bug FB5).
    if ($Value -is [int] -or $Value -is [long] -or $Value -is [int16] -or $Value -is [byte]) { return ([long] $Value -ge $Min -and [long] $Value -le [int]::MaxValue) }
    if ($Value -is [double] -or $Value -is [decimal] -or $Value -is [single]) { return ([double] $Value -eq [math]::Floor([double] $Value) -and [double] $Value -ge $Min -and [double] $Value -le [int]::MaxValue) }
    return $false
}

# A path written relative to the repository root that stays inside it.
function Test-PaperQaInsideRepo([string] $Path) {
    $p = "$Path".Replace('\', '/')
    if (-not $p.Trim() -or -not (ConvertTo-PaperQaRelPath $p)) { return $false }
    if ($p -match '^[A-Za-z]:' -or $p.StartsWith('/')) { return $false }
    foreach ($seg in ($p -split '/')) { if ($seg -eq '..') { return $false } }
    return $true
}

function ConvertTo-PaperQaRelPath([string] $Path) {
    # '.' segments name the folder they sit in: ./src, src/., docs/./qa (find-bug FB13, FB20).
    return (@("$Path".Replace('\', '/') -split '/' | Where-Object { $_ -ne '' -and $_ -ne '.' }) -join '/')
}

function ConvertFrom-PaperQaProfile {
    <#
    .SYNOPSIS
    The qa key of a profile (table B) to a config with every default filled in; Errors lists each wrong
    key, one line each (F67). The profile is the map Read-PaperProfileFile gives, or $null.
    #>
    param($Profile)
    $errors = @()
    $hosts = @()
    $featureDocs = 'docs/features'
    $archDeclared = $false
    if ($null -ne $Profile) {
        $hosts = @($Profile['hosts'] | Where-Object { $_ } | ForEach-Object { "$_".ToLowerInvariant() })
        if ($Profile['featureDocs']) { $featureDocs = "$($Profile['featureDocs'])" }
        $arch = $Profile['architecture']
        if ($arch -is [System.Collections.IDictionary]) {
            $pf = @($arch['platformFree'] | Where-Object { $_ })
            $rv = @($arch['reviewers'] | Where-Object { $_ })
            $archDeclared = ($pf.Count -gt 0 -or $rv.Count -gt 0)
        }
    }
    $docsParent = ConvertTo-PaperQaRelPath $featureDocs
    $cut = $docsParent.LastIndexOf('/')
    $report = if ($cut -gt 0) { $docsParent.Substring(0, $cut) + '/qa' } else { 'qa' }
    $analyzers = [ordered]@{}
    foreach ($k in $script:PaperQaDefaultAnalyzers.Keys) { $analyzers[$k] = $script:PaperQaDefaultAnalyzers[$k] }
    $cfg = [pscustomobject]@{
        Errors = @(); Hosts = $hosts; FeatureDocs = $featureDocs; ArchitectureDeclared = $archDeclared
        Lanes = @{}; Exclude = @(); Report = $report; VerifyCap = 10; BatchTokens = 120000; UiScreens = ''
        Analyzers = $analyzers; GlobalConfig = ''; SarifDir = ''; Kinds = @{}
    }
    $q = $null
    if ($null -ne $Profile) { $q = $Profile['qa'] }
    if ($null -eq $q) { return $cfg }
    if ($q -isnot [System.Collections.IDictionary]) { $cfg.Errors = @('qa: not an object'); return $cfg }

    foreach ($key in @($q.Keys)) {
        $v = $q[$key]
        switch -CaseSensitive ($key) {
            'lanes' {
                if ($v -isnot [System.Collections.IDictionary]) { $errors += 'qa.lanes: not an object'; break }
                foreach ($l in @($v.Keys)) {
                    if ($script:PaperQaLaneNames -notcontains $l) { $errors += "qa.lanes.${l}: unknown lane ($($script:PaperQaLaneNames -join ', '))" }
                    elseif ($v[$l] -isnot [bool]) { $errors += "qa.lanes.${l}: not true or false" }
                    else { $cfg.Lanes[$l] = [bool] $v[$l] }
                }
            }
            'exclude' {
                $ok = ($v -is [System.Array]) -and (@($v | Where-Object { $_ -isnot [string] }).Count -eq 0)
                if (-not $ok) { $errors += 'qa.exclude: not a list of strings' } else { $cfg.Exclude = @($v) }
            }
            'report' {
                if ($v -isnot [string] -or -not (Test-PaperQaInsideRepo $v)) { $errors += 'qa.report: must be a folder inside the repository' }
                else { $cfg.Report = ConvertTo-PaperQaRelPath $v }
            }
            'verifyCap' {
                if (-not (Test-PaperQaWholeNumber $v 0)) { $errors += 'qa.verifyCap: not a whole number >= 0' } else { $cfg.VerifyCap = [int] $v }
            }
            'batchTokens' {
                if (-not (Test-PaperQaWholeNumber $v 20000)) { $errors += 'qa.batchTokens: not a whole number >= 20000' } else { $cfg.BatchTokens = [int] $v }
            }
            'ui' {
                if ($v -isnot [System.Collections.IDictionary]) { $errors += 'qa.ui: not an object'; break }
                foreach ($u in @($v.Keys)) {
                    if ($u -ne 'screens') { $errors += "qa.ui.${u}: unknown key (screens)" }
                    elseif ($v[$u] -isnot [string]) { $errors += 'qa.ui.screens: not a string' }
                    else { $cfg.UiScreens = $v[$u] }
                }
            }
            'staticAnalysis' {
                if ($v -isnot [System.Collections.IDictionary]) { $errors += 'qa.staticAnalysis: not an object'; break }
                foreach ($s in @($v.Keys)) {
                    $sv = $v[$s]
                    switch -CaseSensitive ($s) {
                        'analyzers' {
                            if ($sv -isnot [System.Collections.IDictionary]) { $errors += 'qa.staticAnalysis.analyzers: not an object'; break }
                            foreach ($id in @($sv.Keys)) {
                                $ver = $sv[$id]
                                if ($ver -isnot [string] -or ($ver -ne 'off' -and $ver -notmatch $script:PaperQaVersionPattern)) {
                                    $errors += "qa.staticAnalysis.analyzers.${id}: '$ver' is not a version or `"off`""
                                }
                                else { $cfg.Analyzers[$id] = $ver }
                            }
                        }
                        'kinds' {
                            if ($sv -isnot [System.Collections.IDictionary]) { $errors += 'qa.staticAnalysis.kinds: not an object'; break }
                            foreach ($id in @($sv.Keys)) {
                                if ($script:PaperQaKindNames -cnotcontains "$($sv[$id])") { $errors += "qa.staticAnalysis.kinds.${id}: '$($sv[$id])' is not bug, vulnerability, smell or ignore" }
                                else { $cfg.Kinds[$id] = "$($sv[$id])" }
                            }
                        }
                        'globalconfig' {
                            if ($sv -isnot [string] -or -not (Test-PaperQaInsideRepo $sv)) { $errors += 'qa.staticAnalysis.globalconfig: must be a file inside the repository' }
                            else { $cfg.GlobalConfig = ConvertTo-PaperQaRelPath $sv }
                        }
                        'sarifDir' {
                            if ($sv -isnot [string] -or -not (Test-PaperQaInsideRepo $sv)) { $errors += 'qa.staticAnalysis.sarifDir: must be a folder inside the repository' }
                            else { $cfg.SarifDir = ConvertTo-PaperQaRelPath $sv }
                        }
                        default { $errors += "qa.staticAnalysis.${s}: unknown key (analyzers, globalconfig, sarifDir, kinds)" }
                    }
                }
            }
            default { $errors += "qa.${key}: unknown key (lanes, exclude, report, verifyCap, batchTokens, ui, staticAnalysis)" }
        }
    }
    $cfg.Errors = $errors
    return $cfg
}

# ------------------------------------------------------------------ C. scope

function Select-PaperQaScope {
    <#
    .SYNOPSIS
    Which scope a run reviews and why (table C): asked for, or by default the branch when it is ahead of its
    base or has uncommitted work, else the whole project. Error is set when the request cannot be served.
    #>
    param([string] $Requested, [string] $Path, [string] $Base, [string] $CurrentBranch, [string] $BaseBranch, [string] $BaseError, [int] $Ahead, [int] $Dirty, [switch] $External)
    $new = { param($mode, $label, $reason, $folder, $err) [pscustomobject]@{ Mode = $mode; Label = $label; Reason = $reason; Folder = $folder; Error = $err } }
    $req = "$Requested".ToLowerInvariant()
    # An external repository (ADR-0037): the project unless a scope is asked for; a branch scope's label and
    # reason come from qa.ps1, which lists it with the kit's own read-only git (F98).
    if ($External -and -not $req -and -not $Path -and -not $Base) { return & $new 'project' '' 'project (external repository: the default scope)' '' '' }
    if (-not $req -and $Path) { $req = 'path' }
    if (-not $req -and $Base) { $req = 'branch' }
    if (@('', 'project', 'branch', 'path') -notcontains $req) { return & $new '' '' '' '' "-Scope must be project, branch or path, not '$Requested'" }
    if ($Base -and ($req -eq 'project' -or $req -eq 'path')) { return & $new '' '' '' '' '-Base goes with -Scope branch only' }
    if ($Path -and ($req -eq 'project' -or $req -eq 'branch')) { return & $new '' '' '' '' '-Path goes with -Scope path only' }
    $baseName = if ($Base) { $Base } else { $BaseBranch }
    switch ($req) {
        'path' {
            $p = ConvertTo-PaperQaRelPath $Path
            if (-not $p -or -not (Test-PaperQaInsideRepo $Path)) { return & $new '' '' '' '' '-Path must be a folder inside the repository' }
            return & $new 'path' $p "folder $p" $p ''
        }
        'project' { return & $new 'project' '' 'project as asked' '' '' }
        'branch' {
            if ($External) { return & $new 'branch' '' '' '' '' }
            $reason = "branch $CurrentBranch against $baseName"
            if ($Ahead -gt 0) { $reason = "branch $CurrentBranch is $Ahead commit(s) ahead of $baseName" }
            elseif ($Dirty -gt 0) { $reason = "$Dirty uncommitted change(s)" }
            return & $new 'branch' $CurrentBranch $reason '' ''
        }
    }
    # No base branch, or no merge-base with it: a branch scope cannot be listed, so the default is the project,
    # saying why - never "no change" (F69, find-bug FB19, FB23).
    if ($BaseError) { return & $new 'project' '' "no branch scope - $BaseError" '' '' }
    if (-not $baseName) { return & $new 'project' '' 'no base branch found' '' '' }
    if ($Ahead -gt 0) { return & $new 'branch' $CurrentBranch "branch $CurrentBranch is $Ahead commit(s) ahead of $baseName" '' '' }
    if ($Dirty -gt 0) { return & $new 'branch' $CurrentBranch "$Dirty uncommitted change(s)" '' '' }
    return & $new 'project' '' "no change against $baseName" '' ''
}

function ConvertFrom-PaperReviewFilesOutput {
    <#
    .SYNOPSIS
    The output of `paperflow.ps1 review-files` (its contract) to Files (Path, Status), Excluded (Path,
    Reason) and Total; ExitCode 5 for NOT APPLICABLE (F65), 2 with Reason for not verifiable (F69).
    #>
    param([string[]] $Lines, [int] $ExitCode)
    $files = @(); $excluded = @(); $total = 0; $code = $ExitCode; $reason = ''
    $section = 'files'
    foreach ($line in @($Lines)) {
        $l = "$line"
        if ($l -match '^review-files: NOT APPLICABLE') { $code = 5; continue }
        # Only the runner's own line is a verdict: rule text and file names may say "not verifiable" too (FB8).
        if ($l -match '^(review-files|paperflow):.*not verifiable') { if (-not $reason) { $reason = $l.Trim() }; if ($code -eq 0) { $code = 2 }; continue }
        if ($l -match '^review-files:') { continue }
        if ($l -match '^(rules:|other files:)') { $section = 'files'; continue }
        if ($l -match '^excluded:') { $section = 'excluded'; continue }
        if ($l -match '^total:\s*(\d+)') { $total = [int] $Matches[1]; continue }
        if ($l.StartsWith('    ') -or -not $l.Trim()) { continue }
        $m = [regex]::Match($l, '^  (\S.*?)   (\S.*)$')
        if (-not $m.Success) { continue }
        $rest = ($m.Groups[2].Value -replace '\s*\[[^\]]*\]\s*$', '').Trim()
        if ($section -eq 'excluded') { $excluded += [pscustomobject]@{ Path = $m.Groups[1].Value; Reason = $rest } }
        else { $files += [pscustomobject]@{ Path = $m.Groups[1].Value; Status = (($rest -split '\s+')[0]) } }
    }
    if ($code -eq 2 -and -not $reason) { $reason = (@($Lines | Where-Object { "$_".Trim() }) | Select-Object -Last 1) }
    return [pscustomobject]@{ ExitCode = $code; Files = $files; Excluded = $excluded; Total = $total; Reason = "$reason" }
}

# A glob of qa.exclude or qa.ui.screens (** any folders, * any name part, ? one character), matched without
# case by the task gate's own glob rules (paperflow/tasks-gate-plan.ps1, which the caller dot-sources): one
# reading of a glob in the kit.
function Test-PaperQaGlob([string] $Path, [string] $Glob) {
    return [regex]::IsMatch((ConvertTo-PaperGlobKey $Path), (ConvertTo-PaperGlobRegex (ConvertTo-PaperGlobKey $Glob)))
}

function Get-PaperQaScopeFiles {
    <#
    .SYNOPSIS
    The files of a project or folder scope (table C): tracked plus untracked-not-ignored, one entry per path
    whatever its case, ordinal order; what is left out carries its reason. ExitCode 5 when nothing is left.
    #>
    param([string[]] $Tracked, [string[]] $Untracked, $Bytes, $Binary, [string[]] $Missing, [string[]] $Submodules, [string] $Folder, [string[]] $Exclude)
    $seen = @{}
    $paths = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in (@($Tracked) + @($Untracked))) {
        if (-not $p) { continue }
        $n = $p.Replace('\', '/')
        $k = $n.ToLowerInvariant()
        if ($seen.ContainsKey($k)) { continue }
        $seen[$k] = $true
        $paths.Add($n)
    }
    $paths.Sort([StringComparer]::Ordinal)
    $folder = ConvertTo-PaperQaRelPath $Folder
    $gone = @{}
    foreach ($m in @($Missing)) { if ($m) { $gone[$m.Replace('\', '/').ToLowerInvariant()] = $true } }
    $subs = @{}
    foreach ($m in @($Submodules)) { if ($m) { $subs[$m.Replace('\', '/').ToLowerInvariant()] = $true } }
    $files = @(); $excluded = @()
    foreach ($p in $paths) {
        if ($folder -and -not $p.StartsWith("$folder/", [StringComparison]::OrdinalIgnoreCase)) { continue }
        $first = ($p -split '/')[0].ToLowerInvariant()
        $size = 0
        if ($null -ne $Bytes -and $Bytes.Contains($p)) { $size = [long] $Bytes[$p] }
        $reason = ''
        if ($subs.ContainsKey($p.ToLowerInvariant())) { $reason = 'submodule' }
        elseif ($gone.ContainsKey($p.ToLowerInvariant())) { $reason = 'deleted' }
        elseif (@('.claude', '.agents', '.codex') -contains $first) { $reason = 'agent config' }
        elseif (Test-PaperSecretName $p) { $reason = 'secret_exclude (name)' }
        elseif (Test-PaperQaGeneratedPath $p) { $reason = 'generated' }
        elseif (@($Exclude | Where-Object { $_ -and (Test-PaperQaGlob $p $_) }).Count -gt 0) { $reason = 'profile exclude' }
        elseif ($null -ne $Binary -and $Binary.Contains($p) -and $Binary[$p]) { $reason = 'binary' }
        elseif ($size -gt 1000000) { $reason = 'too_large' }
        if ($reason) { $excluded += [pscustomobject]@{ Path = $p; Reason = $reason } }
        else { $files += [pscustomobject]@{ Path = $p; Bytes = $size } }
    }
    $code = 0
    if ($files.Count -eq 0) { $code = 5 }
    return [pscustomobject]@{ ExitCode = $code; Files = $files; Excluded = $excluded }
}

# ------------------------------------------------------------------ D. lanes

function Get-PaperQaLaneFiles {
    <#
    .SYNOPSIS
    The files of the scope one lane reads, by extension (D.2); the ui lane adds the screens as images.
    #>
    param($Files, [string] $Lane, [string[]] $Screens)
    $out = @()
    foreach ($f in @($Files)) {
        if ($null -eq $f) { continue }
        $path = "$($f.Path)"
        $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
        $leaf = ($path -split '/')[-1]
        $take = switch ($Lane) {
            'static' { @('.cs', '.vb') -contains $ext }
            'security' { ($script:PaperQaCodeExt -contains $ext) -or ($script:PaperQaConfigExt -contains $ext) }
            'bug' { $script:PaperQaCodeExt -contains $ext }
            'smell' { $script:PaperQaCodeExt -contains $ext }
            'architecture' { ($script:PaperQaCodeExt -contains $ext) -or ($script:PaperQaProjectExt -contains $ext) -or ($leaf -ieq 'CODEMAP.md') }
            'ui' { $script:PaperQaUiExt -contains $ext }
            default { $false }
        }
        if ($take) { $out += $f }
    }
    if ($Lane -eq 'ui') {
        foreach ($s in @($Screens)) { if ($s) { $out += [pscustomobject]@{ Path = $s.Replace('\', '/'); Bytes = 0; Image = $true } } }
    }
    return $out
}

function Split-PaperQaLaneList([string[]] $Names) {
    return @(@($Names) | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
}

function Get-PaperQaLanes {
    <#
    .SYNOPSIS
    Which lanes run, in the order static, security, bug, architecture, smell, ui; the first rule of D.1
    that matches decides each one. Error is set for an unknown lane or one both in -Only and -Skip (F66).
    The rules that a lane does not apply come before the choices (profile off, -StaticOnly, -Only, -Skip),
    so a lane that does not apply says why whatever was picked (ADR-0035, F103).
    #>
    param($Hosts, $Config, $LaneFiles, [bool] $HasDotnetProject, [bool] $HasBuildVerb, [string[]] $Only, [string[]] $Skip, [switch] $StaticOnly, [int] $RuleCount = -1, [string] $NoBuildReason = 'no build verb')
    $names = $script:PaperQaLaneNames
    # Not $only/$skip: PowerShell names are case-insensitive, so those would be the typed parameters.
    $onlyList = @(Split-PaperQaLaneList $Only)
    $skipList = @(Split-PaperQaLaneList $Skip)
    foreach ($n in ($onlyList + $skipList)) {
        if ($names -notcontains $n) { return [pscustomobject]@{ Error = "unknown lane '$n' ($($names -join ', '))"; Lanes = @() } }
    }
    foreach ($n in $onlyList) {
        if ($skipList -contains $n) { return [pscustomobject]@{ Error = "lane '$n' is in both -Only and -Skip"; Lanes = @() } }
    }
    $hostList = @($Hosts | Where-Object { $_ } | ForEach-Object { "$_".ToLowerInvariant() })
    $uiHost = (@($hostList | Where-Object { $script:PaperQaNoUiHosts -notcontains $_ }).Count -gt 0)
    $hostText = $(if ($hostList.Count -gt 0) { $hostList -join ', ' } else { 'none' })
    $external = ($null -ne $Config -and $Config.External -eq $true)
    $lanes = @()
    foreach ($lane in $names) {
        $override = $null
        if ($null -ne $Config -and $null -ne $Config.Lanes -and $Config.Lanes.Contains($lane)) { $override = $Config.Lanes[$lane] }
        $count = 0
        if ($null -ne $LaneFiles -and $LaneFiles.Contains($lane)) { $count = @($LaneFiles[$lane] | Where-Object { $null -ne $_ }).Count }
        $state = 'run'; $reason = ''
        if ($lane -eq 'static' -and -not $HasDotnetProject) { $state = 'not applicable'; $reason = 'no .NET project' }
        elseif ($lane -eq 'static' -and -not $HasBuildVerb) { $state = 'not applicable'; $reason = $NoBuildReason }
        elseif ($count -eq 0) {
            $state = 'not applicable'
            $reason = switch ($lane) { 'static' { 'no C# or VB files in scope' } 'ui' { 'no UI files in scope' } default { 'no code files in scope' } }
        }
        elseif ($lane -eq 'architecture' -and $external) {
            # An external repository is judged by its own rule files; with none there is nothing to judge
            # against, whatever qa.lanes.architecture says (F95).
            if ($RuleCount -le 0) { $state = 'not applicable'; $reason = 'no rule files in the repository (qa.profile.json rules)' }
        }
        elseif ($lane -eq 'architecture' -and -not ($null -ne $Config -and $Config.ArchitectureDeclared) -and $override -ne $true) { $state = 'not applicable'; $reason = 'profile declares no architecture' }
        elseif ($lane -eq 'ui' -and -not $external -and -not $uiHost -and $override -ne $true) {
            # An external repository is judged by the UI files of its scope, not by hosts (F104).
            $state = 'not applicable'; $reason = "no host with a user interface (hosts: $hostText)"
        }
        if ($state -eq 'run') {
            if ($override -is [bool] -and -not $override) { $state = 'skipped'; $reason = 'profile turns it off' }
            elseif ($StaticOnly -and $lane -ne 'static') { $state = 'skipped'; $reason = '-StaticOnly' }
            elseif ($onlyList.Count -gt 0 -and $onlyList -notcontains $lane) { $state = 'skipped'; $reason = '-Only' }
            elseif ($skipList -contains $lane) { $state = 'skipped'; $reason = '-Skip' }
        }
        $lanes += [pscustomobject]@{ Name = $lane; State = $state; Reason = $reason }
    }
    return [pscustomobject]@{ Error = ''; Lanes = $lanes }
}

# ------------------------------------------------------------------ E. estimate and batches

function Get-PaperQaFileTokens($File) {
    if ($File.Image) { return $script:PaperQaImageTokens }
    return [long] [math]::Ceiling([double] $File.Bytes / $script:PaperQaBytesPerToken)
}

function Split-PaperQaBatches {
    <#
    .SYNOPSIS
    The batches of one lane (table E): files by path, greedy; a file joins the current batch while the total
    stays within MaxTokens, else opens a new one; a file over MaxTokens alone goes alone. An array of arrays.
    #>
    param($Files, [int] $MaxTokens)
    $sorted = Sort-PaperQaByKey @($Files | Where-Object { $null -ne $_ }) { param($f) "$($f.Path)" }
    $batches = @()
    $current = @()
    $sum = 0
    foreach ($f in $sorted) {
        $t = Get-PaperQaFileTokens $f
        if ($current.Count -gt 0 -and ($sum + $t) -gt $MaxTokens) {
            $batches += , @($current)
            $current = @(); $sum = 0
        }
        $current += $f
        $sum += $t
    }
    if ($current.Count -gt 0) { $batches += , @($current) }
    return , $batches
}

function Get-PaperQaEstimate {
    <#
    .SYNOPSIS
    Agents and tokens of each lane and of the verify readers (table E). Static is free; a lane that does not
    run costs nothing; verify is the cap times one reader, only when an LLM lane runs.
    #>
    param($Lanes, $LaneFiles, $Config, [switch] $StaticOnly, $Rules = $null)
    $rows = @()
    $total = 0
    foreach ($l in @($Lanes)) {
        $files = @()
        if ($null -ne $LaneFiles -and $LaneFiles.Contains($l.Name)) { $files = @($LaneFiles[$l.Name] | Where-Object { $null -ne $_ }) }
        $row = [pscustomobject]@{ Name = $l.Name; State = $l.State; Reason = $l.Reason; Files = 0; Agents = 0; Tokens = 0; Batches = 0 }
        if ($l.State -eq 'run') {
            $row.Files = $files.Count
            if ($l.Name -ne 'static') {
                $batches = Split-PaperQaBatches -Files $files -MaxTokens $Config.BatchTokens
                $content = 0
                foreach ($f in $files) { $content += Get-PaperQaFileTokens $f }
                # An external repository: each architecture batch also reads its rule files (ADR-0034).
                $ruleList = @($Rules | Where-Object { $null -ne $_ })
                if ($l.Name -eq 'architecture' -and $ruleList.Count -gt 0) {
                    foreach ($bt in $batches) {
                        $paths = @($bt | ForEach-Object { "$($_.Path)" })
                        foreach ($rf in @(Get-PaperQaBatchRules -Rules $ruleList -BatchPaths $paths)) { $content += [long] [math]::Ceiling([double] $rf.Bytes / $script:PaperQaBytesPerToken) }
                    }
                    $row.Reason = "rules: $($ruleList.Count) file(s)"
                }
                $row.Batches = @($batches).Count
                $row.Agents = @($batches).Count
                $row.Tokens = [long] ($row.Agents * $script:PaperQaLaneOverhead + [math]::Ceiling($content * $script:PaperQaReadFactor))
            }
        }
        $total += $row.Tokens
        $rows += $row
    }
    $cap = [int] $Config.VerifyCap
    if (Test-PaperQaAgentLaneRuns -Lanes $Lanes) { $verify = [pscustomobject]@{ Name = 'verify'; State = "up to $cap"; Reason = ''; Files = 0; Agents = $cap; Tokens = [long] ($cap * $script:PaperQaVerifyTokens); Batches = 0 } }
    else { $verify = [pscustomobject]@{ Name = 'verify'; State = '0'; Reason = ''; Files = 0; Agents = 0; Tokens = 0; Batches = 0 } }
    $total += $verify.Tokens
    $rows += $verify
    return [pscustomobject]@{ Rows = $rows; Total = [long] $total }
}

function Format-PaperQaEstimate {
    <#
    .SYNOPSIS
    The estimate table the user sees before any token is spent (table E): one line per lane, verify, total,
    how to skip, and whether the run waits for approval.
    #>
    param([string] $Run, [string] $Mode, [string] $Reason, [int] $FileCount, [int] $ExcludedCount, $Estimate, [bool] $Approved, [string] $RepoRoot = '', [string] $RunDir = '', [string[]] $ScopeLines = @())
    $cells = { param($a, $b, $c, $d, $e, $f) ("$a".PadRight(14) + "$b".PadRight(16) + "$c".PadRight(7) + "$d".PadRight(8) + "$e".PadRight(9) + "$f").TrimEnd() }
    $out = @("qa: run $Run - scope $Mode ($Reason) - $FileCount file(s), $ExcludedCount excluded")
    if ($RepoRoot) { $out += "repo: $RepoRoot (external, read only) - run folder $RunDir" }
    # The base: and changes: lines of an external branch scope (ADR-0037), right after the repo line.
    $out += @($ScopeLines | Where-Object { $_ })
    $out += & $cells 'lane' 'state' 'files' 'agents' 'tokens' ''
    foreach ($r in @($Estimate.Rows)) {
        if ($r.Name -eq 'verify') {
            $out += & $cells 'verify' $r.State '-' $r.Agents (Format-PaperQaNumber $r.Tokens) ''
            continue
        }
        if ($r.State -ne 'run') { $out += & $cells $r.Name $r.State '-' '-' '-' $r.Reason; continue }
        if ($r.Name -eq 'static') { $out += & $cells $r.Name $r.State $r.Files '-' 'free' $r.Reason; continue }
        $out += & $cells $r.Name $r.State $r.Files $r.Agents (Format-PaperQaNumber $r.Tokens) $r.Reason
    }
    $out += ('total'.PadRight(45) + (Format-PaperQaNumber $Estimate.Total))
    $out += 'skip a lane: -Skip <lane>   free run: -StaticOnly'
    if ($Approved) { $out += 'approved: lanes may start' } else { $out += "waiting for approval: qa.ps1 approve -Run $Run" }
    return $out
}

# ------------------------------------------------------------------ F. approval

function Test-PaperQaRunApproved {
    param($Plan, [string] $Lane)
    if ($Lane -eq 'static') { return $true }
    return [bool] $Plan.Approved
}

# Whether a token-spending (agent) lane runs: one answer for the estimate's verify row, the approval at
# plan time and the verify queue (find-bug FB17, FB26).
function Test-PaperQaAgentLaneRuns {
    param($Lanes)
    return (@($Lanes | Where-Object { $null -ne $_ -and $_.State -eq 'run' -and $script:PaperQaLlmLanes -contains $_.Name }).Count -gt 0)
}

# A run approves itself at plan time with -Yes, or when no token-spending lane runs.
function Test-PaperQaApprovedAtPlan {
    param($Lanes, [bool] $Yes)
    if ($Yes) { return $true }
    return (-not (Test-PaperQaAgentLaneRuns -Lanes $Lanes))
}

function Get-PaperQaPrefix([string] $Lane, [int] $Batch, [int] $BatchCount) {
    $p = $script:PaperQaLanePrefix[$Lane]
    if ($BatchCount -gt 1 -and $Batch -gt 1) { return "$p$Batch" }
    return $p
}

# ------------------------------------------------------------------ H. findings

# Counts of at most 9 digits: a bigger one is no seen line, not an [int] overflow (find-bug FB10).
$script:PaperQaSeenPattern = '^\s*(?:\S+\s+)?(?:seen|xem)\s+(\d{1,9})\s*/\s*(\d{1,9})\s*$'
$script:PaperQaNotReadPattern = '^\s*(?:not read|\S+\s+xem)\s*:\s*(.+)$'

function ConvertFrom-PaperQaBlocks {
    <#
    .SYNOPSIS
    Key blocks (H.1): an upper-case key at the start of a line and its value; a line indented by two spaces
    or a tab continues the value; a block starts at StartKey and ends at the next one, a seen line, or the
    end. Other lines are ignored. Also returns the seen line (A of B) and the not-read list.
    #>
    param([string[]] $Lines, [string] $StartKey)
    $blocks = @()
    $current = $null
    $last = $null
    $seen = $null
    $notRead = $null
    foreach ($raw in @($Lines)) {
        $line = "$raw".TrimEnd()
        $m = [regex]::Match($line, $script:PaperQaSeenPattern)
        if ($m.Success) {
            if ($null -ne $current) { $blocks += $current; $current = $null }
            $seen = [pscustomobject]@{ A = [int] $m.Groups[1].Value; B = [int] $m.Groups[2].Value }
            $last = $null
            continue
        }
        $m = [regex]::Match($line, $script:PaperQaNotReadPattern)
        if ($m.Success) {
            if ($null -ne $current) { $blocks += $current; $current = $null }
            # "none", "-", "(none)" name no file (find-bug FB24).
            $notRead = @($m.Groups[1].Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and @('none', '-', '(none)', 'nothing') -notcontains $_.ToLowerInvariant() })
            $last = $null
            continue
        }
        $m = [regex]::Match($line, '^([A-Z][A-Z_]*)(?:\s+(.*))?$')
        if ($m.Success) {
            $key = $m.Groups[1].Value
            $value = $m.Groups[2].Value.Trim()
            if ($key -eq $StartKey) {
                if ($null -ne $current) { $blocks += $current }
                $current = [ordered]@{}
            }
            if ($null -ne $current) {
                if (-not $current.Contains($key)) { $current[$key] = $value }
                $last = $key
            }
            continue
        }
        $m = [regex]::Match($line, '^(?: {2,}|\t)\s*(\S.*)$')
        if ($m.Success -and $null -ne $current -and $last) {
            $current[$last] = ("$($current[$last]) " + $m.Groups[1].Value.Trim()).Trim()
            continue
        }
        $last = $null
    }
    if ($null -ne $current) { $blocks += $current }
    return [pscustomobject]@{ Blocks = $blocks; Seen = $seen; NotRead = $notRead }
}

function Test-PaperQaFinding {
    <#
    .SYNOPSIS
    The format errors of one finding block (H.2), each "<id>: <error>".
    #>
    param($Finding, [string] $Lane, [string] $Prefix, [string[]] $ListPaths)
    $id = "$($Finding['FINDING'])"
    $tag = if ($id) { $id } else { '(no id)' }
    $errors = @()
    if ($id -notmatch ('^' + [regex]::Escape($Prefix) + '-\d+$')) { $errors += "${tag}: id must look like $Prefix-<n>" }
    $laneValue = "$($Finding['LANE'])"
    if ($laneValue -ne $Lane) { $errors += "${tag}: LANE '$laneValue' is not $Lane" }
    $kind = $script:PaperQaLaneKinds[$Lane]
    if ("$($Finding['KIND'])" -ne $kind) { $errors += "${tag}: lane $Lane reports $kind, not $($Finding['KIND'])" }
    $sev = "$($Finding['SEVERITY'])"
    if ($script:PaperQaSeverities -cnotcontains $sev) { $errors += "${tag}: SEVERITY '$sev' is not critical, major, minor or info" }
    $where = [regex]::Match("$($Finding['WHERE'])", '^(.+):(\d{1,9})$')
    if (-not $where.Success -or [int] $where.Groups[2].Value -lt 1) { $errors += "${tag}: WHERE needs path:line" }
    else {
        $p = ConvertTo-PaperQaRelPath $where.Groups[1].Value
        if (@($ListPaths | Where-Object { $_ -and ($_.Replace('\', '/') -ieq $p) }).Count -eq 0) { $errors += "${tag}: WHERE $p is not in this lane's list" }
    }
    foreach ($k in @('RULE', 'WHY', 'FIX')) { if (-not "$($Finding[$k])".Trim()) { $errors += "${tag}: $k is empty" } }
    if (-not $Finding.Contains('INPUT') -or -not "$($Finding['INPUT'])".Trim()) { $errors += "${tag}: INPUT is missing (write - for a suspicion)" }
    if ($Finding.Contains('STATUS') -or $Finding.Contains('VERDICT')) { $errors += "${tag}: a lane reports findings only; verification is a separate reader" }
    return $errors
}

function Test-PaperQaLaneAnswer {
    <#
    .SYNOPSIS
    One saved lane answer checked (H.2, H.3): the findings in the right format, the errors of the others,
    and whether the lane read its whole list. FileCount -1 (a re-dispatch for format errors) needs no seen line.
    #>
    param([string[]] $Lines, [string] $Lane, [string] $Prefix, [string[]] $ListPaths, [int] $FileCount)
    $parsed = ConvertFrom-PaperQaBlocks -Lines $Lines -StartKey 'FINDING'
    $errors = @()
    $findings = @()
    $ids = @{}
    foreach ($b in @($parsed.Blocks)) {
        $e = @(Test-PaperQaFinding -Finding $b -Lane $Lane -Prefix $Prefix -ListPaths $ListPaths)
        $id = "$($b['FINDING'])"
        if ($id) {
            if ($ids.ContainsKey($id.ToLowerInvariant())) { $e += "${id}: $id appears twice" }
            $ids[$id.ToLowerInvariant()] = $true
        }
        if ($e.Count -gt 0) { $errors += $e; continue }
        $w = [regex]::Match("$($b['WHERE'])", '^(.+):(\d+)$')
        $path = ConvertTo-PaperQaRelPath $w.Groups[1].Value
        $listed = @($ListPaths | Where-Object { $_ -and ($_.Replace('\', '/') -ieq $path) })
        if ($listed.Count -gt 0) { $path = $listed[0].Replace('\', '/') }
        $findings += [pscustomobject]@{
            Id = $id; Lane = $Lane; Kind = "$($b['KIND'])"; Severity = "$($b['SEVERITY'])"
            Where = "${path}:$($w.Groups[2].Value)"; Path = $path; Line = [int] $w.Groups[2].Value
            Rule = "$($b['RULE'])"; Input = "$($b['INPUT'])"; Why = "$($b['WHY'])"; Fix = "$($b['FIX'])"
        }
    }
    $complete = $true
    $notRead = @()
    $seenText = ''
    if ($null -ne $parsed.Seen) { $seenText = "$($parsed.Seen.A)/$($parsed.Seen.B)" }
    if ($FileCount -ge 0) {
        if ($null -eq $parsed.Seen) { $errors += "no 'seen N/N' line"; $complete = $false }
        elseif ($parsed.Seen.B -ne $FileCount) { $errors += "seen $seenText but the list has $FileCount file(s)"; $complete = $false }
    }
    if ($null -ne $parsed.Seen -and $parsed.Seen.A -lt $parsed.Seen.B) { $complete = $false }
    # A not-read line counts only when it names a file of the list, or the seen line is short: "seen 3/3" with
    # "not read: N/A" (or any wording that names no list file) is a finished lane (find-bug FB28).
    $namedList = @($parsed.NotRead | Where-Object { $_ } | ForEach-Object { ConvertTo-PaperQaRelPath $_ } | Where-Object { $n = $_; @($ListPaths | Where-Object { $_ -and $_.Replace('\', '/') -ieq $n }).Count -gt 0 })
    $seenFull = ($null -ne $parsed.Seen -and $parsed.Seen.A -ge $parsed.Seen.B)
    if (@($parsed.NotRead).Count -gt 0 -and $null -ne $parsed.NotRead -and -not ($seenFull -and $namedList.Count -eq 0)) { $complete = $false }
    if (-not $complete -and $errors.Count -eq 0) {
        if ($null -ne $parsed.NotRead -and @($parsed.NotRead).Count -gt 0) { $notRead = @($parsed.NotRead) }
        else { $notRead = @('unknown - re-dispatch the whole batch') }
    }
    return [pscustomobject]@{ Findings = $findings; Errors = $errors; Complete = $complete; NotRead = $notRead; Seen = $seenText }
}

function Resolve-PaperQaLaneBatch {
    <#
    .SYNOPSIS
    A lane batch after its answer and, if it was sent back, its second answer (H.5): ok, retry (send it back
    once with the errors or the files not read), or not verifiable with the first error of the second answer.
    Findings in the right format count either way; a second answer's finding replaces one of the same id.
    #>
    param($First, $Second)
    $firstOk = (@($First.Errors).Count -eq 0 -and $First.Complete)
    if ($firstOk) { return [pscustomobject]@{ State = 'ok'; Reason = ''; Findings = @($First.Findings) } }
    $why = if (@($First.Errors).Count -gt 0) { @($First.Errors)[0] } else { "not read: $(@($First.NotRead) -join ', ')" }
    if ($null -eq $Second) { return [pscustomobject]@{ State = 'retry'; Reason = $why; Findings = @($First.Findings) } }
    # A second answer's finding replaces a wrong block of the same id (the first answer kept no finding under
    # it); one reusing an id the first answer kept - a retry for files not read starts again at 1 - is
    # renumbered after the highest id, never dropped (H.5, find-bug FB2).
    $byId = [ordered]@{}
    foreach ($f in @($First.Findings)) { if ($null -ne $f) { $byId[$f.Id.ToLowerInvariant()] = $f } }
    $kept = @($byId.Keys)
    $prefixOf = { param($id) $id.Substring(0, $id.LastIndexOf('-')) }
    # Every id either answer uses is taken, so a renumbered finding never lands on one (find-bug FB7).
    $taken = @{}
    foreach ($f in @($First.Findings) + @($Second.Findings)) { if ($null -ne $f) { $taken[$f.Id.ToLowerInvariant()] = $true } }
    foreach ($f in @($Second.Findings)) {
        if ($null -eq $f) { continue }
        $key = $f.Id.ToLowerInvariant()
        if ($kept -contains $key) {
            $p = & $prefixOf $f.Id
            $max = 0
            foreach ($k in @($taken.Keys)) { if ((& $prefixOf $k) -ieq $p) { $max = [math]::Max($max, (Get-PaperQaIdNumber $k)) } }
            $f = $f.PSObject.Copy()
            $f.Id = "$p-$($max + 1)"
            $key = $f.Id.ToLowerInvariant()
            $taken[$key] = $true
        }
        $byId[$key] = $f
    }
    $findings = @($byId.Values)
    if (@($Second.Errors).Count -eq 0 -and $Second.Complete) { return [pscustomobject]@{ State = 'ok'; Reason = ''; Findings = $findings } }
    $why2 = if (@($Second.Errors).Count -gt 0) { @($Second.Errors)[0] } else { "not read: $(@($Second.NotRead) -join ', ')" }
    return [pscustomobject]@{ State = 'not verifiable'; Reason = $why2; Findings = $findings }
}

# ------------------------------------------------------------------ I. verification

function Copy-PaperQaFinding($Finding) {
    $c = $Finding.PSObject.Copy()
    foreach ($n in @('Status', 'Note', 'QueueOrder', 'Evidence', 'Fingerprint', 'VerifiedInput')) {
        if ($null -eq $c.PSObject.Properties[$n]) { $c | Add-Member -NotePropertyName $n -NotePropertyValue $(if ($n -eq 'QueueOrder') { 0 } else { '' }) }
    }
    return $c
}

function Get-PaperQaIdNumber([string] $Id) {
    $m = [regex]::Match($Id, '(\d+)$')
    if ($m.Success) { return [long] $m.Groups[1].Value }
    return 0
}

function Get-PaperQaVerifyQueue {
    <#
    .SYNOPSIS
    The status of every finding before any reader (table I): bug and vulnerability findings with an input
    (LLM lanes) or of critical/major severity (static, not a static-only run) are candidates, ordered by
    severity, lane, id number; the first VerifyCap are queued, the rest needs_validation (cap N reached).
    #>
    param($Findings, [int] $VerifyCap, [bool] $StaticOnly)
    $laneRank = @{ 'security' = 0; 'bug' = 1; 'static' = 2 }
    $all = @()
    $candidates = @()
    foreach ($f in @($Findings)) {
        if ($null -eq $f) { continue }
        $c = Copy-PaperQaFinding $f
        $all += $c
        if (@('bug', 'vulnerability') -contains $c.Kind) {
            if ($c.Lane -eq 'static') {
                if (-not $StaticOnly -and @('critical', 'major') -contains $c.Severity) { $candidates += $c } else { $c.Status = 'machine' }
            }
            elseif ($c.Input -and "$($c.Input)".Trim() -ne '-') { $candidates += $c }
            else { $c.Status = 'suspicion' }
        }
        elseif ($c.Lane -eq 'static') { $c.Status = 'machine' }
        else { $c.Status = 'proposal' }
    }
    $sevRank = @{ 'critical' = 0; 'major' = 1; 'minor' = 2; 'info' = 3 }
    $ordered = Sort-PaperQaByKey $candidates {
        param($x)
        $lr = if ($laneRank.ContainsKey($x.Lane)) { $laneRank[$x.Lane] } else { 3 }
        $sr = if ($sevRank.ContainsKey($x.Severity)) { $sevRank[$x.Severity] } else { 4 }
        "$sr`t$lr`t$((Get-PaperQaIdNumber $x.Id).ToString('D9'))`t$($x.Id)"
    }
    $i = 0
    foreach ($c in $ordered) {
        $i++
        $c.QueueOrder = $i
        if ($i -le $VerifyCap) { $c.Status = 'queued' }
        else { $c.Status = 'needs_validation'; $c.Note = "cap $VerifyCap reached" }
    }
    return $all
}

function ConvertFrom-PaperQaVerdict {
    <#
    .SYNOPSIS
    One reader's verdict block (table I) and its errors.
    #>
    param([string[]] $Lines, [string[]] $Pending)
    $parsed = ConvertFrom-PaperQaBlocks -Lines $Lines -StartKey 'FOR'
    $b = @($parsed.Blocks)[0]
    if ($null -eq $b) { return [pscustomobject]@{ For = ''; Verdict = ''; Input = ''; Evidence = ''; Fingerprint = ''; Errors = @('no FOR line') } }
    $v = [pscustomobject]@{
        For = "$($b['FOR'])"; Verdict = "$($b['VERDICT'])"; Input = "$($b['INPUT'])"; Evidence = "$($b['EVIDENCE'])"; Fingerprint = "$($b['FINGERPRINT'])"; Errors = @()
    }
    $errors = @()
    if (@($Pending | Where-Object { $_ -ieq $v.For }).Count -eq 0) { $errors += "verdict for unknown id $($v.For)" }
    if (@('confirmed', 'rejected', 'needs_validation') -cnotcontains $v.Verdict) { $errors += "VERDICT '$($v.Verdict)' is not confirmed, rejected or needs_validation" }
    if (-not $v.Evidence.Trim()) { $errors += "$($v.For): EVIDENCE is empty" }
    if ($v.Verdict -eq 'confirmed' -and (-not $v.Fingerprint.Trim() -or -not $v.Input.Trim() -or $v.Input.Trim() -eq '-')) { $errors += "$($v.For): confirmed needs FINGERPRINT and INPUT" }
    $v.Errors = $errors
    return $v
}

function Merge-PaperQaVerdicts {
    <#
    .SYNOPSIS
    Queued findings take their reader's verdict; one with no verdict, or a verdict in the wrong format, is
    needs_validation with the reason (F70).
    #>
    param($Findings, $Verdicts)
    $map = @{}
    foreach ($v in @($Verdicts)) { if ($null -ne $v -and $v.For -and -not $map.ContainsKey($v.For.ToLowerInvariant())) { $map[$v.For.ToLowerInvariant()] = $v } }
    $out = @()
    foreach ($f in @($Findings)) {
        if ($null -eq $f) { continue }
        $c = Copy-PaperQaFinding $f
        if ($c.Status -eq 'queued') {
            $v = $map[$c.Id.ToLowerInvariant()]
            if ($null -eq $v) { $c.Status = 'needs_validation'; $c.Note = 'no verdict returned' }
            elseif (@($v.Errors).Count -gt 0) { $c.Status = 'needs_validation'; $c.Note = @($v.Errors)[0] }
            else {
                $c.Status = $v.Verdict; $c.Note = ''; $c.Evidence = $v.Evidence; $c.Fingerprint = $v.Fingerprint; $c.VerifiedInput = $v.Input
            }
        }
        $out += $c
    }
    return $out
}

function ConvertTo-PaperQaFingerprint([string] $Text) {
    return ([regex]::Replace("$Text".Trim().ToLowerInvariant().Replace('\', '/'), '\s+', ' '))
}

function Get-PaperQaLedgerProposals {
    <#
    .SYNOPSIS
    Bug ledger proposals (F61): confirmed findings with an input only, one per fingerprint, in queue order.
    The owner approves each; the command is what the main session runs for an approved one.
    #>
    param($Findings, [string] $Run, [switch] $External)
    $confirmed = @($Findings | Where-Object {
            $null -ne $_ -and $_.Status -eq 'confirmed' -and (Get-PaperQaFindingInput $_)
        })
    $confirmed = Sort-PaperQaByKey $confirmed { param($x) ([long] $x.QueueOrder).ToString('D9') + "`t" + $x.Id }
    $groups = [ordered]@{}
    foreach ($f in $confirmed) {
        $key = ConvertTo-PaperQaFingerprint $f.Fingerprint
        if (-not $key) { $key = "id:$($f.Id)" }
        if (-not $groups.Contains($key)) { $groups[$key] = @() }
        $groups[$key] += $f
    }
    $out = @()
    foreach ($key in $groups.Keys) {
        $members = @($groups[$key])
        $first = $members[0]
        $ids = @($members | ForEach-Object { $_.Id })
        $out += [pscustomobject]@{
            Ids = $ids; Where = $first.Where; Path = $first.Path; Why = $first.Why; Input = (Get-PaperQaFindingInput $first)
            Fingerprint = $first.Fingerprint; Command = $(if ($External) { "external repository: report to its owners - $($first.Why) (qa ${Run}: $($ids -join ', '))" } else { "/task-bug $($first.Why) (qa ${Run}: $($ids -join ', '))" })
        }
    }
    return $out
}

function Get-PaperQaFindingInput($Finding) {
    $vi = ''
    if ($null -ne $Finding.PSObject.Properties['VerifiedInput']) { $vi = "$($Finding.VerifiedInput)".Trim() }
    if ($vi -and $vi -ne '-') { return $vi }
    $i = "$($Finding.Input)".Trim()
    if ($i -and $i -ne '-') { return $i }
    return ''
}

# ------------------------------------------------------------------ J. report

function Get-PaperQaSlug([string] $Text) {
    $s = [regex]::Replace("$Text".ToLowerInvariant(), '[^a-z0-9]+', '-').Trim('-')
    if ($s.Length -gt 40) { $s = $s.Substring(0, 40).TrimEnd('-') }
    return $s
}

function Get-PaperQaReportName {
    param([string] $Date, [string] $Mode, [string] $Label, [string[]] $Existing)
    $stem = if ($Mode -eq 'project') { "$Date-project" } else { "$Date-$Mode-$(Get-PaperQaSlug $Label)" }
    $taken = @{}
    foreach ($e in @($Existing)) { if ($e) { $taken[$e.ToLowerInvariant()] = $true } }
    $name = "$stem.md"
    $n = 1
    while ($taken.ContainsKey($name.ToLowerInvariant())) { $n++; $name = "$stem-$n.md" }
    return $name
}

# A link from the report folder to a repository file; the line stays in the text, never #L.
# An external repository (RepoRoot): an absolute file:/// link, the root escaped too.
function Get-PaperQaReportLink([string] $ReportDir, [string] $Path, [string] $RepoRoot = '') {
    $escape = { param($t) "$t".Replace('\', '/').Replace('%', '%25').Replace(' ', '%20').Replace('(', '%28').Replace(')', '%29').Replace('#', '%23') }
    if ($RepoRoot) { return 'file:///' + (& $escape ("$RepoRoot".Replace('\', '/').TrimEnd('/') + '/' + "$Path".Replace('\', '/'))) }
    $depth = @((ConvertTo-PaperQaRelPath $ReportDir) -split '/' | Where-Object { $_ }).Count
    $prefix = '../' * $depth
    return "$prefix$(& $escape $Path)"
}

function ConvertTo-PaperQaCell([string] $Text) { return ("$Text" -replace '\r?\n', ' ').Replace('|', '\|') }

function Format-PaperQaWhereLink($Finding, [string] $ReportDir, [string] $RepoRoot = '') {
    if (-not $Finding.Path) { return "$($Finding.Where)" }
    return "[$($Finding.Where)]($(Get-PaperQaReportLink $ReportDir $Finding.Path $RepoRoot))"
}

function Format-PaperQaReport {
    <#
    .SYNOPSIS
    The report (table J): every heading in order, "None." under one with nothing; numbers of a lane that did
    not run are "-" (F56).
    #>
    param($Report)
    $r = $Report
    $dir = $r.ReportDir
    $repo = "$($r.RepoRoot)"
    $dot = [string] [char] 0x00B7
    $findings = @($r.Findings | Where-Object { $null -ne $_ })
    $title = if ($r.Mode -eq 'project') { 'project' } else { "$($r.Mode) $($r.Label)" }
    $out = @("# QA report - $title - $($r.Date)", '')
    $out += "Run $($r.Run) $dot scope $($r.Reason) $dot $($r.FileCount) file(s), $($r.ExcludedCount) excluded $dot estimate $(Format-PaperQaNumber $r.EstimateTotal) tokens"
    if ($r.External -eq $true) {
        $out += "Repository $repo (external, read only) $dot profile $($r.ProfilePath)"
        $out += @($r.ScopeLines | Where-Object { $_ })
    }
    $out += @('', '## Summary', '')
    $out += '| Lane | State | critical | major | minor | info | confirmed | rejected | needs validation | seen |'
    $out += '| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |'
    foreach ($l in @($r.Lanes)) {
        if ($l.State -ne 'run') {
            $out += "| $($l.Name) | $($l.State): $(ConvertTo-PaperQaCell $l.Reason) | - | - | - | - | - | - | - | - |"
            continue
        }
        $mine = @($findings | Where-Object { $_.Lane -eq $l.Name })
        $count = { param($prop, $value) @($mine | Where-Object { "$($_.$prop)" -eq $value }).Count }
        $seen = if ($l.Seen) { $l.Seen } else { '-' }
        $out += "| $($l.Name) | run | $(& $count 'Severity' 'critical') | $(& $count 'Severity' 'major') | $(& $count 'Severity' 'minor') | $(& $count 'Severity' 'info') | $(& $count 'Status' 'confirmed') | $(& $count 'Status' 'rejected') | $(& $count 'Status' 'needs_validation') | $seen |"
    }
    if ($r.External -eq $true) {
        $out += @('', '## Rules used', '')
        $used = @($r.Rules | Where-Object { $null -ne $_ } | ForEach-Object { "$($_.Path)" } | Where-Object { $_ })
        if ($used.Count -eq 0) { $out += 'None.' }
        foreach ($u in $used) { $out += "- [$u]($(Get-PaperQaReportLink $dir $u $repo))" }
    }

    $out += @('', '## Proposed bug ledger entries (owner approves each)', '')
    $props = @($r.Proposals | Where-Object { $null -ne $_ })
    if ($props.Count -eq 0) { $out += 'None.' }
    foreach ($p in $props) {
        $out += "- [ ] $(@($p.Ids) -join ', ') - [$($p.Where)]($(Get-PaperQaReportLink $dir $p.Path $repo)) - $($p.Why) - input: $($p.Input)"
        $out += "  ``$($p.Command)``"
    }

    $out += @('', '## Proposed work (refactor, architecture, UI)', '')
    $work = @($findings | Where-Object { $_.Status -eq 'proposal' })
    if ($work.Count -eq 0) { $out += 'None.' }
    foreach ($lane in @('architecture', 'smell', 'ui')) {
        $mine = @($work | Where-Object { $_.Lane -eq $lane })
        if ($mine.Count -eq 0) { continue }
        $out += @("### $lane", '')
        foreach ($f in $mine) { $out += "- [ ] $($f.Id) $(Format-PaperQaWhereLink $f $dir $repo) ($($f.Severity)) - $($f.Why) - fix: $($f.Fix)" }
        $out += ''
    }

    $out += @('', '## Static analysis', '')
    $st = $r.Static
    if ($null -eq $st) { $out += 'None.' }
    else {
        $out += "Analyzers: $($st.Analyzers)"
        $out += "Build: $($st.Build)"
        if ($st.Verdict -and $st.Verdict -ne 'ran') { $out += "Not verifiable: $($st.Reason)" }
        if ($null -ne $st.Result) {
            $c = $st.Result.Counts
            $out += ('Not listed: ' + (@($c.Keys | ForEach-Object { "$_ $($c[$_])" }) -join ', '))
            $statusById = @{}
            foreach ($f in $findings) { if ($f.Lane -eq 'static') { $statusById[$f.Id] = $f.Status } }
            foreach ($kind in @('vulnerability', 'bug', 'smell')) {
                $groups = @($st.Result.Groups | Where-Object { $_.Kind -eq $kind })
                $n = 0; foreach ($g in $groups) { $n += $g.Count }
                $out += @('', "### $kind ($n)")
                foreach ($g in $groups) {
                    $head = "#### $($g.RuleId) - $($g.Title) ($($g.Severity), $($g.Count))"
                    if ($g.Link) { $head += " - $($g.Link)" }
                    $head += " - kind from $($g.Source)"
                    if ($g.Hotspot) { $head += ', hotspot: review' }
                    $out += @('', $head, '')
                    $shown = 0
                    foreach ($f in @($g.Findings)) {
                        if ($shown -ge 20) { break }
                        $status = if ($statusById.ContainsKey($f.Id)) { $statusById[$f.Id] } else { $f.Status }
                        $out += "- $(Format-PaperQaWhereLink $f $dir $repo) $($f.Id) $status"
                        $shown++
                    }
                    if (@($g.Findings).Count -gt 20) { $out += "- ... and $(@($g.Findings).Count - 20) more" }
                }
            }
        }
    }

    $out += @('', '## Findings by lane', '')
    $llm = @($findings | Where-Object { $_.Lane -ne 'static' })
    if ($llm.Count -eq 0) { $out += 'None.' }
    foreach ($l in @($r.Lanes)) {
        if ($l.Name -eq 'static') { continue }
        $mine = @($llm | Where-Object { $_.Lane -eq $l.Name })
        if ($mine.Count -eq 0) { continue }
        $out += @("### $($l.Name)", '', '| Id | Where | Severity | Rule | Why | Smallest fix | Status |', '| --- | --- | --- | --- | --- | --- | --- |')
        foreach ($f in $mine) {
            $out += "| $($f.Id) | $(Format-PaperQaWhereLink $f $dir $repo) | $($f.Severity) | $(ConvertTo-PaperQaCell $f.Rule) | $(ConvertTo-PaperQaCell $f.Why) | $(ConvertTo-PaperQaCell $f.Fix) | $($f.Status) |"
        }
        $out += ''
    }

    $sections = @(
        @('## Suspicions (no input)', 'suspicion', ''),
        @('## Rejected', 'rejected', 'Evidence'),
        @('## Not verified', 'needs_validation', 'Note')
    )
    foreach ($s in $sections) {
        $out += @('', $s[0], '')
        $mine = @($findings | Where-Object { $_.Status -eq $s[1] })
        if ($mine.Count -eq 0) { $out += 'None.' }
        foreach ($f in $mine) {
            $line = "- $($f.Id) $(Format-PaperQaWhereLink $f $dir $repo) - $($f.Why)"
            if ($s[2] -and $null -ne $f.PSObject.Properties[$s[2]] -and "$($f.($s[2]))") { $line += " - $($f.($s[2]))" }
            $out += $line
        }
    }

    $out += @('', '## Excluded from scope', '')
    $byReason = [ordered]@{}
    foreach ($e in @($r.Excluded | Where-Object { $null -ne $_ })) {
        if (-not $byReason.Contains($e.Reason)) { $byReason[$e.Reason] = 0 }
        $byReason[$e.Reason]++
    }
    if ($byReason.Count -eq 0) { $out += 'None.' }
    foreach ($k in $byReason.Keys) { $out += "- ${k}: $($byReason[$k])" }
    return $out
}

# ------------------------------------------------------------------ K: the decisions of qa.ps1's commands
# qa.ps1 reads git and the disk and hands what it read to these; it prints their Lines and exits with
# their ExitCode.

# Uncommitted work for the default scope, minus the reports of earlier runs: a report must not turn the
# next run into a branch run. StatusLines: `git status --porcelain --untracked-files=all`.
function Get-PaperQaDirtyCount {
    param([string[]] $StatusLines, [string] $ReportDir, [string] $SarifDir)
    # What earlier runs wrote on purpose: the reports, and the SARIF copies of qa.staticAnalysis.sarifDir (FB25).
    $prefixes = @(@($ReportDir, $SarifDir) | Where-Object { $_ } | ForEach-Object { (ConvertTo-PaperQaRelPath $_) + '/' })
    $n = 0
    foreach ($l in @($StatusLines)) {
        if (-not "$l".Trim() -or "$l".Length -le 3) { continue }
        $path = "$l".Substring(3).Trim().Trim('"').Replace('\', '/')
        if (@($prefixes | Where-Object { $path.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0) { continue }
        $n++
    }
    return $n
}

function Format-PaperQaNoFileLine {
    param([string] $Mode, [string] $Label)
    return ("qa: NOT APPLICABLE - no file in scope $Mode $Label").TrimEnd()
}

# A branch scope from the parsed review-files list (C): exit 5 when nothing changed (F65), 2 when it could
# not tell (F69); a file deleted, or no longer on disk (Sizes has no entry), is excluded as deleted.
function Resolve-PaperQaBranchScope {
    param($ReviewFiles, $Sizes, [string] $Label)
    if ($ReviewFiles.ExitCode -eq 5) { return [pscustomobject]@{ ExitCode = 5; Line = (Format-PaperQaNoFileLine 'branch' $Label); Files = @(); Excluded = @() } }
    if ($ReviewFiles.ExitCode -ne 0) { return [pscustomobject]@{ ExitCode = 2; Line = "qa: not verifiable: $($ReviewFiles.Reason)"; Files = @(); Excluded = @() } }
    $files = @(); $excluded = @()
    foreach ($f in @($ReviewFiles.Files)) {
        if ($f.Status -eq 'deleted' -or $null -eq $Sizes -or -not $Sizes.Contains($f.Path)) { $excluded += [pscustomobject]@{ Path = $f.Path; Reason = 'deleted' }; continue }
        $files += [pscustomobject]@{ Path = $f.Path; Bytes = [long] $Sizes[$f.Path] }
    }
    $excluded += @($ReviewFiles.Excluded | Where-Object { $null -ne $_ })
    if ($files.Count -eq 0) { return [pscustomobject]@{ ExitCode = 5; Line = (Format-PaperQaNoFileLine 'branch' $Label); Files = @(); Excluded = $excluded } }
    return [pscustomobject]@{ ExitCode = 0; Line = ''; Files = $files; Excluded = $excluded }
}

# D.1 rule 7: a .NET project anywhere in the repository.
function Test-PaperQaDotnetProject {
    param([string[]] $Paths)
    return (@($Paths | Where-Object { "$_" -match '\.(csproj|vbproj|fsproj|sln)$' }).Count -gt 0)
}

# qa.ui.screens: the images of the repository its glob matches.
function Get-PaperQaScreens {
    param([string[]] $Paths, [string] $Glob, [string] $Folder)
    if (-not $Glob) { return @() }
    $f = ConvertTo-PaperQaRelPath $Folder
    return @($Paths | Where-Object {
            "$_" -match '\.(png|jpe?g)$' -and (Test-PaperQaGlob $_ $Glob) -and (-not $f -or "$_".StartsWith("$f/", [StringComparison]::OrdinalIgnoreCase))
        })
}

# The batches of every LLM lane that runs (E), as plan.json keeps them: Lane, Batch, Files (paths).
function Get-PaperQaBatchList {
    param($Lanes, $LaneFiles, [int] $MaxTokens, $Rules = $null)
    $out = @()
    foreach ($l in @($Lanes)) {
        if ($l.State -ne 'run' -or $l.Name -eq 'static') { continue }
        $files = @()
        if ($null -ne $LaneFiles -and $LaneFiles.Contains($l.Name)) { $files = @($LaneFiles[$l.Name]) }
        $n = 0
        # Split-PaperQaBatches returns its array of batches as one object; @() around the call would wrap it again.
        $split = Split-PaperQaBatches -Files $files -MaxTokens $MaxTokens
        foreach ($b in $split) {
            $n++
            $paths = @($b | ForEach-Object { $_.Path })
            # Not $rules: PowerShell names are case-insensitive, so that would be the -Rules parameter.
            $own = @()
            if ($l.Name -eq 'architecture' -and @($Rules | Where-Object { $null -ne $_ }).Count -gt 0) { $own = @(Get-PaperQaBatchRules -Rules $Rules -BatchPaths $paths | ForEach-Object { $_.Path }) }
            $out += [pscustomobject]@{ Lane = $l.Name; Batch = $n; Files = $paths; Rules = $own }
        }
    }
    return $out
}

# A run id: the time stamp, with -2, -3 ... when a run of the same second exists.
function Get-PaperQaRunId {
    param([string] $Stamp, [string[]] $Existing)
    $id = $Stamp; $k = 1
    while (@($Existing) -contains $id) { $k++; $id = "$Stamp-$k" }
    return $id
}

# D.3: what the analyzers already reported, for the smell lane (smell groups) and the security lane
# (vulnerability groups): the ten biggest, so the agent does not report them again. '' when none.
function Get-PaperQaAlreadyReported {
    param([string] $Lane, $Groups)
    $kind = switch ($Lane) { 'smell' { 'smell' } 'security' { 'vulnerability' } default { '' } }
    if (-not $kind) { return '' }
    $mine = @($Groups | Where-Object { $null -ne $_ -and $_.Kind -eq $kind })
    $top = Sort-PaperQaByKey $mine { param($g) "$((1000000000 - [int] $g.Count).ToString('D10'))`t$($g.RuleId)" }
    $top = @($top | Select-Object -First 10)
    if ($top.Count -eq 0) { return '' }
    return ('Already reported by analyzers (do not repeat): ' + (@($top | ForEach-Object { "$($_.RuleId) x$($_.Count)" }) -join ', '))
}

# The list paths a "not read:" line names (H.3, F60): compared like WHERE (\ as /, no ./, any case). No
# known name, or none on the list: the whole batch goes back - never an empty list (find-bug FB9).
function Get-PaperQaNotReadPaths {
    param([string[]] $NotRead, [string[]] $Paths, [string] $Seen)
    $named = @($NotRead | Where-Object { $_ -and $_ -notlike 'unknown*' } | ForEach-Object { ConvertTo-PaperQaRelPath $_ })
    $hit = @($Paths | Where-Object { $p = $_; @($named | Where-Object { $_ -ieq $p.Replace('\', '/') }).Count -gt 0 })
    if ($hit.Count -eq 0) { return @($Paths) }
    # Fewer list files named than the seen line leaves unseen: the list is incomplete, so the whole batch
    # goes back - a file never read must not count as read (find-bug FB27).
    $m = [regex]::Match("$Seen", '^(\d+)/(\d+)$')
    if ($m.Success -and $hit.Count -lt ([int] $m.Groups[2].Value - [int] $m.Groups[1].Value)) { return @($Paths) }
    return $hit
}

function Get-PaperQaLaneRow($Plan, [string] $Name) { return @($Plan.Lanes | Where-Object { $_.Name -eq $Name })[0] }
function Get-PaperQaPlanBatches($Plan, [string] $Name) {
    # Sort-PaperQaByKey returns its array as one object: assigned first, never wrapped in @() (find-bug FB1).
    $sorted = Sort-PaperQaByKey @($Plan.Batches | Where-Object { $_.Lane -eq $Name }) { param($b) ([int] $b.Batch).ToString('D9') }
    return $sorted
}

# `qa.ps1 files` (K, F63): the list of one lane batch in the review-files shape, or why there is none.
# RetryAnswer: the saved answer of that batch when -Retry, so only the files it did not read go back.
function Get-PaperQaFilesList {
    param($Plan, [string] $Lane, [int] $Batch, [string] $Run, [bool] $Retry, [string[]] $RetryAnswer, $StaticGroups)
    $res = { param($code, $lines) [pscustomobject]@{ ExitCode = $code; Lines = @($lines) } }
    if ($script:PaperQaLaneNames -notcontains $Lane) { return & $res 2 @("qa: unknown lane '$Lane' ($($script:PaperQaLaneNames -join ', '))") }
    if ($Lane -eq 'static') { return & $res 2 @('qa: the static lane has no agent list - run qa.ps1 static') }
    $row = Get-PaperQaLaneRow $Plan $Lane
    if ($null -eq $row -or $row.State -ne 'run') { return & $res 5 @("qa: lane $Lane $($row.State) - $($row.Reason)") }
    if (-not (Test-PaperQaRunApproved -Plan $Plan -Lane $Lane)) { return & $res 4 @("qa: run $Run is not approved - show the estimate, then qa.ps1 approve -Run $Run") }
    $all = @(Get-PaperQaPlanBatches $Plan $Lane)
    if ($Batch -lt 1 -or $Batch -gt $all.Count) { return & $res 2 @("qa: lane $Lane has batch 1 to $($all.Count), not $Batch") }
    $paths = @($all[$Batch - 1].Files)
    $prefix = Get-PaperQaPrefix $Lane $Batch $all.Count
    if ($Retry -and $null -ne $RetryAnswer) {
        $a = Test-PaperQaLaneAnswer -Lines $RetryAnswer -Lane $Lane -Prefix $prefix -ListPaths $paths -FileCount $paths.Count
        if (-not $a.Complete) { $paths = @(Get-PaperQaNotReadPaths -NotRead $a.NotRead -Paths $paths -Seen $a.Seen) }
    }
    $lines = @("qa-files: $($paths.Count) to review - lane $Lane batch $Batch of $($all.Count), run $Run, ids $prefix-<n>")
    if ($Plan.External -eq $true) { $lines += "repo: $($Plan.RepoRoot) (external, read only) - every path below is relative to it" }
    $lines += @($paths | ForEach-Object { "  $_" })
    $batchRules = @()
    if ($null -ne $all[$Batch - 1].PSObject.Properties['Rules']) { $batchRules = @($all[$Batch - 1].Rules | Where-Object { $_ }) }
    if ($batchRules.Count -gt 0) { $lines += "rules: $($batchRules -join ', ')" }
    $already = Get-PaperQaAlreadyReported -Lane $Lane -Groups $StaticGroups
    if ($already) { $lines += $already }
    $lines += "total: $($paths.Count)"
    return & $res 0 $lines
}

# The static findings of static.json as findings of the run; none when the lane did not run or failed.
function ConvertTo-PaperQaStaticFindings($Static) {
    if ($null -eq $Static -or $Static.Verdict -ne 'ran') { return @() }
    return @($Static.Findings | Where-Object { $null -ne $_ } | ForEach-Object {
            [pscustomobject]@{
                Id = $_.Id; Lane = 'static'; Kind = $_.Kind; Severity = $_.Severity; Where = $_.Where; Path = $_.Path; Line = $_.Line
                Rule = "$($_.RuleId) $($_.Title)".Trim(); Input = $null; Why = "$($_.Message)"; Fix = "$($_.Link)"
            }
        })
}

# `qa.ps1 check` (H, I): every lane batch's answer and its second answer (Answers: "<lane>-<b>" and
# "<lane>-<b>.2" -> lines), checked; what to send back, the lanes not verifiable, seen a/b per lane, and
# the verify queue with the static findings. ExitCode 1 when something goes back.
function Get-PaperQaCheck {
    param($Plan, $Answers, $Static, [int] $VerifyCap)
    $errors = @(); $findings = @(); $laneStates = @{}; $seen = @{}
    foreach ($l in @($Plan.Lanes)) {
        if ($l.State -ne 'run' -or $l.Name -eq 'static') { continue }
        $batches = @(Get-PaperQaPlanBatches $Plan $l.Name)
        $a = 0; $b = 0
        foreach ($bt in $batches) {
            $n = [int] $bt.Batch
            $paths = @($bt.Files)
            $b += $paths.Count
            $tag = "$($l.Name)-$n"
            $prefix = Get-PaperQaPrefix $l.Name $n $batches.Count
            if ($null -eq $Answers -or -not $Answers.Contains($tag)) {
                $errors += "${tag}: no answer saved"
                $laneStates[$l.Name] = "no answer saved for $tag"
                continue
            }
            $first = Test-PaperQaLaneAnswer -Lines $Answers[$tag] -Lane $l.Name -Prefix $prefix -ListPaths $paths -FileCount $paths.Count
            $second = $null
            if ($Answers.Contains("$tag.2")) {
                $count = -1
                if (-not $first.Complete) { $count = @(Get-PaperQaNotReadPaths -NotRead $first.NotRead -Paths $paths -Seen $first.Seen).Count }
                $second = Test-PaperQaLaneAnswer -Lines $Answers["$tag.2"] -Lane $l.Name -Prefix $prefix -ListPaths $paths -FileCount $count
            }
            $res = Resolve-PaperQaLaneBatch -First $first -Second $second
            $findings += @($res.Findings)
            if ($res.State -eq 'ok') { $a += $paths.Count }
            else {
                $s = 0
                if ($first.Seen -match '^(\d+)/') { $s = [int] $Matches[1] }
                $a += [math]::Min($s, $paths.Count)
            }
            if ($res.State -eq 'retry') {
                # Until its second answer is saved, the batch is not finished: never a clean lane (find-bug FB21).
                $laneStates[$l.Name] = "$tag not sent back yet: $($res.Reason)"
                foreach ($e in @($first.Errors)) { $errors += "${tag}: $e" }
                if (-not $first.Complete -and @($first.Errors).Count -eq 0) { $errors += "${tag}: not read: $(@($first.NotRead) -join ', ')" }
            }
            elseif ($res.State -eq 'not verifiable') { $laneStates[$l.Name] = "$tag $($res.Reason)" }
        }
        $seen[$l.Name] = "$a/$b"
    }
    $findings += @(ConvertTo-PaperQaStaticFindings $Static)
    # No agent lane runs (-StaticOnly, -Only static, all skipped): the estimate counted no reader and the run
    # approved itself, so no reader starts (F63, find-bug FB17).
    $noAgent = [bool] $Plan.StaticOnly -or -not (Test-PaperQaAgentLaneRuns -Lanes $Plan.Lanes)
    $queue = @(Get-PaperQaVerifyQueue -Findings $findings -VerifyCap $VerifyCap -StaticOnly $noAgent)
    $lines = @($errors)
    foreach ($k in @($laneStates.Keys | Sort-Object)) { $lines += "${k}: not verifiable ($($laneStates[$k]))" }
    $queued = Sort-PaperQaByKey @($queue | Where-Object { $_.Status -eq 'queued' }) { param($f) ([int] $f.QueueOrder).ToString('D9') }
    foreach ($f in $queued) { $lines += "verify: $($f.Id) ($($f.Lane), $($f.Severity))" }
    $code = 0
    if ($errors.Count -gt 0) { $code = 1 }
    return [pscustomobject]@{ ExitCode = $code; Errors = $errors; Queue = $queue; LaneStates = $laneStates; Seen = $seen; Lines = $lines }
}

# The Summary rows of the report (J, F56): a static lane that ran but has no static.json, or failed, is
# not verifiable; an LLM lane with a batch not verifiable or never answered is too.
function Get-PaperQaReportLanes {
    param($PlanLanes, $Static, $LaneStates, $Seen)
    $out = @()
    foreach ($l in @($PlanLanes)) {
        $state = $l.State; $reason = $l.Reason; $s = ''
        if ($state -eq 'run' -and $l.Name -eq 'static') {
            if ($null -eq $Static) { $state = 'not verifiable'; $reason = 'qa.ps1 static was not run' }
            elseif ($Static.Verdict -ne 'ran') { $state = 'not verifiable'; $reason = "$($Static.Reason)" }
        }
        elseif ($state -eq 'run') {
            if ($null -ne $Seen -and $Seen.Contains($l.Name)) { $s = $Seen[$l.Name] }
            if ($null -ne $LaneStates -and $LaneStates.Contains($l.Name)) {
                $state = 'not verifiable'; $reason = $LaneStates[$l.Name]
                if ($s) { $reason += " (seen $s)" }
            }
        }
        $out += [pscustomobject]@{ Name = $l.Name; State = $state; Reason = $reason; Seen = $s }
    }
    return $out
}

# static.json (as read back) to the Static object Format-PaperQaReport groups; $null when there is none.
function ConvertFrom-PaperQaStaticJson {
    param($Static)
    if ($null -eq $Static) { return $null }
    # A static lane that did not run to the end has no groups to show: its reason, never 0 per kind (FB12).
    if ($Static.Verdict -ne 'ran') {
        return [pscustomobject]@{ Verdict = $Static.Verdict; Reason = $Static.Reason; Analyzers = $Static.Analyzers; Build = $Static.Build; Result = $null }
    }
    $byId = @{}
    foreach ($f in @($Static.Findings | Where-Object { $null -ne $_ })) { $byId[$f.Id] = $f }
    $groups = @($Static.Groups | Where-Object { $null -ne $_ } | ForEach-Object {
            $g = $_
            [pscustomobject]@{ RuleId = $g.RuleId; Kind = $g.Kind; Severity = $g.Severity; Count = $g.Count; Title = $g.Title; Link = $g.Link; Source = $g.Source; Hotspot = [bool] $g.Hotspot; Findings = @($g.Ids | ForEach-Object { $byId[$_] }) }
        })
    $counts = [ordered]@{}
    if ($null -ne $Static.Counts) { foreach ($p in $Static.Counts.PSObject.Properties) { $counts[$p.Name] = $p.Value } }
    return [pscustomobject]@{
        Verdict = $Static.Verdict; Reason = $Static.Reason; Analyzers = $Static.Analyzers; Build = $Static.Build
        Result = [pscustomobject]@{ Groups = $groups; Counts = $counts; Findings = @($Static.Findings) }
    }
}

# `qa.ps1 report` (J): the report's name and text, and the lines to print. VerdictLines: one string[]
# per saved verdict file.
function New-PaperQaReport {
    param($Plan, $Check, $VerdictLines, $Static, [string] $Run, [string] $Date, [string] $ReportDir, [string[]] $Existing)
    $pending = @($Check.Queue | Where-Object { $_.Status -eq 'queued' } | ForEach-Object { $_.Id })
    $verdicts = @()
    foreach ($v in @($VerdictLines)) { if ($null -ne $v) { $verdicts += ConvertFrom-PaperQaVerdict -Lines $v -Pending $pending } }
    $merged = @(Merge-PaperQaVerdicts -Findings $Check.Queue -Verdicts $verdicts)
    $external = ($Plan.External -eq $true)
    $proposals = @(Get-PaperQaLedgerProposals -Findings $merged -Run $Run -External:$external)
    $name = Get-PaperQaReportName -Date $Date -Mode $Plan.Mode -Label $Plan.Label -Existing $Existing
    $report = [pscustomobject]@{
        Run = $Run; Mode = $Plan.Mode; Label = $Plan.Label; Reason = $Plan.Reason; Date = $Date
        FileCount = @($Plan.Files).Count; ExcludedCount = @($Plan.Excluded).Count; EstimateTotal = [long] $Plan.Estimate.Total; ReportDir = $ReportDir
        Lanes = @(Get-PaperQaReportLanes -PlanLanes $Plan.Lanes -Static $Static -LaneStates $Check.LaneStates -Seen $Check.Seen)
        Findings = $merged; Proposals = $proposals; Static = (ConvertFrom-PaperQaStaticJson -Static $Static); Excluded = @($Plan.Excluded)
        External = $external; RepoRoot = $(if ($external) { "$($Plan.RepoRoot)" } else { '' }); ProfilePath = "$($Plan.ProfilePath)"; Rules = @($Plan.Rules | Where-Object { $null -ne $_ })
        ScopeLines = @($Plan.ScopeLines | Where-Object { $_ })
    }
    $text = ((Format-PaperQaReport -Report $report) -join "`n") + "`n"
    $notVerified = @($merged | Where-Object { $_.Status -eq 'needs_validation' }).Count
    $where = if ($external) { "$ReportDir\$name" } else { "$ReportDir/$name" }
    $lines = @("qa: report $where", "qa: $($merged.Count) finding(s), $($proposals.Count) bug ledger proposal(s), $notVerified not verified")
    return [pscustomobject]@{ Name = $name; Text = $text; Lines = $lines }
}
