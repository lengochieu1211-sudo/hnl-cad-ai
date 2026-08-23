// Electron Preload Script
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronNative', {
  isElectron: true,
  platform: process.platform,
  saveFile: (options) => ipcRenderer.invoke('save-file-dialog', options),
  requestOpenFile: (mode = 'AUTO') => ipcRenderer.invoke('open-file-dialog', mode),
  openExternal: (url) => ipcRenderer.invoke('open-external-url', url),
  selectApprovedDocument: () => ipcRenderer.invoke('select-approved-document'),
  selectLibraryDwg: () => ipcRenderer.invoke('select-library-dwg'),
  readTextFile: (filePath) => ipcRenderer.invoke('read-text-file', filePath),
  getVersion: () => ipcRenderer.invoke('get-app-version'),
  installAutoCadBundle: () => ipcRenderer.invoke('install-autocad-bundle'),
  getAutoCadBundleStatus: () => ipcRenderer.invoke('get-autocad-bundle-status'),
  openAutoCadPluginFolder: () => ipcRenderer.invoke('open-autocad-plugin-folder'),
  sessionToken: process.env.HNL_API_TOKEN || '',
  setDirty: (value) => ipcRenderer.invoke('set-renderer-dirty', value),
  setWindowTitle: (title) => ipcRenderer.invoke('set-window-title', title),
  saveAIKey: (key) => ipcRenderer.invoke('save-ai-key', key),
  getAIKeyStatus: () => ipcRenderer.invoke('get-ai-key-status'),
  getAIProviderConfig: () => ipcRenderer.invoke('get-ai-provider-config'),
  saveAIProviderConfig: (config) => ipcRenderer.invoke('save-ai-provider-config', config),
  getPrinters: () => ipcRenderer.invoke('get-printers'),
  renderPdfFromHtml: (options) => ipcRenderer.invoke('render-pdf-from-html', options),
  printHtml: (options) => ipcRenderer.invoke('print-html', options),
  openSheetSet: () => ipcRenderer.invoke('open-sheetset-dialog'),
  chooseOutputFolder: () => ipcRenderer.invoke('choose-output-folder'),
  chooseSavePath: (options) => ipcRenderer.invoke('choose-save-path', options),
  writeTempTextFile: (options) => ipcRenderer.invoke('write-temp-text-file', options),
  renderPdfToPath: (options) => ipcRenderer.invoke('render-pdf-to-path', options),
  onMenuCommand: (callback) => {
    const listener = (_event, command) => callback(command);
    ipcRenderer.on('menu-command', listener);
    return () => ipcRenderer.removeListener('menu-command', listener);
  },
  onFileOpened: (callback) => {
    const listener = (_event, data) => callback(data);
    ipcRenderer.on('file-opened', listener);
    return () => ipcRenderer.removeListener('file-opened', listener);
  },
});
