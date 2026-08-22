# HNL CAD AI v2.4.4 — AI Input Fix

Video `Đang ghi 2026-08-22 215708.mp4` shows the right-side HNL AI Palette remaining collapsed during the recorded CAD work.

## Fixed — HNL Desktop / Standalone
- Replaced the collapsed `position: fixed` AI overlay with a real 36px dock rail so Canvas cannot steal its click.
- Removed palette-wide `select-none`.
- AI prompt is now a 2-line textarea.
- Enter sends; Shift+Enter inserts newline.
- Pointer/mouse/keyboard events from AI input stop propagation to CAD handlers.
- Auto-focus when AI Chat opens.
- `min-h-0`/`shrink-0` fixes keep the input visible at the bottom.
- HTTP status and returned Command Plan are validated; errors are shown clearly.

## Fixed — AutoCAD Native HNLPALETTE
The native AutoCAD palette had no AI input at all. v2.4.4 adds:
- AI tab
- multiline prompt
- Ctrl+Enter Send AI
- Clear
- Open HNL Manager
- live status
- result/plan viewer
- drawing name and implied-selection count included in CAD context
- local communication through `%TEMP%/HNL_CAD_AI/bridge.json`
- server offline-rule fallback supported
- no destructive plan auto-executes

## Versioning
HNL app/plugin/server/bundle/SketchUp Bridge: 2.4.4
`@google/genai` stays independently pinned to 2.4.0.

## Validation
- all TS/TSX syntax transpile PASS
- Electron main/preload syntax PASS
- 4 AutoCAD csproj + PackageContents.xml parse PASS
- React and Native AI wiring static checks PASS
- ZIP/RBZ integrity PASS

AutoCAD C# runtime compilation still requires the GitHub Actions Windows build because the current environment has no .NET SDK/AutoCAD runtime installed.
