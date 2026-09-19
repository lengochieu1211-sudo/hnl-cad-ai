# HNL CAD AI v2.0.0 — Lisp-Inspired 2D Professional

## Vì sao tái cấu trúc
44 Lisp trong AI.zip có nhiều chức năng tốt nhưng nếu tạo 44 nút riêng Ribbon sẽ khó dùng, khó test và dễ trùng logic.

v2.0 gom chúng thành 6 workflow chính:
1. Smart Text & Attribute
2. Field Doctor
3. Geometry Toolkit
4. Quick Dimension
5. Quantity / BOQ
6. Layout Automation

Ngoài ra giữ:
- Ceiling / Shopdrawing Studio
- CAD ⇄ SketchUp
- Plot / Publish / Sheet Set
- AI
- Legacy Lisp Manager

## Native mới trong v2.0
- Multi Find/Replace Text/MText/MLeader/Block Attribute.
- UPPER / lower / Title Case trên selection.
- Attribute renumber theo Tag + Prefix + Start.
- Replace Block theo tên.
- HNL Field metadata audit.
- Quick Dimension Bounding Box.
- Quantity summary theo Layer + Type:
  Count / Length / Area.
- Full catalog 44 Lisp:
  command / tên / nhóm / mode / priority / mô tả.

## Hybrid / AutoCAD
Các chức năng sau KHÔNG giả chạy Standalone:
- AutoCAD Field ObjectID repair.
- CFE/CFA/CFL/CFS/FieldObjects native DWG.
- CHSPACE / MS2PS.
- VP Freeze.
- Page Setup / PSLTSCALE / CTB/STB.
- Block Editor và nested block transaction.
- `.DST` native.
- DWG native operations.

HNL quản lý UI/workflow/preview/diagnostics; AutoCAD Bridge chịu trách nhiệm database/transaction native.

## Ribbon
Home → 2D Professional → Annotate → Blocks → Analyze → SketchUp → Layout → Publish → AI → Kỹ thuật.

## Trạng thái
Đây là source build-ready. Các chức năng AutoCAD native vẫn phải regression-test bằng đúng AutoCAD 2023–2026.
