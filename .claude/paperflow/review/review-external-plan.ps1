# /qa on an external repository (plan 2026-10-03-qa-external-repo, tables B and C; ADR-0034): a repository
# /qa reads but never writes into. Everything a run makes lives under -Out: the qa profile of that repository
# (review.profile.json), the run folders and the reports. This file holds the decisions of that mode: where -Out
# may be, which paperflow runs, the qa profile and its errors, the declared build and its no-deploy
# properties, the repository's own rule files, and what git saw change during the build.
# Pure, no I/O: review.ps1 reads git, the disk and processes. The caller dot-sources paperflow/tasks-gate-plan.ps1,
# paperflow/review-files-plan.ps1, review-sarif-plan.ps1 and review-plan.ps1 first; declares no param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperReviewExternalKeys = @('repo', 'hosts', 'build', 'rules', 'review', 'qa')
$script:PaperReviewPropertyPattern = '^[A-Za-z_][A-Za-z0-9_.]*=[^\s;"]+$'
$script:PaperReviewProfileComment = 'review profile of a repository the review commands read but never write into (review.ps1 -Repo <repository> -Out <this folder>). build.command: the build line, run from the repository root. build.noDeploy: every MSBuild property that turns deployment off, as Name=Value, each also written in build.command as -p:Name=Value. rules: the repository''s own rule files the architecture lane judges against (paths or globs; remove the key for the defaults). review: the review keys of a project profile (the old name qa is read too), except report and staticAnalysis.sarifDir.'

# A path compared as a folder: / as \, no trailing \, any case.
function ConvertTo-PaperReviewPathKey([string] $Path) { return "$Path".Replace('/', '\').TrimEnd('\').ToLowerInvariant() }

function Test-PaperReviewOutsideRepo {
    <#
    .SYNOPSIS
    Whether -Out lies outside every given root of the repository (F91): $false when it is empty, one of the
    roots, or under one of them. Several roots because one folder has several spellings (8.3 and long).
    #>
    param([string] $Out, [string[]] $RepoRoots)
    if (-not "$Out".Trim()) { return $false }
    $o = ConvertTo-PaperReviewPathKey $Out
    foreach ($r in @($RepoRoots)) {
        if (-not "$r".Trim()) { continue }
        $k = ConvertTo-PaperReviewPathKey $r
        if ($o -eq $k -or $o.StartsWith("$k\")) { return $false }
    }
    return $true
}

function Get-PaperReviewPaperflowCandidates {
    <#
    .SYNOPSIS
    Where review.ps1 looks for paperflow (F97, D7): the folder above the engine, in every mode. The engine sits in
    .claude/paperflow/review/, so that folder is the kit's own paperflow - vendored in a project, or in the plugin.
    The repository, external or not, never supplies one.
    #>
    param([string] $ScriptRoot, [string] $RepoRoot, [bool] $External)
    return @((Join-Path $ScriptRoot '..'))
}

function Test-PaperReviewStringList($Value) {
    return (($Value -is [System.Array]) -and (@($Value | Where-Object { $_ -isnot [string] }).Count -eq 0))
}

function ConvertFrom-PaperReviewExternalProfile {
    <#
    .SYNOPSIS
    review.profile.json of an external repository (B.1) to the config ConvertFrom-PaperReviewProfile gives, plus
    External, Repo, BuildCommand, NoDeploy, DeclaredRules ($null when the key is absent). Errors names each
    wrong key in the order of the file (F92, F93, F95). Report and SarifDir stay '': both would write into
    the repository.
    #>
    param($Profile, [string] $RepoRoot)
    $isDict = $Profile -is [System.Collections.IDictionary]
    $hosts = @()
    $reviewClean = $null
    $reviewErrors = @()
    $rvKey = $null; $qaKey = $null; $keyRead = 'review'
    if ($isDict) {
        if (Test-PaperReviewStringList $Profile['hosts']) { $hosts = @($Profile['hosts']) }
        # The key is "review"; the old "qa" is read when "review" is absent, and both together are an error (F238).
        $rvKey = $Profile['review']; $qaKey = $Profile['qa']
        $keyRead = 'qa'; $q = $qaKey
        if ($null -ne $rvKey) { $keyRead = 'review'; $q = $rvKey }
        if ($q -is [System.Collections.IDictionary]) {
            $reviewClean = [ordered]@{}
            foreach ($k in @($q.Keys)) {
                if ($k -ceq 'report') { $reviewErrors += "${keyRead}.report: not used for an external repository - the report goes to the reports folder of -Out"; continue }
                if ($k -ceq 'staticAnalysis' -and $q[$k] -is [System.Collections.IDictionary]) {
                    $sa = [ordered]@{}
                    foreach ($s in @($q[$k].Keys)) {
                        if ($s -ceq 'sarifDir') { $reviewErrors += "${keyRead}.staticAnalysis.sarifDir: not allowed for an external repository - it would write into it; the SARIF files stay in the run folder"; continue }
                        $sa[$s] = $q[$k][$s]
                    }
                    $reviewClean[$k] = $sa
                    continue
                }
                $reviewClean[$k] = $q[$k]
            }
        }
        elseif ($null -ne $q) { $reviewClean = $q }
    }
    $forBase = [ordered]@{ hosts = $hosts }
    if ($isDict -and $null -ne $rvKey -and $null -ne $qaKey) { $forBase['review'] = $reviewClean; $forBase['qa'] = $qaKey }
    else { $forBase[$keyRead] = $reviewClean }
    $base = ConvertFrom-PaperReviewProfile -Profile $forBase
    $base.Report = ''
    $base.SarifDir = ''
    $base.ArchitectureDeclared = $false
    $cfg = $base
    $cfg | Add-Member -NotePropertyName External -NotePropertyValue $true -Force
    $cfg | Add-Member -NotePropertyName Repo -NotePropertyValue '' -Force
    $cfg | Add-Member -NotePropertyName BuildCommand -NotePropertyValue '' -Force
    $cfg | Add-Member -NotePropertyName NoDeploy -NotePropertyValue ([string[]] @()) -Force
    $cfg | Add-Member -NotePropertyName DeclaredRules -NotePropertyValue $null -Force
    if (-not $isDict) { $cfg.Errors = @('review.profile.json: not a JSON object'); return $cfg }

    $errors = @()
    $repoValue = $Profile['repo']
    if ($repoValue -isnot [string] -or -not $repoValue.Trim()) { $errors += 'repo: missing - the repository this profile is for' }
    $noDeploy = @()
    $keyReported = $false
    foreach ($key in @($Profile.Keys)) {
        $v = $Profile[$key]
        switch -CaseSensitive ($key) {
            'repo' {
                if ($v -is [string] -and $v.Trim()) {
                    $cfg.Repo = $v
                    if ((ConvertTo-PaperReviewPathKey $v) -ne (ConvertTo-PaperReviewPathKey $RepoRoot)) { $errors += "repo: this profile is for $v, not $RepoRoot" }
                }
            }
            'hosts' { if (-not (Test-PaperReviewStringList $v)) { $errors += 'hosts: not a list of strings' } }
            'build' {
                if ($v -isnot [System.Collections.IDictionary]) { $errors += 'build: not an object'; break }
                foreach ($b in @($v.Keys)) {
                    $bv = $v[$b]
                    switch -CaseSensitive ($b) {
                        'command' { if ($bv -isnot [string]) { $errors += 'build.command: not a string' } else { $cfg.BuildCommand = $bv } }
                        'noDeploy' {
                            if (-not (Test-PaperReviewStringList $bv)) { $errors += 'build.noDeploy: not a list of strings'; break }
                            foreach ($item in @($bv)) {
                                if ($item -notmatch $script:PaperReviewPropertyPattern) { $errors += "build.noDeploy: '$item' is not Name=Value" }
                                else { $noDeploy += $item }
                            }
                        }
                        default { $errors += "build.${b}: unknown key (command, noDeploy)" }
                    }
                }
                $statement = ''
                if ("$($cfg.BuildCommand)".Trim()) { $statement = (Get-PaperReviewCommandTokens -Command $cfg.BuildCommand).Problem }
                if ($statement) { $errors += "build.command: $statement" }
                elseif ("$($cfg.BuildCommand)".Trim()) {
                    foreach ($item in $noDeploy) {
                        if (-not (Test-PaperReviewCommandProperty -Command $cfg.BuildCommand -Property $item)) { $errors += "build.noDeploy: $item is not in build.command - write -p:$item there" }
                    }
                }
            }
            'rules' {
                if (-not (Test-PaperReviewStringList $v)) { $errors += 'rules: not a list of strings'; break }
                foreach ($item in @($v)) { if (-not (Test-PaperReviewInsideRepo $item)) { $errors += "rules: '$item' must be a path inside the repository" } }
                $cfg.DeclaredRules = [string[]] @($v)
            }
            { $_ -ceq 'review' -or $_ -ceq 'qa' } { if (-not $keyReported) { $keyReported = $true; $errors += $reviewErrors; $errors += @($base.Errors) } }
            default { $errors += "${key}: unknown key ($($script:PaperReviewExternalKeys -join ', '))" }
        }
    }
    $cfg.NoDeploy = [string[]] @($noDeploy)
    $cfg.Errors = $errors
    return $cfg
}

function Get-PaperReviewCommandTokens {
    <#
    .SYNOPSIS
    The words of a build line as PowerShell splits them (Spec bo sung 2026-10-03, find-bug T8 FB1): quotes kept
    in each token; Problem is set when the line holds more than one statement - an unquoted ;, |, ||, &&, a
    line break, or a backtick line continuation - because PowerShell runs what follows as another command and
    MSBuild never sees a property written after it. A ; inside quotes stays in its token.
    #>
    param([string] $Command)
    $s = "$Command".Trim()
    $bt = [char] 96
    $tokens = New-Object 'System.Collections.Generic.List[string]'
    $cur = New-Object System.Text.StringBuilder
    $quote = [char] 0
    $problem = ''
    for ($i = 0; $i -lt $s.Length; $i++) {
        $c = $s[$i]
        $next = if ($i + 1 -lt $s.Length) { $s[$i + 1] } else { [char] 0 }
        if ($quote -ne [char] 0) {
            [void] $cur.Append($c)
            if ($quote -eq [char] '"' -and $c -eq $bt -and $next -ne [char] 0) { [void] $cur.Append($next); $i++; continue }
            if ($c -eq $quote) { $quote = [char] 0 }
            continue
        }
        $sep = ''
        if ($c -eq [char] ';') { $sep = ';' }
        elseif ($c -eq [char] '|') { $sep = $(if ($next -eq [char] '|') { '||' } else { '|' }) }
        elseif ($c -eq [char] '&' -and $next -eq [char] '&') { $sep = '&&' }
        elseif ($c -eq [char] "`r" -or $c -eq [char] "`n") { $sep = 'a line break' }
        elseif ($c -eq $bt -and ($next -eq [char] 0 -or $next -eq [char] "`r" -or $next -eq [char] "`n")) { $sep = 'a backtick line continuation' }
        if ($sep) {
            $problem = 'more than one statement (unquoted ' + $sep + ') - write one build command; put a ; between properties inside quotes: "-p:A=1;B=2"'
            break
        }
        if ($c -eq [char] '"' -or $c -eq [char] "'") { $quote = $c; [void] $cur.Append($c); continue }
        if ($c -eq $bt) { [void] $cur.Append($next); $i++; continue }
        if ($c -eq [char] ' ' -or $c -eq [char] "`t") {
            if ($cur.Length -gt 0) { $tokens.Add($cur.ToString()); [void] $cur.Clear() }
            continue
        }
        [void] $cur.Append($c)
    }
    if (-not $problem -and $cur.Length -gt 0) { $tokens.Add($cur.ToString()) }
    return [pscustomobject]@{ Tokens = [string[]] $tokens.ToArray(); Problem = $problem }
}

# One pair of quotes around the whole text dropped.
function Remove-PaperReviewOuterQuotes([string] $Text) {
    if ($Text.Length -ge 2 -and (($Text.StartsWith('"') -and $Text.EndsWith('"')) -or ($Text.StartsWith("'") -and $Text.EndsWith("'")))) { return $Text.Substring(1, $Text.Length - 2) }
    return $Text
}

# The text split on ; and , that stand outside quotes, each piece with its quotes dropped.
function Split-PaperReviewPropertyList([string] $Text) {
    $out = @()
    $cur = New-Object System.Text.StringBuilder
    $quote = [char] 0
    foreach ($c in $Text.ToCharArray()) {
        if ($quote -ne [char] 0) { if ($c -eq $quote) { $quote = [char] 0 } else { [void] $cur.Append($c) }; continue }
        if ($c -eq [char] '"' -or $c -eq [char] "'") { $quote = $c; continue }
        if ($c -eq [char] ';' -or $c -eq [char] ',') { $out += $cur.ToString(); [void] $cur.Clear(); continue }
        [void] $cur.Append($c)
    }
    $out += $cur.ToString()
    return $out
}

function Test-PaperReviewCommandProperty {
    <#
    .SYNOPSIS
    Whether the LAST assignment of a property in a build line is the given value (B.2, Spec bo sung
    2026-10-03): every form MSBuild reads (-p:, /p:, -property:, /property:, --property:, any case), values
    split on ; and , outside quotes. MSBuild keeps the last value a name gets, so a later assignment that
    undoes it does not count; a line with more than one statement never counts.
    #>
    param([string] $Command, [string] $Property)
    $eq = "$Property".IndexOf('=')
    if ($eq -lt 1) { return $false }
    $name = $Property.Substring(0, $eq)
    $value = $Property.Substring($eq + 1)
    $t = Get-PaperReviewCommandTokens -Command $Command
    if ($t.Problem) { return $false }
    $last = $null
    foreach ($tok in @($t.Tokens)) {
        $m = [regex]::Match((Remove-PaperReviewOuterQuotes $tok), '^(--|[-/])(p|property):(.+)$', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if (-not $m.Success) { continue }
        foreach ($pair in @(Split-PaperReviewPropertyList (Remove-PaperReviewOuterQuotes $m.Groups[3].Value))) {
            $i = $pair.IndexOf('=')
            if ($i -lt 1) { continue }
            if ($pair.Substring(0, $i).Trim() -ieq $name) { $last = $pair.Substring($i + 1) }
        }
    }
    return ($null -ne $last -and $last -ieq $value)
}

function Get-PaperReviewExternalBuild {
    <#
    .SYNOPSIS
    Whether the declared build may run (B.3, F93): a build line and at least one no-deploy property.
    #>
    param($Config)
    if (-not "$($Config.BuildCommand)".Trim()) { return [pscustomobject]@{ Run = $false; Reason = 'no build command in review.profile.json (build.command)' } }
    if (@($Config.NoDeploy | Where-Object { $_ }).Count -eq 0) { return [pscustomobject]@{ Run = $false; Reason = 'build.noDeploy names no property - deploying is not ruled out' } }
    return [pscustomobject]@{ Run = $true; Reason = '' }
}

# Paths once each whatever their case (first spelling kept), sorted ordinal by their lower case.
function Get-PaperReviewUniqueSorted([string[]] $Paths) {
    $seen = @{}
    $keep = New-Object 'System.Collections.Generic.List[string]'
    foreach ($p in @($Paths)) {
        if (-not $p) { continue }
        $k = $p.ToLowerInvariant()
        if ($seen.ContainsKey($k)) { continue }
        $seen[$k] = $true
        $keep.Add($p)
    }
    $arr = $keep.ToArray()
    [string[]] $keys = @($arr | ForEach-Object { $_.ToLowerInvariant() })
    if ($arr.Count -gt 1) { [Array]::Sort($keys, $arr, [StringComparer]::Ordinal) }
    return $arr
}

function Get-PaperReviewDefaultRules {
    <#
    .SYNOPSIS
    The rule files of a repository that declares none (C): CLAUDE.md, AGENTS.md, ARCHITECTURE.md, BUILD.md at
    the root; the .md files of docs/adr*/, docs/decisions/ and specs/; CLAUDE.md and AGENTS.md of any folder.
    Agent config (.claude, .agents, .codex) and generated folders are not the repository's rules.
    #>
    param([string[]] $Paths)
    $keep = @()
    foreach ($p in @($Paths)) {
        if (-not $p) { continue }
        $n = $p.Replace('\', '/')
        $first = ($n -split '/')[0].ToLowerInvariant()
        if (@('.claude', '.agents', '.codex') -contains $first) { continue }
        if (Test-PaperReviewGeneratedPath $n) { continue }
        if ($n -match '^(CLAUDE|AGENTS|ARCHITECTURE|BUILD)\.md$' -or $n -match '^docs/adr[^/]*/.+\.md$' -or $n -match '^docs/decisions/.+\.md$' -or $n -match '^specs/.+\.md$' -or $n -match '^.+/(CLAUDE|AGENTS)\.md$') { $keep += $n }
    }
    return (Get-PaperReviewUniqueSorted $keep)
}

function Resolve-PaperReviewRules {
    <#
    .SYNOPSIS
    The rule files the architecture lane judges against (C, F95): the defaults when the profile has no rules
    key; else each entry, a path (any case, the repository's spelling returned) or a glob, which must match.
    #>
    param($Declared, [string[]] $Paths)
    $repo = @($Paths | Where-Object { $_ } | ForEach-Object { $_.Replace('\', '/') })
    if ($null -eq $Declared) { return [pscustomobject]@{ Rules = [string[]] @(Get-PaperReviewDefaultRules -Paths $repo); Errors = @() } }
    $hits = @(); $errors = @()
    foreach ($entry in @($Declared)) {
        if ($null -eq $entry) { continue }
        $v = ConvertTo-PaperReviewRelPath $entry
        if ($v -match '[*?]') {
            $m = @($repo | Where-Object { Test-PaperReviewGlob $_ $v })
            if ($m.Count -eq 0) { $errors += "rules: '$entry' matches no file of the repository" } else { $hits += $m }
        }
        else {
            $m = @($repo | Where-Object { $_ -ieq $v })
            if ($m.Count -eq 0) { $errors += "rules: '$entry' is not a file of the repository" } else { $hits += $m[0] }
        }
    }
    return [pscustomobject]@{ Rules = [string[]] @(Get-PaperReviewUniqueSorted $hits); Errors = $errors }
}

function Get-PaperReviewBatchRules {
    <#
    .SYNOPSIS
    The rule files of one batch (C): a CLAUDE.md or AGENTS.md of a folder only when the batch has a file under
    that folder; every other rule always. Order kept.
    #>
    param($Rules, [string[]] $BatchPaths)
    $out = @()
    foreach ($r in @($Rules)) {
        if ($null -eq $r) { continue }
        $p = "$($r.Path)".Replace('\', '/')
        $leaf = ($p -split '/')[-1]
        $cut = $p.LastIndexOf('/')
        if (($leaf -ieq 'CLAUDE.md' -or $leaf -ieq 'AGENTS.md') -and $cut -gt 0) {
            $folder = $p.Substring(0, $cut) + '/'
            if (@($BatchPaths | Where-Object { $_ -and $_.Replace('\', '/').StartsWith($folder, [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) { continue }
        }
        $out += $r
    }
    return $out
}

function Get-PaperReviewStatusPaths {
    <#
    .SYNOPSIS
    The paths of `git -c core.quotepath=false status --porcelain --untracked-files=all` lines: the new name
    of a rename, quotes and their escapes undone.
    #>
    param([string[]] $Lines)
    $out = @()
    foreach ($l in @($Lines)) {
        $s = "$l"
        if ($s.Length -le 3) { continue }
        $p = $s.Substring(3)
        $arrow = $p.IndexOf(' -> ')
        if ($arrow -ge 0) { $p = $p.Substring($arrow + 4) }
        if ($p.Length -ge 2 -and $p.StartsWith('"') -and $p.EndsWith('"')) { $p = [regex]::Replace($p.Substring(1, $p.Length - 2), '\\(["\\])', '$1') }
        if ($p) { $out += $p }
    }
    return $out
}

function Compare-PaperReviewRepoSnapshot {
    <#
    .SYNOPSIS
    The paths whose mark differs between two snapshots (path -> "XY|length|ticks"), or that only one has;
    ordinal order (F94).
    #>
    param($Before, $After)
    $changed = @()
    $keys = @{}
    foreach ($k in @($Before.Keys) + @($After.Keys)) { if ($null -ne $k) { $keys[$k] = $true } }
    foreach ($k in @($keys.Keys)) {
        $inB = $Before.Contains($k); $inA = $After.Contains($k)
        if (-not $inB -or -not $inA -or "$($Before[$k])" -cne "$($After[$k])") { $changed += $k }
    }
    $arr = [string[]] $changed
    if ($arr.Count -gt 1) { [Array]::Sort($arr, [StringComparer]::Ordinal) }
    return $arr
}

function Format-PaperReviewRepoChange([string[]] $Paths) {
    $all = @($Paths | Where-Object { $_ })
    $text = "the build changed $($all.Count) file(s) in the repository that git does not ignore: " + (@($all | Select-Object -First 10) -join ', ')
    if ($all.Count -gt 10) { $text += " and $($all.Count - 10) more" }
    return $text
}

function New-PaperReviewExternalProfile {
    <#
    .SYNOPSIS
    The skeleton `review.ps1 init` writes: the repository, no host, an empty build line, the rule files found
    (as a list the owner can edit), and an empty review key.
    #>
    param([string] $RepoRoot, [string[]] $Rules)
    return [ordered]@{
        '$comment' = $script:PaperReviewProfileComment
        repo       = $RepoRoot
        hosts      = @()
        build      = [ordered]@{ command = ''; noDeploy = @() }
        rules      = @($Rules | Where-Object { $_ })
        review     = [ordered]@{}
    }
}

function Format-PaperReviewInitLines {
    <#
    .SYNOPSIS
    What `review.ps1 init` prints (G): the profile written or left alone, the rule files, the build, what next.
    #>
    param([string] $ProfilePath, [string] $RepoRoot, [string] $Out, [string[]] $Rules, [bool] $Created, $Build, [string] $Command)
    $named = @($Rules | Where-Object { $_ })
    $lines = @()
    if ($Created) { $lines += "review: wrote $ProfilePath for $RepoRoot" } else { $lines += "review: $ProfilePath exists - not changed" }
    if ($named.Count -eq 0) { $lines += 'review: rules: none found - the architecture lane will not apply' }
    else {
        $t = "review: rules: $($named.Count) file(s) - " + (@($named | Select-Object -First 10) -join ', ')
        if ($named.Count -gt 10) { $t += " and $($named.Count - 10) more" }
        $lines += $t
    }
    $next = "review.ps1 plan -Repo $RepoRoot -Out $Out"
    if ($Build.Run) {
        $lines += "review: build: $Command"
        $lines += "next: $next"
    }
    else {
        $lines += "review: build: $($Build.Reason) - the static lane will not apply"
        $lines += "next: fill in build.command (the build line, run from the repository root) and build.noDeploy (every MSBuild property that turns deployment off, also written in the command as -p:Name=Value), then $next"
    }
    return $lines
}
