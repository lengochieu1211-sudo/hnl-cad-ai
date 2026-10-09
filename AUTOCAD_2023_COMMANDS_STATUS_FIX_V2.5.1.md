# HNL CAD AI v2.5.1 — AutoCAD 2023 Commands + Functional Status Bar

## Tài liệu AutoCAD 2023 đã nhập
Nguồn người dùng cung cấp: AutoCAD 2023 Tutorial First Level — 2D Fundamentals, Chapter 1.

Command/workflow đã đưa vào HNL:
- UNITS
- LIMITS
- ZOOM / Zoom All
- LINE + Close / Undo
- ERASE + Select objects
- PAN Realtime
- CIRCLE: Center-Radius, Center-Diameter, 2P, 3P, TTR, TTT
- ARC: 3-Points, Center-Start-End
- GRID / F7
- SNAP / F9
- UCS / WCS
- QSAVE / Ctrl+S
- QUIT / Ctrl+Q
- 5 cách nhập điểm: Interactive, X,Y, @X,Y, @Distance<Angle, Direct Distance

## Command Search
Ctrl+Space tìm được các command AutoCAD 2023 trên cùng:
- alias / shortcut;
- workflow;
- options;
- mô tả.

Khi AutoCAD Connected, chọn command gọi lệnh AutoCAD native.

## Command Line
Bổ sung alias:
UN, UNITS, LIMITS, Z, ZOOM, P, PAN, UCS, GRID, SNAP, QSAVE, SAVE, QUIT.

Các alias hiện có L/LINE, C/CIRCLE, A/ARC, E/ERASE vẫn giữ native.

## Functional status bar
Hàng dưới cùng không còn là nhãn trang trí.
Các nút thật:
- SNAP → F9 / SNAPMODE
- OSNAP → F3 / OSMODE
- ORTHO → F8 / ORTHOMODE
- GRID → F7 / GRIDMODE
- DYN → F12 / DYNMODE
- mm → UNITS khi AutoCAD Connected

Bridge:
- GET_DRAFTING_STATUS
- SET_DRAFTING_MODE
- HNLDRAFTSTATUS

Khi AutoCAD Connected, HNL đọc/ghi system variable native. Khi Standalone, nút điều khiển CadCanvas.

## Phạm vi tài liệu
File đang có là Chapter 1, 44 trang, không phải toàn bộ AutoCAD 2023 Command Reference.
HNL đã nhập toàn bộ command/workflow rõ ràng trong chapter này. Command Knowledge được tách module để có thể bổ sung các chapter/tài liệu tiếp theo mà không phải đổi kiến trúc UI.

## Validation
- TS/TSX syntax transpile PASS
- Electron main/preload syntax PASS
- 4 AutoCAD csproj + PackageContents XML parse PASS
- AutoCAD 2023 command knowledge module PASS
- new aliases map native
- status bar buttons have real onClick handlers
- Bridge GET/SET drafting mappings PASS
- v2.4.8 Palette registration fix preserved
- @google/genai pinned to 2.4.0
- ZIP/RBZ integrity PASS

AutoCAD C# compile/runtime vẫn cần GitHub Actions Windows xác nhận.
