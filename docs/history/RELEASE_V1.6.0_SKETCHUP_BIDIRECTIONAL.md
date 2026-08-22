# HNL CAD AI v2.0.0 — SketchUp ⇄ CAD 2D

## Đã triển khai
- SketchUp Extension `.rbz`.
- Export Scene/Camera/Section metadata/Tags/nested Edge geometry từ SketchUp.
- Persistent Link ID trên SketchUp Edge.
- HNL Projector 3D edge → 2D camera basis.
- Cảnh báo Perspective; khuyến nghị Parallel Projection.
- HNL CAD → SketchUp Import/Update.
- CAD 2D → SketchUp JSON package.
- SketchUp → CAD 2D workspace import.
- SketchUp → DXF 2D ASCII export.
- Rule-based Tag → Layer mapping.
- AI Tag → Layer/Color/Lineweight suggestion endpoint.
- AI fallback khi offline.
- Diff Link ID: Added / Updated / Deleted / Unchanged.
- Cleanup/Preflight/Diagnostics.
- Native SketchUp Pro DXF export menu.
- Help Center cập nhật.

## Chưa được giả nhận là hoàn tất
- True section CUT contour: `sectionCrossing` chỉ đánh dấu cạnh đi qua plane, không phải contour cắt.
- True occlusion hidden-line từ toàn bộ mesh/component hierarchy.
- Silhouette classifier chính xác.
- Conflict resolver hai chiều khi cùng sửa một Link ID.
- Tạo Component đầy đủ từ CAD Block vẫn còn giới hạn.
- DWG native Standalone vẫn phụ thuộc AutoCAD Bridge hoặc exporter tương ứng.

## Ưu tiên tiếp
1. Section contour engine dùng SketchUp geometry API.
2. Occlusion test theo camera với batch/ray strategy.
3. Link-ID conflict resolver.
4. Layer Mapping Editor có Apply/Save Preset.
5. Large-model performance tests 10k / 50k / 100k edges.
