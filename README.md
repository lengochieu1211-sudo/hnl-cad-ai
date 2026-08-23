# HNL CAD AI v2.6.3

HNL CAD AI là ứng dụng CAD/Shopdrawing dạng Windows Desktop (Electron) có hai chế độ kiến trúc:

- **Standalone Workspace:** chạy độc lập, không cần AutoCAD; làm việc với model HNL và DXF cơ bản.
- **AutoCAD Connected Workspace:** chỉ bật khi plugin HNL AutoCAD Bridge thật được cài và phản hồi. DWG native/AutoLISP/Field/Table/Layout/Viewport thuộc chế độ này.

## Điểm mới v2.3.0 Professional Release Candidate
- HNL Pile Studio: PHC/PC/cọc vuông/custom, pile plan + tag + schedule.
- AutoSave/Recovery 30 giây và sau thay đổi.
- AutoCAD Bridge status không giả lập.
- AutoCAD .NET bridge skeleton nằm trong `autocad-plugin/Hnl.CadBridge`.
- UI có tab Cọc & Nền móng, connection indicator, autosave indicator.
- Audit command typo và GitHub Actions build EXE.

## Windows
Mục tiêu: Windows 10 22H2 / Windows 11 x64.

Tối thiểu đề xuất: CPU 4 nhân x64, RAM 8 GB, màn hình 1366x768, GPU hỗ trợ DirectX 11, khoảng 1.5 GB dung lượng trống. Khuyến nghị RAM 16 GB, Full HD và SSD cho bản vẽ lớn/AI.

## Build installer
```bat
npm install
npm run lint
npm run dist:win
```
Output: `dist_electron/HNL_CAD_AI_Setup_2.6.3.exe`

Có thể dùng `.github/workflows/build-windows.yml` trên GitHub Actions Windows runner.


## Release hardening v2.3.0
- Full Project Save/Open schema v2, backward-compatible recovery.
- Dirty-state + cảnh báo thoát/tạo mới khi chưa lưu.
- Start Center chuyên nghiệp, tên file và trạng thái lưu trên Ribbon.
- Safe Mode đồng bộ với Settings.
- Gemini key mã hóa bằng Windows DPAPI qua Electron safeStorage.
- Local API token theo phiên + 127.0.0.1 + CSP + Electron sandbox.
- Installer mặc định per-user để giảm lỗi quyền Administrator.

> DWG native/AutoLISP native vẫn cần AutoCAD Bridge/plugin; Standalone không giả lập khả năng chưa có.

## v2.6.0 — Smart Shopdrawing Platform

HNL v2.6 adds a unified shopdrawing workspace:

- Smart Library: annotation, ceiling, wall, steel and user-imported DWG blocks.
- Smart Ceiling: board module 1220×2440; HNL cross-runner rule 1220/3 = 406.67 mm.
- Smart Wall: stud module 1220/3 = 406.67 mm or 1220/2 = 610 mm.
- Approved Material/Submittal manager with project-first technical precedence.
- Smart BOQ and HNL XData tags for native AutoCAD-generated systems.
- Shopdrawing Audit and existing parametric Section/Detail workflow.
- Project templates: Airport / Hotel / Condo / Factory.
- AI planning includes Approved Material + manufacturer KB and returns sourceRefs/certainty.
- DWG open modes:
  - AutoCAD Native: full DWG fidelity.
  - HNL Canvas: AutoCAD Bridge reads DWG in background and exports a temporary DXF preview for HNL; source DWG is never overwritten.

Important: HNL Canvas DWG mode is not a full independent DWG decoder. Without AutoCAD Bridge, HNL directly opens DXF/HNL JSON; full-fidelity DWG remains Autodesk-native.

## v2.6.1 — Direct DWG Edit & Live Sync
- Direct DWG mode opens the DWG through AutoCAD and mirrors supported Model Space entities into HNL Canvas.
- HNL Canvas creation of Line/Polyline/Rectangle/Circle/Text/MText writes native entities back to the DWG.
- HNL selection is synchronized by AutoCAD handles.
- MOVE/ROTATE/SCALE/ERASE use direct DatabaseServices bridge actions.
- Ctrl+S remains native DWG save.
- Unsupported/proxy/custom objects remain authoritative in AutoCAD and are never removed merely because HNL cannot render them.
## v2.6.2 — Direct DWG Hardening

Deep audit after v2.6.1 found and corrected several important Direct-DWG issues:

- Default Save/Save As now follows the active workspace mode instead of merely checking whether AutoCAD happens to be connected. HNL Canvas Preview and Standalone no longer save an unrelated active AutoCAD DWG.
- Native commands, drafting toggles and layout changes are gated to `AUTOCAD_NATIVE` / `DIRECT_DWG`; HNL Canvas Preview remains local.
- Direct live sync is non-overlapping and adaptive (3/5/8 seconds depending on mirrored entity count); layer refresh is throttled to about 10 seconds.
- AutoCAD bridge action queue/results now have TTL, queue cap and one-shot result cleanup to avoid long-session memory/backlog growth.
- Direct local-only mutations are blocked instead of pretending to modify the DWG. Supported native paths remain enabled.
- Direct text translation writes DBText/MText through `UPDATE_TEXT_CONTENTS`.
- Palette block insertion can insert an existing native block definition through `INSERT_EXISTING_BLOCK`.
- Direct WALL_100/WALL_200 can create a native closed wall polyline.
- Rotate/Scale now use the selected geometry center rather than hard-coded 0,0.
- AI wall/ceiling execution in Direct DWG is routed to Preview/Smart Shopdrawing instead of silently changing only the HNL canvas.
- Project examples/tooltips/export samples were aligned to concealed-ceiling `1220/3 = 406.67 mm`; manufacturer/tested assembly values remain separate and are not silently rewritten.

AutoCAD .NET compilation/runtime still requires GitHub Actions Windows and installed AutoCAD.



## v2.6.3 — Version Sync Hardening

- Canonical renderer/server version constants.
- package.json / metadata / AutoCAD bundle / DLL AssemblyVersion / SketchUp Bridge synchronized to 2.6.3.
- GitHub artifact names derive from package.json instead of hard-coded version strings.
- `node scripts/check-version-sync.mjs` is a required GitHub build gate.
- Historical release documents keep their original version labels intentionally.
