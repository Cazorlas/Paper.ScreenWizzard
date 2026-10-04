# The pure half of jev-eval: normalise text, apply a recipe's keyword rules, build a Jev request body, read a
# Jev answer, and score predictions against a fixture. No disk, no network, no clock - tests/jev-recipes.tests.ps1
# dot-sources this file. The I/O half is jev-eval.ps1 next to it.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Lower case, Vietnamese accents stripped, d-stroke folded to d, every run of non letters/digits one space.
# Rules match on this form, so "Ong gio", "ong gio" and the accented spelling all hit the same keyword.
function ConvertTo-JevPlainText {
    param([string] $Text)
    if (-not $Text) { return '' }
    $d = $Text.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object Text.StringBuilder
    foreach ($ch in $d.ToCharArray()) {
        if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch) -eq [Globalization.UnicodeCategory]::NonSpacingMark) { continue }
        if ($ch -eq [char]0x0111) { [void]$sb.Append('d'); continue }
        if ($ch -eq [char]0x0110) { [void]$sb.Append('d'); continue }
        [void]$sb.Append([char]::ToLowerInvariant($ch))
    }
    $s = [regex]::Replace($sb.ToString(), '[^a-z0-9]+', ' ')
    return $s.Trim()
}

# True when the keyword (already plain or not) occurs in the plain text on word boundaries. A keyword of
# several words matches as a phrase.
function Test-JevKeyword {
    param([string] $Plain, [string] $Keyword)
    $k = ConvertTo-JevPlainText $Keyword
    if (-not $k) { return $false }
    return (" $Plain " -like "* $k *")
}

# The labelled fields of a state "label: value | label: value": split on ' | ', each part on its first ': '.
# Labels are trimmed and lower case, values trimmed; a part without ': ' (or starting with it) is dropped and the
# first of two equal labels wins. Returns an ordered hashtable label -> value.
function Get-JevStateFields {
    param([string] $State)
    $out = [ordered]@{}
    if (-not $State) { return $out }
    foreach ($part in $State.Split([string[]]@(' | '), [StringSplitOptions]::None)) {
        $i = $part.IndexOf(': ')
        if ($i -le 0) { continue }
        $label = $part.Substring(0, $i).Trim().ToLowerInvariant()
        if ($out.Contains($label)) { continue }
        $out[$label] = $part.Substring($i + 2).Trim()
    }
    return $out
}

# The share of the distinct plain words of $Of (minus every plain word of $Ignore) that occur among the plain
# words of $In, as a double; 0 when nothing of $Of is left. Numbers are words too.
function Get-JevOverlap {
    param([string] $Of, [string] $In, [string[]] $Ignore)
    $skip = @{}
    foreach ($w in @($Ignore)) { foreach ($t in (ConvertTo-JevPlainText $w).Split(' ')) { if ($t) { $skip[$t] = $true } } }
    $inWords = @{}
    foreach ($t in (ConvertTo-JevPlainText $In).Split(' ')) { if ($t) { $inWords[$t] = $true } }
    $ofWords = @{}
    foreach ($t in (ConvertTo-JevPlainText $Of).Split(' ')) { if ($t -and -not $skip.ContainsKey($t)) { $ofWords[$t] = $true } }
    if ($ofWords.Count -eq 0) { return [double]0 }
    $found = 0
    foreach ($t in $ofWords.Keys) { if ($inWords.ContainsKey($t)) { $found++ } }
    return ([double]$found / [double]$ofWords.Count)
}

# One question of a recipe's rules.json:
#   { default, default_confidence, overlap_ignore:[], rules: [ { option, any:[], none:[], overlap:{ of, in, min }, confidence } ] }.
# A rule hits when its "any" hits (when it has any), its overlap reaches min (when it has overlap: both fields in
# the state and Get-JevOverlap of field "of" in field "in", minus overlap_ignore, -ge min), and its "none" does
# not hit; a rule with neither any nor overlap never hits. The first rule that hits wins.
# Returns @{ Choice; Confidence; Source }.
function Invoke-JevRuleQuestion {
    param([string] $Text, $QuestionRules)
    $plain = ConvertTo-JevPlainText $Text
    $fields = $null
    $ignore = @(@($QuestionRules.overlap_ignore) | Where-Object { $null -ne $_ } | ForEach-Object { [string]$_ })
    foreach ($r in @($QuestionRules.rules)) {
        if ($null -eq $r.any -and $null -eq $r.overlap) { continue }
        if ($null -ne $r.any) {
            $hit = $false
            foreach ($k in @($r.any)) { if (Test-JevKeyword $plain $k) { $hit = $true; break } }
            if (-not $hit) { continue }
        }
        if ($null -ne $r.overlap) {
            if ($null -eq $fields) { $fields = Get-JevStateFields $Text }
            $of = [string]$r.overlap.of
            $in = [string]$r.overlap.in
            if (-not $of -or -not $in -or -not $fields.Contains($of) -or -not $fields.Contains($in)) { continue }
            if ((Get-JevOverlap $fields[$of] $fields[$in] $ignore) -lt [double]$r.overlap.min) { continue }
        }
        $blocked = $false
        foreach ($k in @($r.none)) { if (Test-JevKeyword $plain $k) { $blocked = $true; break } }
        if ($blocked) { continue }
        $c = 0.9
        if ($null -ne $r.confidence) { $c = [double]$r.confidence }
        return @{ Choice = [string]$r.option; Confidence = $c; Source = 'rules' }
    }
    $dc = 0.3
    if ($null -ne $QuestionRules.default_confidence) { $dc = [double]$QuestionRules.default_confidence }
    return @{ Choice = [string]$QuestionRules.default; Confidence = $dc; Source = 'default' }
}

# Every question of the recipe answered by rules. Returns an ordered hashtable questionId -> answer.
function Invoke-JevRules {
    param([string] $Text, $Rules)
    $out = [ordered]@{}
    foreach ($p in $Rules.PSObject.Properties) { $out[$p.Name] = Invoke-JevRuleQuestion $Text $p.Value }
    return $out
}

# The request body of POST /v1/systemone: the state text, the model, and the recipe's questions verbatim.
function New-JevRequestBody {
    param([string] $State, $Questions, [string] $Model)
    return [ordered]@{ state = $State; model = $Model; questions = $Questions }
}

# Reads answers.<id>.choice / .confidence for every question; a missing answer, a choice outside the
# question's options or a confidence outside 0-1 is $null for that question (never a guess).
function Read-JevAnswer {
    param($Response, $Questions)
    $out = [ordered]@{}
    foreach ($q in $Questions.PSObject.Properties) {
        $out[$q.Name] = $null
        if ($null -eq $Response -or $null -eq $Response.answers) { continue }
        $a = $Response.answers.($q.Name)
        if ($null -eq $a -or $null -eq $a.choice -or $null -eq $a.confidence) { continue }
        $options = @($q.Value.criteria.PSObject.Properties | ForEach-Object { $_.Name })
        $choice = [string]$a.choice
        if ($options -notcontains $choice) { continue }
        # A confidence that is not a number is no answer, never a thrown error: one odd answer must not keep a
        # whole run from being recorded.
        $c = 0.0
        if (-not [double]::TryParse([string]$a.confidence, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref] $c)) { continue }
        if ($c -lt 0 -or $c -gt 1) { continue }
        $out[$q.Name] = @{ Choice = $choice; Confidence = $c; Source = 'jev' }
    }
    return $out
}

# Why a Jev response counts as a failed call, or '' when it carries answers. An HTTP 200 whose body has no
# answers (an upstream error passed through) is a failure: otherwise every question would fall back to the
# rules and the rules' score would be recorded as Jev's.
function Get-JevResponseFailure {
    param($Response)
    if ($null -eq $Response -or $null -eq $Response.answers) { return 'the response has no answers' }
    return ''
}

# One row's prediction: per question, Jev's answer when it gave one inside the options, else the rules' answer.
# No consistency rule of the recipe is applied after Jev (SKILL.md, "Do mot cong thuc").
function Merge-JevPrediction {
    param($JevAnswer, $RuleAnswer, $Questions)
    $merged = [ordered]@{}
    foreach ($q in $Questions.PSObject.Properties.Name) {
        $a = $null
        if ($JevAnswer) { $a = $JevAnswer[$q] }
        if ($null -eq $a) { $a = $RuleAnswer[$q] }
        $merged[$q] = $a
    }
    return $merged
}

# Problems of a recipe's parts against each other, as strings; empty means consistent.
#   every rules question is a question, every rule option and default is one of its options,
#   every overlap rule has of, in and a min in (0, 1],
#   every fixture row is synthetic, has an id and input, expects only known questions and options, and carries
#   every state field an overlap rule of its questions reads.
function Test-JevRecipeParts {
    param($Questions, $Rules, [object[]] $Rows)
    $problems = New-Object System.Collections.Generic.List[string]
    $qOptions = @{}
    foreach ($q in $Questions.PSObject.Properties) {
        if ($q.Value.type -ne 'choice') { $problems.Add("question $($q.Name): type must be choice") }
        $opts = @($q.Value.criteria.PSObject.Properties | ForEach-Object { $_.Name })
        if ($opts.Count -lt 2) { $problems.Add("question $($q.Name): fewer than two options") }
        if ($opts.Count -gt 255) { $problems.Add("question $($q.Name): more than 255 options") }
        $qOptions[$q.Name] = $opts
    }
    # question -> the state labels its overlap rules read, in rule order
    $overlapFields = [ordered]@{}
    foreach ($r in $Rules.PSObject.Properties) {
        if (-not $qOptions.ContainsKey($r.Name)) { $problems.Add("rules $($r.Name): not a question"); continue }
        if ($qOptions[$r.Name] -notcontains [string]$r.Value.default) { $problems.Add("rules $($r.Name): default '$($r.Value.default)' is not an option") }
        foreach ($rule in @($r.Value.rules)) {
            if ($qOptions[$r.Name] -notcontains [string]$rule.option) { $problems.Add("rules $($r.Name): option '$($rule.option)' is not an option") }
            if ($null -eq $rule.overlap) { continue }
            $ov = $rule.overlap
            $min = $ov.min
            $minOk = ($min -is [int] -or $min -is [long] -or $min -is [double] -or $min -is [decimal] -or $min -is [single]) -and [double]$min -gt 0 -and [double]$min -le 1
            if (-not ($ov.of -is [string] -and $ov.of -and $ov.in -is [string] -and $ov.in -and $minOk)) {
                $problems.Add("rules $($r.Name): overlap of rule '$($rule.option)' needs of, in and a min above 0 and at most 1")
            }
            if (-not $overlapFields.Contains($r.Name)) { $overlapFields[$r.Name] = New-Object System.Collections.Generic.List[string] }
            foreach ($f in @($ov.of, $ov.in)) {
                if ($f -is [string] -and $f -and -not $overlapFields[$r.Name].Contains($f)) { $overlapFields[$r.Name].Add($f) }
            }
        }
    }
    foreach ($q in $qOptions.Keys) { if (-not ($Rules.PSObject.Properties.Name -contains $q)) { $problems.Add("rules: question $q has no rules") } }
    # At least 20 rows and at least a third of them hard: rows written next to the rules always pass the rules,
    # so only the hard rows (the failure patterns seen on real data) say anything about where Jev helps.
    $n = @($Rows).Count
    $hard = @($Rows | Where-Object { $_.case -eq 'hard' }).Count
    if ($n -lt 20) { $problems.Add("fixture: $n rows, at least 20 needed") }
    if ($hard * 3 -lt $n) { $problems.Add("fixture: $hard hard rows of $n, at least a third needed") }
    $ids = @{}
    foreach ($row in $Rows) {
        $id = [string]$row.id
        if (-not $id) { $problems.Add('fixture: a row has no id'); continue }
        if ($ids.ContainsKey($id)) { $problems.Add("fixture ${id}: duplicate id") }
        $ids[$id] = $true
        # Only the JSON value true marks a row synthetic: 1 or "true" would compare equal to $true.
        if (-not ($row.synthetic -is [bool] -and $row.synthetic)) { $problems.Add("fixture ${id}: not marked synthetic") }
        if (-not $row.input) { $problems.Add("fixture ${id}: no input") }
        if (@('plain', 'hard') -notcontains [string]$row.case) { $problems.Add("fixture ${id}: case must be plain or hard") }
        if ($overlapFields.Count -gt 0) {
            $rowFields = Get-JevStateFields ([string]$row.input)
            foreach ($q in $overlapFields.Keys) {
                foreach ($f in $overlapFields[$q]) { if (-not $rowFields.Contains($f)) { $problems.Add("fixture ${id}: no '$f' field for the overlap rules of $q") } }
            }
        }
        # A row that expects nothing would count as right for rules and Jev alike.
        if ($null -eq $row.expect -or @($row.expect.PSObject.Properties).Count -eq 0) { $problems.Add("fixture ${id}: expects nothing"); continue }
        foreach ($e in $row.expect.PSObject.Properties) {
            if (-not $qOptions.ContainsKey($e.Name)) { $problems.Add("fixture ${id}: expects unknown question $($e.Name)"); continue }
            if ($qOptions[$e.Name] -notcontains [string]$e.Value) { $problems.Add("fixture ${id}: '$($e.Value)' is not an option of $($e.Name)") }
        }
    }
    return ,$problems.ToArray()
}

# Scores predictions (rowId -> questionId -> answer or $null) against the fixture rows.
# A row counts as right only when every expected question is right; a $null answer is wrong.
# HardWrong lists the ids of the hard rows that are wrong, in fixture order.
function Get-JevScore {
    param([object[]] $Rows, $Predictions)
    $perQ = [ordered]@{}
    $hardWrong = New-Object System.Collections.Generic.List[string]
    $rowsRight = 0
    $hardRight = 0
    $hardRows = 0
    foreach ($row in $Rows) {
        $all = $true
        foreach ($e in $row.expect.PSObject.Properties) {
            if (-not $perQ.Contains($e.Name)) { $perQ[$e.Name] = @{ Right = 0; Total = 0 } }
            $perQ[$e.Name].Total++
            $p = $Predictions[[string]$row.id][$e.Name]
            if ($null -ne $p -and $p.Choice -eq [string]$e.Value) { $perQ[$e.Name].Right++ } else { $all = $false }
        }
        if ($all) { $rowsRight++ }
        if ($row.case -eq 'hard') { $hardRows++; if ($all) { $hardRight++ } else { $hardWrong.Add([string]$row.id) } }
    }
    return @{ RowsRight = $rowsRight; Rows = @($Rows).Count; HardRight = $hardRight; HardRows = $hardRows; PerQuestion = $perQ; HardWrong = $hardWrong.ToArray() }
}

# The line a recipe's measurement section carries, so the test can compare it with a fresh offline run.
function Format-JevRulesLine {
    param($Score)
    return ('rules: {0}/{1} rows, hard {2}/{3}' -f $Score.RowsRight, $Score.Rows, $Score.HardRight, $Score.HardRows)
}

# The line a recipe records after a real Jev run, printed by jev-eval.ps1 so it is copied, never typed.
function Format-JevLine {
    param($Score, [string] $Model, [string] $Date)
    return ('jev ({0}, {1}): {2}/{3} rows, hard {4}/{5}' -f $Model, $Date, $Score.RowsRight, $Score.Rows, $Score.HardRight, $Score.HardRows)
}

# True when a Jev switch (PAPER_JEV, or a product's PAPER_<FEATURE>_JEV) holds an off value: 0, false, off or no,
# trimmed, any case. Anything else - unset, 1, yes - is on: Jev is on by default (ADR-0042).
function Test-JevSwitchOff {
    param([string] $Value)
    $v = ([string]$Value).Trim().ToLowerInvariant()
    return (@('0', 'false', 'off', 'no') -contains $v)
}

# Which way a -Jev run goes, from the switch and the two keys of this run's environment. The PowerShell form of
# the JevSwitchPolicy a product writes: 'off' when the switch holds an off value, whatever the keys; else 'call' -
# OpenRouter with its Jev model when its key exists (the main path, even beside a TypeSafe key), TypeSafe when only
# its key exists; 'no-key' when both keys are empty.
function Get-JevRoute {
    param([string] $Switch, [string] $TypeSafeKey, [string] $OpenRouterKey)
    if (Test-JevSwitchOff $Switch) { return @{ Outcome = 'off'; Endpoint = ''; Model = ''; Key = '' } }
    # OpenRouter: base https://openrouter.ai/api + /v1/systemone. Base /api/v1 plus /v1 answers 404.
    if ($OpenRouterKey) { return @{ Outcome = 'call'; Endpoint = 'https://openrouter.ai/api/v1/systemone'; Model = 'typesafe/jev-1.13'; Key = $OpenRouterKey } }
    if ($TypeSafeKey) { return @{ Outcome = 'call'; Endpoint = 'https://api.typesafe.ai/v1/systemone'; Model = 'jev-latest'; Key = $TypeSafeKey } }
    return @{ Outcome = 'no-key'; Endpoint = ''; Model = ''; Key = '' }
}

# Problems of the numbers a recipe.md records, as strings (nothing when they agree; callers wrap in @()).
#   the rules line `rules: X/Y rows, hard H/K` equal to a fresh offline run ($RulesLine);
#   every jev line `jev (<model>, <yyyy-MM-dd>): X/Y rows, hard H/K` alone in backticks on its line, Y the fixture's
#   rows, K its hard rows, X <= Y, H <= K. A row whose text changed but whose count did not is not caught.
function Get-JevRecordProblems {
    param([string[]] $Lines, [object[]] $Rows, [string] $RulesLine)
    $problems = New-Object System.Collections.Generic.List[string]
    $n = @($Rows).Count
    $hard = @($Rows | Where-Object { $_.case -eq 'hard' }).Count
    if (@($Lines) -notcontains ('`' + $RulesLine + '`')) { $problems.Add("rules line missing or stale: the recipe should carry ``$RulesLine`` on a line of its own") }
    foreach ($l in @($Lines)) {
        $t = ([string]$l).Trim()
        if (-not $t.Contains('`jev (')) { continue }
        # A jev line inside a sentence or a list item is still a jev line: it must stand alone to be checked.
        if (-not $t.StartsWith('`jev (')) { $problems.Add("jev line not alone on its line: $t"); continue }
        $m = [regex]::Match($t, '^`jev \(([^,()]+), (\d{4}-\d{2}-\d{2})\): (\d+)/(\d+) rows, hard (\d+)/(\d+)`$')
        if (-not $m.Success) { $problems.Add("jev line not in the form ``jev (<model>, <yyyy-MM-dd>): X/Y rows, hard H/K``: $t"); continue }
        $x = [int]$m.Groups[3].Value; $y = [int]$m.Groups[4].Value; $h = [int]$m.Groups[5].Value; $k = [int]$m.Groups[6].Value
        if ($y -ne $n) { $problems.Add("jev line counts $y rows, the fixture has ${n}: measure again or drop the line: $t") }
        if ($k -ne $hard) { $problems.Add("jev line counts $k hard rows, the fixture has ${hard}: measure again or drop the line: $t") }
        if ($x -gt $y) { $problems.Add("jev line has $x rows right of ${y}: $t") }
        if ($h -gt $k) { $problems.Add("jev line has $h hard rows right of ${k}: $t") }
    }
    return $problems.ToArray()
}

# Problems between the skill's recipe list and the recipe folders, as strings (callers wrap in @()); none: every folder is
# linked as ](recipes/<slug>/recipe.md) and every such link has its folder.
function Get-JevRecipeLinkProblems {
    param([string] $SkillText, [string[]] $RecipeNames)
    $problems = New-Object System.Collections.Generic.List[string]
    $linked = @([regex]::Matches([string]$SkillText, '\]\(recipes/([a-z0-9-]+)/recipe\.md\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $names = @($RecipeNames)
    foreach ($l in $linked) { if ($names -notcontains $l) { $problems.Add("SKILL.md links recipe $l, which has no folder under recipes/") } }
    foreach ($r in ($names | Sort-Object)) { if ($linked -notcontains $r) { $problems.Add("recipe $r has a folder but SKILL.md does not link it") } }
    return $problems.ToArray()
}
