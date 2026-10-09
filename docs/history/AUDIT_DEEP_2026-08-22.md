# HNL CAD AI v1.2.0 - Deep Logic / UI / Windows Build Audit

## Kết luận
- Kiến trúc Electron + React/Vite + Express local server phù hợp để đóng gói EXE Windows 10/11 x64.
- Standalone dùng DXF ASCII/JSON và engine CAD nội bộ; DWG native/AutoLISP/AutoCAD Table/Layout/Field thật vẫn cần AutoCAD Bridge plugin.
- AutoCAD Bridge hiện mới là skeleton HNLBRIDGESTATUS/HNLBRIDGEPING, chưa phải cầu nối automation hoàn chỉnh.

## Sửa trực tiếp trong lần audit này
1. New Drawing gọi clearProjectSnapshot() trước khi tạo bản vẽ mới, tránh recovery cũ quay lại.
2. Save HNL JSON đổi version 1.0.0 -> 1.2.0, thêm schemaVersion và spreadsheetParameters.
3. /api/health đổi version 1.0.0 -> 1.2.0.
4. build-exe.bat sửa tên output dự kiến 1.1.0 -> 1.2.0.
5. Electron thêm single-instance lock để tránh 2 instance tranh cổng API local 32145.
6. open-external-url chỉ cho phép http/https, giảm rủi ro mở protocol ngoài ý muốn.
7. GitHub Actions bỏ npm cache cấu hình dựa package.json khi chưa có package-lock; thêm log Node/npm version.
8. node --check electron/main.cjs PASS; electron/preload.cjs PASS.

## Logic chức năng đã có nền tảng thật
- Undo/Redo entity history; Select All/Delete.
- AutoSave/Recovery local.
- Import DXF ASCII cơ bản: LINE/CIRCLE/TEXT/MTEXT/LWPOLYLINE.
- Lưu HNL JSON, Export engine, Print/PDF qua browser print.
- Canvas pan/zoom/select, LINE/RECTANGLE/CIRCLE/WALL/MLeader cơ bản.
- Area/perimeter, layer controls, layouts/viewports nội bộ.
- Smart Object + Project Tree + Dependency + Spreadsheet.
- Pile Studio nội bộ, Shopdrawing/MEP/Building Code knowledge modules.
- AI Gemini có offline fallback nếu không có API key.

## Điểm chưa được phép hiểu là hoàn thiện
- Đọc/ghi DWG native trong EXE độc lập.
- Điều khiển AutoCAD ObjectId/Transaction hai chiều.
- Chạy AutoLISP trực tiếp từ EXE vào AutoCAD.
- AutoCAD Table/Field/Layout/Viewport thật.
- Build DLL tương thích riêng AutoCAD 2023/2024/2025/2026.
- AI Offline kiểu local LLM/Ollama chưa nối thật trong Settings.

## Đề xuất UI/UX ưu tiên
1. Giảm 16 tab Ribbon thành 6 nhóm lớn: Draw, Annotate, Data, Layout, AI/Automation, Engineering; tab chuyên sâu nằm trong dropdown/workbench.
2. Top bar đang nhiều nút; giữ Project Tree, AI, Auto Detail; chuyển Addons/Shop Check/Drywall vào launcher "Tools".
3. Hiển thị badge rõ: STANDALONE / AUTOCAD CONNECTED / AI ONLINE / AI OFFLINE.
4. Các nút cần plugin phải disabled + tooltip thay vì vẫn bấm rồi mới toast.
5. Thêm màn hình Start Center: New/Open/Recent/Recovery/Connect AutoCAD.
6. Thêm thanh trạng thái cố định: tọa độ, snap, ortho, grid, scale, units, selection count.
7. Modal kỹ thuật cần footer chuẩn: Cancel / Preview / Apply, không trộn hành động nguy hiểm với hành động xem trước.
8. Chuẩn hóa font-size tối thiểu 12px; hiện nhiều text 10px/11px gây khó đọc trên laptop 125%-150% scaling.
9. Thêm layout responsive cho 1366x768; Ribbon 96px + top bar + palette dễ làm canvas thấp.
10. Dùng toast thay alert() trong Lisp Builder/Palette để đồng nhất UX.

## Đề xuất tính năng tiếp theo
- Recent files + crash recovery manager nhiều phiên bản.
- Project file .hnl (zip/json) thay vì chỉ JSON rời.
- Command permission matrix: Standalone / Requires AutoCAD / Requires AI.
- Real named-pipe localhost bridge với token phiên.
- Plugin installer tự nhận AutoCAD version và cài .bundle đúng thư mục.
- DXF parser/exporter nâng cấp block/attribute/dimension/hatch/spline.
- Test automation cho geometry, undo/redo, import/export round-trip.
- Installer diagnostics: WebView/GPU/log folder/Reset settings/Safe mode.

## Build verification
- Static Electron syntax: PASS.
- Full TypeScript/Vite/NSIS build: chưa xác nhận trong sandbox vì npm dependency download không hoàn tất.
- GitHub Actions workflow đã có pipeline Windows x64: install -> lint -> dist:win -> verify EXE -> artifact.
