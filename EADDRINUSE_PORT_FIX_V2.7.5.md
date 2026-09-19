# HNL CAD AI v2.7.5 — EADDRINUSE 32145 Fix

## Lỗi
`Error: listen EADDRINUSE: address already in use 127.0.0.1:32145`

Nghĩa là cổng localhost 32145 đang bị một process khác sử dụng.
Ảnh người dùng đang chạy HNL v2.7.3, là bản còn ép cứng cổng 32145.

## Sửa
- HNL thử 32145.
- Nếu bận: 32146, 32147... tối đa 40 cổng.
- Nếu vẫn bận: xin Windows cấp ephemeral port.
- Ghi cổng thật vào `%TEMP%\HNL_CAD_AI\bridge.json`.
- AutoCAD Bridge đọc bridge.json nên tự đi theo cổng mới.
- server.ts có error handler cho EADDRINUSE, không để thành uncaught JavaScript exception.
- single-instance lock vẫn giữ nguyên.

## Ví dụ
Nếu 32145 đang bận:
- HNL chọn 32146.
- bridge.json ghi `port: 32146`.
- HNL UI tải từ `http://127.0.0.1:32146`.
- AutoCAD Bridge pair tới 32146.

## Kiểm tra
- Version sync PASS
- TS/TSX syntax
- Electron main/preload node --check
- AutoCAD csproj/PackageContents XML
- Dynamic port selection wiring
- bridge.json resolved-port wiring
- EADDRINUSE handler
- ZIP/RBZ integrity

Không có Windows/AutoCAD runtime trong container; GitHub Actions + chạy EXE là runtime gate cuối.
