# HNL CAD AI v2.4.6 — Layout Rename + Configurable Ceiling Fix

## 1. Lỗi đổi tên Layout
Audit v2.4.5 cho thấy bottom Layout tabs chỉ có `setActiveLayout(layout)`.
Không có handler Rename, vì vậy sửa tên Layout thực tế chưa được triển khai.

### Đã sửa Standalone
- Double-click Layout tab → Rename.
- Right-click Layout tab → Rename.
- Có nút Pencil cạnh nút `+` khi đang ở Paper Space.
- Kiểm tra:
  - không rỗng;
  - không được là `Model`;
  - không trùng tên;
  - chặn ký tự Layout không hợp lệ.
- Rename cập nhật đồng thời:
  - `layouts`;
  - `activeLayout`;
  - `viewport.layoutName`.

### Đã sửa AutoCAD Native
Bridge actions mới:
- `GET_LAYOUTS` trả thêm handle + currentLayout.
- `SET_CURRENT_LAYOUT`.
- `RENAME_LAYOUT`.

Khi AutoCAD Connected:
- bottom Layout tabs đồng bộ từ DWG thật mỗi 8 giây;
- click tab → đổi CurrentLayout trong AutoCAD;
- rename → `LayoutManager.Current.RenameLayout(...)`;
- không còn đổi tên một Layout giả trong HNL rồi DWG không đổi.

Command native mới:
- `HNLRENLAYOUT`

AutoCAD Native Palette → tab Layout có nút `Đổi tên / Rename`.

## 2. Lỗi Smart Ceiling không có tùy chọn
Audit v2.4.5 cho thấy `SMART_CEILING` tạo thẳng CEILING_GRID bằng preset cứng:
- Main @800
- Cross @400
- Hanger @1000
- Rotation 0

Nút không mở cấu hình trước khi vẽ.

### Đã sửa workflow
`Smart Ceiling` giờ luôn mở tab `AI Ceiling Grid Optimizer`.

Người dùng chọn:
- hệ trần chìm / trần nổi từ Knowledge Base;
- xương chính @;
- xương phụ @;
- ty treo @;
- góc xoay;
- cao độ trần;
- kích thước tấm/module X/Y;
- gốc cân tâm / bắt từ biên / custom offset;
- Offset X/Y;
- có/không vẽ vị trí ty treo;
- yêu cầu kiểm tra né MEP/đèn;
- khoảng viền tường;
- chiến lược cân tâm / trục / lighting.

Đổi hệ trần sẽ tự nạp spacing theo hệ trong Knowledge Base nhưng người dùng vẫn chỉnh lại được.

## 3. Tạo trần AutoCAD native
Bridge action mới:
- `CREATE_CEILING_GRID`

Workflow:
1. Chọn một LWPOLYLINE kín trong AutoCAD.
2. Mở Smart Ceiling.
3. Chỉnh spacing / rotation / origin.
4. `Tạo Trần Theo Tùy Chọn`.
5. Plugin:
   - đọc Polyline kín;
   - chuyển polygon sang hệ tọa độ theo góc xoay;
   - tạo grid;
   - clip từng xương theo polygon;
   - tạo xương chính;
   - tạo xương phụ;
   - tạo điểm ty treo dạng Circle nếu bật;
   - ghi trực tiếp vào Model Space DWG.

Layer native:
- `HNL_CEILING_MAIN`
- `HNL_CEILING_CROSS`
- `HNL_CEILING_HANGER`

Command AutoCAD mới:
- `HNLCEILING`
  - chọn Polyline kín;
  - chọn Trần Chìm/Nổi;
  - nhập Main/Cross/Hanger spacing;
  - nhập góc xoay;
  - tạo trực tiếp trong DWG.

AutoCAD Native Palette tab 2D/Draw có:
- `Trần tùy chọn / Ceiling options`
- `Mở Studio / HNL Manager`

## 4. Standalone
Nếu không có AutoCAD:
- chọn Polyline kín hoặc Rectangle;
- cấu hình cùng bộ thông số;
- tạo một `CEILING_GRID` nội bộ có đầy đủ spacing, hanger, angle, level, panel size, origin offsets.

## 5. Giới hạn minh bạch
- Checkbox `Yêu cầu né MEP / đèn` hiện là yêu cầu phối hợp/audit; grid native v2.4.6 chưa tự động cắt xương quanh mọi MEP object.
- `wallAngleOffset` và `levelElevation` được lưu trong cấu hình/metadata; engine 2D Model Space hiện tập trung tạo framing plan.
- AutoCAD DLL cần GitHub Actions Windows compile/runtime test.

## Static validation
- TS/TSX syntax transpile.
- Electron syntax.
- AutoCAD csproj / PackageContents XML.
- Action mapping/capability static checks.
- ZIP/RBZ integrity.
