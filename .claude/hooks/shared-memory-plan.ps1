# Shared memory between Claude Code and Codex as pure functions: which folder Claude Code keeps the memory
# of a repository in, how its index (MEMORY.md) is read, and the block Codex is handed at session start.
# No file system, no git: shared-memory-read.ps1 asks git and the disk and hands the answers in. Dot-sourced;
# declares no param() block. ADR-0032, SPEC F75, F76, F80.
#
# The folder name, measured on one machine 2026-10-02: 93 folders under ~/.claude/projects with a readable
# cwd, 90 equal to the path with every character outside [A-Za-z0-9] turned into '-' (one per UTF-16 unit),
# 3 differing only in case (sessions opened from d:\), so a folder is looked up ignoring case. The longest
# was 158; past 200 Claude Code shortens the name with a suffix it computes itself, which is not guessed
# here (F76). 60 worktree folders, none holding memory: Claude keeps a worktree's memory under the main
# checkout, so Codex does too. The largest MEMORY.md was 90 lines, 13992 characters.
#
# The block's limit (F80), measured 2026-10-02 (ADR-0032, "Bo sung 2026-10-02"): Codex 0.158.0 kept about
# 10000 bytes of a 22200-byte additionalContext and cut out its middle ("...3056 tokens truncated..."), so
# the whole block, the kit's own lines included, is at most 8000 UTF-8 bytes (2000 of margin, since 10000
# is inferred from the marker), and at most 200 index lines (Claude Code's own cap on MEMORY.md). The index
# is cut at a whole line; the rules never are.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$script:PaperMemorySlugLimit = 200
$script:PaperMemoryContextLines = 200
$script:PaperCodexContextBytes = 8000
$script:PaperMemoryIndexLink = '\[([^\]]*)\]\(<?([^)<>\s]+\.md)>?\)'

function ConvertTo-PaperClaudeSlug([string] $Path) {
    <# The folder name Claude Code gives a project path: every character outside [A-Za-z0-9] is a dash. #>
    if ([string]::IsNullOrEmpty($Path)) { return '' }
    $p = $Path
    if ($p -notmatch '^[A-Za-z]:[\\/]$') { $p = $p.TrimEnd('\', '/') }
    return ($p -creplace '[^A-Za-z0-9]', '-')
}

function Get-PaperMemoryCheckout([string] $GitCommonDir, [string] $TopLevel, [string] $ProjectDir) {
    <#
    The checkout whose memory this is: the parent of a common dir named .git (a worktree shares its main
    checkout's), else the worktree top level (a submodule, a bare-named common dir), else the project folder.
    #>
    $common = ([string] $GitCommonDir).TrimEnd('\', '/')
    if ($common) {
        $cut = $common.LastIndexOfAny([char[]] @('\', '/'))
        if ($cut -gt 0 -and $common.Substring($cut + 1) -ieq '.git') { return $common.Substring(0, $cut) }
    }
    if (-not [string]::IsNullOrEmpty($TopLevel)) { return $TopLevel }
    return $ProjectDir
}

function Resolve-PaperMemoryDir([string] $ConfigDir, [string] $Slug, [AllowNull()][string[]] $ExistingNames = @()) {
    <#
    Dir (the memory folder, or $null when it cannot be told), Folder, Reason (found, new, shortened, none,
    ambiguous), Matches and ProjectsDir. ExistingNames are the folder names under ProjectsDir.
    #>
    $projects = ([string] $ConfigDir).TrimEnd('\', '/') + '\projects'
    $names = @($ExistingNames | Where-Object { $_ })
    $folder = $null; $reason = ''; $count = 0
    if ($Slug.Length -le $script:PaperMemorySlugLimit) {
        $hit = @($names | Where-Object { $_ -ieq $Slug } | Select-Object -First 1)
        if ($hit.Count -gt 0) { $folder = $hit[0]; $reason = 'found'; $count = 1 }
        else { $folder = $Slug; $reason = 'new' }
    }
    else {
        $prefix = $Slug.Substring(0, $script:PaperMemorySlugLimit)
        $hits = @($names | Where-Object { $_.Length -gt $script:PaperMemorySlugLimit -and $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })
        $count = $hits.Count
        if ($hits.Count -eq 1) { $folder = $hits[0]; $reason = 'shortened' }
        elseif ($hits.Count -eq 0) { $reason = 'none' }
        else { $reason = 'ambiguous' }
    }
    $dir = if ($folder) { "$projects\$folder\memory" } else { $null }
    return [pscustomobject]@{ Dir = $dir; Folder = $folder; Reason = $reason; Matches = $count; ProjectsDir = $projects }
}

function Get-PaperMemoryIndexEntries([AllowNull()][AllowEmptyString()][string] $Text) {
    <# Line (from 1, CRLF as LF), File (the leaf name) and Title of every .md link in an index. #>
    $out = New-Object System.Collections.Generic.List[psobject]
    if ([string]::IsNullOrEmpty($Text)) { return @() }
    $lines = $Text.Replace("`r", '') -split "`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($m in [regex]::Matches($lines[$i], $script:PaperMemoryIndexLink)) {
            $out.Add([pscustomobject]@{ Line = $i + 1; File = ($m.Groups[2].Value -split '[\\/]')[-1]; Title = $m.Groups[1].Value })
        }
    }
    return $out.ToArray()
}

function Get-PaperCodexMemoryContext($Resolved, [bool] $FolderExists, $IndexText = $null, [int] $MaxBytes = $script:PaperCodexContextBytes) {
    <#
    The block Codex is handed at session start, LF line ends: the folder, the rules for writing it, and the
    index cut at a whole line so the block is at most MaxBytes UTF-8 bytes, and at 200 lines (F80).
    IndexText $null = no MEMORY.md (F75). Resolved without a Dir = one line saying no folder was found (F76).
    #>
    if ($null -eq $Resolved -or -not $Resolved.Dir) {
        $n = if ($null -ne $Resolved) { [int] $Resolved.Matches } else { 0 }
        $projects = if ($null -ne $Resolved) { [string] $Resolved.ProjectsDir } else { '' }
        return "Shared memory: no folder found. This repository's path is long enough that Claude Code shortens its memory folder name, and $n folder(s) under $projects start with its first 200 characters. Ask the user which one belongs to this repository; do not create one."
    }
    $dir = [string] $Resolved.Dir
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("Shared memory - Claude Code's memory of this repository; Claude and Codex read and write the same files.")
    $out.Add("Folder: $dir")
    if (-not $FolderExists) { $out.Add('The folder does not exist yet: the first apply_patch "*** Add File" creates it.') }
    $out.Add('Fact files are not loaded here: open one from the folder when its index line bears on the task.')
    $out.Add('Rules for writing it:')
    $out.Add('- One fact per file: <kebab-name>.md with frontmatter name, description (one line) and metadata.type (user, feedback, project or reference). Update the file that covers a fact rather than add a near-duplicate; delete a fact no longer true together with its index line.')
    $out.Add('- In MEMORY.md add or change only that file''s line ("- [Title](file.md) - hook", under 150 characters), with one apply_patch "*** Update File" hunk. A patch that no longer applies: read the file again and patch again.')
    $out.Add('- apply_patch on the absolute path, never a shell command. A write the sandbox refuses goes to the user for approval, never into the repository.')
    $out.Add('- Never a secret, key, token, password or <private> text; nothing the repository already records.')

    if (-not $FolderExists -or $null -eq $IndexText) {
        $out.Add("MEMORY.md: none yet - add it with the first fact's line.")
        return ($out -join "`n")
    }

    $utf8 = [Text.Encoding]::UTF8
    $text = ([string] $IndexText).Replace("`r", '')
    if ($text.EndsWith("`n")) { $text = $text.Substring(0, $text.Length - 1) }
    $lines = if ($text.Length -eq 0) { @() } else { @($text -split "`n") }
    $n = $lines.Count
    $out.Add("MEMORY.md ($n lines):")
    $cutLine = { param($k, $why) "(MEMORY.md cut at line $k of $n ($why) - read the rest in $dir\MEMORY.md)" }
    $fixed = $utf8.GetByteCount(($out -join "`n"))
    $reserve = $utf8.GetByteCount("`n" + (& $cutLine $n 'session-start limit'))
    $budget = $MaxBytes - $fixed - $reserve
    $kept = 0; $used = 0; $why = ''
    foreach ($line in $lines) {
        if ($kept -ge $script:PaperMemoryContextLines) { $why = "$($script:PaperMemoryContextLines) lines"; break }
        $add = $utf8.GetByteCount("`n" + $line)
        if ($used + $add -gt $budget) { $why = 'session-start limit'; break }
        $out.Add($line); $kept++; $used += $add
    }
    if ($why) { $out.Add((& $cutLine $kept $why)) }
    else { $out.Add('(end of MEMORY.md)') }
    return ($out -join "`n")
}
