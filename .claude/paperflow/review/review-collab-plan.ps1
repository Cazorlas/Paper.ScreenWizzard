# /qa through collab, the pure part (ADR-0043; SPEC F205, F210-F213): who answers the lanes, the time of a run, the lines of the
# estimate, the prompt of every lane batch and of every reader, and which agent reads which finding. No I/O: review.ps1 reads the
# machine and the disk and hands the facts to these. The caller dot-sources review-plan.ps1 first (Format-PaperReviewNumber); declares no
# param() block.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Measured 2026-10-04 on collab worker turns (the main session): one Codex turn reads about 1.4 million input tokens, mostly cache.
$script:PaperReviewCodexInputPerTurn = 1400000
# Not measured yet (plan G.3): a Codex lane turn takes this many seconds, plus the seconds per 15 000 content tokens.
$script:PaperReviewCodexTurnBaseSec = 180
$script:PaperReviewCodexTurnSecPer15k = 60
$script:PaperReviewClaudeTurnUsd = 5

# ------------------------------------------------------------------ G.1 who answers

function Get-PaperReviewExecutor {
    <#
    .SYNOPSIS
    Who answers the agent lanes (G.1, ADR-0044 D): solo (the session reads a small scope alone), collab (Codex through a read-only collab
    session) or subagents (Claude subagents of the session). Kind with a Reason, or an Error (review: ...) for a forced choice that cannot
    be met. Checked in order: a word that is no choice, subagents asked, solo asked (over the limit: an error), a small scope on auto
    (SoloTokens within a SoloLimit above 0; SoloTokens -1 means no reading lane runs), a collab turn, -Session codex, collab installed,
    codex on PATH. A solo answer carries SoloReader: codex when collab and codex are here (CollabScript then starts the read-only session
    for a finding) or when Codex is the session, else claude.
    #>
    param([string] $Requested = 'auto', [string] $CollabDir, [bool] $CollabFound, [bool] $CodexFound, [string] $CollabRole, [bool] $CodexCopy, [long] $SoloTokens = -1, [long] $SoloLimit = 0)
    $res = { param($kind, $reason, $err, $script, $reader = '') return [pscustomobject]@{ Kind = $kind; Reason = $reason; Error = $err; CollabScript = $script; SoloReader = $reader } }
    $req = "$Requested".Trim()
    if ($req -eq '') { $req = 'auto' }
    if (@('auto', 'collab', 'subagents', 'solo') -cnotcontains $req) { return & $res '' '' 'review: -Executor must be auto, collab, subagents or solo' '' }
    if ($req -eq 'subagents') { return & $res 'subagents' 'asked (-Executor subagents)' '' '' }
    $inTurn = -not ([string]::IsNullOrWhiteSpace($CollabRole))
    $soloScript = ''
    if (-not $inTurn -and -not $CodexCopy -and $CollabFound -and $CodexFound) { $soloScript = Join-Path (Join-Path $CollabDir 'scripts') 'collab.ps1' }
    $soloReader = 'claude'
    if ($CodexCopy -or $soloScript) { $soloReader = 'codex' }
    if ($req -eq 'solo') {
        if ($SoloTokens -gt $SoloLimit) { return & $res '' '' "review: -Executor solo needs a scope of at most $(Format-PaperReviewNumber $SoloLimit) content tokens; this one has $(Format-PaperReviewNumber $SoloTokens) - name a folder or files" '' }
        return & $res 'solo' 'asked (-Executor solo)' '' $soloScript $soloReader
    }
    if ($req -eq 'auto' -and $SoloLimit -gt 0 -and $SoloTokens -ge 0 -and $SoloTokens -le $SoloLimit) { return & $res 'solo' 'scope within the solo limit' '' $soloScript $soloReader }
    if ($inTurn) {
        if ($req -eq 'collab') { return & $res '' '' 'review: -Executor collab cannot run inside a collab turn (COLLAB_ROLE is set)' '' }
        return & $res 'subagents' 'inside a collab turn (COLLAB_ROLE is set)' '' ''
    }
    if ($CodexCopy) {
        if ($req -eq 'collab') { return & $res '' '' 'review: -Executor collab cannot run from the Codex copy of the skill' '' }
        return & $res 'subagents' 'the Codex copy of the skill runs it' '' ''
    }
    if (-not $CollabFound) {
        if ($req -eq 'collab') { return & $res '' '' "review: -Executor collab needs collab installed at $CollabDir" '' }
        return & $res 'subagents' "collab not installed at $CollabDir" '' ''
    }
    if (-not $CodexFound) {
        if ($req -eq 'collab') { return & $res '' '' 'review: -Executor collab needs codex on PATH' '' }
        return & $res 'subagents' 'codex not on PATH' '' ''
    }
    return & $res 'collab' '' '' (Join-Path (Join-Path $CollabDir 'scripts') 'collab.ps1')
}

function Get-PaperReviewCollabCaps {
    <#
    .SYNOPSIS
    The places for turns at once on the machine and the seconds between Codex starts, from the models.json beside collab's scripts
    folder (collab.maxParallelTurns 1-16, collab.codexStartGapSec 0-600). A missing key, a value out of range or a broken file
    means what collab itself means: one place, 45 seconds.
    #>
    param([string] $ModelsText)
    $n = 1; $g = 45
    $o = $null
    try { if (-not [string]::IsNullOrWhiteSpace($ModelsText)) { $o = ConvertFrom-Json -InputObject $ModelsText } } catch { $o = $null }
    if ($null -ne $o -and $null -ne $o.PSObject.Properties['collab'] -and $null -ne $o.collab) {
        $c = $o.collab
        $v = 0
        if ($null -ne $c.PSObject.Properties['maxParallelTurns'] -and [int]::TryParse("$($c.maxParallelTurns)", [ref] $v) -and $v -ge 1 -and $v -le 16) { $n = $v }
        if ($null -ne $c.PSObject.Properties['codexStartGapSec'] -and [int]::TryParse("$($c.codexStartGapSec)", [ref] $v) -and $v -ge 0 -and $v -le 600) { $g = $v }
    }
    return [pscustomobject]@{ Parallel = $n; GapSec = $g }
}

# The content a session would read alone (D): the files that the reading lanes of the run read, each once, in content tokens (a screenshot
# counts its 1,600); Tokens -1 when no reading lane runs - nothing to read alone.
function Get-PaperReviewSoloContent {
    param($Lanes, $LaneFiles)
    $seen = @{}
    $files = 0; $tokens = [long] 0; $reading = $false
    foreach ($l in @($Lanes)) {
        if ($null -eq $l -or $l.State -ne 'run' -or $l.Name -eq 'static') { continue }
        $reading = $true
        if ($null -eq $LaneFiles -or -not $LaneFiles.Contains($l.Name)) { continue }
        foreach ($f in @($LaneFiles[$l.Name] | Where-Object { $null -ne $_ })) {
            $k = "$($f.Path)".Replace('\', '/').ToLowerInvariant()
            if ($seen.ContainsKey($k)) { continue }
            $seen[$k] = $true
            $files++
            $tokens += Get-PaperReviewFileTokens $f
        }
    }
    if (-not $reading) { return [pscustomobject]@{ Files = 0; Tokens = [long] -1 } }
    return [pscustomobject]@{ Files = $files; Tokens = $tokens }
}

# ------------------------------------------------------------------ G.3 time

function Get-PaperReviewCodexTurnSec {
    # 180 seconds and 60 more for every 15 000 content tokens (not measured yet).
    param([long] $Tokens)
    return [int] ($script:PaperReviewCodexTurnBaseSec + [math]::Floor($Tokens * $script:PaperReviewCodexTurnSecPer15k / 15000))
}

function Get-PaperReviewWallClock {
    <#
    .SYNOPSIS
    The seconds a run takes: -Parallel places free at 0; each turn, in plan order, starts at the later of the earliest free place and
    the previous start plus -GapSec (the first at the earliest free place), takes the first of the earliest places and holds it
    until it is done. The latest finish.
    #>
    param([int[]] $Durations, [int] $Parallel, [int] $GapSec)
    $d = @($Durations | Where-Object { $null -ne $_ })
    if ($d.Count -eq 0) { return 0 }
    $n = [math]::Max(1, $Parallel)
    $free = New-Object 'double[]' $n
    $prevStart = $null
    $latest = [double] 0
    foreach ($dur in $d) {
        $slot = 0
        for ($i = 1; $i -lt $n; $i++) { if ($free[$i] -lt $free[$slot]) { $slot = $i } }
        $start = $free[$slot]
        if ($null -ne $prevStart -and ($prevStart + $GapSec) -gt $start) { $start = $prevStart + $GapSec }
        $end = $start + $dur
        $free[$slot] = $end
        $prevStart = $start
        if ($end -gt $latest) { $latest = $end }
    }
    return [int] $latest
}

function Format-PaperReviewDuration {
    # Minutes rounded up: "28 min", "2 h", "7 h 15 min".
    param([int] $Seconds)
    $m = [int] [math]::Ceiling($Seconds / 60.0)
    if ($m -lt 60) { return "$m min" }
    $h = [int] [math]::Floor($m / 60)
    $r = $m - $h * 60
    if ($r -eq 0) { return "$h h" }
    return "$h h $r min"
}

function Get-PaperReviewExecutorLines {
    <#
    .SYNOPSIS
    The lines the estimate (and, with -Brief, the lane list) print about who answers (G.4): collab - who, how many at once, the time,
    the Codex input and the line that starts the read-only collab session; subagents - the one line with its reason.
    #>
    param([string] $Executor, [string] $Reason, $Collab, [string] $Run, [switch] $Brief, $Solo = $null)
    if ($Executor -eq 'solo') {
        $one = "lanes run: A alone - $($Solo.Files) files, ~$(Format-PaperReviewNumber $Solo.Tokens) content tokens (solo limit $(Format-PaperReviewNumber $Solo.Limit))"
        if ($Brief -or $null -eq $Collab -or -not "$($Collab.CollabScript)") { return @($one) }
        $argS = { param($v) if (Get-Command ConvertTo-PaperReviewArg -ErrorAction SilentlyContinue) { return (ConvertTo-PaperReviewArg ([string] $v)) } else { return [string] $v } }
        $budgetS = $script:PaperReviewClaudeTurnUsd * [int] $Collab.VerifyCap
        return @($one, "collab-start: powershell -NoProfile -ExecutionPolicy Bypass -File $(& $argS $Collab.CollabScript) start -A claude -B codex -Host claude -Repo $(& $argS $Collab.Root) -Slug $(& $argS "review-$Run") -ReadOnly -TimeoutSec 3600 -ClaudeTurnBudgetUsd $($script:PaperReviewClaudeTurnUsd) -SessionBudgetUsd $budgetS (only if check queues a finding)")
    }
    if ($Executor -ne 'collab') { return @("lanes run: claude subagents - $Reason") }
    $script1 = "$($Collab.CollabScript)"
    $n = [int] $Collab.Parallel; $g = [int] $Collab.GapSec
    $out = @("lanes run: codex via collab - $script1 - $n at once on this machine, Codex starts $g s apart; the Claude Sonnet worker takes a batch when Codex is out (up to $($script:PaperReviewClaudeTurnUsd) USD a batch)")
    if ($Brief) { return $out }
    $tokens = @($Collab.TurnTokens | Where-Object { $null -ne $_ })
    $turns = $tokens.Count
    $secs = @($tokens | ForEach-Object { Get-PaperReviewCodexTurnSec -Tokens ([long] $_) })
    $wall = Get-PaperReviewWallClock -Durations ([int[]] $secs) -Parallel $n -GapSec $g
    $mean = 0.0
    if ($turns -gt 0) { $mean = (($secs | Measure-Object -Sum).Sum) / [double] $turns }
    $meanMin = [int] [math]::Round($mean / 60.0, [MidpointRounding]::AwayFromZero)
    $out += "time: $turns turn(s) - about $(Format-PaperReviewDuration -Seconds $wall) (start gate alone $(Format-PaperReviewDuration -Seconds ($turns * $g)); a turn about $meanMin min, not measured yet)"
    $out += "codex input: about $(Format-PaperReviewNumber ($turns * $script:PaperReviewCodexInputPerTurn)) tokens, mostly cached ($(Format-PaperReviewNumber $script:PaperReviewCodexInputPerTurn) a turn, measured 2026-10-04 on collab worker turns)"
    $budget = $script:PaperReviewClaudeTurnUsd * ($turns + [int] $Collab.VerifyCap)
    $arg = { param($v) if (Get-Command ConvertTo-PaperReviewArg -ErrorAction SilentlyContinue) { return (ConvertTo-PaperReviewArg ([string] $v)) } else { return [string] $v } }
    $out += "collab-start: powershell -NoProfile -ExecutionPolicy Bypass -File $(& $arg $script1) start -A claude -B codex -Host claude -Repo $(& $arg $Collab.Root) -Slug $(& $arg "review-$Run") -ReadOnly -TimeoutSec 3600 -ClaudeTurnBudgetUsd $($script:PaperReviewClaudeTurnUsd) -SessionBudgetUsd $budget"
    return $out
}

# ------------------------------------------------------------------ G.6 the reader

function Get-PaperReviewReader {
    <#
    .SYNOPSIS
    Which agent reads a finding back (G.6): never the one that found it while the other is available. A Codex finding goes to Claude;
    a finding of the Claude worker, or of a batch with no record, goes to Codex; a subagent run, a static finding and a plan from
    before this change go to Claude, as before.
    #>
    param([string] $Executor, [string] $Lane, [string] $FinderAgent, [string] $SoloReader = '')
    if ($Executor -eq 'solo') {
        if ($Lane -eq 'static' -or -not $SoloReader) { return 'claude' }
        return $SoloReader
    }
    if ($Executor -ne 'collab') { return 'claude' }
    if ($Lane -eq 'static') { return 'claude' }
    if ($FinderAgent -eq 'codex') { return 'claude' }
    return 'codex'
}

# ------------------------------------------------------------------ G.5 prompts

function Get-PaperReviewPromptTemplate {
    # The first fenced block under "## <Name>" of references/prompts.md, as lines joined with LF.
    param([string] $PromptsText, [string] $Name)
    $lines = @("$PromptsText" -split "`r?`n")
    $i = 0
    while ($i -lt $lines.Count -and $lines[$i].TrimEnd() -ne "## $Name") { $i++ }
    $i++
    while ($i -lt $lines.Count -and -not $lines[$i].StartsWith('```')) { if ($lines[$i].StartsWith('## ')) { return '' }; $i++ }
    $i++
    $out = New-Object System.Collections.Generic.List[string]
    while ($i -lt $lines.Count -and -not $lines[$i].StartsWith('```')) { $out.Add($lines[$i]); $i++ }
    return ($out -join "`n")
}

function Remove-PaperReviewFrontMatter {
    # The text of a file without its YAML front matter (from a first line "---" to the next "---"), edge blank lines trimmed.
    param([string] $Text)
    $lines = @("$Text" -split "`r?`n")
    $start = 0
    if ($lines.Count -gt 0 -and $lines[0].TrimEnd() -eq '---') {
        $j = 1
        while ($j -lt $lines.Count -and $lines[$j].TrimEnd() -ne '---') { $j++ }
        if ($j -lt $lines.Count) { $start = $j + 1 }
    }
    $body = New-Object System.Collections.Generic.List[string]
    for ($k = $start; $k -lt $lines.Count; $k++) { $body.Add($lines[$k]) }
    while ($body.Count -gt 0 -and $body[0].Trim() -eq '') { $body.RemoveAt(0) }
    while ($body.Count -gt 0 -and $body[$body.Count - 1].Trim() -eq '') { $body.RemoveAt($body.Count - 1) }
    return $body.ToArray()
}

function Get-PaperReviewSection {
    # The lines under "## <Heading>" up to the next "## " heading (fenced code is skipped over), edge blank lines trimmed.
    param([string] $Text, [string] $Heading)
    $lines = @("$Text" -split "`r?`n")
    $i = 0
    while ($i -lt $lines.Count -and $lines[$i].TrimEnd() -ne "## $Heading") { $i++ }
    if ($i -ge $lines.Count) { return @() }
    $i++
    $body = New-Object System.Collections.Generic.List[string]
    $fence = $false
    while ($i -lt $lines.Count) {
        $l = $lines[$i]
        if ($l.StartsWith('```')) { $fence = -not $fence }
        if (-not $fence -and $l.StartsWith('## ')) { break }
        $body.Add($l)
        $i++
    }
    while ($body.Count -gt 0 -and $body[0].Trim() -eq '') { $body.RemoveAt(0) }
    while ($body.Count -gt 0 -and $body[$body.Count - 1].Trim() -eq '') { $body.RemoveAt($body.Count - 1) }
    return $body.ToArray()
}

function Expand-PaperReviewTemplate {
    <#
    .SYNOPSIS
    The lines of a prompt template after its conditions: a line that starts with one or more {flag} tags is kept only when every
    flag is true (the tags go); a part between {flag} and {/flag} inside a line is kept or dropped the same way.
    #>
    param([string] $Template, [hashtable] $Flags)
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($raw in ("$Template" -split "`r?`n")) {
        $line = $raw
        $keep = $true
        while ($line -match '^\{([a-z]+)\}') {
            $name = $Matches[1]
            if (-not ($Flags.ContainsKey($name) -and $Flags[$name])) { $keep = $false }
            $line = $line.Substring($Matches[0].Length)
        }
        if (-not $keep) { continue }
        foreach ($name in @($Flags.Keys)) {
            $open = '{' + $name + '}'; $close = '{/' + $name + '}'
            while ($true) {
                $a = $line.IndexOf($open, [StringComparison]::Ordinal)
                if ($a -lt 0) { break }
                $b = $line.IndexOf($close, $a, [StringComparison]::Ordinal)
                if ($b -lt 0) { break }
                $inner = $line.Substring($a + $open.Length, $b - $a - $open.Length)
                if (-not $Flags[$name]) { $inner = '' }
                $line = $line.Substring(0, $a) + $inner + $line.Substring($b + $close.Length)
            }
        }
        $out.Add($line)
    }
    return $out.ToArray()
}

function New-PaperReviewLanePrompt {
    <#
    .SYNOPSIS
    The prompt of one lane batch (G.5): the opening, the list of files exactly as review.ps1 files printed it, the finding format of
    findings.md and the instruction files pasted word for word without their front matter. Retry: the errors of the first answer.
    -Instructions: objects with Path and Text (the ui lane has two).
    #>
    param([string] $Template, [string] $Lane, [int] $Batch, [int] $BatchCount, [string] $Run, [string] $Prefix, [string[]] $FilesLines, [bool] $External,
        [string] $Repo, [bool] $Retry, [string[]] $RetryErrors, [string] $FindingsPath, [string] $FindingsText, $Instructions)
    $flags = @{ retry = $Retry; external = $External; architecture = ($Lane -eq 'architecture'); bug = ($Lane -eq 'bug'); ui = ($Lane -eq 'ui'); project = (-not $External); static = $false }
    $lines = Expand-PaperReviewTemplate -Template $Template -Flags $flags
    $out = New-Object System.Collections.Generic.List[string]
    $first = $true
    $skip = ''   # '' | 'finding' (blank lines, then a placeholder) | 'open' (inside a placeholder that goes on)
    $fill = { param($l) return $l.Replace('<repo>', $Repo).Replace('<PREFIX>', $Prefix) }
    foreach ($line in $lines) {
        if ($skip -eq 'open') { if ($line.TrimEnd().EndsWith('>')) { $skip = '' }; continue }
        if ($skip -eq 'finding') {
            if ($line.Trim() -eq '') { continue }
            if ($line.StartsWith('<the section')) { if (-not $line.TrimEnd().EndsWith('>')) { $skip = 'open' } else { $skip = '' }; continue }
            $skip = ''
        }
        if ($first) {
            $first = $false
            $out.Add($line.Replace('<lane>', $Lane).Replace('<b>', "$Batch").Replace('<n>', "$BatchCount").Replace('<run>', $Run))
            continue
        }
        if ($line.StartsWith('<one line per error')) { foreach ($e in @($RetryErrors)) { $out.Add("$e") }; continue }
        if ($line.StartsWith('<the lines of: review.ps1 files')) { foreach ($f in @($FilesLines)) { $out.Add("$f") }; continue }
        if ($line.StartsWith('## Finding format (')) {
            $out.Add("## Finding format ($FindingsPath)")
            $out.Add('')
            foreach ($l in @(Get-PaperReviewSection -Text $FindingsText -Heading 'A finding')) { $out.Add($l) }
            $skip = 'finding'
            continue
        }
        if ($line.StartsWith('## Instructions for the ')) {
            $n = 0
            foreach ($ins in @($Instructions)) {
                if ($n -gt 0) { $out.Add('') }
                $out.Add("## Instructions for the $Lane lane ($($ins.Path))")
                $out.Add('')
                foreach ($l in @(Remove-PaperReviewFrontMatter -Text "$($ins.Text)")) { $out.Add($l) }
                $n++
            }
            break
        }
        $out.Add((& $fill $line))
    }
    return ($out -join "`n")
}

function New-PaperReviewVerifyPrompt {
    <#
    .SYNOPSIS
    The prompt of one reader (G.5): the RULE verbatim and the INPUT, never WHERE (a static finding only), WHY, FIX, the lane or who
    found it, then the section of find-bug on how to verify. A static finding has no input: its rule id, title and link are the
    RULE line and WHERE starts the search.
    #>
    param([string] $Template, $Finding, [string] $Run, [bool] $External, [string] $Repo, [string] $HowToPath, [string] $HowToText)
    $isStatic = ("$($Finding.Lane)" -eq 'static')
    $flags = @{ external = $External; static = $isStatic }
    $lines = Expand-PaperReviewTemplate -Template $Template -Flags $flags
    $rule = "$($Finding.Rule)"
    if ($isStatic -and -not [string]::IsNullOrWhiteSpace("$($Finding.Fix)")) { $rule = "$rule $($Finding.Fix)".Trim() }
    $inputText = "$($Finding.Input)"
    if ([string]::IsNullOrWhiteSpace($inputText)) { $inputText = '-' }
    $out = New-Object System.Collections.Generic.List[string]
    $first = $true
    $skip = $false
    foreach ($line in $lines) {
        if ($skip) { if ($line.TrimEnd().EndsWith('>')) { $skip = $false }; continue }
        if ($first) { $first = $false; $out.Add($line.Replace('<id>', "$($Finding.Id)").Replace('<run>', $Run)); continue }
        if ($line.StartsWith('RULE      ')) { $out.Add("RULE      $rule"); continue }
        if ($line.StartsWith('INPUT     ')) { $out.Add("INPUT     $inputText"); continue }
        if ($line.StartsWith('WHERE     ')) { $out.Add("WHERE     $($Finding.Where)"); continue }
        if ($line.StartsWith('## How to verify (')) {
            $out.Add("## How to verify ($HowToPath)")
            $out.Add('')
            foreach ($l in @(Get-PaperReviewSection -Text $HowToText -Heading 'Verifying a finding before it becomes a task')) { $out.Add($l) }
            $skip = $true
            continue
        }
        if ($line.StartsWith('<the section')) { if (-not $line.TrimEnd().EndsWith('>')) { $skip = $true }; continue }
        if ($line.Trim() -eq '' -and $out.Count -gt 0 -and $out[$out.Count - 1].StartsWith('## How to verify (')) { continue }
        $out.Add($line.Replace('<repo>', $Repo))
    }
    return ($out -join "`n")
}
