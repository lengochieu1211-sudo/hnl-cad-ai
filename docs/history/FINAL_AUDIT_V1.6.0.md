# FINAL AUDIT — HNL CAD AI v2.0.0

## PASS tĩnh
- 58 TypeScript/TSX files transpile syntax PASS.
- Electron main/preload node syntax PASS.
- SketchUp loader/main Ruby syntax PASS.
- RBZ ZIP integrity PASS.
- API SketchUp AI mapping có offline fallback.
- CAD→SketchUp và SketchUp→CAD đều có Diagnostics.
- SketchUp→CAD có Project workspace + DXF 2D.
- AI không tự quyết CUT/HIDDEN geometry.

## Chức năng thật trong v2.0.0
- Cài SketchUp Extension `.rbz`.
- Export current Scene/Camera/Section metadata/Tags/nested edges.
- CAD→SketchUp Import/Update group bằng Link ID.
- SketchUp→HNL camera-basis 2D projection.
- HNL→DXF 2D.
- Native SketchUp Pro DXF export.
- 2D Cleanup/Preflight.
- AI/rule Layer Mapping.
- Link diff Added/Updated/Deleted/Unchanged.
- Detailed Diagnostics + Help Center.

## Giới hạn phải tiếp tục test thực tế
- Chưa chạy trong một cài đặt SketchUp thật ở môi trường hiện tại.
- `sectionCrossing` không phải section CUT contour thật.
- Edge.hidden? không thay thế occlusion hidden-line.
- Model rất lớn cần benchmark.
- Standalone vẫn không đọc/ghi DWG binary native.

## Test thực tế đề nghị
1. SketchUp model nhỏ 100–1000 edges, Parallel Projection.
2. Model nested Group/Component.
3. Scene có Section Plane.
4. Export JSON → HNL Project → DXF → mở AutoCAD.
5. HNL CAD→SketchUp → Import/Update lần 1 và lần 2.
6. Tag tiếng Việt/không dấu/ký tự đặc biệt.
7. Model 10k/50k/100k edges.
