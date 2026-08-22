# HNL CAD AI v2.0.5

HNL CAD AI là ứng dụng CAD/Shopdrawing dạng Windows Desktop (Electron) có hai chế độ kiến trúc:

- **Standalone Workspace:** chạy độc lập, không cần AutoCAD; làm việc với model HNL và DXF cơ bản.
- **AutoCAD Connected Workspace:** chỉ bật khi plugin HNL AutoCAD Bridge thật được cài và phản hồi. DWG native/AutoLISP/Field/Table/Layout/Viewport thuộc chế độ này.

## Điểm mới v2.0.5 Professional Release Candidate
- HNL Pile Studio: PHC/PC/cọc vuông/custom, pile plan + tag + schedule.
- AutoSave/Recovery 30 giây và sau thay đổi.
- AutoCAD Bridge status không giả lập.
- AutoCAD .NET bridge skeleton nằm trong `autocad-plugin/Hnl.CadBridge`.
- UI có tab Cọc & Nền móng, connection indicator, autosave indicator.
- Audit command typo và GitHub Actions build EXE.

## Windows
Mục tiêu: Windows 10 22H2 / Windows 11 x64.

Tối thiểu đề xuất: CPU 4 nhân x64, RAM 8 GB, màn hình 1366x768, GPU hỗ trợ DirectX 11, khoảng 1.5 GB dung lượng trống. Khuyến nghị RAM 16 GB, Full HD và SSD cho bản vẽ lớn/AI.

## Build installer
```bat
npm install
npm run lint
npm run dist:win
```
Output: `dist_electron/HNL_CAD_AI_Setup_2.0.5.exe`

Có thể dùng `.github/workflows/build-windows.yml` trên GitHub Actions Windows runner.


## Release hardening v2.0.5
- Full Project Save/Open schema v2, backward-compatible recovery.
- Dirty-state + cảnh báo thoát/tạo mới khi chưa lưu.
- Start Center chuyên nghiệp, tên file và trạng thái lưu trên Ribbon.
- Safe Mode đồng bộ với Settings.
- Gemini key mã hóa bằng Windows DPAPI qua Electron safeStorage.
- Local API token theo phiên + 127.0.0.1 + CSP + Electron sandbox.
- Installer mặc định per-user để giảm lỗi quyền Administrator.

> DWG native/AutoLISP native vẫn cần AutoCAD Bridge/plugin; Standalone không giả lập khả năng chưa có.
