# HNL CAD AI v2.7.1 — Managed Dynamic Block Library

## Đã nâng cấp
- Nạp 1 hoặc nhiều DWG cùng lúc.
- Nạp cả thư mục, quét recursive tối đa 8 cấp / 2000 DWG mỗi lượt.
- Mặc định COPY file vào kho HNL; có LINK file gốc/project/network.
- Library index v2 lưu trong Electron userData.
- SHA256 chống nạp trùng cùng file trong cùng Scope/Category.
- Scope: HNL Standard / Project Library / My Library.
- Category: Annotation / Ceiling / Wall / Steel / MEP / Detail / Custom.
- Search / Filter / Favorite / Recent.
- Hiển thị Layer + ACI Color + Linetype + Lineweight.
- Khi chèn gọi ENSURE_HNL_STANDARDS để bảo đảm layer đúng chuẩn.
- Reveal file trong Explorer, mở Library root, xóa managed copy an toàn.

## Dynamic Block native
Bridge mới:
- INSPECT_LIBRARY_DWG
- IMPORT_LIBRARY_DEFINITION

INSPECT_LIBRARY_DWG đọc side database bằng AutoCAD Database.ReadDwgFile và liệt kê named block definitions.
Dynamic flag được đọc reflection-safe để giảm rủi ro khác API giữa AutoCAD 2023–2026.

IMPORT_LIBRARY_DEFINITION dùng sourceDb.WblockCloneObjects vào BlockTable của DWG hiện tại.
Mục tiêu là giữ native block definition/attribute/dynamic behavior thay vì flatten thành entity HNL.

Sau khi chèn, nếu BlockReference.IsDynamicBlock:
- trả DynamicBlockReferencePropertyCollection,
- HNL hiện panel Dynamic Properties,
- SET_DYNAMIC_BLOCK_PROPERTIES cập nhật property native.

HNL không biến static block thành dynamic giả.
Built-in symbol fallback được ghi đúng là STATIC; dynamicPreferred chỉ là định hướng thay bằng DWG Dynamic Block thật.

## AutoCAD UI
- Ribbon HNL / Shopdrawing / Library -> HNLLIBRARY.
- HNLLIBRARY mở HNL EXE bằng --hnl-tool=LIBRARY.
- Electron single-instance nhận deep link và mở đúng Library Manager.
- HNLINSERT vẫn tồn tại dưới dạng Quick Insert trong Native Palette.

## File storage
COPY:
- nằm trong userData/library/<scope>/<category>/.
- duplicate filename nhưng khác nội dung thêm suffix SHA8.
- khi Remove, chỉ xóa managed file nếu file nằm trong managed root và không còn item khác dùng.

LINK:
- không copy.
- Remove chỉ xóa index, không xóa file nguồn.

## Chưa tuyên bố hoàn thành
- Thumbnail hình học DWG thật chưa render ở môi trường này; v2.7.1 dùng icon/category + màu layer.
- Chưa compile Autodesk .NET thật tại đây.
- Chưa runtime test WblockCloneObjects với mọi loại Dynamic Block AutoCAD 2023–2026.

GitHub Actions Windows = compile gate.
AutoCAD cài thực tế = runtime gate.
