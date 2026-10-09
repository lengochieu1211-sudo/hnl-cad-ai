# HNL CAD AI v2.4.2 — AutoCAD Plugin CI Build Fix

Log: `logs_88295946182.zip`

## Nguyên nhân
Cả 4 project AutoCAD 2023–2026 đều fail tại `NativePalette.cs` do tên kiểu mơ hồ:
- `Application`: Autodesk AutoCAD vs Windows Forms.
- `Exception`: Autodesk Runtime vs System.

Riêng AutoCAD 2023–2024 (net48) còn lỗi `CS0656` tại Sheet Set COM `dynamic` vì thiếu framework reference `Microsoft.CSharp`.

## Đã sửa
1. `NativePalette.cs`
   - dùng `AcApplication = Autodesk.AutoCAD.ApplicationServices.Application`.
   - dùng `SysException = System.Exception`.
   - không còn gọi tên `Application` / `Exception` mơ hồ.

2. AutoCAD 2023–2024
   - thêm:
     `<Reference Include="Microsoft.CSharp"><Private>false</Private></Reference>`
   - giữ `Private=false` để không copy framework DLL vào bundle AutoCAD.

3. Sheet Set cleanup
   - dùng dynamic local non-null trong `finally` để giảm cảnh báo nullable.

4. Đồng bộ version
   - HNL Desktop / Server / AutoCAD Bridge / Bundle / workflow / SketchUp Bridge → 2.4.2.

## Kiểm tra tại môi trường hiện tại
- 4 `.csproj` + `PackageContents.xml`: XML parse PASS.
- `NativePalette.cs`: không còn bare ambiguous Application/Exception.
- AutoCAD 2023/2024: Microsoft.CSharp reference PASS.
- Electron `main.cjs` / `preload.cjs`: syntax PASS.
- toàn bộ TS/TSX: syntax transpile PASS.
- ZIP/RBZ: integrity PASS.

## Giới hạn
Máy tạo patch không cài .NET SDK + AutoCAD SDK runtime nên không compile DLL thực tế tại đây. GitHub Actions Windows là bước xác nhận compile kế tiếp.
