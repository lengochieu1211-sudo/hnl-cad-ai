# HNL Tool - VXT Pro v7.0.0-alpha.1

Staging branch: `research/vxt-pro-v7-bootstrap`

This branch is intentionally isolated from `main` while HNL VXT is migrated from AutoLISP/DCL to C# + WPF + AutoCAD .NET.

## Target
- AutoCAD 2023
- .NET Framework 4.8
- WPF dockable PaletteSet
- HNL vector logo in the header
- Model-space transient preview for XC / XP / Ty / DIM
- DIM position preview before real entity creation

## Golden baseline
`HNL Tool - Vẽ Xương Trần V6.7.4_VXT.lsp` remains the Golden engine. Real drawing in v7 stays locked until parity tests pass.

## Source package prepared in ChatGPT workspace
`HNL-VXT-Pro-v7.0.0-alpha.1-SOURCE.zip`

SHA256: `ec1d55fd6bff549ba9a963528e73ca2d8e833f492f654397cb21799c1d41c0ac`

The prepared source contains Core / UI / AutoCAD / Tests / bundle manifest / GitHub Actions / Golden test documents. It uses the same AutoCAD 2023 CI dependency strategy already used by HNL CAD AI: `Microsoft.NETFramework.ReferenceAssemblies 1.0.3` + `AutoCAD.NET 24.2.0` with runtime assets excluded.

## Guardrails
- Do not merge this branch to `main` yet.
- Do not replace V6.7.4.
- Do not enable real Create until Runtime Golden is complete.
- Final preferred repository name: `hnl-vxt-pro`.
