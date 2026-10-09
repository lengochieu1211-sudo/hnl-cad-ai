# HNL CAD AI v2.5.2 — Native HNL Ribbon + Multi-Provider AI

## Native AutoCAD HNL Ribbon
AutoCAD now attempts to create a native `HNL` Ribbon tab after the Ribbon UI is ready.

Panels:
- AI: HNL AI, AI Settings/Manager, Selection
- 2D Pro: HNL 2D, Smart Ceiling, Polyline, Trim
- Data / BOQ: Data/Field palette, Layers, Properties, Select Similar
- Layout: HNL Layout, Rename, Plot, Publish
- Tools: HNL Tools, Manager, Bridge Status, Draft Status

Commands:
- HNLRIBBON
- HNLAI
- HNL2D
- HNLDATA
- HNLLAYOUT
- HNLTOOLS
- HNLMANAGER

The Ribbon is created through runtime reflection against `AdWindows`, reducing hard compile coupling.
If the Ribbon is not ready yet, installation is retried on AutoCAD Idle.
A classic `HNL` menu is attempted as fallback for AutoCAD menu-bar users.

## Unified AI Provider Manager
Providers:
- HNL Offline Rules
- Google Gemini
- ChatGPT / OpenAI
- Claude / Anthropic
- Grok / xAI
- Ollama Local
- Custom OpenAI-compatible

Common settings:
- active provider
- model
- base URL
- API key
- context-only sharing
- preview-before-execute
- automatic offline fallback

## Secret storage
Electron EXE stores provider secrets in one encrypted `safeStorage` blob under userData.
On Windows this uses OS-backed encryption (DPAPI through Electron safeStorage).
Keys are not written to source, project JSON, or DWG.

Environment mapping used by local HNL server:
- GEMINI_API_KEY
- OPENAI_API_KEY
- ANTHROPIC_API_KEY
- XAI_API_KEY
- CUSTOM_OPENAI_API_KEY
plus HNL_AI_<PROVIDER>_MODEL / BASE_URL.

## Unified server routes
- GET /api/ai/status
- POST /api/ai/test
- POST /api/ai/plan

Legacy `/api/gemini/plan` remains as a compatibility alias.

The main CAD AI planner, AutoLISP Builder, translation AI path and SketchUp layer assistant can use the selected provider.

## Provider protocols
- OpenAI: Responses API
- xAI/Grok: Responses API
- Anthropic: Messages API
- Gemini: official @google/genai SDK
- Ollama: /api/chat
- Custom: tries OpenAI Responses first, then Chat Completions fallback

## Safety
If provider is unavailable and auto-fallback is enabled, CAD planning falls back to HNL Offline Rules.
Destructive CAD changes remain plan/preview-first.

## Validation performed in this environment
- TypeScript/TSX syntax transpile
- Electron main/preload node syntax
- XML parse for 4 csproj + PackageContents
- static AI provider route/storage wiring checks
- static HNL Ribbon command/panel wiring checks
- package version / dependency pin checks
- ZIP/RBZ integrity

Important: Autodesk DLL compilation and real Ribbon creation still require GitHub Actions Windows + AutoCAD runtime testing.
