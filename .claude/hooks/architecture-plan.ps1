# The decision half of the two architecture guards every host shares. Pure: lines in, hits out.
# Dot-sourced by layer-guard.ps1 and no-static-host-state.ps1; tests/architecture.tests.ps1 covers it.
#
# Projects on different hosts each wrote these guards with their own project and host names baked in.
# Written once here, the project's .claude/paper.profile.json says what is forbidden where (Vendor.HostApi
# stands for the host's namespace and package; the host pack's profile.json carries the real ones):
#
#   "architecture": {
#     "platformFree":   [ { "path": "**/UseCases/**", "forbid": ["Vendor.HostApi"], "forbidPackages": ["Vendor.HostApi"] } ],
#     "hostStateTypes": [ "Document", "Element" ]
#   }
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function ConvertTo-PaperGlobRegex([string] $Glob) {
    # `**/` may be empty (a file at the root), `/**` may be empty (the folder itself), `*` stays inside one
    # folder. Everything else is literal - a dot in `*.Domain` is a dot.
    $g = $Glob.Replace('\', '/')
    $sb = New-Object System.Text.StringBuilder
    [void] $sb.Append('^')
    $i = 0
    while ($i -lt $g.Length) {
        if ($g.Substring($i).StartsWith('**/')) { [void] $sb.Append('(?:.*/)?'); $i += 3; continue }
        if ($g.Substring($i).StartsWith('/**')) { [void] $sb.Append('(?:/.*)?'); $i += 3; continue }
        if ($g.Substring($i).StartsWith('**')) { [void] $sb.Append('.*'); $i += 2; continue }
        $c = $g[$i]
        if ($c -eq '*') { [void] $sb.Append('[^/]*') }
        elseif ($c -eq '?') { [void] $sb.Append('[^/]') }
        else { [void] $sb.Append([regex]::Escape([string] $c)) }
        $i++
    }
    [void] $sb.Append('$')
    return $sb.ToString()
}

function Get-PaperRuleValue($Rule, [string] $Name) {
    if ($null -eq $Rule) { return @() }
    if ($Rule -is [System.Collections.IDictionary]) { return @($Rule[$Name]) | Where-Object { $_ } }
    return @($Rule.$Name) | Where-Object { $_ }
}

function Get-PaperLayerHits {
    <#
    .SYNOPSIS
    Lines of one file that reach a namespace or package its layer forbids.
    .PARAMETER RelPath
    The file's path relative to the project root. Either slash; matching ignores case.
    .PARAMETER Rules
    architecture.platformFree from the profile: path (a glob), forbid (namespace prefixes),
    forbidPackages (substrings of a package, reference or hint path).
    #>
    param(
        [Parameter(Mandatory = $true)][string] $RelPath,
        [AllowEmptyString()][string[]] $Lines = @(),
        $Rules = @()
    )

    $hits = New-Object System.Collections.ArrayList
    $rel = $RelPath.Replace('\', '/').TrimStart('/')
    $isProject = $rel -match '\.csproj$'
    $isCode = $rel -match '\.cs$'
    if (-not ($isProject -or $isCode)) { return @() }

    foreach ($rule in @($Rules)) {
        if ($null -eq $rule) { continue }
        $path = @(Get-PaperRuleValue $rule 'path')[0]
        if (-not $path) { continue }
        if ($rel -notmatch ('(?i)' + (ConvertTo-PaperGlobRegex $path))) { continue }

        $forbid = @(Get-PaperRuleValue $rule 'forbid')
        $packages = @(Get-PaperRuleValue $rule 'forbidPackages')

        for ($n = 0; $n -lt $Lines.Count; $n++) {
            $line = [string] $Lines[$n]

            if ($isProject) {
                $value = [regex]::Match($line, '<(?:PackageReference|Reference|FrameworkReference)\s+Include="([^"]+)"')
                $hint = [regex]::Match($line, '<HintPath>([^<]+)</HintPath>')
                $text = if ($value.Success) { $value.Groups[1].Value } elseif ($hint.Success) { $hint.Groups[1].Value } else { '' }
                $found = $null
                if ($text) { foreach ($p in $packages) { if ($text.IndexOf([string] $p, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $found = [string] $p; break } } }
                # WPF and WinForms arrive through a property, not a package.
                if (-not $found -and $line -match '<(UseWPF|UseWindowsForms)>\s*true\s*<') {
                    foreach ($f in $forbid) { if ([string] $f -like 'System.Windows*') { $found = [string] $f; break } }
                }
                if ($found) { [void] $hits.Add([pscustomobject]@{ Line = $n + 1; Forbidden = $found; Text = $line.Trim(); Rule = $path }) }
                continue
            }

            # Strip comments and string literals first: a type NAME spelled in a message is not a dependency.
            $code = $line -replace '//.*$', '' -replace '"(?:[^"\\]|\\.)*"', '""'
            foreach ($f in $forbid) {
                $prefix = [regex]::Escape([string] $f)
                # A prefix ending in a dot ("Vendor.") covers every namespace under it; without one it must
                # end at a dot or a word boundary, so "Vendor.HostApi" never matches "Vendor.HostApiTools".
                $pattern = if (([string] $f).EndsWith('.')) { "(?<![\w.])$prefix" } else { "(?<![\w.])$prefix(?:\.|\b)" }
                if ($code -match $pattern) {
                    [void] $hits.Add([pscustomobject]@{ Line = $n + 1; Forbidden = [string] $f; Text = $line.Trim(); Rule = $path })
                    break
                }
            }
        }
    }
    return @($hits)   # callers wrap in @(); a comma here as well double-wraps an empty result into Count 1
}

function Get-PaperStaticStateHits {
    <#
    .SYNOPSIS
    Static fields that hold a live host object.
    .DESCRIPTION
    A static field initializer runs once per host PROCESS and captures the first document it sees. Close and
    reopen the file and every call on it throws - long after the edit that caused it, in a session nobody is
    debugging, and invisible to unit tests, which never open a second document. It has shipped as a
    production bug in a real add-in.
    .PARAMETER Types
    architecture.hostStateTypes from the profile. Empty means the guard is off.
    #>
    param(
        [AllowEmptyString()][string[]] $Lines = @(),
        $Types = @()
    )

    $names = @($Types | Where-Object { $_ } | ForEach-Object { [regex]::Escape([string] $_) })
    if ($names.Count -eq 0) { return @() }
    $typePattern = '\b(' + ($names -join '|') + ')\b'

    $hits = New-Object System.Collections.ArrayList
    for ($n = 0; $n -lt $Lines.Count; $n++) {
        $line = [string] $Lines[$n]
        if ($line -match '^\s*//') { continue }
        if ($line -notmatch '\bstatic\b') { continue }
        if ($line -notmatch $typePattern) { continue }
        if ($line -match '\bconst\b') { continue }

        # The first real assignment: not ==, !=, <=, >=, and not => (a property that reads fresh every call).
        $assign = [regex]::Match($line, '(?<![=!<>])=(?![=>])')
        if (-not $assign.Success) { continue }
        # A '(' before it makes the '=' a default parameter value in a method signature.
        if ($line.Substring(0, $assign.Index).Contains('(')) { continue }

        [void] $hits.Add([pscustomobject]@{ Line = $n + 1; Text = $line.Trim() })
    }
    return @($hits)   # callers wrap in @(); a comma here as well double-wraps an empty result into Count 1
}
