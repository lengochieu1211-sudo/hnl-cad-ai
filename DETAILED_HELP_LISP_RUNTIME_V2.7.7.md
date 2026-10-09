# HNL CAD AI v2.7.7 — Detailed Help + Legacy Lisp Runtime

## Vấn đề
- Help Center trước đây chỉ có vài dòng tóm tắt.
- Tab `44 Lisp nguồn` chỉ hiển thị catalog 44 mục; source package không chứa file `.lsp`, nên không thể LOAD/Run.
- Audit cũ cũng xác định đây là danh mục workflow/nguồn tham khảo, chưa phải Lisp runtime. fileciteturn19file12

## v2.7.7
### Help Center
Phần `2D Professional + 44 Lisp nguồn` được viết lại chi tiết:
- điều kiện trước khi chạy;
- giải thích Native / Hybrid / AutoCAD;
- cách nạp folder AI.zip sau khi giải nén;
- cách xác nhận command;
- LOAD và LOAD + RUN;
- SECURELOAD / Trusted Locations;
- DCL / Excel / COM / dependency;
- Unknown command;
- selection;
- Undo và bảo vệ DWG.

### 44 Lisp nguồn
Electron mới:
- `select-lisp-files`
- `select-lisp-folder`
- scan recursive `.lsp`
- parse `(defun c:COMMAND)`.

UI:
- Nạp file Lisp
- Nạp thư mục Lisp
- CHƯA CÓ / ✓ file
- Hướng dẫn chi tiết từng Lisp
- Nạp
- Nạp + Chạy

AutoCAD Bridge:
- `LOAD_LISP_FILE`
- chỉ nhận `.lsp` có thật;
- escape path;
- LOAD bằng AutoLISP;
- optional command name được kiểm tra ký tự an toàn;
- Command Line AutoCAD là nguồn xác nhận cuối.

## Quan trọng
Bộ FULL SOURCE v2.7.7 vẫn không tự nhúng 44 file Lisp gốc vì source hiện hành không chứa các file đó.
Người dùng trỏ HNL tới thư mục Lisp đã giải nén; HNL sẽ lập chỉ mục và chạy file thật qua AutoCAD.
