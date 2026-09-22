# HNL VXT Pro — Continuation Checkpoint — 2026-09-05

## 1. Nguồn tiếp tục bắt buộc
- Repository: `lengochieu1211-sudo/hnl-cad-ai`
- Branch: `research/vxt-pro-v7-bootstrap`
- Draft PR: `#1`
- Không merge `main` khi chưa có yêu cầu rõ ràng của người dùng.
- Không rollback về alpha cũ.
- Golden baseline: AutoLISP V6.7.4 của Vẽ Xương Trần.
- Phiên bản C# đang phát triển: `HNL Tool - VXT Pro v7.0.0-beta.1`.
- FileVersion mục tiêu: `7.0.0.6`.

## 2. Mục tiêu sản phẩm
Hoàn thiện plugin C#/WPF cho AutoCAD 2023–2027, cài bằng một Universal Setup EXE, không cần NETLOAD/APPLOAD thủ công. Preview và Create phải dùng cùng engine, kết quả tạo thật phải bám Golden V6.7.4.

## 3. Chức năng đã đưa vào source beta.1
- WPF Palette modeless, giữ mở khi chọn CAD.
- XC / XP / Ty treo.
- Hướng bố trí XC, kiểu bố trí, Min/Max tâm, Min/Max biên, bỏ XC dưới ngưỡng, bước bù.
- XP có khoảng cách và hướng rải từng vùng theo logic Golden.
- Ty có cân hai đầu / dồn theo XP.
- Né thiết bị, ưu tiên dịch cả hàng, clearance.
- Chia vùng HCN.
- Block động, chọn Block mẫu trực tiếp từ CAD.
- Layer / ACI / Linetype / Lineweight cho XC / XP / Ty / DIM.
- DimStyle đọc từ bản vẽ; DIM dùng RotatedDimension thật.
- Preview WYSIWYG dùng Block/Layer/DIM thật hoặc fallback theo cấu hình.
- NumericBox hỗ trợ biểu thức tính trực tiếp: `1220/3`, `(1200-100)/2`, `2*450`, `1200:3`, v.v.
- Nút Xuất lỗi/chẩn đoán.
- Tùy biến giao diện: theme, màu nhấn, màu chữ, cỡ chữ, khoảng dòng, khoảng cột, khoảng khối; lưu cấu hình.
- Create engine thật với transaction/rollback.
- Lệnh `VXT` và `VXTCREATE`.
- Nút TẠO KHUNG XƯƠNG đã mở khi biên/thông số hợp lệ.

## 4. Gate đã PASS
Run matrix gần nhất đã xác nhận:
- Core / Golden / DIM / Numeric Expression Tests: PASS.
- 34/34 Core tests: PASS.
- AutoCAD 2023 / NET48: PASS.
- AutoCAD 2024 / NET48: PASS.
- AutoCAD 2025 / NET8: PASS.
- AutoCAD 2026 pre-1.2 / NET8: PASS.
- AutoCAD 2026.1.2+ / NET10: PASS.
- AutoCAD 2027 / NET10: PASS.

Run chứng nhận gần nhất trước backup: `33895927336`.

## 5. Lỗi đang dang dở duy nhất trước khi đóng Universal beta.1
Universal Setup đang fail ở bước tạo branding/icon:
- `generate-installer-branding.ps1`
- PNG Base64 cũ có header PNG nhưng Windows WPF/System.Drawing báo image format unrecognized.
- Engine và 6 bridge không fail.
- Không được dùng EXE beta.1 nào trước khi branding gate PASS.

Hướng tiếp tục đã chốt:
1. Thay asset logo bằng PNG RGBA chuẩn được tạo từ logo HNL gốc người dùng gửi.
2. Dùng cùng một asset cho Palette và ICO installer.
3. ICO đa kích thước: 256/128/64/48/32/24/16, 256px frame đầu tiên.
4. Chạy lại branding decode gate.
5. Chạy lại Golden/Core + 6 bridge.
6. Đóng Universal Setup beta.1.
7. Tải artifact và kiểm trực tiếp FileVersion, SHA256, icon bên trong EXE rồi mới giao người dùng.

## 6. Các lỗi UI người dùng đã báo — phải kiểm lại runtime
- Logo Palette từng bị crop/mất phần dưới.
- Icon EXE từng méo/cũ.
- ComboBox dropdown từng trắng chữ/trắng nền, chọn xong không thấy giá trị.
- Title bar từng giữ alpha.1, footer từng giữ Alpha.2 dù header đã alpha mới.
- Khoảng cách dòng/cột từng quá rộng; mặc định phải gọn hơn.
- DIM và Layer/kiểu nét phải hiện đầy đủ như LISP V6.7.4.

## 7. Câu chữ UI đã thống nhất
Không viết quá ngắn, hạn chế viết tắt. Giữ thuật ngữ CAD quen thuộc như Block, Layer, DIM, DimStyle.
Ví dụ:
- `Hướng bố trí`
- `Kiểu bố trí`
- `Khoảng cách tâm Min / Max`
- `Khoảng cách biên Min / Max`
- `Bỏ Xương chính nếu ngắn hơn`
- `Bù khoảng cách tự động`
- `Sử dụng Block động`
- `Chọn hướng cho từng vùng`
- `Tự động né thiết bị`
- `Ưu tiên dịch cả hàng`
- `Khoảng hở né thiết bị`
- `Khoảng cách DIM`
- `Khoảng cách giữa các hàng DIM`

## 8. Quy tắc AutoLISP/DCL HNL Tool nếu còn chỉnh LISP
- Branding `HNL Tool`.
- Chuỗi tiếng Việt trong runtime LISP bắt buộc dùng Unicode Hex `\U+XXXX` một slash trong mã LISP runtime.
- Chuỗi DCL ghi bằng `write-line` dùng `\\U+XXXX` trong source LISP để file DCL nhận `\U+XXXX`.
- Không gõ trực tiếp tiếng Việt có dấu trong string LISP.
- CAD TABLE phải decode HEX thành Unicode thật trong RAM trước khi ghi bảng.
- DCL dùng boxed_column/boxed_row, spacer, width/edit_width thẳng cột, errtile, default/cancel, ghi version HNL Tool.

## 9. Chính sách phát hành
- Không phát hành thêm bản chỉ sửa giao diện.
- Bản tiếp theo phải là bản test chức năng thật.
- Không merge `main`.
- CI compile/package không được gọi là runtime AutoCAD chứng nhận.
- Chỉ nói runtime PASS khi có bằng chứng từ AutoCAD thật.

## 10. Drive backup
Backup của checkpoint này được lưu trong thư mục Drive:
`HNL CAD AI / HNL VXT Pro - Continuation Backup 2026-09-05 - beta.1 WIP`

Chat mới phải đọc file checkpoint này trước, lấy FULL SOURCE trong cùng thư mục Drive làm nền, rồi tiếp tục đúng mục 5. Không rollback.
