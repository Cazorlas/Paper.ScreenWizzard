# The pure half of comparing a window's shot with its recorded baseline: the baseline's name, whether this run
# records, and the verdict on two decoded images. No disk, no drawing - tests/ui-shots.tests.ps1 dot-sources
# this file. The I/O half is compare-shot.ps1 next to it.
# ASCII only: PowerShell 5.1 reads a .ps1 without a BOM as ANSI.

# One baseline per .NET framework: the two print the same number as different text.
function Get-ShotBaselineName {
    param([string] $Name, [string] $Framework)
    return "$Name-$Framework.png"
}

# Only the exact value 1 records.
function Test-ShotRecording {
    param([string] $Value)
    return ($Value -eq '1')
}

function Format-ShotColor {
    param([byte[]] $Pixels, [int] $Offset)
    return ('#{0:X2}{1:X2}{2:X2}{3:X2}' -f $Pixels[$Offset + 3], $Pixels[$Offset + 2], $Pixels[$Offset + 1], $Pixels[$Offset])
}

# -Recorded / -Actual: @{ Width; Height; Pixels } with Pixels as BGRA bytes row by row (Recorded = $null when
# the baseline file is not there). Returns @{ Outcome = recorded / missing / size / pixel / same; X; Y; Message }.
# A recording run is never a comparison, so it never reports same.
function Get-ShotBaselineVerdict {
    param([bool] $Recording, [string] $RecordedPath, [string] $ActualPath, $Recorded, $Actual)
    $leaf = [System.IO.Path]::GetFileName($RecordedPath)
    function Out([string] $Outcome, [int] $X, [int] $Y, [string] $Message) {
        return [pscustomobject]@{ Outcome = $Outcome; X = $X; Y = $Y; Message = $Message }
    }
    if ($Recording) {
        return Out 'recorded' -1 -1 "RECORDED $RecordedPath - this run is not a comparison; run again without PAPER_UI_BASELINE_RECORD"
    }
    if ($null -eq $Recorded) {
        return Out 'missing' -1 -1 "$RecordedPath is not recorded; run once with PAPER_UI_BASELINE_RECORD=1 on a build known to be right"
    }
    if ($Recorded.Width -ne $Actual.Width -or $Recorded.Height -ne $Actual.Height) {
        return Out 'size' -1 -1 ("{0}: recorded {1}x{2}, now {3}x{4}; compare {5} with {6}" -f $leaf, $Recorded.Width, $Recorded.Height, $Actual.Width, $Actual.Height, $ActualPath, $RecordedPath)
    }
    $r = [byte[]] $Recorded.Pixels
    $a = [byte[]] $Actual.Pixels
    $w = [int] $Actual.Width
    $count = [int] $Actual.Width * [int] $Actual.Height
    for ($p = 0; $p -lt $count; $p++) {
        $o = $p * 4
        if ($r[$o] -ne $a[$o] -or $r[$o + 1] -ne $a[$o + 1] -or $r[$o + 2] -ne $a[$o + 2] -or $r[$o + 3] -ne $a[$o + 3]) {
            $x = $p % $w
            $y = [int][math]::Floor($p / $w)
            return Out 'pixel' $x $y ("{0}: first different pixel at ({1}, {2}): recorded {3}, now {4}; compare {5} with {6}" -f $leaf, $x, $y, (Format-ShotColor $r $o), (Format-ShotColor $a $o), $ActualPath, $RecordedPath)
        }
    }
    return Out 'same' -1 -1 "${leaf}: same as recorded"
}
