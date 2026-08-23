# HNL CAD AI v2.7.2 — Compact Professional AutoCAD UI

## Mục tiêu
Tập trung riêng vào giao diện plugin trong AutoCAD:
- gọn hơn,
- chuyên nghiệp hơn,
- có icon/glyph minh họa,
- không lặp các lệnh AutoCAD native,
- giảm trùng chức năng,
- ẩn diagnostics khỏi giao diện chính.

## Native AutoCAD Ribbon mới

### AI
- AI Copilot — Large

### Shopdrawing
- Ceiling — Large
- Wall — Large
- Library — Large
- Audit — Standard

### 2D Pro
- Text / Attr — Standard
- Field Doctor — Standard
- Geometry — Standard
- Quick Dim — Standard

### Data / BOQ
- BOQ — Large
- Standards — Standard

### Layout
- Layout+ — Large

### Tools
- Lisp Center — Standard
- Manager — Standard
- Bridge — Standard

Tổng cộng chỉ 6 nút Large.
Các tiện ích phụ dùng Standard size để giảm chiều ngang Ribbon.

## Những lệnh AutoCAD native cố ý KHÔNG đưa vào HNL Ribbon
- LINE / PLINE
- TRIM / EXTEND
- LAYER
- PROPERTIES
- PLOT / PUBLISH
- OSNAP / ORTHO / GRID / SNAP
- MOVE / COPY / ROTATE / SCALE

Các lệnh này thuộc AutoCAD; HNL chỉ cung cấp workflow riêng.

## Icon
Native Ribbon tiếp tục dùng icon vector 16/32px:
AI, Ceiling, Wall, Library, Text, Field, Geometry, Dimension, BOQ, Layout, Audit, Lisp, Bridge, Manager.

## Native Palette
Giảm từ 6 tab xuống 4 tab:
1. AI
2. SHOP
3. DATA
4. TOOLS

### SHOP
- Smart Ceiling
- Smart Wall
- Library
- Shop Audit
- Quick Insert

### DATA
- Text / Attribute
- Field Doctor
- Geometry Doctor
- Quick Dimension
- BOQ
- Layout+

### TOOLS
- Lisp Center
- Layer Standards
- HNL Manager
- Advanced / Diagnostics — mặc định ẩn

Advanced mới chứa:
- Bridge Status
- Bridge Ping
- Palette Status

## Compact Palette
- Width mặc định: 340px
- Minimum: 300px
- Height: 640px
- Header: 52px
- Buttons: 84x28
- Tab labels ngắn: AI / SHOP / DATA / TOOLS
- Nút VI|EN tự căn theo chiều rộng Palette.
- Các nút có glyph trực quan nhỏ để dễ nhận biết nhanh.

## Đồng bộ command tab sau khi giảm Palette
- HNLAI -> tab 0
- HNL2D -> DATA tab 2
- HNLDATA -> DATA tab 2
- HNLLAYOUT -> DATA tab 2
- HNLTOOLS -> TOOLS tab 3

## Library
- Ribbon Library vẫn mở HNLLIBRARY / HNL Library Manager v2.7.1+.
- HNLINSERT chỉ giữ vai trò Quick Insert trong Palette.
- Classic menu cũng đổi Library sang HNLLIBRARY.

## Kiểm tra
- TypeScript / TSX syntax transpile
- Electron main/preload syntax
- AutoCAD csproj + PackageContents XML
- Version sync gate
- Native Ribbon static structure
- Palette tab count / command mapping
- Không có native AutoCAD commands trong HNL Ribbon
- ZIP / RBZ integrity

## Giới hạn kiểm chứng
Môi trường hiện tại không có AutoCAD/.NET SDK Autodesk nên chưa runtime-test bố cục Ribbon thật trên AutoCAD 2023.
GitHub Actions Windows là compile gate; AutoCAD 2023 thực tế là UI/runtime gate.
