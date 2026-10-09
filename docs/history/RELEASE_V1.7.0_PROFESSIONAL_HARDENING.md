# HNL CAD AI v2.0.0 — 2D Professional Hardening

## Đã bổ sung
- Geometry Doctor:
  - zero-length line;
  - duplicate line;
  - duplicate polyline vertex;
  - self-intersection cơ bản;
  - missing layer;
  - negative block scale;
  - geometry quá xa origin;
  - Safe Fix có xác nhận.
- Drawing Compare:
  - Added / Deleted / Modified / Unchanged;
  - ưu tiên Link ID, sau đó Handle/ID.
- Smart Cleanup:
  - Safe;
  - Standard;
  - Aggressive;
  - Preview + Confirm.
- Professional Audit Center:
  - Geometry Doctor;
  - Drawing Compare;
  - Publish Readiness;
  - Command Health;
  - Recovery.
- Publish Readiness:
  - geometry errors;
  - layout;
  - viewport lock;
  - layer plottable;
  - drawing content;
  - ghi rõ CTB/Page Setup/Xref/Font native cần AutoCAD Bridge.
- Command Health:
  - số lần chạy;
  - số lần fail;
  - duration;
  - last error;
  - PASS/FAIL/PARTIAL.
- Core Self-Test:
  - Geometry Doctor;
  - Drawing Compare;
  - Cleanup duplicate.
- Recovery Generations:
  - giữ tối đa 5 AutoSave gần nhất.
- Ribbon được tinh giản lại theo Home / Annotate / Blocks / Analyze / Layout / AI / Kỹ thuật.
- SketchUp Bridge đồng bộ v2.0.0.

## Chưa coi là hoàn tất
- Geometry Doctor chưa có đầy đủ Hatch boundary/Spline/Arc topology doctor.
- Drawing Compare chưa tạo Revision Cloud tự động.
- Publish Readiness chưa đọc CTB/Xref/Page Setup/Font từ DWG binary Standalone.
- Recovery UI hiện xem các thế hệ; restore thủ công từng generation chưa được nối nút để tránh thay workspace ngoài ý muốn.
- Large Drawing spatial index/quadtree chưa triển khai.
- True SketchUp section contour/occlusion vẫn cần engine sâu hơn.

## Nguyên tắc
Không báo PASS giả cho chức năng phụ thuộc AutoCAD/SketchUp hoặc geometry kernel chưa đủ chắc chắn.
