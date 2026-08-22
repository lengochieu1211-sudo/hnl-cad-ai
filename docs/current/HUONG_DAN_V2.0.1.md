# HNL CAD AI v2.0.4 — Sơ đồ chức năng hiện hành

## Ribbon
Home → 2D Professional → SketchUp → Layout & Publish → AI & Legacy → Kỹ thuật.

## 2D Professional Tool Center
1. Text / Attribute / Block
2. Field & Links
3. Geometry
4. Dimension
5. Data / BOQ / Ceiling
6. Layout & Publish
7. Công cụ chuẩn — 17 mục
8. 44 Lisp nguồn — chỉ để tra cứu/Legacy

## Chống trùng
- 44 Lisp nguồn → 17 công cụ chuẩn.
- Mỗi Lisp nguồn chỉ có một owner.
- Gõ lệnh Lisp cũ trong Command Search vẫn tìm được, nhưng mở tool chuẩn tương ứng.
- Không tạo 44 nút mới trên Ribbon.

## Trạng thái
- NATIVE: HNL Standalone.
- NATIVE_AI: HNL + AI hỗ trợ.
- HYBRID: HNL UI/logic + AutoCAD Bridge khi đụng DWG native.
- AUTOCAD: yêu cầu AutoCAD Bridge.

## Tài liệu kiểm tra
- `AUDIT_LISP_DEDUP_LAYOUT_V2.0.4.md`
- `AUDIT_44_LISP_TO_HNL_V2.0.0.md` (audit nguồn ban đầu)
