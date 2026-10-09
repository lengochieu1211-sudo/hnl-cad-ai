# HNL CAD AI v2.3.0 — AutoCAD-style Command Input

## Command Line
Command Line được dock cố định phía dưới vùng vẽ, không floating đè Status Bar.

- Gõ trực tiếp khi con trỏ đang ở Canvas: `L`, `PL`, `C`, `REC`...
- `Enter` hoặc `Space`: chạy lệnh.
- `Esc`: hủy nhập lệnh / hủy command / bỏ selection theo thứ tự.
- `Ctrl+9`: bật/tắt Command Line.
- Command Line hiển thị trạng thái `READY / PARTIAL / HYBRID`.

## Drawing aliases
- L / LINE → LINE — READY
- PL / PLINE → PLINE — PARTIAL (Standalone hiện 2-point)
- C / CIRCLE → CIRCLE — READY
- REC / RECTANG / RECTANGLE → RECTANGLE — READY
- POL / POLYGON → PARTIAL
- A / ARC → PARTIAL
- H / HATCH → PARTIAL

## Editing aliases
- CO / CP / COPY → COPY — PARTIAL; hiện duplicate offset 250 mm
- M / MOVE → PARTIAL; nhập ΔX/ΔY
- RO / ROTATE → PARTIAL; nhập góc, tâm selection
- SC / SCALE → PARTIAL; nhập factor, tâm selection
- TR / TRIM → PARTIAL
- EX / EXTEND → PARTIAL
- F / FILLET → PARTIAL
- CHA / CHAMFER → PARTIAL
- MI / MIRROR → PARTIAL; X/Y qua tâm selection
- O / OFFSET → PARTIAL; LINE Standalone
- E / ERASE → READY

## Dimension/Text aliases
- D / DIMSTYLE → Dimension Studio — HYBRID
- DLI / DIMLINEAR → Dimension Studio — HYBRID
- DAL / DIMALIGNED → Dimension Studio — HYBRID
- DAN / DIMANGULAR → Dimension Studio — HYBRID
- DRA / DIMRADIUS → Dimension Studio — HYBRID
- DDI / DIMDIAMETER → Dimension Studio — HYBRID
- DI / DIST → PARTIAL; đo nhanh LINE/WALL/DIMENSION đang chọn
- DT / MT / MTEXT → READY; click điểm đặt rồi nhập text

## Ctrl shortcuts
- Ctrl+N: New Drawing
- Ctrl+S: Save Drawing
- Ctrl+O: Open Drawing
- Ctrl+P: Plot / Publish Center
- Ctrl+Z: Undo
- Ctrl+Y: Redo
- Ctrl+A: Select All
- Ctrl+C: Copy selected
- Ctrl+X: Cut selected
- Ctrl+V: Paste
- Ctrl+1: toggle Properties
- Ctrl+9: toggle Command Line

## ESC
- Nếu đang nhập alias: ESC hủy chuỗi đang nhập.
- Nếu đang chạy command: ESC lần 1 hủy command/temp points/dynamic input/ghost preview.
- Nếu không chạy command nhưng đang chọn object: ESC lần tiếp theo bỏ selection.
- Không xóa entity khi nhấn ESC.

## Selection
Giữ sửa từ v2.1.x:
- click pick;
- PICKADD;
- Ctrl/Shift toggle;
- Window trái→phải;
- Crossing phải→trái;
- hover preselection;
- middle mouse pan.
