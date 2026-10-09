# HNL CAD AI v1.1.0 – Deep Audit

## Phạm vi
Audit từ ZIP gốc AI Studio và đối chiếu bản v1.0.1 Audited. Mục tiêu: không có nút báo thành công giả, tách rõ Standalone/AutoCAD native, giảm rủi ro kỹ thuật từ dữ liệu mẫu, và chuẩn bị build Windows.

## Sửa logic/chức năng
- 51/51 command được Ribbon gọi hiện đã có case xử lý trong App.
- Bổ sung xử lý thật/khả dụng trong Standalone cho: Polyline 2 điểm, Offset LINE, Join LINE liên tiếp, Text Uppercase, Trim Space, Find/Replace, Area/Perimeter thực, Block Count thực, Block similarity theo tên/attribute, Block library purge nội bộ, Attribute schema sync nội bộ, AI selection summary, light block count, Field scan nội bộ, title-block candidate recognition, viewport auto-fit, viewport grid arrange.
- Các lệnh cần geometry kernel/native AutoCAD (Trim/Extend/Fillet/Array/Hatch, DWG Field native, DWG folder scan...) không còn báo thành công giả; UI nói rõ cần plugin/engine tiếp theo.
- MLeader Align trước đây chỉ toast, nay thực sự cập nhật vị trí text của các MLeader.
- Ghi diện tích phòng trước đây hard-code 32.40m²; nay tính từ Polyline kín/Rectangle đang chọn.
- Tổng diện tích/chu vi trước đây hard-code; nay tính từ hình học thật.
- Block similarity/count trước đây hard-code; nay tính từ dữ liệu hiện tại.
- Smart Ceiling trước đây tự sinh vùng 6000x4000; nay bắt buộc chọn Polyline kín/Rectangle làm boundary.
- Settings trước đây bấm Lưu nhưng không lưu; nay lưu localStorage và Smart Ceiling đọc spacing đã lưu.
- AI executor không có handler trước đây vẫn báo “đã thực thi”; nay không thay đổi dữ liệu và cảnh báo executor chưa hỗ trợ.

## Sửa lỗi tích hợp
- Sửa mismatch prop của HnlSectionGeneratorModal (`onInsertSectionToModel` vs `onInsertToCanvas`).
- Nút “Chèn vào bản vẽ CAD” của Section Generator trước đây chỉ alert; nay sinh LINE/TEXT nội bộ và gọi callback thật.
- Kiểm tra toàn bộ menu Electron: 12/12 menu-command đều có handler phía React.
- Electron About dùng app.getVersion() thay version hard-code.
- Vite config đổi `__dirname` sang ESM-safe `fileURLToPath(import.meta.url)`.
- Bỏ webPreference `hardwareAcceleration` không phù hợp vị trí cấu hình BrowserWindow.

## An toàn kỹ thuật xây dựng
- Loại các mã Test Report giả/seed khỏi trạng thái VERIFIED.
- Fire-rated assemblies mẫu chuyển sang NEEDS_CONFIRMATION / REQUIRES_PROJECT_SUBMITTAL.
- Shop Check không còn khẳng định một cấu tạo EI60 đã có chứng thư IBST giả.
- Property Editor không còn hiển thị VERIFIED khi chưa gắn Approved System/Test Report dự án.
- Building Code có cảnh báo rõ dữ liệu hiện là seed/demo; không dùng để nghiệm thu/kết luận EI/STC/tải trọng.
- Hàm tính ty treo demo không còn trả `isCompliantASTM=true`; bắt buộc engineering verification.
- MLeader vật liệu không còn tự chèn cấu tạo EI60 cụ thể như thể đã được duyệt.

## Giao diện
- Giảm min/max width panel trái/phải để Canvas còn không gian tốt hơn ở laptop 1366x768.
- Giữ top bar/ribbon cuộn ngang an toàn; phân biệt rõ Standalone và AutoCAD 2023+ plugin riêng.
- Audit Modal không còn ghi “đạt 100% tiêu chuẩn”; chỉ nói không còn lỗi trong phạm vi rule đang bật.

## Build Windows
- Version: 1.1.0.
- NSIS x64, per-machine, Desktop shortcut, Start Menu shortcut, icon HNL.
- Output dự kiến: `HNL_CAD_AI_Setup_1.1.0.exe`.
- GitHub Actions Windows workflow có sẵn.

## Kiểm tra tĩnh đã chạy
- 51/51 Ribbon commands có dispatcher case.
- 12/12 Electron menu commands có React handler.
- `node --check electron/main.cjs`: PASS.
- `node --check electron/preload.cjs`: PASS.
- package.json / metadata.json: JSON hợp lệ.
- `tsc --noEmit`: không còn lỗi TypeScript logic phát hiện được sau khi loại nhóm lỗi do môi trường hiện tại chưa có node_modules/@types React/Node.
- Không còn chuỗi thương hiệu An Phu/An Phú trong source chính.

## Chưa thể coi là hoàn tất/native
- DWG binary native: cần AutoCAD API hoặc SDK DWG hợp lệ.
- AutoCAD Table/Field/Layout/Viewport native: cần build plugin .NET AutoCAD.
- Chạy AutoLISP thật: cần AutoCAD host/plugin.
- Trim/Extend/Fillet/Array/Hatch Standalone: cần geometry kernel hoàn thiện.
- AI online trong EXE: endpoint Gemini hiện yêu cầu GEMINI_API_KEY runtime; Custom/Ollama chưa nối endpoint.
- Tiêu chuẩn/certification: production phải nạp tài liệu gốc hiện hành và Approved Project Documents; seed demo không phải nguồn thiết kế.
