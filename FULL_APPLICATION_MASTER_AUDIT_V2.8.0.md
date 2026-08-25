# HNL CAD AI v2.8.0 — Full Application Master Audit

## Kết luận điều hành
Baseline được audit: **v2.7.12**. Sau audit, các lỗi source-level có thể sửa chắc chắn đã được harden thành **v2.8.0 Master Audit**. Đây **chưa phải bản Production VERIFIED** vì môi trường hiện tại không có Windows/AutoCAD runtime và không có .NET SDK/AutoCAD SDK để compile plugin tại chỗ. GitHub Windows build là compile gate tiếp theo; AutoCAD 2023 trên máy thật là runtime gate đầu tiên.

### Các lỗi quan trọng phát hiện và đã sửa trong v2.8.0
- **P0 — GitHub build gate bị stale:** workflow vẫn tìm `Field Doctor` + `Quick Dim` trong Native Ribbon trong khi v2.7.12 đã đổi thành `Field` + `Dim+`; build có thể fail giả. Đã đồng bộ gate.
- **P0 — Electron tool routing thiếu taxonomy mới:** `HNLBLOCK`, `HNLLAYER`, `HNLSHOP2D` mở EXE với `--hnl-tool=...` nhưng Electron không cho phép BLOCK/LAYER/SHOPDRAWING. Đã bổ sung.
- **P0 — 12 đường `window.prompt()` không tương thích Electron:** đây trùng đúng lỗi diagnostic trước đó `prompt() is not supported`. Đã thay bằng HNL input dialog riêng.
- **P0/P1 — AI AutoLISP Builder online trả payload khác UI mong đợi:** server online trả `{code,...}` top-level còn UI chỉ đọc `data.lisp`. Đã normalize cả hai dạng.
- **P1 — AI tự fallback Offline mặc định:** đã đổi mặc định thành OFF; chỉ chuyển Offline khi người dùng chủ động bật tùy chọn.
- **P1 — Native Palette còn taxonomy cũ:** vẫn ghi `Text / Attribute` và thiếu Block/Layer/Shopdrawing. Đã đồng bộ taxonomy v2.7.12.
- **P1 — Layout sync chỉ phụ thuộc trạng thái Connected:** đổi workspace mode khi AutoCAD đã Connected có thể không kích hoạt sync. Đã đổi dependency sang `isNativeDwgWorkspace`.
- **P1 — Bridge timeout 12 giây cho mọi action:** quá ngắn cho Publish/Open/Preview/Save. Đã dùng timeout theo action 20–180 giây và tăng TTL buffer server.
- **P2 — Tools menu có 2 nút Smart Shopdrawing giống hệt:** đã xóa duplicate.
- **P2 — thông báo POLYLINE nói còn 2 điểm dù engine đã multi-segment:** đã sửa nội dung.
- **P2 — Smart Shopdrawing header hard-code `v2.6`:** đã dùng version canonical.

## Master Audit Matrix
| ID | Workflow | Khu vực | Static | Runtime | Final | Ghi chú |
|---|---|---|---|---|---|---|
| SYS-01 | Baseline/version sync | Core | PASS | N/A | VERIFIED | package/metadata/AutoCAD/SketchUp version synchronized |
| SYS-02 | Master static regression gate | Core | PASS | N/A | VERIFIED | 21/21 static checks |
| START-01 | Start Center primary flow | Start Center | PASS | REQUIRED | REVIEW | Source has Continue / AutoCAD+HNL / DXF-HNL / New; visual Windows test pending |
| START-02 | Advanced DWG modes | Start Center | PASS | REQUIRED | REVIEW | Direct DWG and HNL Canvas are collapsed/guarded; runtime launch test pending |
| DWG-01 | Open DWG via AutoCAD | DWG | PASS | REQUIRED | REVIEW | OPEN_DWG/launch workflow wired; AutoCAD runtime required |
| DWG-02 | HNL Canvas preview | DWG | PASS | REQUIRED | REVIEW | DWG→temporary DXF preview wired; fidelity/runtime pending |
| DWG-03 | Direct DWG live snapshot | DWG | PASS | REQUIRED | REVIEW | GET_MODELSPACE_SNAPSHOT guarded; unsupported/truncated entities possible |
| BRG-01 | Bridge pairing/heartbeat | Bridge | PASS | REQUIRED | REVIEW | Local token + heartbeat + port fallback present; real AutoCAD gate pending |
| BRG-02 | Bridge action timeout policy | Bridge | PASS | REQUIRED | REVIEW | v2.8.0 adds action-specific 20–180 s budgets; long publish runtime pending |
| 2D-01 | 2D taxonomy routing | 2D Pro | PASS | N/A | VERIFIED | Electron tool args + native Palette + Ribbon + App routes aligned |
| 2D-02 | Electron-safe input dialogs | 2D Pro | PASS | REQUIRED | REVIEW | 12 window.prompt calls removed; Windows interaction test pending |
| 2D-03 | Quick Dim / Dim+ | 2D Pro | PASS | REQUIRED | REVIEW | DN/DNC + JD + TKD mapped; actual AutoCAD dimensions not runtime-verified |
| 2D-04 | Field / Links | 2D Pro | PASS | REQUIRED | REVIEW | Field center mapped; ObjectID/Field behavior requires native AutoCAD |
| 2D-05 | Geometry / Polyline | 2D Pro | PASS | REQUIRED | REVIEW | Standalone partial; native path required for Trim/Fillet/Hatch/Arc |
| 2D-06 | Layer / Properties | 2D Pro | PASS | REQUIRED | REVIEW | TLE + DM/RDM/FDM taxonomy correct; native behavior pending |
| LSP-01 | 45-file archive inventory | 44 Lisp + ARX | PASS | N/A | VERIFIED | 44 .lsp + 1 legacy GeomProps2021x64.arx |
| LSP-02 | 44 Lisp exact-file taxonomy | 44 Lisp + ARX | PASS | N/A | VERIFIED | 44/44 exact sourceFile entries; group counts pass |
| LSP-03 | On-demand Lisp loading policy | 44 Lisp + ARX | PASS | REQUIRED | REVIEW | Default does not preload all; SECURELOAD/dependencies require AutoCAD test |
| LSP-04 | TKL runtime | 44 Lisp + ARX | PASS | REQUIRED | REVIEW | Present in Layout group; full Layout workflow runtime pending |
| LSP-05 | TKT runtime | 44 Lisp + ARX | PASS | REQUIRED | REVIEW | Present in Quantity group; table/geometry runtime pending |
| LSP-06 | Legacy ARX compatibility | 44 Lisp + ARX | PASS | REQUIRED | REVIEW | ARX excluded from autoload/bundle for 2023–2026; compatibility not certified |
| LIB-01 | Library DWG inspect | Library | PASS | REQUIRED | REVIEW | Auto-inspect and multi-definition guard present |
| LIB-02 | Library insert point flow | Library | PASS | REQUIRED | REVIEW | Queued HNLINSERTPENDING avoids web timeout; native insert runtime pending |
| LIB-03 | Dynamic Block properties | Library | PASS | REQUIRED | REVIEW | GET/SET_DYNAMIC_BLOCK_PROPERTIES present; dynamic block test file needed |
| SHOP-01 | Smart Ceiling | Shopdrawing | PASS | REQUIRED | REVIEW | Native CREATE_CEILING_* present; geometric benchmark pending |
| SHOP-02 | Smart Wall | Shopdrawing | PASS | REQUIRED | REVIEW | CREATE_WALL_SYSTEM present; geometric benchmark pending |
| SHOP-03 | Shopdrawing Audit | Shopdrawing | PASS | REQUIRED | REVIEW | Native audit scans HNL XData and block scale; coverage is intentionally limited |
| BOQ-01 | HNL native BOQ | BOQ | PASS | REQUIRED | REVIEW | Counts HNL-tagged ceiling/wall/library entities only; not a full arbitrary-DWG takeoff |
| BOQ-02 | TKT Quantity/BOQ | BOQ | PASS | REQUIRED | REVIEW | Source mapping verified; AutoCAD table/result benchmark pending |
| LAY-01 | Native layout mirror/activate/rename | Layout | PASS | REQUIRED | REVIEW | v2.8.0 fixes effect dependency on workspace mode; runtime pending |
| LAY-02 | TKL Layout Automation | Layout | PASS | REQUIRED | REVIEW | Source present/on-demand; viewport/title block/page setup benchmark pending |
| LAY-03 | Plot/Publish | Layout | PASS | REQUIRED | REVIEW | Native actions present; long-action timeout raised; CTB/printer runtime pending |
| AI-01 | Provider config/key storage | AI | PASS | REQUIRED | REVIEW | safeStorage/DPAPI wiring present; provider connection test pending |
| AI-02 | No silent provider fallback | AI | PASS | REQUIRED | REVIEW | v2.8.0 defaults fallback OFF; user opt-in required |
| AI-03 | AI AutoLISP Builder response | AI | PASS | REQUIRED | REVIEW | v2.8.0 accepts online top-level code and offline nested payload; API runtime pending |
| AI-04 | Offline rule planner | AI | PASS | REQUIRED | REVIEW | Rules present; output quality/geometry application benchmark pending |
| SAVE-01 | Standalone Undo/Redo | Save/Undo | PASS | REQUIRED | REVIEW | History stack wired; UI runtime test pending |
| SAVE-02 | Native Undo/Redo | Save/Undo | PASS | REQUIRED | REVIEW | U/REDO routed to AutoCAD; transaction behavior pending |
| SAVE-03 | Native DWG Save/Save As | Save/Undo | PASS | REQUIRED | REVIEW | SAVE_CURRENT_DWG/SAVE_AS_DWG wired; read-only/unsaved cases pending |
| SAVE-04 | AutoSave/recovery | Save/Undo | PASS | REQUIRED | REVIEW | localStorage + recovery generations present; quota/large-project test pending |
| BUILD-01 | GitHub current Ribbon gate | Installer | PASS | N/A | VERIFIED | v2.8.0 fixes stale v2.7.12 token checks |
| BUILD-02 | AutoCAD plugin build 2023–2026 | Installer | SOURCE PASS | REQUIRED | REVIEW | No dotnet/AutoCAD SDK runtime locally; GitHub Windows is compile gate |
| BUILD-03 | Windows Electron installer | Installer | SOURCE PASS | REQUIRED | REVIEW | Workflow/build config present; actual installer artifact pending |
| BUILD-04 | Dependency reproducibility | Installer | WARNING | REQUIRED | REVIEW | package-lock.json absent; npm install can resolve transitive versions differently |

**Tổng: 44 hạng mục — VERIFIED 6 — REVIEW 38.**

## Diễn giải trạng thái
- **VERIFIED**: có thể chứng minh đầy đủ bằng source/manifest/static gate trong môi trường hiện tại, không phụ thuộc AutoCAD/Windows/API bên ngoài.
- **REVIEW**: source đã có và static audit PASS nhưng cần GitHub Windows compile, AutoCAD runtime, provider thật hoặc benchmark hình học trước khi được nâng thành VERIFIED.

## 44 Lisp + 1 ARX
Inventory **VERIFIED**: 45 file tổng = 44 `.lsp` + 1 `GeomProps2021x64.arx`. Taxonomy 44/44 PASS. Tuy nhiên **không Lisp nào được gọi là runtime VERIFIED chỉ vì có trong archive**. TKL, TKT, DN/DNC, BRK... đều vẫn REVIEW cho đến khi chạy Golden Test trên AutoCAD.

## Giới hạn BOQ hiện tại
`GET_HNL_BOQ` hiện chỉ tổng hợp entity có HNL tag/XData do HNL tạo (ceiling main/cross/hanger, wall track/stud, library block). Đây **không phải bộ BOQ tổng quát cho mọi DWG**. TKT và các engine Quantity khác vẫn cần benchmark riêng.

## Gate tiếp theo
1. Push **v2.8.0** lên GitHub và chạy workflow Windows. Chỉ tiếp tục nếu: 44 Lisp extraction PASS, taxonomy PASS, master audit PASS, TS PASS, plugin 2023–2026 compile PASS, installer tạo thành công.
2. Cài plugin + EXE trên máy có **AutoCAD 2023** và chạy checklist runtime đi kèm.
3. Fix đúng lỗi runtime tìm thấy; chạy regression cluster liên quan.
4. Khi AutoCAD 2023 Golden PASS, lặp lại smoke test 2024/2025/2026.
5. Chỉ sau đó đổi các dòng REVIEW đạt đủ điều kiện thành VERIFIED và khóa Release Candidate.

## Các hạng mục chưa nên thêm tính năng mới
Ưu tiên đóng REVIEW hiện hữu trước: **Library insert**, **Quick Dim**, **TKL**, **TKT**, **BRK/Field**, **Smart Ceiling/Wall**, **Layout Publish**, **AI provider**, **Save/Undo**, **installer**. Không mở thêm module mới trước khi các workflow P0/P1 này có Golden Runtime.
