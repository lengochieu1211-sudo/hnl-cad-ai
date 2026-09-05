# BUILD HNL CAD AI v2.8.2 thanh .EXE dung duoc voi AutoCAD

1. Cai Node.js, .NET SDK 8.x va .NET SDK 10.x tren may build. AutoCAD DLL 2023/2024 build `net48`, AutoCAD DLL 2025/2026 build `net8.0-windows`, AutoCAD DLL 2027 build `net10.0-windows`.
2. Chay `npm install`.
3. Kiem tra version: `node scripts/check-version-sync.mjs`.
4. Build AutoCAD bundle: `npm run build:autocad`.
5. Build installer: `npm run dist:win`.

Ket qua:

- Installer Windows: `dist_electron\HNL_CAD_AI_Setup_2.8.2.exe`.
- AutoCAD bundle: `autocad-plugin\HNL.CadBridge.bundle`.
- GitHub artifact installer: `HNL-CAD-AI-v2.8.2-Windows-Installer`.
- GitHub artifact AutoCAD bundle: `HNL-CAD-AI-v2.8.2-AutoCAD-Plugin-Bundle`.

Luu y CAD:

- Bundle phai co `Contents\2023`, `Contents\2024`, `Contents\2025`, `Contents\2026`, `Contents\2027`, moi thu muc co `Hnl.CadBridge.dll`.
- Bundle phai co `Contents\Lisp` voi dung 44 file `.lsp`; khong dong goi ARX cu cho AutoCAD 2023-2027.
- Installer se copy bundle vao `%APPDATA%\Autodesk\ApplicationPlugins\HNL.CadBridge.bundle` cho user hien tai.
- Neu AutoCAD dang mo khi cai/cap nhat plugin, dong tat ca cua so AutoCAD roi mo lai de AutoLoader nap DLL moi.
- Khi smoke test bang AutoCAD Core Console 2023 voi `NETLOAD`, can dat tam `SECURELOAD=0` hoac them bundle vao trusted paths; duong cai that van la AutoLoader tu `ApplicationPlugins`.

Khong tao installer CAD neu chua build duoc AutoCAD bundle. Viec nay tranh truong hop app Electron chay duoc nhung AutoCAD khong nap duoc plugin.
