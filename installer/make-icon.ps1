<#
.SYNOPSIS
  Draws the application icon (a blue rounded square, four white corner marks, a dot in the middle - the same picture the tray icon
  draws in code) at 16 to 256 pixels and writes it as src/Paper.ScreenWizzard.App/app.ico. Run it only when the picture changes; the
  .ico is committed.
#>
param([string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Paper.ScreenWizzard.App\app.ico'))

Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $scale = $size / 32.0
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 103, 192))
    $radius = 7 * $scale
    $x = 1 * $scale; $side = 30 * $scale; $d = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $x, $d, $d, 180, 90)
    $path.AddArc($x + $side - $d, $x, $d, $d, 270, 90)
    $path.AddArc($x + $side - $d, $x + $side - $d, $d, $d, 0, 90)
    $path.AddArc($x, $x + $side - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single](2.5 * $scale))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $near = 8 * $scale; $far = 24 * $scale; $arm = 6 * $scale
    function P($a, $b) { New-Object System.Drawing.PointF ([single]$a), ([single]$b) }
    $g.DrawLines($pen, @((P $near ($near + $arm)), (P $near $near), (P ($near + $arm) $near)))
    $g.DrawLines($pen, @((P ($far - $arm) $near), (P $far $near), (P $far ($near + $arm))))
    $g.DrawLines($pen, @((P $near ($far - $arm)), (P $near $far), (P ($near + $arm) $far)))
    $g.DrawLines($pen, @((P ($far - $arm) $far), (P $far $far), (P $far ($far - $arm))))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $g.FillEllipse($white, [single](13 * $scale), [single](13 * $scale), [single](6 * $scale), [single](6 * $scale))
    $g.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return , $stream.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { , (New-Frame $_) }

# ICONDIR (6 bytes) + one ICONDIRENTRY (16 bytes) per frame + the PNG data of each frame.
$icoStream = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $icoStream
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[System.IO.File]::WriteAllBytes($Out, $icoStream.ToArray())
Write-Host "wrote $Out ($($icoStream.Length) bytes, sizes: $($sizes -join ', '))"
