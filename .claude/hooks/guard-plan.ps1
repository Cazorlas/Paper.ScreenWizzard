# The decision half of the three command guards (test-guard, build-guard, destructive-guard). Pure:
# a command line and the profile's rules in, a hit or $null out. Dot-sourced; tests/hooks.tests.ps1 drives
# it through the hooks.
#
# Rules are judged per SEGMENT, never over the whole line. Matching the whole line blocked a commit message
# or a search that merely spelled the words, and still let `cd x && dotnet test` through when the pattern
# was anchored. A separator inside quotes does not split: a JSON payload piped to a hook in its own test
# contained "cd x && dotnet test", and the first splitter that ignored quotes blocked that test run.
#
#   "guards": {
#     "test":        [ { "pattern": "<regex over one segment>", "use": "<the command to run instead>", "escape": "<token>" } ],
#     "build":       { "pattern": "<regex naming a build>", "requireConfiguration": "<regex the build segment must match>", "message": "..." },
#     "destructive": [ { "pattern": "<regex over one segment, and over one pipeline>", "reason": "..." } ]
#   }
#
# destructive reads a command in two views, because "find it | end it" is the same act as "end it":
# every segment alone, and every pipeline - the segments between ; && || joined back with " | ". So a rule
# can say "a stage names hostapp, and a later stage of the same pipeline starts with a kill verb" as
#   \bhostapp\b[^|]*\|(?:[^|]*\|)*\s*(?:Stop-Process|kill)\b
# The convention for a pack pattern: match a stage with [^|]* where .* would run on into the next stage, and
# cross a pipe only with an explicit \|. A quoted string reaches a destructive rule only when it reads as an
# argument - one word or a path ("hostapp.exe", "$env:APPDATA\...\X.bundle"); any other quoted text (a commit
# message, an echo) is emptied first. A quoted string after a shell's -c / -Command / /c is checked again as
# a command line of its own.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function Split-PaperCommandSegments([string] $Command) {
    $segments = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    $quote = [char]0
    for ($i = 0; $i -lt $Command.Length; $i++) {
        $ch = $Command[$i]
        if ($quote -ne [char]0) {
            if ($ch -eq $quote) { $quote = [char]0 }
            [void] $current.Append($ch)
            continue
        }
        if ($ch -eq '"' -or $ch -eq "'") { $quote = $ch; [void] $current.Append($ch); continue }
        $pair = if ($i + 1 -lt $Command.Length) { $Command.Substring($i, 2) } else { '' }
        if ($pair -eq '&&' -or $pair -eq '||') { $segments.Add($current.ToString()); [void] $current.Clear(); $i++; continue }
        if ($ch -eq ';' -or $ch -eq '|' -or $ch -eq "`r" -or $ch -eq "`n") { $segments.Add($current.ToString()); [void] $current.Clear(); continue }
        [void] $current.Append($ch)
    }
    $segments.Add($current.ToString())
    return , @($segments | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
}

# The same scan as Split-PaperCommandSegments, kept apart so test-guard and build-guard read exactly what
# they always read: one string[] per pipeline, its stages in order. ; && || and a line break end a pipeline,
# | ends a stage.
function Split-PaperCommandPipelines([string] $Command) {
    $pipelines = New-Object System.Collections.Generic.List[object]
    $stages = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    $quote = [char]0
    for ($i = 0; $i -le $Command.Length; $i++) {
        $end = $i -eq $Command.Length
        $closesPipeline = $false
        if (-not $end) {
            $ch = $Command[$i]
            if ($quote -ne [char]0) {
                if ($ch -eq $quote) { $quote = [char]0 }
                [void] $current.Append($ch)
                continue
            }
            if ($ch -eq '"' -or $ch -eq "'") { $quote = $ch; [void] $current.Append($ch); continue }
            $pair = if ($i + 1 -lt $Command.Length) { $Command.Substring($i, 2) } else { '' }
            $closesPipeline = $pair -eq '&&' -or $pair -eq '||' -or $ch -eq ';' -or $ch -eq "`r" -or $ch -eq "`n"
            if (-not $closesPipeline -and $ch -ne '|') { [void] $current.Append($ch); continue }
            if ($pair -eq '&&' -or $pair -eq '||') { $i++ }
        }
        $stage = $current.ToString().Trim()
        [void] $current.Clear()
        if ($stage) { $stages.Add($stage) }
        if ($end -or $closesPipeline) {
            if ($stages.Count -gt 0) { $pipelines.Add([string[]] $stages.ToArray()) }
            $stages.Clear()
        }
    }
    return , $pipelines.ToArray()
}

# Quoted text the way a destructive rule reads it. A quoted string that is an argument stays as written: one
# word ("hostapp.exe", 'hostapp') or a path ("$env:APPDATA\Vendor\Plugins\X.bundle"). Any other -
# a commit message, an echo, an awk program - has its contents emptied, so a word in it never triggers a rule.
# Emptying every quoted string would let `Remove-Item -Recurse "<bundle path>"` through.
function Hide-PaperQuotedMessages([string] $Text) {
    return [regex]::Replace($Text, '"[^"]*"|''[^'']*''', {
            param($m)
            $inner = $m.Value.Substring(1, $m.Value.Length - 2)
            $isPath = $inner -match '^(?:[A-Za-z]:[\\/]|\\\\|\$\{?env:|\$HOME\b|%\w+%|~[\\/]|\.{1,2}[\\/])'
            if ($inner -notmatch '[|;&]' -and ($inner -notmatch '\s' -or $isPath)) { return $m.Value }
            return [string] $m.Value[0] + $m.Value[0]
        })
}

# The segment with every quoted string emptied: what a built-in rule reads, so `git commit -m "never git
# reset --hard"` is a commit and not a reset.
function Remove-PaperQuoted([string] $Segment) {
    return ($Segment -replace '"[^"]*"', '""' -replace "'[^']*'", "''")
}

# Words of a segment with their quotes removed; a quoted path with spaces stays one word.
function Split-PaperWords([string] $Segment) {
    $words = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($Segment, '"([^"]*)"|''([^'']*)''|(\S+)')) {
        if ($m.Groups[1].Success) { $words.Add($m.Groups[1].Value) }
        elseif ($m.Groups[2].Success) { $words.Add($m.Groups[2].Value) }
        else { $words.Add($m.Groups[3].Value) }
    }
    return , $words.ToArray()
}

function Get-PaperGuardField($Rule, [string] $Name) {
    if ($null -eq $Rule) { return $null }
    if ($Rule -is [System.Collections.IDictionary]) { return $Rule[$Name] }
    return $Rule.$Name
}

function Test-PaperRegex([string] $Text, [string] $Pattern) {
    if ([string]::IsNullOrWhiteSpace($Pattern)) { return $false }
    try { return [regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase) }
    catch { return $false }   # a broken pattern in a profile is the profile's bug, never a blocked turn
}

# guards.test: the first segment matching a rule whose escape token is not in the command.
function Get-PaperTestGuardHit([string] $Command, $Rules) {
    foreach ($rule in @($Rules)) {
        if ($null -eq $rule) { continue }
        $escape = [string] (Get-PaperGuardField $rule 'escape')
        if ($escape -and $Command.IndexOf($escape, [StringComparison]::Ordinal) -ge 0) { continue }
        foreach ($segment in (Split-PaperCommandSegments $Command)) {
            if (Test-PaperRegex $segment ([string] (Get-PaperGuardField $rule 'pattern'))) {
                return [pscustomobject]@{ Segment = $segment; Use = [string] (Get-PaperGuardField $rule 'use'); Escape = $escape }
            }
        }
    }
    return $null
}

# guards.build: one object or a list of them. A build segment that does not match requireConfiguration.
function Get-PaperBuildGuardHit([string] $Command, $Rules) {
    foreach ($rule in @($Rules)) {
        if ($null -eq $rule) { continue }
        $pattern = [string] (Get-PaperGuardField $rule 'pattern')
        $require = [string] (Get-PaperGuardField $rule 'requireConfiguration')
        if (-not $pattern -or -not $require) { continue }
        foreach ($segment in (Split-PaperCommandSegments $Command)) {
            if (-not (Test-PaperRegex $segment $pattern)) { continue }
            if (Test-PaperRegex $segment $require) { continue }
            return [pscustomobject]@{ Segment = $segment; Require = $require; Message = [string] (Get-PaperGuardField $rule 'message') }
        }
    }
    return $null
}

# True when Target names the project root, a folder above it, or anything inside a .git folder.
function Test-PaperProtectedTarget([string] $Target, [string] $ProjectDir, [string] $Cwd) {
    $t = $Target.Trim()
    if (-not $t) { return $false }
    if ($t -match '^\$(\{)?(env:)?CLAUDE_PROJECT_DIR(\})?([\\/]\*?)?$') { return $true }
    if (@($t -split '[\\/]') -contains '.git') { return $true }
    if ($t -match '^\$' -or $t -match '^~') { return $false }
    if (-not $ProjectDir) { return $false }
    $t = $t -replace '[\\/]\*$', ''
    if ($t -eq '*') { $t = '.' }
    try {
        $base = if ($Cwd) { $Cwd } else { $ProjectDir }
        $full = if ([System.IO.Path]::IsPathRooted($t)) { [System.IO.Path]::GetFullPath($t) } else { [System.IO.Path]::GetFullPath((Join-Path $base $t)) }
    }
    catch { return $false }
    $full = $full.TrimEnd('\', '/')
    $root = [System.IO.Path]::GetFullPath($ProjectDir).TrimEnd('\', '/')
    if ($full.Equals($root, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    # A folder that contains the project deletes it too.
    return $root.StartsWith($full + '\', [StringComparison]::OrdinalIgnoreCase)
}

# guards.destructive plus the rules every project gets: forced git clean, git reset --hard, and a recursive
# delete of the project root or of .git. Profile rules read every segment and every pipeline of more than one
# stage, quoted messages emptied (see the top of this file); the built-in rules read segments.
function Get-PaperDestructiveHit([string] $Command, $Rules, [string] $ProjectDir, [string] $Cwd, [int] $Depth = 0) {
    $segments = New-Object System.Collections.Generic.List[string]
    foreach ($pipeline in (Split-PaperCommandPipelines $Command)) {
        $views = @($pipeline)
        if ($pipeline.Count -gt 1) { $views += ($pipeline -join ' | ') }
        foreach ($view in $views) {
            $read = Hide-PaperQuotedMessages $view
            foreach ($rule in @($Rules)) {
                if ($null -eq $rule) { continue }
                if (Test-PaperRegex $read ([string] (Get-PaperGuardField $rule 'pattern'))) {
                    return [pscustomobject]@{ Segment = $view; Reason = [string] (Get-PaperGuardField $rule 'reason') }
                }
            }
        }
        $segments.AddRange([string[]] $pipeline)
    }

    foreach ($segment in $segments) {
        # `powershell -Command "Get-Process hostapp | Stop-Process"`: that quoted string is a command line, and
        # the masking above emptied it. Checked again on its own, two levels deep at most.
        if ($Depth -lt 2) {
            foreach ($m in [regex]::Matches($segment, '"[^"]*"|''[^'']*''')) {
                $before = $segment.Substring(0, $m.Index)
                if ($before -notmatch '(?i)(?:^|[\s&\\/])(?:powershell|pwsh|bash|sh|zsh|cmd)(?:\.exe)?["'']?\s(?:.*\s)?(?:-\w*c|-com\w*|/c|/k)\s+$') { continue }
                $nested = Get-PaperDestructiveHit $m.Value.Substring(1, $m.Value.Length - 2) $Rules $ProjectDir $Cwd ($Depth + 1)
                if ($null -ne $nested) { return $nested }
            }
        }

        $bare = Remove-PaperQuoted $segment
        if ($bare -match '(?i)\bgit\b.*\sclean\b' -and $bare -match '(?i)\s(-[a-z]*f[a-z]*|--force)\b' -and $bare -notmatch '(?i)\s(-[a-z]*n[a-z]*|--dry-run)\b') {
            return [pscustomobject]@{ Segment = $segment; Reason = 'git clean -f deletes untracked files for good - work this session may not have created, and git cannot bring it back. Run git clean -n to see the list, then ask the user.' }
        }
        if ($bare -match '(?i)\bgit\b.*\sreset\b.*\s--hard\b') {
            return [pscustomobject]@{ Segment = $segment; Reason = 'git reset --hard discards every uncommitted change in the working tree, not just this session''s. Use git stash, or git restore on the files you mean, or ask the user.' }
        }
        # git checkout / git restore of the whole tree ('.', ':/', '*') throws away every uncommitted change
        # just as reset --hard does. A named file, a branch, or restore --staged alone (which only unstages)
        # is ordinary work.
        if ($bare -match '(?i)\bgit\b.*\s(checkout|restore)\b(.*)$') {
            $gitVerb = $Matches[1]; $tail = ' ' + $Matches[2] + ' '
            $whole = $tail -match '\s(?:\.|\./|:/|\*)\s'
            $stagedOnly = ($gitVerb -ieq 'restore') -and ($tail -match '(?i)\s(?:--staged|-S)\s') -and ($tail -notmatch '(?i)\s(?:--worktree|-W)\s')
            if ($whole -and -not $stagedOnly) {
                return [pscustomobject]@{ Segment = $segment; Reason = "git $gitVerb of the whole tree discards every uncommitted change in it, not just this session's. Name the files you mean, use git stash, or ask the user." }
            }
        }

        $words = Split-PaperWords $segment   # the function returns , array: @() here would wrap it again
        $i = 0
        if ($words.Count -gt 0 -and $words[0] -eq '&') { $i = 1 }
        if ($words.Count -le $i) { continue }
        $verb = [string] $words[$i]
        if ($verb -notmatch '(?i)^(Remove-Item|ri|rm|rmdir|rd|del|erase)$') { continue }
        $rest = @($words | Select-Object -Skip ($i + 1))
        $recursive = @($rest | Where-Object { $_ -cmatch '^-[a-zA-Z]{0,3}[rR][a-zA-Z]{0,3}$' -and $_ -notmatch '(?i)^-(Force|Filter)' -or $_ -match '(?i)^-rec(u(r(se?)?)?)?$' -or $_ -match '(?i)^/s$' }).Count -gt 0
        if (-not $recursive) { continue }
        foreach ($target in $rest) {
            if ($target -match '^[-/]') { continue }
            if (Test-PaperProtectedTarget $target $ProjectDir $Cwd) {
                return [pscustomobject]@{ Segment = $segment; Reason = "A recursive delete of '$target' removes the project itself or its git history. Delete the specific folder you mean, or ask the user." }
            }
        }
    }
    return $null
}
