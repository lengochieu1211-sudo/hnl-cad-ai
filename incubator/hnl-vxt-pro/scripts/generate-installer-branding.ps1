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
if ($bytes.Length -lt 8 -or
    $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47 -or
    $bytes[4] -ne 0x0D -or $bytes[5] -ne 0x0A -or $bytes[6] -ne 0x1A -or $bytes[7] -ne 0x0A) {
  throw 'HNL logo asset is not a valid PNG stream.'
}
[IO.File]::WriteAllBytes($officialPng, $bytes)

function New-HnlArgbSource {
  param([System.Drawing.Image]$InputImage)

  $normalized = [System.Drawing.Bitmap]::new(
    $InputImage.Width,
    $InputImage.Height,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($normalized)
  try {
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($InputImage, 0, 0, $InputImage.Width, $InputImage.Height)
  }
  finally { $g.Dispose() }
  return $normalized
}

function New-HnlPngFrame {
  param(
    [System.Drawing.Image]$Source,
    [int]$Size
  )

  $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # The official artwork already contains its own rounded-edge safety area.
    # Keep only a very small additional margin for Windows' smallest icon slots.
    $margin = if ($Size -le 32) { 1 } else { [Math]::Max(1, [int][Math]::Round($Size * 0.02)) }
    $draw = $Size - (2 * $margin)
    $g.DrawImage($Source, $margin, $margin, $draw, $draw)
  }
  finally { $g.Dispose() }

  $ms = [System.IO.MemoryStream]::new()
  try {
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return [byte[]]$ms.ToArray()
  }
  finally {
    $ms.Dispose()
    $bitmap.Dispose()
  }
}

function Write-HnlMultiSizeIco {
  param(
    [System.Drawing.Image]$Source,
    [byte[]]$OriginalPngBytes,
    [string]$Path
  )

  # Highest-resolution frame first. The 256px frame is the exact official PNG bytes,
  # avoiding any GDI+ resampling/indexed-palette corruption in Explorer/Inno resources.
  $sizes = @(256, 128, 64, 48, 32, 24, 16)
  $frames = @()
  foreach ($size in $sizes) {
    $frameData = if ($size -eq 256 -and $Source.Width -eq 256 -and $Source.Height -eq 256) {
      [byte[]]$OriginalPngBytes
    }
    else {
      [byte[]](New-HnlPngFrame -Source $Source -Size $size)
    }

    $frames += ,([PSCustomObject]@{
      Size = $size
      Data = $frameData
    })
  }

  $fs = [System.IO.File]::Create($Path)
  $bw = [System.IO.BinaryWriter]::new($fs)
  try {
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
      $wh = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
      $bw.Write($wh)
      $bw.Write($wh)
      $bw.Write([byte]0)
      $bw.Write([byte]0)
      $bw.Write([UInt16]1)
      $bw.Write([UInt16]32)
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

$loaded = [System.Drawing.Image]::FromFile($officialPng)
$source = $null
try {
  if ($loaded.Width -ne 256 -or $loaded.Height -ne 256) {
    throw "Official HNL logo must be 256x256 for installer branding; found $($loaded.Width)x$($loaded.Height)."
  }

  $source = New-HnlArgbSource -InputImage $loaded
  Write-HnlMultiSizeIco -Source $source -OriginalPngBytes $bytes -Path $iconPath

  $small = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $gs = [System.Drawing.Graphics]::FromImage($small)
  try {
    $gs.Clear([System.Drawing.Color]::White)
    $gs.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
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
  if ($source -ne $null) { $source.Dispose() }
  $loaded.Dispose()
}

foreach ($required in @($officialPng, $iconPath, $smallPath)) {
  if (-not (Test-Path $required)) { throw "Missing generated HNL branding asset: $required" }
}

$icoBytes = [IO.File]::ReadAllBytes($iconPath)
if ($icoBytes.Length -lt 128) { throw "Generated HNL icon is unexpectedly small." }
$count = [BitConverter]::ToUInt16($icoBytes, 4)
if ($count -ne 7) { throw "Generated HNL icon must contain 7 sizes; found $count." }
$firstWidth = $icoBytes[6]
if ($firstWidth -ne 0) { throw "Generated HNL icon must place the 256px frame first." }

# Validate that the first ICO payload is byte-for-byte the official 256px PNG.
$firstSize = [BitConverter]::ToUInt32($icoBytes, 14)
$firstOffset = [BitConverter]::ToUInt32($icoBytes, 18)
if ($firstSize -ne $bytes.Length) {
  throw "Generated 256px icon payload size mismatch: $firstSize vs $($bytes.Length)."
}
for ($i = 0; $i -lt $bytes.Length; $i++) {
  if ($icoBytes[$firstOffset + $i] -ne $bytes[$i]) {
    throw "Generated 256px icon payload differs from official HNL PNG at byte $i."
  }
}

Write-Host 'HNL official branding generated:'
Write-Host "  PNG: $officialPng"
Write-Host "  ICO: $iconPath (256 exact PNG + 128/64/48/32/24/16 ARGB frames)"
Write-Host "  BMP: $smallPath"
