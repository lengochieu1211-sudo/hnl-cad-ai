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

function New-RoundedPath {
  param(
    [System.Drawing.RectangleF]$Rect,
    [float]$Radius
  )

  $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $d = $Radius * 2.0
  $path.AddArc($Rect.X, $Rect.Y, $d, $d, 180, 90)
  $path.AddArc($Rect.Right - $d, $Rect.Y, $d, $d, 270, 90)
  $path.AddArc($Rect.Right - $d, $Rect.Bottom - $d, $d, $d, 0, 90)
  $path.AddArc($Rect.X, $Rect.Bottom - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

function Draw-HnlLogo {
  param(
    [System.Drawing.Graphics]$Graphics,
    [float]$X,
    [float]$Y,
    [float]$Width,
    [float]$Height
  )

  $Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $blue = [System.Drawing.Color]::FromArgb(14, 165, 233)
  $bg = [System.Drawing.SolidBrush]::new($blue)
  $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [single][Math]::Max(2.0, $Width * 0.065))
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Square
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Square
  $rect = [System.Drawing.RectangleF]::new($X, $Y, $Width, $Height)
  $path = New-RoundedPath -Rect $rect -Radius ([single]([Math]::Min($Width, $Height) * 0.16))

  try {
    $Graphics.FillPath($bg, $path)
    $sx = $Width / 108.0
    $sy = $Height / 68.0

    function LX([double]$v) { return [single]($X + $v * $sx) }
    function LY([double]$v) { return [single]($Y + $v * $sy) }

    # H
    $Graphics.DrawLine($pen, (LX 17.5), (LY 20), (LX 17.5), (LY 48))
    $Graphics.DrawLine($pen, (LX 37.5), (LY 20), (LX 37.5), (LY 48))
    $Graphics.DrawLine($pen, (LX 18), (LY 34), (LX 37), (LY 34))
    # N
    $Graphics.DrawLine($pen, (LX 52.5), (LY 20), (LX 52.5), (LY 48))
    $Graphics.DrawLine($pen, (LX 73.5), (LY 20), (LX 73.5), (LY 48))
    $Graphics.DrawLine($pen, (LX 54), (LY 21), (LX 72), (LY 47))
    # L
    $Graphics.DrawLine($pen, (LX 86.5), (LY 20), (LX 86.5), (LY 48))
    $Graphics.DrawLine($pen, (LX 87), (LY 48), (LX 103), (LY 48))
  }
  finally {
    $path.Dispose()
    $pen.Dispose()
    $bg.Dispose()
  }
}

$iconPath = Join-Path $out 'HNL-VXT.ico'
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'

$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
try {
  $g.Clear([System.Drawing.Color]::Transparent)
  Draw-HnlLogo -Graphics $g -X 18 -Y 47 -Width 220 -Height 139
}
finally {
  $g.Dispose()
}

$hIcon = $bitmap.GetHicon()
try {
  $icon = [System.Drawing.Icon]::FromHandle($hIcon)
  $stream = [System.IO.File]::Create($iconPath)
  try { $icon.Save($stream) }
  finally { $stream.Dispose(); $icon.Dispose() }
}
finally {
  [HnlNativeIcon]::DestroyIcon($hIcon) | Out-Null
  $bitmap.Dispose()
}

$small = [System.Drawing.Bitmap]::new(64, 64)
$gs = [System.Drawing.Graphics]::FromImage($small)
try {
  $gs.Clear([System.Drawing.Color]::White)
  Draw-HnlLogo -Graphics $gs -X 4 -Y 13 -Width 56 -Height 35
}
finally {
  $gs.Dispose()
}
$small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$small.Dispose()

if (-not (Test-Path $iconPath)) { throw "Missing generated installer icon: $iconPath" }
if (-not (Test-Path $smallPath)) { throw "Missing generated wizard image: $smallPath" }

Write-Host 'HNL installer branding generated:'
Write-Host "  $iconPath"
Write-Host "  $smallPath"
