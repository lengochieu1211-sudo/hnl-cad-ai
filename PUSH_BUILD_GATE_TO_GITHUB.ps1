param(
  [Parameter(Mandatory=$true)]
  [string]$RepoPath
)

$ErrorActionPreference = "Stop"
$Source = Split-Path -Parent $MyInvocation.MyCommand.Path
$Replace = Join-Path $Source "REPLACE_WHOLE_REPO.ps1"

if (-not (Test-Path $Replace)) { throw "Thiếu REPLACE_WHOLE_REPO.ps1" }
if (-not (Test-Path (Join-Path $RepoPath ".git"))) { throw "RepoPath không phải Git repo: $RepoPath" }

& $Replace -RepoPath $RepoPath
Push-Location $RepoPath
try {
  git status --short
  git add -A
  $pending = git status --porcelain
  if (-not $pending) {
    Write-Host "Không có thay đổi để commit."
    exit 0
  }
  git commit -m "HNL CAD AI v2.8.1 Bridge P0 patched build gate"
  if ($LASTEXITCODE -ne 0) { throw "git commit thất bại" }
  git push origin main
  if ($LASTEXITCODE -ne 0) { throw "git push thất bại. Kiểm tra đăng nhập GitHub/quyền repo." }
  Write-Host "Đã push source build-gate lên main. GitHub Actions sẽ tự chạy workflow build-windows.yml."
} finally {
  Pop-Location
}
