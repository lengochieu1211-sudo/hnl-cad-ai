# HNL CAD AI v2.6.2 — Deep Audit / Direct DWG Hardening

## Audit conclusion
v2.6.1 was structurally valid at the TS syntax/XML level, but a deeper code-path audit found several issues that meant it should not yet have been called fully hardened. v2.6.2 fixes the high-risk items below.

## High-risk issues found and fixed

### 1. Save routing could save the wrong AutoCAD drawing
In v2.6.1, `Ctrl+S` and Save As selected the native AutoCAD save path whenever the Bridge was connected, even when HNL was in Standalone or HNL Canvas Preview.

Fix: introduced explicit workspace routing:
- STANDALONE → DXF/HNL local save
- HNL_CANVAS_PREVIEW → local DXF save; never native DWG by default
- AUTOCAD_NATIVE → AutoCAD native DWG save
- DIRECT_DWG → AutoCAD native DWG save

### 2. Preview mode could accidentally route commands to AutoCAD
Native command aliases, Drafting toggles, Layout activation/rename and Smart Ceiling could use the Bridge simply because AutoCAD was connected.

Fix: native modification is now gated by `isNativeDwgWorkspace`, not raw Bridge connection.

### 3. Live Sync could overlap and backlog
v2.6.1 used a fixed `setInterval(1800)` while each snapshot could itself take longer than 1.8 s on a large DWG. Multiple requests could overlap.

Fix:
- single in-flight sync guard
- recursive timeout rather than overlapping interval
- adaptive interval: about 3 s / 5 s / 8 s based on mirrored entity count
- layer list refresh throttled to about 10 s

### 4. Bridge result memory leak / stale queue
The server result map retained completed results indefinitely and action queue had no cap/TTL.

Fix:
- action TTL 30 s
- result TTL 60 s
- queue max 200
- completed result removed after successful client read

### 5. HNL-only edits could look successful then disappear on next sync
Several old HNL tools still changed only React Canvas state. Direct Live Sync would overwrite those local edits with the real DWG snapshot.

Fix: central Direct-DWG guard blocks local-only `updateEntitiesWithHistory` operations and explains that a native/Smart tool must be used. This is safer than pretending the DWG changed.

### 6. Direct translation was not native
Fix: new `UPDATE_TEXT_CONTENTS` bridge action updates AutoCAD DBText/MText by handle, then re-syncs.

### 7. General HNL block palette insertion was local-only
Fix: new `INSERT_EXISTING_BLOCK` bridge action inserts an existing BlockTable definition and copies supplied attributes. If the definition does not exist, HNL tells the user to use Smart Library / Import DWG instead of faking an insert.

### 8. WALL_100 / WALL_200 was unsupported by CREATE_NATIVE_ENTITY
Fix: Direct DWG creates a closed native polyline footprint from wall centerline + thickness. Smart drywall systems still use the separate Smart Wall engine.

### 9. Rotate / Scale used world 0,0
Fix: HNL now derives the bounding-center of selected mirrored geometry and uses it as base point.

### 10. AI DRAW_WALL / DRAW_CEILING could modify only local canvas
Fix: in Direct DWG these plans now open/route to Smart Shopdrawing Preview/confirmation instead of applying fake local geometry.

### 11. Old 400-mm generic samples remained
Generic HNL examples/tooltips/export rows were updated to `1220/3 = 406.67 mm`. Manufacturer or tested-assembly records that explicitly use 400/600 remain separate source data and are not rewritten.

## Direct DWG operations verified by source wiring
- OPEN_DWG
- GET_MODELSPACE_SNAPSHOT
- SELECT_HANDLES
- CREATE_NATIVE_ENTITY
- APPLY_ENTITY_TRANSFORM
- ERASE_HANDLES
- SET_ENTITY_LAYER
- UPDATE_TEXT_CONTENTS
- INSERT_EXISTING_BLOCK
- SAVE_CURRENT_DWG / SAVE_AS_DWG
- CREATE_CEILING_SMART / CREATE_WALL_SYSTEM
- INSERT_LIBRARY_BLOCK
- GET_HNL_BOQ
- AUDIT_HNL_SHOPDRAWING

## Validation performed here
- 82 TS/TSX files syntax-transpiled: PASS
- Electron `main.cjs` and `preload.cjs`: `node --check` PASS
- 4 AutoCAD csproj + PackageContents.xml parse: PASS
- bridge capability/action mapping static checks: PASS
- save-mode safety checks: PASS
- non-overlap/adaptive-sync wiring checks: PASS
- queue/result cleanup wiring checks: PASS
- Direct text/block/wall native action wiring checks: PASS
- project rule check: no live `1220/4` HNL rule
- `@google/genai` remains pinned to 2.4.0
- ZIP integrity: PASS

## What is still NOT runtime-proven
This environment has no `dotnet`/AutoCAD runtime, so Autodesk .NET compilation and real AutoCAD behavior are not claimed as tested here. GitHub Actions must compile AutoCAD 2023/2024/2025/2026, then the installed plugin must be tested in AutoCAD.

Full independent DWG decoding is also not claimed: HNL Direct mode relies on Autodesk AutoCAD database engine through the Bridge.
