# HNL CAD AI v2.3.0 — Continuous PLINE & CAD Save

## PL / PLINE
Đã sửa PLINE từ thao tác 2 điểm thành session nhiều đỉnh:
- `PL` / `PLINE` bắt đầu lệnh.
- Click chuột trái liên tiếp để thêm đỉnh.
- Dynamic Input cũng thêm đỉnh liên tục, không tự kết thúc ở điểm thứ 2.
- `Enter` hoặc `Space`: kết thúc polyline mở.
- `C`: Close, nối đỉnh cuối về đỉnh đầu và kết thúc.
- `U`: bỏ đỉnh vừa nhập.
- `Esc`: hủy lệnh, không tạo polyline.
- Chuột phải: kết thúc polyline mở.
- Preview hiển thị toàn bộ các đoạn đã nhập + đoạn rubber-band tới con trỏ.
- Lưu thành 1 entity POLYLINE/LWPOLYLINE, không phải nhiều LINE rời.

Chưa giả vờ parity hoàn toàn với AutoCAD:
- Arc/Bulge trong PLINE chưa có.
- Width/halfwidth chưa có.

## CAD Save
`Ctrl+S` giờ là **Save CAD DXF**, không còn mặc định lưu HNL JSON.
- DXF ASCII AC1015 / AutoCAD 2000, đơn vị mm.
- LINE, CIRCLE, RECTANGLE, LWPOLYLINE, TEXT, MTEXT được ghi trực tiếp.
- MLEADER và DIMENSION nội bộ được xuất dạng visual fallback tương thích DXF R2000.
- Mọi layer entity đang sử dụng được khai báo trong LAYER table.

`Ctrl+Shift+S`:
- Lưu Project HNL JSON để giữ metadata nội bộ, layout, smart object, dependency...

## DWG native
Khi AutoCAD Bridge Connected:
- File → **Lưu DWG native qua AutoCAD Bridge**
- HNL sinh DXF tạm.
- AutoCAD plugin dùng `Database.DxfIn()` trên database mới.
- AutoCAD plugin dùng `Database.SaveAs(outputPath, DwgVersion.Current)`.
- Đây là DWG do Autodesk database engine tạo, không phải đổi đuôi file.

Bridge action mới:
`SAVE_DXF_AS_DWG`

## File menu
- Ctrl+S — Lưu bản vẽ CAD DXF
- Ctrl+Shift+S — Lưu Project HNL JSON
- Save DWG native via AutoCAD Bridge
- Export DXF

## Lưu ý fidelity
DXF/DWG xuất từ Standalone chỉ bảo toàn các entity mà HNL geometry/export engine hiện hỗ trợ.
DWG native được Autodesk engine tạo khi Bridge connected, nhưng đối tượng HNL không có tương đương native hoàn chỉnh (ví dụ một số Smart Object) sẽ được xuất theo fallback DXF trước khi convert.
