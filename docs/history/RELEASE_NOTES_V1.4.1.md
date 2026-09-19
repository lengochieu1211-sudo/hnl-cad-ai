# HNL CAD AI v1.4.1 Diagnostics Release

## Bổ sung chính
- Trung tâm chẩn đoán lỗi tích hợp trong **Công cụ**.
- Mã lỗi riêng cho command/runtime/async/file open/file save.
- Ghi context: file, selection count, workbench, AutoCAD Bridge, Safe Mode.
- Ghi cause/stack kỹ thuật khi có exception.
- Copy toàn bộ Diagnostic Report hoặc xuất TXT.
- Lưu tối đa 150 diagnostic events trong localStorage để không mất ngay khi đóng/mở giao diện.
- Global handlers cho `window.error` và `unhandledrejection`.
- Unsupported command không còn chỉ toast chung; tạo event có mã và hướng xử lý.
- Save/Open failure có diagnostic event riêng.
- Hướng dẫn sử dụng tích hợp: **Công cụ → Hướng dẫn sử dụng**.
- Tài liệu `HUONG_DAN_SU_DUNG_V1.4.1.md` kèm quy trình gửi lỗi.

## Quy tắc test đề nghị
Mỗi tính năng cần test ít nhất: không chọn đối tượng, chọn đúng đối tượng, chọn sai loại đối tượng, file rỗng, file dữ liệu lớn, Standalone, AutoCAD Bridge offline và (khi plugin có) AutoCAD Connected.

## Giới hạn kiểm chứng trong môi trường audit
Electron main/preload PASS `node --check`. Các file TS/TSX mới và App/Ribbon PASS TypeScript `transpileModule`. Full `tsc --noEmit` không thể hoàn tất do ZIP build-ready không chứa `node_modules`, nên build installer Windows vẫn cần GitHub Actions/Windows với dependencies được cài đầy đủ.
