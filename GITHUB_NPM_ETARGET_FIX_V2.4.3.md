# HNL CAD AI v2.4.3 — npm ETARGET Fix

## Log analyzed
`logs_88297496433.zip`

## What passed
The GitHub Actions log confirms the AutoCAD bridge plugin now builds successfully for:
- AutoCAD 2023 — PASS
- AutoCAD 2024 — PASS
- AutoCAD 2025 — PASS
- AutoCAD 2026 — PASS

The workflow reached `Install dependencies`, so the previous C# / AutoCAD SDK errors are resolved.

## Current failure
`npm install` failed with:

`npm error code ETARGET`
`No matching version found for @google/genai@2.4.2`

Cause:
The HNL application version was bumped to `2.4.2` using a broad text replacement, and this accidentally changed the independent npm SDK dependency from a valid package version to `@google/genai: 2.4.2`.

`@google/genai` has a published `2.4.0` release, while `2.4.2` is not a published release.

## Fixed in v2.4.3
- HNL application version → `2.4.3`.
- `@google/genai` pinned independently to exact version `2.4.0`.
- Added GitHub Actions dependency preflight:
  - checks package.json still pins `@google/genai` to `2.4.0`;
  - checks npm registry can resolve `@google/genai@2.4.0`;
  - only then runs `npm install`.
- App version bumps must no longer be applied blindly to dependency version strings.
- Kept `electron-builder --publish never`.
- Also cleaned the two nullable warnings shown during the otherwise-successful AutoCAD plugin builds.
- AutoCAD bundle / Desktop / Server / SketchUp version strings synchronized to `2.4.3`.

## Expected next pipeline
1. Checkout
2. Setup Node/.NET
3. Build AutoCAD 2023–2026 plugins
4. Validate npm dependency pins
5. npm install
6. TypeScript audit
7. Vite/Server build
8. electron-builder NSIS
9. Verify installer
10. Upload AutoCAD plugin bundle + Windows installer

## Local/static validation
- package.json parses
- `@google/genai === 2.4.0`
- Electron main/preload syntax PASS
- TS/TSX syntax transpile PASS
- AutoCAD csproj / PackageContents XML parse PASS
- ZIP/RBZ integrity PASS

The actual Windows EXE build must still be confirmed by the next GitHub Actions run.
