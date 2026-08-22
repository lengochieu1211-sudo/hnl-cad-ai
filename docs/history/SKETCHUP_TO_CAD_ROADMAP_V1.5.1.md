# SKETCHUP → CAD 2D — Đề xuất phát triển v2.0.0+

## Mục tiêu
Biến mô hình SketchUp thành linework CAD 2D sạch, có layer/nét/màu hợp lý để chỉnh tiếp trong HNL/AutoCAD và Publish.

## Kiến trúc nên dùng
1. SketchUp Extension `.rbz` làm đầu đọc chính.
2. Extension đọc Scene/Camera/Section Plane/Tags/Components/Transformations/Units.
3. Xuất dữ liệu hình học trung gian HNL JSON, không đọc `.SKP` binary trực tiếp trong HNL nếu chưa có SDK phù hợp.
4. HNL Projector chiếu 3D → 2D theo camera/section.
5. Classifier chia nét:
   - CUT: nét mặt cắt, ưu tiên đậm.
   - VISIBLE: nét thấy.
   - HIDDEN: nét khuất, linetype hidden.
   - SILHOUETTE: biên ngoài.
   - CONSTRUCTION/HELPER: mặc định không plot.
6. Layer Mapping:
   - SketchUp Tag → CAD Layer.
   - Preset: Kiến trúc / Trần / Vách / Hoàn thiện / MEP / Shopdrawing.
7. Styling:
   - màu ByLayer;
   - lineweight theo loại nét;
   - linetype hidden/center;
   - hatch chỉ sinh khi cần.
8. Cleanup:
   - duplicate;
   - tiny edges;
   - collinear join;
   - gap repair;
   - simplify polyline;
   - flatten Z=0;
   - object quá xa origin;
   - purge layer rỗng.
9. Export:
   - HNL JSON;
   - DXF sạch;
   - Send to AutoCAD Bridge;
   - SaveAs DWG;
   - Prepare for Publish.

## Tính năng nên có trong UI
- Chọn Scene.
- Chọn Section Plane.
- Plan / Elevation / Section / Current View.
- Orthographic mặc định.
- Preview linework trước khi xuất.
- Slider/tolerance cho gap, simplify, hidden-line.
- Layer/Tag Mapping Editor.
- Preset màu/nét.
- Base Point và Rotation.
- Preserve dimensions/text: mặc định OFF trừ annotation chuyên dụng.
- Preview Before/After Cleanup.
- Link ID để Update Existing Import.
- Diff: Added / Updated / Deleted / Unchanged.
- Conflict Resolver nếu CAD và SketchUp cùng sửa.

## Diagnostics bắt buộc
- HNL-SU2CAD-SCENE: không tìm thấy Scene/Camera.
- HNL-SU2CAD-SECTION: Section Plane không hợp lệ.
- HNL-SU2CAD-PROJECT: lỗi projection.
- HNL-SU2CAD-HIDDEN: hidden-line classification vượt giới hạn.
- HNL-SU2CAD-LAYER: mapping layer thất bại.
- HNL-SU2CAD-DXF: lỗi xuất DXF.
- HNL-SU2CAD-BRIDGE: AutoCAD Bridge offline/fail.

Mỗi lỗi phải kèm: scene, section, units, entity count, source file, stage, stack và suggestion.

## Không nên làm
- Không raster hóa view SketchUp thành ảnh nếu mục tiêu là CAD editable.
- Không coi export PDF/SVG là thay thế DXF vector.
- Không tự explode tất cả Component.
- Không tự xóa geometry gốc khi Cleanup.
- Không ghi đè DWG/SKP nguồn.
