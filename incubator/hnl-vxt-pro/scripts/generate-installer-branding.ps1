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

function Test-HnlImageBytes {
  param([byte[]]$Data, [int]$ExpectedSize)
  $ms = [System.IO.MemoryStream]::new($Data, $false)
  $img = $null
  try {
    $img = [System.Drawing.Image]::FromStream($ms, $true, $true)
    if ($img.Width -ne $ExpectedSize -or $img.Height -ne $ExpectedSize) {
      throw "Decoded frame is $($img.Width)x$($img.Height), expected ${ExpectedSize}x${ExpectedSize}."
    }
  }
  finally {
    if ($img -ne $null) { $img.Dispose() }
    $ms.Dispose()
  }
}

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
  param([System.Drawing.Image]$Source, [int]$Size)

  $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $margin = if ($Size -le 32) { 1 } else { [Math]::Max(1, [int][Math]::Round($Size * 0.02)) }
    $draw = $Size - (2 * $margin)
    $g.DrawImage($Source, $margin, $margin, $draw, $draw)
  }
  finally { $g.Dispose() }

  $ms = [System.IO.MemoryStream]::new()
  try {
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $result = [byte[]]$ms.ToArray()
  }
  finally {
    $ms.Dispose()
    $bitmap.Dispose()
  }
  Test-HnlImageBytes -Data $result -ExpectedSize $Size
  return $result
}

function Write-HnlMultiSizeIco {
  param([System.Drawing.Image]$Source, [string]$Path)

  $sizes = @(256, 128, 64, 48, 32, 24, 16)
  $frames = @()
  foreach ($size in $sizes) {
    $frames += ,([PSCustomObject]@{
      Size = $size
      Data = [byte[]](New-HnlPngFrame -Source $Source -Size $size)
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
      $bw.Write($wh); $bw.Write($wh); $bw.Write([byte]0); $bw.Write([byte]0)
      $bw.Write([UInt16]1); $bw.Write([UInt16]32)
      $bw.Write([UInt32]$frame.Data.Length); $bw.Write([UInt32]$offset)
      $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $bw.Write([byte[]]$frame.Data) }
  }
  finally {
    $bw.Dispose()
    $fs.Dispose()
  }
}

# Decode the actual embedded source before doing any branding work. This catches malformed
# palette/indexed PNGs that can pass a simple 8-byte PNG signature check.
Test-HnlImageBytes -Data $bytes -ExpectedSize 192
$loaded = [System.Drawing.Image]::FromFile($officialPng)
$source = $null
try {
  if ($loaded.Width -lt 192 -or $loaded.Height -lt 192) {
    throw "Official HNL logo must be at least 192x192; found $($loaded.Width)x$($loaded.Height)."
  }
  $source = New-HnlArgbSource -InputImage $loaded
  Write-HnlMultiSizeIco -Source $source -Path $iconPath

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
if ($icoBytes[6] -ne 0) { throw "Generated HNL icon must place the 256px frame first." }

# Round-trip decode every ICO payload so CI catches an icon that is structurally present
# but visually undecodable/corrupt.
for ($entry = 0; $entry -lt $count; $entry++) {
  $base = 6 + (16 * $entry)
  $w = $icoBytes[$base]
  $size = if ($w -eq 0) { 256 } else { [int]$w }
  $dataLen = [BitConverter]::ToUInt32($icoBytes, $base + 8)
  $dataOff = [BitConverter]::ToUInt32($icoBytes, $base + 12)
  $frame = New-Object byte[] $dataLen
  [Array]::Copy($icoBytes, [int]$dataOff, $frame, 0, [int]$dataLen)
  Test-HnlImageBytes -Data $frame -ExpectedSize $size
}

Write-Host 'HNL official branding generated and decode-verified:'
Write-Host "  PNG: $officialPng (RGBA source)"
Write-Host "  ICO: $iconPath (256/128/64/48/32/24/16, every frame round-trip decoded)"
Write-Host "  BMP: $smallPath"
