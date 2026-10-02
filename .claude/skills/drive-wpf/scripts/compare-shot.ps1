# Compares a window's shot with its recorded baseline, pixel by pixel after decoding both PNGs to 32bpp ARGB
# (alpha included). See ../references/ui-shots.md.
#   compare-shot.ps1 -Actual <png> -Recorded <baseline png>
#   PAPER_UI_BASELINE_RECORD=1  copy -Actual over -Recorded (making its folder) and exit 3 - a recording run
#                               is never a pass; the evidence is the next run without the variable
# Exit: 0 same | 1 baseline missing, other size, or a different pixel | 2 -Actual missing or unreadable | 3 recorded.
# ASCII only, PowerShell 5.1: a .ps1 without a BOM is read as ANSI.
param(
    [Parameter(Mandatory = $true)] [string] $Actual,
    [Parameter(Mandatory = $true)] [string] $Recorded
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shot-baseline-plan.ps1')
Add-Type -AssemblyName System.Drawing

# @{ Width; Height; Pixels } of a PNG, redrawn to Format32bppArgb so both sides are read the same way.
function Read-Shot([string] $Path) {
    $src = New-Object System.Drawing.Bitmap $Path
    try {
        $w = $src.Width; $h = $src.Height
        $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $w, $h))
            $g.Dispose()
            $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
            $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $px = New-Object byte[] ($w * $h * 4)
                for ($y = 0; $y -lt $h; $y++) {
                    [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + [int64]$y * $data.Stride), $px, $y * $w * 4, $w * 4)
                }
            }
            finally { $bmp.UnlockBits($data) }
            return @{ Width = $w; Height = $h; Pixels = $px }
        }
        finally { $bmp.Dispose() }
    }
    finally { $src.Dispose() }
}

if (-not (Test-Path -LiteralPath $Actual -PathType Leaf)) {
    Write-Output "actual shot $Actual is not there"
    exit 2
}
if (Test-ShotRecording -Value $env:PAPER_UI_BASELINE_RECORD) {
    $dir = Split-Path $Recorded -Parent
    if ($dir) { [void][System.IO.Directory]::CreateDirectory($dir) }
    Copy-Item -LiteralPath $Actual -Destination $Recorded -Force
    $v = Get-ShotBaselineVerdict -Recording $true -RecordedPath $Recorded -ActualPath $Actual -Recorded $null -Actual $null
    Write-Output $v.Message
    exit 3
}
try { $act = Read-Shot $Actual }
catch {
    Write-Output "actual shot $Actual cannot be read: $($_.Exception.Message)"
    exit 2
}
$rec = $null
if (Test-Path -LiteralPath $Recorded -PathType Leaf) { $rec = Read-Shot $Recorded }
$v = Get-ShotBaselineVerdict -Recording $false -RecordedPath $Recorded -ActualPath $Actual -Recorded $rec -Actual $act
Write-Output $v.Message
if ($v.Outcome -eq 'same') { exit 0 }
exit 1
