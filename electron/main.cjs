// Electron Main Process for HNL CAD AI TOOL Standalone Desktop (.EXE)
const { app, BrowserWindow, dialog, ipcMain, Menu, shell, safeStorage } = require('electron');
const crypto = require('crypto');
const path = require('path');
const fs = require('fs');

let mainWindow = null;
let isRendererDirty = false;
let forceQuit = false;
process.env.HNL_API_TOKEN = process.env.HNL_API_TOKEN || crypto.randomBytes(32).toString('hex');

// Prevent two app instances from competing for the same local API port.
const gotSingleInstanceLock = app.requestSingleInstanceLock();
if (!gotSingleInstanceLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (!mainWindow) return;
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.show();
    mainWindow.focus();
  });
}

function getAutoCadBundlePaths() {
  const source = path.join(process.resourcesPath || '', 'autocad', 'HNL.CadBridge.bundle');
  const target = path.join(app.getPath('appData'), 'Autodesk', 'ApplicationPlugins', 'HNL.CadBridge.bundle');
  return { source, target };
}

function bundleHasDll(bundlePath) {
  try {
    for (const year of ['2023','2024','2025','2026']) {
      if (fs.existsSync(path.join(bundlePath, 'Contents', year, 'Hnl.CadBridge.dll'))) return true;
    }
  } catch (_) {}
  return false;
}

function installOrRepairAutoCadBundle() {
  try {
    const { source, target } = getAutoCadBundlePaths();
    if (!fs.existsSync(path.join(source, 'PackageContents.xml')) || !bundleHasDll(source)) {
      return { success: false, reason: 'PLUGIN_BUNDLE_NOT_PACKAGED', source, target };
    }
    fs.mkdirSync(path.dirname(target), { recursive: true });
    if (fs.existsSync(target)) fs.rmSync(target, { recursive: true, force: true });
    fs.cpSync(source, target, { recursive: true });
    return { success: true, source, target, installedDll: bundleHasDll(target) };
  } catch (error) {
    return { success: false, error: String(error?.message || error) };
  }
}

function getAutoCadBundleStatus() {
  try {
    const { source, target } = getAutoCadBundlePaths();
    return {
      source,
      target,
      packaged: fs.existsSync(path.join(source, 'PackageContents.xml')) && bundleHasDll(source),
      installed: fs.existsSync(path.join(target, 'PackageContents.xml')) && bundleHasDll(target),
    };
  } catch (error) {
    return { packaged: false, installed: false, error: String(error?.message || error) };
  }
}

function createWindow() {
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
    process.env.HNL_PORT = process.env.HNL_PORT || '32145';
    try {
      const bridgeDir = path.join(app.getPath('temp'), 'HNL_CAD_AI');
      fs.mkdirSync(bridgeDir, { recursive: true });
      fs.writeFileSync(path.join(bridgeDir, 'bridge.json'), JSON.stringify({
        host: '127.0.0.1',
        port: Number(process.env.HNL_PORT),
        token: process.env.HNL_API_TOKEN,
        pid: process.pid,
        version: app.getVersion()
      }, null, 2), 'utf-8');
    } catch (_) {}
    try {
      const appRoot = app.getAppPath();
      // Packaged Electron returns resources/app.asar here. app.asar is a file-backed
      // virtual archive, not a real directory, so process.chdir(appRoot) fails on
      // Windows. Pass the ASAR root to the bundled server explicitly instead.
      process.env.HNL_APP_ROOT = appRoot;
      try { if (!process.env.GEMINI_API_KEY && safeStorage.isEncryptionAvailable() && fs.existsSync(getSecretPath())) process.env.GEMINI_API_KEY = safeStorage.decryptString(fs.readFileSync(getSecretPath())); } catch (_) {}
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
  mainWindow.on('closed', () => { mainWindow = null; });

  buildAppMenu();
}

async function openProjectFileDialog() {
  if (!mainWindow) return { success: false };
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Mở tệp HNL CAD AI',
    filters: [
      { name: 'CAD Drawings', extensions: ['dwg', 'dxf'] },
      { name: 'HNL Project', extensions: ['json'] },
      { name: 'AutoLISP Scripts', extensions: ['lsp'] },
      { name: 'All Files', extensions: ['*'] },
    ],
    properties: ['openFile'],
  });
  if (result.canceled || !result.filePaths.length) return { success: false };
  const filePath = result.filePaths[0];
  const ext = path.extname(filePath).toLowerCase();
  if (ext === '.dwg') {
    mainWindow.webContents.send('file-opened', { filePath, fileName: path.basename(filePath), extension: 'dwg', requiresAutoCad: true });
    return { success: true, filePath, requiresAutoCad: true };
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
          label: 'Mở DWG / DXF / Project...',
          accelerator: 'CmdOrCtrl+O',
          click: () => { openProjectFileDialog(); },
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

// IPC Handlers for Native OS Dialogs
ipcMain.handle('open-file-dialog', async () => openProjectFileDialog());

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

const getSecretPath = () => path.join(app.getPath('userData'), 'gemini-key.bin');
ipcMain.handle('save-ai-key', async (_event, key) => {
  try {
    const value = String(key || '').trim(); if (!value) return { success: false, error: 'API key trống.' };
    if (!safeStorage.isEncryptionAvailable()) return { success: false, error: 'Windows encryption chưa sẵn sàng.' };
    fs.writeFileSync(getSecretPath(), safeStorage.encryptString(value)); process.env.GEMINI_API_KEY = value;
    return { success: true };
  } catch (error) { return { success: false, error: String(error?.message || error) }; }
});
ipcMain.handle('get-ai-key-status', () => {
  try { return { configured: Boolean(process.env.GEMINI_API_KEY) || fs.existsSync(getSecretPath()) }; } catch { return { configured: false }; }
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
app.whenReady().then(() => {
  // Publish the actual EXE path so the AutoCAD palette can reopen HNL even if the user chose a custom install folder.
  try {
    const markerDir = path.join(app.getPath('appData'), 'HNL CAD AI');
    fs.mkdirSync(markerDir, { recursive: true });
    fs.writeFileSync(path.join(markerDir, 'manager-path.txt'), process.execPath, 'utf-8');
  } catch (_) {}
  // Per-user Autodesk ApplicationPlugins autoload: no NETLOAD required.
  installOrRepairAutoCadBundle();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
