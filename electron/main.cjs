// Electron Main Process for HNL CAD AI TOOL Standalone Desktop (.EXE)
const { app, BrowserWindow, dialog, ipcMain, Menu, shell, safeStorage } = require('electron');
const crypto = require('crypto');
const path = require('path');
const fs = require('fs');
const net = require('net');
const { spawn, execFileSync } = require('child_process');

let mainWindow = null;
let isRendererDirty = false;
let forceQuit = false;
const SUPPORTED_AUTOCAD_YEARS = ['2027','2026','2025','2024','2023'];
process.env.HNL_API_TOKEN = process.env.HNL_API_TOKEN || crypto.randomBytes(32).toString('hex');

function getHnlToolArg(argv = []) {
  const item = (argv || []).find((x) => /^--hnl-tool=/i.test(String(x || '')));
  return item ? String(item).split('=', 2)[1].trim().toUpperCase() : '';
}

function dispatchHnlToolArg(argv = process.argv) {
  if (!mainWindow) return;
  const tool = getHnlToolArg(argv);
  if (!tool) return;
  const allowed = new Set(['TEXT','BLOCK','FIELD','GEOMETRY','DIMENSION','LAYER','QUANTITY','SHOPDRAWING','LAYOUT','TOOLS','SOURCES','LIBRARY']);
  if (!allowed.has(tool)) return;
  mainWindow.webContents.send('menu-command', tool === 'LIBRARY' ? 'OPEN_SMART_LIBRARY' : `OPEN_2D_PRO_${tool}`);
}

// Prevent two app instances from competing for the same local API port.
const gotSingleInstanceLock = app.requestSingleInstanceLock();
if (!gotSingleInstanceLock) {
  app.quit();
} else {
  app.on('second-instance', (_event, argv) => {
    if (!mainWindow) return;
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.show();
    mainWindow.focus();
    dispatchHnlToolArg(argv);
  });
}

function findAutoCadExe() {
  if (process.platform !== 'win32') return null;
  const roots = [...new Set([
    process.env.ProgramW6432,
    process.env.ProgramFiles,
    'C:\\Program Files',
  ].filter(Boolean))];
  for (const year of SUPPORTED_AUTOCAD_YEARS) {
    for (const root of roots) {
      const candidate = path.join(root, 'Autodesk', `AutoCAD ${year}`, 'acad.exe');
      if (fs.existsSync(candidate)) return { exePath: candidate, year };
    }
  }
  return null;
}

async function launchAutoCadWithDwg(filePath) {
  try {
    const target = String(filePath || '');
    if (!target || !fs.existsSync(target) || path.extname(target).toLowerCase() !== '.dwg') {
      return { success: false, reason: 'DWG_NOT_FOUND', filePath: target };
    }
    const found = findAutoCadExe();
    if (found) {
      const child = spawn(found.exePath, [target], { detached: true, stdio: 'ignore', windowsHide: false });
      child.unref();
      return { success: true, method: 'acad.exe', ...found, filePath: target };
    }
    // Fallback to the Windows DWG file association. This still uses the installed
    // Autodesk application if the machine has a valid association.
    const errorText = await shell.openPath(target);
    if (!errorText) return { success: true, method: 'shell-association', filePath: target };
    return { success: false, reason: 'AUTOCAD_NOT_FOUND', error: errorText, filePath: target };
  } catch (error) {
    return { success: false, reason: 'AUTOCAD_LAUNCH_FAILED', error: String(error?.message || error), filePath };
  }
}

function getAutoCadBundlePaths() {
  const source = path.join(process.resourcesPath || '', 'autocad', 'HNL.CadBridge.bundle');
  const target = path.join(app.getPath('appData'), 'Autodesk', 'ApplicationPlugins', 'HNL.CadBridge.bundle');
  return { source, target };
}

function readBundleVersion(bundlePath) {
  try {
    const xml = fs.readFileSync(path.join(bundlePath, 'PackageContents.xml'), 'utf8');
    return xml.match(/\bAppVersion="([^"]+)"/i)?.[1] || null;
  } catch {
    return null;
  }
}

function isAutoCadRunning() {
  if (process.platform !== 'win32') return false;
  try {
    const out = execFileSync('tasklist.exe', ['/FI', 'IMAGENAME eq acad.exe', '/NH'], {
      encoding: 'utf8',
      windowsHide: true,
      timeout: 3000,
    });
    return /\bacad\.exe\b/i.test(out);
  } catch {
    return false;
  }
}

function findLegacyHnlBundles() {
  const roots = [
    path.join(app.getPath('appData'), 'Autodesk', 'ApplicationPlugins'),
    process.env.ProgramData ? path.join(process.env.ProgramData, 'Autodesk', 'ApplicationPlugins') : null,
  ].filter(Boolean);
  const canonical = path.resolve(getAutoCadBundlePaths().target).toLowerCase();
  const hits = [];
  for (const root of roots) {
    let dirs = [];
    try { dirs = fs.readdirSync(root, { withFileTypes: true }); } catch { continue; }
    for (const ent of dirs) {
      if (!ent.isDirectory()) continue;
      const candidate = path.join(root, ent.name);
      const pc = path.join(candidate, 'PackageContents.xml');
      if (!fs.existsSync(pc)) continue;
      let xml = '';
      try { xml = fs.readFileSync(pc, 'utf8'); } catch { continue; }
      if (!/HNL\s+CAD\s+AI|HNL\.CadBridge|HNL CAD AI Bridge/i.test(xml + ' ' + ent.name)) continue;
      const resolved = path.resolve(candidate).toLowerCase();
      if (resolved === canonical) continue;
      hits.push({ path: candidate, version: readBundleVersion(candidate), writableRoot: resolved.startsWith(path.resolve(app.getPath('appData')).toLowerCase()) });
    }
  }
  return hits;
}

function removeWritableLegacyHnlBundles() {
  const removed = [];
  const blocked = [];
  for (const item of findLegacyHnlBundles()) {
    if (!item.writableRoot) { blocked.push(item); continue; }
    try {
      fs.rmSync(item.path, { recursive: true, force: true });
      removed.push(item);
    } catch (error) {
      blocked.push({ ...item, error: String(error?.message || error) });
    }
  }
  return { removed, blocked };
}

function bundleHasDll(bundlePath) {
  try {
    return SUPPORTED_AUTOCAD_YEARS.every((year) =>
      fs.existsSync(path.join(bundlePath, 'Contents', year, 'Hnl.CadBridge.dll'))
    );
  } catch (_) {}
  return false;
}

function installOrRepairAutoCadBundle() {
  try {
    const { source, target } = getAutoCadBundlePaths();
    const packagedVersion = readBundleVersion(source);
    if (!fs.existsSync(path.join(source, 'PackageContents.xml')) || !bundleHasDll(source)) {
      return { success: false, reason: 'PLUGIN_BUNDLE_NOT_PACKAGED', source, target, packagedVersion };
    }

    const autoCadWasRunning = isAutoCadRunning();
    const legacy = removeWritableLegacyHnlBundles();

    fs.mkdirSync(path.dirname(target), { recursive: true });
    if (fs.existsSync(target)) fs.rmSync(target, { recursive: true, force: true });
    fs.cpSync(source, target, { recursive: true });

    const installedVersion = readBundleVersion(target);
    const versionMatch = Boolean(packagedVersion && installedVersion === packagedVersion);
    const restartRequired = autoCadWasRunning;

    try {
      const markerDir = path.join(app.getPath('appData'), 'HNL CAD AI');
      fs.mkdirSync(markerDir, { recursive: true });
      fs.writeFileSync(path.join(markerDir, 'autocad-plugin-state.json'), JSON.stringify({
        updatedAt: new Date().toISOString(),
        packagedVersion,
        installedVersion,
        target,
        restartRequired,
        legacyRemoved: legacy.removed,
        legacyBlocked: legacy.blocked,
      }, null, 2), 'utf8');
    } catch {}

    return {
      success: versionMatch && bundleHasDll(target),
      source,
      target,
      installedDll: bundleHasDll(target),
      packagedVersion,
      installedVersion,
      versionMatch,
      restartRequired,
      autoCadWasRunning,
      legacyRemoved: legacy.removed,
      legacyBlocked: legacy.blocked,
    };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
}

function getAutoCadBundleStatus() {
  try {
    const { source, target } = getAutoCadBundlePaths();
    const packagedVersion = readBundleVersion(source);
    const installedVersion = readBundleVersion(target);
    return {
      source,
      target,
      packaged: fs.existsSync(path.join(source, 'PackageContents.xml')) && bundleHasDll(source),
      installed: fs.existsSync(path.join(target, 'PackageContents.xml')) && bundleHasDll(target),
      packagedVersion,
      installedVersion,
      versionMatch: Boolean(packagedVersion && installedVersion === packagedVersion),
      autoCadRunning: isAutoCadRunning(),
      legacyBundles: findLegacyHnlBundles(),
    };
  } catch (error) {
    return { packaged: false, installed: false, error: String(error?.message || error) };
  }
}


function canBindLocalPort(port) {
  return new Promise((resolve) => {
    const probe = net.createServer();
    probe.unref();
    probe.once('error', () => resolve(false));
    probe.listen({ host: '127.0.0.1', port, exclusive: true }, () => {
      probe.close(() => resolve(true));
    });
  });
}

async function findAvailableHnlPort(preferred = 32145, attempts = 40) {
  const start = Number.isFinite(Number(preferred)) ? Math.max(1024, Number(preferred)) : 32145;
  for (let offset = 0; offset < Math.max(1, attempts); offset++) {
    const port = start + offset;
    if (port > 65535) break;
    if (await canBindLocalPort(port)) return port;
  }

  return await new Promise((resolve, reject) => {
    const probe = net.createServer();
    probe.unref();
    probe.once('error', reject);
    probe.listen({ host: '127.0.0.1', port: 0, exclusive: true }, () => {
      const address = probe.address();
      const port = typeof address === 'object' && address ? address.port : 0;
      probe.close(() => port ? resolve(port) : reject(new Error('Không tìm được cổng localhost trống cho HNL.')));
    });
  });
}

async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1440,
    height: 920,
    minWidth: 1024,
    minHeight: 700,
    title: 'HNL CAD AI TOOL - Desktop Standalone Professional Edition',
    backgroundColor: '#121212',
    icon: path.join(__dirname, 'icon.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      nodeIntegration: false,
      contextIsolation: true,
      enableRemoteModule: false,
      sandbox: true,
      webSecurity: true,
    },
    frame: true,
    show: false,
  });

  // Development loads the local dev URL. Packaged builds run the bundled
  // Express API locally so AI/offline fallback endpoints keep working.
  const isDev = !app.isPackaged;
  if (isDev && process.env.ELECTRON_START_URL) {
    mainWindow.loadURL(process.env.ELECTRON_START_URL);
  } else {
    process.env.NODE_ENV = 'production';

    const preferredPort = Number(process.env.HNL_PORT || '32145');
    const resolvedPort = await findAvailableHnlPort(preferredPort, 40);
    const portAdjusted = resolvedPort !== preferredPort;
    process.env.HNL_PORT = String(resolvedPort);

    try {
      const bridgeDir = path.join(app.getPath('temp'), 'HNL_CAD_AI');
      fs.mkdirSync(bridgeDir, { recursive: true });
      fs.writeFileSync(path.join(bridgeDir, 'bridge.json'), JSON.stringify({
        host: '127.0.0.1',
        port: resolvedPort,
        preferredPort,
        portAdjusted,
        token: process.env.HNL_API_TOKEN,
        pid: process.pid,
        version: app.getVersion(),
        updatedAt: new Date().toISOString()
      }, null, 2), 'utf-8');
    } catch (_) {}

    if (portAdjusted) {
      console.warn(`[HNL] Port ${preferredPort} đang bận. Tự chuyển sang 127.0.0.1:${resolvedPort}.`);
    }
    try {
      const appRoot = app.getAppPath();
      // Packaged Electron returns resources/app.asar here. app.asar is a file-backed
      // virtual archive, not a real directory, so process.chdir(appRoot) fails on
      // Windows. Pass the ASAR root to the bundled server explicitly instead.
      process.env.HNL_APP_ROOT = appRoot;
      try { loadAiProviderStateIntoEnv(); } catch (_) {}
      require(path.join(appRoot, 'dist', 'server.cjs'));
      const appUrl = `http://127.0.0.1:${process.env.HNL_PORT}`;
      const loadApp = async () => {
        for (let i = 0; i < 50; i++) {
          try {
            const response = await fetch(`${appUrl}/api/health`);
            if (response.ok) {
              await mainWindow.loadURL(appUrl);
              return;
            }
          } catch (_) {}
          await new Promise(resolve => setTimeout(resolve, 100));
        }
        throw new Error('Không khởi động được dịch vụ HNL CAD AI nội bộ.');
      };
      loadApp().catch(err => {
        dialog.showErrorBox('HNL CAD AI', err.message);
        app.quit();
      });
    } catch (err) {
      dialog.showErrorBox('HNL CAD AI', `Không thể khởi động ứng dụng: ${err.message}`);
      app.quit();
    }
  }

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
    mainWindow.maximize();
  });

  mainWindow.on('close', (event) => {
    if (!forceQuit && isRendererDirty) {
      const choice = dialog.showMessageBoxSync(mainWindow, {
        type: 'warning', title: 'HNL CAD AI - Thay đổi chưa lưu',
        message: 'Bản vẽ hiện có thay đổi chưa lưu.',
        detail: 'Hãy chọn Hủy để quay lại và lưu bằng Ctrl+S, hoặc Thoát không lưu.',
        buttons: ['Hủy', 'Thoát không lưu'], defaultId: 0, cancelId: 0, noLink: true,
      });
      if (choice === 0) { event.preventDefault(); return; }
      forceQuit = true;
    }
  });
  mainWindow.webContents.once('did-finish-load', () => dispatchHnlToolArg(process.argv));
  mainWindow.on('closed', () => { mainWindow = null; });

  buildAppMenu();
}

async function openProjectFileDialog(openMode = 'AUTO') {
  if (!mainWindow) return { success: false };
  const normalizedMode = String(openMode || 'AUTO').toUpperCase();
  const localOnly = normalizedMode === 'HNL_LOCAL';
  const result = await dialog.showOpenDialog(mainWindow, {
    title: localOnly ? 'Mở DXF / Dự án HNL' : 'Mở tệp HNL CAD AI',
    filters: localOnly ? [
      { name: 'HNL / DXF', extensions: ['dxf', 'json'] },
      { name: 'AutoLISP Scripts', extensions: ['lsp'] },
    ] : [
      { name: 'AutoCAD DWG', extensions: ['dwg'] },
      { name: 'DXF / HNL Project', extensions: ['dxf', 'json'] },
      { name: 'AutoLISP Scripts', extensions: ['lsp'] },
      { name: 'All Files', extensions: ['*'] },
    ],
    properties: ['openFile'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false };
  const filePath = result.filePaths[0];
  const ext = path.extname(filePath).toLowerCase();
  if (ext === '.dwg') {
    const mode = ['AUTO','AUTOCAD_NATIVE','HNL_CANVAS','DIRECT_DWG'].includes(String(openMode || '').toUpperCase())
      ? String(openMode).toUpperCase()
      : 'AUTO';
    mainWindow.webContents.send('file-opened', {
      filePath,
      fileName: path.basename(filePath),
      extension: 'dwg',
      openMode: mode,
      requiresAutoCad: true,
    });
    return { success: true, filePath, openMode: mode, requiresAutoCad: true };
  }
  if (!['.dxf', '.json', '.lsp'].includes(ext)) {
    await dialog.showMessageBox(mainWindow, { type: 'warning', title: 'Định dạng chưa hỗ trợ', message: 'Chọn DWG/DXF/JSON/LSP.', detail: 'DWG được chuyển cho AutoCAD Bridge; DXF/JSON/LSP có thể xử lý trong HNL.' });
    return { success: false };
  }
  const content = fs.readFileSync(filePath, 'utf-8');
  mainWindow.webContents.send('file-opened', { filePath, content, fileName: path.basename(filePath), extension: ext.slice(1), requiresAutoCad: false });
  return { success: true, filePath };
}

function buildAppMenu() {
  const template = [
    {
      label: 'Tập tin (File)',
      submenu: [
        {
          label: 'Bản vẽ mới (New CAD Drawing)',
          accelerator: 'CmdOrCtrl+N',
          click: () => {
            mainWindow?.webContents.send('menu-command', 'NEW_DRAWING');
          },
        },
        {
          label: 'Mở DWG bằng AutoCAD + HNL...',
          accelerator: 'CmdOrCtrl+O',
          click: () => { openProjectFileDialog('AUTOCAD_NATIVE'); },
        },
        {
          label: 'Mở DXF / Dự án HNL...',
          click: () => { openProjectFileDialog('HNL_LOCAL'); },
        },
        {
          label: 'Tùy chọn DWG nâng cao',
          submenu: [
            {
              label: 'Điều khiển DWG từ HNL (cần AutoCAD Bridge)...',
              click: () => { openProjectFileDialog('DIRECT_DWG'); },
            },
            {
              label: 'Xem DWG nhanh trên HNL Canvas (DXF tạm)...',
              click: () => { openProjectFileDialog('HNL_CANVAS'); },
            },
          ],
        },
        {
          label: 'Lưu bản vẽ hiện tại (Save)',
          accelerator: 'CmdOrCtrl+S',
          click: () => mainWindow?.webContents.send('menu-command', 'SAVE_DRAWING'),
        },
        {
          label: 'Lưu thành... (Save As)',
          accelerator: 'CmdOrCtrl+Shift+S',
          click: () => mainWindow?.webContents.send('menu-command', 'SAVE_AS_DRAWING'),
        },
        {
          label: 'Backup Project HNL JSON...',
          click: () => mainWindow?.webContents.send('menu-command', 'SAVE_PROJECT_JSON'),
        },
        { type: 'separator' },
        {
          label: 'Xuất tệp DXF (Export DXF)...',
          click: () => mainWindow?.webContents.send('menu-command', 'EXPORT_DXF'),
        },
        {
          label: 'In / Xuất PDF (Print/Plot)...',
          accelerator: 'CmdOrCtrl+P',
          click: () => {
            mainWindow?.webContents.send('menu-command', 'PRINT_PDF');
          },
        },
        { type: 'separator' },
        {
          label: 'Thoát ứng dụng (Exit)',
          accelerator: 'CmdOrCtrl+Q',
          click: () => { mainWindow?.close(); },
        },
      ],
    },
    {
      label: 'Chỉnh sửa (Edit)',
      submenu: [
        { label: 'Hoàn tác (Undo)', accelerator: 'CmdOrCtrl+Z', click: () => mainWindow?.webContents.send('menu-command', 'UNDO') },
        { label: 'Làm lại (Redo)', accelerator: 'CmdOrCtrl+Y', click: () => mainWindow?.webContents.send('menu-command', 'REDO') },
        { type: 'separator' },
        { label: 'Cắt (Cut)', accelerator: 'CmdOrCtrl+X', click: () => mainWindow?.webContents.send('menu-command', 'CUT') },
        { label: 'Sao chép (Copy)', accelerator: 'CmdOrCtrl+C', click: () => mainWindow?.webContents.send('menu-command', 'COPY') },
        { label: 'Dán (Paste)', accelerator: 'CmdOrCtrl+V', click: () => mainWindow?.webContents.send('menu-command', 'PASTE') },
        { type: 'separator' },
        { label: 'Chọn tất cả (Select All)', accelerator: 'CmdOrCtrl+A', click: () => mainWindow?.webContents.send('menu-command', 'SELECT_ALL') },
        { label: 'Xóa đối tượng (Delete)', accelerator: 'Delete', click: () => mainWindow?.webContents.send('menu-command', 'DELETE') },
      ],
    },
    {
      label: 'Công cụ AI (AI Tools)',
      submenu: [
        {
          label: 'AI Auto Detail & Layout Composer',
          accelerator: 'CmdOrCtrl+Shift+D',
          click: () => mainWindow?.webContents.send('menu-command', 'OPEN_AUTO_DETAIL'),
        },
        {
          label: 'Trợ lý AI Palette',
          accelerator: 'CmdOrCtrl+Shift+A',
          click: () => mainWindow?.webContents.send('menu-command', 'TOGGLE_AI_PALETTE'),
        },
        {
          label: 'HNL Pile Studio',
          click: () => mainWindow?.webContents.send('menu-command', 'OPEN_PILE_STUDIO'),
        },
        {
          label: 'Kiểm tra lỗi CAD (Audit Engine)',
          click: () => mainWindow?.webContents.send('menu-command', 'OPEN_AUDIT'),
        },
        {
          label: 'Dịch thuật ghi chú bản vẽ (Bilingual)',
          click: () => mainWindow?.webContents.send('menu-command', 'OPEN_TRANSLATE'),
        },
      ],
    },
    {
      label: 'Giao diện (View)',
      submenu: [
        { label: 'Tải lại (Reload)', accelerator: 'CmdOrCtrl+R', click: () => mainWindow?.reload() },
        { label: 'Toàn màn hình (Fullscreen)', accelerator: 'F11', click: () => mainWindow?.setFullScreen(!mainWindow.isFullScreen()) },
      ],
    },
    {
      label: 'AutoCAD',
      submenu: [
        {
          label: 'Cài / Sửa HNL AutoCAD Plugin',
          click: async () => {
            const result = installOrRepairAutoCadBundle();
            await dialog.showMessageBox(mainWindow, {
              type: result.success ? 'info' : 'warning',
              title: 'HNL AutoCAD Plugin',
              message: result.success ? 'Đã cài/sửa HNL.CadBridge.bundle.' : 'Chưa cài được HNL.CadBridge.bundle.',
              detail: result.success
                ? `${result.target}\nĐóng/mở lại AutoCAD để AutoLoader nạp plugin.`
                : `${result.error || result.reason || 'Không rõ lỗi'}\n${result.source || ''}`
            });
          },
        },
        {
          label: 'Kiểm tra trạng thái Plugin',
          click: async () => {
            const status = getAutoCadBundleStatus();
            await dialog.showMessageBox(mainWindow, {
              type: status.installed ? 'info' : 'warning',
              title: 'AutoCAD Plugin Status',
              message: status.installed ? 'Plugin đã được cài.' : 'Plugin chưa được cài hoặc thiếu DLL.',
              detail: `Packaged: ${Boolean(status.packaged)}\nInstalled: ${Boolean(status.installed)}\n${status.target || ''}`
            });
          },
        },
        {
          label: 'Mở thư mục Autodesk ApplicationPlugins',
          click: async () => {
            const { target } = getAutoCadBundlePaths();
            fs.mkdirSync(path.dirname(target), { recursive: true });
            await shell.openPath(path.dirname(target));
          },
        },
      ],
    },
    {
      label: 'Trợ giúp (Help)',
      submenu: [
        {
          label: 'Tài liệu hướng dẫn HNL CAD AI',
          click: () => dialog.showMessageBox(mainWindow, { type: 'info', title: 'Hướng dẫn HNL CAD AI', message: 'HNL CAD AI', detail: 'Sử dụng các nhóm công cụ trên Ribbon/Palette. Chế độ độc lập hoạt động không cần AutoCAD; chức năng tích hợp AutoCAD yêu cầu cài plugin tương ứng.' }),
        },
        {
          label: 'Giới thiệu HNL CAD AI (About)',
          click: () => {
            dialog.showMessageBox(mainWindow, {
              type: 'info',
              title: 'Về HNL CAD AI TOOL',
              message: 'HNL CAD AI TOOL - Phiên bản Desktop Độc Lập (.EXE)',
              detail: `Phiên bản: ${app.getVersion()} Standalone\nPhát triển: HNL\nStandalone xử lý dữ liệu HNL/DXF cơ bản. DWG native, AutoLISP và thao tác AutoCAD thật cần plugin AutoCAD riêng.`,
            });
          },
        },
      ],
    },
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}


function getLibraryRoot() {
  const dir = path.join(app.getPath('userData'), 'library');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function getLibraryIndexPath() {
  return path.join(getLibraryRoot(), 'library-index-v2.json');
}

function readLibraryIndex() {
  try {
    const parsed = JSON.parse(fs.readFileSync(getLibraryIndexPath(), 'utf8'));
    return Array.isArray(parsed?.items) ? parsed.items : [];
  } catch {
    return [];
  }
}

function writeLibraryIndex(items) {
  const payload = { schemaVersion: 2, updatedAt: new Date().toISOString(), items };
  fs.writeFileSync(getLibraryIndexPath(), JSON.stringify(payload, null, 2), 'utf8');
  return payload;
}

function safeLibrarySegment(value, fallback = 'CUSTOM') {
  const cleaned = String(value || fallback)
    .replace(/[<>:"/\\|?*\x00-\x1F]/g, '_')
    .replace(/\s+/g, '_')
    .replace(/\.+$/g, '')
    .slice(0, 80);
  return cleaned || fallback;
}

async function sha256File(filePath) {
  return new Promise((resolve, reject) => {
    const hash = crypto.createHash('sha256');
    const stream = fs.createReadStream(filePath);
    stream.on('data', (chunk) => hash.update(chunk));
    stream.on('error', reject);
    stream.on('end', () => resolve(hash.digest('hex')));
  });
}

function collectDwgFiles(folder, depth = 0, out = []) {
  if (depth > 8 || out.length >= 2000) return out;
  let entries = [];
  try { entries = fs.readdirSync(folder, { withFileTypes: true }); } catch { return out; }
  for (const entry of entries) {
    if (out.length >= 2000) break;
    const full = path.join(folder, entry.name);
    if (entry.isDirectory()) collectDwgFiles(full, depth + 1, out);
    else if (entry.isFile() && path.extname(entry.name).toLowerCase() === '.dwg') out.push(full);
  }
  return out;
}

function defaultLibraryLayer(category) {
  const map = {
    ANNOTATION: 'HNL-ANNO-DETAIL',
    CEILING: 'HNL-CLG-BOARD',
    WALL: 'HNL-WALL-BOARD',
    STEEL: 'HNL-STEEL-RHS',
    MEP_REFERENCE: 'HNL-NOPLOT-HELPER',
    DETAIL: 'HNL-ANNO-DETAIL',
    CUSTOM: 'HNL-DATA-FIELD',
  };
  return map[String(category || 'CUSTOM').toUpperCase()] || map.CUSTOM;
}

async function importLibraryFile(sourcePath, options, index) {
  const stat = fs.statSync(sourcePath);
  const sha256 = await sha256File(sourcePath);
  const scope = ['HNL_STANDARD','PROJECT','MY_LIBRARY'].includes(String(options.scope))
    ? String(options.scope) : 'MY_LIBRARY';
  const category = String(options.category || 'CUSTOM').toUpperCase();
  const storageMode = String(options.storageMode || 'COPY').toUpperCase() === 'LINK' ? 'LINK' : 'COPY';

  const duplicate = index.find((x) => x.sha256 === sha256 && x.scope === scope && x.category === category);
  if (duplicate) return { item: duplicate, duplicate: true };

  let storedPath = sourcePath;
  if (storageMode === 'COPY') {
    const targetDir = path.join(getLibraryRoot(), safeLibrarySegment(scope), safeLibrarySegment(category));
    fs.mkdirSync(targetDir, { recursive: true });
    const ext = path.extname(sourcePath) || '.dwg';
    const base = safeLibrarySegment(path.basename(sourcePath, ext), 'BLOCK');
    let target = path.join(targetDir, `${base}${ext}`);
    if (fs.existsSync(target)) {
      const existingHash = await sha256File(target).catch(() => '');
      if (existingHash !== sha256) target = path.join(targetDir, `${base}_${sha256.slice(0,8)}${ext}`);
    }
    if (!fs.existsSync(target)) fs.copyFileSync(sourcePath, target);
    storedPath = target;
  }

  const idSeed = `${scope}|${category}|${sha256}|${storedPath}`;
  const item = {
    id: `lib_${crypto.createHash('sha1').update(idSeed).digest('hex').slice(0,18)}`,
    name: path.basename(sourcePath, path.extname(sourcePath)),
    fileName: path.basename(sourcePath),
    category,
    scope,
    storageMode,
    sourceDwg: storedPath,
    originalPath: sourcePath,
    layer: defaultLibraryLayer(category),
    description: `DWG library • ${storageMode === 'COPY' ? 'Managed copy' : 'Linked file'}`,
    tags: [category.toLowerCase(), 'dwg'],
    sizeBytes: stat.size,
    sha256,
    modifiedAt: stat.mtime.toISOString(),
    createdAt: new Date().toISOString(),
    favorite: false,
    recentAt: null,
    dynamicState: 'UNKNOWN',
    definitions: [],
    selectedDefinition: null,
  };
  return { item, duplicate: false };
}



function getBundledLispRoot() {
  if (app.isPackaged) return path.join(process.resourcesPath, 'legacy-lisp');
  return path.join(__dirname, '..', 'resources', 'legacy-lisp', 'extracted');
}

function getBundledLispManifestPath() {
  if (app.isPackaged) return path.join(process.resourcesPath, 'legacy-lisp', 'legacy-lisp-manifest.json');
  return path.join(__dirname, '..', 'resources', 'legacy-lisp', 'legacy-lisp-manifest.json');
}

function getBundledLispIndex() {
  const root = getBundledLispRoot();
  const manifestPath = getBundledLispManifestPath();
  let manifest = null;
  try { manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8')); } catch {}

  const lispFiles = collectLispFiles(root);
  const items = buildLispIndex(lispFiles).map(x => ({ ...x, bundled: true, origin: 'HNL_BUNDLED_44' }));

  let runtimeIndex = null;
  try {
    runtimeIndex = JSON.parse(fs.readFileSync(path.join(root, 'legacy-lisp-index.json'), 'utf8'));
  } catch {}

  const arx = [];
  try {
    for (const filePath of collectFilesByExtension(root, '.arx')) {
      const stat = fs.statSync(filePath);
      arx.push({
        name: path.basename(filePath),
        path: filePath,
        sizeBytes: stat.size,
        autoLoad: false,
        compatibility: 'LEGACY_AUTOCAD_2021_X64_UNVERIFIED_FOR_2023_2027'
      });
    }
  } catch {}

  return {
    success: true,
    root,
    manifest,
    runtimeIndex,
    items,
    lispCount: items.length,
    expectedLispCount: Number(manifest?.expectedLispCount || 44),
    complete: items.length === Number(manifest?.expectedLispCount || 44),
    legacyArx: arx,
  };
}

function collectFilesByExtension(folder, ext, depth = 0, out = []) {
  if (depth > 8 || out.length >= 2000) return out;
  let entries = [];
  try { entries = fs.readdirSync(folder, { withFileTypes: true }); } catch { return out; }
  for (const entry of entries) {
    const full = path.join(folder, entry.name);
    if (entry.isDirectory()) collectFilesByExtension(full, ext, depth + 1, out);
    else if (entry.isFile() && path.extname(entry.name).toLowerCase() === ext.toLowerCase()) out.push(full);
  }
  return out;
}

function scanLispCommands(filePath) {
  try {
    const text = fs.readFileSync(filePath, 'utf8');
    const found = [];
    const rx = /\(\s*defun\s+c:([A-Za-z0-9_\-$]+)\b/gi;
    let m;
    while ((m = rx.exec(text))) {
      const cmd = String(m[1] || '').toUpperCase();
      if (cmd && !found.includes(cmd)) found.push(cmd);
    }
    return found;
  } catch {
    return [];
  }
}

function collectLispFiles(folder, depth = 0, out = []) {
  if (depth > 8 || out.length >= 2000) return out;
  let entries = [];
  try { entries = fs.readdirSync(folder, { withFileTypes: true }); } catch { return out; }
  for (const entry of entries) {
    if (out.length >= 2000) break;
    const full = path.join(folder, entry.name);
    if (entry.isDirectory()) collectLispFiles(full, depth + 1, out);
    else if (entry.isFile() && path.extname(entry.name).toLowerCase() === '.lsp') out.push(full);
  }
  return out;
}

function buildLispIndex(filePaths) {
  return filePaths
    .filter((p) => path.extname(String(p || '')).toLowerCase() === '.lsp' && fs.existsSync(p))
    .map((filePath) => {
      let stat = null;
      try { stat = fs.statSync(filePath); } catch {}
      return {
        path: filePath,
        name: path.basename(filePath),
        commands: scanLispCommands(filePath),
        sizeBytes: stat?.size || 0,
        modifiedAt: stat?.mtime?.toISOString?.() || null,
      };
    });
}

// IPC Handlers for Native OS Dialogs
ipcMain.handle('open-file-dialog', async (_event, mode) => openProjectFileDialog(mode));
ipcMain.handle('launch-autocad-with-dwg', async (_event, filePath) => launchAutoCadWithDwg(filePath));
ipcMain.handle('select-approved-document', async () => {
  if (!mainWindow) return { success: false };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Chọn Approved Material / Submittal / Catalog',
    filters: [
      { name: 'Technical Documents', extensions: ['pdf','dwg','dxf','xlsx','xls','docx','doc','jpg','jpeg','png'] },
      { name: 'All Files', extensions: ['*'] },
    ],
    properties: ['openFile'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false };
  const filePath = result.filePaths[0];
  return { success: true, filePath, fileName: path.basename(filePath) };
});




ipcMain.handle('get-bundled-lisp-index', async () => getBundledLispIndex());

ipcMain.handle('reveal-bundled-lisp-root', async () => {
  const root = getBundledLispRoot();
  if (!fs.existsSync(root)) return { success: false, root, error: 'Bundled Lisp folder is missing.' };
  const error = await shell.openPath(root);
  return { success: !error, root, error: error || null };
});

ipcMain.handle('select-lisp-files', async () => {
  if (!mainWindow) return { success: false, items: [] };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Chọn AutoLISP nguồn (.lsp)',
    filters: [{ name: 'AutoLISP', extensions: ['lsp'] }],
    properties: ['openFile', 'multiSelections'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false, canceled: true, items: [] };
  return { success: true, items: buildLispIndex(result.filePaths) };
});

ipcMain.handle('select-lisp-folder', async () => {
  if (!mainWindow) return { success: false, items: [] };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Chọn thư mục chứa AutoLISP nguồn',
    properties: ['openDirectory'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false, canceled: true, items: [] };
  const files = collectLispFiles(result.filePaths[0]);
  return { success: true, root: result.filePaths[0], items: buildLispIndex(files) };
});

ipcMain.handle('get-library-index', async () => ({
  success: true,
  root: getLibraryRoot(),
  items: readLibraryIndex()
}));

ipcMain.handle('import-library-items', async (_event, options = {}) => {
  if (!mainWindow) return { success: false, items: [] };
  const sourceType = String(options.sourceType || 'FILES').toUpperCase();
  let filePaths = [];

  if (sourceType === 'FOLDER') {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: 'Nạp cả thư mục DWG vào HNL Library',
      properties: ['openDirectory'],
    });
    if (result.canceled || !result.filePaths.length) return { success: false, canceled: true, items: [] };
    filePaths = collectDwgFiles(result.filePaths[0]);
  } else {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: 'Nạp Block DWG vào HNL Library',
      filters: [{ name: 'AutoCAD DWG', extensions: ['dwg'] }],
      properties: ['openFile', 'multiSelections'],
    });
    if (result.canceled || !result.filePaths.length) return { success: false, canceled: true, items: [] };
    filePaths = result.filePaths.filter((p) => path.extname(p).toLowerCase() === '.dwg');
  }

  const index = readLibraryIndex();
  const imported = [];
  let duplicates = 0;

  for (const filePath of filePaths.slice(0, 2000)) {
    try {
      const r = await importLibraryFile(filePath, options, index);
      if (r.duplicate) duplicates++;
      else index.unshift(r.item);
      imported.push(r.item);
    } catch (error) {
      imported.push({ error: String(error?.message || error), originalPath: filePath });
    }
  }

  writeLibraryIndex(index);
  return {
    success: true,
    root: getLibraryRoot(),
    imported: imported.filter((x) => !x.error),
    errors: imported.filter((x) => x.error),
    duplicates,
    totalSelected: filePaths.length,
  };
});

ipcMain.handle('update-library-item', async (_event, { id, patch } = {}) => {
  const index = readLibraryIndex();
  const i = index.findIndex((x) => x.id === id);
  if (i < 0) return { success: false, error: 'Library item not found.' };

  const allowed = new Set([
    'name','category','scope','layer','description','tags','favorite','recentAt',
    'dynamicState','definitions','selectedDefinition','lineweightMm','linetype','colorHex'
  ]);
  const safePatch = {};
  for (const [k,v] of Object.entries(patch || {})) if (allowed.has(k)) safePatch[k] = v;

  index[i] = { ...index[i], ...safePatch, updatedAt: new Date().toISOString() };
  writeLibraryIndex(index);
  return { success: true, item: index[i] };
});

ipcMain.handle('remove-library-item', async (_event, { id, deleteManagedFile = false } = {}) => {
  const index = readLibraryIndex();
  const item = index.find((x) => x.id === id);
  if (!item) return { success: false, error: 'Library item not found.' };
  const next = index.filter((x) => x.id !== id);

  if (deleteManagedFile && item.storageMode === 'COPY' && item.sourceDwg) {
    const root = path.resolve(getLibraryRoot()) + path.sep;
    const target = path.resolve(String(item.sourceDwg));
    const stillUsed = next.some((x) => x.sourceDwg && path.resolve(String(x.sourceDwg)) === target);
    if (!stillUsed && target.startsWith(root) && fs.existsSync(target)) {
      try { fs.unlinkSync(target); } catch {}
    }
  }

  writeLibraryIndex(next);
  return { success: true };
});

ipcMain.handle('reveal-library-item', async (_event, filePath) => {
  const target = String(filePath || '');
  if (!target || !fs.existsSync(target)) return { success: false, error: 'File không tồn tại.' };
  shell.showItemInFolder(target);
  return { success: true };
});

ipcMain.handle('open-library-root', async () => {
  const root = getLibraryRoot();
  const error = await shell.openPath(root);
  return { success: !error, root, error: error || null };
});

ipcMain.handle('select-library-dwg', async () => {
  if (!mainWindow) return { success: false };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Nạp block/thư viện DWG vào HNL',
    filters: [{ name: 'AutoCAD DWG', extensions: ['dwg'] }],
    properties: ['openFile'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false };
  const filePath = result.filePaths[0];
  return { success: true, filePath, fileName: path.basename(filePath) };
});

ipcMain.handle('read-text-file', async (_event, filePath) => {
  try {
    const target = String(filePath || '');
    if (!target || !fs.existsSync(target)) return { success: false, error: 'File không tồn tại.' };
    const stat = fs.statSync(target);
    if (stat.size > 100 * 1024 * 1024) return { success: false, error: 'File text quá lớn (>100MB).' };
    return { success: true, content: fs.readFileSync(target, 'utf-8'), filePath: target };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});


ipcMain.handle('save-file-dialog', async (event, { defaultName, content, extDescription, extension }) => {
  const result = await dialog.showSaveDialog(mainWindow, {
    title: 'Lưu tệp CAD',
    defaultPath: defaultName || 'BanVe_HNL.dxf',
    filters: [{ name: extDescription || 'CAD File', extensions: [extension || 'dxf'] }],
  });

  if (!result.canceled && result.filePath) {
    fs.writeFileSync(result.filePath, content, 'utf-8');
    return { success: true, filePath: result.filePath };
  }
  return { success: false };
});

ipcMain.handle('open-external-url', async (_event, url) => {
  try {
    const parsed = new URL(String(url));
    if (!['https:', 'http:'].includes(parsed.protocol)) {
      return { success: false, error: 'Chỉ cho phép liên kết http/https.' };
    }
    await shell.openExternal(parsed.toString());
    return { success: true };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('set-renderer-dirty', (_event, value) => { isRendererDirty = Boolean(value); return true; });
ipcMain.handle('set-window-title', (_event, title) => { if (mainWindow && typeof title === 'string') mainWindow.setTitle(title.slice(0, 240)); return true; });

const AI_PROVIDER_IDS = ['OFFLINE','GEMINI','OPENAI','CLAUDE','GROK','OLLAMA','CUSTOM_OPENAI'];
const AI_PROVIDER_DEFAULTS = {
  OFFLINE: { model:'hnl-rules-v1', baseUrl:'' },
  GEMINI: { model:'gemini-3.7-flash', baseUrl:'https://generativelanguage.googleapis.com' },
  OPENAI: { model:'gpt-5.6', baseUrl:'https://api.openai.com/v1' },
  CLAUDE: { model:'claude-sonnet-4-20250514', baseUrl:'https://api.anthropic.com/v1' },
  GROK: { model:'grok-4.6', baseUrl:'https://api.x.ai/v1' },
  OLLAMA: { model:'gemma3', baseUrl:'http://127.0.0.1:11434' },
  CUSTOM_OPENAI: { model:'gpt-4o-mini', baseUrl:'http://127.0.0.1:1234/v1' },
};

const getAiProviderConfigPath = () => path.join(app.getPath('userData'), 'ai-provider-config.json');
const getAiProviderSecretPath = () => path.join(app.getPath('userData'), 'ai-provider-secrets.bin');

function readAiProviderConfig() {
  const defaults = {
    activeProvider: 'GEMINI',
    autoFallbackOffline: false,
    contextOnly: true,
    previewBeforeExecute: true,
    providers: Object.fromEntries(AI_PROVIDER_IDS.map(id => [id, {...AI_PROVIDER_DEFAULTS[id]}])),
  };
  try {
    if (!fs.existsSync(getAiProviderConfigPath())) return defaults;
    const raw = JSON.parse(fs.readFileSync(getAiProviderConfigPath(), 'utf-8'));
    return {
      ...defaults,
      ...raw,
      providers: Object.fromEntries(AI_PROVIDER_IDS.map(id => [
        id,
        { ...AI_PROVIDER_DEFAULTS[id], ...(raw?.providers?.[id] || {}) }
      ])),
    };
  } catch (_) {
    return defaults;
  }
}

function writeAiProviderConfig(config) {
  fs.mkdirSync(path.dirname(getAiProviderConfigPath()), { recursive: true });
  fs.writeFileSync(getAiProviderConfigPath(), JSON.stringify(config, null, 2), 'utf-8');
}

function readAiProviderSecrets() {
  try {
    if (!safeStorage.isEncryptionAvailable() || !fs.existsSync(getAiProviderSecretPath())) return {};
    return JSON.parse(safeStorage.decryptString(fs.readFileSync(getAiProviderSecretPath())) || '{}');
  } catch (_) {
    return {};
  }
}

function writeAiProviderSecrets(secrets) {
  if (!safeStorage.isEncryptionAvailable()) throw new Error('Windows encryption chưa sẵn sàng.');
  fs.mkdirSync(path.dirname(getAiProviderSecretPath()), { recursive: true });
  fs.writeFileSync(getAiProviderSecretPath(), safeStorage.encryptString(JSON.stringify(secrets || {})));
}

function applyAiProviderStateToEnv(config = readAiProviderConfig(), secrets = readAiProviderSecrets()) {
  const active = AI_PROVIDER_IDS.includes(String(config.activeProvider || '').toUpperCase())
    ? String(config.activeProvider).toUpperCase()
    : 'OFFLINE';
  process.env.HNL_AI_ACTIVE_PROVIDER = active;
  process.env.HNL_AI_AUTO_FALLBACK_OFFLINE = config.autoFallbackOffline === true ? 'true' : 'false';

  for (const id of AI_PROVIDER_IDS) {
    const p = { ...AI_PROVIDER_DEFAULTS[id], ...(config.providers?.[id] || {}) };
    process.env[`HNL_AI_${id}_MODEL`] = String(p.model || '');
    process.env[`HNL_AI_${id}_BASE_URL`] = String(p.baseUrl || '');
  }

  process.env.GEMINI_API_KEY = String(secrets.GEMINI || process.env.GEMINI_API_KEY || '');
  process.env.OPENAI_API_KEY = String(secrets.OPENAI || process.env.OPENAI_API_KEY || '');
  process.env.ANTHROPIC_API_KEY = String(secrets.CLAUDE || process.env.ANTHROPIC_API_KEY || '');
  process.env.XAI_API_KEY = String(secrets.GROK || process.env.XAI_API_KEY || '');
  process.env.CUSTOM_OPENAI_API_KEY = String(secrets.CUSTOM_OPENAI || process.env.CUSTOM_OPENAI_API_KEY || '');
}

function publicAiProviderConfig() {
  const cfg = readAiProviderConfig();
  const secrets = readAiProviderSecrets();
  return {
    ...cfg,
    configured: Object.fromEntries(AI_PROVIDER_IDS.map(id => [
      id,
      id === 'OFFLINE' || id === 'OLLAMA' || Boolean(secrets[id])
    ])),
  };
}

ipcMain.handle('get-ai-provider-config', () => {
  try {
    applyAiProviderStateToEnv();
    return { success: true, config: publicAiProviderConfig() };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('save-ai-provider-config', async (_event, input) => {
  try {
    const provider = String(input?.provider || input?.activeProvider || 'GEMINI').toUpperCase();
    if (!AI_PROVIDER_IDS.includes(provider)) throw new Error(`Provider không hợp lệ: ${provider}`);

    const cfg = readAiProviderConfig();
    cfg.activeProvider = String(input?.activeProvider || provider).toUpperCase();
    cfg.autoFallbackOffline = input?.autoFallbackOffline !== false;
    cfg.contextOnly = input?.contextOnly !== false;
    cfg.previewBeforeExecute = input?.previewBeforeExecute !== false;
    cfg.providers = cfg.providers || {};
    cfg.providers[provider] = {
      ...AI_PROVIDER_DEFAULTS[provider],
      ...(cfg.providers[provider] || {}),
      ...(typeof input?.model === 'string' ? { model: input.model.trim() } : {}),
      ...(typeof input?.baseUrl === 'string' ? { baseUrl: input.baseUrl.trim() } : {}),
    };
    writeAiProviderConfig(cfg);

    const secrets = readAiProviderSecrets();
    if (input?.clearKey) delete secrets[provider];
    if (typeof input?.apiKey === 'string' && input.apiKey.trim()) secrets[provider] = input.apiKey.trim();
    if (provider !== 'OFFLINE' && provider !== 'OLLAMA' && (input?.clearKey || (typeof input?.apiKey === 'string' && input.apiKey.trim()))) {
      writeAiProviderSecrets(secrets);
    }

    applyAiProviderStateToEnv(cfg, secrets);
    return { success: true, config: publicAiProviderConfig() };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

// Legacy Gemini key IPC kept for old renderers.
const getSecretPath = () => getAiProviderSecretPath();
ipcMain.handle('save-ai-key', async (_event, key) => {
  try {
    const value = String(key || '').trim();
    if (!value) return { success: false, error: 'API key trống.' };
    const secrets = readAiProviderSecrets();
    secrets.GEMINI = value;
    writeAiProviderSecrets(secrets);
    const cfg = readAiProviderConfig();
    cfg.activeProvider = 'GEMINI';
    writeAiProviderConfig(cfg);
    applyAiProviderStateToEnv(cfg, secrets);
    return { success: true };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});
ipcMain.handle('get-ai-key-status', () => {
  try {
    const secrets = readAiProviderSecrets();
    return { configured: Boolean(secrets.GEMINI || process.env.GEMINI_API_KEY) };
  } catch {
    return { configured: false };
  }
});


ipcMain.handle('get-printers', async () => {
  try {
    if (!mainWindow) return [];
    const printers = await mainWindow.webContents.getPrintersAsync();
    return printers.map(p => ({
      name: p.name,
      displayName: p.displayName || p.name,
      description: p.description || '',
      status: p.status,
      isDefault: Boolean(p.isDefault)
    }));
  } catch (error) {
    return [];
  }
});

async function createPrintWindow(html) {
  const win = new BrowserWindow({
    show: false,
    width: 1200,
    height: 900,
    webPreferences: { nodeIntegration: false, contextIsolation: true, sandbox: true, webSecurity: true }
  });
  const dataUrl = 'data:text/html;charset=utf-8,' + encodeURIComponent(String(html || ''));
  await win.loadURL(dataUrl);
  return win;
}

ipcMain.handle('render-pdf-from-html', async (_event, { html, defaultName, landscape, pageSize }) => {
  let win = null;
  try {
    const result = await dialog.showSaveDialog(mainWindow, {
      title: 'Xuất PDF',
      defaultPath: defaultName || 'HNL_Plot.pdf',
      filters: [{ name: 'PDF', extensions: ['pdf'] }],
    });
    if (result.canceled || !result.filePath) return { success: false, canceled: true };
    win = await createPrintWindow(html);
    const data = await win.webContents.printToPDF({
      printBackground: true,
      landscape: Boolean(landscape),
      pageSize: pageSize || 'A4',
      preferCSSPageSize: true,
      margins: { top: 0, bottom: 0, left: 0, right: 0 },
    });
    fs.writeFileSync(result.filePath, data);
    return { success: true, filePath: result.filePath, bytes: data.length };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  } finally {
    if (win && !win.isDestroyed()) win.destroy();
  }
});

ipcMain.handle('print-html', async (_event, { html, printerName, landscape, copies }) => {
  let win = null;
  try {
    win = await createPrintWindow(html);
    return await new Promise((resolve) => {
      win.webContents.print({
        silent: Boolean(printerName),
        deviceName: printerName || undefined,
        printBackground: true,
        landscape: Boolean(landscape),
        copies: Math.max(1, Number(copies || 1)),
      }, (success, failureReason) => {
        resolve(success ? { success: true } : { success: false, error: failureReason || 'Print failed' });
      });
    });
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  } finally {
    setTimeout(() => { if (win && !win.isDestroyed()) win.destroy(); }, 500);
  }
});



ipcMain.handle('write-temp-text-file', async (_event, { fileName, content }) => {
  try {
    const safeName = String(fileName || `hnl_${Date.now()}.txt`).replace(/[<>:"/\\|?*\x00-\x1F]/g, '_');
    const dir = path.join(app.getPath('temp'), 'HNL_CAD_AI', 'exports');
    fs.mkdirSync(dir, { recursive: true });
    const filePath = path.join(dir, safeName);
    fs.writeFileSync(filePath, String(content || ''), 'utf-8');
    return { success: true, filePath, bytes: Buffer.byteLength(String(content || ''), 'utf8') };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('choose-save-path', async (_event, { title, defaultName, extension, description }) => {
  try {
    const result = await dialog.showSaveDialog(mainWindow, {
      title: title || 'Chọn tệp xuất',
      defaultPath: defaultName || 'output.pdf',
      filters: [{ name: description || 'File', extensions: [extension || 'pdf'] }],
    });
    if (result.canceled || !result.filePath) return { success: false, canceled: true };
    return { success: true, filePath: result.filePath };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('choose-output-folder', async () => {
  try {
    const result = await dialog.showOpenDialog(mainWindow, { title: 'Chọn thư mục xuất', properties: ['openDirectory', 'createDirectory'] });
    if (result.canceled || !result.filePaths?.[0]) return { success: false, canceled: true };
    return { success: true, folderPath: result.filePaths[0] };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('render-pdf-to-path', async (_event, { html, filePath, landscape, pageSize }) => {
  let win = null;
  try {
    if (!filePath) return { success: false, error: 'Thiếu filePath.' };
    win = await createPrintWindow(html);
    const data = await win.webContents.printToPDF({
      printBackground: true,
      landscape: Boolean(landscape),
      pageSize: pageSize || 'A4',
      preferCSSPageSize: true,
      margins: { top: 0, bottom: 0, left: 0, right: 0 },
    });
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, data);
    return { success: true, filePath, bytes: data.length };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  } finally {
    if (win && !win.isDestroyed()) win.destroy();
  }
});

ipcMain.handle('open-sheetset-dialog', async () => {
  try {
    const result = await dialog.showOpenDialog(mainWindow, {
      title: 'Mở Sheet Set',
      properties: ['openFile'],
      filters: [
        { name: 'Sheet Set', extensions: ['json', 'dst'] },
        { name: 'HNL Sheet Set', extensions: ['json'] },
        { name: 'AutoCAD Sheet Set', extensions: ['dst'] },
      ],
    });
    if (result.canceled || !result.filePaths?.[0]) return { success: false, canceled: true };
    const filePath = result.filePaths[0];
    const ext = path.extname(filePath).toLowerCase();
    if (ext === '.dst') {
      return { success: true, filePath, extension: 'dst', requiresAutoCad: true };
    }
    return { success: true, filePath, extension: 'json', content: fs.readFileSync(filePath, 'utf-8'), requiresAutoCad: false };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('install-autocad-bundle', async () => installOrRepairAutoCadBundle());
ipcMain.handle('get-autocad-bundle-status', async () => getAutoCadBundleStatus());
ipcMain.handle('open-autocad-plugin-folder', async () => {
  try {
    const { target } = getAutoCadBundlePaths();
    fs.mkdirSync(path.dirname(target), { recursive: true });
    const error = await shell.openPath(path.dirname(target));
    return error ? { success: false, error } : { success: true, folderPath: path.dirname(target) };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
});

ipcMain.handle('get-app-version', () => {
  return app.getVersion();
});

// App Lifecycle
app.whenReady().then(async () => {
  // Publish the actual EXE path so the AutoCAD palette can reopen HNL even if the user chose a custom install folder.
  try {
    const markerDir = path.join(app.getPath('appData'), 'HNL CAD AI');
    fs.mkdirSync(markerDir, { recursive: true });
    fs.writeFileSync(path.join(markerDir, 'manager-path.txt'), process.execPath, 'utf-8');
  } catch (_) {}
  // Per-user Autodesk ApplicationPlugins autoload: no NETLOAD required.
  const pluginInstallResult = installOrRepairAutoCadBundle();
  await createWindow();

  if (pluginInstallResult?.restartRequired || pluginInstallResult?.legacyBlocked?.length) {
    setTimeout(() => {
      const details = [];
      if (pluginInstallResult.restartRequired) details.push('AutoCAD đang mở và vẫn giữ DLL/menu HNL cũ trong bộ nhớ. BẮT BUỘC đóng TOÀN BỘ AutoCAD rồi mở lại.');
      if (pluginInstallResult.legacyRemoved?.length) details.push(`Đã tự xóa ${pluginInstallResult.legacyRemoved.length} bundle HNL cũ trong AppData.`);
      if (pluginInstallResult.legacyBlocked?.length) details.push(`Phát hiện ${pluginInstallResult.legacyBlocked.length} bundle HNL khác trong ProgramData/đường dẫn cần quyền Admin.`);
      dialog.showMessageBox(mainWindow, {
        type: 'warning',
        title: 'HNL AutoCAD Plugin cần khởi động lại',
        message: `HNL plugin ${pluginInstallResult.installedVersion || app.getVersion()} đã cập nhật.`,
        detail: details.join('\n\n'),
      }).catch(() => {});
    }, 700);
  }

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) void createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
