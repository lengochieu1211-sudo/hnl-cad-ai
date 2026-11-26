# HNL Tool – Ceiling Framing Pro: AutoCAD 2013–2027 compatibility gate

Updated 2026-11-26. Baseline `89a5caecef01440834d8a1d7eaaa21871c5ed011` (research/vxt-pro-v7-bootstrap).

## Why one executable cannot mean one AutoCAD DLL

The HCF solver/UI is shared in source, but Autodesk's Managed .NET SDK, host runtime and
binary APIs are not identical across AutoCAD versions. A compatible installer must
ship **version-matched, CI-built DLL stages** and a `PackageContents.xml` selector.
An installer must never claim support for a year when its bridge has not been built
and checked for that host.

Autodesk reference:
https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm

| AutoCAD | Release | Autodesk host runtime | HCF status at baseline |
|---|---|---|---|
| 2013 | R19.0 | .NET Framework 4.0 | Not built: requires legacy Core/UI port + SDK |
| 2014 | R19.1 | .NET Framework 4.0 | Not built: requires legacy Core/UI port + SDK |
| 2015 | R20.0 | .NET Framework 4.5 | Not built: requires version-matched bridge + Core/UI port |
| 2016 | R20.1 | .NET Framework 4.5 | Not built: requires version-matched bridge + Core/UI port |
| 2017 | R21.0 | .NET Framework 4.6 | Not built: requires version-matched bridge + Core/UI port |
| 2018 | R22.0 | .NET Framework 4.6 | Not built: requires version-matched bridge + Core/UI port |
| 2019 | R23.0 | .NET Framework 4.7 | Not built: requires 4.7-compatible Core/UI port |
| 2020 | R23.1 | .NET Framework 4.7 | Not built: requires 4.7-compatible Core/UI port |
| 2021 | R24.0 | .NET Framework 4.8 | Isolated API build probe added; not installed until CI passes |
| 2022 | R24.1 | .NET Framework 4.8 | Isolated API build probe added; not installed until CI passes |
| 2023 | R24.2 | .NET Framework 4.8 | Existing CI PASS / Runtime Candidate |
| 2024 | R24.3 | .NET Framework 4.8 | Existing CI PASS / Runtime Candidate |
| 2025 | R25.0 | .NET 8 | Existing CI PASS / Runtime Candidate |
| 2026 | R25.1 | .NET 8 or .NET 10 depending update | Existing CI PASS / Runtime Candidate |
| 2027 | R26.0 | .NET 10 | Existing CI PASS / Runtime Candidate |

The 2021/2022 trial bridges compile the original HCF AutoCAD source via year-specific
Autodesk API packages, with unchanged net48 HCF Core and WPF UI dependencies.
Their build alone does **not** prove support on actual AutoCAD.

## Mandatory implementation gates

1. Confirm exact HEAD and protect already-certified 2023–2027 outputs.
2. For each legacy band, create isolated version-matched projects. Never copy the
   2023 DLL into a folder marked 2013–2022.
3. For 2013–2020, port the shared Core and WPF UI to a host-compatible TFM in a
   separate build branch or compatibility project without downgrading the current
   2023–2027 assemblies. Validate any changed API, not just compilation.
4. Build / regression / verify no Autodesk runtime DLL is redistributed;
   verify command-lazy-load manifests, OS prerequisites, and version routing.
5. Only then update the Universal installer stage list and the registry detection
   summary for that CAD version. Never announce a year with no binary stage.
6. Runtime in each actual AutoCAD year: HCF startup, UI, SPLINE/MLINE/CIRCLE,
   nested holes, XC/XP/Ty DIM, MEP/ShiftAll toggles, Preview/Create parity,
   Cancel/Undo, drawing switch; compare geometry and counts to Golden.
7. Mark a year Runtime Golden only with recorded screenshots, logs and fixtures.

Do not merge main, change XC/XP/Ty or MEP algorithms, or claim a 2013–2027
installer before all 15 years pass the above gates.
