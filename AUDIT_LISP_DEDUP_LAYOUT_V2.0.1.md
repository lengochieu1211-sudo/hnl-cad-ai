# HNL CAD AI v2.0.5 — Kiểm tra trùng và bố trí lại 44 Lisp

## Kết quả

- **44/44 Lisp nguồn** đã được kiểm tra.
- Chỉ còn **17 công cụ chuẩn** trong giao diện.
- Mỗi Lisp nguồn có **đúng 1 ownerId**, không gán kép.
- Command Search tìm được tên lệnh Lisp cũ nhưng mở **công cụ chuẩn**, không tạo nút/lệnh UI trùng.
- Ribbon bỏ các nhóm top-level `Annotate / Blocks / Analyze` riêng; chúng được gom vào **2D Professional**.
- `Layout` và `Publish` được gom thành một nhóm **Layout & Publish**.
- Lisp gốc vẫn xem được ở tab **44 Lisp nguồn** và Legacy Lisp Manager.

## Bố trí 17 công cụ chuẩn

| Công cụ chuẩn | Nhóm | Mode | Ưu tiên | Lisp/lệnh nguồn đã gộp |
|---|---|---|---|---|
| **Smart Text Editor** | `TEXT` | **HYBRID** | **P0** | `QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT` • `FRM` • `CHANGETEXTSTYLE` • `TRANSCAD` |
| **Text Calculator** | `TEXT` | **NATIVE** | **P1** | `APC/APS/APE` |
| **Attribute Manager** | `TEXT` | **HYBRID** | **P0** | `APA` • `INC/MVAT` |
| **Block Manager** | `BLOCK_FIELD` | **HYBRID** | **P0** | `ATL/ATK/AT1/DY1/ATC/NDC/BLC` • `ATB/ADDTOBLOCK` • `BFM` • `RBL1` • `QB/BS/BQ/BN` • `DM/RDM/FDM` |
| **Field Doctor** | `FIELD` | **AUTOCAD** | **P0** | `OFT` • `CFM/APFIELD/CFE/CFA/CFL/CFS` • `FIELDOBJECTS` |
| **Offset / Break / Trim** | `GEOMETRY` | **HYBRID** | **P0** | `MOF` • `BRK` |
| **Geometry Convert** | `GEOMETRY` | **NATIVE** | **P1** | `C2P` • `APTD` |
| **Intersection Tools** | `GEOMETRY` | **NATIVE** | **P2** | `INT` • `IPMX` |
| **Quick Dimension Studio** | `DIMENSION` | **HYBRID** | **P0** | `TKD` • `JD` • `DN/DNC` |
| **Quantity / BOQ Studio** | `QUANTITY` | **HYBRID** | **P0** | `TKT` • `CALCULATEAREAPERIMETER` • `AF/AFM/LF/LFM/BF` |
| **Layer Data Manager** | `QUANTITY` | **HYBRID** | **P0** | `TLE` |
| **Ceiling / Shopdrawing Studio** | `CEILING` | **HYBRID** | **P0** | `VXT` • `DEMTC` |
| **Layout Manager** | `LAYOUT` | **HYBRID** | **P0** | `CLM` • `RNL` • `DMBV` |
| **Viewport Manager** | `LAYOUT` | **AUTOCAD** | **P0** | `MS2PS` • `TAOKHUNG` • `APT` • `CVP1/CVP2` • `LL/BT` |
| **Layout Automation Studio** | `LAYOUT` | **HYBRID** | **P0** | `TCD` • `TKL` |
| **Publish Setup** | `LAYOUT` | **AUTOCAD** | **P0** | `SAP` • `PSL` |
| **Legacy Lisp Tooling** | `TOOLS` | **AUTOCAD** | **P3** | `LTFV` |

## Quy tắc chống trùng

1. **Một chức năng = một nơi chính để dùng.**
2. Lệnh Lisp cũ chỉ là **alias tìm kiếm/Legacy**, không phải nút Ribbon mới.
3. Nếu một Lisp có nhiều hành vi, nó vẫn có **một owner chính**; các module khác gọi chung engine thay vì tạo bản sao.
4. `NATIVE` chạy trong HNL; `HYBRID` chia logic HNL + AutoCAD; `AUTOCAD` chỉ chạy native DWG qua Bridge.
5. Field ObjectID / CHSPACE / VP Freeze / Page Setup / CTB-STB / Block Editor / DST không được tạo bản Standalone giả.

## Ribbon v2.0.5

**Home → 2D Professional → SketchUp → Layout & Publish → AI & Legacy → Kỹ thuật**

Trong **2D Professional**:
- Tool Center
- Text & Attribute
- Field
- Block
- Quantity
- Area & Length
- Table
- Translation

## Command Search

Ví dụ:
- gõ `BRK` → mở Geometry
- gõ `CFE` → mở Field & Links
- gõ `DN` → mở Dimension
- gõ `TKT` → mở Data / BOQ / Ceiling
- gõ `TKL` → mở Layout & Publish
- gõ `QQ` → mở Text / Attribute / Block

Không xuất hiện thêm 44 command cards độc lập ngoài 17 tool chuẩn.
