# HNL CAD AI v2.7.9 — Auto Load 44 Lisp

## Cách hoạt động
- GitHub Windows build giải nén đúng AI.rar của người dùng.
- Build fail nếu không đúng 44 `.lsp` + 1 legacy `.arx`.
- 44 `.lsp` được copy trực tiếp vào:
  `HNL.CadBridge.bundle/Contents/Lisp`
- `GeomProps2021x64.arx` KHÔNG được copy vào bundle AutoCAD 2023–2026.

## Auto Load
AutoCAD Bridge chạy `OnIdle`:
1. lấy active Document;
2. tìm `Contents/Lisp`;
3. nếu document này chưa được nạp và AutoLoad đang ON;
4. queue `(vl-catch-all-apply 'load ...)` cho từng file;
5. đánh dấu document đã queue.

AutoLISP là document-scoped, nên mở DWG mới sẽ tự LOAD lại cho document mới.

## Không tự chạy lệnh
Auto Load chỉ LOAD định nghĩa Lisp.
HNL KHÔNG tự gọi BRK/TKL/QQ/... sau khi load.

## Lệnh quản lý
- `HNLLISPSTATUS` — xem ON/OFF, folder, số file, document đã load hay chưa.
- `HNLLISPRELOAD` — cưỡng bức nạp lại 44 Lisp cho document hiện tại.
- `HNLLISPAUTOON` — bật Auto Load trong phiên AutoCAD.
- `HNLLISPAUTOOFF` — tắt Auto Load trong phiên AutoCAD.

## An toàn
- Mỗi file dùng `vl-catch-all-apply`, một Lisp lỗi không chặn 43 file còn lại.
- AutoCAD Command Line vẫn là nguồn xác nhận lỗi từng Lisp.
- Legacy `GeomProps2021x64.arx` không auto-load.
