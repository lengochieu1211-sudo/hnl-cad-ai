# HNL CAD AI v2.7.12 — 44 Lisp Taxonomy Cleanup

## Kết luận audit
Phân nhóm cũ có các lỗi semantic:
- APA và INC/MVAT nằm TEXT dù chức năng chính là Attribute.
- DM/RDM/FDM nằm BLOCK dù là Layer/Color.
- TLE nằm QUANTITY dù là Layer/Data.
- TCD nằm LAYOUT dù mục đích chính là trích chi tiết shopdrawing.
- QUANTITY tab trộn BOQ + Layer + Ceiling.
- TEXT tab trộn Text + Block/Attribute.
- Source matching chỉ dựa command, dễ match sai/khó chẩn đoán.

## Taxonomy chuẩn 44/44
| Nhóm | Số Lisp |
|---|---:|
| Text | 5 |
| Block / Attribute | 7 |
| Field / Links | 4 |
| Geometry / Polyline | 6 |
| Dimension | 3 |
| Layer / Properties | 2 |
| Quantity / BOQ | 2 |
| Shopdrawing / Detail / Ceiling | 3 |
| Layout / Viewport / Publish | 11 |
| Tools / Legacy | 1 |
| **Tổng** | **44** |

## Quy tắc
1 Lisp chỉ có 1 `sourceGroup` chính theo mục đích sử dụng.
Một công cụ HNL có thể gộp nhiều Lisp, nhưng không đổi nhóm nguồn.

## Các thay đổi quan trọng
- `LispCenter` bỏ BLOCK_FIELD/CEILING lẫn lộn; thêm BLOCK, LAYER, SHOPDRAWING.
- Mỗi catalog item có `sourceFile` đúng tên file AI.rar.
- `matchedFiles()` ưu tiên exact source filename, sau đó mới fallback command.
- Source UI hiển thị command runtime scan nếu có.
- Source UI có filter theo 10 nhóm và số lượng chuẩn.
- Tab 2D Pro tách Text khỏi Block/Attribute.
- Tab Data/BOQ/Ceiling bị bỏ; thay bằng Layer/Data, Quantity/BOQ, Shopdrawing.
- TCD chuyển Shopdrawing.
- DM/RDM/FDM chuyển Layer.
- TLE chuyển Layer.
- AF/LF/BF giữ Field vì công nghệ chính là AutoCAD Field/ObjectID.
- Quick Dim ngoài UI vẫn là workflow chính, nhưng source chỉ có đúng DN/DNC + JD + TKD.

## Gate
`scripts/audit-lisp-taxonomy.mjs`:
- catalog phải có đúng 44 sourceFile;
- không trùng sourceFile;
- mọi sourceFile phải tồn tại trong manifest AI.rar;
- group count phải đúng 44/44.
