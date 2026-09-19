# HNL CAD AI v1.4.0 Professional Release Candidate

## Hardening implemented
- Full-project JSON Save/Open schema v2: entities, layers, layouts, viewports, smart objects, spreadsheet parameters, translation memory, block library, active layout.
- Backward-compatible AutoSave migration from schema v1.
- New Drawing resets the whole project and warns on unsaved changes.
- Dirty-state document title and Electron close guard.
- Safe Mode is now one shared state between Settings and execution UI.
- Gemini key can be stored with Electron safeStorage / Windows DPAPI.
- Local API remains bound to 127.0.0.1 and now requires a random per-session token for privileged /api endpoints in packaged mode.
- Electron sandbox enabled, CSP added, duplicate Delete menu removed.
- Installer defaults to per-user installation to avoid unnecessary administrator requirement.
- Version unified to 1.4.0.

## Remaining release limits
- Native DWG read/write, native AutoLISP execution and real AutoCAD ObjectId/Transaction require the separate AutoCAD bridge/plugin.
- Undo/Redo is still entity-history first; a future transaction history should cover every project subsystem atomically.
- Code signing certificate is not included; unsigned installers may trigger Windows SmartScreen.
- Full Windows installer smoke test must be run on Windows/GitHub Actions.
