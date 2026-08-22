# HNL CAD AI v2.3.0 — FULL SOURCE REPLACE

## Mục đích
Bộ này dùng để **chép đè toàn bộ source repo** thay vì vá từng file.

Đã gộp:
- sửa TypeScript GitHub Actions;
- bỏ `build.win.publisherName` không hợp lệ với electron-builder 26;
- thêm `author` đúng vị trí;
- bỏ `import.meta.url` khỏi `server.ts` khi bundle CommonJS;
- giữ 2D Professional / 44 Lisp → 17 công cụ chuẩn;
- AutoCAD Bridge;
- SketchUp Bridge;
- Plot / Publish / Sheet Set;
- Diagnostics / Recovery / Revision.

## Cách dùng nhanh nhất
1. Giải nén ZIP.
2. Chạy `REPLACE_WHOLE_REPO.bat`.
3. Nhập đường dẫn repo local, ví dụ:
   `D:\GitHub\hnl-cad-ai-26`
4. Script giữ nguyên `.git` và chép đè source.
5. Chạy:
   `git add .`
   `git commit -m "Update HNL CAD AI v2.3.0 full source"`
   `git push`

GitHub Actions sẽ tự build lại.

## Lưu ý
Không chép `node_modules`, `dist`, `dist_electron` vào GitHub.
