export type LispSupportMode='NATIVE'|'HYBRID'|'AUTOCAD'|'NATIVE_AI';
export type LispCenter='TEXT'|'FIELD'|'GEOMETRY'|'DIMENSION'|'QUANTITY'|'LAYOUT'|'BLOCK_FIELD'|'CEILING'|'TOOLS';
export interface LispFeatureItem{commands:string;name:string;center:LispCenter;summary:string;mode:LispSupportMode;priority:'P0'|'P1'|'P2'|'P3';ownerId:string;}
export const LISP_FEATURE_CATALOG:LispFeatureItem[]=[
{"commands": "ATL/ATK/AT1/DY1/ATC/NDC/BLC", "name": "Block & Attribute", "center": "BLOCK_FIELD", "summary": "Thống kê Block Attribute/Dynamic/Visibility, table/field", "mode": "HYBRID", "priority": "P0", "ownerId": "BLOCK_MANAGER"},
{"commands": "TKD", "name": "Dim Radius/Arc", "center": "DIMENSION", "summary": "Ghép Dim bán kính/Arc và thống kê", "mode": "HYBRID", "priority": "P2", "ownerId": "QUICK_DIMENSION"},
{"commands": "ATB/ADDTOBLOCK", "name": "Add to Block", "center": "BLOCK_FIELD", "summary": "Thêm geometry vào block/nested transform", "mode": "AUTOCAD", "priority": "P1", "ownerId": "BLOCK_MANAGER"},
{"commands": "QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT", "name": "Smart Text", "center": "TEXT", "summary": "Copy/sửa Text, clipboard, Excel, Text→Attribute, Dynamic props, đổi case", "mode": "HYBRID", "priority": "P0", "ownerId": "SMART_TEXT"},
{"commands": "MS2PS", "name": "Model → Paper", "center": "LAYOUT", "summary": "CHSPACE giữ scale/rotation/vị trí viewport", "mode": "AUTOCAD", "priority": "P1", "ownerId": "VIEWPORT_MANAGER"},
{"commands": "APA", "name": "Attribute Formula", "center": "TEXT", "summary": "Công thức Attribute theo Tag, batch replace/copy", "mode": "HYBRID", "priority": "P0", "ownerId": "ATTRIBUTE_MANAGER"},
{"commands": "CLM", "name": "Copy Layout + Model", "center": "LAYOUT", "summary": "Copy Layout kèm geometry liên quan", "mode": "AUTOCAD", "priority": "P2", "ownerId": "LAYOUT_MANAGER"},
{"commands": "RNL", "name": "Layout Manager", "center": "LAYOUT", "summary": "Sort/rename/prefix/suffix/find-replace layout", "mode": "HYBRID", "priority": "P0", "ownerId": "LAYOUT_MANAGER"},
{"commands": "BFM", "name": "Block Filter in Polyline", "center": "BLOCK_FIELD", "summary": "Chọn block trong boundary polyline", "mode": "NATIVE", "priority": "P1", "ownerId": "BLOCK_MANAGER"},
{"commands": "CHANGETEXTSTYLE", "name": "Text Style & Encoding", "center": "TEXT", "summary": "TextStyle + Unicode/VNI/TCVN3", "mode": "HYBRID", "priority": "P0", "ownerId": "SMART_TEXT"},
{"commands": "C2P", "name": "Circle → Polyline", "center": "GEOMETRY", "summary": "Chuyển circle thành closed polyline", "mode": "NATIVE", "priority": "P2", "ownerId": "GEOMETRY_CONVERT"},
{"commands": "OFT", "name": "Offset giữ Field", "center": "FIELD", "summary": "Offset geometry và giữ ObjectID Field", "mode": "AUTOCAD", "priority": "P1", "ownerId": "FIELD_DOCTOR"},
{"commands": "JD", "name": "Join Dimension", "center": "DIMENSION", "summary": "Gộp dim cùng phương thành dim tổng", "mode": "HYBRID", "priority": "P1", "ownerId": "QUICK_DIMENSION"},
{"commands": "TAOKHUNG", "name": "Create Viewport", "center": "LAYOUT", "summary": "Tạo viewport từ vùng Model, scale/lock", "mode": "HYBRID", "priority": "P0", "ownerId": "VIEWPORT_MANAGER"},
{"commands": "TCD", "name": "Extract Detail", "center": "LAYOUT", "summary": "Trích chi tiết Model/Layout theo khung", "mode": "HYBRID", "priority": "P1", "ownerId": "LAYOUT_AUTOMATION"},
{"commands": "FRM", "name": "Multi Find Replace", "center": "TEXT", "summary": "Find/Replace nhiều cặp trên text/attribute", "mode": "NATIVE", "priority": "P0", "ownerId": "SMART_TEXT"},
{"commands": "TLE", "name": "Layer from Excel", "center": "QUANTITY", "summary": "Tạo/cập nhật layer từ Excel", "mode": "HYBRID", "priority": "P0", "ownerId": "LAYER_DATA"},
{"commands": "INT", "name": "Grid Intersection", "center": "GEOMETRY", "summary": "Giao điểm 2 nhóm trục + đánh số", "mode": "NATIVE", "priority": "P2", "ownerId": "INTERSECTION_TOOLS"},
{"commands": "VXT", "name": "Ceiling Framing", "center": "CEILING", "summary": "Xương chính/phụ/ty treo, né thiết bị, auto dim", "mode": "HYBRID", "priority": "P0", "ownerId": "CEILING_STUDIO"},
{"commands": "TRANSCAD", "name": "Translation Studio", "center": "TEXT", "summary": "Dịch kỹ thuật + bilingual + Unicode", "mode": "NATIVE_AI", "priority": "P0", "ownerId": "SMART_TEXT"},
{"commands": "CFM/APFIELD/CFE/CFA/CFL/CFS", "name": "Field Doctor", "center": "FIELD", "summary": "Field lỗi/trùng/link/scope/type", "mode": "AUTOCAD", "priority": "P0", "ownerId": "FIELD_DOCTOR"},
{"commands": "APT", "name": "Viewport Lock", "center": "LAYOUT", "summary": "Lock/Unlock viewport hàng loạt", "mode": "HYBRID", "priority": "P1", "ownerId": "VIEWPORT_MANAGER"},
{"commands": "TKT", "name": "Quantity/BOQ", "center": "QUANTITY", "summary": "Area/Length/Volume, group/sort, custom columns, Excel/CAD Table", "mode": "HYBRID", "priority": "P0", "ownerId": "QUANTITY_BOQ"},
{"commands": "CVP1/CVP2", "name": "VP Freeze Copy", "center": "LAYOUT", "summary": "Copy trạng thái VP Freeze", "mode": "AUTOCAD", "priority": "P1", "ownerId": "VIEWPORT_MANAGER"},
{"commands": "MOF", "name": "Multi Offset", "center": "GEOMETRY", "summary": "Offset nhiều polyline cùng phía", "mode": "NATIVE", "priority": "P0", "ownerId": "SMART_OFFSET_BREAK"},
{"commands": "BRK", "name": "Break/Trim + Field", "center": "GEOMETRY", "summary": "Break nhiều điểm, xóa đoạn, rollback, giữ Field", "mode": "HYBRID", "priority": "P0", "ownerId": "SMART_OFFSET_BREAK"},
{"commands": "APC/APS/APE", "name": "Text Calculator", "center": "TEXT", "summary": "Tính công thức và ghi kết quả Text/Attribute", "mode": "NATIVE", "priority": "P1", "ownerId": "TEXT_FORMULA"},
{"commands": "DMBV", "name": "Drawing Index", "center": "LAYOUT", "summary": "Lấy text từ Layout tạo danh mục bản vẽ", "mode": "HYBRID", "priority": "P0", "ownerId": "LAYOUT_MANAGER"},
{"commands": "CALCULATEAREAPERIMETER", "name": "Area/Perimeter", "center": "QUANTITY", "summary": "Tổng diện tích/chu vi nhiều geometry", "mode": "NATIVE", "priority": "P0", "ownerId": "QUANTITY_BOQ"},
{"commands": "AF/AFM/LF/LFM/BF", "name": "Field Quantity", "center": "FIELD", "summary": "Field tổng Area/Length/Block Count", "mode": "AUTOCAD", "priority": "P0", "ownerId": "QUANTITY_BOQ"},
{"commands": "RBL1", "name": "Replace Block", "center": "BLOCK_FIELD", "summary": "Thay block hàng loạt", "mode": "HYBRID", "priority": "P1", "ownerId": "BLOCK_MANAGER"},
{"commands": "APTD", "name": "Width → 2 Lines", "center": "GEOMETRY", "summary": "Đường có width thành hai đường song song", "mode": "NATIVE", "priority": "P1", "ownerId": "GEOMETRY_CONVERT"},
{"commands": "SAP", "name": "Batch Page Setup", "center": "LAYOUT", "summary": "Áp Page Setup hàng loạt Layout", "mode": "HYBRID", "priority": "P0", "ownerId": "PUBLISH_SETUP"},
{"commands": "QB/BS/BQ/BN", "name": "Block Editor", "center": "BLOCK_FIELD", "summary": "Open/Save/Cancel/Rename Block Editor", "mode": "AUTOCAD", "priority": "P2", "ownerId": "BLOCK_MANAGER"},
{"commands": "LTFV", "name": "Lisp Compiler", "center": "TOOLS", "summary": "Compile Lisp FAS/VLX", "mode": "AUTOCAD", "priority": "P3", "ownerId": "LEGACY_LISP_TOOLING"},
{"commands": "LL/BT", "name": "Layout Light Mode", "center": "LAYOUT", "summary": "Giảm viewport render để chống lag", "mode": "AUTOCAD", "priority": "P1", "ownerId": "VIEWPORT_MANAGER"},
{"commands": "IPMX", "name": "Intersection Number", "center": "GEOMETRY", "summary": "Giao 2 polyline, point + text đánh số", "mode": "NATIVE", "priority": "P2", "ownerId": "INTERSECTION_TOOLS"},
{"commands": "DEMTC", "name": "Ceiling Tile Quantity", "center": "CEILING", "summary": "Tính tấm trần nổi và cắt ghép", "mode": "HYBRID", "priority": "P0", "ownerId": "CEILING_STUDIO"},
{"commands": "DM/RDM/FDM", "name": "Color Manager", "center": "BLOCK_FIELD", "summary": "Lọc/đổi/restore màu kể cả nested block", "mode": "HYBRID", "priority": "P1", "ownerId": "BLOCK_MANAGER"},
{"commands": "DN/DNC", "name": "Quick Dimension", "center": "DIMENSION", "summary": "Dim nhanh bbox/2 điểm/layer/nested block/model-layout", "mode": "HYBRID", "priority": "P0", "ownerId": "QUICK_DIMENSION"},
{"commands": "PSL", "name": "PSLTSCALE", "center": "LAYOUT", "summary": "Thiết lập PSLTSCALE nhiều layout", "mode": "AUTOCAD", "priority": "P1", "ownerId": "PUBLISH_SETUP"},
{"commands": "TKL", "name": "Layout Automation", "center": "LAYOUT", "summary": "Tạo hàng loạt Layout từ khung, paper/scale/CTB/viewport", "mode": "HYBRID", "priority": "P0", "ownerId": "LAYOUT_AUTOMATION"},
{"commands": "FIELDOBJECTS", "name": "Field Object Locator", "center": "FIELD", "summary": "Zoom/highlight geometry được Field tham chiếu", "mode": "AUTOCAD", "priority": "P1", "ownerId": "FIELD_DOCTOR"},
{"commands": "INC/MVAT", "name": "Attribute Number/Move", "center": "TEXT", "summary": "Đánh số attribute + dời attribute text", "mode": "HYBRID", "priority": "P1", "ownerId": "ATTRIBUTE_MANAGER"},
];

export type LispGuide = {
  whenToUse:string;
  prerequisites:string[];
  selection:string;
  steps:string[];
  expected:string;
  commonErrors:string[];
};

export function primaryLispCommand(commands:string){
  return String(commands||"")
    .split("/")
    .map(x=>x.trim())
    .find(x=>x && !x.includes("-")) || "";
}

export function buildLispGuide(item:LispFeatureItem):LispGuide{
  const mode = item.mode;
  const cmd = primaryLispCommand(item.commands);
  const requiresAcad = mode==="AUTOCAD" || mode==="HYBRID";
  const selectionByCenter:Record<string,string>={
    TEXT:"Thường chọn Text/MText/MLeader hoặc Block Attribute tùy lệnh.",
    FIELD:"Chọn Text/MText/Attribute có Field và/hoặc geometry được Field tham chiếu.",
    GEOMETRY:"Chọn Line/Polyline/Circle/Spline/geometry theo yêu cầu lệnh.",
    DIMENSION:"Chọn geometry hoặc Dimension liên quan; kiểm tra DimStyle/Layer.",
    QUANTITY:"Chọn geometry/block cần thống kê hoặc phạm vi bản vẽ.",
    LAYOUT:"Chọn Layout/Viewport/khung bản vẽ theo workflow.",
    BLOCK_FIELD:"Chọn Block/Attribute/Dynamic Block hoặc boundary liên quan.",
    CEILING:"Chọn boundary trần/phòng và thiết bị MEP nếu workflow yêu cầu.",
    TOOLS:"Phụ thuộc công cụ; thường không cần selection ban đầu."
  };
  const centerStep:Record<string,string>={
    TEXT:"Kiểm tra đúng loại Text/Attribute và Tag trước khi xác nhận thay đổi hàng loạt.",
    FIELD:"Không xóa geometry tham chiếu trước khi kiểm tra Field/ObjectID.",
    GEOMETRY:"Preview hình học, kiểm tra phía/điểm/ngưỡng trước thao tác phá hủy.",
    DIMENSION:"Kiểm tra DIMSTYLE, layer DIM, scale Model/Paper trước khi tạo dim.",
    QUANTITY:"Kiểm tra đơn vị, layer, loại entity và phạm vi thống kê.",
    LAYOUT:"Kiểm tra Paper size, viewport scale, CTB/STB/Page Setup trước batch.",
    BLOCK_FIELD:"Kiểm tra tên block, Dynamic/Attribute/Xref trước khi sửa block.",
    CEILING:"Kiểm tra module, board width, spacing, hướng chạy và boundary.",
    TOOLS:"Đọc Command Line sau khi chạy để xác nhận công cụ đã nạp/thực thi."
  };

  return {
    whenToUse:`Dùng khi cần: ${item.summary}.`,
    prerequisites:[
      "Save/Save As bản vẽ trước thao tác batch hoặc phá hủy.",
      requiresAcad ? "Cần AutoCAD + HNL Bridge Connected để chạy Lisp gốc/native DWG." : "Có thể dùng workflow HNL tương ứng; Lisp gốc vẫn cần AutoCAD để LOAD.",
      mode==="AUTOCAD" ? "Đây là workflow phụ thuộc AutoCAD native; Standalone không thể chạy tương đương đầy đủ." :
      mode==="HYBRID" ? "Một phần có thể có HNL native, nhưng Field/Layout/Block/DWG thật vẫn cần AutoCAD." :
      "Ưu tiên công cụ HNL native nếu đã có; Lisp nguồn dùng để tương thích lệnh cũ."
    ],
    selection:selectionByCenter[item.center] || "Chọn đối tượng theo prompt của Lisp.",
    steps:[
      `Trong tab 44 Lisp nguồn, tìm ${cmd || item.commands}.`,
      "Nếu chưa có file nguồn: bấm Nạp file Lisp hoặc Nạp thư mục Lisp và chọn thư mục đã giải nén AI.zip.",
      "Kiểm tra cột File nguồn phải hiện ĐÃ TÌM THẤY.",
      "Bấm Nạp để AutoCAD LOAD file .lsp. Xem Command Line để xác nhận không có lỗi SECURELOAD/dependency.",
      `Bấm Nạp + Chạy để LOAD rồi gọi lệnh ${cmd || item.commands}.`,
      centerStep[item.center] || "Làm theo prompt trên AutoCAD Command Line.",
      "Sau khi chạy, kiểm tra kết quả; nếu không đúng dùng Undo và gửi Diagnostic + dòng lỗi AutoCAD."
    ],
    expected:`Lệnh ${cmd || item.commands} được AutoCAD nạp/chạy; kết quả mong đợi: ${item.summary}.`,
    commonErrors:[
      "Bridge OFFLINE: HNL không thể LOAD Lisp vào AutoCAD.",
      "SECURELOAD / trusted path: AutoCAD có thể chặn .lsp ngoài Trusted Locations.",
      "Unknown command sau LOAD: file không định nghĩa command đã chọn, LOAD lỗi, hoặc command nằm trong dependency khác.",
      "DCL/Excel/COM lỗi: Lisp cũ có thể cần file phụ, Excel hoặc đường dẫn riêng.",
      "Không có đối tượng phù hợp: làm đúng bước selection theo prompt AutoCAD."
    ]
  };
}
