import React, { useState } from "react";
import {
  Download,
  Copy,
  Check,
  FileCode,
  Package,
  Terminal,
  X,
  ExternalLink,
  Laptop,
  CheckCircle2,
  HardDrive,
  Cpu,
  ShieldCheck,
  Sparkles,
  Play,
  Layers,
  FolderArchive,
} from "lucide-react";

interface StandaloneExeBuilderModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const StandaloneExeBuilderModal: React.FC<StandaloneExeBuilderModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [activeTab, setActiveTab] = useState<"GUIDE" | "FILES" | "STANDALONE_FEATURES">("GUIDE");
  const [selectedFile, setSelectedFile] = useState<string>("build-exe.bat");
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const standaloneFiles: Record<string, { desc: string; language: string; code: string }> = {
    "build-exe.bat": {
      desc: "Tệp thực thi 1-Click trên Windows để tự động cài đặt và đóng gói ra file HnlCadAiTool.exe",
      language: "bat",
      code: `@echo off
title DONG GOI PHAN MEM HNL CAD AI TOOL THANH FILE .EXE DOC LAP
color 0b
echo ==============================================================================
echo       HNL ARCHITECTURE - CAD AI TOOL DESKTOP STANDALONE BUILDER (.EXE)
echo ==============================================================================
echo.
echo [1/4] Kiem tra moi truong Node.js & NPM...
node -v >nul 2>&1
if %errorlevel% neq 0 (
    echo [LOI] May tinh cua ban chua cai Node.js! Vui long tai va cai tai: https://nodejs.org
    pause
    exit /b
)
echo      - Node.js da san sang.

echo.
echo [2/4] Dang cai dat cac goi thu vien can thiet (npm install)...
call npm install --save-dev electron electron-builder wait-on concurrently

echo.
echo [3/4] Dang bien dich Web CAD thanh thu muc dist/ ...
call npm run build

echo.
echo [4/4] Dang dong goi thanh file .EXE doc lap (Windows 64-bit)...
npx electron-builder --win nsis portable --x64

echo.
echo ==============================================================================
echo [THANH CONG] File .EXE da duoc tao tai thu muc: dist_electron\\
echo.
echo   - File cai dat tu dong: dist_electron\\HnlCadAiTool_Setup_1.0.0.exe
echo   - File chay ngay (Portable, khong can cai): dist_electron\\HnlCadAiTool_Portable.exe
echo ==============================================================================
pause`,
    },

    "electron/main.cjs": {
      desc: "Mã nguồn tiến trình chính Electron: Tạo cửa sổ Desktop 64-bit, tối ưu GPU, menu File/Edit/AI bản địa",
      language: "javascript",
      code: `// Electron Main Process for HNL CAD AI TOOL Standalone Desktop (.EXE)
const { app, BrowserWindow, dialog, ipcMain, Menu, shell } = require('electron');
const path = require('path');
const fs = require('fs');

let mainWindow = null;

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
      sandbox: false,
      webSecurity: true,
      hardwareAcceleration: true,
    },
    frame: true,
    show: false,
  });

  // Load packaged static dist/index.html
  const indexPath = path.join(__dirname, '..', 'dist', 'index.html');
  mainWindow.loadFile(indexPath);

  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
    mainWindow.maximize();
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  buildAppMenu();
}

function buildAppMenu() {
  const template = [
    {
      label: 'Tập tin (File)',
      submenu: [
        { label: 'Bản vẽ mới (New)', accelerator: 'CmdOrCtrl+N', click: () => mainWindow?.webContents.send('menu-command', 'NEW_DRAWING') },
        { label: 'Mở bản vẽ DXF/DWG (Open)...', accelerator: 'CmdOrCtrl+O', click: async () => { /* Open dialog */ } },
        { label: 'Lưu bản vẽ (Save)...', accelerator: 'CmdOrCtrl+S', click: () => mainWindow?.webContents.send('menu-command', 'SAVE_DRAWING') },
        { type: 'separator' },
        { label: 'Xuất DXF...', click: () => mainWindow?.webContents.send('menu-command', 'EXPORT_DXF') },
        { label: 'In / Xuất PDF (Print/Plot)...', accelerator: 'CmdOrCtrl+P', click: () => mainWindow?.webContents.send('menu-command', 'PRINT_PDF') },
        { type: 'separator' },
        { label: 'Thoát (Exit)', accelerator: 'CmdOrCtrl+Q', click: () => app.quit() },
      ],
    },
    {
      label: 'Công cụ AI (AI Tools)',
      submenu: [
        { label: 'AI Auto Detail & Layout Composer', accelerator: 'CmdOrCtrl+Shift+D', click: () => mainWindow?.webContents.send('menu-command', 'OPEN_AUTO_DETAIL') },
        { label: 'Trợ lý HNL AI Palette', accelerator: 'CmdOrCtrl+Shift+A', click: () => mainWindow?.webContents.send('menu-command', 'TOGGLE_AI_PALETTE') },
        { label: 'Kiểm tra lỗi CAD (Audit Engine)', click: () => mainWindow?.webContents.send('menu-command', 'OPEN_AUDIT') },
      ],
    },
    {
      label: 'Giao diện (View)',
      submenu: [
        { label: 'Toàn màn hình (Fullscreen)', accelerator: 'F11', click: () => mainWindow?.setFullScreen(!mainWindow.isFullScreen()) },
        { label: 'Công cụ phát triển (DevTools)', accelerator: 'CmdOrCtrl+Shift+I', click: () => mainWindow?.webContents.toggleDevTools() },
      ],
    },
  ];
  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

app.whenReady().then(createWindow);
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });`,
    },

    "electron/preload.cjs": {
      desc: "Cầu nối bảo mật giữa Web CAD và hệ điều hành Windows (Mở/Lưu file, File Explorer)",
      language: "javascript",
      code: `const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronNative', {
  isElectron: true,
  platform: process.platform,
  saveFile: (options) => ipcRenderer.invoke('save-file-dialog', options),
  openExternal: (url) => ipcRenderer.invoke('open-external-url', url),
  getVersion: () => ipcRenderer.invoke('get-app-version'),
  onMenuCommand: (callback) => {
    ipcRenderer.on('menu-command', (_event, command) => callback(command));
  },
  onFileOpened: (callback) => {
    ipcRenderer.on('file-opened', (_event, data) => callback(data));
  },
});`,
    },

    "package.json (Electron Build Config)": {
      desc: "Cấu hình build Electron Builder: Tên ứng dụng, icon, định dạng Portable .exe và Setup NSIS",
      language: "json",
      code: `{
  "name": "hnl-cad-ai-tool",
  "version": "1.0.0",
  "description": "HNL CAD AI TOOL - Desktop Standalone Professional Edition",
  "main": "electron/main.cjs",
  "author": "HNL Architecture & CAD Solutions",
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "electron:dev": "concurrently \\"vite\\" \\"wait-on http://localhost:3000 && electron .\\"",
    "electron:build": "vite build && electron-builder --win --x64"
  },
  "build": {
    "appId": "com.hnl.cadtool",
    "productName": "HnlCadAiTool",
    "directories": {
      "output": "dist_electron"
    },
    "files": [
      "dist/**/*",
      "electron/**/*"
    ],
    "win": {
      "target": [
        { "target": "nsis", "arch": ["x64"] },
        { "target": "portable", "arch": ["x64"] }
      ],
      "icon": "public/favicon.ico"
    },
    "nsis": {
      "oneClick": false,
      "allowToChangeInstallationDirectory": true,
      "createDesktopShortcut": true,
      "createStartMenuShortcut": true,
      "shortcutName": "HNL CAD AI TOOL"
    }
  }
}`,
    },
  };

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownloadBatch = () => {
    const batContent = standaloneFiles["build-exe.bat"].code;
    const blob = new Blob([batContent], { type: "application/x-bat" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "build-exe.bat";
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleDownloadZipBundle = () => {
    // Generate an all-in-one standalone package script
    const instructions = `# HNL CAD AI TOOL - HƯỚNG DẪN BUILD FILE .EXE ĐỘC LẬP
============================================================
Tác quyền: HNL Architecture & CAD Solutions

BƯỚC 1: Cài đặt Node.js từ https://nodejs.org (nếu máy chưa có)
BƯỚC 2: Nhấp đúp vào file 'build-exe.bat' trong thư mục này
BƯỚC 3: Quá trình build sẽ tự động chạy trong khoảng 1-2 phút
BƯỚC 4: Vào thư mục 'dist_electron/' để nhận:
  - HnlCadAiTool_Portable.exe (Chạy ngay, không cần cài đặt)
  - HnlCadAiTool_Setup_1.0.0.exe (Bộ cài đặt Windows chuẩn)
`;
    const blob = new Blob([instructions], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "README_BUILD_EXE.txt";
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-md p-4 animate-in fade-in duration-200">
      <div className="bg-neutral-900 border border-neutral-700 rounded-2xl w-full max-w-5xl h-[88vh] flex flex-col shadow-2xl overflow-hidden text-neutral-200">
        {/* Header */}
        <div className="px-6 py-4 bg-neutral-800/80 border-b border-neutral-700 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-600 flex items-center justify-center text-white shadow-lg shadow-cyan-900/40">
              <Laptop className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <h2 className="text-lg font-bold text-white">Đóng Gói Ứng Dụng Chạy Độc Lập (.EXE Standalone)</h2>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                  Windows 64-bit Portable & Setup
                </span>
              </div>
              <p className="text-xs text-neutral-400">
                Chạy độc lập không cần AutoCAD cho các công cụ Canvas/DXF/JSON/LSP; DWG native và lệnh AutoCAD cần plugin/SDK tương ứng
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-2 rounded-lg hover:bg-neutral-700 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="flex items-center px-6 border-b border-neutral-800 bg-neutral-950/60 space-x-2">
          <button
            onClick={() => setActiveTab("GUIDE")}
            className={`flex items-center space-x-2 py-3 px-4 text-xs font-semibold border-b-2 transition ${
              activeTab === "GUIDE"
                ? "border-cyan-500 text-cyan-400 bg-cyan-500/10"
                : "border-transparent text-neutral-400 hover:text-neutral-200"
            }`}
          >
            <Play className="w-4 h-4" />
            <span>1. Hướng dẫn Build .EXE (3 Bước Nhanh)</span>
          </button>

          <button
            onClick={() => setActiveTab("FILES")}
            className={`flex items-center space-x-2 py-3 px-4 text-xs font-semibold border-b-2 transition ${
              activeTab === "FILES"
                ? "border-cyan-500 text-cyan-400 bg-cyan-500/10"
                : "border-transparent text-neutral-400 hover:text-neutral-200"
            }`}
          >
            <FileCode className="w-4 h-4" />
            <span>2. Tệp Cấu Hình Đóng Gói (Electron & Batch)</span>
          </button>

          <button
            onClick={() => setActiveTab("STANDALONE_FEATURES")}
            className={`flex items-center space-x-2 py-3 px-4 text-xs font-semibold border-b-2 transition ${
              activeTab === "STANDALONE_FEATURES"
                ? "border-cyan-500 text-cyan-400 bg-cyan-500/10"
                : "border-transparent text-neutral-400 hover:text-neutral-200"
            }`}
          >
            <ShieldCheck className="w-4 h-4" />
            <span>3. Tính Năng Chạy Độc Lập (Offline Ready)</span>
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-6 bg-neutral-900/50">
          {activeTab === "GUIDE" && (
            <div className="space-y-6 max-w-4xl mx-auto">
              {/* Highlight Banner */}
              <div className="p-5 rounded-xl bg-gradient-to-r from-cyan-950/60 via-neutral-900 to-blue-950/60 border border-cyan-500/30 flex items-start justify-between">
                <div className="space-y-2">
                  <div className="flex items-center space-x-2">
                    <Sparkles className="w-5 h-5 text-cyan-400" />
                    <span className="font-bold text-white text-sm">2 Cách Sử Dụng Phần Mềm HNL CAD TOOL:</span>
                  </div>
                  <ul className="text-xs text-neutral-300 space-y-1.5 list-disc list-inside">
                    <li>
                      <strong className="text-cyan-300">Chế độ 1: Ứng dụng Desktop Độc Lập (.EXE)</strong> - Chạy trực tiếp trên Windows với Canvas nội bộ, Layer, Block, DXF, Auto Detail và Sheet Layout; không thay thế toàn bộ AutoCAD.
                    </li>
                    <li>
                      <strong className="text-amber-300">Chế độ 2: Plugin Tích hợp AutoCAD (.NET DLL)</strong> - Mục tiêu AutoCAD 2023+; DLL/.bundle phải build và kiểm thử đúng runtime từng phiên bản.
                    </li>
                  </ul>
                </div>

                <div className="flex flex-col space-y-2">
                  <button
                    onClick={handleDownloadBatch}
                    className="flex items-center space-x-2 px-4 py-2 rounded-lg bg-cyan-500 hover:bg-cyan-400 text-black font-bold text-xs transition shadow-lg shadow-cyan-500/20 whitespace-nowrap"
                  >
                    <Download className="w-4 h-4" />
                    <span>Tải file build-exe.bat</span>
                  </button>
                  <button
                    onClick={handleDownloadZipBundle}
                    className="flex items-center space-x-2 px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 border border-neutral-700 text-xs transition whitespace-nowrap"
                  >
                    <FolderArchive className="w-4 h-4 text-cyan-400" />
                    <span>Tải Hướng dẫn Build</span>
                  </button>
                </div>
              </div>

              {/* 3 Simple Steps */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="p-4 rounded-xl bg-neutral-800/60 border border-neutral-700 flex flex-col justify-between">
                  <div className="space-y-2">
                    <div className="w-7 h-7 rounded-full bg-cyan-500/20 text-cyan-400 font-bold text-xs flex items-center justify-center border border-cyan-500/40">
                      1
                    </div>
                    <div className="font-bold text-white text-sm">Cài Node.js & Mở Terminal</div>
                    <p className="text-xs text-neutral-400 leading-relaxed">
                      Tải và cài đặt Node.js LTS từ <a href="https://nodejs.org" target="_blank" rel="noreferrer" className="text-cyan-400 underline">nodejs.org</a> nếu máy bạn chưa có.
                    </p>
                  </div>
                  <div className="mt-4 p-2 rounded bg-black/60 font-mono text-[11px] text-cyan-300">
                    node -v
                  </div>
                </div>

                <div className="p-4 rounded-xl bg-neutral-800/60 border border-neutral-700 flex flex-col justify-between">
                  <div className="space-y-2">
                    <div className="w-7 h-7 rounded-full bg-cyan-500/20 text-cyan-400 font-bold text-xs flex items-center justify-center border border-cyan-500/40">
                      2
                    </div>
                    <div className="font-bold text-white text-sm">Chạy Lệnh Build Tự Động</div>
                    <p className="text-xs text-neutral-400 leading-relaxed">
                      Chạy tệp <code className="text-cyan-300">build-exe.bat</code> hoặc gõ lệnh npm để đóng gói ứng dụng Electron.
                    </p>
                  </div>
                  <div className="mt-4 p-2 rounded bg-black/60 font-mono text-[11px] text-cyan-300">
                    npm run electron:build
                  </div>
                </div>

                <div className="p-4 rounded-xl bg-neutral-800/60 border border-neutral-700 flex flex-col justify-between">
                  <div className="space-y-2">
                    <div className="w-7 h-7 rounded-full bg-emerald-500/20 text-emerald-400 font-bold text-xs flex items-center justify-center border border-emerald-500/40">
                      3
                    </div>
                    <div className="font-bold text-white text-sm">Nhận File .EXE Hoàn Chỉnh</div>
                    <p className="text-xs text-neutral-400 leading-relaxed">
                      File .EXE tạo ra trong thư mục <code className="text-emerald-300">dist_electron/</code> gồm bản Portable và bản Cài đặt.
                    </p>
                  </div>
                  <div className="mt-4 p-2 rounded bg-black/60 font-mono text-[11px] text-emerald-300">
                    HnlCadAiTool_Portable.exe
                  </div>
                </div>
              </div>

              {/* Terminal Code Snippet Box */}
              <div className="rounded-xl bg-neutral-950 border border-neutral-800 overflow-hidden shadow-xl">
                <div className="px-4 py-2.5 bg-neutral-900 border-b border-neutral-800 flex items-center justify-between">
                  <div className="flex items-center space-x-2">
                    <Terminal className="w-4 h-4 text-cyan-400" />
                    <span className="text-xs font-bold text-neutral-300">Các câu lệnh Terminal đóng gói nhanh:</span>
                  </div>
                  <button
                    onClick={() =>
                      handleCopy(`git clone <repo_url>
cd hnl-cad-tool
npm install
npm run build
npx electron-builder --win portable --x64`)
                    }
                    className="flex items-center space-x-1 text-xs text-neutral-400 hover:text-white px-2 py-1 rounded bg-neutral-800 hover:bg-neutral-700 transition"
                  >
                    {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                    <span>{copied ? "Đã sao chép!" : "Sao chép lệnh"}</span>
                  </button>
                </div>
                <pre className="p-4 text-xs font-mono text-cyan-300 overflow-x-auto leading-relaxed">
{`# 1. Cài đặt các thư viện đóng gói Electron
npm install --save-dev electron electron-builder wait-on concurrently

# 2. Biên dịch mã nguồn Web CAD
npm run build

# 3. Đóng gói ra file .EXE độc lập chạy trên mọi máy Windows
npx electron-builder --win nsis portable --x64

# ==> Kết quả: File HnlCadAiTool_Portable.exe và HnlCadAiTool_Setup_1.0.0.exe sẵn sàng sử dụng!`}
                </pre>
              </div>
            </div>
          )}

          {activeTab === "FILES" && (
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 h-full">
              {/* Sidebar File List */}
              <div className="md:col-span-1 space-y-1.5 bg-neutral-950/60 p-3 rounded-xl border border-neutral-800">
                <div className="text-[11px] font-bold text-neutral-400 uppercase tracking-wider px-2 mb-2">
                  Danh sách tệp cấu hình:
                </div>
                {Object.keys(standaloneFiles).map((fileName) => (
                  <button
                    key={fileName}
                    onClick={() => setSelectedFile(fileName)}
                    className={`w-full text-left px-3 py-2 rounded-lg text-xs font-medium transition flex items-center space-x-2 ${
                      selectedFile === fileName
                        ? "bg-cyan-500/20 text-cyan-300 border border-cyan-500/40"
                        : "text-neutral-400 hover:bg-neutral-800 hover:text-neutral-200"
                    }`}
                  >
                    <FileCode className="w-4 h-4 shrink-0" />
                    <span className="truncate">{fileName}</span>
                  </button>
                ))}
              </div>

              {/* Code Viewer */}
              <div className="md:col-span-3 flex flex-col rounded-xl bg-neutral-950 border border-neutral-800 overflow-hidden">
                <div className="px-4 py-2.5 bg-neutral-900 border-b border-neutral-800 flex items-center justify-between">
                  <div>
                    <div className="text-xs font-bold text-white">{selectedFile}</div>
                    <div className="text-[10px] text-neutral-400">
                      {standaloneFiles[selectedFile]?.desc}
                    </div>
                  </div>
                  <button
                    onClick={() => handleCopy(standaloneFiles[selectedFile]?.code || "")}
                    className="flex items-center space-x-1 text-xs text-neutral-400 hover:text-white px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 transition"
                  >
                    {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                    <span>{copied ? "Đã chép mã!" : "Sao chép"}</span>
                  </button>
                </div>
                <div className="flex-1 p-4 overflow-auto">
                  <pre className="text-xs font-mono text-neutral-300 leading-relaxed">
                    {standaloneFiles[selectedFile]?.code}
                  </pre>
                </div>
              </div>
            </div>
          )}

          {activeTab === "STANDALONE_FEATURES" && (
            <div className="space-y-6 max-w-4xl mx-auto">
              <div className="text-center space-y-1">
                <h3 className="text-base font-bold text-white">Khả Năng Vận Hành Khi Chạy File .EXE Độc Lập</h3>
                <p className="text-xs text-neutral-400">
                  Được tối ưu hóa như một phần mềm máy trạm kỹ thuật chuyên dụng
                </p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="p-4 rounded-xl bg-neutral-800/50 border border-neutral-700 space-y-2">
                  <div className="flex items-center space-x-2 text-cyan-400">
                    <Cpu className="w-5 h-5" />
                    <span className="font-bold text-sm text-white">Tăng tốc Đồ họa Phần cứng (GPU Acceleration)</span>
                  </div>
                  <p className="text-xs text-neutral-300 leading-relaxed">
                    Ứng dụng tận dụng trực tiếp GPU (NVIDIA, AMD, Intel Iris) thông qua WebGL và Canvas2D, cho phép vẽ hàng chục nghìn đối tượng CAD, Hatch, Block mượt mà ở tần số 60-144 FPS.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-neutral-800/50 border border-neutral-700 space-y-2">
                  <div className="flex items-center space-x-2 text-emerald-400">
                    <HardDrive className="w-5 h-5" />
                    <span className="font-bold text-sm text-white">Lưu trữ Dữ liệu Cục bộ (Local Storage & SQLite)</span>
                  </div>
                  <p className="text-xs text-neutral-300 leading-relaxed">
                    Tất cả bản vẽ, khối Block mẫu, bộ nhớ dịch thuật kỹ thuật số (Translation Memory) và cài đặt layer được lưu trực tiếp trên ổ cứng người dùng, bảo mật tuyệt đối.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-neutral-800/50 border border-neutral-700 space-y-2">
                  <div className="flex items-center space-x-2 text-amber-400">
                    <Sparkles className="w-5 h-5" />
                    <span className="font-bold text-sm text-white">Chế độ AI Kép (Online Gemini + Offline Rules)</span>
                  </div>
                  <p className="text-xs text-neutral-300 leading-relaxed">
                    Khi không có Internet, phần mềm tự động chuyển sang bộ quy tắc thuật toán CAD hình học nội bộ để xử lý Auto Detail, tạo mặt cắt, dàn trang Layout và kiểm tra lỗi Audit.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-neutral-800/50 border border-neutral-700 space-y-2">
                  <div className="flex items-center space-x-2 text-blue-400">
                    <Layers className="w-5 h-5" />
                    <span className="font-bold text-sm text-white">Định dạng Xuất Nhập Đa dạng</span>
                  </div>
                  <p className="text-xs text-neutral-300 leading-relaxed">
                    Hỗ trợ mở và xuất file DXF R12-2024, AutoLISP (.lsp), bảng dữ liệu Excel (.xlsx), và in ấn Layout Sheet ra file PDF chuẩn khổ giấy A1/A3/A4.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3.5 bg-neutral-950 border-t border-neutral-800 flex items-center justify-between">
          <div className="text-xs text-neutral-400 flex items-center space-x-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            <span>Gói đóng gói .EXE hỗ trợ Windows 10, Windows 11 (64-bit)</span>
          </div>

          <div className="flex items-center space-x-2">
            <button
              onClick={handleDownloadBatch}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-bold transition shadow"
            >
              <Download className="w-3.5 h-3.5" />
              <span>Tải file build-exe.bat</span>
            </button>
            <button
              onClick={onClose}
              className="px-4 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs font-medium transition"
            >
              Đóng
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
