# HNL CAD AI v2.7.0 — Video/F8/DWG/Ribbon/Layer Audit

## Video findings
Video reviewed: `Đang ghi 2026-08-23 101854.mp4` (~103.6 s, 1354x720).

### 1. F8 / ORTHO defect confirmed
At roughly 42–54 s the PLINE session shows ORTHO enabled, yet the current segment becomes diagonal.

Root causes in v2.6.3:
- PLINE ORTHO constraint used `tempPoints[0]` (the first vertex) instead of the last PLINE vertex.
- OSNAP returned early from mouse-move before the ORTHO constraint was applied.
- mouse-down and Dynamic Input commit could use an unconstrained point.

v2.7.0 fix:
- PLINE constraint origin = last vertex.
- ORTHO is the final geometric constraint.
- off-axis OSNAP candidates no longer bypass F8.
- mouse preview, click commit and Dynamic Input commit share the same constraint logic.

### 2. Direct DWG open failure/recovery
At the beginning of the video HNL is in Standalone/CAD OFF while Direct DWG is selected. The old flow required the AutoCAD Bridge to already be connected and rejected/failed the DWG workflow.

At roughly 92–95 s AutoCAD appears, but the flow is not seamless back into Direct DWG.

v2.7.0 fix:
- when Direct DWG / AutoCAD Native is requested while Bridge is offline, Electron looks for AutoCAD 2026/2025/2024/2023 `acad.exe`;
- falls back to Windows DWG association if necessary;
- launches the selected DWG;
- waits up to 30 seconds for HNL.CadBridge to register;
- automatically enters Direct DWG + Live Sync after connection;
- clear diagnostic shown if AutoCAD opens but the plugin does not connect.

## HNL CAD layer standard
All HNL-generated ceiling/wall/library geometry is intended to be BYLAYER.

| Layer | ACI | Lineweight | Linetype | Purpose |
|---|---:|---:|---|---|
| HNL-CLG-BOARD | 151 | 0.18 mm | Continuous | Board/joint |
| HNL-CLG-MAIN | 30 | 0.35 mm | Continuous | Main runner |
| HNL-CLG-CROSS | 2 | 0.25 mm | Continuous | Cross runner, 1220/3 |
| HNL-CLG-HANGER | 3 | 0.18 mm | HIDDEN2 | Hanger |
| HNL-CLG-START | 4 | 0.25 mm | CENTER2 | Board start/direction |
| HNL-WALL-BOARD | 7 | 0.18 mm | Continuous | Wall board/joint |
| HNL-WALL-STUD | 6 | 0.25 mm | Continuous | Stud |
| HNL-WALL-TRACK | 5 | 0.35 mm | Continuous | Track |
| HNL-WALL-REINF | 1 | 0.40 mm | Continuous | Door/header reinforcement |
| HNL-STEEL-RHS | 1 | 0.35 mm | Continuous | RHS/SHS |
| HNL-STEEL-PLATE | 30 | 0.35 mm | Continuous | Plate |
| HNL-ANNO-SECTION | 7 | 0.35 mm | Continuous | Section mark |
| HNL-ANNO-LEVEL | 4 | 0.25 mm | Continuous | Level mark |
| HNL-ANNO-DETAIL | 2 | 0.25 mm | Continuous | Detail mark |

AutoCAD Bridge now creates/updates these layer properties and loads HIDDEN2/CENTER2/DASHED when available from `acadiso.lin` / `acad.lin`.

Native command: `HNLLAYERSYNC`.
Bridge action: `ENSURE_HNL_STANDARDS`.

Direct DWG layer sync now returns color, lineweight, linetype and plot flag; HNL Canvas renders BYLAYER color/lineweight/dashed-center patterns instead of forcing white/continuous.

## Dynamic Block support
v2.7.0 adds native Dynamic Block support for block references already defined as dynamic in the DWG:
- `GET_DYNAMIC_BLOCK_PROPERTIES`
- `SET_DYNAMIC_BLOCK_PROPERTIES`
- inserted existing blocks return `isDynamicBlock` and property metadata.

The HNL library marks shopdrawing symbols as **Dynamic Block preferred** and keeps layer standards separate from the block definition.

Important engineering limitation:
- the generated HNL starter symbols are still safe static fallback symbols;
- HNL does not fake AutoCAD Dynamic Block constraints/actions;
- actual company/project Dynamic Block DWGs should be imported/kept as true AutoCAD Dynamic Blocks, then HNL can insert them and edit their Dynamic Properties.

## AutoCAD HNL Ribbon redesign
Removed duplicate native CAD buttons from the HNL tab:
- LINE / PLINE / TRIM
- Layer
- Properties
- Plot / Publish
- ordinary Selection tools

These remain in normal AutoCAD tabs where they belong.

New HNL-only compact groups with generated icons:

### AI
- AI Copilot
- Manager

### Shopdrawing
- Ceiling
- Wall
- Library

### 2D Pro
- Text / Attribute
- Field Doctor
- Geometry
- Quick Dimension

### Data / Layout
- BOQ
- Layout+
- Audit

### Tools
- Lisp Center
- Standards
- Manager
- Bridge

The Ribbon now assigns 16/32 px icon images and large native AutoCAD buttons instead of text-only items.

## 44 Lisp integration
The Lisp-inspired system remains consolidated rather than exposing 44 duplicate buttons.

Native AutoCAD launch commands now route directly into the HNL 2D Professional Tool Center:
- `HNLTEXT` -> Text / Attribute / Block
- `HNLFIELD` -> Field Doctor
- `HNLGEOM` -> Geometry
- `HNLDIM` -> Quick Dimension
- `HNLQTY` -> Quantity / BOQ / Ceiling
- `HNLLAYOUTAUTO` -> Layout / Publish automation
- `HNLLISP` -> 44 Lisp source center

If HNL EXE is already running, AutoCAD passes `--hnl-tool=...` into the existing instance and opens the corresponding tab.

## Validation performed
- Version sync gate: PASS 2.7.0
- TS/TSX syntax transpile: PASS 83 files
- Electron `node --check`: PASS
- AutoCAD csproj + PackageContents XML parse: PASS 5
- C# brace/static source checks: PASS
- F8/ORTHO static regression checks: PASS
- Direct DWG auto-launch/wait wiring: PASS
- HNL color/lineweight/linetype wiring: PASS
- Dynamic Block API wiring: PASS
- 44 Lisp routing: PASS
- HNL Ribbon contains no duplicated LINE/PLINE/TRIM/PROPERTIES/PLOT/PUBLISH buttons: PASS

Not verified locally:
- Autodesk .NET compile against AutoCAD assemblies (no dotnet SDK in this runtime)
- actual AutoCAD 2023 runtime rendering of the reflection-based Ribbon icons
- actual dynamic property editing against every third-party Dynamic Block definition

GitHub Actions Windows remains the compile gate; AutoCAD 2023 on the user's machine remains the runtime gate.
