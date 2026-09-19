param(
  [Parameter(Mandatory=$true)]
  [string]$RepoPath
)

$ErrorActionPreference = "Stop"
$Source = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path $RepoPath)) {
  throw "RepoPath không tồn tại: $RepoPath"
}

$version = (Get-Content (Join-Path $PSScriptRoot "package.json") -Raw | ConvertFrom-Json).version
Write-Host "HNL CAD AI v$version - Full Source Replace"
Write-Host "Nguồn: $Source"
Write-Host "Repo:  $RepoPath"
Write-Host ""

# Preserve Git metadata
$git = Join-Path $RepoPath ".git"
if (-not (Test-Path $git)) {
  throw "Thư mục đích không phải Git repo (không có .git)"
}

# Copy all source except helper scripts
$exclude = @(".git","node_modules","dist","dist_electron")
Get-ChildItem -Path $Source -Force | Where-Object {
  $exclude -notcontains $_.Name -and $_.Name -notlike "REPLACE_*"
} | ForEach-Object {
  $dst = Join-Path $RepoPath $_.Name
  if ($_.PSIsContainer) {
    Copy-Item $_.FullName $dst -Recurse -Force
  } else {
    Copy-Item $_.FullName $dst -Force
  }
}

Write-Host ""
Write-Host "Đã chép source v$version vào repo."
Write-Host "Tiếp theo chạy:"
Write-Host "  cd `"$RepoPath`""
Write-Host "  git add ."
Write-Host "  git commit -m `"Update HNL CAD AI v$version full source`""
Write-Host "  git push"
