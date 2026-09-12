# HNL Tool - VXT Pro v7 | VXT V6.7.2 Parity Audit

Date: 2026-09-06
Source of truth: original `Vẽ Xương Trần V6.7.2_VXT.lsp` supplied in the HNL Tool project.
Target: `HNL Tool - VXT Pro v7.0.0-beta.1`, AutoCAD 2023-2027.

## Audit result

The original Lisp contains 38 local/public functions. Every functional group has a mapped implementation in VXT Pro. Legacy DCL-only preview drawing is intentionally replaced by WYSIWYG AutoCAD transient preview; this is a functional upgrade, not a removed drawing feature.

The final parity pass specifically restored edge paths that were easy to miss in a UI-first port:

- Auto direction asks Shadowline Yes/No at Create time and applies the answer per ceiling geometry.
- Many closed ceiling Polylines remain independent layout domains.
- `ask_each` stores XP start side independently for each selected ceiling Polyline; manual HCN regions retain their own XP side and are not asked twice.
- XC off + Ty on can start without a ceiling boundary, then select existing XC and distribute Ty on it.
- Existing XC/XP/Ty can be selected as DIM sources when the corresponding system is not redrawn.
- One-side Ty on existing XC asks separate horizontal and vertical directions.
- Equipment selection Cancel/Enter clears the old selection set like Lisp `ssget -> nil`.
- Picking XC/XP/Ty sample blocks captures both effective block name and source layer.
- Reset preserves the current DIM style and does not falsely display equipment selections as cleared.
- If Ty is enabled but the Ty block does not exist, the workflow stops before drawing XC/XP, matching the original global block gate.
- No-feature-selected is rejected instead of creating an empty successful run.

## Functional mapping

| Original Lisp function/group | VXT Pro implementation | Status |
| --- | --- | --- |
| `c:VXT`, create workflow | `HVX`, `HNLVXTCREATE`, `VxtLegacyParityCoordinator`, `VxtCreateEngine` | Ported |
| `*error*`, Undo/Error handling | transaction rollback + `VxtDiagnosticService` | Ported/strengthened |
| `get-dim-styles`, `get-linetypes` | `VxtHostBridge` symbol-table readers | Ported |
| DCL main/advanced/settings dialogs | WPF `VxtPaletteView` + `VxtPaletteEnhancer` | Replaced by Pro UI |
| DCL preview A-E diagrams | `VxtTransientPreview` using real Core plan | Upgraded |
| `setup-layer` | `VxtCadResources` | Ported |
| floor/ceil/substitute helpers | `SmartLayout1D` helpers | Ported |
| `adjust-grid` | `SmartLayout1D.AdjustGrid` | Ported + safety fallback |
| equipment bbox/filter/rotated bbox | `VxtLayoutContextFactory` + Core transforms | Ported |
| `is_grid_clear`, `optimize_grid_offset` | fixed-grid/obstacle optimizer in Core | Ported |
| `place_ty_dynamic` | generated `BuildHangerRow` + `ExistingMemberLayout.HangerPoints` | Ported |
| `get-ss-coords`, `get-ty-ss-coords` | `VxtManualExistingEngine` | Ported |
| `make-mlstyle-xp35` | `VxtCreateEngine.EnsureXp35Style` | Ported |
| `draw-pline` | dynamic block / MLINE XP_35 / Polyline create bridge | Ported |
| `clean-points`, repeat helper | Core tolerance/list helpers | Ported |
| `calc-smart-layout` | `SmartLayout1D` | Golden-locked |
| `create-dim`, `process-dims`, `get-dim-placement` | `VxtPreviewPlanBuilder` DIM chains + real `RotatedDimension` | Ported |
| `get-bone-axis` | `ExistingMemberLayout.FromBounds` | Ported |
| `draw_bars`, `do_draw_line` | Core scanline clipping/plan builder + Create bridge | Ported |

## Default values locked from Lisp

- XC: Min/Max 700/1000; edge Min/Max 300/400; balance step 50; skip 500.
- Ty: Min/Max 700/1000; edge Min/Max 300/400; balance step 50.
- XP: exact `1220.0 / 3.0`; still user editable for other board widths/systems.
- Avoidance: enabled; clearance 20; shift-all preferred.
- Ty layout: balanced by default.
- DIM: disabled by default; distance 500; row spacing 350.
- Default layers/colors/linetypes/lineweights and AP block names remain compatible with the original drawing standard.

## Economic layout rule

For XC and Ty, candidate layouts prioritize engineering constraints and material economy: minimize member/hanger count first, then prefer large spacing near Max, then edge balance/increment quality. Obstacle safety remains higher priority when geometry makes all spacing/edge requirements mutually impossible.

The legacy exact 4000-run tie remains unchanged where two layouts use the same material quantity. Wide-run economic behavior is locked by tests.

## Certification gates

Automated certification requires:

1. Core/Golden/DIM/Numeric tests PASS.
2. AutoCAD 2023 NET48 bridge PASS.
3. AutoCAD 2024 NET48 bridge PASS.
4. AutoCAD 2025 NET8 bridge PASS.
5. AutoCAD 2026 pre-1.2 NET8 bridge PASS.
6. AutoCAD 2026 1.2+ NET10 bridge PASS.
7. AutoCAD 2027 NET10 bridge PASS.
8. Universal Setup EXE build and installer verification PASS.

A real installed AutoCAD Runtime Golden remains a separate final field gate because GitHub Actions does not run an interactive licensed AutoCAD desktop session.

## Release safety

This work remains on `research/vxt-pro-v7-bootstrap`. PR #1 stays Draft / DO NOT MERGE. `main` must not be changed until the real AutoCAD Runtime Golden is accepted.
