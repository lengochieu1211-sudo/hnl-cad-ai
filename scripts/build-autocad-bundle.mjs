import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const version = String(pkg.version || "").trim();
const bridgeDir = path.join(root, "autocad-plugin", "Hnl.CadBridge");
const bundleDir = path.join(root, "autocad-plugin", "HNL.CadBridge.bundle");
const lispSourceDir = path.join(root, "resources", "legacy-lisp", "extracted");
const lispBundleDir = path.join(bundleDir, "Contents", "Lisp");

const projects = [
  { year: "2023", project: path.join(bridgeDir, "Hnl.CadBridge.AutoCAD2023.csproj") },
  { year: "2024", project: path.join(bridgeDir, "Hnl.CadBridge.AutoCAD2024.csproj") },
  { year: "2025", project: path.join(bridgeDir, "Hnl.CadBridge.AutoCAD2025.csproj") },
  { year: "2026", project: path.join(bridgeDir, "Hnl.CadBridge.AutoCAD2026.csproj") },
  { year: "2027", project: path.join(bridgeDir, "Hnl.CadBridge.AutoCAD2027.csproj") },
];

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: root,
    windowsHide: true,
    encoding: "utf8",
    stdio: options.capture ? ["ignore", "pipe", "pipe"] : "inherit",
    timeout: options.timeout || 120000,
  });

  if (options.capture) {
    if (result.stdout) process.stdout.write(result.stdout);
    if (result.stderr) process.stderr.write(result.stderr);
  }

  if (result.error || result.status !== 0) {
    const detail = result.error ? `: ${result.error.message}` : "";
    throw new Error(`${command} ${args.join(" ")} failed${detail}`);
  }

  return result;
}

function ensureDotnetSdk() {
  const result = spawnSync("dotnet", ["--list-sdks"], {
    cwd: root,
    windowsHide: true,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
    timeout: 10000,
  });

  const sdks = String(result.stdout || "").trim();
  if (result.error || result.status !== 0 || !sdks) {
    throw new Error(
      "Missing .NET SDK. Install .NET SDK 8.x and 10.x before building the AutoCAD bundle, " +
      "or use the GitHub Actions Windows build."
    );
  }

  const hasSdk8 = /^8\./m.test(sdks);
  const hasSdk10 = /^10\./m.test(sdks);
  if (!hasSdk8 || !hasSdk10) {
    throw new Error(
      `Missing required .NET SDK major version(s). Need 8.x and 10.x; found:\n${sdks}`
    );
  }

  console.log(`.NET SDK:\n${sdks}`);
}

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

function removeAutoCadRuntimeCopies(outDir) {
  const runtimeRef = /^(Ac|AutoCAD).*\.dll$/i;
  for (const file of walk(outDir)) {
    if (runtimeRef.test(path.basename(file))) fs.rmSync(file, { force: true });
  }
}

function getWindowsFileVersion(dllPath) {
  if (process.platform !== "win32") return "";
  const ps = [
    "-NoProfile",
    "-Command",
    `[System.Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path '${dllPath.replaceAll("'", "''")}')).FileVersion`,
  ];
  const result = spawnSync("powershell.exe", ps, {
    cwd: root,
    windowsHide: true,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
    timeout: 10000,
  });
  if (result.error || result.status !== 0) return "";
  return String(result.stdout || "").trim();
}

function copyBundledLisp() {
  fs.rmSync(lispBundleDir, { recursive: true, force: true });
  fs.mkdirSync(lispBundleDir, { recursive: true });

  const lispFiles = walk(lispSourceDir)
    .filter((file) => path.extname(file).toLowerCase() === ".lsp")
    .sort((a, b) => path.basename(a).localeCompare(path.basename(b)));

  for (const file of lispFiles) {
    fs.copyFileSync(file, path.join(lispBundleDir, path.basename(file)));
  }

  const bundled = walk(lispBundleDir).filter((file) => path.extname(file).toLowerCase() === ".lsp");
  if (bundled.length !== 44) {
    throw new Error(`AutoCAD bundle must contain exactly 44 Lisp files, found ${bundled.length}.`);
  }
  if (walk(lispBundleDir).some((file) => path.extname(file).toLowerCase() === ".arx")) {
    throw new Error("Legacy ARX must not be included in AutoCAD 2023-2027 plugin bundle.");
  }

  console.log(`AutoCAD bundle Lisp On-Demand pack PASS: ${bundled.length}/44`);
}

if (!version) throw new Error("package.json version is empty.");
if (!fs.existsSync(path.join(bundleDir, "PackageContents.xml"))) {
  throw new Error("Missing AutoCAD PackageContents.xml.");
}

ensureDotnetSdk();
run(process.execPath, [path.join("scripts", "prepare-legacy-lisp.mjs")], { timeout: 120000 });

const failures = [];
for (const item of projects) {
  const outDir = path.join(bundleDir, "Contents", item.year);
  fs.rmSync(outDir, { recursive: true, force: true });
  fs.mkdirSync(outDir, { recursive: true });

  console.log(`===== AutoCAD ${item.year} =====`);
  try {
    run("dotnet", ["restore", item.project], { timeout: 120000 });

    const build = spawnSync("dotnet", ["build", item.project, "-c", "Release", "--no-restore", "-o", outDir], {
      cwd: root,
      windowsHide: true,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
      timeout: 120000,
    });

    const buildOutput = `${build.stdout || ""}${build.stderr || ""}`;
    if (build.stdout) process.stdout.write(build.stdout);
    if (build.stderr) process.stderr.write(build.stderr);

    if (build.error || build.status !== 0) {
      failures.push(`${item.year}:build`);
      continue;
    }
    if (/\bCS8604\b/.test(buildOutput)) {
      failures.push(`${item.year}:CS8604`);
      continue;
    }

    removeAutoCadRuntimeCopies(outDir);

    const dll = path.join(outDir, "Hnl.CadBridge.dll");
    if (!fs.existsSync(dll)) {
      failures.push(`${item.year}:missing-dll`);
      continue;
    }

    const fileVersion = getWindowsFileVersion(dll);
    if (fileVersion && !fileVersion.startsWith(version)) {
      failures.push(`${item.year}:dll-version=${fileVersion} expected=${version}`);
      continue;
    }

    console.log(`AutoCAD ${item.year} plugin PASS: ${fs.statSync(dll).size} bytes${fileVersion ? ` | FileVersion ${fileVersion}` : ""}`);
  } catch (error) {
    failures.push(`${item.year}:${String(error?.message || error)}`);
  }
}

if (failures.length) {
  throw new Error(`AutoCAD plugin failures: ${failures.join(", ")}`);
}

copyBundledLisp();

console.log("AutoCAD bundle contents:");
for (const file of walk(bundleDir).sort()) {
  const rel = path.relative(bundleDir, file).split(path.sep).join("/");
  console.log(` - ${rel} (${fs.statSync(file).size} bytes)`);
}
