# HNL CAD AI v2.7.3 — GitHub AutoCAD Build Fix + Official Logo/Icon

## Nguồn lỗi
Đã đọc `logs_88397058580.zip`.

GitHub Actions build AutoCAD 2023/2024/2025/2026 cùng fail bởi 2 lỗi:

1. `BridgeCommands.cs(171,9) CS0103`
   - Sai: `NativePalette.OpenManagerWindow("LIBRARY")`
   - Đúng: `NativePaletteCommands.OpenManagerWindow("LIBRARY")`

2. `NativeRibbon.cs(325,13) CS0104`
   - `Application` ambiguous giữa:
     - `Autodesk.AutoCAD.ApplicationServices.Application`
     - `System.Windows.Application`
   - Sửa bằng fully-qualified AutoCAD Application tại điểm gửi command.

Các warning nullable trong log không phải nguyên nhân build fail.

## Logo / Icon
Nguồn duy nhất: file người dùng tải lên `HNL LOGO 2.png`.

SHA256 nguồn:
`9344acd58cb8c3fb5cd1ec688d920062cf3e8a867ee0c2fc4224281af5a946eb`

Đã áp dụng:
- `public/hnl-logo.png` — copy byte-for-byte từ ảnh tải lên.
- `electron/icon.png` — copy byte-for-byte từ ảnh tải lên.
- `public/favicon.png` — copy từ ảnh tải lên.
- `electron/icon.ico` — ICO đa kích thước 16/24/32/48/64/128/256, tạo từ ảnh tải lên.
- `public/favicon.ico` — cùng nguồn.
- `index.html` — favicon + apple-touch-icon.
- `HnlLogo.tsx` — hiển thị ảnh chính thức trực tiếp, bỏ lớp glow/badge giả bọc ngoài.
- electron-builder tiếp tục dùng `electron/icon.ico`, nên EXE/installer/shortcut sẽ dùng icon mới sau khi build lại.

## Version
Toàn bộ current runtime/build metadata đồng bộ v2.7.3.
`@google/genai` giữ nguyên 2.4.0.

## Kiểm tra
- HNL version sync: PASS.
- TS/TSX syntax: PASS (84 files).
- Electron main/preload syntax: PASS.
- AutoCAD csproj + PackageContents XML: PASS (5 files).
- 2 lỗi đúng theo GitHub log: static regression check PASS.
- Logo UI và electron PNG có SHA256 đúng bằng ảnh tải lên: PASS.
- ICO/Favicon mở được bằng Pillow: PASS.
- ZIP/RBZ integrity: kiểm tra khi đóng gói.

## Chưa thể xác nhận tại môi trường này
Không có Autodesk .NET SDK / AutoCAD runtime trong container nên không thể chạy `dotnet build` thật tại đây.
Bản này sửa đúng hai compiler errors trong log; GitHub Actions tiếp theo là compile gate thực tế.
