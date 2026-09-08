# HNL VXT Pro - UI Compact + Layer & DIM

Head: cca67efa2ad28a2d2170ec6e13a57bd3589307d6

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
VxtCommands already reads `BlockReference.Layer` for XC/XP/Ty. The AutoCAD host now exposes that stored layer to the UI, and the compact palette synchronizes the visible Layer field whenever a CAD block pick updates the corresponding Block name. Manual Layer edits remain available after the pick.

## CI
- Beta CI #622 / run 34249056989: PASS
- Universal Matrix #558 / run 34249057266: PASS
- Core / Golden / DIM / Numeric Expression: 68/68 PASS
- AutoCAD 2023/2024/2025/2026 NET8/2026 NET10/2027: PASS
- Universal artifact ID: 10065305486
- Artifact ZIP SHA256: dd4523e46a79e005f9566ff13e9c9f548f5472c05499daca8104e334556c516f
- Setup EXE SHA256: 618bc7786a1b0820ef785c11b0a44225227997d05eea6647945ccda6545b613d

Runtime AutoCAD visual verification is still required before merging main.
