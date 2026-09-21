<#
.SYNOPSIS
  Draws the Paper Engineer application icon and writes it as src/Paper.ScreenWizzard.App/app.ico. Run it only when the picture
  changes; the .ico is committed and is the one source of the picture (the exe, the tray icon and the installer all use it).

.DESCRIPTION
  The picture: a blue rounded square, a white sheet of paper with a folded corner (Paper), a blueprint line and a small set-square
  on the sheet (Engineer), and four white corner marks around the sheet like a camera viewfinder (the screen capture). Drawn at
  256 x 256 and scaled down, so the small sizes keep the same shape; the fine details (the ruled lines) are left out under 48 pixels.
#>
param([string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Paper.ScreenWizzard.App\app.ico'))

Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $u = $size / 256.0
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    function P($x, $y) { New-Object System.Drawing.PointF ([single]($x * $u)), ([single]($y * $u)) }
    function Rounded($x, $y, $w, $h, $r) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $r * 2
        $path.AddArc(($x * $u), ($y * $u), ($d * $u), ($d * $u), 180, 90)
        $path.AddArc((($x + $w - $d) * $u), ($y * $u), ($d * $u), ($d * $u), 270, 90)
        $path.AddArc((($x + $w - $d) * $u), (($y + $h - $d) * $u), ($d * $u), ($d * $u), 0, 90)
        $path.AddArc(($x * $u), (($y + $h - $d) * $u), ($d * $u), ($d * $u), 90, 90)
        $path.CloseFigure()
        return $path
    }

    # the tile: a blue that goes a little darker downward
    $tile = Rounded 8 8 240 240 52
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((P 0 8), (P 0 248), ([System.Drawing.Color]::FromArgb(255, 20, 122, 214)), ([System.Drawing.Color]::FromArgb(255, 0, 84, 166)))
    $g.FillPath($gradient, $tile)

    # the sheet of paper, its top-right corner folded
    $sheet = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sheet.AddPolygon(@((P 70 52), (P 158 52), (P 190 84), (P 190 204), (P 70 204)))
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 0, 40, 100))
    $g.TranslateTransform([single](5 * $u), [single](6 * $u))
    $g.FillPath($shadow, $sheet)
    $g.ResetTransform()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))), $sheet)
    $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fold.AddPolygon(@((P 158 52), (P 158 84), (P 190 84)))
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 176, 208, 240))), $fold)

    $ink = [System.Drawing.Color]::FromArgb(255, 0, 84, 166)
    if ($size -ge 48) {
        # blueprint: ruled lines and a set-square (the engineer's triangle) on the sheet
        $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 150, 190, 232)), ([single](5 * $u))
        $line.StartCap = 'Round'; $line.EndCap = 'Round'
        $g.DrawLine($line, (P 88 108), (P 140 108))
        $g.DrawLine($line, (P 88 128), (P 172 128))
        $square = New-Object System.Drawing.Drawing2D.GraphicsPath
        $square.AddPolygon(@((P 92 188), (P 92 142), (P 138 188)))
        $square.AddPolygon(@((P 104 176), (P 104 158), (P 122 176)))
        $g.FillPath((New-Object System.Drawing.SolidBrush $ink), $square)
    }
    else {
        $g.FillPolygon((New-Object System.Drawing.SolidBrush $ink), @((P 84 196), (P 84 128), (P 152 196)))
    }

    # the viewfinder: four white corner marks around the sheet
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([single]([Math]::Max(1.6, 13 * $u)))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $near = 34; $far = 222; $arm = 34
    $g.DrawLines($pen, @((P $near ($near + $arm)), (P $near $near), (P ($near + $arm) $near)))
    $g.DrawLines($pen, @((P ($far - $arm) $near), (P $far $near), (P $far ($near + $arm))))
    $g.DrawLines($pen, @((P $near ($far - $arm)), (P $near $far), (P ($near + $arm) $far)))
    $g.DrawLines($pen, @((P ($far - $arm) $far), (P $far $far), (P $far ($far - $arm))))
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

# A preview to look at: the 256 and 32 pixel frames side by side, not committed.
$preview = Join-Path ([IO.Path]::GetTempPath()) 'paper-engineer-icon-preview.png'
$sheetBitmap = New-Object System.Drawing.Bitmap 420, 280, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$pg = [System.Drawing.Graphics]::FromImage($sheetBitmap)
$pg.Clear([System.Drawing.Color]::FromArgb(255, 240, 240, 240))
$big = [System.Drawing.Image]::FromStream((New-Object System.IO.MemoryStream (, $frames[6])))
$pg.DrawImage($big, 10, 10, 256, 256)
foreach ($pair in @(@(1, 290, 20), @(2, 290, 60), @(3, 290, 110), @(4, 290, 170))) {
    $img = [System.Drawing.Image]::FromStream((New-Object System.IO.MemoryStream (, $frames[$pair[0]])))
    $pg.DrawImage($img, $pair[1], $pair[2], $img.Width, $img.Height)
}
$pg.Dispose()
$sheetBitmap.Save($preview)
Write-Host "preview: $preview"

# The small picture in the header of the installer wizard (Inno wants a BMP, here 110 x 110 for a screen at 200%; white behind the corners).
$wizardSmall = Join-Path $PSScriptRoot 'wizard-small.bmp'
$logo = [System.Drawing.Image]::FromStream((New-Object System.IO.MemoryStream (, $frames[6])))
$small = New-Object System.Drawing.Bitmap 110, 110, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$sg = [System.Drawing.Graphics]::FromImage($small)
$sg.Clear([System.Drawing.Color]::White)
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.DrawImage($logo, 0, 0, 110, 110)
$sg.Dispose()
$small.Save($wizardSmall, [System.Drawing.Imaging.ImageFormat]::Bmp)
Write-Host "wrote $wizardSmall"
