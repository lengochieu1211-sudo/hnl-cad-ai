# HNL CAD AI v2.4.9 — UI Occlusion / Snap / AI Input Audit

## Video reviewed
`Đang ghi 2026-08-22 232036.mp4`
- duration: ~419.6 seconds
- capture size: 1354 × 732

## Visible problems confirmed

### 1. Top Application Bar clips tools on the right
At 1354 px the file name + full AutoCAD bridge text + search + workbench selector consume too much horizontal space.
Result: Project / Tools / AI / Focus / Collapse buttons can be partially or fully hidden.

Fix:
- file chip only expands on 2XL;
- AutoCAD bridge text becomes compact below 2XL;
- search becomes flexible;
- Project/Tools/AI/Focus text labels collapse to icons below 2XL;
- workbench selector has a bounded width;
- top bar no longer relies on invisible horizontal scrolling.

### 2. OSNAP settings opens in the wrong direction
`OsnapIndicator` was placed in a toolbar at the TOP of Canvas, but its popover used:
`bottom-full`
so it opened upward into Ribbon/outside the clipped Canvas.

Fix:
- popover now uses `top-full`;
- opens downward into Canvas;
- max height constrained to viewport;
- snap tooltip position is clamped inside Canvas.

### 3. Fake SNAP status at the global bottom bar
The outer status bar always displayed decorative:
`SNAP ORTHO GRID`
independent of the actual Canvas F8/F9/F3 state.

Meanwhile the real Canvas status was hidden because the `<canvas>` used `h-full` inside a flex column and pushed the real status bar out of the clipped container.

Fix:
- Canvas changed from `h-full` to `flex-1 min-h-0`;
- real status bar is visible;
- adds live:
  - SNAP F9 (Grid Snap)
  - OSNAP F3 (Object Snap)
  - OTRACK F11
  - ORTHO F8
  - DYN F12
- outer fake SNAP/ORTHO/GRID labels removed.
This also distinguishes correctly between **SNAP F9** and **OSNAP F3**.

### 4. AI input is below the fold
The AI tab permanently stacked:
- CAD Context
- Auto Detail launcher
- Drywall launcher
- 3 prompt chips
- Messages
- Input

At ~732 px height this leaves the textarea below the visible area.

Fix:
- AI tab content itself no longer scrolls as one giant page;
- message stream is the scrolling area;
- input is sticky at the bottom;
- context condensed to one row;
- quick launchers/prompts collapsed by default behind `Công cụ nhanh & gợi ý`.

### 5. AI Palette tab bar overflows horizontally
Six text tabs at ~300 px palette width caused a visible horizontal scrollbar and hidden tabs.

Fix:
- 2 rows × 3 columns:
  AI / Vẽ nhanh / Block
  Lisp / Dịch / Audit
- no horizontal tab clipping.

### 6. Dynamic Input can leave the Canvas near edges
Dynamic Input was always placed cursor +22/+16 px.

Fix:
- dynamic input HUD is clamped to the current Canvas width/height using ResizeObserver.

### 7. Ribbon tool groups can be hidden with no visual indication
Large contextual tool groups used horizontal overflow with hidden scrollbar.

Fix:
- native thin scrollbar is visible on crowded Ribbon tool panels.

### 8. Layout tabs versus global status
Layout tab row had scrollable tabs plus a fixed large fake CAD status on the right.

Fix:
- layout tabs use `flex-1 min-w-0`;
- each tab is `shrink-0`;
- right status reduced to Units + AutoCAD connection only.

### 9. Toast versus Command Line
Toast at `bottom-12` could overlay Command Line / Layout tabs.

Fix:
- when Command Line is visible the toast is raised to `bottom-20`.

## Other interfaces reviewed in the video
- Diagnostics Center: modal frame/scroll behavior is reasonable.
- Knowledge Base / technical modal: no visible clipping in the recording.
- Project Tree / layer list: scrollable and usable; no blocking overlap observed.
- Model/Layout drawing region: no UI overlay defect observed after accounting for the right/left docks.

## Static verification
- all TS/TSX syntax transpile PASS;
- Electron main/preload syntax PASS;
- 4 AutoCAD csproj + PackageContents XML parse PASS;
- v2.4.8 native Palette registration fix remains included;
- @google/genai remains pinned to 2.4.0;
- ZIP/RBZ integrity PASS.

Actual Windows/AutoCAD runtime verification still requires GitHub Actions and the installed app.
