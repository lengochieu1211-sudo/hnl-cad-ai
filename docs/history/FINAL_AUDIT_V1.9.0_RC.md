# FINAL AUDIT — HNL CAD AI v2.0.0 Release Candidate

## PASS trong môi trường hiện tại
- 69 TypeScript/TSX files: transpile syntax PASS.
- Electron main/preload: Node syntax PASS.
- SketchUp Ruby extension: syntax PASS.
- SketchUp RBZ: ZIP integrity PASS.
- Main release ZIP: sẽ test integrity sau đóng gói.
- Direct npm versions: pinned.
- Version: 2.0.0.

## Chức năng mới đã nối logic
### Standalone
- Model → PDF.
- Multi-sheet PDF.
- Publish Queue từng PDF + progress/cancel.
- Windows printers.
- Plot preview + per-layer plot override.
- HNL Sheet Set.
- Batch Page Setup.
- Revision Manager.
- Recovery Restore 5 generations.
- Geometry Doctor / Drawing Compare / Command Health / Self-Test.
- CAD ⇄ SketchUp.

### AutoCAD native protocol
- Pairing file trong TEMP.
- Session token.
- Plugin register + heartbeat.
- Action queue + result.
- GET_STATUS.
- GET_PLOT_DEVICES.
- GET_LAYOUTS.
- PUBLISH_LAYOUTS_PDF.
- PLOT_CURRENT_PDF.
- GET_SHEETSET_INFO.
- UPDATE_SHEET.

### AutoCAD Sheet Set
- Legacy `.DST` read qua AcSmSheetSetMgr.
- Recursive subset/sheet enumeration.
- Edit sheet number/title.
- LockDb/UnlockDb commit.
- Rollback khi update lỗi.

## Build target AutoCAD
- 2023–2024: net48 project.
- 2025–2026: net8.0-windows project.
- Phải build bằng đúng managed DLL của AutoCAD version target.

## Chưa thể xác nhận tại đây
Không có AutoCAD/Autodesk SDK/.NET SDK Windows trong môi trường hiện tại, nên KHÔNG tuyên bố DLL AutoCAD đã compile/load PASS.
Các bước bắt buộc trên máy Windows:
1. Build đúng project Bridge theo version AutoCAD.
2. NETLOAD DLL.
3. HNLBRIDGESTATUS.
4. Đọc native plot devices/styles/layouts.
5. Publish 1/10/100 layouts.
6. Mở/sửa `.DST`.
7. Kiểm tra Sheet Set đang bị lock bởi client khác.
8. Test CTB/STB/PC3 thật.
9. Test printer driver thật.

## Lưu ý AutoCAD API
Native multi-layout publish đi theo Publisher/DSD.
Legacy Sheet Set đi theo SSO COM và không phải Sheet Set Manager for Web.

## Release decision
v2.0.0 là source Release Candidate đủ để build/test tích hợp thật. Chỉ nên gọi Stable sau khi AutoCAD Bridge DLL + Windows installer qua regression test trên máy thật.
