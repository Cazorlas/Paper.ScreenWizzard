# Shared input half of the kit's hooks: the tool payload from stdin and the project profile from disk.
# Dot-sourced; declares no param() block, because dot-sourcing runs one in the caller's scope.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

. (Join-Path $PSScriptRoot '..\paperflow\profile-map.ps1')

function Read-PaperHookPayload {
    # stdin as UTF-8 BYTES. PowerShell 5.1 decodes [Console]::In with the OEM codepage, so a path through
    # a Vietnamese folder ("Thuc hanh_Cap thoat nuoc" with its diacritics) arrives mangled and never exists
    # on disk - the guard would silently pass every edit in that folder.
    # Under a dispatcher (pre-tool.ps1, post-tool.ps1) stdin was read once already and is handed over in
    # $global:PaperHookRaw, so every guard of the group sees the same payload (ADR-0021). Test-Path, not a
    # read: a caller under Set-StrictMode (session-anchor through kit-version-plan) throws on an unset variable.
    try {
        if (Test-Path -LiteralPath 'variable:global:PaperHookRaw') { $raw = [string] $global:PaperHookRaw }
        else {
            $stream = [Console]::OpenStandardInput()
            $reader = New-Object System.IO.StreamReader($stream, (New-Object System.Text.UTF8Encoding $false))
            $raw = $reader.ReadToEnd()
        }
        if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
        $payload = $raw | ConvertFrom-Json
        # Codex reports file edits as apply_patch with the patch in tool_input.command. The current Paper
        # guards consume tool_input.file_path, so expose the first touched path in the same shape. Claude's
        # native Edit/Write payload is left unchanged. The full patch is deliberately not copied into any
        # guard message or evidence record.
        $toolNameProperty = $payload.PSObject.Properties['tool_name']
        $toolInputProperty = $payload.PSObject.Properties['tool_input']
        $toolName = if ($toolNameProperty) { [string]$toolNameProperty.Value } else { '' }
        $toolInput = if ($toolInputProperty) { $toolInputProperty.Value } else { $null }
        $commandProperty = if ($toolInput) { $toolInput.PSObject.Properties['command'] } else { $null }
        if ($toolName -eq 'apply_patch' -and $toolInput -and $commandProperty) {
            $match = [regex]::Match([string]$commandProperty.Value, '(?m)^\*\*\* (?:Update|Add|Delete) File: (.+?)\s*$')
            if ($match.Success) {
                $path = $match.Groups[1].Value.Trim()
                if (-not [IO.Path]::IsPathRooted($path) -and $payload.cwd) { $path = Join-Path ([string]$payload.cwd) $path }
                if ($null -eq $toolInput.PSObject.Properties['file_path']) { $toolInput | Add-Member -NotePropertyName file_path -NotePropertyValue $path }
                else { $toolInput.file_path = $path }
            }
        }
        # Existing Paper guards use this variable after reading the payload. Resolve it to the current
        # profile-backed checkout so Claude worktrees and Codex hooks inspect the right project.
        if ($payload.PSObject.Properties['cwd']) {
            $projectDir = Get-PaperHookProjectDir $payload
            if ($projectDir) { $env:CLAUDE_PROJECT_DIR = $projectDir }
        }
        return $payload
    }
    catch { return $null }
}


# The profile as a map, or $null - missing and unreadable alike, so a hook lets the turn through (F10).
# The reading itself is Read-PaperProfileFile in paperflow/profile-map.ps1, shared with the runner.
function Read-PaperProfile([string] $ProjectDir) {
    return (Read-PaperProfileFile $ProjectDir).Map
}

# Path relative to the project, / separators - the form a {files:} glob and a rule's path are written in.
# The one helper for this in the hooks (session-state's Get-PaperRelative forwards here).
#
# Outside the project, and for the project folder itself, the answer is $null: that path has no name inside
# the project. The callers that can meet one (layer-guard, live-first-guard) read $null as "not this
# project's file" and stay quiet; handing back the absolute path instead, as a second copy of this once did,
# gives a string a glob such as **/*.cs matches. A path that cannot be resolved at all is $null too.
function Get-PaperRelativePath([string] $ProjectDir, [string] $Path) {
    if ([string]::IsNullOrWhiteSpace($ProjectDir) -or [string]::IsNullOrWhiteSpace($Path)) { return $null }
    try {
        $full = [System.IO.Path]::GetFullPath($Path)
        $root = [System.IO.Path]::GetFullPath($ProjectDir).TrimEnd('\', '/') + '\'
    }
    catch { return $null }
    if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { return $full.Substring($root.Length).Replace('\', '/') }
    return $null
}

# The checkout the hook works on.
#
# Claude Code hands a hook two different places, and when the session is isolated in a git worktree they
# are different ON PURPOSE (docs: "Run parallel sessions with worktrees"):
#
#   CLAUDE_PROJECT_DIR  stays at the checkout the session was LAUNCHED from, so a hook command written as
#                       $CLAUDE_PROJECT_DIR/.claude/hooks/x.ps1 still finds its script.
#   payload.cwd         follows the session into the worktree, and moves again when Claude runs cd.
#
# Reading the variable first pointed every hook at the main checkout while the session worked in a
# worktree: the tick reminder read the plans of whoever was working THERE, the Stop verb built that
# checkout and took its run lock, and session state was written across it. Two sessions on one repository
# reading each other's work is the thing a worktree exists to prevent.
#
# cwd alone is no better, because a cd makes a subfolder look like the project. The checkout is the
# nearest folder at or above cwd holding .claude/paper.profile.json - the file that says a project starts
# here - and only when cwd offers none does the launch directory answer.
function Get-PaperHookProjectDir($Payload) {
    if ($null -ne $Payload) {
        $cwd = [string] $Payload.cwd
        if (-not [string]::IsNullOrWhiteSpace($cwd)) {
            try {
                $dir = [System.IO.Path]::GetFullPath($cwd).TrimEnd('\', '/')
                while ($dir) {
                    if (Test-Path -LiteralPath (Join-Path $dir '.claude\paper.profile.json') -PathType Leaf) { return $dir }
                    $parent = [System.IO.Path]::GetDirectoryName($dir)
                    if ($parent -eq $dir) { break }
                    $dir = $parent
                }
            }
            catch { }
        }
    }
    $candidates = @($env:CLAUDE_PROJECT_DIR)
    if ($null -ne $Payload) { $candidates += [string] $Payload.cwd }
    foreach ($c in $candidates) {
        if ([string]::IsNullOrWhiteSpace($c)) { continue }
        try { if (Test-Path -LiteralPath $c -PathType Container) { return [System.IO.Path]::GetFullPath($c).TrimEnd('\', '/') } } catch { }
    }
    return $null
}

# Text to Claude as UTF-8 BYTES, the mirror of Read-PaperHookPayload. Write-Output and [Console]::Error
# encode with the OEM codepage on 5.1, so a Vietnamese path quoted in a block message reached the agent as
# question marks - a message about the wrong file.
function Write-PaperHookText([string] $Text, [switch] $ToError) {
    try {
        $stream = if ($ToError) { [Console]::OpenStandardError() } else { [Console]::OpenStandardOutput() }
        $bytes = (New-Object System.Text.UTF8Encoding $false).GetBytes($Text + "`n")
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
    }
    catch { }
}

# One value of a profile map, or $null. The profile arrives as nested hashtables (ConvertTo-PaperMap in paperflow/profile-map.ps1).
function Get-PaperProfileValue($Map, [string[]] $Keys) {
    $v = $Map
    foreach ($k in $Keys) {
        if ($null -eq $v -or $v -isnot [System.Collections.IDictionary]) { return $null }
        $v = $v[$k]
    }
    return $v
}
