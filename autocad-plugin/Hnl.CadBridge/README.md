# HNL AutoCAD Bridge v2.0.5

## Mục tiêu
Bridge chạy **bên trong AutoCAD**, kết nối HNL CAD AI EXE qua localhost pairing có token.

HNL EXE tạo:
`%TEMP%\HNL_CAD_AI\bridge.json`

Plugin đọc file này, đăng ký với HNL, gửi heartbeat và poll action queue. Không mở port riêng trong AutoCAD.

## Phiên bản AutoCAD / .NET
Không dùng một DLL cho tất cả version:

- **AutoCAD 2023–2024:** build `Hnl.CadBridge.AutoCAD2023-2024.csproj` → `net48`.
- **AutoCAD 2025–2026:** build `Hnl.CadBridge.AutoCAD2025-2026.csproj` → `net8.0-windows`.

Đặt biến `AUTOCAD_SDK` trỏ tới thư mục chứa `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll` của đúng AutoCAD đang target.

## Capabilities hiện có
- GET_STATUS
- GET_PLOT_DEVICES
- GET_LAYOUTS
- PUBLISH_LAYOUTS_PDF
- PLOT_CURRENT_PDF
- GET_SHEETSET_INFO
- UPDATE_SHEET

## Plot/Publish Native
`PUBLISH_LAYOUTS_PDF` dùng AutoCAD `DsdEntryCollection`, `DsdData`, `Publisher.PublishExecute` và `DWG To PDF.pc3`.

Bridge tạm chuyển `BACKGROUNDPLOT=0` trong lúc publish rồi restore lại giá trị cũ.

## Sheet Set Native (.DST)
Dùng legacy Sheet Set Object API (`AcSmSheetSetMgr`) qua COM late binding.

- Read: `OpenDatabase` → `GetSheetSet` → recursive `GetSheetEnumerator`.
- Update: `LockDb` → `SetNumber` / `SetTitle` → `UnlockDb(commit=true)`.
- Nếu lỗi: `UnlockDb(commit=false)` để rollback.

Lưu ý: Autodesk xác định SSO API này áp dụng cho **legacy Sheet Set Manager**, không phải Sheet Set Manager for Web.

## Lệnh kiểm tra trong AutoCAD
- `HNLBRIDGESTATUS`
- `HNLBRIDGEPING`
- `HNLPLOTDEVICES`
- `HNLLAYOUTS`

## Không giả hoàn thiện
Bridge source đã có protocol/action handlers nhưng vẫn phải build/test bằng SDK AutoCAD thật. Không thể xác nhận binary plugin chỉ bằng kiểm tra source trên Linux.
