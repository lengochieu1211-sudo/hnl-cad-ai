# HNL CAD AI v2.5.3 — Ceiling Manufacturer Knowledge Pack

## Scope
Added sourced ceiling knowledge from official manufacturer/company websites:
- Knauf Vietnam
- Vĩnh Tường / Saint-Gobain
- Lê Trần
- I.S Corporation / D&S

## Critical engineering rule
HNL keeps two different classes of information:

1. MANUFACTURER FACT
   - exact published profile / spacing / board / standard
   - source + revision/confidence stored with the data

2. HNL PROJECT RULE
   - user/project-specific logic
   - never presented as a manufacturer requirement

Current HNL project rule:
- concealed ceiling board: 1220 x 2440mm
- cross runner spacing = 1220 / 3 = 406.666...mm
- drywall wall stud = 1220/3 = 406.666...mm OR 1220/4 = 305mm

Manufacturer published 406mm values remain stored as 406mm, not rewritten to 406.67.

## Knauf knowledge added
- Ultra: main max 1200, cross max 406
- Pro: main max 1100, cross max 406
- Xtra: main max 1000, cross max 406
- Pro V / Pro C / V32 verified profile dimensions
- T3 exposed grid: main 1220, cross 610, hanger max 1220, first hanger to wall <=610
- standards/source references stored

## Vĩnh Tường knowledge added
- TIKA GypCeil / GypCeil Aqua brochure: aperture 800 x 406
- TIKA profile and accessory information
- legacy OMEGA table: cross 406, main <=1200, hanger <=1200
- current concealed family reference: ALPHA, BASI Plus, TIKA, EKO Plus
- legacy/current sources are explicitly distinguished

## Lê Trần knowledge added
- MacroTEK system + profile families
- ChannelTEK system + verified profile families
- GypLINE profile information
- ASTM C635 / ISO 9001:2015 references where stated
- public pages did not publish a universal installation spacing, so HNL leaves spacing unset instead of guessing

## I.S / D&S knowledge added
- manufacturer family list: MONO, PARA 1, PARA 3, PARA 5, GAMMA 2, GAMMA 6, SUPER 8, SUPER 10
- verified project usage references for D&S Para 5:
  Becamex, Ho Tram, Nisshin, Mekophar
- public sources used here did not establish installation spacing, so HNL does not invent one

## UI
Drywall & Ceiling Studio adds tab:
`Hãng / Catalog Verified`

Features:
- filter Knauf / Vĩnh Tường / Lê Trần / I.S-D&S
- search system/profile
- show published/partial/not-published status
- show main/cross/hanger values only when source supports them
- source/revision confidence label
- HNL 1220/3 and wall 1220/3-or-4 rule displayed separately

## AI
Both:
- HNL AI Palette
- Multi-provider Drywall/Ceiling Engineering AI

receive manufacturer ceiling context.

AI instruction:
- do not mix spacings across brands/systems/revisions
- do not turn HNL project rules into manufacturer facts
- if manufacturer spacing is absent, request Approved Material/Catalog instead of guessing

## Existing ceiling default corrected
The old concealed-ceiling preset used cross runner = 400mm.
It is now:
`1220 / 3 = 406.666...mm`

The numeric input now supports 0.01mm increments.

## Validation
- TS/TSX transpile syntax check
- Electron syntax
- AutoCAD csproj + PackageContents XML
- knowledge source/schema static checks
- manufacturer tab wiring
- AI context wiring
- version/dependency pin
- ZIP/RBZ integrity

Real AutoCAD runtime still requires Windows GitHub Actions + installed AutoCAD test.
