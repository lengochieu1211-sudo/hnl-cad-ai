# HNL CAD AI v2.4.5 — Collapsible UI Layout

## Mục tiêu
Bố trí giao diện theo kiểu AutoCAD: vùng vẽ luôn được ưu tiên, các công cụ HNL có thể thu gọn nhanh thay vì chiếm cố định màn hình.

## Các khu vực và nút thu gọn

### 1. Ribbon phía trên
- Thêm nút `▲ / ▼` ở Application Bar.
- Thu gọn chỉ ẩn:
  - nhóm Ribbon,
  - contextual tabs,
  - panel nút lệnh lớn.
- Application Bar vẫn còn để:
  - thấy tên file / trạng thái AutoCAD,
  - Undo/Redo,
  - Command Search,
  - AI,
  - mở lại Ribbon.

### 2. Project / Properties Dock bên trái
- Khi mở: Tree / Property / DAG / Sheet dùng chung một dock.
- Nút `◀` thu gọn toàn bộ dock.
- Khi thu gọn không biến mất hoàn toàn: còn rail 36 px với 4 icon:
  - Tree
  - Property
  - DAG
  - Sheet
- Bấm icon nào thì mở đúng panel đó.

### 3. HNL AI Palette
- Giữ nút Minimize hiện có.
- Khi thu gọn: còn rail 36 px `HNL AI`, không floating đè Canvas.
- Có thể chuyển dock trái/phải.

### 4. Command Line
- Giữ `Ctrl+9` và nút X để bật/tắt.
- Trạng thái được lưu.

### 5. Tập trung vẽ
Thêm nút `Tập trung vẽ` trên thanh trên:
- thu Ribbon,
- thu Left Dock,
- thu AI Palette,
- ẩn Command Line.
Canvas/DWG được mở rộng tối đa.
Bấm `Khôi phục UI` để trả lại đúng trạng thái trước khi vào Focus Mode.

### 6. AutoCAD Native Palette
`PaletteSet` native vẫn sử dụng:
- Auto-hide pin,
- Close,
- dock Left/Right,
do AutoCAD xử lý native.

## Ghi nhớ bố cục
Trạng thái UI được lưu trong `localStorage` key:
`hnl.ui.layout.v2`

Ghi nhớ:
- Ribbon collapsed,
- Left Dock open/closed,
- AI open/closed,
- AI dock left/right,
- Command Line visible.

## Nguyên tắc bố trí
- Không mở đồng thời nhiều panel nặng.
- Left = Project/Properties/Data model.
- Center = CAD/DWG.
- Right = AI/HNL contextual tools.
- Bottom = Command Line + Model/Layout + status.
- Top = Application Bar + Ribbon có thể thu gọn.

## Validation
- TS/TSX syntax transpile PASS.
- Electron main/preload syntax PASS.
- AutoCAD csproj + PackageContents XML parse PASS.
- `@google/genai` vẫn pin độc lập ở 2.4.0.
- ZIP/RBZ integrity PASS.
