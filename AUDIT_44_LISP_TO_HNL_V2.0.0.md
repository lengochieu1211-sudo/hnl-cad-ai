# Audit tính năng AI.zip → HNL CAD AI

Đã kiểm tra **44 file AutoLISP**. Mục tiêu: dùng hành vi/workflow của Lisp làm đặc tả chức năng cho HNL CAD AI, ưu tiên viết lại native/hybrid thay vì chỉ nhúng Lisp.

## Nguyên tắc tích hợp

- **Native HNL**: hình học 2D, selection, text, thống kê, cleanup, formula, layer mapping, UI/preview.
- **Hybrid**: HNL xử lý logic/UI nhưng khi làm việc với DWG native sẽ gọi AutoCAD Bridge.
- **AutoCAD Bridge**: Field ObjectID, Block Editor, CHSPACE, Page Setup, VP Freeze, CTB/STB, Layout/DWG native.
- **Không copy mù code Lisp**: một số file có nguồn/đoạn chỉnh từ tác giả bên ngoài; dùng làm tham khảo hành vi, chỉ tái sử dụng mã khi giấy phép cho phép.

## Danh mục 44 Lisp

| # | Lisp | Lệnh | Chức năng nên đưa vào HNL | Cách triển khai | Ưu tiên |
|---:|---|---|---|---|---|
| 1 | **Block - Thong ke Block Att, Dynamic New 8.lsp** | `ATL/ATK/AT1/DY1/ATC/NDC/BLC` | Thống kê Block Attribute/Dynamic, đếm theo Tag/Visibility, tạo AutoCAD Table/Field, minh họa block. | **Hybrid** | **P0** |
| 2 | **Dim - Thống kê Dim Bán kính và Dim Arc_TKD.lsp** | `TKD` | Ghép Dim bán kính với Dim Arc gần nhất và tạo bảng thống kê. | **Native HNL + Bridge** | **P2** |
| 3 | **Block - AddObjectsToBlockV1-2_ATB New 11_FixLag_All.lsp** | `ATB/ADDTOBLOCK` | Thêm đối tượng vào block, xử lý nested/dynamic/xref, ma trận biến đổi, tối ưu cache. | **AutoCAD Bridge** | **P1** |
| 4 | **Text - Copy Text, Att, Dynamic v1.01b_QQ New 39.lsp** | `QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT` | Bộ công cụ copy/sửa Text, clipboard, nhập Excel, copy Text→Attribute, swap, copy thuộc tính Dynamic/Scale/Rotation, đổi hoa/thường/title. | **Native HNL + Bridge** | **P0** |
| 5 | **Layout - Ms2psV1-0_Copy, Move Model Sang Layout Nhanh Trong Viewport New.lsp** | `MS2PS` | Copy/Move Model sang Paper Space bằng CHSPACE, tự giữ scale/rotation/vị trí viewport. | **AutoCAD Bridge** | **P1** |
| 6 | **Block - Copy Block Att_APA 5.lsp** | `APA` | Công thức Attribute theo Tag, copy hoặc replace hàng loạt, precision và kiểm tra công thức. | **Native HNL + Bridge** | **P0** |
| 7 | **Layout- Copy Layout và Model_CLM 3.lsp** | `CLM` | Copy Layout kèm geometry/model liên quan. | **AutoCAD Bridge** | **P2** |
| 8 | **Layout - Rename Layout List_RNL - New 8.lsp** | `RNL` | Quản lý danh sách Layout, reorder, A-Z/Z-A, rename đơn, đánh số hàng loạt, prefix/suffix, find/replace, đánh dấu xóa. | **Native HNL + Bridge** | **P0** |
| 9 | **Block_BFM Chọn block theo tùy chọn trong vùng poline.lsp** | `BFM` | Chọn block nằm trong polyline, hỗ trợ boundary có bulge và lọc nội dung block. | **Native HNL** | **P1** |
| 10 | **Text - Change Text Style 8.lsp** | `CHANGETEXTSTYLE` | Đổi TextStyle và chuyển mã Unicode/VNI/TCVN3 cho Text/MText/Attribute/MLeader/Dim/Table, bảo toàn format. | **Native HNL + Bridge** | **P0** |
| 11 | **Polyline - Chuyển circle sang polyline_C2P.lsp** | `C2P` | Circle → closed polyline hai cung, có tùy chọn giữ/xóa circle gốc. | **Native HNL** | **P2** |
| 12 | **Polyline - Offset Field Text_OFT 7.lsp** | `OFT` | Offset geometry nhưng cập nhật sâu ObjectID của Field để không hỏng liên kết. | **AutoCAD Bridge** | **P1** |
| 13 | **Dim - Join Dim_JD.lsp** | `JD` | Gộp các Dim cùng phương thành một Dim tổng theo min/max. | **Native HNL + Bridge** | **P1** |
| 14 | **Layout - TaoKhung_Tạo Viewport Từ Model.lsp** | `TAOKHUNG` | Tạo viewport từ vùng Model, fit/scale, zoom đúng vùng và khóa viewport. | **Native HNL + Bridge** | **P0** |
| 15 | **Block - TCD_Trích chi tiết trên model và layout 7.lsp** | `TCD` | Trích chi tiết theo khung có sẵn/rectangle/circle, tên chi tiết, tỷ lệ, MLeader/Text style, dùng Model hoặc Layout. | **Hybrid/Bridge** | **P1** |
| 16 | **Text - Find Replace Multiple_FRM.lsp** | `FRM` | Find/Replace nhiều cặp cùng lúc trên Text/MText/Attribute/Block, không phân biệt hoa/thường; scope All/Model/Layout/Selection. | **Native HNL + Bridge** | **P0** |
| 17 | **Layer_Tạo Layer từ Excel_TLE 3.lsp** | `TLE` | Tạo/cập nhật Layer hàng loạt từ Excel, gồm màu/linetype/lineweight/plot. | **Native HNL + Bridge** | **P0** |
| 18 | **Int_eng_Đánh số các điểm giao nhau.lsp** | `INT` | Tìm giao điểm hai nhóm trục và chèn block đánh số tại giao điểm. | **Native HNL** | **P2** |
| 19 | **Vẽ Xương Trần V6.7.2_VXT.lsp** | `VXT` | Sinh xương chính/phụ/ty treo, né thiết bị, dim tự động, tùy layer/color/linetype/lineweight/dimstyle, nhiều mode bố trí. | **Native HNL + Bridge** | **P0** |
| 20 | **Text - Translate Text, Mtext_TransCad 17.lsp** | `TRANSCAD` | Dịch Text/MText/Block/MLeader/Table/Dim; Replace/Bilingual/Update/Remove; OpenAI/Gemini/Google; auto Unicode/VNI/TCVN3; glossary kỹ thuật. | **Native HNL + AI** | **P0** |
| 21 | **Field - CFM, CFA, CFE, CFL, CFS (Check Field Duplicate, Errors, Links) 12.lsp** | `CFM/APFIELD/CFE/CFA/CFL/CFS` | Field Manager: Field lỗi, tham chiếu trùng, geometry chưa có Field, lọc theo scope/type và hợp nhất kết quả. | **Hybrid/AutoCAD Bridge** | **P0** |
| 22 | **Layout - Viewport_APT Khóa hoặc mở khóa Viewport - New 2.lsp** | `APT` | Lock/Unlock Viewport hàng loạt xuyên Layout. | **Native HNL + Bridge** | **P1** |
| 23 | **Geomprops_TKT Tính tổng diện tích, chu vi, xuất bảng - 87.lsp** | `TKT` | Bóc tách Area/Length/Volume, group Layer, sort đa cột, custom columns, Field-linked CAD table, Block Attribute output, Excel export, import/export cấu hình. | **Native HNL + Bridge** | **P0** |
| 24 | **Layout - CPV1, CPV2_Sao chép VP freeze xuyên layout và bản vẽ.lsp** | `CVP1/CVP2` | Copy/Paste trạng thái VP Freeze giữa viewport/layout/bản vẽ. | **AutoCAD Bridge** | **P1** |
| 25 | **Polyline - Offset Nhanh Polyline_MOF.lsp** | `MOF` | Offset hàng loạt nhiều polyline theo một hướng chung; closed polyline dùng area để chọn đúng phía. | **Native HNL** | **P0** |
| 26 | **Polyline - Polyline Break_BRK_V5.0.lsp** | `BRK` | Break nhiều điểm, xóa đoạn, xử lý closed curves, giữ/tách Field, thay đúng ObjectID, transaction rollback. | **Native Geometry + AutoCAD Field Bridge** | **P0** |
| 27 | **Text - Calculator Text_APC, APS, APE 6.lsp** | `APC/APS/APE` | Tính toán Text theo nhiều biến/công thức, áp kết quả vào Text/Attribute. | **Native HNL** | **P1** |
| 28 | **Layout - DMBV tạo bảng từ text, mtext từ layout New 10.lsp** | `DMBV` | Lấy Text/MText từ Layout, làm sạch MText/Unicode, tạo bảng danh mục bản vẽ, preview/sort/export Excel. | **Native HNL + Bridge** | **P0** |
| 29 | **Field - CalculateAreaPerimeter_Tổng diện tích, chu vi.lsp** | `CALCULATEAREAPERIMETER` | Tổng Area/Perimeter nhiều loại geometry, highlight và copy clipboard dạng Excel. | **Native HNL** | **P0** |
| 30 | **Field - LengthAreaFieldV1-4 AF,LF,BF Tổng diện tích, chu vi, block New 6.lsp** | `AF/AFM/LF/LFM/BF` | Tạo Field tổng Length/Area/Block Count và chèn vào Text/MText/Table/Attribute/MLeader. | **Hybrid/AutoCAD Bridge** | **P0** |
| 31 | **Block-V2_RBL1 Thay thế block.lsp** | `RBL1` | Thay hàng loạt block bằng block mẫu. | **Native HNL + Bridge** | **P1** |
| 32 | **Polyline - Polyline To 2 Polyline_APTD 6.lsp** | `APTD` | Chuyển Line/Polyline/Spline có bề rộng thành 2 đường song song; join, region cleanup, xử lý góc/đầu mút. | **Native HNL** | **P1** |
| 33 | **Layout - Page setup For All Layouts_SAP Áp Page setup Layout New.lsp** | `SAP` | Áp Page Setup cho nhiều Layout. | **Native HNL config + AutoCAD Bridge** | **P0** |
| 34 | **Block - Edit Block_QB Vào, BS Lưu, BQ Hủy, BN Đổi tên (New).lsp** | `QB/BS/BQ/BN` | Vào Block Editor, Save/Cancel, Rename nhanh. | **AutoCAD Bridge** | **P2** |
| 35 | **Cover Lsp to Fas, Vxl_LTFV.lsp** | `LTFV` | Compile Lisp sang FAS/VLX, đóng gói/bảo vệ Lisp. | **AutoCAD/Lisp Tooling** | **P3** |
| 36 | **Layout_LL Làm nhẹ Layout_BT Mở lại giá trị bình thường.lsp** | `LL/BT` | Chế độ Layout nhẹ, giảm viewport hiển thị rồi khôi phục. | **AutoCAD Bridge** | **P1** |
| 37 | **IPMX_Chèn Point Điểm Giao 2 Polyline (Có đánh số).lsp** | `IPMX` | Tìm giao điểm hai polyline, sort theo X, tạo POINT + TEXT đánh số. | **Native HNL** | **P2** |
| 38 | **Demtc_Tính tấm trần nổi 2.lsp** | `DEMTC` | Tính tấm trần nổi 600/610 và 1200/1220, xử lý hatch/boundary, mô phỏng cắt ghép, tạo bảng thống kê. | **Native HNL + Bridge** | **P0** |
| 39 | **Layer - Change Color Tìm, Đổi Màu Đối Tượng và Phụ Hồi DM, RDM, FDM New 10.lsp** | `DM/RDM/FDM` | Đổi màu theo lọc loại đối tượng, deep search nested/dynamic blocks, tìm theo màu, restore màu cũ. | **Native HNL + Bridge** | **P1** |
| 40 | **Dim - Quick Dim_DN Dim nhanh xuyên block - New 50.lsp** | `DN/DNC` | Quick Dim theo bbox hoặc 2 điểm, layer filter, nested block layers, Model/Layout, Paper dim, bỏ wipeout/hatch/hidden, center MLine/Block, total dim. | **Native HNL + Bridge** | **P0** |
| 41 | **Layout- PSL Thiết lập PsltScale.lsp** | `PSL` | Thiết lập PSLTSCALE nhiều Layout và regen. | **AutoCAD Bridge** | **P1** |
| 42 | **Layout - Tạo Khung Tự Động Từ Model Sang Layout_TKL New 43.lsp** | `TKL` | Tạo hàng loạt Layout từ Block/Pline khung, A1-A4, fit hoặc 1:X, CTB/STB, auto naming/sort, insert title block, viewport. | **Native HNL orchestration + AutoCAD Bridge** | **P0** |
| 43 | **Block - FieldObjectsV1-1_Khoan vùng đối tượng New 7.lsp** | `FIELDOBJECTS` | Hiển thị/zoom/highlight geometry được Field tham chiếu, hỗ trợ nhiều object và Field hỏng/null handle. | **Hybrid/AutoCAD Bridge** | **P1** |
| 44 | **Block - Đánh số Block Att trùng_INC, Dời Attribute Text_MVAT 2.6.lsp** | `INC/MVAT` | Đánh số attribute trùng theo Tag; dời Attribute Text hàng loạt. | **Native HNL + Bridge** | **P1** |

## Nhóm nên đưa vào HNL trước

### P0 — giá trị cao cho CAD 2D hằng ngày
- **ATL/ATK/AT1/DY1/ATC/NDC/BLC** — Thống kê Block Attribute/Dynamic, đếm theo Tag/Visibility, tạo AutoCAD Table/Field, minh họa block.
- **QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT** — Bộ công cụ copy/sửa Text, clipboard, nhập Excel, copy Text→Attribute, swap, copy thuộc tính Dynamic/Scale/Rotation, đổi hoa/thường/title.
- **APA** — Công thức Attribute theo Tag, copy hoặc replace hàng loạt, precision và kiểm tra công thức.
- **RNL** — Quản lý danh sách Layout, reorder, A-Z/Z-A, rename đơn, đánh số hàng loạt, prefix/suffix, find/replace, đánh dấu xóa.
- **CHANGETEXTSTYLE** — Đổi TextStyle và chuyển mã Unicode/VNI/TCVN3 cho Text/MText/Attribute/MLeader/Dim/Table, bảo toàn format.
- **TAOKHUNG** — Tạo viewport từ vùng Model, fit/scale, zoom đúng vùng và khóa viewport.
- **FRM** — Find/Replace nhiều cặp cùng lúc trên Text/MText/Attribute/Block, không phân biệt hoa/thường; scope All/Model/Layout/Selection.
- **TLE** — Tạo/cập nhật Layer hàng loạt từ Excel, gồm màu/linetype/lineweight/plot.
- **VXT** — Sinh xương chính/phụ/ty treo, né thiết bị, dim tự động, tùy layer/color/linetype/lineweight/dimstyle, nhiều mode bố trí.
- **TRANSCAD** — Dịch Text/MText/Block/MLeader/Table/Dim; Replace/Bilingual/Update/Remove; OpenAI/Gemini/Google; auto Unicode/VNI/TCVN3; glossary kỹ thuật.
- **CFM/APFIELD/CFE/CFA/CFL/CFS** — Field Manager: Field lỗi, tham chiếu trùng, geometry chưa có Field, lọc theo scope/type và hợp nhất kết quả.
- **TKT** — Bóc tách Area/Length/Volume, group Layer, sort đa cột, custom columns, Field-linked CAD table, Block Attribute output, Excel export, import/export cấu hình.
- **MOF** — Offset hàng loạt nhiều polyline theo một hướng chung; closed polyline dùng area để chọn đúng phía.
- **BRK** — Break nhiều điểm, xóa đoạn, xử lý closed curves, giữ/tách Field, thay đúng ObjectID, transaction rollback.
- **DMBV** — Lấy Text/MText từ Layout, làm sạch MText/Unicode, tạo bảng danh mục bản vẽ, preview/sort/export Excel.
- **CALCULATEAREAPERIMETER** — Tổng Area/Perimeter nhiều loại geometry, highlight và copy clipboard dạng Excel.
- **AF/AFM/LF/LFM/BF** — Tạo Field tổng Length/Area/Block Count và chèn vào Text/MText/Table/Attribute/MLeader.
- **SAP** — Áp Page Setup cho nhiều Layout.
- **DEMTC** — Tính tấm trần nổi 600/610 và 1200/1220, xử lý hatch/boundary, mô phỏng cắt ghép, tạo bảng thống kê.
- **DN/DNC** — Quick Dim theo bbox hoặc 2 điểm, layer filter, nested block layers, Model/Layout, Paper dim, bỏ wipeout/hatch/hidden, center MLine/Block, total dim.
- **TKL** — Tạo hàng loạt Layout từ Block/Pline khung, A1-A4, fit hoặc 1:X, CTB/STB, auto naming/sort, insert title block, viewport.

## Kiến trúc UI đề xuất sau khi nhập các Lisp

1. **Home / Modify** — QQ, MOF, BRK, C2P, APTD, RBL1, color tools.
2. **Annotate** — DN/DNC, JD, TKD, TextStyle, FRM, Calculator Text, TransCAD.
3. **Blocks & Fields** — ATL/ATK, APA, INC/MVAT, Field Manager, FieldObjects, LF/AF/BF.
4. **Analyze / Quantity** — TKT, Area/Perimeter, DEMTC, block/text statistics.
5. **Ceiling / Shopdrawing** — VXT, DEMTC, TCD.
6. **Layout / Sheets** — TKL, RNL, TAOKHUNG, DMBV, APT, CLM, MS2PS.
7. **Publish** — SAP, PSL, CTB/STB/Page Setup, Publish Manager.
8. **Tools / Legacy Lisp** — chạy Lisp gốc qua AutoCAD Bridge, compiler FAS/VLX, diagnostic.

## Tính năng lõi nên viết lại native

- **Smart Text Tools**: Q1/Q1A/QED/QE/FRM/QT/SW trong một palette thay vì nhiều lệnh rời.
- **Smart Block/Attribute Manager**: thống kê, formula tag, copy dynamic props, renumber attributes, replace blocks.
- **Field Doctor**: hợp nhất CFE/CFA/CFL/CFS/FieldObjects/OFT thành một trung tâm Field có graph tham chiếu.
- **Geometry Toolkit**: MOF + BRK + C2P + APTD + intersection tools.
- **Quick Dimension Engine**: DN/DNC + JD + TKD, có layer filter, nested block, Model/Paper.
- **Quantity Studio**: TKT + Area/Perimeter + AF/LF/BF + DEMTC, export Excel/PDF/AutoCAD Table.
- **Layout Automation Studio**: TKL + RNL + TAOKHUNG + DMBV + SAP + PSL + APT.
- **Ceiling Studio**: VXT + DEMTC, tích hợp tránh thiết bị và auto dim.
- **Translation/Unicode Studio**: Change Text Style + TransCAD + Unicode/VNI/TCVN3 normalization.

## Những Lisp không nên bê nguyên vào Standalone

- `OFT`, `CFE/CFA/CFL/CFS`, `AF/LF/BF`: AutoCAD Field/ObjectID là dữ liệu native; HNL nên quản lý logic, AutoCAD Bridge ghi Field thật.
- `MS2PS`, `CPV1/CVP2`, `QB/BS/BQ`, `SAP`, `PSL`: phụ thuộc command/database/layout của AutoCAD.
- `ATB`: chỉnh block definition/nested transforms cần AutoCAD transaction để giữ DWG an toàn.
- `LTFV`: chỉ hữu ích trong Lisp Manager, không phải tính năng CAD 2D lõi.

## Đề xuất phiên bản tiếp theo

**v2.0 Lisp-Inspired 2D Professional** nên ưu tiên 6 trung tâm thay vì tạo thêm 40 nút:

1. Smart Text & Attribute
2. Field Doctor
3. Geometry Tools
4. Quick Dimension
5. Quantity / BOQ
6. Layout Automation

Mỗi trung tâm có `Standalone / AutoCAD Required / Hybrid`, Preview, Undo Transaction, Diagnostics và Help Center.
