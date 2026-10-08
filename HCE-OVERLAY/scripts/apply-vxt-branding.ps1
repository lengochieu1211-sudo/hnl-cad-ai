param([string]$Root='.')
$ErrorActionPreference='Stop'
$r=(Resolve-Path $Root).Path
$v=Join-Path $r 'incubator\hnl-vxt-pro'
$h=Join-Path $r 'incubator\hnl-ceiling-estimator-pro'
$master=Join-Path $v 'installer\assets\HNL-VXT-EXE-Logo.png'
& (Join-Path $v 'scripts\generate-installer-branding.ps1') -Root $v
$source=Join-Path $v 'artifacts\installer-assets'
$dir=Join-Path $h 'installer\assets'
New-Item -Force -ItemType Directory $dir | Out-Null
$ico=Join-Path $dir 'HNL-HCE.ico'
$bmp=Join-Path $dir 'HNL-HCE.bmp'
$png=Join-Path $dir 'HNL-Logo-Official.png'
Copy-Item (Join-Path $source 'HNL-VXT.ico') $ico -Force
Copy-Item (Join-Path $source 'HNL-VXT-Small.bmp') $bmp -Force
Copy-Item $master $png -Force
if ((Get-FileHash $master).Hash -ne (Get-FileHash $png).Hash) { throw 'Logo parity failed' }
$iss=@(Get-ChildItem $h -Recurse -Filter '*.iss' -File)
if ($iss.Count -lt 1) { throw 'Missing HCE Inno source' }
foreach($f in $iss){
 $t=[IO.File]::ReadAllText($f.FullName)
 if($t -notmatch '(?m)^\[Setup\]' -or $t -notmatch '(?m)^\[Files\]') { throw 'Invalid Inno source' }
 $setup='[Setup]'+[Environment]::NewLine+'SetupIconFile='+$ico+[Environment]::NewLine+'WizardSmallImageFile='+$bmp+[Environment]::NewLine+'UninstallDisplayIcon={app}\Assets\HNL-HCE.ico'
 $t=$t.Replace('[Setup]',$setup)
 $entry='Source: "'+$ico+'"; DestDir: "{app}\Assets"; DestName: "HNL-HCE.ico"; Flags: ignoreversion'
 $entry2='Source: "'+$png+'"; DestDir: "{app}\Assets"; DestName: "HNL-Logo-Official.png"; Flags: ignoreversion'
 $t=$t.Replace('[Files]','[Files]'+[Environment]::NewLine+$entry+[Environment]::NewLine+$entry2)
 [IO.File]::WriteAllText($f.FullName,$t,[Text.UTF8Encoding]::new($false))
 Write-Host "HCE installer logo updated: $($f.Name)"
}
Write-Host 'HNL VXT/HCE PNG SHA256 identical; 7-frame ICO reused.'
