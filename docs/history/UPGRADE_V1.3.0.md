# HNL CAD AI v1.3.0 – Professional Desktop UI Upgrade

## Đã chỉnh
- Gom 16 tab Ribbon thành 6 nhóm nghiệp vụ chính, vẫn giữ toàn bộ lệnh cũ qua sub-tab.
- Thanh trên gọn hơn: Project + Công cụ + AI Palette; các công cụ ít dùng đưa vào menu Công cụ.
- Tăng kích thước chữ điều hướng chính, giảm tình trạng chật trên laptop.
- Status bar kiểu CAD: tọa độ, SNAP/ORTHO/GRID, đơn vị, trạng thái AutoCAD Bridge động.
- Giữ Workbench selector cho các luồng CAD/Trần/Vách/Shopdrawing/BOQ/Layout/AI.
- Nâng version đồng bộ lên 1.3.0.

## Nguyên tắc chức năng
- Không xóa chức năng cũ; chỉ tái cấu trúc đường truy cập.
- Standalone vẫn dùng các engine nội bộ.
- Các thao tác DWG/AutoLISP native vẫn cần AutoCAD Bridge thật.

## Build Windows
Chạy `build-exe.bat` trên Windows 10/11 x64 hoặc GitHub Actions. Installer dự kiến: `HNL_CAD_AI_Setup_1.3.0.exe`.
