# HCE AutoCAD Runtime Bridge Overlay - RC1

Base checkpoint: research/hce-pro-bootstrap @ 65bfbed5a273d209bdf2a65f824def0279b5b373 (v0.3.0).

The canonical C# Core and Golden fixture remain inside HCE-PAYLOAD, unchanged.
The CI workflow expands that frozen ZIP, then copies the adapter source in this overlay
into its AutoCAD adapter directory BEFORE Golden/build/installer. This avoids repacking
the frozen Core or accidentally modifying Golden.

Scope RC1:
- HCE/DTC/DEMTC use one entry point and calculation report.
- Multi-Hatch selection, direct straight-edge HatchLoop extraction (no HATCHEDIT/Region).
- Hatch pattern WCS origin and pattern angle mapped to unchanged Core default grid.
- Group by layer/ACI/pattern; packing once per material group.
- Read-only numeric preview; optional AutoCAD result Table, same report, one undoable write.
- Reject unsupported normals, solid fills, multi-loop/hole and curved edges with handle reason.
- Cancel/Exit makes no drawing changes.

NOT YET INCLUDED / NOT CLAIMED:
- visual tile-geometry preview/create: Core only returns aggregate inner full counts;
- safe handling of arc/ellipse/spline and multiple nested boundary loops;
- pattern spacing deduction from arbitrary user Hatch pattern;
- actual AutoCAD 2023 runtime, screenshot/Undo/DWG switch/GOLDEN confirmation.

This is a Runtime CANDIDATE; a passing build alone does not certify runtime.
