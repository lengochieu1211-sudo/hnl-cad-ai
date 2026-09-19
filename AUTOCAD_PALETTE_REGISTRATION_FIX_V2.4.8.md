# HNL CAD AI v2.4.8 — AutoCAD Palette Command Registration Fix

## Runtime symptom
AutoCAD accepts:

`HNLBRIDGEPING`

and prints pairing OK, but:

`HNLPALETTE`

returns:

`Unknown command "HNLPALETTE"`

This proves `Hnl.CadBridge.dll` is loaded but the palette command class is not being registered by AutoCAD.

## Root cause in v2.4.7
- `HNLBRIDGEPING` belongs to `BridgeCommands`, the class implementing `IExtensionApplication`.
- `HNL`, `HNLPALETTE`, and `HNLHIDE` belonged only to a separate `NativePaletteCommands` class.
- On the affected AutoCAD installation, commands in `BridgeCommands` are discovered, while the separate palette command class is not.
- The source also contained duplicate bridge command attributes for `HNLBRIDGESTATUS`, `HNLBRIDGEPING`, `HNLPLOTDEVICES`, and `HNLLAYOUTS`, which was unnecessary and could make command registration less deterministic.

## Fix in v2.4.8
### Command ownership moved to BridgeCommands
`BridgeCommands` now directly owns:
- `HNL`
- `HNLPALETTE`
- `HNLHIDE`
- `HNLPALETTESTATUS`

These commands call static palette APIs:
- `NativePaletteCommands.ShowPaletteWindow()`
- `NativePaletteCommands.HidePaletteWindow()`
- `NativePaletteCommands.IsPaletteVisible`

### Explicit AutoCAD assembly registration
New `CommandRegistration.cs`:
- `ExtensionApplication(typeof(BridgeCommands))`
- `CommandClass(typeof(BridgeCommands))`
- `CommandClass(typeof(NativePaletteCommands))`

This removes reliance on AutoCAD's implicit class scan.

### Duplicate command attributes removed
The second duplicate definitions of:
- HNLBRIDGESTATUS
- HNLBRIDGEPING
- HNLPLOTDEVICES
- HNLLAYOUTS

were removed. The richer first implementations remain.

## Expected test after installing the new bundle
1. Close every AutoCAD window.
2. Remove the old `HNL.CadBridge.bundle`.
3. Copy the v2.4.8 bundle to `%APPDATA%\Autodesk\ApplicationPlugins`.
4. Reopen AutoCAD.
5. Startup text should include:
   `Commands: HNL, HNLPALETTE, HNLHIDE, ...`
6. Test:
   - `HNLBRIDGEPING`
   - `HNLPALETTESTATUS`
   - `HNLPALETTE`
   - `HNL`
   - `HNLHIDE`

## Static validation
- Palette command methods exist in BridgeCommands.
- NativePalette exposes static Show/Hide/Status.
- Explicit CommandClass registration exists.
- duplicate HNLBRIDGEPING/HNLBRIDGESTATUS command attributes removed.
- TS/TSX syntax transpile PASS.
- Electron main/preload syntax PASS.
- 4 AutoCAD csproj + PackageContents.xml parse PASS.
- @google/genai remains 2.4.0.
- ZIP/RBZ integrity PASS.

Actual AutoCAD DLL compilation/runtime must be confirmed by GitHub Actions and AutoCAD.
