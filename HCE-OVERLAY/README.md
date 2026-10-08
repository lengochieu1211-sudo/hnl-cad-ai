# HCE AutoCAD Runtime Bridge Overlay - RC3

Base: RC2 `ca2000064dfc676d0109e819501787e5623ae3fe`, branch `research/hce-pro-bootstrap`.

**Frozen**: `HCE-PAYLOAD/**` (Golden C# Core, fixture expected values, original installer) remains unchanged.
RC3 changes only overlay AutoCAD adapter and this README.

- New `HCEGOLDEN`: enumerate all Hatches in the current drawing space with no interactive selection, log handle/pattern/origin/grid/area, call the unchanged Core **once per accepted Hatch**, log full/boundary/cut/sliver/fallback/residual and consolidated material-group pack. **Read-only**; no entity creation.
- **Table safety**: if any selected Hatch was rejected during extraction or engine execution, print incomplete numeric preview but **do not create an incomplete quantity table**.
- Single-ring area guard: compare extracted shoelace area with `Hatch.Area` before calling Core; reject mismatch and log handle/reason.
- Duplicate-terminal polyline vertex with bulge is rejected rather than silently discarding a curved closing edge.
- Group preview additionally prints sliver counts for Runtime Golden comparison.
- HCE / DTC / DEMTC aliases, Golden policy 610x610 main, mixed small-main packing and five-version installer are otherwise unchanged.

## Runtime script checklist
1. **On a copy** of the Golden DWG/DXF, run `HCEQA`, select relevant Hatches, capture output.
2. Run `HCEGOLDEN` from Model Space (or explicitly switch to desired Layout). Use `LOGFILEON` / `LOGFILEOFF` to capture read-only per-handle and aggregate counts.
3. Group by layer/ACI/pattern. Reconcile per-handle rejections before accepting any table output.
4. Run `HCE`, `DTC`, `DEMTC` on the same selection to check parity; verify 610mm and millimeter units before `Use610`.
5. Verify Exit/Cancel creates no objects, Table creates one entity, Undo reverses it, and re-run/switch DWG works.
6. Compare with unchanged Golden DEM BLOCK 2 (full 20290, boundary 857, cuts 872, bins 299), 18 blue (full 66853, cuts 3081, boundary 3191), 6 green (full 74, cuts 52, slivers 36).
7. Store real AutoCAD 2023 logs/screenshots/DXF identifiers in existing Drive `05_SCREENSHOT_VIDEO_LOG`.

**Gate**: CI PASS only certifies Core Golden fixture and AutoCAD binary compilation, **not** AutoCAD runtime, actual DXF parity or geometric Preview/Create parity. Multi-loop/hole, curved edges and non-+Z Hatch stay explicitly unsupported by RC3; no silent approximations.
