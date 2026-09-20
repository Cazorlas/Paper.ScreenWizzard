# The task gate, as a pure function over the lines of one markdown file: the task's PLAN,
# docs/features/<slug>/YYYY-MM-DD-<task>-plan.md. The requirement lives in SPEC.md and the request in the
# brief beside it; neither carries a status line, so neither is what this gate reads. A task file written
# before paper-kit 0.3.0 (Spec + Tasks + evidence in one file) has the same status line, Tasks heading and
# evidence table, and still parses.
#
# It reads markdown and nothing else - no project, no host, no build. That is what lets the same
# lifecycle run in a web repository and in a desktop add-in, and it is why this file must never learn what a
# host is. Dot-source it; tests/tasks-gate.tests.ps1 covers every branch.
#
# Exit codes (spec rule 10):
#   0  approved, every task ticked, and every tick carries a row in the evidence table
#   1  approved with work left: an open task, or a tick with no evidence
#   2  malformed: no status line, no Tasks heading, a duplicate id, a lane this project does not run,
#      a work type the profile does not declare or a task outside its work type's lanes, a [model] task
#      citing no rule code (F12), two lanes of one group touching the same files (F3), a broken task
#      split (rule 11), or - in an older task file only - an approved Spec that was edited afterwards
#      (rule 13)
#   4  not approved - /task-do must not read code, let alone change it
#
# Work type (kit 1.0): one line "**Loai viec:** code | model" ("**Work type:**" in English, and the older
# spelling "**Loai:** dung hinh" for model). No line = code, which is every plan written before
# the line existed. The profile's workTypes gives each type its lanes and, for model, its rule prefixes.
#
# THIS FILE STAYS ASCII. PowerShell 5.1 decodes a .ps1 with no BOM using the ANSI codepage, and a BOM
# does not survive most rewrites. Vietnamese is matched by stripping diacritics first, which also means
# a user who types "da duyet" without diacritics gets the same answer as one who types "da duyet" with
# them - that is a feature, not a fallback.

# Lanes whose first task must be the red test. `live` is absent on purpose: a lane that needs the host
# running cannot be made to fail before the host exists, so demanding a red step there would only teach
# people to write a fake one.
$script:PaperRedFirstLanes = @('unit', 'ui', 'e2e')

# Rule prefixes a [model] task may cite when the profile declares none: C- for a project-wide rule, R- for
# a feature's own (the RULE.md convention of the first project to edit models).
$script:PaperDefaultRulePrefixes = @('C-', 'R-')

# A profile arrives as a hashtable (tests, paperflow.ps1's ConvertTo-PaperMap) or as the PSCustomObject
# ConvertFrom-Json makes; read both the same way.
function Get-PaperMapValue($Map, [string] $Key) {
    if ($null -eq $Map) { return $null }
    if ($Map -is [System.Collections.IDictionary]) {
        foreach ($k in $Map.Keys) { if ([string]::Equals([string] $k, $Key, [System.StringComparison]::OrdinalIgnoreCase)) { return $Map[$k] } }
        return $null
    }
    $prop = $Map.PSObject.Properties | Where-Object { [string]::Equals($_.Name, $Key, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($prop) { return $prop.Value }
    return $null
}

function Get-PaperMapKeys($Map) {
    if ($null -eq $Map) { return @() }
    if ($Map -is [System.Collections.IDictionary]) { return @($Map.Keys | ForEach-Object { [string] $_ }) }
    return @($Map.PSObject.Properties | Where-Object { -not $_.Name.StartsWith('$') } | ForEach-Object { $_.Name })
}

# ---- {files:} globs (F3) ----------------------------------------------------------------------------
#
# The overlap rule is deliberately coarse, because a false "they meet" only costs an "(sau T<n>)" while a
# false "they do not meet" lets two agents write the same file:
#   - both plain paths (no * or ?)  -> they meet only when equal;
#   - one plain path, one glob      -> they meet when the glob matches the path: ** crosses folders,
#                                      * and ? stay inside one folder name;
#   - both globs                    -> they meet when the literal part before the first wildcard of one
#                                      starts the other's (src/** meets src/Core/*.cs; skills/spec/** does
#                                      not meet skills/task-*/**).
# Paths compare with / for \, without surrounding backticks or a leading ./ or / (both mean the project
# root), ignoring case (Windows), and composed to Unicode NFC: a combining-mark keyboard types a letter
# decomposed (NFD) while the file system hands the same name back composed, and the two never matched. A
# trailing / means the whole folder. Brace alternation {a,b} and [classes] are not globs here: the comma
# separates globs in {files:}.
function ConvertTo-PaperGlobKey([string] $Glob) {
    $g = ([string] $Glob).Trim().Trim('`').Trim().Replace('\', '/')
    if ($g.EndsWith('/')) { $g = $g + '**' }
    while ($g.StartsWith('./') -or $g.StartsWith('/')) {
        if ($g.StartsWith('./')) { $g = $g.Substring(2) } else { $g = $g.Substring(1) }
    }
    return $g.Normalize([System.Text.NormalizationForm]::FormC).ToLowerInvariant()
}

function ConvertTo-PaperGlobRegex([string] $Glob) {
    $sb = New-Object System.Text.StringBuilder
    [void] $sb.Append('^')
    $i = 0
    while ($i -lt $Glob.Length) {
        $ch = $Glob[$i]
        if ($ch -eq '*' -and $i + 1 -lt $Glob.Length -and $Glob[$i + 1] -eq '*') {
            # "**/" also matches no folder at all: src/**/a.cs covers src/a.cs.
            if ($i + 2 -lt $Glob.Length -and $Glob[$i + 2] -eq '/') { [void] $sb.Append('(?:.*/)?'); $i += 3 }
            else { [void] $sb.Append('.*'); $i += 2 }
            continue
        }
        if ($ch -eq '*') { [void] $sb.Append('[^/]*') }
        elseif ($ch -eq '?') { [void] $sb.Append('[^/]') }
        else { [void] $sb.Append([regex]::Escape([string] $ch)) }
        $i++
    }
    [void] $sb.Append('$')
    return $sb.ToString()
}

function Test-PaperGlobOverlap([string] $A, [string] $B) {
    $a = ConvertTo-PaperGlobKey $A
    $b = ConvertTo-PaperGlobKey $B
    $wild = [char[]] @('*', '?')
    $ia = $a.IndexOfAny($wild)
    $ib = $b.IndexOfAny($wild)
    if ($ia -lt 0 -and $ib -lt 0) { return ($a -ceq $b) }
    if ($ia -ge 0 -and $ib -ge 0) {
        $pa = $a.Substring(0, $ia)
        $pb = $b.Substring(0, $ib)
        return ($pa.StartsWith($pb, [System.StringComparison]::Ordinal) -or $pb.StartsWith($pa, [System.StringComparison]::Ordinal))
    }
    if ($ia -ge 0) { return [regex]::IsMatch($b, (ConvertTo-PaperGlobRegex $a)) }
    return [regex]::IsMatch($a, (ConvertTo-PaperGlobRegex $b))
}

# ---- one task line ----------------------------------------------------------------------------------
#
# "- [ ] T7 [red][unit] <text> {files: <glob>, <glob>}" -> the fields any reader of a plan needs, or $null
# when the line is not a task. It is a function and not four lines inside the gate's loop because the Stop
# hook that reminds you to tick has to read the same line the same way: two readers of one syntax is where
# the next leak starts. A line knows neither which section nor which group it sits in, so the caller keeps
# both and adds Group itself.
function Get-PaperPlanTaskEntry([string] $Line, $LaneAliases) {
    # T8a is a sub-task, not a second T8: /task-do adds a repair task below the one it repairs and the
    # field numbers it off the parent so the pair reads together. Without the suffix here, a group of six
    # sub-tasks read as six duplicates of the parent and the gate answered 2 for every plan that had one.
    if ($Line -notmatch '^\s*[-*]\s*\[( |x|X)\]\s*(T\d+[a-z]?)\s*(.*)$') { return $null }
    $ticked = $Matches[1] -ne ' '
    $id = $Matches[2]
    $rest = $Matches[3]
    # {files: <glob>, <glob>} at the end of the line: what this task may write (F3).
    $files = @()
    $tagText = $rest
    if ($rest -match '\{files:\s*(.*)\}\s*$') {
        $files = @($Matches[1] -split ',' | ForEach-Object { $_.Trim().Trim('`').Trim() } | Where-Object { $_ })
        $tagText = $rest.Substring(0, $rest.LastIndexOf('{files:'))
    }
    # The tags are the leading run of [..] straight after the id, and nothing later on the line is one.
    # Matching them anywhere and letting the LAST one win put a task quoting `new string[0]` into a lane
    # called "0", and a task mentioning `byte[16]` into "16" - both rejected as lanes the project does not
    # run, with the real lane sitting in plain sight two words earlier.
    $tags = @()
    $prefix = $tagText
    while ($prefix -match '^\s*\[([a-zA-Z0-9-]+)\]') {
        $tags += $Matches[1].ToLowerInvariant()
        $prefix = $prefix.Substring($prefix.IndexOf(']') + 1)
    }
    $lane = ''
    foreach ($tag in $tags) { if ($tag -ne 'red') { $lane = $tag } }
    $alias = Get-PaperMapValue $LaneAliases $lane
    if ($lane -and $alias) { $lane = ([string] $alias).ToLowerInvariant() }
    return [pscustomobject]@{
        Id     = $id
        Ticked = $ticked
        Lane   = $lane
        Red    = ($tags -contains 'red')
        Text   = $rest
        Files  = $files
    }
}

function Get-PaperPlainText([string] $Text) {
    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $decomposed = $Text.Normalize([System.Text.NormalizationForm]::FormD)
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $decomposed.ToCharArray()) {
        $cat = [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)
        if ($cat -ne [System.Globalization.UnicodeCategory]::NonSpacingMark) { [void] $sb.Append($ch) }
    }
    # d-with-stroke has no decomposition, so FormD leaves it whole: "da duyet" would keep its first
    # letter as U+0111 and never match an ASCII pattern.
    return $sb.ToString().Replace([char]0x0111, 'd').Replace([char]0x0110, 'D').ToLowerInvariant()
}

# ---- the shape of a plan ----------------------------------------------------------------------------
#
# One row per line: the section it sits in (head | tasks | evidence | other), the "### n." group inside
# that section, whether the line is a heading, and whether it is inside a fenced code block (``` or ~~~).
# The gate and the tick reminder both read a plan through this, so "which line is a task" has one answer:
# a task line quoted as an example in Decisions, or fenced anywhere, is text. A heading inside a fence is
# text too - a "# comment" in a fenced script used to end the section it sat in.
#
# Plain text is worked out for heading lines only: the reminder runs in a Stop hook over every approved
# plan, and the other lines need it only for what the gate reads.
function Get-PaperPlanOutline([string[]] $Lines) {
    $rows = New-Object System.Collections.Generic.List[psobject]
    $section = 'head'
    # The level of the heading that opened the current section. A plan groups its tasks under
    # "### 1. <group>" ... "### Last. Close" inside "## Tasks", and a heading deeper than the one that
    # opened the section continues it. Ending the section there instead dropped every task of a grouped
    # plan and answered "the Tasks section has no task line".
    $sectionLevel = 0
    # The "### n." group a task sits in. Two lanes of one group run at the same time (F3); groups run one
    # after the other.
    $group = 0
    $fence = ''
    foreach ($line in @($Lines)) {
        $text = [string] $line
        if ($text -match '^\s{0,3}(`{3,}|~{3,})') {
            $marker = $Matches[1]
            if (-not $fence) { $fence = $marker }
            elseif ($marker[0] -eq $fence[0] -and $marker.Length -ge $fence.Length -and $text.Trim() -eq $marker) { $fence = ''; }
            $rows.Add([pscustomobject]@{ Line = $text; Section = $section; Group = $group; Heading = $false; Fenced = $true })
            continue
        }
        if ($fence) {
            $rows.Add([pscustomobject]@{ Line = $text; Section = $section; Group = $group; Heading = $false; Fenced = $true })
            continue
        }
        if ($text -match '^\s*#') {
            $plain = Get-PaperPlainText $text
            if ($plain -match '^(#+)\s') {
                $level = $Matches[1].Length
                if ($plain -match '^#+\s*\d*\.?\s*tasks?\b') { $section = 'tasks'; $sectionLevel = $level }
                elseif ($plain -match '^#+\s*\d*\.?\s*(bang chung|evidence)\b') { $section = 'evidence'; $sectionLevel = $level }
                elseif (($section -eq 'tasks' -or $section -eq 'evidence') -and $level -gt $sectionLevel) { $group++ }
                else {
                    # Any other heading ends the section it followed: "## API da tra" after a plan's Tasks, and
                    # in an older task file "Spec bo sung", which is a new section, not a continuation of the Tasks.
                    $section = 'other'
                    $sectionLevel = $level
                }
                $rows.Add([pscustomobject]@{ Line = $text; Section = $section; Group = $group; Heading = $true; Fenced = $false })
                continue
            }
        }
        $rows.Add([pscustomobject]@{ Line = $text; Section = $section; Group = $group; Heading = $false; Fenced = $false })
    }
    return $rows.ToArray()
}

# Every task of a plan, read the way the gate reads them: a task line under the Tasks heading and outside
# a code block, with the group it sits in.
function Get-PaperPlanTasks([string[]] $Lines, $LaneAliases) {
    $tasks = New-Object System.Collections.Generic.List[psobject]
    foreach ($row in (Get-PaperPlanOutline $Lines)) {
        if ($row.Section -ne 'tasks' -or $row.Heading -or $row.Fenced) { continue }
        $entry = Get-PaperPlanTaskEntry $row.Line $LaneAliases
        if ($null -eq $entry) { continue }
        $tasks.Add([pscustomobject]@{
                Id     = $entry.Id
                Ticked = $entry.Ticked
                Lane   = $entry.Lane
                Red    = $entry.Red
                Text   = $entry.Text
                Files  = $entry.Files
                Group  = $row.Group
            })
    }
    return $tasks.ToArray()
}

function Get-PaperSpecHash {
    <#
    .SYNOPSIS
    Hash of part 1 (the Spec) of a task file written before paper-kit 0.3.0: every line from the status
    line to the Tasks heading, with the status line itself and blank lines left out.
    .DESCRIPTION
    The status line carries this hash, so it cannot be part of what is hashed. Blank lines are dropped
    so reflowing the section does not read as a changed requirement - a word has to change.
    A plan carries no hash: what sits above its Tasks heading is Context and Decisions, which grow while
    the work runs, and the requirement a hash would have frozen now lives in SPEC.md.
    #>
    # AllowEmptyString, and no Mandatory on Lines: a Mandatory [string[]] parameter in PowerShell
    # 5.1 validates every ELEMENT as not-null-or-empty, so a task file - which is mostly blank
    # lines - is refused at the call with "Cannot bind argument to parameter 'Lines' because it is
    # an empty string".
    param([AllowEmptyString()][string[]] $Lines = @())

    $body = New-Object System.Collections.Generic.List[string]
    foreach ($line in $Lines) {
        $plain = Get-PaperPlainText $line
        if ($plain -match '^\*\*(trang thai|status)') { continue }
        if ($plain -match '^#+\s*\d*\.?\s*(tasks|task)\b') { break }
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $body.Add($line.Trim())
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes(($body -join "`n"))) }
    finally { $sha.Dispose() }
    return (-join ($digest | ForEach-Object { $_.ToString('x2') })).Substring(0, 12)
}

function Get-PaperTaskGateVerdict {
    <#
    .SYNOPSIS
    The verdict for one plan (or a task file written before paper-kit 0.3.0).
    .PARAMETER Lines
    The file, line by line.
    .PARAMETER Lanes
    The lanes this project runs, from paper.profile.json. A task in any other lane is malformed.
    .PARAMETER WorkTypes
    The profile's workTypes: code|model -> { lanes, rulePrefixes, ... }. Absent: any lane the project runs
    is allowed in any plan, and a model task cites C-/R- codes.
    .PARAMETER LaneAliases
    The profile's laneAliases: an old lane tag -> the lane it now counts as (a host-named tag -> live).
    #>
    param(
        [AllowEmptyString()][string[]] $Lines = @(),
        [AllowEmptyString()][string[]] $Lanes = @(),
        $WorkTypes,
        $LaneAliases
    )

    function New-Verdict([int] $code, [string] $reason, $open) {
        return [pscustomobject]@{ ExitCode = $code; Reason = $reason; Open = @($open) }
    }

    $lanes = @($Lanes | ForEach-Object { ([string] $_).ToLowerInvariant() })
    # A lane a work type declares is a lane the project runs, even if the profile's lanes list forgot it.
    foreach ($typeName in (Get-PaperMapKeys $WorkTypes)) {
        foreach ($l in @(Get-PaperMapValue (Get-PaperMapValue $WorkTypes $typeName) 'lanes')) {
            if ($l -and ($lanes -notcontains ([string] $l).ToLowerInvariant())) { $lanes += ([string] $l).ToLowerInvariant() }
        }
    }
    $workTypeLine = $null

    # ---- read the file once ------------------------------------------------------------------
    # Sections, groups and code blocks come from Get-PaperPlanOutline, and the tasks from Get-PaperPlanTasks:
    # the tick reminder reads a plan through the same two, so it cannot count a task the gate does not.
    $status = $null
    $specHash = $null
    $tasksHeadingSeen = $false
    $evidenceHeadingSeen = $false
    $cases = New-Object System.Collections.Generic.List[psobject]
    $tasks = @(Get-PaperPlanTasks $Lines $LaneAliases)
    $evidenceIds = New-Object System.Collections.Generic.List[string]

    foreach ($row in (Get-PaperPlanOutline $Lines)) {
        $line = $row.Line
        $section = $row.Section
        if ($row.Heading) {
            if ($section -eq 'tasks') { $tasksHeadingSeen = $true }
            if ($section -eq 'evidence') { $evidenceHeadingSeen = $true }
            continue
        }
        if ($row.Fenced) { continue }
        $plain = Get-PaperPlainText $line

        # The colon sits INSIDE the bold in the template - "**Trang thai:**" - so it is matched before
        # the closing asterisks, not after them.
        if ($null -eq $status -and $plain -match '^\*\*(trang thai|status):?\*\*:?\s*(.*)$') {
            $status = $Matches[2].Trim()
            if ($line -match 'spec-hash:\s*([0-9a-f]{6,})') { $specHash = $Matches[1] }
            continue
        }

        # The work-type line. "**Loai:**" alone is an ordinary Vietnamese word ("kind"), so it names a work
        # type only in the older spelling "**Loai:** dung hinh"; any other "**Loai:**" line is text.
        if ($null -eq $workTypeLine -and $section -ne 'tasks' -and $section -ne 'evidence' -and
            $plain -match '^\*\*(loai viec|work type|loai):?\*\*:?\s*(.*)$') {
            $label = $Matches[1]
            $value = $Matches[2].Trim()
            if ($label -ne 'loai') { $workTypeLine = $value; continue }
            if ($value -match '^dung hinh') { $workTypeLine = 'model'; continue }
        }

        # An acceptance row: | A1 | given | then | lane |
        if ($line -match '^\s*\|\s*(A\d+)\s*\|(.*)$') {
            $id = $Matches[1]
            $cells = @($Matches[2] -split '\|' | ForEach-Object { $_.Trim() })
            $laneCell = ''
            foreach ($cell in $cells) {
                $c = (Get-PaperPlainText $cell)
                foreach ($known in @('unit', 'ui', 'e2e', 'live')) {
                    if ($c -match "(^|\W)$known(\W|$)") { $laneCell = $known }
                }
            }
            $cases.Add([pscustomobject]@{
                    Id            = $id
                    Lane          = $laneCell
                    NotApplicable = ($plain -match 'khong ap dung|not applicable|n/a')
                })
            continue
        }

        # Same suffix as the task line: an evidence row for T8a belongs to T8a, not to T8.
        if ($section -eq 'evidence' -and $line -match '^\s*\|\s*(T\d+[a-z]?)\s*\|') {
            $evidenceIds.Add($Matches[1])
        }
    }

    # ---- shape (rule 10) ---------------------------------------------------------------------
    if ($null -eq $status) {
        # The brief and SPEC.md carry no status line on purpose, so this is also what running the gate on
        # the wrong one of a task's three documents looks like - say so.
        return (New-Verdict 2 'no status line: expected "**Trang thai:** cho duyet | da duyet <date> | xong <date>" - the gate reads the plan (YYYY-MM-DD-<task>-plan.md), not the brief or SPEC.md' @())
    }
    if (-not $tasksHeadingSeen) { return (New-Verdict 2 'no "Tasks" heading in the file' @()) }
    if ($tasks.Count -eq 0) { return (New-Verdict 2 'the Tasks section has no task line ("- [ ] T1 [lane] ...")' @()) }

    $seen = @{}
    foreach ($t in $tasks) {
        if ($seen.ContainsKey($t.Id)) { return (New-Verdict 2 "duplicate task id $($t.Id)" @($t.Id)) }
        $seen[$t.Id] = $true
    }
    # A task with no lane tag belongs to no lane, and that is legal: the find-bug and docs steps that end
    # every real task file are not in a lane. Demanding a tag there rejected all three task files in
    # the first real project the first time this gate ran against them, which is a migration nobody asked for. Such a
    # task is still ticked and still needs evidence; it is only left out of the lane checks below.
    foreach ($t in $tasks) {
        if (-not $t.Lane) { continue }
        if ($lanes.Count -gt 0 -and $lanes -notcontains $t.Lane) {
            return (New-Verdict 2 "task $($t.Id) is in lane '$($t.Lane)', which this project does not run (lanes: $($lanes -join ', '))" @($t.Id))
        }
    }

    # ---- work type (SPEC "Quy trinh theo loai viec") -----------------------------------------
    $declaredTypes = @(Get-PaperMapKeys $WorkTypes | ForEach-Object { $_.ToLowerInvariant() })
    $workType = 'code'
    if ($null -ne $workTypeLine) {
        $plainType = Get-PaperPlainText $workTypeLine
        if ($plainType -match '^\W*([a-z0-9-]+)') { $workType = $Matches[1] } else { $workType = '' }
        $known = $declaredTypes
        if ($known.Count -eq 0) { $known = @('code', 'model') }
        if ($known -notcontains $workType) {
            return (New-Verdict 2 "work type '$workTypeLine' is not one this project declares (workTypes: $($known -join ', '))" @())
        }
    }
    $typeLanes = @(@(Get-PaperMapValue (Get-PaperMapValue $WorkTypes $workType) 'lanes') | Where-Object { $_ } | ForEach-Object { ([string] $_).ToLowerInvariant() })
    if ($typeLanes.Count -gt 0) {
        foreach ($t in $tasks) {
            if ($t.Lane -and ($typeLanes -notcontains $t.Lane)) {
                return (New-Verdict 2 "task $($t.Id) is in lane '$($t.Lane)', which a $workType plan does not use (lanes: $($typeLanes -join ', ')) - split the work into a plan of its own type" @($t.Id))
            }
        }
    }

    # ---- F12: a model task names the rules it follows ----------------------------------------
    # A code counts only as a whole token: the prefix, then digits, with no letter or digit before it
    # (ABC-16 does not cite C-16, and the template's "C-xx" cites nothing).
    $prefixes = @(@(Get-PaperMapValue (Get-PaperMapValue $WorkTypes 'model') 'rulePrefixes') | Where-Object { $_ } | ForEach-Object { [string] $_ })
    if ($prefixes.Count -eq 0) { $prefixes = $script:PaperDefaultRulePrefixes }
    $ruleCode = '(?<![A-Za-z0-9])(?:' + (($prefixes | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')\d+'
    foreach ($t in $tasks) {
        if ($t.Lane -ne 'model') { continue }
        if (-not [regex]::IsMatch($t.Text, $ruleCode)) {
            return (New-Verdict 2 "F12: model task $($t.Id) cites no rule code ($(($prefixes | ForEach-Object { "$_<n>" }) -join ', ')): name the rule it builds to, or write the rule first" @($t.Id))
        }
    }

    # ---- F3: lanes of one group run in parallel, so they may not share files -----------------
    for ($i = 0; $i -lt $tasks.Count; $i++) {
        $a = $tasks[$i]
        if (-not $a.Lane -or $a.Files.Count -eq 0) { continue }
        for ($j = $i + 1; $j -lt $tasks.Count; $j++) {
            $b = $tasks[$j]
            if ($b.Group -ne $a.Group -or -not $b.Lane -or $b.Lane -eq $a.Lane -or $b.Files.Count -eq 0) { continue }
            # "(sau T1)" / "(after T1)" - also "(sau T1, T3)" - on the later task puts the two in order.
            if ((Get-PaperPlainText $b.Text) -match "\((sau|after)\b[^)]*\b$($a.Id.ToLowerInvariant())\b") { continue }
            foreach ($ga in $a.Files) {
                foreach ($gb in $b.Files) {
                    if (Test-PaperGlobOverlap $ga $gb) {
                        return (New-Verdict 2 "F3: tasks $($a.Id) [$($a.Lane)] and $($b.Id) [$($b.Lane)] run in parallel in one group and both write '$ga' / '$gb': mark $($b.Id) '(sau $($a.Id))' or split the files" @($a.Id, $b.Id))
                    }
                }
            }
        }
    }

    # ---- task split (rule 11) ----------------------------------------------------------------
    #
    # The two content checks below - every case proved by a task, and the red test first - rest on two
    # markers this template introduced: `[red]` and `spec-hash`. Run against the 19 task files that
    # already existed in the first real project, they rejected 17 (11 for red-first, 5 for an unproved case), because
    # those files predate both markers. Enforcing them everywhere would be a migration nobody asked for,
    # and the same reasoning already governs the frozen Spec: an absent marker means an older file, not a
    # violation. So a file is measured against them only once it carries one - and then fully, which
    # makes this a ratchet instead of an exemption.
    $usesNewConvention = ($null -ne $specHash) -or @($tasks | Where-Object { $_.Red })
    if ($usesNewConvention) {
        $taskBlob = ($tasks | ForEach-Object { $_.Text }) -join ' '
        foreach ($case in $cases) {
            if ($case.NotApplicable) { continue }
            if ($lanes.Count -gt 0 -and $case.Lane -and ($lanes -notcontains $case.Lane)) {
                return (New-Verdict 2 "case $($case.Id) is in lane '$($case.Lane)', which this project does not run: mark it 'khong ap dung' or change the lane" @($case.Id))
            }
            if ($taskBlob -notmatch "(^|\W)$($case.Id)(\W|$)") {
                return (New-Verdict 2 "no task proves case $($case.Id)" @($case.Id))
            }
        }
        foreach ($lane in @($cases | Where-Object { -not $_.NotApplicable -and $_.Lane } | ForEach-Object { $_.Lane } | Select-Object -Unique)) {
            if ($lanes.Count -gt 0 -and $lanes -notcontains $lane) { continue }
            if (-not @($tasks | Where-Object { $_.Lane -eq $lane })) {
                return (New-Verdict 2 "lane '$lane' has an acceptance case but no task" @())
            }
        }
        # Per plan, not per group, and that is a known limit rather than an oversight: a group that codes
        # until an EARLIER group's red test goes green has no red step of its own and should not need one.
        # The gate cannot tell that task from one introducing new behaviour with no test at all, which is
        # what group 4b of the export plan turned out to be (measured 2026-09-21). Telling them apart needs
        # the acceptance case each task proves, and the plans in this project keep their cases in SPEC.md,
        # where this file - markdown, no project, no host - cannot follow. Reviewed and left as is.
        foreach ($lane in @($tasks | ForEach-Object { $_.Lane } | Select-Object -Unique)) {
            if ($script:PaperRedFirstLanes -notcontains $lane) { continue }
            $first = @($tasks | Where-Object { $_.Lane -eq $lane })[0]
            if (-not $first.Red) {
                return (New-Verdict 2 "the first task of lane '$lane' is $($first.Id), which is not the red test: mark the red step '[red]' and put it first" @($first.Id))
            }
        }
    }

    # ---- approval (rule 10) and the freeze (rule 13) -----------------------------------------
    $plainStatus = Get-PaperPlainText $status
    # The word opens the status, as plan-nag and check-spec read it: matched anywhere, "not approved yet" was
    # approved here and /task-do could start on a plan nobody approved.
    $isApproved = $plainStatus -match '^\s*(da duyet|approved|xong|done)\b'
    if (-not $isApproved) {
        return (New-Verdict 4 "not approved yet (status: $status) - nothing may be read or changed until the user approves" @())
    }

    # A file approved before the freeze existed carries no hash. Refusing it would be a migration
    # nobody asked for, so an absent hash is not a violation - only a hash that no longer matches.
    if ($specHash) {
        $now = Get-PaperSpecHash -Lines $Lines
        if ($now -ne $specHash) {
            return (New-Verdict 2 "the approved Spec was edited (spec-hash $specHash, now $now): add a dated 'Spec bo sung' section and get it approved instead" @())
        }
    }

    # ---- work left (rule 10) -----------------------------------------------------------------
    $open = @($tasks | Where-Object { -not $_.Ticked } | ForEach-Object { $_.Id })
    if ($open.Count -gt 0) {
        return (New-Verdict 1 "$($open.Count) task(s) still open: $($open -join ', ')" $open)
    }

    if (-not $evidenceHeadingSeen) {
        return (New-Verdict 2 'no "Bang chung"/"Evidence" heading: every tick needs a row there' @())
    }
    $noEvidence = @($tasks | Where-Object { $evidenceIds -notcontains $_.Id } | ForEach-Object { $_.Id })
    if ($noEvidence.Count -gt 0) {
        return (New-Verdict 1 "ticked with no evidence row: $($noEvidence -join ', ')" $noEvidence)
    }

    return (New-Verdict 0 "$($tasks.Count) task(s) done, each with evidence" @())
}
