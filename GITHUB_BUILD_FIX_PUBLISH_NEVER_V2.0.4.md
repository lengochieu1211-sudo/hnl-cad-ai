# GitHub Actions build fix v2.0.4

Lỗi đã gặp:
`GitHub Personal Access Token is not set, neither programmatically, nor using env "GH_TOKEN"`

Nguyên nhân:
electron-builder hoàn tất NSIS installer rồi cố bước publish GitHub release/update metadata.

Cách sửa:
`electron-builder --win nsis --x64 --publish never`

Workflow vẫn dùng `actions/upload-artifact@v4` để lưu installer, nên không cần GH_TOKEN.

Kết quả mong đợi:
- TypeScript audit: PASS
- Build installer: PASS
- Verify installer exists: PASS
- Upload Windows installer: PASS
- Artifact: HNL-CAD-AI-v2.0.4-Windows-Installer
