HNL HCE RC5.1 — Default Palette command patch
Base exact HEAD 1a382276fff19958e5adfc441226881666e0d8d5.
Root cause: RC5 used HCE for direct Command Line calculation and HCEUI for its VXT-style WPF Palette, so typing HCE never opened the UI.
Safe change: HCE aliases HcePaletteCommands.Open, HCECALC runs the unchanged RunCeilingEstimator method, and the Palette button dispatches HCECALC to avoid reopening itself. HCEUI remains a backwards-compatible Palette alias; DTC and DEMTC still invoke the original calculation.
Impact: AutoCAD adapter routing only, plus non-runtime CI assertions. Unchanged: Golden engine, geometry, packing, Hatch extraction, table and installer icon. A live Windows AutoCAD 2023 runtime test is required to verify first-open focus, Palette docking and click-to-select Hatch; GitHub Actions builds do not certify runtime.
Expected: HCE -> Palette; HCEUI -> Palette; HCECALC -> selection/calculation/table; DTC/DEMTC -> selection/calculation/table.
