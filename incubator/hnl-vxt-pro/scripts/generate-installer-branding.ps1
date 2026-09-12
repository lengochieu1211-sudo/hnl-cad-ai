param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $Root 'artifacts\installer-assets'
$logoB64 = Join-Path $Root 'src\HNL.VXT.UI\Assets\HNL-Logo-Official.b64'
$repoRoot = Split-Path -Parent (Split-Path -Parent $Root)
$sharedLogo = Join-Path $repoRoot 'public\hnl-logo.png'
$officialPng = Join-Path $out 'HNL-Logo-Official.png'
$iconPath = Join-Path $out 'HNL-VXT.ico'
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'

New-Item -ItemType Directory -Force -Path $out | Out-Null

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

function Draw-HnlContained {
  param(
    [System.Drawing.Graphics]$Graphics,
    [System.Drawing.Image]$Source,
    [int]$CanvasWidth,
    [int]$CanvasHeight,
    [int]$Margin = 0
  )
  $availableW = [Math]::Max(1, $CanvasWidth - (2 * $Margin))
  $availableH = [Math]::Max(1, $CanvasHeight - (2 * $Margin))
  $scale = [Math]::Min($availableW / [double]$Source.Width, $availableH / [double]$Source.Height)
  $drawW = [Math]::Max(1, [int][Math]::Round($Source.Width * $scale))
  $drawH = [Math]::Max(1, [int][Math]::Round($Source.Height * $scale))
  $x = [int][Math]::Floor(($CanvasWidth - $drawW) / 2.0)
  $y = [int][Math]::Floor(($CanvasHeight - $drawH) / 2.0)
  $Graphics.DrawImage($Source, $x, $y, $drawW, $drawH)
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
    # Keep the logo large in Explorer. The source artwork already contains its own safe padding.
    Draw-HnlContained -Graphics $g -Source $Source -CanvasWidth $Size -CanvasHeight $Size -Margin 0
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

  # IMPORTANT: conventional ascending directory order is intentional.
  # The previous 256-first custom ICO decoded correctly by itself but Inno/Windows
  # produced a visibly corrupted large Explorer frame in the compiled Setup EXE.
  $sizes = @(16, 24, 32, 48, 64, 128, 256)
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

# Prefer the repository's full-resolution shared HNL artwork when available.
# Fallback remains the embedded Palette asset, so installer builds stay self-contained.
$loaded = $null
$sourceLabel = $null
if (Test-Path $sharedLogo) {
  try {
    $candidate = [System.Drawing.Image]::FromFile($sharedLogo)
    if ($candidate.Width -ge 256 -and $candidate.Height -ge 256) {
      $loaded = $candidate
      $sourceLabel = "$sharedLogo ($($loaded.Width)x$($loaded.Height))"
    }
    else {
      $candidate.Dispose()
    }
  }
  catch {
    Write-Warning "Cannot use shared HNL logo; falling back to embedded asset: $($_.Exception.Message)"
  }
}

if ($loaded -eq $null) {
  if (-not (Test-Path $logoB64)) { throw "Missing official HNL logo asset: $logoB64" }
  $base64 = (Get-Content $logoB64 -Raw) -replace '\s',''
  $bytes = [Convert]::FromBase64String($base64)
  if ($bytes.Length -lt 8 -or
      $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47 -or
      $bytes[4] -ne 0x0D -or $bytes[5] -ne 0x0A -or $bytes[6] -ne 0x1A -or $bytes[7] -ne 0x0A) {
    throw 'HNL logo asset is not a valid PNG stream.'
  }
  [IO.File]::WriteAllBytes($officialPng, $bytes)
  $loaded = [System.Drawing.Image]::FromFile($officialPng)
  $sourceLabel = "$logoB64 ($($loaded.Width)x$($loaded.Height))"
}

$source = $null
try {
  $source = New-HnlArgbSource -InputImage $loaded

  # Save a normalized 256px reference PNG alongside the installer assets.
  $refBytes = [byte[]](New-HnlPngFrame -Source $source -Size 256)
  [IO.File]::WriteAllBytes($officialPng, $refBytes)

  Write-HnlMultiSizeIco -Source $source -Path $iconPath

  $small = [System.Drawing.Bitmap]::new(64, 64, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
  $gs = [System.Drawing.Graphics]::FromImage($small)
  try {
    $gs.Clear([System.Drawing.Color]::White)
    $gs.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gs.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $gs.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    Draw-HnlContained -Graphics $gs -Source $source -CanvasWidth 64 -CanvasHeight 64 -Margin 2
  }
  finally { $gs.Dispose() }
  $small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $small.Dispose()
}
finally {
  if ($source -ne $null) { $source.Dispose() }
  if ($loaded -ne $null) { $loaded.Dispose() }
}

foreach ($required in @($officialPng, $iconPath, $smallPath)) {
  if (-not (Test-Path $required)) { throw "Missing generated HNL branding asset: $required" }
}

$icoBytes = [IO.File]::ReadAllBytes($iconPath)
if ($icoBytes.Length -lt 128) { throw "Generated HNL icon is unexpectedly small." }
$count = [BitConverter]::ToUInt16($icoBytes, 4)
if ($count -ne 7) { throw "Generated HNL icon must contain 7 sizes; found $count." }
$expectedSizes = @(16, 24, 32, 48, 64, 128, 256)

for ($entry = 0; $entry -lt $count; $entry++) {
  $base = 6 + (16 * $entry)
  $w = $icoBytes[$base]
  $size = if ($w -eq 0) { 256 } else { [int]$w }
  if ($size -ne $expectedSizes[$entry]) {
    throw "Generated HNL icon entry $entry is ${size}px; expected $($expectedSizes[$entry])px."
  }
  $dataLen = [BitConverter]::ToUInt32($icoBytes, $base + 8)
  $dataOff = [BitConverter]::ToUInt32($icoBytes, $base + 12)
  $frame = New-Object byte[] $dataLen
  [Array]::Copy($icoBytes, [int]$dataOff, $frame, 0, [int]$dataLen)
  Test-HnlImageBytes -Data $frame -ExpectedSize $size
}

Write-Host 'HNL official branding generated and decode-verified:'
Write-Host "  SOURCE: $sourceLabel"
Write-Host "  PNG: $officialPng (256x256 normalized reference)"
Write-Host "  ICO: $iconPath (16/24/32/48/64/128/256 ascending, every frame round-trip decoded)"
Write-Host "  BMP: $smallPath"
