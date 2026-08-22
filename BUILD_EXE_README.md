# BUILD HNL CAD AI v2.0.2 thành .EXE

## Cách nhanh nhất trên Windows 10/11
1. Giải nén ZIP này vào đường dẫn ngắn, ví dụ `D:\HNL-CAD-AI`.
2. Máy phải có Internet ở lần build đầu.
3. Cài Node.js 22 LTS x64.
4. Nhấp đúp `BUILD_EXE_NOW.bat`.
5. Khi thành công, installer nằm trong:
   `dist_electron\HNL_CAD_AI_Setup_2.0.2.exe`

Script tự chạy:
- `npm install`
- `npm run lint`
- `npm run build`
- `electron-builder --win nsis --x64`
- kiểm tra EXE > 5 MB
- in SHA256

## GitHub Actions
Workflow `.github/workflows/build-windows.yml` chạy trên `windows-latest`.
Sau khi push source lên GitHub:
- Actions → Build HNL CAD AI Windows EXE → Run workflow
- Tải artifact `HNL-CAD-AI-v2.0.2-Windows-Installer`.

## Vì sao phiên ChatGPT này chưa tạo được EXE thật
Môi trường build hiện tại không resolve được `registry.npmjs.org`, không có `node_modules`,
không có Wine/NSIS và GitHub connector hiện không có repository được cấp quyền.
Vì Electron cần tải dependencies/runtime, không thể tạo installer hợp lệ mà không có các thành phần đó.
