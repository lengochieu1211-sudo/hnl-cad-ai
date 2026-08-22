# HNL CAD AI v2.4.1 — GitHub AutoCAD Plugin Build Fix

## Log đã phân tích
Nguồn: `logs_88294751821.zip`, job `Build AutoCAD plugins 2023-2026`.

Job dừng tại AutoCAD 2023 với 5 lỗi compile:
- `Autodesk.AutoCAD.PublishingServices` không tồn tại.
- `System.Net.Http` không được resolve cho target `net48`.
- Kéo theo `HttpClient`, `HttpMethod`, `HttpRequestMessage` không tìm thấy.

## Nguyên nhân
1. `DsdEntry`, `DsdData`, `DsdEntryCollection` thuộc `Autodesk.AutoCAD.PlottingServices`.
   Source đã import `PlottingServices` đúng nhưng đồng thời import thêm namespace
   `PublishingServices` không tồn tại trong managed API package hiện dùng.
2. AutoCAD 2023/2024 target .NET Framework 4.8. SDK-style CI cần reference rõ
   `System.Net.Http` để compiler resolve các type HTTP.

## Đã sửa
- Xóa `using Autodesk.AutoCAD.PublishingServices;`.
- Thêm framework reference `System.Net.Http` với `Private=false` vào project 2023 và 2024.
- Không copy một System.Net.Http NuGet DLL riêng vào AutoCAD plugin bundle để giảm nguy cơ assembly conflict.
- Workflow build cả 4 phiên bản và gom lỗi, thay vì dừng ngay ở phiên bản đầu tiên.
- Bump version/plugin/bundle/artifact lên `2.4.1`.

## Kiểm tra tĩnh
- XML csproj + PackageContents parse PASS.
- Không còn namespace `Autodesk.AutoCAD.PublishingServices`.
- 2023/2024 có explicit `System.Net.Http` reference.
- Workflow YAML có aggregate failure reporting.
- TypeScript/JS audit giữ nguyên từ nhánh v2.4.0.

## Cần xác nhận trên GitHub Actions
Môi trường hiện tại không có Windows AutoCAD SDK runtime/.NET SDK nên DLL AutoCAD
không thể compile trực tiếp tại đây. GitHub Actions là bước xác nhận compile thực tế.
Sau patch này, nếu còn lỗi API riêng của 2025/2026, workflow mới sẽ trả về toàn bộ
phiên bản lỗi trong cùng một run để sửa nhanh hơn.
