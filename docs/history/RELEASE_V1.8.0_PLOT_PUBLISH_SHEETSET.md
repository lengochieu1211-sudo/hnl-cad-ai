# HNL CAD AI v2.0.0 — Plot / Publish / Sheet Set Professional

## Đã triển khai
- Ribbon mới theo workflow:
  Home / Annotate / Blocks / Analyze / SketchUp / Layout / Publish / AI / Kỹ thuật.
- Publish Center riêng.
- Quick Plot Model → PDF.
- Model → Windows Printer.
- Lấy danh sách máy in thật từ Electron/Windows.
- Printer selection + copies.
- Plot preview SVG vector.
- Monochrome/Color preset.
- Lineweight scale.
- Per-Layer plot color/lineweight override, không sửa layer gốc.
- Multi-Sheet PDF.
- Viewport-aware crop cho sheet có viewport nội bộ chính.
- HNL Sheet Set JSON.
- Create Sheet Set từ Layout.
- Mở/lưu HNL Sheet Set.
- Nhận diện `.DST`; không parse giả khi AutoCAD Bridge offline.
- Sheet Set Preflight.
- Duplicate sheet number check.
- Printer existence check.
- Batch Page Setup.
- Search + paging 100 sheet/trang để giảm lag.
- Diagnostic codes cho Plot/Print/Publish/SheetSet.

## Giới hạn rõ ràng
- Standalone không thể đảm bảo CTB/STB/PC3/PMP/Page Setup native của DWG.
- Multi-viewport Layout phức tạp nên Publish bằng AutoCAD native.
- `.DST` native chưa đọc/sửa trực tiếp nếu AutoCAD Bridge chưa có SheetSet API.
- Xref/font plot dependency trong DWG cần AutoCAD Bridge.
- PDF standalone render các entity HNL hỗ trợ; entity DWG chưa import vào HNL sẽ không xuất hiện.

## Tiếp theo
- AutoCAD Native Plot Bridge.
- CTB/STB editor/preview native.
- PC3/PMP/media query.
- `.DST` SheetSet API Bridge.
- Publish Queue có progress/cancel/resume.
- Revision + issue report per sheet.
