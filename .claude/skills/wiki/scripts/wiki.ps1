# The wiki skill's runner (plan 2026-10-03-llm-wiki, table E; ADR-0036). Three commands:
#   init  -Wiki <folder> [-Name <kebab>] [-Serves a,b] [-About "<one line>"] [-DryRun] [-NoRegister]
#         lays out a wiki: adds what is missing, keeps every file that is there, registers it on this machine
#   lint  [-Wiki <folder or registered name>]   free checks, no model; exit 0 clean, 1 errors, 2 cannot run
#   which [-Repo <folder>]                      the wikis serving the project open there; exit 5 when none
# -Registry <file> replaces the machine's registry (%LOCALAPPDATA%\paper-kit\wikis.json). Every git call is
# read only and runs with GIT_OPTIONAL_LOCKS=0. Output is UTF-8 without BOM on stdout, errors included.
# The pure parts are scripts/wiki-plan.ps1 and scripts/wiki-lint-plan.ps1; secrets come from the kit's
# hooks/memory-nag-plan.ps1, found beside the skill (Claude layout) or in the .claude beside .agents (Codex).
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.
param(
    [Parameter(Position = 0)][string] $Command = '',
    [string] $Wiki = '',
    [string] $Name = '',
    [string] $Serves = '*',
    [string] $About = '',
    [string] $Repo = '',
    [string] $Registry = '',
    [switch] $DryRun,
    [switch] $NoRegister
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$utf8 = New-Object System.Text.UTF8Encoding $false
$stdout = [Console]::OpenStandardOutput()
function Say([string] $Line) {
    $bytes = $utf8.GetBytes($Line + "`n")
    $stdout.Write($bytes, 0, $bytes.Length)
}
function Stop-Wiki([int] $Code) {
    $stdout.Flush()
    exit $Code
}

$usage = @(
    'wiki.ps1 init -Wiki <folder> [-Name <kebab>] [-Serves a,b] [-About "<one line>"] [-DryRun] [-NoRegister] [-Registry <file>]',
    'wiki.ps1 lint [-Wiki <folder or registered name>] [-Registry <file>]',
    'wiki.ps1 which [-Repo <folder>] [-Registry <file>]'
)
if ($Command -eq '' -or $Command -eq 'help') {
    foreach ($u in $usage) { Say $u }
    Stop-Wiki 0
}
if (@('init', 'lint', 'which') -notcontains $Command) {
    Say "wiki: unknown command '$Command' (init, lint, which)"
    Stop-Wiki 2
}

[Environment]::SetEnvironmentVariable('GIT_OPTIONAL_LOCKS', '0')

function Get-FullPath([string] $Path) {
    $p = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if ($p -notmatch '^[A-Za-z]:\\$') { $p = $p.TrimEnd('\') }
    return $p
}
if ($Registry -eq '') {
    $base = if ($env:LOCALAPPDATA) { $env:LOCALAPPDATA } else { $env:TEMP }
    $Registry = Join-Path $base 'paper-kit\wikis.json'
}
$Registry = Get-FullPath $Registry

. (Join-Path $PSScriptRoot 'wiki-plan.ps1')
. (Join-Path $PSScriptRoot 'wiki-lint-plan.ps1')
$candidates = @(Get-PaperWikiHooksCandidates $PSScriptRoot)
$hooks = $null
foreach ($c in $candidates) { if (Test-Path -LiteralPath (Join-Path $c 'memory-nag-plan.ps1')) { $hooks = $c; break } }
if ($null -eq $hooks) {
    Say "wiki: the kit's hooks folder is not beside this script (looked in $($candidates[0]), $($candidates[1])) - rerun paper-kit setup"
    Stop-Wiki 2
}
. (Join-Path $hooks 'memory-nag-plan.ps1')

function Invoke-WikiGit([string] $Dir, [string[]] $Arguments) {
    <# Code and Out (stdout lines) of a read-only git call; stderr dropped. #>
    $ErrorActionPreference = 'Continue'
    $out = @(& git.exe -C $Dir @Arguments 2>$null | ForEach-Object { "$_" })
    return [pscustomobject]@{ Code = $LASTEXITCODE; Out = $out }
}
function Read-Utf8([string] $Path) { return [IO.File]::ReadAllText($Path, $utf8) }

# The registry, when its file is there.
$wikis = @()
$registryExists = Test-Path -LiteralPath $Registry -PathType Leaf
if ($registryExists) {
    $parsed = ConvertFrom-PaperWikiRegistry (Read-Utf8 $Registry)
    if (@($parsed.Errors).Count -gt 0) {
        foreach ($e in @($parsed.Errors)) { Say "wiki: $e" }
        if ($Command -eq 'init') { Say 'wiki: nothing written' }
        Stop-Wiki 2
    }
    $wikis = @($parsed.Wikis)
}

try {
    if ($Command -eq 'init') {
        if ($Wiki -eq '') { Say 'wiki: init needs -Wiki <folder>'; Stop-Wiki 2 }
        $target = Get-FullPath $Wiki
        if (Test-Path -LiteralPath $target -PathType Leaf) { Say "wiki: $target is a file, not a folder"; Stop-Wiki 2 }
        if ($Name -eq '') {
            $Name = ConvertTo-PaperWikiName $target
            if ($Name -eq '') { Say "wiki: cannot make a name from $target - pass -Name"; Stop-Wiki 2 }
        }
        if ($Name -cnotmatch $script:PaperWikiNamePattern) { Say "wiki: name '$Name' is not kebab-case"; Stop-Wiki 2 }

        # A wiki is a repository of its own: never inside another one (F107). Measured with --show-prefix, not by
        # comparing paths - %TEMP% on CI is an 8.3 path and git answers with the long one.
        $targetExists = Test-Path -LiteralPath $target -PathType Container
        $probe = $target
        while (-not (Test-Path -LiteralPath $probe -PathType Container)) {
            $parent = Split-Path $probe -Parent
            if ([string]::IsNullOrEmpty($parent) -or $parent -eq $probe) { break }
            $probe = $parent
        }
        $g = Invoke-WikiGit $probe @('rev-parse', '--show-prefix')
        if ($g.Code -eq 0) {
            $prefix = (($g.Out -join '')).Trim()
            if (-not ($targetExists -and $probe -eq $target -and $prefix -eq '')) {
                $top = ((Invoke-WikiGit $probe @('rev-parse', '--show-toplevel')).Out -join '').Trim().Replace('/', '\')
                Say "wiki: $target is inside the repository $top - a wiki is a repository of its own; pick a folder outside it"
                Stop-Wiki 2
            }
        }

        $reg = $null
        if (-not $NoRegister) {
            $serveList = @($Serves -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
            # An empty list would be written as "serves": [], which the next read refuses as a broken registry.
            if ($serveList.Count -eq 0) {
                Say "wiki: -Serves '$Serves' names no project - give repository folder names separated by commas, or * for every project"
                Say 'wiki: nothing written'
                Stop-Wiki 2
            }
            $reg = Add-PaperWikiRegistryEntry $wikis $Name $target $serveList $About
            if ($reg.Action -eq 'conflict') {
                Say "wiki: $($reg.Message)"
                Say 'wiki: nothing written'
                Stop-Wiki 2
            }
        }

        $templates = @{}
        foreach ($t in @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\template') -Filter '*.tmpl' -File)) { $templates[$t.Name] = Read-Utf8 $t.FullName }
        $existing = @()
        if ($targetExists) {
            $existing = @(Get-ChildItem -LiteralPath $target -Recurse -Force -File | ForEach-Object {
                    $_.FullName.Substring($target.Length).TrimStart('\').Replace('\', '/')
                } | Where-Object { $_ -notmatch '^\.git(/|$)' })
        }
        $aboutText = if ($About -ne '') { $About } else { $script:PaperWikiDefaultAbout }
        $values = @{ name = $Name; date = (Get-Date).ToString('yyyy-MM-dd'); about = $aboutText; runner = $PSCommandPath }
        $plan = @(Get-PaperWikiScaffold $templates $values $existing)

        if ($DryRun) {
            foreach ($i in $plan) { if ($i.Action -eq 'create') { Say "would create $($i.Path)" } else { Say "kept $($i.Path)" } }
            if ($null -ne $reg) {
                if ($reg.Action -eq 'add') { Say "would register $Name in $Registry" } else { Say $reg.Message }
            }
            Say 'wiki: dry run - nothing written'
            Stop-Wiki 0
        }

        $created = 0
        $kept = 0
        if (-not $targetExists) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
        foreach ($i in $plan) {
            if ($i.Action -eq 'keep') { Say "kept $($i.Path)"; $kept++; continue }
            $full = Join-Path $target ($i.Path.Replace('/', '\'))
            $dir = Split-Path $full -Parent
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            [IO.File]::WriteAllText($full, [string] $i.Text, $utf8)
            Say "created $($i.Path)"
            $created++
        }
        if ($null -ne $reg) {
            if ($reg.Action -eq 'add') {
                $regDir = Split-Path $Registry -Parent
                if (-not (Test-Path -LiteralPath $regDir)) { New-Item -ItemType Directory -Path $regDir -Force | Out-Null }
                $tmp = $Registry + '.tmp'
                [IO.File]::WriteAllText($tmp, (ConvertTo-PaperWikiRegistryJson $reg.Wikis), $utf8)
                Move-Item -LiteralPath $tmp -Destination $Registry -Force
                Say "registered $Name in $Registry"
            }
            else { Say $reg.Message }
        }
        if ((Invoke-WikiGit $target @('rev-parse', '--show-toplevel')).Code -ne 0) {
            Say "wiki: $target is not a git repository yet - run git init there when the owner wants history"
        }
        Say "wiki: $Name at $target - $created created, $kept kept"
        Stop-Wiki 0
    }

    if ($Command -eq 'lint') {
        if ($Wiki -eq '') { $Wiki = '.' }
        $path = Get-FullPath $Wiki
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            $named = @($wikis | Where-Object { $Wiki -cmatch $script:PaperWikiNamePattern -and $_.Name -ieq $Wiki })
            if ($named.Count -eq 0) { Say "wiki: no folder and no registered wiki named '$Wiki'"; Stop-Wiki 2 }
            $path = $named[0].Path
            if (-not (Test-Path -LiteralPath $path -PathType Container)) {
                Say "wiki: $Wiki is registered at $path, which is missing - fix the path in $Registry or remove the entry"
                Stop-Wiki 2
            }
        }
        if (-not (Test-Path -LiteralPath (Join-Path $path 'index.md') -PathType Leaf) -and -not (Test-Path -LiteralPath (Join-Path $path 'wiki') -PathType Container)) {
            Say "wiki: $path is not a wiki (no index.md, no wiki/ folder) - run wiki.ps1 init -Wiki $path to make one"
            Stop-Wiki 2
        }
        $files = New-Object System.Collections.Generic.List[string]
        foreach ($top in @('wiki', 'raw')) {
            $dir = Join-Path $path $top
            if (-not (Test-Path -LiteralPath $dir -PathType Container)) { continue }
            foreach ($f in @(Get-ChildItem -LiteralPath $dir -Recurse -Force -File)) {
                $rel = $f.FullName.Substring($path.Length).TrimStart('\').Replace('\', '/')
                if ($f.Name -eq '.gitkeep') { continue }
                if (@($rel.Split('/') | Where-Object { $_.StartsWith('.') }).Count -gt 0) { continue }
                $files.Add($rel)
            }
        }
        $texts = @{}
        foreach ($rf in $script:PaperWikiRootFiles) {
            $full = Join-Path $path $rf
            if (Test-Path -LiteralPath $full -PathType Leaf) { $texts[$rf] = Read-Utf8 $full }
        }
        foreach ($rel in $files) {
            if ($rel.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) { $texts[$rel] = Read-Utf8 (Join-Path $path ($rel.Replace('/', '\'))) }
        }
        $heads = @{}
        foreach ($rel in $files) {
            if ($rel -notmatch '^raw/.+\.md$') { continue }
            $pin = Get-PaperWikiPinnedSource $texts[$rel]
            if ($null -eq $pin -or $heads.ContainsKey($pin.Repo)) { continue }
            $headSha = ''
            if (Test-Path -LiteralPath $pin.Repo -PathType Container) {
                $g = Invoke-WikiGit $pin.Repo @('rev-parse', 'HEAD')
                if ($g.Code -eq 0) { $headSha = ($g.Out -join '').Trim() }
            }
            $heads[$pin.Repo] = $headSha
        }
        $findings = @(Get-PaperWikiLintFindings -Texts $texts -Files $files.ToArray() -Heads $heads)
        foreach ($f in $findings) { Say (Format-PaperWikiFinding $f) }
        $pages = @($files | Where-Object { $_ -match '^wiki/.+\.md$' }).Count
        Say (Format-PaperWikiSummary $findings $pages)
        Stop-Wiki (Get-PaperWikiLintExitCode $findings)
    }

    if ($Command -eq 'which') {
        $repoDir = if ($Repo -eq '') { (Get-Location).ProviderPath } else { Get-FullPath $Repo }
        $common = ''
        $top = ''
        $g = Invoke-WikiGit $repoDir @('rev-parse', '--git-common-dir')
        if ($g.Code -eq 0) {
            $common = ($g.Out -join '').Trim()
            if ($common -ne '' -and -not [IO.Path]::IsPathRooted($common)) { $common = Join-Path $repoDir $common }
            $t = Invoke-WikiGit $repoDir @('rev-parse', '--show-toplevel')
            if ($t.Code -eq 0) { $top = ($t.Out -join '').Trim() } else { $common = '' }
        }
        $checkout = (Get-PaperMemoryCheckout $common $top $repoDir).TrimEnd('\', '/')
        $project = $checkout.Substring($checkout.LastIndexOfAny([char[]] @('\', '/')) + 1)
        if (-not $registryExists) { Say "wiki: no registry at $Registry - create a wiki with wiki.ps1 init"; Stop-Wiki 5 }
        $hits = @(Get-PaperWikiMatches $wikis $project)
        if ($hits.Count -eq 0) {
            $names = if ($wikis.Count -gt 0) { (@($wikis | ForEach-Object { $_.Name }) -join ', ') } else { 'none' }
            Say "wiki: no wiki serves $project; $Registry lists $($wikis.Count): $names"
            Stop-Wiki 5
        }
        $present = 0
        foreach ($w in $hits) {
            if (Test-Path -LiteralPath $w.Path -PathType Container) {
                $present++
                $line = "wiki: $($w.Name) -> $($w.Path) (serves $(@($w.Serves) -join ', '))"
                if ($w.About -ne '') { $line += " - $($w.About)" }
                Say $line
            }
            else { Say "wiki: $($w.Name) -> $($w.Path) (folder missing - fix the path in $Registry or remove the entry)" }
        }
        if ($present -eq 0) { Stop-Wiki 5 }
        Stop-Wiki 0
    }
}
catch {
    Say "wiki: $($_.Exception.Message)"
    Stop-Wiki 2
}
