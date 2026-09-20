# Pure half of the kit-version line in session-anchor.ps1: no disk, no registry, no clock.
#
# A project carries a vendored copy of the kit and records its version in .claude/paper-kit.lock.json.
# The kit itself moves on. Nothing used to compare the two, so a machine could - and on 2026-09-20 did -
# hold the source at 1.8.0, the installed plugin at 1.5.0, and two projects vendored at 1.7.0, with
# nothing anywhere saying so.
#
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

Set-StrictMode -Version Latest

<#
.SYNOPSIS
    Compares two dotted versions.
.OUTPUTS
    -1, 0 or 1. Anything unparseable sorts as 0 so a malformed file never raises a false alarm.
#>
function Compare-PaperVersion {
    param([string] $Left, [string] $Right)

    if ([string]::IsNullOrWhiteSpace($Left) -or [string]::IsNullOrWhiteSpace($Right)) { return 0 }

    # Numeric compare, part by part. A string compare would call 1.10.0 older than 1.9.0.
    $leftParts = @($Left.Trim() -split '\.')
    $rightParts = @($Right.Trim() -split '\.')
    $count = [Math]::Max($leftParts.Count, $rightParts.Count)

    for ($i = 0; $i -lt $count; $i++) {
        $l = 0
        $r = 0
        if ($i -lt $leftParts.Count -and -not [int]::TryParse($leftParts[$i], [ref] $l)) { return 0 }
        if ($i -lt $rightParts.Count -and -not [int]::TryParse($rightParts[$i], [ref] $r)) { return 0 }
        if ($l -lt $r) { return -1 }
        if ($l -gt $r) { return 1 }
    }

    return 0
}

<#
.SYNOPSIS
    What to tell the reader at session start about the vendored kit.
.PARAMETER Vendored
    The version in this project's .claude/paper-kit.lock.json, or empty when the project has no lock.
.PARAMETER Source
    The version in the marketplace's paper-kit/.claude-plugin/plugin.json, or empty when it cannot be read.
.OUTPUTS
    Status: current | behind | ahead | unknown, and the one line to print (empty when there is nothing
    worth saying - silence is the normal case and a heartbeat that fires every session gets ignored).
#>
function Get-PaperKitVersionNotice {
    param(
        [string] $Vendored,
        [string] $Source,
        [string] $SourcePath = ''
    )

    if ([string]::IsNullOrWhiteSpace($Vendored) -or [string]::IsNullOrWhiteSpace($Source)) {
        return [pscustomobject]@{ Status = 'unknown'; Message = '' }
    }

    $order = Compare-PaperVersion $Vendored $Source

    if ($order -lt 0) {
        $where = if ([string]::IsNullOrWhiteSpace($SourcePath)) { 'the marketplace' } else { $SourcePath }
        return [pscustomobject]@{
            Status  = 'behind'
            Message = "paper-kit: this project is vendored at $Vendored, the kit at $where is $Source. " `
                    + "Run /paper-kit:setup to take the newer one, or say once that you are staying on $Vendored."
        }
    }

    if ($order -gt 0) {
        # Vendored ahead of source is not a version to chase: somebody edited the payload inside the
        # project, which the kit forbids - every locked file is edited in Paper-skills, never here.
        return [pscustomobject]@{
            Status  = 'ahead'
            Message = "paper-kit: this project is vendored at $Vendored but the kit is only $Source. " `
                    + "A project never gets ahead by itself - check whether a locked file was edited here " `
                    + "instead of in Paper-skills."
        }
    }

    return [pscustomobject]@{ Status = 'current'; Message = '' }
}
