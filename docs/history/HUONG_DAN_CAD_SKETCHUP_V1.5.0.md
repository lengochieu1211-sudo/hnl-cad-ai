# Hướng dẫn nhanh — CAD 2D ⇄ SketchUp

1. Save As bản CAD trước khi Cleanup.
2. Vào **Công cụ → CAD 2D ⇄ SketchUp Bridge**.
3. Tab **Kiểm tra trước khi xuất**: sửa lỗi màu đỏ.
4. Tab **Làm sạch 2D**: đặt tolerance; chọn Preview & Apply Cleanup; đọc số lượng Before/After rồi mới xác nhận.
5. Tab **Xuất sang SketchUp**: chọn Toàn Model hoặc Selected; tạo gói.
6. HNL tạo `HNL_CAD_to_SketchUp.json` và `HNL_CAD_to_SketchUp_Importer.rb`.
7. Trong SketchUp, dùng Ruby importer/extension để tạo group `HNL_CAD_IMPORT`.
8. Kiểm tra đơn vị, gốc tọa độ và Tags trước khi dựng tiếp.

Nếu lỗi: mở **Công cụ → Trung tâm chẩn đoán lỗi**, Copy báo cáo và gửi kèm file mẫu.
