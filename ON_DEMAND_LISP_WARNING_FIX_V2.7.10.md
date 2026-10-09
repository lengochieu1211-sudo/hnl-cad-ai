# HNL CAD AI v2.7.10 — On-Demand Lisp + Build Warning Cleanup

## 44 Lisp
Mặc định đổi từ AutoLoad-All sang ON_DEMAND.

- 44 Lisp vẫn nằm sẵn trong installer/plugin bundle.
- Không LOAD cả 44 khi AutoCAD khởi động.
- `Nạp + Chạy` chỉ LOAD đúng file Lisp cần dùng.
- `HNLLISPRELOAD` vẫn cho phép chủ động nạp toàn bộ.
- `HNLLISPAUTOON` bật preload-all cho phiên hiện tại nếu thật sự cần.
- `HNLLISPAUTOOFF` trả về ON_DEMAND.

=> giảm thời gian khởi động, RAM/namespace pollution và rủi ro Lisp cũ/DCL/COM.

## GitHub Actions Node warning
Đổi:
- actions/checkout v4 -> v5
- actions/setup-node v4 -> v5
- actions/setup-dotnet v4 -> v5
- actions/upload-artifact v4 -> v6

Các major mới chạy action runtime Node.js 24. Project Node vẫn là 22.

## C# nullable warnings
### BridgeCommands GetHnlLayerProfile
Không còn gán `name` có thể null vào `HnlLayerProfile.Name`.
Chuẩn hóa:
`normalizedName = (name ?? "").Trim()`
rồi dùng `Name=normalizedName`.

### NativeRibbon classic menu
Không còn:
`dynamic hnl = null`
Thay bằng `object? hnlObject`, kiểm tra null rõ ràng rồi mới chuyển sang dynamic.

## Lưu ý
Các annotation người dùng gửi là warning, không phải compiler error. Bản này dọn warning để log build sạch hơn.
