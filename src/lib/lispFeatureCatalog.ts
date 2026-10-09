export type LispSupportMode='NATIVE'|'HYBRID'|'AUTOCAD'|'NATIVE_AI';
export type LispCenter='TEXT'|'BLOCK'|'FIELD'|'GEOMETRY'|'DIMENSION'|'LAYER'|'QUANTITY'|'SHOPDRAWING'|'LAYOUT'|'TOOLS';
export type LispSourceGroup=LispCenter;
export interface LispFeatureItem{
  commands:string;
  name:string;
  center:LispCenter;
  sourceGroup:LispSourceGroup;
  sourceFile:string;
  summary:string;
  mode:LispSupportMode;
  priority:'P0'|'P1'|'P2'|'P3';
  ownerId:string;
}
export const LISP_SOURCE_GROUP_LABELS:Record<LispSourceGroup,string> = {"TEXT":"Text","BLOCK":"Block / Attribute","FIELD":"Field / Links","GEOMETRY":"Geometry / Polyline","DIMENSION":"Dimension","LAYER":"Layer / Properties","QUANTITY":"Quantity / BOQ","SHOPDRAWING":"Shopdrawing / Detail / Ceiling","LAYOUT":"Layout / Viewport / Publish","TOOLS":"Tools / Legacy"};
export const LISP_SOURCE_GROUP_ORDER:LispSourceGroup[] = ["TEXT","BLOCK","FIELD","GEOMETRY","DIMENSION","LAYER","QUANTITY","SHOPDRAWING","LAYOUT","TOOLS"];
export const LISP_FEATURE_CATALOG:LispFeatureItem[]=[
{"commands":"APA","name":"Attribute Formula","center":"BLOCK","summary":"Công thức Attribute theo Tag, batch replace/copy","mode":"HYBRID","priority":"P0","ownerId":"ATTRIBUTE_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block - Copy Block Att_APA 5.lsp"},
{"commands":"INC/MVAT","name":"Attribute Number/Move","center":"BLOCK","summary":"Đánh số attribute + dời attribute text","mode":"HYBRID","priority":"P1","ownerId":"ATTRIBUTE_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block - Đánh số Block Att trùng_INC, Dời Attribute Text_MVAT 2.6.lsp"},
{"commands":"QB/BS/BQ/BN","name":"Block Editor","center":"BLOCK","summary":"Open/Save/Cancel/Rename Block Editor","mode":"AUTOCAD","priority":"P2","ownerId":"BLOCK_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block - Edit Block_QB Vào, BS Lưu, BQ Hủy, BN Đổi tên (New).lsp"},
{"commands":"FIELDOBJECTS","name":"Field Object Locator","center":"FIELD","summary":"Zoom/highlight geometry được Field tham chiếu","mode":"AUTOCAD","priority":"P1","ownerId":"FIELD_DOCTOR","sourceGroup":"FIELD","sourceFile":"Block - FieldObjectsV1-1_Khoan vùng đối tượng New 7.lsp"},
{"commands":"TCD","name":"Extract Detail","center":"SHOPDRAWING","summary":"Trích chi tiết Model/Layout theo khung","mode":"HYBRID","priority":"P1","ownerId":"DETAIL_SHOPDRAWING","sourceGroup":"SHOPDRAWING","sourceFile":"Block - TCD_Trích chi tiết trên model và layout 7.lsp"},
{"commands":"ATL/ATK/AT1/DY1/ATC/NDC/BLC","name":"Block & Attribute","center":"BLOCK","summary":"Thống kê Block Attribute/Dynamic/Visibility, table/field","mode":"HYBRID","priority":"P0","ownerId":"BLOCK_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block - Thong ke Block Att, Dynamic New 8.lsp"},
{"commands":"BFM","name":"Block Filter in Polyline","center":"BLOCK","summary":"Chọn block trong boundary polyline","mode":"NATIVE","priority":"P1","ownerId":"BLOCK_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block_BFM Chọn block theo tùy chọn trong vùng poline.lsp"},
{"commands":"RBL1","name":"Replace Block","center":"BLOCK","summary":"Thay block hàng loạt","mode":"HYBRID","priority":"P1","ownerId":"BLOCK_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block-V2_RBL1 Thay thế block.lsp"},
{"commands":"LTFV","name":"Lisp Compiler","center":"TOOLS","summary":"Compile Lisp FAS/VLX","mode":"AUTOCAD","priority":"P3","ownerId":"LEGACY_LISP_TOOLING","sourceGroup":"TOOLS","sourceFile":"Cover Lsp to Fas, Vxl_LTFV.lsp"},
{"commands":"DEMTC","name":"Ceiling Tile Quantity","center":"SHOPDRAWING","summary":"Tính tấm trần nổi và cắt ghép","mode":"HYBRID","priority":"P0","ownerId":"SHOPDRAWING_STUDIO","sourceGroup":"SHOPDRAWING","sourceFile":"Demtc_Tính tấm trần nổi 2.lsp"},
{"commands":"JD","name":"Join Dimension","center":"DIMENSION","summary":"Gộp dim cùng phương thành dim tổng","mode":"HYBRID","priority":"P1","ownerId":"QUICK_DIMENSION","sourceGroup":"DIMENSION","sourceFile":"Dim - Join Dim_JD.lsp"},
{"commands":"DN/DNC","name":"Quick Dimension","center":"DIMENSION","summary":"Dim nhanh bbox/2 điểm/layer/nested block/model-layout","mode":"HYBRID","priority":"P0","ownerId":"QUICK_DIMENSION","sourceGroup":"DIMENSION","sourceFile":"Dim - Quick Dim_DN Dim nhanh xuyên block - New 50.lsp"},
{"commands":"TKD","name":"Dim Radius/Arc","center":"DIMENSION","summary":"Ghép Dim bán kính/Arc và thống kê","mode":"HYBRID","priority":"P2","ownerId":"QUICK_DIMENSION","sourceGroup":"DIMENSION","sourceFile":"Dim - Thống kê Dim Bán kính và Dim Arc_TKD.lsp"},
{"commands":"CALCULATEAREAPERIMETER","name":"Area/Perimeter","center":"QUANTITY","summary":"Tổng diện tích/chu vi nhiều geometry","mode":"NATIVE","priority":"P0","ownerId":"QUANTITY_BOQ","sourceGroup":"QUANTITY","sourceFile":"Field - CalculateAreaPerimeter_Tổng diện tích, chu vi.lsp"},
{"commands":"CFM/APFIELD/CFE/CFA/CFL/CFS","name":"Field Doctor","center":"FIELD","summary":"Field lỗi/trùng/link/scope/type","mode":"AUTOCAD","priority":"P0","ownerId":"FIELD_DOCTOR","sourceGroup":"FIELD","sourceFile":"Field - CFM, CFA, CFE, CFL, CFS (Check Field Duplicate, Errors, Links) 12.lsp"},
{"commands":"AF/AFM/LF/LFM/BF","name":"Field Quantity","center":"FIELD","summary":"Field tổng Area/Length/Block Count","mode":"AUTOCAD","priority":"P0","ownerId":"QUANTITY_BOQ","sourceGroup":"FIELD","sourceFile":"Field - LengthAreaFieldV1-4 AF,LF,BF Tổng diện tích, chu vi, block New 6.lsp"},
{"commands":"TKT","name":"Quantity/BOQ","center":"QUANTITY","summary":"Area/Length/Volume, group/sort, custom columns, Excel/CAD Table","mode":"HYBRID","priority":"P0","ownerId":"QUANTITY_BOQ","sourceGroup":"QUANTITY","sourceFile":"Geomprops_TKT Tính tổng diện tích, chu vi, xuất bảng - 87.lsp"},
{"commands":"INT","name":"Grid Intersection","center":"GEOMETRY","summary":"Giao điểm 2 nhóm trục + đánh số","mode":"NATIVE","priority":"P2","ownerId":"INTERSECTION_TOOLS","sourceGroup":"GEOMETRY","sourceFile":"Int_eng_Đánh số các điểm giao nhau.lsp"},
{"commands":"IPMX","name":"Intersection Number","center":"GEOMETRY","summary":"Giao 2 polyline, point + text đánh số","mode":"NATIVE","priority":"P2","ownerId":"INTERSECTION_TOOLS","sourceGroup":"GEOMETRY","sourceFile":"IPMX_Chèn Point Điểm Giao 2 Polyline (Có đánh số).lsp"},
{"commands":"DM/RDM/FDM","name":"Color Manager","center":"LAYER","summary":"Lọc/đổi/restore màu kể cả nested block","mode":"HYBRID","priority":"P1","ownerId":"LAYER_MANAGER","sourceGroup":"LAYER","sourceFile":"Layer - Change Color Tìm, Đổi Màu Đối Tượng và Phụ Hồi DM, RDM, FDM New 10.lsp"},
{"commands":"TLE","name":"Layer from Excel","center":"LAYER","summary":"Tạo/cập nhật layer từ Excel","mode":"HYBRID","priority":"P0","ownerId":"LAYER_MANAGER","sourceGroup":"LAYER","sourceFile":"Layer_Tạo Layer từ Excel_TLE 3.lsp"},
{"commands":"CVP1/CVP2","name":"VP Freeze Copy","center":"LAYOUT","summary":"Copy trạng thái VP Freeze","mode":"AUTOCAD","priority":"P1","ownerId":"VIEWPORT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - CPV1, CPV2_Sao chép VP freeze xuyên layout và bản vẽ.lsp"},
{"commands":"DMBV","name":"Drawing Index","center":"LAYOUT","summary":"Lấy text từ Layout tạo danh mục bản vẽ","mode":"HYBRID","priority":"P0","ownerId":"LAYOUT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - DMBV tạo bảng từ text, mtext từ layout New 10.lsp"},
{"commands":"MS2PS","name":"Model → Paper","center":"LAYOUT","summary":"CHSPACE giữ scale/rotation/vị trí viewport","mode":"AUTOCAD","priority":"P1","ownerId":"VIEWPORT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - Ms2psV1-0_Copy, Move Model Sang Layout Nhanh Trong Viewport New.lsp"},
{"commands":"SAP","name":"Batch Page Setup","center":"LAYOUT","summary":"Áp Page Setup hàng loạt Layout","mode":"HYBRID","priority":"P0","ownerId":"PUBLISH_SETUP","sourceGroup":"LAYOUT","sourceFile":"Layout - Page setup For All Layouts_SAP Áp Page setup Layout New.lsp"},
{"commands":"RNL","name":"Layout Manager","center":"LAYOUT","summary":"Sort/rename/prefix/suffix/find-replace layout","mode":"HYBRID","priority":"P0","ownerId":"LAYOUT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - Rename Layout List_RNL - New 8.lsp"},
{"commands":"TKL","name":"Layout Automation","center":"LAYOUT","summary":"Tạo hàng loạt Layout từ khung, paper/scale/CTB/viewport","mode":"HYBRID","priority":"P0","ownerId":"LAYOUT_AUTOMATION","sourceGroup":"LAYOUT","sourceFile":"Layout - Tạo Khung Tự Động Từ Model Sang Layout_TKL New 43.lsp"},
{"commands":"TAOKHUNG","name":"Create Viewport","center":"LAYOUT","summary":"Tạo viewport từ vùng Model, scale/lock","mode":"HYBRID","priority":"P0","ownerId":"VIEWPORT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - TaoKhung_Tạo Viewport Từ Model.lsp"},
{"commands":"APT","name":"Viewport Lock","center":"LAYOUT","summary":"Lock/Unlock viewport hàng loạt","mode":"HYBRID","priority":"P1","ownerId":"VIEWPORT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout - Viewport_APT Khóa hoặc mở khóa Viewport - New 2.lsp"},
{"commands":"CLM","name":"Copy Layout + Model","center":"LAYOUT","summary":"Copy Layout kèm geometry liên quan","mode":"AUTOCAD","priority":"P2","ownerId":"LAYOUT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout- Copy Layout và Model_CLM 3.lsp"},
{"commands":"PSL","name":"PSLTSCALE","center":"LAYOUT","summary":"Thiết lập PSLTSCALE nhiều layout","mode":"AUTOCAD","priority":"P1","ownerId":"PUBLISH_SETUP","sourceGroup":"LAYOUT","sourceFile":"Layout- PSL Thiết lập PsltScale.lsp"},
{"commands":"LL/BT","name":"Layout Light Mode","center":"LAYOUT","summary":"Giảm viewport render để chống lag","mode":"AUTOCAD","priority":"P1","ownerId":"VIEWPORT_MANAGER","sourceGroup":"LAYOUT","sourceFile":"Layout_LL Làm nhẹ Layout_BT Mở lại giá trị bình thường.lsp"},
{"commands":"C2P","name":"Circle → Polyline","center":"GEOMETRY","summary":"Chuyển circle thành closed polyline","mode":"NATIVE","priority":"P2","ownerId":"GEOMETRY_CONVERT","sourceGroup":"GEOMETRY","sourceFile":"Polyline - Chuyển circle sang polyline_C2P.lsp"},
{"commands":"OFT","name":"Offset giữ Field","center":"FIELD","summary":"Offset geometry và giữ ObjectID Field","mode":"AUTOCAD","priority":"P1","ownerId":"FIELD_DOCTOR","sourceGroup":"FIELD","sourceFile":"Polyline - Offset Field Text_OFT 7.lsp"},
{"commands":"MOF","name":"Multi Offset","center":"GEOMETRY","summary":"Offset nhiều polyline cùng phía","mode":"NATIVE","priority":"P0","ownerId":"SMART_OFFSET_BREAK","sourceGroup":"GEOMETRY","sourceFile":"Polyline - Offset Nhanh Polyline_MOF.lsp"},
{"commands":"BRK","name":"Break/Trim + Field","center":"GEOMETRY","summary":"Break nhiều điểm, xóa đoạn, rollback, giữ Field","mode":"HYBRID","priority":"P0","ownerId":"SMART_OFFSET_BREAK","sourceGroup":"GEOMETRY","sourceFile":"Polyline - Polyline Break_BRK_V5.0.lsp"},
{"commands":"APTD","name":"Width → 2 Lines","center":"GEOMETRY","summary":"Đường có width thành hai đường song song","mode":"NATIVE","priority":"P1","ownerId":"GEOMETRY_CONVERT","sourceGroup":"GEOMETRY","sourceFile":"Polyline - Polyline To 2 Polyline_APTD 6.lsp"},
{"commands":"APC/APS/APE","name":"Text Calculator","center":"TEXT","summary":"Tính công thức và ghi kết quả Text/Attribute","mode":"NATIVE","priority":"P1","ownerId":"TEXT_FORMULA","sourceGroup":"TEXT","sourceFile":"Text - Calculator Text_APC, APS, APE 6.lsp"},
{"commands":"CHANGETEXTSTYLE","name":"Text Style & Encoding","center":"TEXT","summary":"TextStyle + Unicode/VNI/TCVN3","mode":"HYBRID","priority":"P0","ownerId":"SMART_TEXT","sourceGroup":"TEXT","sourceFile":"Text - Change Text Style 8.lsp"},
{"commands":"QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT","name":"Smart Text","center":"TEXT","summary":"Copy/sửa Text, clipboard, Excel, Text→Attribute, Dynamic props, đổi case","mode":"HYBRID","priority":"P0","ownerId":"SMART_TEXT","sourceGroup":"TEXT","sourceFile":"Text - Copy Text, Att, Dynamic v1.01b_QQ New 39.lsp"},
{"commands":"FRM","name":"Multi Find Replace","center":"TEXT","summary":"Find/Replace nhiều cặp trên text/attribute","mode":"NATIVE","priority":"P0","ownerId":"SMART_TEXT","sourceGroup":"TEXT","sourceFile":"Text - Find Replace Multiple_FRM.lsp"},
{"commands":"TRANSCAD","name":"Translation Studio","center":"TEXT","summary":"Dịch kỹ thuật + bilingual + Unicode","mode":"NATIVE_AI","priority":"P0","ownerId":"SMART_TEXT","sourceGroup":"TEXT","sourceFile":"Text - Translate Text, Mtext_TransCad 17.lsp"},
{"commands":"VXT","name":"Ceiling Framing","center":"SHOPDRAWING","summary":"Xương chính/phụ/ty treo, né thiết bị, auto dim","mode":"HYBRID","priority":"P0","ownerId":"SHOPDRAWING_STUDIO","sourceGroup":"SHOPDRAWING","sourceFile":"Vẽ Xương Trần V6.7.2_VXT.lsp"},
{"commands":"ATB/ADDTOBLOCK","name":"Add to Block","center":"BLOCK","summary":"Thêm geometry vào block/nested transform","mode":"AUTOCAD","priority":"P1","ownerId":"BLOCK_MANAGER","sourceGroup":"BLOCK","sourceFile":"Block - AddObjectsToBlockV1-2_ATB New 11_FixLag_All.lsp"},
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
    TEXT:"Chọn Text/MText/MLeader hoặc đối tượng chữ theo lệnh.",
    BLOCK:"Chọn Block/Attribute/Dynamic Block hoặc block mẫu theo lệnh.",
    FIELD:"Chọn Text/MText/Attribute có Field và/hoặc geometry được Field tham chiếu.",
    GEOMETRY:"Chọn Line/Polyline/Circle/Spline/geometry theo yêu cầu lệnh.",
    DIMENSION:"Chọn geometry hoặc Dimension liên quan; kiểm tra DimStyle/Layer.",
    LAYER:"Chọn đối tượng/layer hoặc file Excel cấu hình layer tùy lệnh.",
    QUANTITY:"Chọn geometry/block cần thống kê hoặc phạm vi bản vẽ.",
    SHOPDRAWING:"Chọn boundary/khung chi tiết/trần và đối tượng liên quan theo workflow.",
    LAYOUT:"Chọn Layout/Viewport/khung bản vẽ theo workflow.",
        TOOLS:"Phụ thuộc công cụ; thường không cần selection ban đầu."
  };
  const centerStep:Record<string,string>={
    TEXT:"Kiểm tra đúng loại Text và phạm vi trước khi xác nhận thay đổi hàng loạt.",
    BLOCK:"Kiểm tra block name, Tag, Dynamic properties và Xref trước khi thay đổi.",
    FIELD:"Không xóa geometry tham chiếu trước khi kiểm tra Field/ObjectID.",
    GEOMETRY:"Preview hình học, kiểm tra phía/điểm/ngưỡng trước thao tác phá hủy.",
    DIMENSION:"Kiểm tra DIMSTYLE, layer DIM, scale Model/Paper trước khi tạo dim.",
    LAYER:"Kiểm tra layer name, color, linetype, lineweight và phạm vi áp dụng.",
    QUANTITY:"Kiểm tra đơn vị, layer, loại entity và phạm vi thống kê.",
    SHOPDRAWING:"Kiểm tra boundary, scale, layer và quy tắc shopdrawing trước khi tạo.",
    LAYOUT:"Kiểm tra Paper size, viewport scale, CTB/STB/Page Setup trước batch.",
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
      "Bộ 44 Lisp chính thức đã được tích hợp. Nếu dòng này chưa match file, kiểm tra trạng thái 44/44; chỉ Nạp file/thư mục khi dùng Lisp bổ sung.",
      "Kiểm tra cột File nguồn phải hiện dấu ✓ và nhãn HNL đối với Lisp tích hợp.",
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
