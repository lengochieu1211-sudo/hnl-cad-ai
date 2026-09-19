# HNL CAD AI v1.5.0 — Audit CAD 2D ⇄ SketchUp

## Mục tiêu
HNL CAD AI tiếp tục ưu tiên CAD 2D. 3D không trở thành lõi modeling. SketchUp được xem là hệ thống dựng mô hình bên ngoài; HNL đóng vai trò làm sạch, mapping, chuyển đổi và kiểm tra dữ liệu 2D.

## Đã triển khai trong v1.5.0
- `CAD 2D ⇄ SketchUp Bridge` trong menu Công cụ.
- 2D Cleanup có preview/xác nhận:
  - xóa LINE/CIRCLE/RECTANGLE/POLYLINE trùng theo tolerance;
  - xóa LINE cực ngắn;
  - Join LINE đồng tuyến, cùng layer, chạm nhau trong tolerance;
  - chuẩn hóa entity tham chiếu layer không tồn tại về layer `0`;
  - báo cáo Before/After.
- Preflight trước khi xuất SketchUp:
  - layer thiếu;
  - geometry tọa độ lỗi;
  - loại entity không phù hợp để đưa sang SketchUp.
- Xuất gói trung gian `HNL_CAD_to_SketchUp.json`.
- Xuất `HNL_CAD_to_SketchUp_Importer.rb` để nhập vector vào SketchUp bằng Ruby API.
- Layer → Tag metadata; giữ metadata màu; Block → Component metadata để chuẩn bị cho Bridge nâng cao.
- Diagnostic Event cho Cleanup và Export.
- Hướng dẫn sử dụng trong ứng dụng.

## Kiểm tra logic/an toàn
1. Cleanup không tự chạy khi mở file.
2. Cleanup phải hiện Preview và hỏi xác nhận.
3. Không ghi đè file nguồn từ module Bridge.
4. Export Selected phải chặn nếu chưa chọn đối tượng.
5. Geometry NaN/Infinity là lỗi chặn export.
6. Entity TEXT/MTEXT/DIMENSION/HATCH không bị giả vờ chuyển thành geometry SketchUp; được báo là unsupported/info.
7. Ruby importer dùng `start_operation`; khi lỗi gọi `abort_operation`.
8. Gói Bridge có schema + version để hỗ trợ migration về sau.

## Chưa được coi là hoàn thành
### SketchUp → CAD 2D
Chưa triển khai chuyển `.SKP` binary trực tiếp. Để làm đúng cần SketchUp API/extension lấy:
- Scene/Camera;
- Section Plane;
- Visible/Hidden edges;
- Tags;
- Component instances;
- transformation;
- units/origin;
sau đó project sang mặt phẳng 2D, hidden-line classification và xuất DXF.

### Đồng bộ hai chiều
Chưa có Link ID bền vững giữa CAD entity và SketchUp entity. Chưa có Added/Updated/Deleted diff hoặc Update Existing Import.

### DWG
Standalone không đọc/ghi DWG binary native. Luồng DWG nên đi qua AutoCAD Bridge hoặc DXF trung gian có kiểm tra.

## Đề xuất tiếp theo
### P0 — cần làm trước khi gọi là Bridge hoàn chỉnh
- SketchUp Extension `.rbz` chính thức thay vì Ruby script thủ công.
- Import JSON có unit conversion mm/cm/m thực.
- Block CAD → SketchUp Component thật, không chỉ metadata.
- Base Point + rotation mapping.
- Layer/Tag mapping editor với preset.
- Link ID + update existing group không import chồng.
- Test file lớn 10k/50k/100k entities và giới hạn thời gian/memory.

### P1 — SketchUp → CAD
- Export Scene/Section từ SketchUp Extension về HNL JSON.
- Orthographic projection 2D.
- Visible/Cut/Hidden line classification.
- Preset nét: Cut / Projection / Hidden / Annotation.
- Cleanup + DXF export.
- Color/lineweight mapping cho bản vẽ đẹp trước Publish.

### P2 — Publish workflow
- `Prepare for Publish`: Audit → Cleanup → Layer mapping → Font/Xref/Viewport/CTB check.
- READY / NOT READY checklist.
- Xuất `_CLEAN.dxf` mặc định, không overwrite.
- Khi AutoCAD Bridge online: Send to AutoCAD → SaveAs DWG → Page Setup/PUBLISH.

## Những thứ không nên thêm vào lõi
- FEM, CAM, Assembly 3D, Part Design kiểu FreeCAD.
- Modeling 3D tổng quát.
HNL nên tập trung tốc độ và độ tin cậy của CAD 2D + workflow SketchUp/AutoCAD.
