# HNL CAD AI v2.8.1 — Bridge P0 Build Gate

## Mục tiêu
Build đúng source patched v2.8.1 và bắt buộc AutoCAD 2023/2024/2025/2026 đều **0 CS8604** trước khi chuyển sang runtime Golden Test.

## CI bắt buộc
Workflow `.github/workflows/build-windows.yml` phải PASS các gate:

1. Version sync = 2.8.1.
2. 44 Lisp + 1 ARX nguồn; ARX 2021 không bundle cho AutoCAD 2023–2026.
3. AutoCAD plugin build 2023/2024/2025/2026.
4. Mỗi target phải in `nullable gate PASS: 0 CS8604`.
5. Lisp taxonomy = 44/44.
6. Master Audit = 22/22 PASS.
7. Bridge P0 static = 23/23 PASS.
8. TypeScript `tsc --noEmit` PASS.
9. Installer `HNL_CAD_AI_Setup_2.8.1.exe` tồn tại và > 5 MB.
10. Upload AutoCAD plugin bundle + Windows installer artifact.

## Promotion rule
- Build PASS **không** đổi BRG-01/BRG-02 sang VERIFIED.
- BRG-01 chỉ VERIFIED sau `Golden Test Bridge` trên AutoCAD thật.
- BRG-02 chỉ VERIFIED sau Golden PASS + stress 20 lần + busy/ESC/restart/reconnect, không late action.
