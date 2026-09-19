# Cách dùng phần hoàn thiện v2.0.0

## AutoCAD Native Plot
1. Mở HNL EXE.
2. Mở AutoCAD full.
3. NETLOAD `Hnl.CadBridge.dll` build đúng version AutoCAD.
4. Gõ `HNLBRIDGESTATUS`.
5. HNL phải đổi trạng thái sang Connected trong vài giây.
6. Publish → Printer & Nét in → `Đọc cấu hình Plot Native`.
7. Kiểm tra Devices / CTB-STB / Layouts.
8. Publish → chọn sheet → `AutoCAD Native Publish → PDF`.

## Publish Queue Standalone
1. Publish → chọn sheet.
2. `Publish Queue → từng PDF`.
3. Chọn thư mục.
4. Theo dõi PENDING/RUNNING/DONE/FAILED.
5. Có thể `Cancel Queue`.

## DST Native
1. AutoCAD Bridge phải Connected.
2. Publish → Sheet Set → Mở Sheet Set.
3. Chọn `.dst`.
4. HNL đọc Sheet/Subsets qua AutoCAD.
5. Sửa Sheet No./Title.
6. Bấm `Apply DST Changes`.
7. Nếu lỗi lock, không thử ghi đè thủ công; xem Diagnostics.

## Recovery Restore
Professional Audit Center → Recovery → chọn generation → Restore.
Workspace hiện tại sẽ bị thay nên HNL luôn hỏi xác nhận.

## Revision
Professional Audit Center → Drawing Compare → mở revision cũ → nhập Revision/Mô tả → Lưu Revision → chuyển DRAFT/ISSUED.
