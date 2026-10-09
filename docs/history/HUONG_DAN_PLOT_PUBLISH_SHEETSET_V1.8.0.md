# Hướng dẫn Plot / Publish / Sheet Set — HNL CAD AI v2.0.0

## 1. Mở Publish Center
Ribbon **Publish → Plot / Publish / Sheet Set** hoặc **Công cụ → Plot / Publish / Sheet Set**.

## 2. Quick Plot từ Model
1. Chọn preset A4/A3/A1.
2. Chọn Orientation và Scale preset.
3. Tab **Printer & Nét in** để chỉnh:
   - Lineweight scale toàn cục;
   - lineweight override từng Layer;
   - color override từng Layer;
   - Monochrome/Color theo preset.
4. Chọn máy in Windows hoặc để Windows Print Dialog.
5. **Xuất PDF** hoặc **In**.

Plot Override chỉ tác động bản in HNL, không sửa Layer gốc.

## 3. Publish nhiều Layout/Sheet
1. Tab **Publish**.
2. Bật/tắt các sheet cần in.
3. Tìm theo số sheet, tên sheet hoặc Layout.
4. Danh sách chỉ render 100 sheet/trang để hạn chế lag.
5. Chọn preset + printer.
6. **Batch Page Setup → sheet bật** để áp cấu hình hàng loạt.
7. **Publish Selected → 1 PDF nhiều trang**.

Nếu Viewport nội bộ có `modelCenter` và `scaleFactor`, HNL crop trang theo viewport chính.
Layout DWG có nhiều viewport phức tạp vẫn nên Native Publish qua AutoCAD.

## 4. Sheet Set
### HNL Sheet Set
- Tạo từ Layout hiện tại.
- Chỉnh tên Sheet Set.
- Chỉnh sheet enable/disable.
- Printer/Plot Style/Paper có thể batch.
- Lưu thành `.hnl-sheetset.json`.
- Mở lại mà không cần load toàn bộ DWG.

### AutoCAD `.DST`
HNL nhận diện `.DST` nhưng không tự parse/sửa binary/proprietary data khi AutoCAD Bridge offline.
Để đọc/sửa native đúng chuẩn cần AutoCAD Bridge có SheetSet API.

## 5. Máy in
Khi chạy EXE Electron, HNL lấy danh sách bằng Windows/Electron `getPrintersAsync`.
Có thể chọn printer thật rồi gửi job in.
Nếu danh sách trống:
- kiểm tra driver;
- kiểm tra Windows Print Spooler;
- mở Diagnostics.

## 6. Nét in
Standalone:
- Color/Monochrome;
- lineweight Layer;
- lineweight scale;
- preview SVG/PDF.

AutoCAD Connected:
- nên bổ sung/đọc CTB/STB;
- PC3/PMP;
- Page Setup;
- plot device/media native.

Không dùng AI tự chỉnh CTB hoặc lineweight kỹ thuật.

## 7. Preflight
Publish Center kiểm tra:
- Sheet trùng số;
- paper thiếu;
- printer không tồn tại;
- viewport chưa lock;
- sheet không có viewport;
- trạng thái READY/CHECK/ERROR.

Professional Audit Center tiếp tục kiểm tra Geometry/Layout/Viewports.
CTB/Xref/Font/Page Setup DWG native phải được AutoCAD Bridge xác nhận.

## 8. Mã lỗi
- `HNL-PLOT-PDF-*`: xuất PDF lỗi.
- `HNL-PLOT-PRINT-*`: máy in/driver lỗi.
- `HNL-PUBLISH-PDF-*`: PDF nhiều trang lỗi.
- `HNL-SHEETSET-OPEN/SAVE`: HNL Sheet Set lỗi.
- `HNL-SHEETSET-DST`: `.DST` cần AutoCAD Bridge.
- `HNL-PAGESETUP-BATCH`: Batch Page Setup.

Khi lỗi hãy Copy Diagnostic Report và gửi cùng file mẫu.
