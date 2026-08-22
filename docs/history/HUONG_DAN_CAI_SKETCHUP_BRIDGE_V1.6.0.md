# Cài HNL CAD AI Bridge cho SketchUp

## Cài Extension
1. Mở SketchUp.
2. Window → Extension Manager.
3. Chọn **Install Extension**.
4. Chọn `sketchup-extension/HNL_CAD_AI_Bridge_v2.0.0.rbz`.
5. Khởi động lại SketchUp nếu menu chưa xuất hiện.
6. Mở **Extensions → HNL CAD AI Bridge**.

## SketchUp → HNL CAD
1. Chọn Scene phù hợp.
2. Với bản vẽ kỹ thuật nên dùng Camera → Parallel Projection.
3. Nếu dùng mặt cắt, kích hoạt Section Plane.
4. Extensions → HNL CAD AI Bridge → **Export Scene/Section → HNL CAD**.
5. Chọn nơi lưu `HNL_SketchUp_Scene.json`.
6. Trong HNL CAD AI: Công cụ → CAD 2D ⇄ SketchUp Bridge → **SketchUp → CAD**.
7. Chọn JSON.
8. Tùy chọn chạy **AI gợi ý Layer/Nét**.
9. Chọn **Project → CAD 2D** hoặc **Xuất DXF 2D**.
10. Cleanup, kiểm tra nét rồi mới Save/Publish.

## HNL CAD → SketchUp
1. Trong HNL chạy Cleanup/Preflight.
2. Tab CAD → SketchUp → tạo `HNL_CAD_to_SketchUp.json`.
3. Trong SketchUp: Extensions → HNL CAD AI Bridge → **Import/Update HNL CAD → SketchUp**.
4. Extension tạo/cập nhật group `HNL_CAD_IMPORT`.

## Export DXF native từ SketchUp
Extensions → HNL CAD AI Bridge → **Export Native DXF (SketchUp Pro)**.
Đây là exporter native của SketchUp Pro. Với workflow linework 2D có kiểm soát, ưu tiên HNL Scene JSON → HNL DXF 2D.

## Khi lỗi
Gửi:
- ảnh lỗi;
- file JSON mẫu;
- Scene/Section đang dùng;
- phiên bản SketchUp;
- Diagnostic Report từ HNL;
- nếu lỗi Extension: nội dung Ruby Console/backtrace.
