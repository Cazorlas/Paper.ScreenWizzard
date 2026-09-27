# The decision half of render-shapes.ps1, with no file system, no browser and no process: the window one
# drawing is rendered in, and the size a PNG's header states. The script reads the bytes and hands them
# here. Dot-source it; tests/render-shapes.tests.ps1 covers it.
#
# THIS FILE STAYS ASCII (PowerShell 5.1 reads a .ps1 without a BOM in the ANSI codepage).

# CSS pixels per unit, 96 px per inch - the unit a browser window is sized in.
$script:PaperSvgPxPerUnit = @{ '' = 1.0; 'px' = 1.0; 'mm' = 96 / 25.4; 'cm' = 96 / 2.54; 'in' = 96.0; 'pt' = 96 / 72; 'pc' = 16.0 }

function ConvertTo-PaperSvgPx([string] $Value) {
    <#
    .SYNOPSIS
    An SVG length attribute in CSS pixels, or $null for a percentage, an unknown unit or nothing.
    .DESCRIPTION
    A percentage is relative to a window that does not exist yet, so it gives no size and the caller falls
    back to the viewBox.
    #>
    if (-not $Value) { return $null }
    $m = [regex]::Match($Value.Trim(), '^([0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)\s*([a-zA-Z]*)$')
    if (-not $m.Success) { return $null }
    $unit = $m.Groups[2].Value.ToLowerInvariant()
    if (-not $script:PaperSvgPxPerUnit.ContainsKey($unit)) { return $null }
    return [double]::Parse($m.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) * $script:PaperSvgPxPerUnit[$unit]
}

function Get-PaperSvgWindow {
    <#
    .SYNOPSIS
    The browser window for one SVG: Width and Height in whole CSS pixels, or a Reason when it has no size.
    .DESCRIPTION
    Its width and height when both are given, rounded up so no edge is cut off. Else its viewBox, enlarged so
    the long side is at least 800 px: an SVG with only a viewBox fills the window, so any window of the same
    proportions shows all of it, and a bigger one gives the labels more pixels.
    Bytes, not text: the XML reader then detects the encoding from the BOM or the declaration the way it
    would reading the file. A DOCTYPE is ignored, never fetched - rendering must not reach the network.
    #>
    param([byte[]] $Bytes)

    function New-Window([int] $w, [int] $h, [string] $reason) { return [pscustomobject]@{ Width = $w; Height = $h; Reason = $reason } }
    try {
        $settings = New-Object System.Xml.XmlReaderSettings
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
        $settings.XmlResolver = $null
        $stream = New-Object System.IO.MemoryStream (, [byte[]] @($Bytes))
        $reader = [System.Xml.XmlReader]::Create($stream, $settings)
        try {
            $doc = New-Object System.Xml.XmlDocument
            $doc.Load($reader)
        }
        finally { $reader.Dispose(); $stream.Dispose() }
        $svg = $doc.DocumentElement
        if ($svg.LocalName -ne 'svg') { return New-Window 0 0 "the root element is <$($svg.LocalName)>, not <svg>" }

        $w = ConvertTo-PaperSvgPx $svg.GetAttribute('width')
        $h = ConvertTo-PaperSvgPx $svg.GetAttribute('height')
        if ($w -and $h) { return New-Window ([math]::Ceiling($w)) ([math]::Ceiling($h)) '' }

        $box = @($svg.GetAttribute('viewBox') -split '[\s,]+' | Where-Object { $_ })
        if ($box.Count -ne 4) { return New-Window 0 0 'no width and height, and no viewBox' }
        $inv = [Globalization.CultureInfo]::InvariantCulture
        $bw = [double]::Parse($box[2], $inv)
        $bh = [double]::Parse($box[3], $inv)
        if ($bw -le 0 -or $bh -le 0) { return New-Window 0 0 "viewBox '$($svg.GetAttribute('viewBox'))' has no area" }
        $grow = [math]::Max(1.0, 800 / [math]::Max($bw, $bh))
        return New-Window ([math]::Ceiling($bw * $grow)) ([math]::Ceiling($bh * $grow)) ''
    }
    catch {
        # A file that is not XML, or a viewBox number that does not parse: a reason for this one drawing,
        # never a throw that stops the others from rendering.
        return New-Window 0 0 "not readable as XML: $($_.Exception.Message)"
    }
}

function Get-PaperPngSize([byte[]] $Bytes) {
    <#
    .SYNOPSIS
    Width and Height of a PNG from its header, or $null when the bytes are not a PNG.
    .DESCRIPTION
    How the script knows the browser really wrote a picture: a browser that failed may exit 0 and leave an
    empty or partial file.
    #>
    if ($null -eq $Bytes -or $Bytes.Length -lt 24 -or $Bytes[0] -ne 137 -or $Bytes[1] -ne 80 -or $Bytes[2] -ne 78 -or $Bytes[3] -ne 71) { return $null }
    # Each byte widened to int first: a byte shifted left stays a byte in PowerShell and loses its bits.
    $w = ([int] $Bytes[16] -shl 24) -bor ([int] $Bytes[17] -shl 16) -bor ([int] $Bytes[18] -shl 8) -bor [int] $Bytes[19]
    $h = ([int] $Bytes[20] -shl 24) -bor ([int] $Bytes[21] -shl 16) -bor ([int] $Bytes[22] -shl 8) -bor [int] $Bytes[23]
    return [pscustomobject]@{ Width = $w; Height = $h }
}
