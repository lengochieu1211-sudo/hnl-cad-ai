import { LispCenter, LispSupportMode } from './lispFeatureCatalog';
export interface CanonicalLispTool{id:string;name:string;center:LispCenter;summary:string;sources:string[];mode:LispSupportMode;priority:'P0'|'P1'|'P2'|'P3';}
export const CANONICAL_LISP_TOOLS:CanonicalLispTool[]=[
{"id":"SMART_TEXT","name":"Smart Text Editor","center":"TEXT","summary":"Copy/sửa/Find-Replace/đổi hoa-thường/chuẩn hóa mã chữ và dịch thuật.","sources":["QQ/QED/QE/QC/QV/Q1/Q1A/T2A/SW/Q2-Q6/QT","FRM","CHANGETEXTSTYLE","TRANSCAD"],"mode":"HYBRID","priority":"P0"},
{"id":"TEXT_FORMULA","name":"Text Calculator","center":"TEXT","summary":"Tính công thức và ghi kết quả vào Text/Attribute.","sources":["APC/APS/APE"],"mode":"NATIVE","priority":"P1"},
{"id":"ATTRIBUTE_MANAGER","name":"Attribute Manager","center":"BLOCK","summary":"Công thức Attribute theo Tag, đánh số và dời Attribute.","sources":["APA","INC/MVAT"],"mode":"HYBRID","priority":"P0"},
{"id":"BLOCK_MANAGER","name":"Block Manager","center":"BLOCK","summary":"Thống kê/replace/filter/edit/add geometry cho Block/Dynamic Block.","sources":["ATL/ATK/AT1/DY1/ATC/NDC/BLC","ATB/ADDTOBLOCK","BFM","RBL1","QB/BS/BQ/BN"],"mode":"HYBRID","priority":"P0"},
{"id":"FIELD_DOCTOR","name":"Field Doctor","center":"FIELD","summary":"Kiểm tra Field lỗi/trùng/link/source, locate object và offset giữ Field.","sources":["OFT","CFM/APFIELD/CFE/CFA/CFL/CFS","FIELDOBJECTS"],"mode":"AUTOCAD","priority":"P0"},
{"id":"FIELD_QUANTITY","name":"Field Quantity","center":"FIELD","summary":"Tạo Field tổng Area/Length/Block Count trên đối tượng AutoCAD.","sources":["AF/AFM/LF/LFM/BF"],"mode":"AUTOCAD","priority":"P0"},
{"id":"SMART_OFFSET_BREAK","name":"Offset / Break / Trim","center":"GEOMETRY","summary":"Offset hàng loạt đúng phía, Break/Trim nhiều điểm, transaction/rollback.","sources":["MOF","BRK"],"mode":"HYBRID","priority":"P0"},
{"id":"GEOMETRY_CONVERT","name":"Geometry Convert","center":"GEOMETRY","summary":"Circle→Polyline và đường width→hai biên.","sources":["C2P","APTD"],"mode":"NATIVE","priority":"P1"},
{"id":"INTERSECTION_TOOLS","name":"Intersection Tools","center":"GEOMETRY","summary":"Tìm giao điểm, tạo Point/Text/Block và đánh số.","sources":["INT","IPMX"],"mode":"NATIVE","priority":"P2"},
{"id":"QUICK_DIMENSION","name":"Quick Dimension Studio","center":"DIMENSION","summary":"DN/DNC + Join Dim + Radius/Arc statistics trong một workflow Dim.","sources":["DN/DNC","JD","TKD"],"mode":"HYBRID","priority":"P0"},
{"id":"LAYER_MANAGER","name":"Layer / Property Manager","center":"LAYER","summary":"Tạo Layer từ Excel và đổi/khôi phục màu kể cả nested block.","sources":["TLE","DM/RDM/FDM"],"mode":"HYBRID","priority":"P0"},
{"id":"QUANTITY_BOQ","name":"Quantity / BOQ Studio","center":"QUANTITY","summary":"Area/Length/Volume, group/sort, Excel/CAD Table.","sources":["TKT","CALCULATEAREAPERIMETER"],"mode":"HYBRID","priority":"P0"},
{"id":"SHOPDRAWING_STUDIO","name":"Shopdrawing / Detail Studio","center":"SHOPDRAWING","summary":"Vẽ xương trần, tính tấm trần nổi và trích chi tiết Model/Layout.","sources":["VXT","DEMTC","TCD"],"mode":"HYBRID","priority":"P0"},
{"id":"LAYOUT_MANAGER","name":"Layout Manager","center":"LAYOUT","summary":"Rename/sort/copy Layout và tạo danh mục bản vẽ.","sources":["CLM","RNL","DMBV"],"mode":"HYBRID","priority":"P0"},
{"id":"VIEWPORT_MANAGER","name":"Viewport Manager","center":"LAYOUT","summary":"Model→Paper, tạo/lock viewport, copy VP Freeze và Layout light mode.","sources":["MS2PS","TAOKHUNG","APT","CVP1/CVP2","LL/BT"],"mode":"AUTOCAD","priority":"P0"},
{"id":"LAYOUT_AUTOMATION","name":"Layout Automation Studio","center":"LAYOUT","summary":"Tạo hàng loạt Layout từ khung, title block và viewport.","sources":["TKL"],"mode":"HYBRID","priority":"P0"},
{"id":"PUBLISH_SETUP","name":"Publish Setup","center":"LAYOUT","summary":"Batch Page Setup và PSLTSCALE chuẩn bị Publish.","sources":["SAP","PSL"],"mode":"AUTOCAD","priority":"P0"},
{"id":"LEGACY_LISP_TOOLING","name":"Legacy Lisp Tooling","center":"TOOLS","summary":"Compile/đóng gói Lisp FAS/VLX.","sources":["LTFV"],"mode":"AUTOCAD","priority":"P3"},
];
