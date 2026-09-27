# One way to turn ConvertFrom-Json output into maps, and one way to read a project's profile. Dot-sourced by
# the hooks (hook-input.ps1), the runner (paperflow.ps1, worktree.ps1) and setup (paper-kit/scripts/host-plan.ps1);
# declares no param() block.
#
# There were four converters and two profile readers, and they disagreed: one left arrays of objects as
# PSCustomObject, one unrolled a one-element array into its element, one kept "$comment" keys and one dropped
# them. A profile key that worked in a hook could be unreadable in the runner.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

function ConvertTo-PaperMap($Value, [switch] $SkipDocKeys) {
    <#
    .SYNOPSIS
    ConvertFrom-Json output (PSCustomObject on 5.1, which has no -AsHashtable) to ordered maps and arrays, so
    every caller indexes a value the same way whether it came from a file or a test literal.
    .DESCRIPTION
    A dictionary is copied, so a caller that edits the result never edits its input. -SkipDocKeys drops keys
    that start with '$' ($comment and friends are documentation); a profile is read with it, a settings
    fragment without it, because the fragment's own "$comment" keys are skipped where they are merged.
    #>
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value -is [System.Collections.IDictionary]) {
        $map = [ordered]@{}
        foreach ($k in $Value.Keys) {
            if ($SkipDocKeys -and ([string] $k).StartsWith('$')) { continue }
            $map[$k] = ConvertTo-PaperMap $Value[$k] -SkipDocKeys:$SkipDocKeys
        }
        return $map
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        # The leading comma keeps a one-element array an array on the way out of the function.
        return , @($Value | ForEach-Object { ConvertTo-PaperMap $_ -SkipDocKeys:$SkipDocKeys })
    }
    # PSCustomObject exactly, not [psobject]: nearly every value is a [psobject] in PowerShell, and an
    # empty JSON object `{}` has no properties - both used to fall through and stay unindexable.
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $map = [ordered]@{}
        foreach ($prop in $Value.PSObject.Properties) {
            if ($SkipDocKeys -and $prop.Name.StartsWith('$')) { continue }
            $map[$prop.Name] = ConvertTo-PaperMap $prop.Value -SkipDocKeys:$SkipDocKeys
        }
        return $map
    }
    return $Value
}

# .claude/paper.profile.json of a project: Found (the file exists), Map (the profile, or $null), Error (why a
# file that exists could not be read, or ''). Callers decide what an error means - a hook lets the turn
# through, the runner stops with exit 2 - so this function never exits and never writes.
function Read-PaperProfileFile([string] $ProjectDir) {
    $none = [pscustomobject]@{ Found = $false; Map = $null; Error = '' }
    if (-not $ProjectDir) { return $none }
    $file = [IO.Path]::Combine($ProjectDir, '.claude', 'paper.profile.json')
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { return $none }
    try {
        $map = ConvertTo-PaperMap ([IO.File]::ReadAllText($file, [Text.Encoding]::UTF8) | ConvertFrom-Json) -SkipDocKeys
        return [pscustomobject]@{ Found = $true; Map = $map; Error = '' }
    }
    catch { return [pscustomobject]@{ Found = $true; Map = $null; Error = $_.Exception.Message } }
}
