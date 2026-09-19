# HNL CAD AI v2.6.1 — Direct DWG Edit & Live Sync

## New Direct DWG mode
HNL now offers three explicit DWG workflows:
1. AutoCAD Native — official full-fidelity editing in AutoCAD.
2. HNL Canvas Preview — temporary DXF preview; source DWG is not overwritten.
3. Direct DWG Edit — HNL Canvas mirrors supported Model Space entities while supported edits are written to the active native DWG database through HNL Bridge.

## Live sync
- `GET_MODELSPACE_SNAPSHOT` every 1.8 seconds by default.
- Mirror support: LINE, LWPOLYLINE, CIRCLE, DBText, MText, BlockReference.
- HNL shows returned/unsupported/truncated counts.
- HNL selection calls `SELECT_HANDLES`; AutoCAD implied selection returns during refresh.

## Direct native creation/edit
- `CREATE_NATIVE_ENTITY`: LINE, POLYLINE, RECTANGLE, CIRCLE, TEXT, MTEXT.
- `APPLY_ENTITY_TRANSFORM`: MOVE / ROTATE / SCALE.
- `ERASE_HANDLES`: safe direct erase.
- `SET_ENTITY_LAYER`: native layer assignment.
- Ctrl+S remains `SAVE_CURRENT_DWG`, therefore native Autodesk DWG save.

Complex topology commands TRIM/EXTEND/FILLET/OFFSET still route to AutoCAD native command engine. HNL does not claim independent full DWG editing for proxy/custom objects.

## Safety
Safe Mode asks confirmation before direct erase. Unsupported objects remain in AutoCAD and are never deleted simply because HNL cannot render them. HNL Canvas is a mirror; AutoCAD remains authoritative.

## Validation
TS/TSX syntax, Electron syntax, XML parse, static wiring, dependency pin and ZIP integrity are checked. Real Autodesk C# compile/runtime remains for GitHub Actions Windows + installed AutoCAD.

## Current boundary
View-camera zoom/pan synchronization between HNL Canvas and AutoCAD viewport is not yet bidirectional in v2.6.1. Geometry, selection, layer list, layout polling and drafting status are synchronized; complex native commands may still shift interaction to the AutoCAD command engine.
