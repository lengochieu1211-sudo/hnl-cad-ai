# DEEP AUDIT — HNL CAD AI v2.6.3 VERSION SYNC HARDENING

## Kết quả audit v2.6.2 trước khi sửa
Không đồng bộ hoàn toàn. Các lệch active/release được phát hiện:
- `src/lib/branding.ts`: v2.6.0
- `metadata.json`: 2.3.0
- SketchUp extension source: 2.4.2
- Net Plugin Exporter templates: 2.6.0
- HNL Project JSON export: 2.6.0
- Diagnostics default: 2.3.0
- Usage Guide / SketchUp UI prerequisite: v2.3.0 RBZ
- README/build docs/scripts: 2.2.0–2.3.0
- AutoCAD csproj did not explicitly stamp DLL Version/AssemblyVersion/FileVersion
- GitHub artifact names were hard-coded, allowing future drift.

## v2.6.3 corrections
- Added `src/lib/version.ts` as canonical renderer/server version source.
- package.json and metadata synchronized to 2.6.3.
- Branding, App title, Project JSON, Diagnostics and Server health use canonical constants.
- AutoCAD Bridge has one `PluginVersion` constant.
- PackageContents AppVersion + all 2023–2026 ComponentEntry versions synchronized.
- All four AutoCAD csproj stamp Version, AssemblyVersion, FileVersion and InformationalVersion.
- SketchUp Ruby manifest/main and versioned RBZ synchronized.
- Net Plugin Exporter generated manifests/installers use canonical version.
- GitHub artifact names derive dynamically from package.json.
- Build workflow runs `node scripts/check-version-sync.mjs` before compile.
- Local BAT build scripts run the same version-sync gate and derive expected installer name dynamically.
- Historical docs are intentionally not rewritten; `docs/history` and old audit/release filenames preserve history.

## Release rule
`package.json` is the release version authority for packaging. `src/lib/version.ts` mirrors it for bundled renderer/server code and is verified automatically. A release fails CI if package, metadata, AutoCAD bundle/DLL, SketchUp source/RBZ, or active UI version sources disagree.
