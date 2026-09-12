# HNL VXT Pro - UI Compact + Layer & DIM

Code head for the compact UI implementation: `cca67efa2ad28a2d2170ec6e13a57bd3589307d6`  
Certified branch head after this note update: `a4d04607ce90178388a69b189315b19a896a3091`

## Runtime UI changes
- Header reduced to a compact HNL/VXT title row; redundant subtitle is hidden in the AutoCAD palette.
- Footer reduced to the primary Create button; obsolete Alpha/Golden footer note is hidden.
- Card padding/margins and row spacing are reduced for better usable height.
- TextBox, ComboBox and HnlNumericBox controls are normalized to 27 px and stretch to one input axis.
- Form label columns are normalized to 142 px; input/dropdown columns stretch; CAD pick buttons use a common 68 px column.
- `LAYER & KIỂU NÉT` is surfaced as `LAYER & DIM`, moved directly below PHẠM VI BỐ TRÍ and expanded by default.
- Duplicate Layer DIM / DimStyle fields inside the DIM card are removed at runtime so resources have one source of truth.
- The existing Layer & DIM editor retains XC/XP/Ty/DIM Layer, ACI Color, Linetype, Lineweight and DIM Style.

## Block -> Layer parity
`VxtCommands` reads `BlockReference.Layer` for XC/XP/Ty. The AutoCAD host now exposes the stored selected-block layer to the compact palette, which synchronizes the visible Layer field whenever a CAD Block pick updates the corresponding Block name. Manual Layer edits remain available after the pick.

## Certified CI
- Beta CI #624 / run `34249522898`: PASS
- Universal Matrix #560 / run `34249522940`: PASS
- Core / Golden / DIM / Numeric Expression: 68/68 PASS
- AutoCAD 2023 / NET48: PASS
- AutoCAD 2024 / NET48: PASS
- AutoCAD 2025 / NET8: PASS
- AutoCAD 2026 pre-1.2 / NET8: PASS
- AutoCAD 2026 1.2+ / NET10: PASS
- AutoCAD 2027 / NET10: PASS
- Universal Setup / installer gate: PASS

## Certified artifact
- Universal artifact ID: `10065455757`
- Artifact ZIP SHA256: `1b0956c8ac90d83c045a800eadf1c3ab79f978669af86c3484dc7bbf30bc9f8c`
- Setup EXE SHA256: `b63154ed70075858a9a4c84526e7c0bddaad5d75d89a589e508440fcbebcc09e`
- Drive handoff file ID: `17JFzm580svsA_h9ESqz9mNApTcB-AwF0`

Runtime AutoCAD visual verification is still required before merging main.
