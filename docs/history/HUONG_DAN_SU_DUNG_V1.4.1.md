# HNL CAD AI v1.4.1 — Hướng dẫn sử dụng & báo lỗi

## 1. Khởi động
- **Continue Workspace**: tiếp tục dữ liệu Recovery/AutoSave gần nhất.
- **Open Project/DXF**: mở `.json` dự án HNL hoặc DXF ASCII.
- **New Drawing**: tạo dự án mới sạch. Nếu file hiện tại chưa lưu, ứng dụng phải cảnh báo.
- Dấu **●** cạnh tên file = còn thay đổi chưa lưu.

## 2. Quy trình làm việc khuyến nghị
1. Chọn **Workbench** phù hợp.
2. Tạo/mở bản vẽ.
3. Kiểm tra đơn vị, Layer, Layout và thông số Spreadsheet.
4. Vẽ/chọn đối tượng trên Canvas.
5. Dùng Ribbon theo nhóm: Vẽ & Chỉnh sửa → Ghi chú & Field → Dữ liệu & Thống kê → Layout & Viewport → AI & Automation → Kỹ thuật.
6. Lưu thường xuyên. Trước khi xuất BOQ/Excel/PDF phải kiểm tra đơn vị và Project Spec.

## 3. Standalone và AutoCAD
- **Standalone**: CAD Engine nội bộ, HNL JSON, DXF ASCII, Smart Objects, Spreadsheet, BOQ, AI và các công cụ nội bộ.
- **AutoCAD Bridge Connected**: dùng cho DWG native, AutoCAD ObjectId/Transaction, AutoLISP, Field/Table/Layout/Viewport native và các lệnh cần AutoCAD API.
- Nếu một nút cần AutoCAD nhưng Bridge chưa kết nối, ứng dụng phải **báo rõ điều kiện thiếu**, không báo thành công giả.

## 4. Khi một chức năng chạy không được
1. Không đóng app ngay nếu vẫn còn phản hồi.
2. Mở **Công cụ → Trung tâm chẩn đoán lỗi**.
3. Chọn lỗi mới nhất.
4. Ghi nhận: **Mã lỗi, Command, Message, Cause, Context, Suggestion**.
5. Bấm **Copy báo cáo** hoặc **Xuất TXT**.
6. Gửi báo cáo cùng ảnh màn hình và file mẫu nếu lỗi phụ thuộc dữ liệu.

### Mẫu thông tin cần gửi
- Mã lỗi: `HNL-...`
- Nút/lệnh vừa dùng:
- Các bước trước khi lỗi:
- Kết quả mong muốn:
- Kết quả thực tế:
- Diagnostic Report:
- File mẫu/ảnh màn hình nếu có:

## 5. Nhóm mã lỗi
- `HNL-CMD-*`: lệnh/ribbon không chạy hoặc chưa hỗ trợ.
- `HNL-CMD-ERR-*`: exception khi thực thi command.
- `HNL-FILE-OPEN-*`: lỗi đọc/migrate/import file.
- `HNL-FILE-SAVE-*`: lỗi lưu file/quyền ghi/đường dẫn.
- `HNL-RUNTIME-*`: lỗi JavaScript UI.
- `HNL-ASYNC-*`: Promise/API/AI/tác vụ nền thất bại.

## 6. Nguyên tắc an toàn
- Không xóa Diagnostic Log trước khi copy.
- Không ghi đè file dự án duy nhất sau khi vừa gặp lỗi dữ liệu; ưu tiên Save As file mới.
- AI không thay thế kiểm tra thiết kế/kỹ thuật và tiêu chuẩn dự án.
- Các preset thạch cao/cọc/kỹ thuật phải đối chiếu Project Spec/Approved System/tiêu chuẩn áp dụng.
