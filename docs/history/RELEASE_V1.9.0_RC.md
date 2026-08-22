# HNL CAD AI v2.0.0 — Release Candidate Professional

## Hoàn thiện thêm so với v1.8
- AutoCAD local pairing protocol có token.
- AutoCAD plugin register/heartbeat/action queue/result.
- Plot device + CTB/STB list đọc từ AutoCAD native.
- Layout/Page Setup metadata đọc từ AutoCAD.
- Native multi-layout PDF dùng AutoCAD Publisher/DSD.
- Standalone Publish Queue:
  - từng PDF;
  - progress;
  - DONE/FAILED/CANCELED;
  - chọn thư mục;
  - Cancel queue.
- Revision Manager:
  - lưu revision từ Drawing Compare;
  - Added/Deleted/Modified;
  - Draft/Issued.
- Recovery:
  - Restore trực tiếp 1 trong 5 generations;
  - confirm trước khi thay workspace.
- Native `.DST`:
  - đọc Sheet Set legacy qua AcSmSheetSetMgr;
  - recursive Subset/Sheet;
  - sửa Sheet Number/Title;
  - LockDb/UnlockDb commit;
  - rollback khi lỗi.
- AutoCAD plugin build tách:
  - 2023–2024 net48;
  - 2025–2026 net8.
- HNL UI vẫn giữ Standalone fallback khi AutoCAD offline.

## Những phần vẫn phải test bằng phần mềm thật
Không thể hợp lý tuyên bố hoàn thiện binary AutoCAD plugin nếu chưa build/load trong AutoCAD thật:
- AcMgd/AcDbMgd/AcCoreMgd đúng version.
- AcSmComponents COM registration từng AutoCAD.
- DWG To PDF.pc3 trên máy thật.
- Plot media/page setup và printer driver thực.
- Sheet Set đang mở bởi nhiều client.
- Publish 100–500 sheets.
- AutoCAD LT không hỗ trợ .NET plugin giống AutoCAD full.

## Trạng thái Release Candidate
Source đã được harden để test thực tế Windows/AutoCAD/SketchUp. Khi có log từ máy thật, Diagnostic Center + Bridge action error sẽ cho biết chính xác bước lỗi.
