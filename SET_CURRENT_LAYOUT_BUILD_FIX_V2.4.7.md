# HNL CAD AI v2.4.7 — SET_CURRENT_LAYOUT Compile Fix

## Log analyzed
`logs_88310379934.zip`

All four AutoCAD projects fail with the same single compile error:

`BridgeCommands.cs(270,41): CS0103: The name 'SetCurrentLayout' does not exist in the current context`

Affected:
- AutoCAD 2023
- AutoCAD 2024
- AutoCAD 2025
- AutoCAD 2026

## Root cause
v2.4.6 added this Bridge action:

`"SET_CURRENT_LAYOUT" => SetCurrentLayout(payload)`

but the `SetCurrentLayout(JObject payload)` method was not actually inserted into `BridgeCommands.cs`.
The UI side therefore referenced an action whose C# handler did not exist.

## Fixed in v2.4.7
Added `SetCurrentLayout(JObject payload)`:
- gets active AutoCAD document;
- validates `name`;
- checks the real AutoCAD LayoutDictionary contains that name;
- locks document;
- assigns `LayoutManager.Current.CurrentLayout = name`;
- returns activated/currentLayout.

Added manual diagnostic command:
`HNLSETLAYOUT`

Existing layout workflow remains:
- `GET_LAYOUTS`
- `SET_CURRENT_LAYOUT`
- `RENAME_LAYOUT`
- `HNLRENLAYOUT`

## Static consistency checks
- switch contains `SET_CURRENT_LAYOUT => SetCurrentLayout(payload)`;
- `SetCurrentLayout(JObject payload)` exists exactly once;
- `HNLSETLAYOUT` exists;
- `RENAME_LAYOUT` handler exists;
- `GET_LAYOUTS` handler exists;
- AutoCAD capability list contains all three actions;
- 4 csproj + PackageContents XML parse PASS;
- Electron main/preload syntax PASS;
- TS/TSX syntax transpile PASS;
- @google/genai remains pinned to 2.4.0;
- ZIP/RBZ integrity PASS.

## Important
This environment cannot compile Autodesk DLLs directly. The next GitHub Actions run is the real C# compile test.
