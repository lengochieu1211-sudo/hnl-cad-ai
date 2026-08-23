# BUILD HNL CAD AI v2.6.3 thành .EXE

1. Chạy `BUILD_EXE_NOW.bat`, hoặc `npm run dist:win`.
2. Trước build, chạy `node scripts/check-version-sync.mjs`.
3. Installer dự kiến: `dist_electron\HNL_CAD_AI_Setup_2.6.3.exe`.
4. GitHub artifact dùng version lấy trực tiếp từ `package.json`: `HNL-CAD-AI-v2.6.3-Windows-Installer`.
5. AutoCAD bundle artifact: `HNL-CAD-AI-v2.6.3-AutoCAD-Plugin-Bundle`.

Không chỉnh version thủ công ở từng file. Đổi release version phải cập nhật canonical release và để version-sync checker bắt mọi chỗ lệch.
