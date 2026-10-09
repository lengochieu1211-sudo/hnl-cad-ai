# HNL CAD AI v2.7.11 — Library Insert + Compact Ribbon

## Lỗi chèn Library
### Root cause 1: Bridge bị giữ bởi GetPoint
Bản cũ gọi `PromptPoint()` ngay trong action `INSERT_LIBRARY_BLOCK` /
`IMPORT_LIBRARY_DEFINITION`. HNL renderer chỉ chờ Bridge khoảng 12 giây.
Nếu người dùng chưa chuyển sang AutoCAD để click điểm, request nhìn như treo/timeout.

### Sửa
Bridge action không prompt trực tiếp nữa khi payload chưa có `point`:
1. lưu action vào `PendingLibraryInserts`;
2. gửi native command `HNLINSERTPENDING`;
3. trả result ngay cho HNL (`awaitingPoint=true`);
4. AutoCAD command mới prompt điểm trong command context;
5. sau khi click điểm mới chèn block.

UI báo rõ:
`Đã gửi ... sang AutoCAD. Chuyển sang AutoCAD và chọn điểm chèn.`

## Root cause 2: DWG có nhiều block definitions
Bản cũ nếu chưa chọn definition có thể fallback sang insert toàn Model Space.
Nếu DWG chỉ chứa block definitions mà Model Space rỗng, block chèn nhìn như không có gì.

### Sửa
- Tự INSPECT DWG trước khi chèn.
- 1 definition: tự chọn.
- >1 definitions: bắt buộc chọn đúng `Block definition`.
- 0 definitions + Model Space=0: chặn và báo lỗi.
- Không còn label `Whole drawing fallback` gây hiểu nhầm.

## Native commands
- `HNLINSERTPENDING`: xử lý block đang chờ và prompt điểm native.
- `GET_LIBRARY_INSERT_STATUS`: Bridge status cho pending insert.

## Ribbon
Các nhóm được chuyển sang layout 2 hàng bằng `RibbonRowPanel`:
- Shopdrawing: Ceiling | Wall / Library | Audit
- 2D Pro: Text | Field / Geometry | Quick Dim
- Tools: Lisp Center | Manager / Bridge
- Data/BOQ gọn một hàng.
- Nút dùng Standard size thay vì trải nhiều Large columns.
- Có fallback sequential nếu một bản AutoCAD/theme không tạo được RibbonRowPanel.

## Kiểm chứng
- Version sync
- TS/TSX syntax
- Electron/scripts syntax
- AutoCAD project XML
- queued insert wiring
- multi-definition guard
- compact Ribbon row wiring
- ZIP integrity

Không có AutoCAD runtime trong container; GitHub Actions + AutoCAD 2023 thực tế là compile/runtime gate cuối.
