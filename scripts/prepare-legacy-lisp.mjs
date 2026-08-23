import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const sourceDir = path.join(root, "resources", "legacy-lisp");
const archive = path.join(sourceDir, "AI.rar");
const manifestPath = path.join(sourceDir, "legacy-lisp-manifest.json");
const outDir = path.join(sourceDir, "extracted");
const runtimeIndexPath = path.join(outDir, "legacy-lisp-index.json");

function walk(dir, out = []) {
  let entries = [];
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (entry.isFile()) out.push(full);
  }
  return out;
}

function scanCommands(filePath) {
  try {
    const buf = fs.readFileSync(filePath);
    // Lisp command declarations are ASCII even if comments/text use Vietnamese ANSI/UTF-8.
    const text = buf.toString("latin1");
    const found = [];
    const rx = /\(\s*defun\s+c:([A-Za-z0-9_\-$]+)\b/gi;
    let m;
    while ((m = rx.exec(text))) {
      const cmd = String(m[1] || "").toUpperCase();
      if (cmd && !found.includes(cmd)) found.push(cmd);
    }
    return found;
  } catch {
    return [];
  }
}

function executableCandidates() {
  const pf = process.env.ProgramFiles || "C:\\Program Files";
  const pfx86 = process.env["ProgramFiles(x86)"] || "C:\\Program Files (x86)";
  const chocolatey = process.env.ChocolateyInstall;
  return [
    { type: "7z", exe: path.join(pf, "7-Zip", "7z.exe") },
    { type: "7z", exe: path.join(pfx86, "7-Zip", "7z.exe") },
    ...(chocolatey ? [{ type: "7z", exe: path.join(chocolatey, "bin", "7z.exe") }] : []),
    { type: "7z", exe: "7z.exe" },
    { type: "7z", exe: "7zz.exe" },
    { type: "winrar", exe: path.join(pf, "WinRAR", "WinRAR.exe") },
    { type: "winrar", exe: path.join(pfx86, "WinRAR", "WinRAR.exe") },
  ];
}

function testExecutable(candidate) {
  try {
    if (path.isAbsolute(candidate.exe) && !fs.existsSync(candidate.exe)) return false;
    const args = candidate.type === "7z" ? ["i"] : ["?"];
    const r = spawnSync(candidate.exe, args, { windowsHide: true, stdio: "ignore", timeout: 5000 });
    return !r.error;
  } catch {
    return false;
  }
}

function extract(candidate) {
  fs.rmSync(outDir, { recursive: true, force: true });
  fs.mkdirSync(outDir, { recursive: true });

  const args = candidate.type === "7z"
    ? ["x", archive, `-o${outDir}`, "-y"]
    : ["x", "-ibck", "-y", archive, outDir + path.sep];

  const r = spawnSync(candidate.exe, args, {
    windowsHide: true,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
    timeout: 120000,
  });
  if (r.error || r.status !== 0) {
    throw new Error(`Không giải nén được AI.rar bằng ${candidate.exe}.\n${r.stderr || r.stdout || r.error || ""}`);
  }
}

if (!fs.existsSync(archive)) throw new Error(`Missing bundled archive: ${archive}`);
if (!fs.existsSync(manifestPath)) throw new Error(`Missing manifest: ${manifestPath}`);

const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));

function validateAndIndex() {
  const files = walk(outDir);
  const lisps = files.filter(f => path.extname(f).toLowerCase() === ".lsp");
  const arx = files.filter(f => path.extname(f).toLowerCase() === ".arx");

  if (lisps.length !== Number(manifest.expectedLispCount || 44)) {
    throw new Error(`Legacy Lisp extraction count mismatch: ${lisps.length}/44`);
  }
  if (arx.length !== Number(manifest.expectedArxCount || 1)) {
    throw new Error(`Legacy ARX extraction count mismatch: ${arx.length}/1`);
  }

  const byName = new Map(files.map(f => [path.basename(f).toLowerCase(), f]));
  const missing = (manifest.lispFiles || [])
    .map(x => x.name)
    .filter(name => !byName.has(path.basename(name).toLowerCase()));
  if (missing.length) throw new Error(`Missing Lisp after extraction:\n${missing.join("\n")}`);

  const items = lisps.map(filePath => ({
    name: path.basename(filePath),
    relativePath: path.relative(outDir, filePath).split(path.sep).join("/"),
    commands: scanCommands(filePath),
    sizeBytes: fs.statSync(filePath).size,
  })).sort((a,b) => a.name.localeCompare(b.name));

  const index = {
    schemaVersion: 1,
    sourceArchive: manifest.sourceArchive,
    sourceSha256: manifest.sourceSha256,
    generatedAt: new Date().toISOString(),
    lispCount: items.length,
    arxCount: arx.length,
    items,
    legacyArx: arx.map(filePath => ({
      name: path.basename(filePath),
      relativePath: path.relative(outDir, filePath).split(path.sep).join("/"),
      sizeBytes: fs.statSync(filePath).size,
      autoLoad: false,
      compatibility: "LEGACY_AUTOCAD_2021_X64_UNVERIFIED_FOR_2023_2026",
    })),
  };
  fs.writeFileSync(runtimeIndexPath, JSON.stringify(index, null, 2), "utf8");
  console.log(`HNL LEGACY LISP PASS: ${items.length} Lisp + ${arx.length} ARX`);
  console.log(`Runtime index: ${runtimeIndexPath}`);
  return true;
}

// Idempotent: accept an already-extracted verified directory.
try {
  if (fs.existsSync(outDir)) {
    validateAndIndex();
    process.exit(0);
  }
} catch {
  fs.rmSync(outDir, { recursive: true, force: true });
}

if (process.platform !== "win32") {
  console.warn("HNL legacy Lisp prepare skipped: RAR extraction is performed on Windows/GitHub build.");
  console.warn("The source archive + manifest are still included and verified.");
  process.exit(0);
}

const tool = executableCandidates().find(testExecutable);
if (!tool) {
  throw new Error(
    "Không tìm thấy 7-Zip/WinRAR để giải nén resources/legacy-lisp/AI.rar.\n" +
    "Cài 7-Zip hoặc build bằng GitHub Actions windows-latest."
  );
}

console.log(`Extracting HNL legacy Lisp with: ${tool.exe}`);
extract(tool);
validateAndIndex();
