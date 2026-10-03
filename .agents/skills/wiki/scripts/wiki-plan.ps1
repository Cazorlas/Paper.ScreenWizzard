# The wiki skill's pure part (plan 2026-10-03-llm-wiki, ADR-0036): the scaffold a new wiki gets, the
# machine's registry of wikis (read, add, write), which wiki serves a project, and where the kit's hooks
# folder is. pure; dot-sourced; declares no param(). No file system, no git, no clock: wiki.ps1 does those.
# Paths inside a wiki are relative to its root, separated by '/'.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperWikiTemplateFiles = [ordered]@{ 'AGENTS.md' = 'AGENTS.md.tmpl'; 'CLAUDE.md' = 'CLAUDE.md.tmpl'; 'index.md' = 'index.md.tmpl'; 'log.md' = 'log.md.tmpl'; '.gitignore' = 'gitignore.tmpl' }
$script:PaperWikiFolders = @('raw', 'raw/assets', 'wiki/sources', 'wiki/topics', 'wiki/projects', 'wiki/answers')
$script:PaperWikiNamePattern = '^[a-z0-9]+(-[a-z0-9]+)*$'
$script:PaperWikiRegistryComment = "Wikis on this machine, kept by paper-kit's wiki skill (wiki.ps1 init adds a line). name: kebab-case; path: absolute; serves: repository folder names this wiki is for, * for every project; about: one line."
$script:PaperWikiDefaultAbout = '(say in one line what this wiki is for)'

function ConvertTo-PaperWikiName([string] $Path) {
    <# The last folder of Path as kebab-case: lower case, every run outside [a-z0-9] a dash; '' when nothing is left. #>
    $p = ([string] $Path).TrimEnd('\', '/')
    if ($p -match '^[A-Za-z]:$' -or $p -eq '') { return '' }
    $cut = $p.LastIndexOfAny([char[]] @('\', '/'))
    $leaf = if ($cut -ge 0) { $p.Substring($cut + 1) } else { $p }
    $name = ($leaf.ToLowerInvariant() -creplace '[^a-z0-9]+', '-').Trim('-')
    return $name
}

function Expand-PaperWikiTemplate([string] $Text, [hashtable] $Values) {
    <# Each {{key}} of Values replaced by its value (exact key); an unknown placeholder stays; every CR dropped. #>
    $t = [string] $Text
    foreach ($k in @($Values.Keys)) { $t = $t.Replace('{{' + $k + '}}', [string] $Values[$k]) }
    return $t.Replace("`r", '')
}

function Test-PaperWikiListed([string[]] $List, [string] $Path) {
    foreach ($e in @($List)) { if ([string]::Equals([string] $e, $Path, [StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

function Get-PaperWikiScaffold([hashtable] $Templates, [hashtable] $Values, [AllowNull()][AllowEmptyCollection()][string[]] $Existing) {
    <#
    What init makes, in order: each root file of PaperWikiTemplateFiles, then <folder>/.gitkeep for each of
    PaperWikiFolders. Path, Action (create, keep) and Text ($null when kept). A root file is kept when it is
    there; a .gitkeep when any file is already under its folder. Existing: every file under the target,
    relative with '/', .git left out.
    #>
    $have = @($Existing | Where-Object { $_ })
    $out = New-Object System.Collections.Generic.List[object]
    foreach ($file in $script:PaperWikiTemplateFiles.Keys) {
        if (Test-PaperWikiListed $have $file) {
            $out.Add([pscustomobject]@{ Path = $file; Action = 'keep'; Text = $null })
            continue
        }
        $tmpl = $script:PaperWikiTemplateFiles[$file]
        $src = if ($Templates.ContainsKey($tmpl)) { [string] $Templates[$tmpl] } else { '' }
        $out.Add([pscustomobject]@{ Path = $file; Action = 'create'; Text = (Expand-PaperWikiTemplate $src $Values) })
    }
    foreach ($folder in $script:PaperWikiFolders) {
        $prefix = $folder + '/'
        $used = @($have | Where-Object { ([string] $_).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
        $path = $prefix + '.gitkeep'
        if ($used) { $out.Add([pscustomobject]@{ Path = $path; Action = 'keep'; Text = $null }) }
        else { $out.Add([pscustomobject]@{ Path = $path; Action = 'create'; Text = '' }) }
    }
    return $out.ToArray()
}

function Test-PaperWikiSamePath([string] $A, [string] $B) {
    <# One folder: '/' as '\', no trailing '\', case ignored. #>
    $x = ([string] $A).Replace('/', '\').TrimEnd('\')
    $y = ([string] $B).Replace('/', '\').TrimEnd('\')
    return [string]::Equals($x, $y, [StringComparison]::OrdinalIgnoreCase)
}

function Get-PaperWikiProperty($Object, [string] $Name) {
    <# @{ Has; Value } of a JSON object's property, strict-mode safe. #>
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return @{ Has = $false; Value = $null } }
    return @{ Has = $true; Value = $p.Value }
}

function ConvertFrom-PaperWikiRegistry([AllowNull()][AllowEmptyString()][string] $Text) {
    <#
    Wikis (Name, Path, Serves as string[], About; in file order) and Errors (plan table B.1). An empty text is
    an empty registry. A wiki with an error is left out; unknown keys, $comment among them, are ignored.
    #>
    $wikis = New-Object System.Collections.Generic.List[object]
    $errors = New-Object System.Collections.Generic.List[string]
    $result = { [pscustomobject]@{ Wikis = $wikis.ToArray(); Errors = $errors.ToArray() } }
    if ([string]::IsNullOrWhiteSpace($Text)) { return (& $result) }
    try { $root = ConvertFrom-Json -InputObject $Text -ErrorAction Stop }
    catch {
        $errors.Add("registry: not valid JSON - $($_.Exception.Message)")
        return (& $result)
    }
    if ($null -eq $root -or $root -isnot [System.Management.Automation.PSCustomObject]) {
        $errors.Add('registry: not a JSON object')
        return (& $result)
    }
    $list = Get-PaperWikiProperty $root 'wikis'
    if (-not $list.Has -or $list.Value -isnot [array]) {
        $errors.Add('registry: "wikis" is not a list')
        return (& $result)
    }
    $i = 0
    foreach ($item in $list.Value) {
        $i++
        if ($null -eq $item -or $item -isnot [System.Management.Automation.PSCustomObject]) {
            $errors.Add("registry: wiki $i is not an object")
            continue
        }
        $bad = $false
        $label = "$i"
        $n = Get-PaperWikiProperty $item 'name'
        $name = ''
        if ($n.Has -and $n.Value -is [string] -and $n.Value -cmatch $script:PaperWikiNamePattern) {
            $name = $n.Value
            $label = $name
        }
        else {
            $shown = if ($n.Has -and $null -ne $n.Value) { "$($n.Value)" } else { '' }
            $errors.Add("registry: wiki ${i}: name '$shown' is not kebab-case")
            $bad = $true
        }
        $p = Get-PaperWikiProperty $item 'path'
        $path = if ($p.Has -and $p.Value -is [string]) { $p.Value } else { '' }
        if ($path -notmatch '^([A-Za-z]:[\\/]|\\\\[^\\])') {
            $shown = if ($p.Has -and $null -ne $p.Value) { "$($p.Value)" } else { '' }
            $errors.Add("registry: wiki ${label}: path '$shown' is not absolute")
            $bad = $true
        }
        $s = Get-PaperWikiProperty $item 'serves'
        $serves = @()
        $okServes = $s.Has -and $s.Value -is [array] -and @($s.Value).Count -gt 0
        if ($okServes) {
            foreach ($v in $s.Value) {
                if ($v -isnot [string] -or [string]::IsNullOrWhiteSpace($v)) { $okServes = $false; break }
                $serves += $v
            }
        }
        if (-not $okServes) {
            $errors.Add("registry: wiki ${label}: serves is not a list of names")
            $bad = $true
        }
        $ab = Get-PaperWikiProperty $item 'about'
        $about = ''
        if ($ab.Has) {
            if ($ab.Value -is [string]) { $about = $ab.Value }
            else {
                $errors.Add("registry: wiki ${label}: about is not text")
                $bad = $true
            }
        }
        if ($bad) { continue }
        $clash = $false
        foreach ($w in $wikis) {
            if ($w.Name -ieq $name) { $errors.Add("registry: two wikis are named $name"); $clash = $true; break }
            if (Test-PaperWikiSamePath $w.Path $path) { $errors.Add("registry: $($w.Name) and $name have the same path"); $clash = $true; break }
        }
        if ($clash) { continue }
        $wikis.Add([pscustomobject]@{ Name = $name; Path = $path; Serves = [string[]] $serves; About = $about })
    }
    return (& $result)
}

function Add-PaperWikiRegistryEntry($Wikis, [string] $Name, [string] $Path, [string[]] $Serves, [string] $About) {
    <# Action add, same or conflict, Message, and Wikis (the new list on add, else unchanged) - plan B.2. #>
    $list = @($Wikis | Where-Object { $null -ne $_ })
    foreach ($w in $list) {
        if (Test-PaperWikiSamePath $w.Path $Path) {
            if ($w.Name -ieq $Name) { return [pscustomobject]@{ Action = 'same'; Message = "already registered as $($w.Name)"; Wikis = $list } }
            return [pscustomobject]@{ Action = 'conflict'; Message = "$($w.Path) is already registered as $($w.Name) - use that name"; Wikis = $list }
        }
    }
    foreach ($w in $list) {
        if ($w.Name -ieq $Name) {
            return [pscustomobject]@{ Action = 'conflict'; Message = "the name $Name is taken by $($w.Path) - choose another with -Name"; Wikis = $list }
        }
    }
    $new = [pscustomobject]@{ Name = $Name; Path = $Path; Serves = [string[]] @($Serves); About = [string] $About }
    return [pscustomobject]@{ Action = 'add'; Message = "registered $Name"; Wikis = @($list + $new) }
}

function ConvertTo-PaperWikiRegistryJson($Wikis) {
    <# The registry file's text: $comment, then wikis (name, path, serves always a list, about). #>
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($w in @($Wikis | Where-Object { $null -ne $_ })) {
        $items.Add([ordered]@{ name = [string] $w.Name; path = [string] $w.Path; serves = [object[]] @($w.Serves); about = [string] $w.About })
    }
    $doc = [ordered]@{ '$comment' = $script:PaperWikiRegistryComment; wikis = [object[]] $items.ToArray() }
    return (ConvertTo-Json -InputObject $doc -Depth 5)
}

function Get-PaperWikiMatches($Wikis, [string] $Project) {
    <# Wikis naming the project (a serves entry other than * that the project is -like), then the * wikis; registry order. #>
    $named = New-Object System.Collections.Generic.List[object]
    $star = New-Object System.Collections.Generic.List[object]
    foreach ($w in @($Wikis | Where-Object { $null -ne $_ })) {
        $hit = $false
        $hasStar = $false
        foreach ($s in @($w.Serves)) {
            if ($s -eq '*') { $hasStar = $true; continue }
            if ($Project -like $s) { $hit = $true }
        }
        if ($hit) { $named.Add($w) }
        elseif ($hasStar) { $star.Add($w) }
    }
    return @($named.ToArray() + $star.ToArray())
}

function Get-PaperWikiHooksCandidates([string] $ScriptRoot) {
    <# Where wiki.ps1 looks for the kit's hooks folder: beside the skill (.claude), then the .claude beside .agents (Codex). #>
    return @((Join-Path $ScriptRoot '..\..\..\hooks'), (Join-Path $ScriptRoot '..\..\..\..\.claude\hooks'))
}
