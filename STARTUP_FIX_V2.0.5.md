# HNL CAD AI v2.3.0 — Windows Packaged Startup Fix

Lỗi v2.0.4:
`ENOENT: no such file or directory, chdir '...\\resources\\app.asar'`

Nguyên nhân: `app.getAppPath()` trả về `resources/app.asar` trong bản đóng gói.
`app.asar` là archive file, không phải thư mục thật, nên `process.chdir(appRoot)` bị lỗi.

Sửa:
- bỏ `process.chdir(appRoot)`;
- đặt `HNL_APP_ROOT = app.getAppPath()`;
- server Production lấy `dist` từ `HNL_APP_ROOT`;
- giữ `require(app.asar/dist/server.cjs)` bằng absolute ASAR path.

Cần test sau build:
1. Cài vào Program Files.
2. Mở từ Desktop shortcut.
3. Mở từ Start Menu.
4. Mở trực tiếp EXE.
5. Kiểm tra giao diện và `/api/health`.
