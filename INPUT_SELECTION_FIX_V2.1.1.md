# HNL CAD AI v2.3.0 — Input / Selection Core Fix

## Đã sửa Ctrl shortcuts
- Ctrl+A: chọn tất cả entity trong CAD.
- Ctrl+C: copy selection vào HNL entity clipboard.
- Ctrl+X: cut selection.
- Ctrl+V: paste entity, sinh ID/handle mới, offset 250 mm mỗi lần paste.
- Ctrl+Z / Ctrl+Y: Undo / Redo giữ nguyên.
- Delete/Backspace: xóa selection.
- Trong INPUT/TEXTAREA/contenteditable: Ctrl+A/C/X/V dùng hành vi Windows bình thường, không bị CAD chặn.

## Đã sửa chọn bằng chuột
- Click entity: chọn.
- Click tiếp entity khác: PICKADD, thêm vào selection.
- Ctrl/Shift click: toggle/remove.
- Click vùng trống: bắt đầu Window/Crossing.
- Trái → phải: Window màu xanh dương, chỉ chọn đối tượng nằm hoàn toàn trong khung.
- Phải → trái: Crossing màu xanh lá, chọn đối tượng giao khung.
- Shift không còn bị dùng nhầm để Pan.
- Middle mouse vẫn Pan.
- Hover entity có preselection highlight.
- Hit-test bổ sung LINE/POLYLINE/CIRCLE/RECTANGLE/MLEADER/DIMENSION/CEILING_GRID ngoài WALL/TEXT/BLOCK.

## Khác
- Scale HUD không còn hiển thị 1:0; tối thiểu 1:1.
