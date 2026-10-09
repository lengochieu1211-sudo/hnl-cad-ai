HCE RC5.4 — CAD Table presentation-only patch
Base RC5.3.3 exact HEAD 43c4ac79d7ce85e99d8e150cd001ded92f81230b.
Root: original DEMTC Text Style and Table font size never migrated into C# Palette; Table output was hardcoded at rowHeight55/columnWidth230.
Scope: HceLegacyProfile.cs; HcePaletteUi.cs; Commands.cs Table method; workflow CI gate. Core/Geometry/Packing/Golden/Hatch ingestion unchanged.
GUI: 'Kiểu chữ bảng' dropdown lists current DWG Text Styles and preserves per-DWG choice in memory. 'Cao chữ bảng (mm)' accepts 0..10000, 0 preserves previous RC5.3.3 table formatting.
Adapter: style & height applied to Title/Header/Data RowType before any Table transaction is committed. Missing selected style fails Table creation rather than silently substituting.
Known pending: old Lisp numeric tile labels/text height not implemented because HCE Core provides aggregate counts, not tile-by-tile geometry. Cut lists summary/details also not yet restored. No real Windows AutoCAD Runtime in CI. No Golden change expected.
