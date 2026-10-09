# HNL CAD AI v1.0.1 - Logic/UI Audit

## Đã sửa
- Thống nhất thương hiệu HNL; không còn chuỗi An Phu/An Phú trong source chính.
- Chuyển trạng thái giả `AutoCAD Bridge Connected` thành trạng thái thực: Standalone, plugin AutoCAD chưa kết nối.
- Thống nhất mục tiêu AutoCAD 2023+ thay vì các nhãn 2020-2026 không đúng phạm vi.
- Lệnh chưa triển khai không còn báo giả `Đã chạy lệnh CAD`; hiện báo rõ chưa hỗ trợ trong Standalone.
- AutoLISP trong Standalone không còn báo đang chạy thật; yêu cầu AutoCAD plugin.
- AutoCAD Table trong Standalone đổi thành bảng HNL nội bộ; chèn AutoCAD Table thật cần plugin.
- Auto Detail Composer không còn ghi Apply trực tiếp vào AutoCAD khi chưa có bridge.
- Menu Electron File/Edit/AI đã nối với ứng dụng: New, Save JSON, Export, Print, Undo/Redo, Select All, Delete, Auto Detail, AI Palette, Audit.
- Menu Open DXF/JSON đã có xử lý thật. DXF ASCII cơ bản hỗ trợ LINE, CIRCLE, TEXT, MTEXT, LWPOLYLINE.
- DWG vẫn không đọc native; mở bằng AutoCAD/app mặc định. Đây là hành vi chủ ý và đúng logic.
- Thêm lệnh vẽ Circle thật bằng 2 điểm (tâm + bán kính) trên Canvas.
- Cải thiện layout: top bar có horizontal overflow an toàn; search width responsive; palette phải thu nhỏ hơn; status dưới ẩn trên màn hình hẹp để không che canvas.
- Listener Electron có cleanup để tránh đăng ký lặp.

## Chức năng Standalone có logic thực
- Canvas CAD nội bộ, chọn đối tượng, pan/zoom, OSNAP/Dynamic input nền tảng.
- LINE, RECTANGLE, CIRCLE, WALL 100/200, MLeader cơ bản.
- Smart Object/Project Tree/Property/Dependency/Spreadsheet ở mức dữ liệu nội bộ.
- Layout/Viewports nội bộ.
- Export DXF/CSV/PDF theo engine hiện có.
- Import DXF ASCII cơ bản và HNL JSON.
- Undo/Redo theo history entity.
- AI/Knowledge modules tùy provider và logic có sẵn.

## Chức năng cần plugin AutoCAD / kiểm thử riêng
- DWG native read/write.
- AutoLISP execution trong AutoCAD.
- AutoCAD Table thật.
- NETLOAD/.bundle Ribbon trong AutoCAD.
- Gọi AutoCAD API để tạo Layout/Viewport/Field trực tiếp vào DWG.
- Đồng bộ hai chiều Standalone <-> AutoCAD.

## Nút chưa triển khai
Các command chưa có implementation thật được giữ an toàn: khi bấm sẽ thông báo `chưa được triển khai trong bản Standalone hiện tại`, không báo thành công giả.

## Kiểm tra build
- `tsc --noEmit` đã parse source nhưng môi trường hiện thiếu node_modules nên báo thiếu React/Express/Vite typings; không phát hiện lỗi cú pháp từ các thay đổi audit trước khi dừng ở dependency resolution.
- Cần `npm install` thành công rồi chạy `npm run lint` và `npm run dist:win` trên Windows/GitHub Actions để xác nhận installer thực tế.

## Mục tiêu hệ điều hành
- Windows 10 22H2 x64
- Windows 11 x64
- AutoCAD integration: AutoCAD 2023+; DLL cần build theo runtime/API phù hợp từng nhóm phiên bản.
