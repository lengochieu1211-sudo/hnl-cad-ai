# HNL CAD AI v2.7.4 — AutoCAD Ribbon Update / Stale Plugin Hardening

## Ảnh người dùng xác nhận
AutoCAD đang hiện Ribbon HNL cũ với:
AI Settings / Selection / Smart Ceiling / Smart Wall / Library / Polyline /
HNL BOQ / Data-Field / Layers / Properties / Layout Rename / Plot Publish...

Đây KHÔNG phải Ribbon v2.7.2/v2.7.3.

## Nguyên nhân
AutoCAD nạp .NET DLL vào process và không unload khi file bundle trên đĩa bị thay.
Nếu HNL cập nhật bundle trong lúc acad.exe đang chạy, menu cũ vẫn tồn tại đến khi đóng toàn bộ AutoCAD.

Ngoài ra có thể tồn tại nhiều HNL bundle ở:
- %APPDATA%\Autodesk\ApplicationPlugins
- %PROGRAMDATA%\Autodesk\ApplicationPlugins

## v2.7.4 sửa
1. NativeRibbon không còn chấp nhận tab HNL có sẵn là "đã đúng".
   Tab cùng ID sẽ bị remove rồi tạo lại theo định nghĩa hiện tại.
2. HNLRIBBONRESET: cưỡng bức rebuild Ribbon HNL.
3. HNLVERSION: hiện version + AssemblyVersion + đường dẫn DLL thực tế AutoCAD đang load.
4. HNL app kiểm tra version PackageContents packaged/installed.
5. Tự xóa duplicate HNL bundle trong AppData nếu có.
6. Bundle ở ProgramData được phát hiện và báo cần quyền Admin, không tự xóa trái phép.
7. Nếu acad.exe đang chạy lúc HNL cập nhật plugin, HNL báo rõ BẮT BUỘC đóng toàn bộ AutoCAD rồi mở lại.
8. GitHub Actions thêm gate kiểm tra source Ribbon mới và reject các button legacy.

## Ribbon mong đợi
AI:
- AI Copilot

Shopdrawing:
- Ceiling
- Wall
- Library
- Audit

2D Pro:
- Text / Attr
- Field Doctor
- Geometry
- Quick Dim

Data / BOQ:
- BOQ
- Standards

Layout:
- Layout+

Tools:
- Lisp Center
- Manager
- Bridge

Không có các nút native AutoCAD cũ như Selection/Polyline/Layers/Properties/Plot Publish.

## Sau khi cài bản mới
1. Đóng TOÀN BỘ cửa sổ AutoCAD.
2. Chạy HNL CAD AI v2.7.4 một lần để nó repair bundle.
3. Mở AutoCAD.
4. Gõ `HNLVERSION`.
   Kết quả phải bắt đầu: HNL CAD AI Plugin v2.7.4
5. Nếu menu chưa đúng, gõ `HNLRIBBONRESET`.
6. Nếu HNLVERSION vẫn báo bản cũ, dùng HNL > Cài/Sửa AutoCAD Plugin và kiểm tra duplicate bundle được báo.

## Kiểm chứng tại đây
- Version sync
- TS/TSX syntax
- Electron syntax
- XML
- static Ribbon new/legacy tokens
- HNLVERSION/HNLRIBBONRESET wiring
- duplicate-bundle/version/restart detection
- ZIP/RBZ integrity

Không có AutoCAD runtime trong container, nên compile/runtime Autodesk vẫn do GitHub Actions + AutoCAD 2023 thật xác nhận.


## Xác nhận từ ảnh AutoCAD người dùng gửi
Ảnh cho thấy tab HNL đang hiển thị dãy cũ:
`HNL AI / AI Settings / Selection / Smart Ceiling / Smart Wall / Library / Polyline / HNL BOQ / Data-Field / Layers / Properties / HNL Layout / Rename / Plot / Publish / Shop Audit / HNL Tools / Manager / Bridge Status`.

Đây là dấu hiệu chắc chắn AutoCAD đang dùng plugin/Ribbon legacy, vì source hiện tại không còn các nút Selection/Polyline/Layers/Properties/Plot Publish trên HNL Ribbon.
