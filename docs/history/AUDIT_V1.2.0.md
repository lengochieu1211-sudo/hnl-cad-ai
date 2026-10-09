# HNL CAD AI v1.2.0 - Upgrade & Deep Audit

## Đã nâng cấp
- Thêm HNL Pile Studio: PHC/PC/cọc vuông/custom, bố trí hàng-cột tham số, tag P001..., schedule tọa độ/size/length.
- Không suy luận manufacturer capacity = geotechnical/allowable capacity; mặc định NEEDS CONFIRMATION.
- Thêm AutoSave/Recovery project snapshot (schemaVersion=1), lưu cục bộ sau thay đổi và chu kỳ 30 giây.
- New Drawing xóa recovery cũ để tránh khôi phục nhầm.
- Thêm AutoCAD Bridge detection: UI chỉ báo Connected nếu có bridge thật được plugin inject; mặc định Standalone.
- Dựng AutoCAD .NET Bridge skeleton với HNLBRIDGESTATUS/HNLBRIDGEPING, không giả là đã DWG native.
- Đổi typo HNLTATBLE -> HNLTABLE trong library command mẫu.
- Thêm Ribbon tab `16. Cọc & Nền móng` và Pile Studio trong menu Electron.
- Thanh trên hiển thị AutoSave status và AutoCAD connection status rõ ràng.
- GitHub Actions build Windows có lint -> build -> verify EXE -> artifact.
- Version nâng 1.2.0.

## Audit logic
- Standalone và AutoCAD native tách rõ trách nhiệm.
- DWG binary vẫn không được đọc giả bằng text parser.
- AutoLISP native/Field/Table/Layout trong DWG vẫn yêu cầu AutoCAD plugin.
- Các hệ EI/acoustic/test report mẫu phải ở NEEDS CONFIRMATION nếu chưa có nguồn dự án thực.
- Pile Studio chỉ tạo plan/schedule nội bộ; không tự thiết kế nền móng cuối cùng.

## Cần làm tiếp khi có AutoCAD SDK/runtime để kiểm thử
1. Build Hnl.CadBridge cho AutoCAD 2023/2024/2025/2026 theo từng managed API target.
2. Chọn transport IPC an toàn (Named Pipe/WebSocket localhost có token phiên).
3. Map Structured CAD Action -> AutoCAD Transaction/ObjectId.
4. Test DWG native: selection, Block/Attribute, Table, Field, Layout/Viewport, Zoom/Highlight.
5. Test installer thật trên Win 10 22H2 và Win 11, máy có/không có AutoCAD.

## Kiểm tra tĩnh trong môi trường hiện tại
- electron/main.cjs: node --check PASS.
- electron/preload.cjs: node --check PASS.
- package.json/metadata.json: JSON PASS.
- TypeScript command chạy được nhưng môi trường không có node_modules nên báo thiếu dependency React/Express/Vite; không thấy lỗi type nội bộ mới trước khi dependency resolution dừng.
