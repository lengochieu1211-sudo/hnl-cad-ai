# FINAL AUDIT — HNL CAD AI v2.0.0

## PASS tĩnh
- 67 TypeScript/TSX files: transpile syntax PASS.
- Electron main/preload: node syntax PASS.
- SketchUp Ruby loader/main: syntax PASS.
- SketchUp RBZ: integrity PASS.

## Plot/Publish đã có
- Model PDF.
- Windows printer list.
- In trực tiếp.
- Plot preset.
- Per-layer plot overrides.
- Multi-sheet PDF.
- Viewport-aware crop khi có viewport nội bộ chính.
- Batch Page Setup.
- HNL Sheet Set JSON.
- Sheet Set preflight.
- Paging 100 sheet/trang.

## Sheet Set
- HNL Sheet Set: đọc/chỉnh/lưu.
- AutoCAD `.DST`: nhận diện, không parse/sửa giả.
- `.DST` native cần AutoCAD Bridge SheetSet API.

## UI
- Home
- Annotate
- Blocks
- Analyze
- SketchUp
- Layout
- Publish
- AI
- Kỹ thuật

Các chức năng phụ đưa vào Công cụ để Ribbon không bị quá tải.

## Cần test thực tế trên Windows
1. Build EXE.
2. getPrintersAsync trên máy có 1/3/10 printer.
3. PDF A4/A3/A1.
4. In printer thật.
5. 10/100/500 sheet.
6. Layout có viewport 1:50/1:100.
7. HNL Sheet Set save/open.
8. `.DST` khi AutoCAD Bridge offline/online.
9. Driver PDF khác nhau.
10. File lớn 50k/100k entities.

## Giới hạn
- CTB/STB/PC3/PMP/Page Setup/Xref/Font DWG native chưa thể xác nhận hoàn toàn trong Standalone.
- Layout nhiều viewport phức tạp nên Native Publish qua AutoCAD.
