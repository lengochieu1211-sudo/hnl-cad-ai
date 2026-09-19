# HNL CAD AI v2.7.8 — Bundled 44 Lisp Source Pack

## Nguồn
Đã nhận trực tiếp `AI.rar` của người dùng.

Archive metadata:
- 45 file tổng cộng.
- 44 `.lsp`.
- 1 `GeomProps2021x64.arx`.
- SHA256: `7a34fadfe6d4afb7b61eb41e47c2b6b0c64742f0b22850c60e6050d5e5b84f0e`

## Thay đổi chính
1. `resources/legacy-lisp/AI.rar` giữ nguyên archive người dùng.
2. `legacy-lisp-manifest.json` ghi đủ 44 tên Lisp + kích thước archive.
3. `scripts/prepare-legacy-lisp.mjs` tự giải nén trên Windows bằng 7-Zip/WinRAR.
4. Build fail nếu không đủ chính xác 44 Lisp + 1 ARX.
5. Sau giải nén, script scan `(defun c:COMMAND)` và sinh `legacy-lisp-index.json`.
6. electron-builder đóng thư mục Lisp đã giải nén vào `resources/legacy-lisp` của installer.
7. HNL Lisp Center tự index bộ tích hợp; người dùng không cần chọn folder AI.rar thủ công.
8. Nút `Nạp` / `Nạp + Chạy` tiếp tục dùng `LOAD_LISP_FILE` qua AutoCAD Bridge.
9. Lisp bổ sung ngoài bộ 44 vẫn có thể nạp file/thư mục riêng.

## ARX
`GeomProps2021x64.arx` là binary legacy AutoCAD 2021 x64.
- giữ trong resource/source pack;
- không auto-load;
- không tuyên bố tương thích AutoCAD 2023-2026 nếu chưa có runtime test/build tương ứng.

## Build
GitHub Actions Windows có gate:
- `npm run prepare:lisp`
- đúng 44 `.lsp`
- đúng 1 `.arx`
- runtime index lispCount = 44

## Kiểm tra tại môi trường hiện tại
RAR metadata đọc được và xác nhận 44 Lisp + 1 ARX.
Do container Linux không có RAR5 extractor, nội dung `.lsp` được giải nén ở Windows build gate, không giả vờ đã extract ở đây.
