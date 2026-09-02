import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const version = pkg.version;
const failures = [];
const ok = (condition, message) => { if (!condition) failures.push(message); };
const read = (p) => fs.readFileSync(path.join(root, p), "utf8");

const metadata = JSON.parse(read("metadata.json"));
ok(metadata.version === version, `metadata.json=${metadata.version}`);

const versionTs = read("src/lib/version.ts");
ok(versionTs.includes(`HNL_APP_VERSION = "${version}"`), "src/lib/version.ts does not match package version");

const xml = read("autocad-plugin/HNL.CadBridge.bundle/PackageContents.xml");
const xmlVersions = [...xml.matchAll(/(?:\sAppVersion|\sVersion)="([0-9.]+)"/g)].map(m => m[1]);
ok(xmlVersions.length === 6 && xmlVersions.every(v => v === version), `PackageContents versions=${xmlVersions.join(",")}`);

const bridge = read("autocad-plugin/Hnl.CadBridge/BridgeCommands.cs");
ok(bridge.includes(`PluginVersion = "${version}"`), "BridgeCommands.PluginVersion mismatch");

for (const year of [2023,2024,2025,2026,2027]) {
  const cs = read(`autocad-plugin/Hnl.CadBridge/Hnl.CadBridge.AutoCAD${year}.csproj`);
  ok(cs.includes(`<Version>${version}</Version>`), `AutoCAD ${year} <Version> mismatch`);
  ok(cs.includes(`<AssemblyVersion>${version}.0</AssemblyVersion>`), `AutoCAD ${year} AssemblyVersion mismatch`);
  ok(cs.includes(`<FileVersion>${version}.0</FileVersion>`), `AutoCAD ${year} FileVersion mismatch`);
  ok(cs.includes(`<InformationalVersion>${version}</InformationalVersion>`), `AutoCAD ${year} InformationalVersion mismatch`);
}

const suRoot = read("sketchup-extension/hnl_cad_ai_bridge.rb");
const suMain = read("sketchup-extension/hnl_cad_ai_bridge/main.rb");
ok(suRoot.includes(`EXTENSION.version = '${version}'`), "SketchUp extension manifest version mismatch");
ok(suMain.includes(`VERSION = '${version}'`), "SketchUp main version mismatch");
ok(fs.existsSync(path.join(root, `sketchup-extension/HNL_CAD_AI_Bridge_v${version}.rbz`)), "Versioned SketchUp RBZ missing");

const workflow = read(".github/workflows/build-windows.yml");
ok(workflow.includes("steps.hnl_version.outputs.version"), "GitHub artifact names are not dynamic");
ok(workflow.includes("node scripts/check-version-sync.mjs"), "GitHub version-sync gate missing");

const branding = read("src/lib/branding.ts");
ok(branding.includes("HNL_DISPLAY_VERSION"), "Branding not using canonical version constant");
const app = read("src/App.tsx");
ok(app.includes("version:HNL_APP_VERSION") && app.includes("HNL_DISPLAY_VERSION"), "App version display/project export not canonicalized");
const diagnostics = read("src/lib/diagnostics.ts");
ok(diagnostics.includes("appVersion = HNL_APP_VERSION"), "Diagnostics default version not canonicalized");
const server = read("server.ts");
ok(server.includes("version: HNL_APP_VERSION"), "Server health version not canonicalized");
const exporter = read("src/components/Dialogs/NetPluginExporterModal.tsx");
ok(exporter.includes("${HNL_APP_VERSION}"), "Net Plugin Exporter templates not canonicalized");

const packageContents = xml;
ok(packageContents.includes('SchemaVersion="1.0"'), "PackageContents SchemaVersion must remain 1.0");
ok(pkg.dependencies?.["@google/genai"] === "2.4.0", `@google/genai pin changed to ${pkg.dependencies?.["@google/genai"]}`);
ok(pkg.build?.win?.artifactName === "HNL_CAD_AI_Setup_${version}.${ext}", "electron-builder artifactName must use package version token");

const readme = read("README.md");
ok(readme.startsWith(`# HNL CAD AI v${version}`), "README current heading mismatch");
const buildReadme = read("BUILD_EXE_README.md");
ok(buildReadme.includes(`v${version}`) && buildReadme.includes(`HNL_CAD_AI_Setup_${version}.exe`), "BUILD_EXE_README current version mismatch");
const usageGuide = read("src/components/Dialogs/UsageGuideModal.tsx");
ok(usageGuide.includes(`HNL CAD AI Bridge v${version}.rbz`), "UsageGuide SketchUp RBZ version mismatch");
const suModal = read("src/components/Dialogs/SketchUp2DBridgeModal.tsx");
ok(suModal.includes(`HNL CAD AI Bridge v${version}.rbz`), "SketchUp2DBridge UI RBZ version mismatch");
const bat1 = read("BUILD_EXE_NOW.bat");
const bat2 = read("build-exe.bat");
ok(bat1.includes("%HNL_VERSION%") && bat2.includes("%HNL_VERSION%"), "BAT build scripts still hard-code release version");
const replacePs = read("REPLACE_WHOLE_REPO.ps1");
ok(replacePs.includes("$version") && replacePs.includes("package.json"), "REPLACE_WHOLE_REPO.ps1 is not version-dynamic");
const electronMain = read("electron/main.cjs");
ok(electronMain.includes("app.getVersion()"), "Electron runtime version must come from package/app.getVersion");

if (failures.length) {
  console.error(`HNL VERSION SYNC FAIL (${version})`);
  for (const f of failures) console.error(` - ${f}`);
  process.exit(1);
}
console.log(`HNL VERSION SYNC PASS: ${version}`);
