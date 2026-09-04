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
$logoB64 = Join-Path $Root 'src\HNL.VXT.UI\Assets\HNL-Logo-Official.b64'
$officialPng = Join-Path $out 'HNL-Logo-Official.png'
$iconPath = Join-Path $out 'HNL-VXT.ico'
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'

if (-not (Test-Path $logoB64)) { throw "Missing official HNL logo asset: $logoB64" }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$base64 = (Get-Content $logoB64 -Raw).Trim()
$bytes = [Convert]::FromBase64String($base64)
[IO.File]::WriteAllBytes($officialPng, $bytes)

$source = [System.Drawing.Image]::FromFile($officialPng)
try {
  # Windows icon: preserve the exact official HNL artwork supplied by the user.
  $bitmap = [System.Drawing.Bitmap]::new(256, 256)
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($source, 0, 0, 256, 256)
  }
  finally { $g.Dispose() }

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

  # Inno wizard compact branding image.
  $small = [System.Drawing.Bitmap]::new(64, 64)
  $gs = [System.Drawing.Graphics]::FromImage($small)
  try {
    $gs.Clear([System.Drawing.Color]::White)
    $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gs.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gs.DrawImage($source, 2, 2, 60, 60)
  }
  finally { $gs.Dispose() }
  $small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $small.Dispose()
}
finally {
  $source.Dispose()
}

foreach ($required in @($officialPng, $iconPath, $smallPath)) {
  if (-not (Test-Path $required)) { throw "Missing generated HNL branding asset: $required" }
}

Write-Host 'HNL official branding generated:'
Write-Host "  $officialPng"
Write-Host "  $iconPath"
Write-Host "  $smallPath"
