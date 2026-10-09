# HNL CAD AI v2.4.0 — Deep Logic / UI / AutoCAD Native Audit

## Kết luận
Bản v2.4.0 chuyển rõ hơn sang **AutoCAD-first**:
- AutoCAD xử lý DWG, command, selection, OSNAP, grips, layout/plot native.
- HNL EXE làm Manager/AI/automation và fallback DXF.
- HNL AutoCAD plugin được build theo từng AutoCAD 2023/2024/2025/2026 và đóng vào Autodesk ApplicationPlugins bundle.
- Khi HNL EXE khởi động, bundle được tự cài/sửa theo user vào `%APPDATA%\Autodesk\ApplicationPlugins\HNL.CadBridge.bundle`; không cần NETLOAD nếu build có đủ plugin DLL.

## Audit nút / command
- Tổng button JSX: **338**
- Button có `onClick`: **336**
- Button submit hợp lệ: **2**
- Button hoạt động nhưng không có handler: **0**
- Literal HNL command được gọi từ UI: **61**
- Literal command không có handler: **0**
- Electron menu command: **18**
- Menu command không có renderer handler: **0**
- AutoCAD aliases: **26**
- Alias có `nativeCommand`: **26/26**

### Các nút chết đã sửa
1. Drywall Studio — `Tự động thêm Detail Firestop`
   - trước: button không có onClick.
   - sau: `Mở Detail Firestop` → mở workflow detail có xác nhận.
2. Drywall Studio — `Định vị khe co giãn`
   - trước: button không có onClick.
   - sau: `Mở Control Joint` → mở workflow detail có xác nhận.
3. Project Tree `+`
   - trước: phụ thuộc hover group.
   - sau: click mở/đóng menu rõ ràng, chọn Ceiling/Wall/Detail/Section rồi tự đóng.

## AutoCAD Native command routing
Khi Bridge Connected, các nút/alias sau chuyển thẳng sang AutoCAD bằng `SendStringToExecute`, không dùng geometry engine giả:
- LINE
- PLINE
- CIRCLE
- RECTANG
- POLYGON
- ARC
- HATCH
- COPY
- MOVE
- ROTATE
- SCALE
- TRIM
- EXTEND
- FILLET
- CHAMFER
- MIRROR
- OFFSET
- ERASE
- DIST
- MTEXT
- JOIN

Standalone vẫn giữ fallback hiện có; các lệnh không đủ parity phải báo PARTIAL thay vì giả thành công.

## Ctrl / ESC logic
Khi AutoCAD Connected:
- Ctrl+N → QNEW native.
- Ctrl+S → lưu DWG hiện tại native.
- Ctrl+Shift+S → Save As DWG native.
- Ctrl+O → file picker nhận DWG; DWG được đưa qua AutoCAD Bridge.
- Ctrl+P → Plot native.
- Ctrl+Z → U native.
- Ctrl+Y → REDO native.
- Ctrl+A → `Editor.SelectAll()` + implied selection, không phụ thuộc lệnh Lisp `AI_SELALL`.
- Ctrl+C → COPYCLIP.
- Ctrl+X → CUTCLIP.
- Ctrl+V → PASTECLIP.
- Delete → ERASE.
- ESC → Bridge action `CANCEL_COMMAND`, gửi ESC ESC sang AutoCAD.

Khi Bridge Offline:
- dùng selection/clipboard/undo DXF Standalone.

## DWG logic
Bridge mới có:
- OPEN_DWG
- SAVE_CURRENT_DWG
- SAVE_AS_DWG
- SAVE_DXF_AS_DWG
- GET_SELECTION
- SELECT_ALL
- GET_LAYERS
- GET_LAYOUTS
- EXECUTE_COMMAND
- CANCEL_COMMAND
- Plot/Publish
- Sheet Set actions

DWG được xử lý bởi AutoCAD Database API; không đổi đuôi giả.

## Bridge robustness
Đã sửa lỗi logic pairing cũ:
- Plugin đọc lại `%TEMP%\HNL_CAD_AI\bridge.json` **mỗi chu kỳ**.
- Nếu HNL EXE restart làm token/URL thay đổi → `_registered=false` và tự đăng ký lại.
- AutoCAD có thể mở trước HNL; khi HNL chạy sau, plugin vẫn tự reconnect.
- Không cần NETLOAD lại chỉ vì HNL restart.

Bridge capability audit:
- capability khai báo: **16**
- action switch handler: **16**
- capability thiếu handler: **0**
- handler không khai báo capability: **0**

## AutoCAD Native Palette
Plugin có command:
- `HNL`
- `HNLPALETTE`
- `HNLHIDE`

Palette dock trái/phải, gồm:
- Home
- 2D / Draw
- Data
- Layout / Publish
- Tools
- VI | EN

Các nút vẽ cơ bản trong Palette gọi AutoCAD native command.

Các command chẩn đoán thực:
- HNLBRIDGESTATUS
- HNLBRIDGEPING
- HNLPLOTDEVICES
- HNLLAYOUTS
- HNLSELECTION
- HNLLAYERS

Trước audit, tên các command này chỉ được in ra lúc plugin load nhưng chưa có `CommandMethod`; v2.4.0 đã bổ sung handler thật.

## One-click AutoCAD plugin
Tạo:
`autocad-plugin/HNL.CadBridge.bundle/PackageContents.xml`

Tách build:
- AutoCAD 2023 → AutoCAD.NET 24.2.0 / net48
- AutoCAD 2024 → AutoCAD.NET 24.3.0 / net48
- AutoCAD 2025 → AutoCAD.NET 25.0.1 / net8.0-windows
- AutoCAD 2026 → AutoCAD.NET 25.1.0 / net8.0-windows

GitHub Actions:
1. setup Node 22
2. setup .NET 8
3. restore/build 4 plugin DLL
4. đóng DLL vào `.bundle`
5. TypeScript audit
6. build Electron/NSIS installer
7. upload plugin bundle riêng
8. upload Windows installer

Installer đóng bundle bằng `extraResources`.
Lần chạy HNL đầu tiên tự copy bundle vào Autodesk ApplicationPlugins.

## Manager EXE path
HNL EXE ghi đường dẫn thật vào:
`%APPDATA%\HNL CAD AI\manager-path.txt`

Native AutoCAD Palette dùng marker này để mở lại HNL Manager, kể cả khi người dùng chọn thư mục cài khác mặc định.

## File format
Ưu tiên khi Connected:
1. DWG native
2. DXF
3. HNL JSON chỉ là backup metadata

Standalone:
1. DXF
2. HNL Project JSON

## Static validation thực hiện trong môi trường hiện tại
- TS/TSX syntax transpile: **77/77 PASS**
- Electron `main.cjs`: **PASS**
- Electron `preload.cjs`: **PASS**
- PackageContents.xml: **PASS XML**
- 4 AutoCAD csproj: **PASS XML**
- SketchUp Ruby loader: **Syntax OK**
- SketchUp Ruby main: **Syntax OK**
- 338 button audit: **0 dead button**
- 61 UI command calls: **0 unhandled**
- 18 Electron menu commands: **0 unhandled**
- 26 aliases: **26 native mapping**
- 16 Bridge capabilities: **16 handlers**

## Chưa thể xác nhận trong container Linux
Không có Windows AutoCAD runtime/.NET SDK trong container này nên chưa thể chạy vật lý:
- build DLL AutoCAD;
- AutoCAD autoload bundle;
- DWG open/save;
- PaletteSet thực tế;
- native command execution.

Các mục này được chuyển thành **blocking checks trong GitHub Actions/Windows**. Nếu một trong 4 AutoCAD plugin không compile, workflow phải đỏ và không được coi là release hoàn chỉnh.
