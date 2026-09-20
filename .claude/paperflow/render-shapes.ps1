# Render each drawing a spec links: every shapes\*.svg under a feature folder (or the SVG files given) to a PNG
# beside it, through a headless browser, with the window sized to the drawing.
#
#   render-shapes.ps1 -Path <feature folder | shapes folder | file.svg> [...] [-BrowserPath <exe>] [-Scale 2]
#
# The SVG is the source and stays; the PNG is what a markdown viewer can show. Render again after every edit
# to the SVG and open the PNG before committing: a label over an outline or a caption cut off at the edge is
# only visible in the picture. check-spec reports an SVG newer than its PNG.
#
# Window size, in CSS pixels: the SVG's width and height when both are given (mm, cm, in, pt, pc or px,
# 96 px per inch), else its viewBox, enlarged so the long side is at least 800 px - an SVG with only a
# viewBox fills the window, so any window of the same proportions shows all of it. -Scale multiplies the
# pixels of the PNG, not the drawing.
#
# The browser: -BrowserPath when given, used as is. Otherwise msedge.exe, then chrome.exe, under Program
# Files (x86), Program Files and the user's local application data (-SearchRoots replaces those folders).
# Each render runs with a throwaway browser profile, so an open browser window is neither used nor touched,
# and writes to a temporary file first, so a failed render leaves the old PNG in place.
#
# Exit: 0 every drawing rendered | 1 a render failed | 2 bad arguments | 4 no browser: not verifiable
[CmdletBinding()]
param(
    [Parameter(Position = 0)][string[]] $Path,
    [string] $BrowserPath,
    [string[]] $SearchRoots,
    [ValidateRange(1, 4)][int] $Scale = 2,
    [int] $TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { }

function Say([string] $line) { [Console]::Out.WriteLine($line) }
function Fail([int] $code, [string] $line) {
    if ($code -eq 4) { Say $line } else { [Console]::Error.WriteLine($line) }
    exit $code
}

# ---------------------------------------------------------------- arguments

if (-not $Path -or $Path.Count -eq 0) {
    Fail 2 'render-shapes: -Path is required - a feature folder, a shapes folder or .svg files'
}

$svgs = New-Object System.Collections.Generic.List[string]
foreach ($p in $Path) {
    if (-not (Test-Path -LiteralPath $p)) { Fail 2 "render-shapes: no such file or folder: $p" }
    $item = Get-Item -LiteralPath $p
    if ($item.PSIsContainer) {
        $found = @(Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Filter '*.svg' |
            Where-Object { $_.Directory.Name -eq 'shapes' } | Sort-Object FullName)
        if ($found.Count -eq 0) { Fail 2 "render-shapes: no shapes\*.svg under $($item.FullName)" }
        foreach ($f in $found) { if (-not $svgs.Contains($f.FullName)) { $svgs.Add($f.FullName) } }
    }
    elseif ($item.Extension -ieq '.svg') {
        if (-not $svgs.Contains($item.FullName)) { $svgs.Add($item.FullName) }
    }
    else { Fail 2 "render-shapes: not an .svg file: $p" }
}

# ---------------------------------------------------------------- the browser

function Find-Browser {
    if ($BrowserPath) {
        if (Test-Path -LiteralPath $BrowserPath -PathType Leaf) { return (Resolve-Path -LiteralPath $BrowserPath).ProviderPath }
        return $null
    }
    $roots = $SearchRoots
    if (-not $roots) { $roots = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, $env:LOCALAPPDATA) }
    $roots = @($roots | Where-Object { $_ })
    foreach ($relative in @('Microsoft\Edge\Application\msedge.exe', 'Google\Chrome\Application\chrome.exe')) {
        foreach ($root in $roots) {
            $candidate = Join-Path $root $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

$browser = Find-Browser
if (-not $browser) {
    $where = if ($BrowserPath) { "no browser at $BrowserPath" } else { 'no msedge.exe or chrome.exe found' }
    Fail 4 "render-shapes: not verifiable - $where; pass -BrowserPath to a Chromium browser. Nothing was written."
}

# ---------------------------------------------------------------- sizing

$PxPerUnit = @{ '' = 1.0; 'px' = 1.0; 'mm' = 96 / 25.4; 'cm' = 96 / 2.54; 'in' = 96.0; 'pt' = 96 / 72; 'pc' = 16.0 }

# A length attribute in CSS pixels, or $null for a percentage, an unknown unit or nothing.
function ConvertTo-Px([string] $value) {
    if (-not $value) { return $null }
    $m = [regex]::Match($value.Trim(), '^([0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)\s*([a-zA-Z]*)$')
    if (-not $m.Success) { return $null }
    $unit = $m.Groups[2].Value.ToLowerInvariant()
    if (-not $PxPerUnit.ContainsKey($unit)) { return $null }
    return [double]::Parse($m.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) * $PxPerUnit[$unit]
}

# The window for one SVG, as @(width, height) in whole CSS pixels, or a reason string when it has no size.
function Get-SvgWindow([string] $file) {
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($file, $settings)
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.Load($reader)
    }
    finally { $reader.Dispose() }
    $svg = $doc.DocumentElement
    if ($svg.LocalName -ne 'svg') { return "the root element is <$($svg.LocalName)>, not <svg>" }

    $w = ConvertTo-Px $svg.GetAttribute('width')
    $h = ConvertTo-Px $svg.GetAttribute('height')
    if ($w -and $h) { return @([int][math]::Ceiling($w), [int][math]::Ceiling($h)) }

    $box = @($svg.GetAttribute('viewBox') -split '[\s,]+' | Where-Object { $_ })
    if ($box.Count -ne 4) { return 'no width and height, and no viewBox' }
    $inv = [Globalization.CultureInfo]::InvariantCulture
    $bw = [double]::Parse($box[2], $inv)
    $bh = [double]::Parse($box[3], $inv)
    if ($bw -le 0 -or $bh -le 0) { return "viewBox '$($svg.GetAttribute('viewBox'))' has no area" }
    $grow = [math]::Max(1.0, 800 / [math]::Max($bw, $bh))
    return @([int][math]::Ceiling($bw * $grow), [int][math]::Ceiling($bh * $grow))
}

# Width and height of a PNG from its header, or $null when the file is not a PNG.
function Get-PngSize([string] $file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    if ($bytes.Length -lt 24 -or $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) { return $null }
    # Each byte widened to int first: a byte shifted left stays a byte in PowerShell and loses its bits.
    $w = ([int] $bytes[16] -shl 24) -bor ([int] $bytes[17] -shl 16) -bor ([int] $bytes[18] -shl 8) -bor [int] $bytes[19]
    $h = ([int] $bytes[20] -shl 24) -bor ([int] $bytes[21] -shl 16) -bor ([int] $bytes[22] -shl 8) -bor [int] $bytes[23]
    return @($w, $h)
}

# ---------------------------------------------------------------- rendering

$failed = 0
foreach ($svg in $svgs) {
    $png = [IO.Path]::ChangeExtension($svg, '.png')
    try { $window = Get-SvgWindow $svg }
    catch { $window = "not readable as XML: $($_.Exception.Message)" }
    if ($window -is [string]) {
        [Console]::Error.WriteLine("  FAIL  $svg - $window")
        $failed++
        continue
    }

    $work = Join-Path ([IO.Path]::GetTempPath()) ('render-shapes-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    $shot = Join-Path $work 'shot.png'
    try {
        $url = ([Uri] $svg).AbsoluteUri
        $arguments = @(
            '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--no-default-browser-check',
            '--disable-extensions', "--user-data-dir=`"$work\profile`"", "--force-device-scale-factor=$Scale",
            "--window-size=$($window[0]),$($window[1])", "--screenshot=`"$shot`"", "`"$url`""
        ) -join ' '
        $psi = New-Object System.Diagnostics.ProcessStartInfo $browser, $arguments
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $process = [System.Diagnostics.Process]::Start($psi)
        $errText = $process.StandardError.ReadToEndAsync()
        [void] $process.StandardOutput.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            [Console]::Error.WriteLine("  FAIL  $svg - the browser did not finish in $TimeoutSeconds s")
            $failed++
            continue
        }
        $size = if (Test-Path -LiteralPath $shot) { Get-PngSize $shot } else { $null }
        if (-not $size) {
            $detail = ($errText.Result -split "`r?`n" | Where-Object { $_ } | Select-Object -Last 2) -join ' / '
            [Console]::Error.WriteLine("  FAIL  $svg - no PNG written (browser exit $($process.ExitCode)) $detail")
            $failed++
            continue
        }
        Move-Item -LiteralPath $shot -Destination $png -Force
        Say ("  ok    {0} -> {1} ({2} x {3} px, window {4} x {5} at scale {6})" -f $svg, [IO.Path]::GetFileName($png), $size[0], $size[1], $window[0], $window[1], $Scale)
    }
    finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Say ("render-shapes: {0} drawing(s), {1} rendered, {2} failed" -f $svgs.Count, ($svgs.Count - $failed), $failed)
if ($failed -gt 0) { exit 1 }
exit 0
