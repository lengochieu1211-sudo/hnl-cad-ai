param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

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

function New-HnlPngFrame {
  param(
    [System.Drawing.Image]$Source,
    [int]$Size
  )

  $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Preserve the supplied square HNL artwork and leave a small safety margin so
    # Windows never clips the rounded outside edge at small icon sizes.
    $margin = [Math]::Max(1, [int][Math]::Round($Size * 0.035))
    $draw = $Size - (2 * $margin)
    $g.DrawImage($Source, $margin, $margin, $draw, $draw)
  }
  finally { $g.Dispose() }

  $ms = [System.IO.MemoryStream]::new()
  try {
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
  }
  finally {
    $ms.Dispose()
    $bitmap.Dispose()
  }
}

function Write-HnlMultiSizeIco {
  param(
    [System.Drawing.Image]$Source,
    [string]$Path
  )

  $sizes = @(16, 24, 32, 48, 64, 128, 256)
  $frames = @()
  foreach ($size in $sizes) {
    $frames += ,([PSCustomObject]@{
      Size = $size
      Data = (New-HnlPngFrame -Source $Source -Size $size)
    })
  }

  $fs = [System.IO.File]::Create($Path)
  $bw = [System.IO.BinaryWriter]::new($fs)
  try {
    # ICONDIR
    $bw.Write([UInt16]0)                 # reserved
    $bw.Write([UInt16]1)                 # type = icon
    $bw.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
      $wh = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
      $bw.Write($wh)                     # width
      $bw.Write($wh)                     # height
      $bw.Write([byte]0)                 # color count
      $bw.Write([byte]0)                 # reserved
      $bw.Write([UInt16]1)               # planes
      $bw.Write([UInt16]32)              # bits per pixel
      $bw.Write([UInt32]$frame.Data.Length)
      $bw.Write([UInt32]$offset)
      $offset += $frame.Data.Length
    }

    foreach ($frame in $frames) {
      $bw.Write([byte[]]$frame.Data)
    }
  }
  finally {
    $bw.Dispose()
    $fs.Dispose()
  }
}

$source = [System.Drawing.Image]::FromFile($officialPng)
try {
  Write-HnlMultiSizeIco -Source $source -Path $iconPath

  # Inno wizard compact branding image.
  $small = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $gs = [System.Drawing.Graphics]::FromImage($small)
  try {
    $gs.Clear([System.Drawing.Color]::White)
    $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gs.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gs.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gs.DrawImage($source, 3, 3, 58, 58)
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

# Validate ICO header + expected image count so CI cannot silently regress to a single legacy frame.
$icoBytes = [IO.File]::ReadAllBytes($iconPath)
if ($icoBytes.Length -lt 128) { throw "Generated HNL icon is unexpectedly small." }
$count = [BitConverter]::ToUInt16($icoBytes, 4)
if ($count -ne 7) { throw "Generated HNL icon must contain 7 sizes; found $count." }

Write-Host 'HNL official branding generated:'
Write-Host "  PNG: $officialPng"
Write-Host "  ICO: $iconPath (16/24/32/48/64/128/256)"
Write-Host "  BMP: $smallPath"
