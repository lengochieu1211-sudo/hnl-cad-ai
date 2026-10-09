# HNL VXT Pro — Golden Verification Plan

V6.7.4 AutoLISP là baseline. Nút Create của v7 vẫn LOCKED cho tới khi các case sau đạt parity.

| ID | Scenario | v7 Preview | v7 Real Create |
|---|---|---:|---:|
| G01 | Rectangle 6000x4000 default | PENDING | LOCKED |
| G02 | Rectangle xoay 30° | PENDING | LOCKED |
| G03 | L-shape Polyline kín | PENDING | LOCKED |
| G04 | U-shape Polyline kín | PENDING | LOCKED |
| G05 | Boundary có bulge/arc | PENDING | LOCKED |
| G06 | XC only | PENDING | LOCKED |
| G07 | XP only | PENDING | LOCKED |
| G08 | XC + Ty | PENDING | LOCKED |
| G09 | Auto DIM XC/XP/Ty | PENDING | LOCKED |
| G10 | DIM Top/Bottom/Left/Right | PENDING | LOCKED |
| G11 | Né thiết bị | NOT PORTED | LOCKED |
| G12 | Existing XC/reuse | NOT PORTED | LOCKED |
| G13 | Dynamic block modes | NOT PORTED | LOCKED |
| G14 | Undo toàn bộ 1 lần | NOT PORTED | LOCKED |

So sánh bắt buộc: số lượng, tọa độ, spacing, edge offsets, rotation, layer/block, cạnh DIM, offset DIM, thứ tự lớp DIM và không để lại temporary entities trong DWG.
