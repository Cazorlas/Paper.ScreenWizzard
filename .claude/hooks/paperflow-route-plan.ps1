# The prompt router as a pure function: a prompt, the project profile and the route file of each host in,
# the context to add (or $null) out. No file system, no stdin: paperflow-route.ps1 reads, this decides, so
# every branch is a test with plain data. Dot-sourced; declares no param() block.
#
# Why a router at all. A project owner asked that every task on their host - a code change or a change to
# the model - run through paperflow without typing /paperflow. A memory note said so already and did not
# hold: a rule binds only the reader who recalls it at the start of a task. The first router lived in one
# project with its keywords and its texts written into the script; this is the same mechanism with every
# value moved to where it belongs (docs/adr/0009 of the kit repository): the stage text here, the host's
# words and loop order in the host pack's route file, the project's own words in its profile.
#
# Three sources of keywords, all regular-expression fragments matched as whole words, ignoring case:
#   - work types: generic words below, per work type, plus routing.workTypes.<type>.keywords
#   - hosts: .claude/paperflow/routes/<host>.json of each host pack, plus routing.hosts.<host>.keywords
#   - routing.useDefaults false drops the first half of both: the project's own words only
# Only the work types the profile declares are routed (no workTypes = code only, as paperflow reads it).
#
# It matches words, so it can be wrong both ways. It never blocks: a false match costs one paragraph of
# context, and the text says to carry on when the prompt is only a question.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI, so Vietnamese is written as \u escapes,
# which the .NET regex engine reads itself.

$script:PaperRouteDefaultKeywords = @{
    # Changing the program. Fix, bug, feature, refactor, and the Vietnamese sua (fix), loi (bug),
    # tinh nang (feature), nut (button), lenh (command).
    code  = @(
        'fix(es|ed|ing)?', 'bugs?', 'implement(s|ed|ing)?', 'refactor(s|ed|ing)?', 'features?', 'crash(es|ed)?',
        'buttons?', 'commands?', 'unit tests?',
        's\u1EEDa', 'l\u1ED7i', 't\u00EDnh n\u0103ng', 'n\u00FAt', 'l\u1EC7nh'
    )
    # Changing the user's model or drawing. Model, drawing, draw, and ve (draw), dung (build up),
    # mo hinh (model), ban ve (drawing), bo tri (lay out).
    model = @(
        '(?<!view\s?)models?', 'model(l)?ing', 'drawings?', 'draw(s|n)?', 'sketch(es)?',
        'v\u1EBD', 'd\u1EF1ng', 'm\u00F4 h\u00ECnh', 'b\u1EA3n v\u1EBD', 'b\u1ED1 tr\u00ED'
    )
}

# Map keys whether the profile arrived as nested hashtables (hook-input) or as a PSCustomObject.
function Get-PaperRouteValue($Map, [string[]] $Keys) {
    $v = $Map
    foreach ($k in $Keys) {
        if ($null -eq $v) { return $null }
        if ($v -is [System.Collections.IDictionary]) { $v = $v[$k]; continue }
        $p = $v.PSObject.Properties[$k]
        if ($null -eq $p) { return $null }
        $v = $p.Value
    }
    return $v
}

function Get-PaperRouteKeys($Map) {
    if ($null -eq $Map) { return @() }
    if ($Map -is [System.Collections.IDictionary]) { return @($Map.Keys | ForEach-Object { [string] $_ }) }
    return @($Map.PSObject.Properties | ForEach-Object { $_.Name })
}

# One regex over a keyword list, or $null for an empty list. A letter or digit on either side means the
# keyword is part of a longer word: "cad" in "cascade", "ong" in "trong". A fragment that does not compile
# is dropped rather than taking the whole router down with it.
function New-PaperRouteRegex([string[]] $Keywords) {
    $ok = New-Object System.Collections.Generic.List[string]
    foreach ($k in @($Keywords)) {
        if ([string]::IsNullOrWhiteSpace($k)) { continue }
        try { [void] [regex]::new($k); $ok.Add($k) } catch { }
    }
    if ($ok.Count -eq 0) { return $null }
    $pattern = '(?<![\p{L}\p{N}_])(' + ($ok -join '|') + ')(?![\p{L}\p{N}_])'
    return [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

function Find-PaperRouteKeyword($Regex, [string] $Text) {
    if ($null -eq $Regex) { return $null }
    $m = $Regex.Match($Text)
    if ($m.Success) { return $m.Value }
    return $null
}

function Get-PaperRoute {
    <#
    .SYNOPSIS
    $null (not routed) or WorkType, WorkTypeKeyword, Host, HostKeyword, Context for one prompt.
    .PARAMETER Prompt
    The prompt as typed.
    .PARAMETER ProjectProfile
    .claude/paper.profile.json as a map.
    .PARAMETER HostRoutes
    host -> the host pack's route file as a map (keywords, loop, liveFirst). A host with no file is absent.
    .PARAMETER ModelIndex
    RULE.md, SAMPLE, REFERENCE.md and REFERENCE paths under the feature docs, for a model prompt.
    .PARAMETER DocsRel
    The feature docs folder as the profile spells it, named beside the index.
    #>
    param(
        [AllowNull()][AllowEmptyString()][string] $Prompt,
        $ProjectProfile,
        [hashtable] $HostRoutes = @{},
        [string[]] $ModelIndex = @(),
        [string] $DocsRel = 'docs/features'
    )

    if ([string]::IsNullOrWhiteSpace($Prompt)) { return $null }
    # A slash command is the user choosing the flow themselves, /paperflow included.
    if ($Prompt.TrimStart().StartsWith('/')) { return $null }
    # Typed Vietnamese can arrive decomposed (e + combining tilde); the keywords are precomposed.
    $text = $Prompt.Normalize([System.Text.NormalizationForm]::FormC)

    $useDefaults = $true
    $flag = Get-PaperRouteValue $ProjectProfile @('routing', 'useDefaults')
    if ($null -ne $flag -and -not [bool] $flag) { $useDefaults = $false }

    # ---- work types: only the ones this project declares
    $types = @(Get-PaperRouteKeys (Get-PaperRouteValue $ProjectProfile @('workTypes')) | Where-Object { $_ -ne '$comment' })
    if ($types.Count -eq 0) { $types = @('code') }
    $typeHits = New-Object System.Collections.Generic.List[psobject]
    foreach ($t in $types) {
        $words = @()
        if ($useDefaults -and $script:PaperRouteDefaultKeywords.ContainsKey($t)) { $words += $script:PaperRouteDefaultKeywords[$t] }
        $words += @(Get-PaperRouteValue $ProjectProfile @('routing', 'workTypes', $t, 'keywords') | Where-Object { $_ })
        $hit = Find-PaperRouteKeyword (New-PaperRouteRegex $words) $text
        if ($hit) { $typeHits.Add([pscustomobject]@{ Type = $t; Keyword = $hit }) }
    }

    # ---- hosts: the declared ones, and any the routing block names
    $hosts = New-Object System.Collections.Generic.List[string]
    foreach ($h in @(Get-PaperRouteValue $ProjectProfile @('hosts'))) { if ($h -and -not $hosts.Contains([string] $h)) { $hosts.Add([string] $h) } }
    foreach ($h in @(Get-PaperRouteKeys (Get-PaperRouteValue $ProjectProfile @('routing', 'hosts')))) { if ($h -ne '$comment' -and -not $hosts.Contains($h)) { $hosts.Add($h) } }
    $hostHit = $null
    foreach ($h in $hosts) {
        $words = @()
        if ($useDefaults -and $HostRoutes.ContainsKey($h)) { $words += @(Get-PaperRouteValue $HostRoutes[$h] @('keywords') | Where-Object { $_ }) }
        $words += @(Get-PaperRouteValue $ProjectProfile @('routing', 'hosts', $h, 'keywords') | Where-Object { $_ })
        $hit = Find-PaperRouteKeyword (New-PaperRouteRegex $words) $text
        if ($hit) { $hostHit = [pscustomobject]@{ Host = $h; Keyword = $hit }; break }
    }

    if ($typeHits.Count -eq 0 -and $null -eq $hostHit) { return $null }

    $workType = if ($typeHits.Count -eq 1) { $typeHits[0].Type } else { 'unclear' }
    $lines = New-Object System.Collections.Generic.List[string]

    if ($workType -ne 'unclear') {
        $lines.Add("PaperFlow route: work type $workType (matched '$($typeHits[0].Keyword)').")
    }
    else {
        $seen = if ($typeHits.Count -gt 0) { ' (matched ' + (($typeHits | ForEach-Object { "$($_.Type) '$($_.Keyword)'" }) -join ', ') + ')' } else { '' }
        $lines.Add("PaperFlow route: work type unclear$seen - decide it from the request: code changes the program, model changes the user's model or drawing (declared: $($types -join ', ')). Still unclear: ask one question with those as the choices, and stop (paperflow section 0).")
    }
    if ($null -ne $hostHit) { $lines.Add("Host: $($hostHit.Host) (matched '$($hostHit.Keyword)').") }
    elseif ($hosts.Count -gt 0) { $lines.Add("Host: none named in the prompt; this project declares $($hosts -join ', ').") }

    $lines.Add('Invoke the paperflow skill with the request before other work, even though /paperflow was not typed. Skip the stages that do not apply and name them in the report; the read-back gate always runs. When the prompt is only a question and nothing in it changes the program, a model or a drawing, answer it and carry on without the flow.')

    # ---- the loop order of the host: the named one, or else every declared host that has one
    $loopHosts = if ($null -ne $hostHit) { @($hostHit.Host) } else { @($hosts) }
    if ($workType -ne 'model') {
        foreach ($h in $loopHosts) {
            $loop = @(Get-PaperRouteValue $ProjectProfile @('routing', 'hosts', $h, 'loop') | Where-Object { $_ })
            if ($loop.Count -eq 0 -and $HostRoutes.ContainsKey($h)) { $loop = @(Get-PaperRouteValue $HostRoutes[$h] @('loop') | Where-Object { $_ }) }
            if ($loop.Count -eq 0) { continue }
            $liveFirst = $false
            if ($HostRoutes.ContainsKey($h)) { $liveFirst = [bool] (Get-PaperRouteValue $HostRoutes[$h] @('liveFirst')) }
            $line = "Code loop order on ${h}: " + ($loop -join ' -> ') + '.'
            $liveLoop = [string] (Get-PaperRouteValue $ProjectProfile @('live', 'loop'))
            if ($liveFirst -and $liveLoop) {
                $line += " Each step is one call of the project's loop script: $liveLoop <step> (ensure, measure, red, green, verify, loop)."
            }
            elseif ($liveFirst) {
                $line += ' This project declares no live.loop in its profile, so run each step through the host pack''s live skill by hand.'
            }
            $guard = [string] (Get-PaperRouteValue $ProjectProfile @('live', 'dialogGuard'))
            if ($liveFirst -and $guard) { $line += " Keep the dialog guard running while calls are in flight: $guard." }
            $lines.Add($line)
        }
    }
    if ($workType -ne 'code') {
        if ($types -contains 'model') {
            $lines.Add('Model loop order: survey read-only -> dry run with nothing kept -> commit -> independent read-back, every step citing its rule codes (skill model-task).')
        }
        if (@($ModelIndex).Count -gt 0) {
            $lines.Add("Modelling rules, samples and reference documents ($DocsRel): " + (@($ModelIndex) -join ', ') +
                ". For a model edit, read the project-wide and the feature's RULE.md, SAMPLE and REFERENCE.md before writing the brief, cite the rule codes in the plan, " +
                'write any rule the user states, shows or models in this prompt into RULE.md, and any document or link they hand over into REFERENCE.md, in this same turn (skill model-task).')
        }
    }

    return [pscustomobject]@{
        WorkType        = $workType
        WorkTypeKeyword = if ($typeHits.Count -gt 0) { $typeHits[0].Keyword } else { $null }
        Host            = if ($null -ne $hostHit) { $hostHit.Host } else { $null }
        HostKeyword     = if ($null -ne $hostHit) { $hostHit.Keyword } else { $null }
        Context         = ($lines -join ' ')
    }
}

# Modelling rules, samples and reference documents under the feature docs: "<feature>/RULE.md",
# "<feature>/SAMPLE", "<feature>/REFERENCE.md", "<feature>/REFERENCE", ordinal order. A rule the agent has
# to recall is a rule it forgets (a project's insulation table arrived as an image and was never written
# down), so the router lists them on every model prompt. Found at run time: no feature name is written here.
function Get-PaperModelRuleIndex([string] $FeaturesRoot) {
    if ([string]::IsNullOrEmpty($FeaturesRoot) -or -not (Test-Path -LiteralPath $FeaturesRoot)) { return @() }
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($feature in Get-ChildItem -LiteralPath $FeaturesRoot -Directory) {
        if (Test-Path -LiteralPath (Join-Path $feature.FullName 'RULE.md')) { $found.Add("$($feature.Name)/RULE.md") }
        if (Test-Path -LiteralPath (Join-Path $feature.FullName 'SAMPLE') -PathType Container) { $found.Add("$($feature.Name)/SAMPLE") }
        if (Test-Path -LiteralPath (Join-Path $feature.FullName 'REFERENCE.md')) { $found.Add("$($feature.Name)/REFERENCE.md") }
        if (Test-Path -LiteralPath (Join-Path $feature.FullName 'REFERENCE') -PathType Container) { $found.Add("$($feature.Name)/REFERENCE") }
    }
    $sorted = $found.ToArray(); [Array]::Sort($sorted, [StringComparer]::Ordinal)
    return @($sorted)
}
