import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const catalogPath = path.join(root, "src", "lib", "lispFeatureCatalog.ts");
const manifestPath = path.join(root, "resources", "legacy-lisp", "legacy-lisp-manifest.json");

const src = fs.readFileSync(catalogPath, "utf8");
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));

const sourceFileRx = /"sourceFile":"([^"]+)"/g;
const groupRx = /"sourceGroup":"([^"]+)"/g;

const sourceFiles = [];
let m;
while ((m = sourceFileRx.exec(src))) sourceFiles.push(m[1]);

const groups = [];
while ((m = groupRx.exec(src))) groups.push(m[1]);

if (sourceFiles.length !== 44) throw new Error(`Catalog sourceFile count ${sourceFiles.length}/44`);
if (new Set(sourceFiles.map(x=>x.toLowerCase())).size !== 44) throw new Error("Duplicate sourceFile in Lisp catalog");

const manifestNames = (manifest.lispFiles || []).map(x => path.basename(x.name).toLowerCase());
const missing = sourceFiles.filter(x => !manifestNames.includes(path.basename(x).toLowerCase()));
if (missing.length) throw new Error(`Catalog files not present in AI.rar manifest:\n${missing.join("\n")}`);

const expected = {
  TEXT:5,
  BLOCK:7,
  FIELD:4,
  GEOMETRY:6,
  DIMENSION:3,
  LAYER:2,
  QUANTITY:2,
  SHOPDRAWING:3,
  LAYOUT:11,
  TOOLS:1,
};
for (const [g,n] of Object.entries(expected)) {
  const actual = groups.filter(x=>x===g).length;
  if (actual !== n) throw new Error(`Group ${g}: ${actual}/${n}`);
}
if (groups.length !== 44) throw new Error(`Group count ${groups.length}/44`);

console.log("HNL LISP TAXONOMY PASS: 44/44");
for (const [g,n] of Object.entries(expected)) console.log(`${g}: ${n}`);
