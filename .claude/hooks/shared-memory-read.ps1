# Shared memory, the reading half: asks git where the main checkout is, lists the folders Claude Code keeps
# under its config dir, reads MEMORY.md, and hands all of it to the pure functions of
# shared-memory-plan.ps1. Used by session-anchor.ps1 (Codex only) and by paperflow.ps1 memory, so the two
# cannot print different things. Reads only: never writes a file, never creates a folder (F75). ADR-0032.
# Dot-sourced; declares no param() block.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

. (Join-Path $PSScriptRoot 'shared-memory-plan.ps1')

function Get-PaperSharedMemoryLocation([string] $ProjectDir) {
    <# Checkout (whose memory this is), Resolved (Resolve-PaperMemoryDir) and Exists (the folder is there). #>
    $common = ''; $top = ''
    # Not a repository, or no git: both stay empty and the project folder is the checkout. Continue, so a
    # caller's Stop does not turn git's stderr line into a throw.
    try {
        if (Get-Command git -CommandType Application -ErrorAction SilentlyContinue) {
            $ErrorActionPreference = 'Continue'
            $out = @(& git -C $ProjectDir rev-parse --path-format=absolute --git-common-dir --show-toplevel 2>$null)
            if ($LASTEXITCODE -eq 0 -and $out.Count -ge 2) { $common = ([string] $out[0]).Trim(); $top = ([string] $out[1]).Trim() }
        }
    }
    catch { $common = ''; $top = '' }

    $checkout = Get-PaperMemoryCheckout -GitCommonDir $common -TopLevel $top -ProjectDir $ProjectDir
    $slug = ConvertTo-PaperClaudeSlug $checkout
    $config = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE '.claude' }
    $projects = Join-Path $config 'projects'
    $names = @()
    if (Test-Path -LiteralPath $projects -PathType Container) {
        $names = @(Get-ChildItem -LiteralPath $projects -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    }
    $resolved = Resolve-PaperMemoryDir -ConfigDir $config -Slug $slug -ExistingNames $names
    $exists = [bool] ($resolved.Dir -and (Test-Path -LiteralPath $resolved.Dir -PathType Container))
    return [pscustomobject]@{ Checkout = $checkout; Resolved = $resolved; Exists = $exists }
}

function Get-PaperSharedMemoryText([string] $ProjectDir, [int] $MaxBytes = $script:PaperCodexContextBytes) {
    <# The block Codex is handed for the repository at ProjectDir, at most MaxBytes UTF-8 bytes (F80). #>
    $location = Get-PaperSharedMemoryLocation $ProjectDir
    $index = $null
    if ($location.Exists) {
        $indexPath = Join-Path $location.Resolved.Dir 'MEMORY.md'
        if (Test-Path -LiteralPath $indexPath -PathType Leaf) { $index = [IO.File]::ReadAllText($indexPath, [Text.Encoding]::UTF8) }
    }
    return Get-PaperCodexMemoryContext -Resolved $location.Resolved -FolderExists $location.Exists -IndexText $index -MaxBytes $MaxBytes
}
