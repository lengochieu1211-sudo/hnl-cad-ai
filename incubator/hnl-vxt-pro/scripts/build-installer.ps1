param(
  [string]$Root = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),
  [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'

$bundle = Join-Path $Root 'artifacts\HNL.VXT.bundle'
$iss = Join-Path $Root 'installer\HNL.VXT.Setup.iss'
$branding = Join-Path $Root 'scripts\generate-installer-branding.ps1'
$outDir = Join-Path $Root 'artifacts\installer'
$expected = Join-Path $outDir 'HNL_VXT_Pro_Setup_7.0.0-alpha.1.exe'

if (-not (Test-Path (Join-Path $bundle 'PackageContents.xml'))) {
  throw "Bundle has not been prepared: $bundle"
}
if (-not (Test-Path $iss)) { throw "Missing Inno Setup script: $iss" }

& $branding -Root $Root

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
  $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
  if ($cmd) {
    $IsccPath = $cmd.Source
  }
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
  $pf86 = ${env:ProgramFiles(x86)}
  $candidates = @(
    $(if ($pf86) { Join-Path $pf86 'Inno Setup 6\ISCC.exe' }),
    $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe' }),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    'C:\ProgramData\chocolatey\bin\ISCC.exe'
  ) | Where-Object { $_ }

  $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
  Write-Host 'Searched for ISCC.exe in:'
  Get-ChildItem 'C:\Program Files*' -Directory -ErrorAction SilentlyContinue | Where-Object Name -Match 'Inno' | ForEach-Object FullName
  throw 'ISCC.exe not found although Inno Setup was installed.'
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Write-Host "Using ISCC: $IsccPath"
& $IsccPath $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compiler failed with exit code $LASTEXITCODE" }

if (-not (Test-Path $expected)) { throw "Installer was not created: $expected" }
$length = (Get-Item $expected).Length
if ($length -lt 300KB) { throw "Installer is unexpectedly small: $length bytes" }

$hash = (Get-FileHash $expected -Algorithm SHA256).Hash.ToLowerInvariant()
$hashFile = "$expected.sha256.txt"
"$hash  $([IO.Path]::GetFileName($expected))" | Set-Content -Path $hashFile -Encoding ascii

$info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path $expected))
Write-Host "Installer: $expected"
Write-Host "Size: $length bytes"
Write-Host "FileVersion: $($info.FileVersion)"
Write-Host "SHA256: $hash"
