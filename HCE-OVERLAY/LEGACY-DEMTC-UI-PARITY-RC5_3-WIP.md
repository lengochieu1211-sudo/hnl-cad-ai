# HCE RC5.3 UI migration contract — SOURCE VERIFIED, WIP
Legacy canonical: HNL-DEMTC-RC18_5B_4C-TOPOLOGY-GATE-DIAGNOSTIC.lsp
SHA256: bc3f9f31f6128695d9a2f51285ba5938afa07613ec6f837695243b274c75fcf5
Legacy source stored (existing Drive 01_FULL_SOURCE):
https://drive.google.com/file/d/1naIwWhExp9TsVDbx0tP1AtpupCCCX_io/view

Source static scan: 2986 lines; Lisp delimiters balanced, no unterminated strings. NOT runtime certification.
Legacy DCL in file lines 1539-1742:
- sys600 / sys610 (family).
- m1 short / m2 long / m3 mixed.
- ms / ml: primary stock short / long in mixed. Primary fixes grid and full tile counts; secondary supplies cut pieces only.
- hx / hy: long orientation X/Y.
- g3 per-Hatch origin and angle, auto family lock from FIRST Hatch; g2 user selected origin and direction; g1 WCS 0,0.
- grid_snap toggle, grid_tol 0-10mm default 3.
- Text Style select, numeric text height 100, table text height 200.
- cut_list toggle and cut_sum/cut_detail (slow).
- errtile, default OK, cancel, preferences preserved.

Mode math (all dimensions NOMINAL for Core, not physical labels):
1 = 600 S grid 600x600 stock S600x600 label595x595
2 = 600 D grid 1200x600 for X or 600x1200 for Y, D stock label595x1190
6 = 600 M grid S600x600 if S priority or 1200x600/600x1200 if D priority; 2 stock kinds
3 = 610 S grid610x610 stock S610x610 label605x605
4 = 610 D grid1220x610 or 610x1220, D stock label605x1210
5 = 610 M grid S610x610 if S priority or D1220x610/610x1220 if D priority; 2 stock kinds

C# current DemtcOptions fields in frozen Core: GridWidth, GridHeight, SnapEnabled, SnapTolerance,
AllowRotate90, MixedMode, MixedPrimary, SmallStock, LargeStock.
Missing in RC5.2 GUI: ALL actionable controls (text-only fixed 610). AutoCAD bridge creates default
new DemtcOptions() and assumes UserDefined spacing exactly610. Therefore do not claim UI parity.
Legacy uses group DXF 43/44 Hatch pattern line base first, with VLA Origin fallback;
C# bridge currently uses Hatch.Origin only: phase mismatch risk. Investigate independently.

Proposed minimum safe code work (NOT IMPLEMENTED by this docs-only checkpoint):
RC5.3: separate per-DWG UI settings and adapter mapping for each family/module/priority/orientation/grid/snap.
RC5.4: TextStyle, text heights, full grouped material table, cut list summary and detail.
Regression gates: six modes; mixed priority S/D; long X/Y; grid Hatch/WCS/picked; snap on/off 0-10;
Blue18/Green6 Golden; Preview/Table share report; Cancel/Undo; change DWG; no leaked mode state.
Hard protection: NO changes to HCE-PAYLOAD and DemtcEngine for a presentation-only patch.
Golden default mode must match baseline DEM BLOCK2: full20290 boundary857 cuts872 bins299,
blue18 full66853 boundary3191 cuts3081, green6 full74 cuts52 slivers36.
Current RC5.2 last CI https://github.com/lengochieu1211-sudo/hnl-cad-ai/actions/runs/37782738820 PASS.
No new UI code in this checkpoint; source archive + audit only. State WIP, not Runtime Golden.
