# HNL CAD AI v2.7.6 — Clean Start Center

## Vấn đề từ ảnh v2.7.5
Start Center đang đưa 6 ô cùng cấp:
- Tiếp tục Workspace
- Direct DWG Edit • HNL
- Mở DWG • AutoCAD Native
- Mở DWG • HNL Canvas
- Mở dự án / DXF / DWG
- Bản vẽ mới

Điều này làm người dùng tưởng HNL có nhiều cách mở DWG tương đương và phải hiểu kiến trúc kỹ thuật trước khi bắt đầu.

## Giao diện mới
Chỉ còn 4 hành động chính:
1. Tiếp tục
2. Mở DWG bằng AutoCAD + HNL — khuyên dùng
3. Mở DXF / Dự án HNL — HNL mở trực tiếp
4. Bản vẽ mới

## Tùy chọn DWG nâng cao
Mặc định đóng:
- Điều khiển DWG từ HNL — cần AutoCAD Bridge
- Xem DWG nhanh trên HNL Canvas — AutoCAD chuyển sang DXF tạm

Không còn đặt `Direct DWG`, `AutoCAD Native`, `HNL Canvas` thành 3 nút ngang hàng ở màn hình bắt đầu.

## Trạng thái
4 card Safe Mode / Recovery / Project / Mode được thay bằng một dòng trạng thái nhỏ:
- Safe Mode
- AutoSave
- Project
- Mode

## File menu
Cũng đồng bộ cách gọi:
- Mở DWG bằng AutoCAD + HNL
- Mở DXF / Dự án HNL
- Tùy chọn DWG nâng cao
  - Điều khiển DWG từ HNL
  - Xem DWG nhanh trên HNL Canvas

## HNL_LOCAL
Electron có mode `HNL_LOCAL`:
- File picker chỉ cho DXF / JSON / LSP
- Không đưa DWG vào đường mở trực tiếp của HNL

## Nhận diện
Start Center dùng logo HNL chính thức đã cập nhật từ ảnh người dùng.

## Kiểm tra
- Version sync
- TS/TSX syntax
- Electron syntax
- XML
- Start Center chỉ còn 4 primary actions
- Advanced DWG collapsed
- HNL_LOCAL filter không có DWG
- File menu đồng bộ
- ZIP/RBZ integrity

Không có Windows/AutoCAD runtime trong container; GitHub Actions + chạy EXE vẫn là runtime gate.
