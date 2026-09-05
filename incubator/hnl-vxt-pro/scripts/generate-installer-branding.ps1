param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

$out = Join-Path $Root 'artifacts\installer-assets'
$logoB64 = Join-Path $Root 'src\HNL.VXT.UI\Assets\HNL-Logo-Official.b64'
$officialPng = Join-Path $out 'HNL-Logo-Official.png'
$iconPath = Join-Path $out 'HNL-VXT.ico'
$smallPath = Join-Path $out 'HNL-VXT-Small.bmp'

if (-not (Test-Path $logoB64)) { throw "Missing official HNL logo asset: $logoB64" }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$base64 = (Get-Content $logoB64 -Raw) -replace '\s',''
$bytes = [Convert]::FromBase64String($base64)
if ($bytes.Length -lt 24 -or
    $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47 -or
    $bytes[4] -ne 0x0D -or $bytes[5] -ne 0x0A -or $bytes[6] -ne 0x1A -or $bytes[7] -ne 0x0A) {
  throw 'HNL logo asset is not a valid PNG stream.'
}
[IO.File]::WriteAllBytes($officialPng, $bytes)

function Get-HnlPngSize {
  param([byte[]]$Data)
  if ($Data.Length -lt 24) { throw 'PNG stream is too short.' }
  $width = (($Data[16] -shl 24) -bor ($Data[17] -shl 16) -bor ($Data[18] -shl 8) -bor $Data[19])
  $height = (($Data[20] -shl 24) -bor ($Data[21] -shl 16) -bor ($Data[22] -shl 8) -bor $Data[23])
  return @([int]$width, [int]$height)
}

function Test-HnlImageBytes {
  param([byte[]]$Data, [int]$ExpectedSize)
  $size = Get-HnlPngSize -Data $Data
  if ($size[0] -ne $ExpectedSize -or $size[1] -ne $ExpectedSize) {
    throw "PNG frame is $($size[0])x$($size[1]), expected ${ExpectedSize}x${ExpectedSize}."
  }

  # WPF's PNG decoder supports indexed/paletted PNG reliably on Windows Server,
  # unlike GDI+ Image.FromStream which rejects some optimized indexed PNG files.
  $ms = [System.IO.MemoryStream]::new($Data, $false)
  try {
    $decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new(
      $ms,
      [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
      [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    if ($decoder.Frames.Count -lt 1) { throw 'WPF PNG decoder returned no frames.' }
    $frame = $decoder.Frames[0]
    if ($frame.PixelWidth -ne $ExpectedSize -or $frame.PixelHeight -ne $ExpectedSize) {
      throw "WPF decoded frame is $($frame.PixelWidth)x$($frame.PixelHeight), expected ${ExpectedSize}x${ExpectedSize}."
    }
  }
  finally { $ms.Dispose() }
}

function New-HnlArgbSourceFromPngBytes {
  param([byte[]]$Data)

  $ms = [System.IO.MemoryStream]::new($Data, $false)
  try {
    $decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new(
      $ms,
      [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
      [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $frame = $decoder.Frames[0]
    $converted = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new()
    $converted.BeginInit()
    $converted.Source = $frame
    $converted.DestinationFormat = [System.Windows.Media.PixelFormats]::Bgra32
    $converted.EndInit()
    $converted.Freeze()

    $width = $converted.PixelWidth
    $height = $converted.PixelHeight
    $stride = $width * 4
    $pixels = New-Object byte[] ($stride * $height)
    $converted.CopyPixels($pixels, $stride, 0)

    $bitmap = [System.Drawing.Bitmap]::new(
      $width,
      $height,
      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $locked = $bitmap.LockBits(
      $rect,
      [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
      for ($row = 0; $row -lt $height; $row++) {
        $srcOffset = $row * $stride
        $dst = [IntPtr]::Add($locked.Scan0, $row * $locked.Stride)
        [System.Runtime.InteropServices.Marshal]::Copy($pixels, $srcOffset, $dst, $stride)
      }
    }
    finally { $bitmap.UnlockBits($locked) }
    return $bitmap
  }
  finally { $ms.Dispose() }
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

# Official master: 256x256 generated from the user's original HNL artwork.
Test-HnlImageBytes -Data $bytes -ExpectedSize 256
$source = $null
try {
  $source = New-HnlArgbSourceFromPngBytes -Data $bytes
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
}

foreach ($required in @($officialPng, $iconPath, $smallPath)) {
  if (-not (Test-Path $required)) { throw "Missing generated HNL branding asset: $required" }
}

$icoBytes = [IO.File]::ReadAllBytes($iconPath)
if ($icoBytes.Length -lt 128) { throw "Generated HNL icon is unexpectedly small." }
$count = [BitConverter]::ToUInt16($icoBytes, 4)
if ($count -ne 7) { throw "Generated HNL icon must contain 7 sizes; found $count." }
if ($icoBytes[6] -ne 0) { throw "Generated HNL icon must place the 256px frame first." }

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
Write-Host "  PNG: $officialPng (256x256 official source)"
Write-Host "  ICO: $iconPath (256/128/64/48/32/24/16, every frame WPF-decoded)"
Write-Host "  BMP: $smallPath"
