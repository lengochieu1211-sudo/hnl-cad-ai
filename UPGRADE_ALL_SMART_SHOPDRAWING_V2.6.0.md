# HNL CAD AI v2.6.0 — UPGRADE ALL / SMART SHOPDRAWING PLATFORM

## 1. Smart Shopdrawing Platform
A new unified modal is added with:
- Overview
- Smart Library
- Smart Ceiling
- Smart Wall
- Approved Material
- BOQ
- Audit
- Detail
- Project Template
- DWG Open modes

It is available from the HNL desktop Tools menu and Command Search.

## 2. Correct board-module rules
HNL PROJECT RULE is now:

### Concealed ceiling
- board: 1220 x 2440 mm
- cross runner = 1220 / 3 = 406.666... mm
- do NOT round the HNL project module to 400 mm

### Drywall wall
Allowed HNL project modules:
- 1220 / 3 = 406.666... mm
- 1220 / 2 = 610 mm

The previous HNL project default 1220/4 = 305 mm is removed.

Manufacturer/tested assembly values are not silently rewritten. Project/module rules and manufacturer-approved values remain separate sources.

## 3. Smart Library
Built-in metadata library includes:
- Section mark
- Level mark
- Detail mark
- Ceiling board start point
- Ceiling main runner
- Ceiling cross runner
- Ceiling hanger
- Wall stud
- Wall track
- Door jamb reinforcement
- RHS/SHS steel symbol
- Steel plate

Features:
- AutoCAD insertion through `INSERT_LIBRARY_BLOCK`
- native layers
- HNL XData tagging
- Favorites
- Recent
- custom user DWG library import
- source DWG insertion as a block through Autodesk database engine

Native AutoCAD command:
`HNLINSERT`

Note: built-in symbols are canonical HNL starter geometry. The user's actual company/project DWG blocks can be loaded into Custom Library and used instead.

## 4. Smart Ceiling Generator 2.0
Desktop Smart Ceiling:
- manufacturer system selector
- approved/project source precedence
- board 1220x2440
- cross = 1220/3
- configurable main runner / hanger / elevation / direction
- selected closed Polyline workflow
- HNL native metadata tags

Bridge actions:
- `CREATE_CEILING_GRID`
- `CREATE_CEILING_SMART`

Native command `HNLCEILING` now defaults concealed cross runner to 1220/3 and displays decimal precision rather than rounding the prompt to 407.

## 5. Smart Wall Generator
Smart Wall supports:
- board 1220x2440
- 1220/3 = 406.67 mm
- 1220/2 = 610 mm
- stud profile
- track profile
- height
- start/end stud
- project metadata
- native AutoCAD generation

Bridge action:
`CREATE_WALL_SYSTEM`

Native command:
`HNLWALL`

The AutoCAD command prompts for start/end points and generates track + plan stud markers tagged with HNL XData.

## 6. Approved Material / Submittal Manager
Project data can store:
- category
- manufacturer
- system
- revision
- approval status
- source document path
- technical parameters

Supported status:
DRAFT / SUBMITTED / APPROVED / REJECTED / SUPERSEDED

Approved Ceiling can override main/cross/hanger values.
Approved Wall can select project stud module 1220/3 or 1220/2 and profiles.

Priority:
1. Approved Project Material/Submittal
2. Project Specification
3. Current Manufacturer Catalog
4. Manufacturer Technical Document
5. HNL Project Rule
6. AI Suggestion

## 7. Manufacturer preset integration
The manufacturer KB from v2.5.3 remains active:
- Knauf
- Vĩnh Tường
- Lê Trần
- I.S / D&S

Selecting a manufacturer system loads published main/hanger values when available.
The HNL cross module remains 1220/3 unless an Approved Project source explicitly overrides it.

No system/revision mixing is allowed by AI instructions.

## 8. Smart BOQ
Local HNL smart-object BOQ:
- board m2
- ceiling main runner lm
- ceiling cross runner lm
- hanger pcs
- wall stud lm
- wall track lm

A calculation error in the old ceiling hanger formula was corrected:
area is divided by `(main spacing * hanger spacing)`, not by chained division.

Native AutoCAD HNL entities are tagged with XData and can be scanned through:
- `GET_HNL_BOQ`
- command `HNLBOQ`

## 9. Shopdrawing Audit
Local rules check:
- concealed ceiling cross module
- wall stud module
- invalid zero/negative spacing

Native AutoCAD audit:
- detects HNL Smart/XData objects
- reports non-1 / non-uniform HNL block scale
- reports drawings with no HNL smart metadata

Bridge:
`AUDIT_HNL_SHOPDRAWING`

Native command:
`HNLSHOPAUDIT`

This is the audit core. Advanced geometric checks such as MEP rerouting, board-joint topology and automatic Zoom-to-Issue are not claimed as fully native yet.

## 10. Detail / Section
The platform links to the existing parametric Section Generator and exposes templates:
- ceiling-wall section
- ceiling edge
- wall head
- door jamb

The existing section/detail engine remains the geometry engine.

## 11. Project Templates
Built-in templates:
- Airport
- Hotel
- Condo
- Factory

Templates control HNL project defaults such as main/hanger spacing, wall division preference, layer prefix and Approved Material requirement.

## 12. AI with technical sources
HNL AI context now includes:
- manufacturer ceiling KB
- HNL board-module rules
- project Approved Material records

Structured AI plan schema adds:
- `certainty`
- `sourceRefs`

AI is instructed to:
- prefer Approved Project sources
- never mix manufacturer systems/revisions
- mark missing evidence as UNVERIFIED
- never invent a technical value

## 13. Dual DWG open modes

### A. AutoCAD Native
`Open DWG -> AutoCAD Native`
- full DWG fidelity
- dynamic blocks
- fields
- xrefs
- layouts
- plot
- custom/proxy objects as supported by AutoCAD
- intended for official editing

### B. HNL Canvas
`Open DWG -> HNL Canvas`
- source DWG is read in the AutoCAD database engine in the background
- Bridge creates a temporary DXF
- HNL parses supported 2D entities into its own Canvas
- intended for quick viewing, AI context, audit and lightweight 2D edits
- original DWG path is NOT attached to Ctrl+S, so the source is not overwritten

Bridge:
`CONVERT_DWG_TO_DXF_PREVIEW`

Important limitation:
This is deliberately NOT presented as a full independent DWG decoder.
When AutoCAD Bridge is unavailable, HNL can still open DXF/HNL JSON directly, but full/native DWG fidelity requires Autodesk's engine.

## 14. AutoCAD HNL Ribbon / Menu
Native HNL Ribbon now exposes common shopdrawing actions so commands do not need to be memorized:

Shopdrawing:
- Smart Ceiling
- Smart Wall
- Library
- Polyline

Data / BOQ:
- HNL BOQ
- Data / Field
- Layers
- Properties

Tools:
- Shop Audit
- HNL Tools
- Manager
- Bridge Status

Classic HNL menu fallback also contains Smart Ceiling / Smart Wall / Library / BOQ / Audit.

## 15. Validation performed
- TS/TSX syntax transpile
- Electron main/preload Node syntax
- AutoCAD csproj + PackageContents XML parsing
- live-source check for removed 1220/4 project rule
- Smart Library / Ceiling / Wall / Approved / BOQ / Audit / Template static wiring
- dual DWG open mode wiring
- AI Approved/Manufacturer source context wiring
- HNL Ribbon/Palette command wiring
- @google/genai remains pinned to 2.4.0
- ZIP/RBZ integrity

Not performed locally:
- Autodesk .NET compilation against real AutoCAD assemblies
- runtime AutoCAD 2023/2024/2025/2026 testing
- full Vite production build (node_modules are not present in this runtime)

GitHub Actions Windows remains the AutoCAD compile gate; installed AutoCAD remains the runtime gate.
