# The API lookup check, as pure functions: the plan's API table, which changed files use a host namespace,
# and the verdict (SPEC "Tra API truoc khi goi", F17). api-check.ps1 reads the plan and git and calls these;
# tests/api-check.tests.ps1 covers every branch.
#
# Exit codes it plans:
#   0  new code uses a host namespace and the plan's API table has at least one row
#   1  new code uses a host namespace and the table is empty (F17) - every such file is named
#   5  NOT APPLICABLE - no changed code file uses a host namespace, or the profile declares none
#
# Only one rule is red. The Type.Member tokens no table row mentions are a LIST for the reviewer: telling
# an API member from a variable by the text of a line is a guess (doc.Delete looks exactly like one), and
# a gate that goes red on a guess gets switched off within the week.
#
# Which namespaces are the host's is data: the profile's api.namespaces and nothing else (setup copies it
# from the host templates into a new profile). This file names no host. Dot-source it; it declares no
# param() block.
#
# Host use is judged per FILE: the added lines decide which files are this task's, and a file uses the host
# when the whole file (comments and strings out) or its project (a global using, a <Using Include>) names a
# host namespace.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# Source files a host namespace can be used from, when the profile's codeMap.extensions names none.
$script:PaperApiCodeExtensions = @('.cs', '.vb', '.fs', '.xaml', '.cpp', '.cxx', '.cc', '.h', '.hpp', '.py', '.ts', '.tsx', '.js', '.mjs', '.java', '.kt')
# The languages a project-wide import (global using, <Using Include>, <Import Include>) applies to.
$script:PaperApiImportExtensions = @('.cs', '.vb')

function Get-PaperApiValue($Map, [string] $Key) {
    <# The value under Key of a hashtable or a PSCustomObject, and whether the key is there at all. #>
    if ($null -eq $Map) { return [pscustomobject]@{ Found = $false; Value = $null } }
    if ($Map -is [System.Collections.IDictionary]) {
        foreach ($k in $Map.Keys) {
            if ([string]::Equals([string] $k, $Key, [System.StringComparison]::OrdinalIgnoreCase)) { return [pscustomobject]@{ Found = $true; Value = $Map[$k] } }
        }
        return [pscustomobject]@{ Found = $false; Value = $null }
    }
    $prop = $Map.PSObject.Properties | Where-Object { [string]::Equals($_.Name, $Key, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($prop) { return [pscustomobject]@{ Found = $true; Value = $prop.Value } }
    return [pscustomobject]@{ Found = $false; Value = $null }
}

function Get-PaperApiStrings($Value) {
    return @(@($Value) | Where-Object { $null -ne $_ -and -not [string]::IsNullOrWhiteSpace([string] $_) } | ForEach-Object { ([string] $_).Trim() })
}

function Get-PaperApiNamespaces {
    <#
    .SYNOPSIS
    The host namespace prefixes this project's code is checked against.
    .DESCRIPTION
    The profile's api.namespaces, and nothing else: the profile is the one place a project decides. An
    explicit empty list means none (the check is off); a missing key means none too, and
    Test-PaperApiNamespacesDeclared tells the two apart so the message can say to declare it. Setup writes
    the key from the host templates into a new profile; a project set up before it existed adds it by hand.
    #>
    param($ProjectProfile)

    $api = Get-PaperApiValue $ProjectProfile 'api'
    if (-not $api.Found) { return @() }
    $own = Get-PaperApiValue $api.Value 'namespaces'
    if (-not $own.Found) { return @() }
    return @(Get-PaperApiStrings $own.Value)
}

function Test-PaperApiNamespacesDeclared($ProjectProfile) {
    <# Whether the profile has the api.namespaces key at all - an empty list is declared. #>
    $api = Get-PaperApiValue $ProjectProfile 'api'
    if (-not $api.Found) { return $false }
    return (Get-PaperApiValue $api.Value 'namespaces').Found
}

function Get-PaperApiTableCells([string] $Row) {
    $inner = ([string] $Row).Trim()
    if ($inner.StartsWith('|')) { $inner = $inner.Substring(1) }
    if ($inner.EndsWith('|')) { $inner = $inner.Substring(0, $inner.Length - 1) }
    return @($inner.Split('|') | ForEach-Object { $_.Trim() })
}

function Test-PaperApiTableSeparator($Cells) {
    $cells = @($Cells)
    if ($cells.Count -eq 0) { return $false }
    foreach ($c in $cells) { if ([string] $c -notmatch '^:?-+:?$') { return $false } }
    return $true
}

function Test-PaperApiPlaceholderCell([string] $Cell) {
    # Blank, dashes, an em or en dash, an ellipsis (one character or three dots), or <anything>.
    $c = ([string] $Cell).Trim()
    if (-not $c) { return $true }
    if ($c -match '^<[^>]*>$') { return $true }
    $dashes = [string][char]0x2014 + [char]0x2013 + [char]0x2026
    return ($c -match ('^(?:-+|[' + $dashes + ']+|\.{3,})$'))
}

function Get-PaperApiTableRows {
    <#
    .SYNOPSIS
    The rows of the plan's API table: every table row under a ## or ### heading that contains "API" (the
    template's "## API da tra"), other than a header and a separator, up to the next heading of the same or
    a higher level. A deeper subheading inside the section does not end it.
    .DESCRIPTION
    A separator is any row of dashes with optional colons (|---|, |-|, |:-:|). A header is the row right
    above a separator; a table written with neither still has rows. A row whose every cell is blank, a dash,
    an em dash, an ellipsis or a <placeholder> is not a row: a copied template is still empty. Anything
    inside a ``` or ~~~ fence is an example and is skipped, headings included.
    #>
    param([AllowEmptyString()][string[]] $Lines = @())

    $lines = @(@($Lines) | ForEach-Object { ([string] $_).TrimEnd("`r") })
    $rows = New-Object System.Collections.ArrayList
    $sectionLevel = 0
    $fence = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $f = [regex]::Match($line, '^\s{0,3}(`{3,}|~{3,})')
        if ($fence) {
            if ($f.Success -and $f.Groups[1].Value[0] -eq $fence[0] -and $f.Groups[1].Value.Length -ge $fence.Length) { $fence = $null }
            continue
        }
        if ($f.Success) { $fence = $f.Groups[1].Value; continue }

        $h = [regex]::Match($line, '^\s{0,3}(#{1,6})\s+(.*)$')
        if ($h.Success) {
            $level = $h.Groups[1].Value.Length
            if ($sectionLevel -gt 0 -and $level -gt $sectionLevel) { continue }
            $sectionLevel = 0
            if ($level -le 3 -and $level -ge 2 -and $h.Groups[2].Value -cmatch '(?<![A-Za-z])API(?![A-Za-z])') { $sectionLevel = $level }
            continue
        }
        if ($sectionLevel -eq 0) { continue }
        $t = $line.Trim()
        if (-not $t.StartsWith('|')) { continue }

        $cells = @(Get-PaperApiTableCells $t)
        if (Test-PaperApiTableSeparator $cells) { continue }
        if ($i + 1 -lt $lines.Count) {
            $next = $lines[$i + 1].Trim()
            if ($next.StartsWith('|') -and (Test-PaperApiTableSeparator @(Get-PaperApiTableCells $next))) { continue }   # the header
        }
        if (@($cells | Where-Object { -not (Test-PaperApiPlaceholderCell $_) }).Count -eq 0) { continue }

        $cell = { param($n) if ($n -lt $cells.Count) { $cells[$n] } else { '' } }
        [void] $rows.Add([pscustomobject]@{
                Member  = & $cell 0
                Version = & $cell 1
                Page    = & $cell 2
                Note    = & $cell 3
                Text    = $t
            })
    }
    return @($rows)
}

function ConvertFrom-PaperUnifiedDiff {
    <#
    .SYNOPSIS
    The added lines of a unified diff (git diff -U0 or any -U), per file, in the order the files appear.
    .DESCRIPTION
    Hunk headers are counted, so an added line that itself starts with "++" is still a line and not a
    file header. A deleted file (+++ /dev/null) adds nothing and is left out.
    #>
    param([AllowEmptyString()][string[]] $Lines = @())

    $files = [ordered]@{}
    $path = $null
    $oldLeft = 0
    $newLeft = 0
    foreach ($raw in @($Lines)) {
        $line = ([string] $raw).TrimEnd("`r")
        if ($oldLeft -gt 0 -or $newLeft -gt 0) {
            if ($line.StartsWith('+')) {
                $newLeft--
                if ($path) {
                    if (-not $files.Contains($path)) { $files[$path] = New-Object System.Collections.Generic.List[string] }
                    $files[$path].Add($line.Substring(1))
                }
                continue
            }
            if ($line.StartsWith('-')) { $oldLeft--; continue }
            if ($line.StartsWith(' ')) { $oldLeft--; $newLeft--; continue }
            if ($line.StartsWith('\')) { continue }   # "\ No newline at end of file"
            $oldLeft = 0
            $newLeft = 0
        }
        if ($line.StartsWith('diff --git ')) { $path = $null; continue }
        if ($line.StartsWith('+++ ')) {
            $p = $line.Substring(4).Trim()
            if ($p.StartsWith('"') -and $p.EndsWith('"') -and $p.Length -ge 2) { $p = $p.Substring(1, $p.Length - 2) }
            if ($p -eq '/dev/null') { $path = $null }
            elseif ($p.StartsWith('b/')) { $path = $p.Substring(2) }
            else { $path = $p }
            continue
        }
        $m = [regex]::Match($line, '^@@ -\d+(?:,(\d+))? \+\d+(?:,(\d+))? @@')
        if ($m.Success) {
            $oldLeft = if ($m.Groups[1].Success) { [int] $m.Groups[1].Value } else { 1 }
            $newLeft = if ($m.Groups[2].Success) { [int] $m.Groups[2].Value } else { 1 }
        }
    }
    $result = @()
    foreach ($k in $files.Keys) { $result += [pscustomobject]@{ Path = [string] $k; Added = @($files[$k].ToArray()) } }
    return $result
}

function Test-PaperApiCodePath {
    <# Whether a path is a source file by its extension. #>
    param([string] $Path, [string[]] $Extensions = $script:PaperApiCodeExtensions)
    $ext = [System.IO.Path]::GetExtension([string] $Path)
    if (-not $ext) { return $false }
    foreach ($e in @($Extensions)) { if ([string]::Equals($ext, [string] $e, [System.StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

function Get-PaperApiCommentStyle([string] $Path) {
    # Which comment and string syntax a file is read with, by its extension. Unknown code is C-like.
    $ext = ([System.IO.Path]::GetExtension([string] $Path)).ToLowerInvariant()
    switch ($ext) {
        '.vb' { return 'vb' }
        '.py' { return 'hash' }
        '.ps1' { return 'ps' }
        '.psm1' { return 'ps' }
        '.xaml' { return 'xml' }
        '.axaml' { return 'xml' }
        '.xml' { return 'xml' }
        '.props' { return 'xml' }
        '.targets' { return 'xml' }
        default { if ($ext -match '^\.\w+proj$') { return 'xml' } return 'c' }
    }
}

# One scan per language: whichever of a comment or a string STARTS first wins, so a string holding // or /*
# stays a string and a quote in a char literal opens nothing. Group s is a string, anything else a comment.
$script:PaperApiStripPatterns = @{
    c    = '(?<s>@"(?:[^"]|"")*"|"(?:[^"\\\n]|\\.)*"|''(?:[^''\\\n]|\\.)*'')|//[^\n]*|/\*[\s\S]*?(?:\*/|\z)'
    vb   = '(?<s>"(?:[^"\n]|"")*")|''[^\n]*'
    hash = '(?<s>"""[\s\S]*?(?:"""|\z)|''''''[\s\S]*?(?:''''''|\z)|"(?:[^"\\\n]|\\.)*"|''(?:[^''\\\n]|\\.)*'')|#[^\n]*'
    ps   = '<#[\s\S]*?(?:#>|\z)|(?<s>"(?:[^"`\n]|`.)*"|''(?:[^''\n]|'''')*'')|#[^\n]*'
    # XAML keeps its namespaces in attribute values (clr-namespace:...), so only comments go.
    xml  = '<!--[\s\S]*?(?:-->|\z)'
}

function Get-PaperApiCodeLines {
    <#
    .SYNOPSIS
    The lines of a file with its comments and string literals taken out, one output line per input line.
    .DESCRIPTION
    A name spelled in a message or a note is not a use. A string becomes "", a comment becomes nothing, and
    the line breaks inside a block comment stay, so line N of the output is line N of the input. In C-like
    code a line that starts with "* " is the next line of a block comment whose opening is not in view.
    #>
    param([AllowEmptyString()][string[]] $Lines = @(), [string] $Path)

    $style = Get-PaperApiCommentStyle $Path
    $text = (@(@($Lines) | ForEach-Object { ([string] $_).TrimEnd("`r") }) -join "`n")
    $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
        param($m)
        $breaks = "`n" * @([regex]::Matches($m.Value, "`n")).Count
        if ($m.Groups['s'].Success) { return '""' + $breaks }
        return $breaks
    }
    $stripped = [regex]::Replace($text, $script:PaperApiStripPatterns[$style], $evaluator)
    $out = @($stripped -split "`n")
    if ($style -eq 'c') { $out = @($out | ForEach-Object { if ($_ -match '^\s*\*(?:\s|$|/)') { '' } else { $_ } }) }
    return $out
}

function Get-PaperApiPrefixPattern([string] $Namespace) {
    # A prefix ending in a dot covers everything under it; without one it must end at a dot or a word
    # boundary, so "Vendor.Api" never matches "Vendor.ApiTools". Nothing glued in front ("MyVendor.Api").
    $n = [string] $Namespace
    $prefix = [regex]::Escape($n)
    if ($n.EndsWith('.')) { return "(?<![\w.])$prefix" }
    return "(?<![\w.])$prefix(?:\.|\b)"
}

function Get-PaperProjectHostImport {
    <#
    .SYNOPSIS
    Where a project imports a host namespace for every one of its files, or $null: a global using in any of
    its source files, or a <Using Include> (<Import Include> for VB) in its project file or a
    Directory.Build.props above it.
    .PARAMETER SourceFiles
    Path, Text: the project's source files (the caller may hand in only those that mention "global").
    .PARAMETER ProjectFiles
    Path, Text: the project file and the Directory.Build.props/.targets files that apply to it.
    .OUTPUTS
    Path, Line - the first import found.
    #>
    param($SourceFiles = @(), $ProjectFiles = @(), [string[]] $Namespaces = @())

    $namespaces = @(Get-PaperApiStrings $Namespaces)
    if ($namespaces.Count -eq 0) { return $null }
    foreach ($n in $namespaces) {
        $p = Get-PaperApiPrefixPattern $n
        $globalUsing = '^\s*global\s+using\s+(?:static\s+)?(?:@?\w+\s*=\s*)?(?:global::)?' + $p
        $include = '<\s*(?:Using|Import)\s[^>]*?\bInclude\s*=\s*["'']\s*' + $p
        foreach ($set in @(@{ Files = $SourceFiles; Pattern = $globalUsing }, @{ Files = $ProjectFiles; Pattern = $include })) {
            foreach ($f in @($set.Files)) {
                if ($null -eq $f) { continue }
                $raw = @(([string] $f.Text) -split "`n")
                $code = @(Get-PaperApiCodeLines -Lines $raw -Path ([string] $f.Path))
                for ($i = 0; $i -lt $code.Count; $i++) {
                    if ($code[$i] -match $set.Pattern) { return [pscustomobject]@{ Path = [string] $f.Path; Line = $raw[$i].Trim() } }
                }
            }
        }
    }
    return $null
}

function Get-PaperHostUsage {
    <#
    .SYNOPSIS
    The source files of the change that use a host namespace, with the Type.Member tokens on their added lines.
    .DESCRIPTION
    Host use is judged per FILE: a file with at least one added line uses the host when the whole file - with
    comments and strings out - names a host namespace (a using, a fully qualified name), or when its project
    imports one for every file (Import). The added lines only decide which files are this task's: a file that
    already had the using and gains a call is a host use; a file of the diff with no added line is not.
    .PARAMETER Files
    One object per file: Path; Added (its added lines); Lines (the whole file now - absent, the added lines
    stand for it); Import (Path, Line: its project's import of the host, from Get-PaperProjectHostImport).
    .PARAMETER Extensions
    Which files are code; markdown, JSON and the like never are.
    .OUTPUTS
    Path, Lines (the lines of the file that name the host, or the project's import line), Why, Members
    (Type.Member tokens on the added lines, each once).
    #>
    param(
        $Files = @(),
        [string[]] $Namespaces = @(),
        [string[]] $Extensions = $script:PaperApiCodeExtensions
    )

    $namespaces = @(Get-PaperApiStrings $Namespaces)
    if ($namespaces.Count -eq 0) { return @() }
    $patterns = @($namespaces | ForEach-Object { Get-PaperApiPrefixPattern $_ })

    $usage = New-Object System.Collections.ArrayList
    foreach ($f in @($Files)) {
        if ($null -eq $f) { continue }
        $path = [string] $f.Path
        if (-not (Test-PaperApiCodePath -Path $path -Extensions $Extensions)) { continue }
        $added = @(@($f.Added) | Where-Object { $null -ne $_ })
        if ($added.Count -eq 0) { continue }

        $wholeProp = $f.PSObject.Properties['Lines']
        $whole = if ($null -ne $wholeProp -and $null -ne $wholeProp.Value) { @($wholeProp.Value) } else { $added }
        $wholeCode = @(Get-PaperApiCodeLines -Lines $whole -Path $path)
        $hostLines = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt $wholeCode.Count; $i++) {
            foreach ($p in $patterns) { if ($wholeCode[$i] -match $p) { $hostLines.Add(([string] $whole[$i]).Trim()); break } }
        }
        $why = $null
        if ($hostLines.Count -gt 0) { $why = 'the file names it: ' + $hostLines[0] }
        elseif (Test-PaperApiCodePath -Path $path -Extensions $script:PaperApiImportExtensions) {
            # A project-wide import reaches the languages it is written for, not a XAML or script file.
            $importProp = $f.PSObject.Properties['Import']
            if ($null -ne $importProp -and $null -ne $importProp.Value) {
                $import = $importProp.Value
                $hostLines.Add([string] $import.Line)
                $why = 'its project imports it: ' + [string] $import.Path + ': ' + [string] $import.Line
            }
        }
        if ($null -eq $why) { continue }

        $members = New-Object System.Collections.Generic.List[string]
        foreach ($code in @(Get-PaperApiCodeLines -Lines $added -Path $path)) {
            # using / namespace / import lines name namespaces, not members.
            if ($code -match '^\s*(global\s+)?(using|namespace|import|Imports|open|from)\b') { continue }
            foreach ($m in [regex]::Matches($code, '(?<![\w.])[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+')) {
                $chain = $m.Value
                $isNamespace = $false
                foreach ($n in $namespaces) {
                    $bare = $n.TrimEnd('.')
                    if ($chain -eq $bare -or $chain.StartsWith($bare + '.')) { $isNamespace = $true; break }
                }
                if ($isNamespace) { continue }
                $parts = $chain.Split('.')
                # A Type.Member starts with a capital on both sides: a lowercase receiver is a variable.
                if ($parts[0] -cnotmatch '^[A-Z]' -or $parts[1] -cnotmatch '^[A-Z_]') { continue }
                $token = $parts[0] + '.' + $parts[1]
                if (-not $members.Contains($token)) { $members.Add($token) }
            }
        }
        [void] $usage.Add([pscustomobject]@{ Path = $path; Lines = @($hostLines.ToArray()); Why = $why; Members = @($members.ToArray()) })
    }
    return @($usage)
}

function Get-PaperApiCheckVerdict {
    <#
    .SYNOPSIS
    The verdict of the API lookup check.
    .OUTPUTS
    ExitCode (0 / 1 / 5), Verdict, Reason, Files (the files that use the host), Unlisted (Member, Path: the
    Type.Member tokens no table row mentions - to review, never a failure).
    #>
    param(
        $Rows = @(),
        $Usage = @(),
        [string[]] $Namespaces = @(),
        # Whether the profile has the api.namespaces key (Test-PaperApiNamespacesDeclared); only the message
        # of an empty Namespaces depends on it.
        [bool] $Declared = $true
    )

    $namespaces = @(Get-PaperApiStrings $Namespaces)
    $rows = @(@($Rows) | Where-Object { $null -ne $_ })
    $usage = @(@($Usage) | Where-Object { $null -ne $_ })
    $files = @($usage | ForEach-Object { [string] $_.Path } | Select-Object -Unique)

    function New-Verdict([int] $code, [string] $verdict, [string] $reason, $unlisted) {
        return [pscustomobject]@{ ExitCode = $code; Verdict = $verdict; Reason = $reason; Files = $files; Unlisted = @($unlisted) }
    }

    if ($namespaces.Count -eq 0) {
        if (-not $Declared) {
            return New-Verdict 5 'not applicable' ('the profile declares no api.namespaces - declare the host''s namespace prefixes in .claude/paper.profile.json, ' +
                '"api": { "namespaces": ["<prefix>"] } (the host template carries the value; an empty list turns this check off)') @()
        }
        return New-Verdict 5 'not applicable' 'api.namespaces in the profile is an empty list: this project calls no host API' @()
    }
    $nsText = $namespaces -join ', '
    if ($files.Count -eq 0) {
        return New-Verdict 5 'not applicable' "no added line of code uses $nsText" @()
    }

    $unlisted = New-Object System.Collections.ArrayList
    $seen = @{}
    foreach ($u in $usage) {
        foreach ($member in @($u.Members)) {
            $token = [string] $member
            if (-not $token -or $seen.ContainsKey($token)) { continue }
            $seen[$token] = $true
            $mentioned = $false
            foreach ($r in $rows) { if (([string] $r.Text).IndexOf($token, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { $mentioned = $true; break } }
            if (-not $mentioned) { [void] $unlisted.Add([pscustomobject]@{ Member = $token; Path = [string] $u.Path }) }
        }
    }

    if ($rows.Count -eq 0) {
        return New-Verdict 1 'fail' ("{0} file(s) use {1} and the plan's API table has no row: {2}" -f $files.Count, $nsText, ($files -join ', ')) $unlisted
    }
    return New-Verdict 0 'pass' ("{0} file(s) use {1}; the API table has {2} row(s)" -f $files.Count, $nsText, $rows.Count) $unlisted
}
