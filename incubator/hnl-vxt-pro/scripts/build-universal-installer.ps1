param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
  [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'

$universal = Join-Path $Root 'artifacts\universal'
$iss = Join-Path $Root 'installer\HNL.VXT.Universal.Setup.iss'
$branding = Join-Path $Root 'scripts\generate-installer-branding.ps1'
$outDir = Join-Path $Root 'artifacts\universal-installer'
$expected = Join-Path $outDir 'HNL_VXT_Pro_Universal_Setup_7.0.0-beta.1.exe'

$requiredStages = @('2023','2024','2025','2026-net8','2026-net10','2027')
foreach ($stageName in $requiredStages) {
  $stage = Join-Path $universal $stageName
  if (-not (Test-Path $stage)) { throw "Missing universal bridge stage: $stage" }
  foreach ($dll in @('HNL.VXT.AutoCAD.dll','HNL.VXT.Core.dll','HNL.VXT.UI.dll')) {
    if (-not (Test-Path (Join-Path $stage $dll))) { throw "Missing $dll in $stageName" }
  }
  $bad = Get-ChildItem $stage -File | Where-Object { $_.Name -match '^(Ac|AutoCAD).*\.dll$' }
  if ($bad) { throw "Autodesk runtime DLL detected in $stageName" }
}

foreach ($manifest in @('PackageContents.2026-net8.xml','PackageContents.2026-net10.xml')) {
  $manifestPath = Join-Path $Root "build\universal\$manifest"
  if (-not (Test-Path $manifestPath)) { throw "Missing universal manifest: $manifestPath" }
  try { [xml](Get-Content $manifestPath -Raw) | Out-Null }
  catch { throw "Invalid universal manifest XML: $manifestPath :: $($_.Exception.Message)" }
}

if (-not (Test-Path $iss)) { throw "Missing universal Inno Setup script: $iss" }
& $branding -Root $Root

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
  $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
  $programFiles = [Environment]::GetFolderPath('ProgramFiles')
  $candidates = @(
    (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
    (Join-Path $programFiles 'Inno Setup 6\ISCC.exe'),
    'C:\ProgramData\chocolatey\bin\ISCC.exe'
  )
  $IsccPath = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
  if (-not $IsccPath) {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { $IsccPath = $cmd.Source }
  }
}
if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
  throw 'ISCC.exe not found. Install Inno Setup 6 before building the universal installer.'
}

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Write-Host "Using ISCC: $IsccPath"
& $IsccPath $iss
if ($LASTEXITCODE -ne 0) { throw "Universal Inno Setup compiler failed with exit code $LASTEXITCODE" }

if (-not (Test-Path $expected)) { throw "Universal installer was not created: $expected" }
$length = (Get-Item $expected).Length
if ($length -lt 500KB) { throw "Universal installer is unexpectedly small: $length bytes" }

$hash = (Get-FileHash $expected -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = "$expected.sha256.txt"
"$hash  $([IO.Path]::GetFileName($expected))" | Set-Content -Path $hashFile -Encoding ascii
$info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $expected))

if (-not $info.FileVersion.StartsWith('7.0.0.6')) { throw "Unexpected Universal installer FileVersion: $($info.FileVersion)" }

Write-Host "Universal installer: $expected"
Write-Host "Size: $length bytes"
Write-Host "FileVersion: $($info.FileVersion)"
Write-Host "SHA256: $hash"
