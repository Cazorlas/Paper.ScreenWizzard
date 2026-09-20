# SessionStart: one line that says the kit is active, and a warning when the session is rooted in the
# wrong folder.
#
# The line is a heartbeat, not an alarm. If .claude/settings.json did not load, no hook runs at all - so
# the loud signal is the line NOT appearing. What this hook can see is the milder case: settings loaded,
# but the session is anchored below the git root, or at a git root with no .claude/paper.profile.json, so
# the profile-driven guards read nothing.
#
# It also writes the session stamp the Stop hooks (code-map-nag, plan-nag, verify-on-stop) measure "changed
# this session" against. Always exits 0.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

$ErrorActionPreference = 'Stop'
try {
    . (Join-Path $PSScriptRoot 'hook-input.ps1')
    . (Join-Path $PSScriptRoot 'session-state.ps1')
    . (Join-Path $PSScriptRoot 'kit-version-plan.ps1')

    # Where the vendored kit says it came from, and where the kit itself now is. All three readers
    # return '' rather than throwing: a machine may have no lock, no marketplace registry, or a
    # marketplace installed from github rather than a directory.
    function Get-PaperVendoredKitVersion([string] $root) {
        try {
            $lock = Join-Path (Join-Path $root '.claude') 'paper-kit.lock.json'
            if (-not (Test-Path -LiteralPath $lock)) { return '' }
            return [string] ((Get-Content -LiteralPath $lock -Raw | ConvertFrom-Json).version)
        }
        catch { return '' }
    }

    function Get-PaperMarketplaceKitPath {
        try {
            $registry = Join-Path $env:USERPROFILE '.claude\plugins\known_marketplaces.json'
            if (-not (Test-Path -LiteralPath $registry)) { return '' }
            $entry = (Get-Content -LiteralPath $registry -Raw | ConvertFrom-Json).'paper-skills'
            if ($null -eq $entry) { return '' }
            return [string] $entry.installLocation
        }
        catch { return '' }
    }

    function Get-PaperMarketplaceKitVersion {
        try {
            $market = Get-PaperMarketplaceKitPath
            if (-not $market) { return '' }
            $manifest = Join-Path $market 'paper-kit\.claude-plugin\plugin.json'
            if (-not (Test-Path -LiteralPath $manifest)) { return '' }
            return [string] ((Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).version)
        }
        catch { return '' }
    }

    $payload = Read-PaperHookPayload
    if ($null -ne $payload) { [void] (Get-PaperSessionStart ([string] $payload.session_id) -Create) }

    $cwd = $null
    if ($null -ne $payload -and $payload.cwd -and (Test-Path -LiteralPath ([string] $payload.cwd) -PathType Container)) { $cwd = [string] $payload.cwd }
    if (-not $cwd) { $cwd = Get-PaperHookProjectDir $null }
    if (-not $cwd) { exit 0 }
    $cwd = [System.IO.Path]::GetFullPath($cwd).TrimEnd('\', '/')

    # The git root: the nearest folder holding .git (a folder, or a file in a worktree).
    $gitRoot = $null
    $dir = $cwd
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) { $gitRoot = $dir; break }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }

    $anchor = if ($gitRoot) { $gitRoot } else { $cwd }
    $profileMap = Read-PaperProfile $anchor
    $name = Split-Path -Leaf $anchor
    $hosts = if ($null -ne $profileMap) { (@($profileMap['hosts']) | Where-Object { $_ }) -join ', ' } else { 'none' }

    if (-not $gitRoot -or $gitRoot -ne $cwd -or $null -eq $profileMap) {
        $why = if (-not $gitRoot) { "$cwd is not inside a git repository" }
               elseif ($gitRoot -ne $cwd) { "the session is rooted at $cwd, below the git root $gitRoot" }
               else { "the git root $gitRoot has no .claude/paper.profile.json" }
        Write-PaperHookText @"
paper-kit: WRONG ANCHOR - $why.
The kit's guards read .claude/paper.profile.json at the project root; from here they may read nothing.
Restart Claude Code from the git root that holds .claude/paper.profile.json (or run /paper-kit:setup there).
"@
        exit 0
    }

    $line = "paper-kit active: $name, profile hosts: $hosts. (No such line at session start = the project settings did not load; restart from the repo root.)"

    # And whether the vendored copy has fallen behind the kit it came from. Read-only, best effort:
    # any of these files may be absent on a machine that installed the kit another way, and a session
    # start is not the place to fail over a missing file.
    $notice = Get-PaperKitVersionNotice (Get-PaperVendoredKitVersion $anchor) `
        (Get-PaperMarketplaceKitVersion) (Get-PaperMarketplaceKitPath)
    if ($notice.Message) { $line = $line + [Environment]::NewLine + $notice.Message }

    Write-PaperHookText $line
    exit 0
}
catch {
    exit 0
}
