# HNL CAD AI v2.8.0 — AutoCAD Runtime Golden Checklist

## Chuẩn bị
- Đóng toàn bộ AutoCAD trước khi cài/copy plugin.
- Cài HNL CAD AI v2.8.0.
- Dùng một DWG test có: Line/Polyline/Text/MText/Block Attribute/Dynamic Block/Field/Xref/Layout/Viewport.
- Tạo bản copy DWG để test destructive actions.

## A. Startup / Bridge
- [ ] Mở HNL EXE không JavaScript error / EADDRINUSE.
- [ ] Mở AutoCAD 2023.
- [ ] `HNLVERSION` in đúng v2.8.0 và đúng DLL path.
- [ ] `HNLBRIDGEPING` = pairing OK.
- [ ] Ribbon HNL: AI / Shopdrawing / 2D Pro / Data-BOQ / Layout / Tools.
- [ ] Native Palette: Text / Block-Attribute / Field / Geometry / Dimension+ / Layer / BOQ / Shopdrawing / Layout+.

## B. Start Center / DWG
- [ ] Mở DWG bằng AutoCAD + HNL.
- [ ] Ctrl+S lưu DWG native.
- [ ] Ctrl+Shift+S Save As DWG.
- [ ] Advanced: HNL Canvas preview không ghi đè DWG.
- [ ] Direct DWG snapshot hiển thị unsupported/truncated rõ nếu có.

## C. 2D Pro
- [ ] MTEXT mở HNL dialog, không còn `prompt() is not supported`.
- [ ] Rename Layout mở HNL dialog.
- [ ] MOVE/ROTATE/SCALE/OFFSET dialog chạy trong Standalone.
- [ ] Direct DWG MOVE/ROTATE/SCALE dialog chạy và refresh snapshot.
- [ ] Dim+ chạy Golden: DN/DNC, JD, TKD.
- [ ] Field Doctor kiểm tra Field/ObjectID.
- [ ] Layer center mở đúng từ `HNLLAYER`.
- [ ] Block/Attribute center mở đúng từ `HNLBLOCK`.

## D. 44 Lisp
- [ ] Lisp Center hiện 44/44.
- [ ] `HNLLISPSTATUS` báo ON_DEMAND.
- [ ] TKL: load đúng file, tạo Layout theo test case.
- [ ] TKT: load đúng file, thống kê và xuất bảng đúng test case.
- [ ] BRK: load đúng file, break polyline test.
- [ ] DN/DNC: load đúng file, Dim test.
- [ ] Một Lisp có Excel/DCL: dependency lỗi phải báo rõ ở AutoCAD Command Line.
- [ ] Không auto-load `GeomProps2021x64.arx`.

## E. Library
- [ ] Import một DWG có 1 block definition.
- [ ] Import một DWG có nhiều block definitions → bắt chọn definition.
- [ ] Bấm Chèn → HNL trả ngay → AutoCAD prompt điểm → click → block xuất hiện.
- [ ] Dynamic Block đọc/sửa property.
- [ ] ESC ở bước pick point không treo Bridge.

## F. Shopdrawing / BOQ
- [ ] Smart Ceiling chạy trên boundary kín.
- [ ] Smart Wall chạy với 1220/3 và 1220/2 theo setting.
- [ ] Audit nhận HNL XData và cảnh báo scale block.
- [ ] GET_HNL_BOQ khớp số entity HNL đã tạo.
- [ ] TKT benchmark riêng với geometry thủ công.

## G. Layout / Publish
- [ ] Chuyển Model/Layout từ HNL phản ánh đúng AutoCAD.
- [ ] Rename Layout cập nhật cả HNL và DWG.
- [ ] TKL Golden: khung → Layout → viewport → scale → title block → page setup.
- [ ] PLOT_CURRENT_PDF chạy >12 s vẫn không false-timeout.
- [ ] PUBLISH_LAYOUTS_PDF nhiều layout hoàn tất trong timeout mới.

## H. AI
- [ ] Chọn Gemini/OpenAI/... nhưng chưa có key → báo lỗi, KHÔNG tự chuyển Offline.
- [ ] Bật `Cho phép chuyển Offline khi AI lỗi` → lúc đó fallback mới được phép.
- [ ] Lưu & kiểm tra provider đúng model.
- [ ] AI AutoLISP Builder online hiển thị code trả về.
- [ ] Preview trước execute vẫn bật mặc định.

## I. Undo / Save / Recovery
- [ ] Ctrl+Z/Ctrl+Y Standalone.
- [ ] Ctrl+Z/Ctrl+Y Direct/Native DWG.
- [ ] Library/Shopdrawing có thể Undo theo AutoCAD.
- [ ] Save, đóng, mở lại DWG không mất dữ liệu.
- [ ] AutoSave recovery hoạt động sau crash test.

## J. Exit criteria
Một workflow chỉ đổi REVIEW → VERIFIED khi: source PASS + GitHub compile PASS + AutoCAD runtime PASS + expected geometry/data PASS + Undo/Save PASS + regression cluster PASS.
