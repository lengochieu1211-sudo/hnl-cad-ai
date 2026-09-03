param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class HnlNativeIcon {
  [DllImport("user32.dll")]
  public static extern bool DestroyIcon(IntPtr handle);
}
"@

$out = Join-Path $Root 'artifacts\installer-assets'
New-Item -ItemType Directory -Force -Path $out | Out-Null

function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $radius * 2
  $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
  $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
  $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
  $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

function Draw-HnlLogo([System.Drawing.Graphics]$g, [float]$x, [float]$y, [float]$w, [float]$h) {
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $blue = [System.Drawing.Color]::FromArgb(14,165,233)
  $white = [System.Drawing.Color]::White
  $bgBrush = New-Object System.Drawing.SolidBrush($blue)
  $stroke = New-Object System.Drawing.Pen($white, [Math]::Max(2.0, $w * 0.065))
  $stroke.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
  $stroke.EndCap = [System.Drawing.Drawing2D.LineCap]::Square

  $path = New-RoundedPath (New-Object System.Drawing.RectangleF($x,$y,$w,$h)) ([Math]::Min($w,$h) * 0.16)
  $g.FillPath($bgBrush, $path)

  $sx = $w / 108.0
  $sy = $h / 68.0
  function P([double]$px,[double]$py) { New-Object System.Drawing.PointF(($x + $px*$sx), ($y + $py*$sy)) }

  # H
  $g.DrawLine($stroke, (P 17.5 20), (P 17.5 48))
  $g.DrawLine($stroke, (P 37.5 20), (P 37.5 48))
  $g.DrawLine($stroke, (P 18 34), (P 37 34))
  # N
  $g.DrawLine($stroke, (P 52.5 20), (P 52.5 48))
  $g.DrawLine($stroke, (P 73.5 20), (P 73.5 48))
  $g.DrawLine($stroke, (P 54 21), (P 72 47))
  # L
  $g.DrawLine($stroke, (P 86.5 20), (P 86.5 48))
  $g.DrawLine($stroke, (P 87 48), (P 103 48))

  $stroke.Dispose(); $bgBrush.Dispose(); $path.Dispose()
}

# Setup icon
$bmp = New-Object System.Drawing.Bitmap(256,256)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
Draw-HnlLogo $g 18 47 220 139
$g.Dispose()
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$iconPath = Join-Path $out 'HNL-VXT.ico'
$fs = [System.IO.File]::Create($iconPath)
$icon.Save($fs)
$fs.Dispose(); $icon.Dispose(); [HnlNativeIcon]::DestroyIcon($hIcon) | Out-Null; $bmp.Dispose()

# Small wizard logo
$small = New-Object System.Drawing.Bitmap(64,64)
$gs = [System.Drawing.Graphics]::FromImage($small)
$gs.Clear([System.Drawing.Color]::White)
Draw-HnlLogo $gs 4 13 56 35
$gs.Dispose()
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'
$small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$small.Dispose()

Write-Host "HNL installer branding generated:"
Write-Host "  $iconPath"
Write-Host "  $smallPath"
