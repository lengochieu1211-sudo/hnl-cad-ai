# FINAL DEEP AUDIT — HNL CAD AI v1.4.0 Professional RC

## Đã sửa
1. Full-project Save/Open schema v2: entities, layers, layouts, viewports, smart objects, spreadsheet, translation memory, block library, dependency DAG, modules, workbench và active layout.
2. AutoSave migrate schema v1 -> v2, không còn bắt buộc project phải có entity mới phục hồi.
3. New Drawing reset toàn bộ project; cảnh báo khi có dữ liệu chưa lưu.
4. Dirty state + tên file trên Ribbon + title cửa sổ; đóng EXE có cảnh báo thay đổi chưa lưu.
5. Safe Mode dùng chung giữa Settings và App, có hiệu lực ngay.
6. Gemini API key lưu qua Electron safeStorage / Windows DPAPI trong EXE.
7. Local Express chỉ bind 127.0.0.1; packaged mode thêm random session token cho API đặc quyền.
8. Electron sandbox bật; CSP được thêm; URL ngoài chỉ http/https.
9. Xóa menu Delete bị trùng.
10. Start Center mới: Continue Workspace / Open Project-DXF / New Drawing + trạng thái SafeMode/Recovery/Project/Bridge.
11. Installer chuyển mặc định per-user, giảm lỗi quyền Admin.
12. Workflow Windows kiểm tra installer tồn tại, >5MB, sinh SHA256 trước khi upload artifact.
13. Version thống nhất 1.4.0 trong package/server/metadata/readme/workflow/build script.
14. Xóa bun.lock 0-byte gây hiểu nhầm toolchain.

## Kiểm tra đã thực hiện
- `node --check electron/main.cjs`: PASS.
- `node --check electron/preload.cjs`: PASS.
- TypeScript/TSX syntax transpile trên toàn bộ `src`, `server.ts`, `vite.config.ts`: PASS.
- Full `npm install`/`electron-builder` chưa chạy được trong môi trường hiện tại do tải dependency bị timeout; GitHub Actions/Windows vẫn là bước xác nhận installer thực tế.

## Những giới hạn còn lại cần làm ở nhánh tiếp theo
- Native DWG read/write cần AutoCAD/RealDWG/ODA tương ứng.
- Native AutoLISP, ObjectId, Transaction, AutoCAD Table/Field/Layout/Viewport cần plugin AutoCAD.
- Undo/Redo hiện entity-first; nên nâng thành Project Transaction History để undo nguyên tử cả layer/layout/spreadsheet/smart object.
- Nên bổ sung crash telemetry local opt-in, recent-file registry, autosave generations (3-5 phiên), project templates và command macro recorder.
- Code signing chưa có nên Windows SmartScreen có thể cảnh báo Unknown Publisher.
