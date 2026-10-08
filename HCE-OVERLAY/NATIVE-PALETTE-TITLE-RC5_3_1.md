HCE RC5.3.1 — Native AutoCAD Palette title cleanup.
Source base exact HEAD 8d9e30f1c88932409c933659d16e88eaabdb6286. No Core, geometry, table calculations, or controls touched.
Root cause: WPF view rendered a second blue logo/name/version header plus a branded footer below the native AutoCAD PaletteSet caption, wasting height and duplicating identity.
Patch HcePaletteUi.cs: native PaletteSet caption becomes "HNL HCE Pro | dd/MM/yyyy HH:mm". CI injects a fixed build timestamp in Vietnam local time UTC+07 before five AutoCAD builds. Removed inner image/header/footer entirely; first view is the 2-tab control. Palette logo in the INSTALLER and setup branding remains unchanged.
Workflow asserts caption wiring, logo/header/footer absence, stamps once for all builds, and leaves Golden and Core policy unchanged.
Runtime tests still needed: Windows AutoCAD 2023 docked title visibility/truncation, close/reopen, theme, DWG switching, command-selection focus. Not Runtime Golden.
