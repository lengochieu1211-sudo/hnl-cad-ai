# HNL CAD AI v2.0.0 — Sơ đồ chức năng hiện hành

## Ribbon
Home → 2D Professional → Annotate → Blocks → Analyze → SketchUp → Layout → Publish → AI → Kỹ thuật.

## 2D Professional
1. Text & Attribute
2. Field Doctor
3. Geometry Toolkit
4. Quick Dimension
5. Quantity / BOQ
6. Layout Automation
7. Catalog 44 Lisp

## Trạng thái chức năng
- NATIVE: chạy trong HNL Standalone.
- NATIVE_AI: chạy HNL; AI là lớp hỗ trợ/gợi ý.
- HYBRID: HNL làm UI/logic, AutoCAD Bridge xử lý phần DWG native.
- AUTOCAD: phải có AutoCAD Bridge.

## Chức năng native bổ sung v2.0
- Find/Replace Text/MText/MLeader/Attribute.
- UPPER/lower/Title Case.
- Attribute renumber.
- Replace Block name.
- HNL Field metadata audit.
- Quick Dim bounding box.
- Quantity Count/Length/Area theo Layer/Type.

## Không giả Standalone
Field ObjectID, CHSPACE, VP Freeze, Page Setup, CTB/STB, PSLTSCALE, Block Editor, DST/DWG native.

## Tài liệu
- `AUDIT_44_LISP_TO_HNL_V2.0.0.md`
- `FINAL_AUDIT_V2.0.0.md`
- `RELEASE_V2.0.0_LISP_INSPIRED_2D_PRO.md`
