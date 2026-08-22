import React, { useMemo, useState } from "react";
import { BookOpen, ChevronRight, Search, X, Wrench, AlertTriangle, ArrowRightLeft, CheckCircle2, Clock3 } from "lucide-react";

type HelpStatus = "READY" | "PARTIAL" | "ROADMAP" | "REQUIRES_AUTOCAD";
type HelpItem = {
  id:string; title:string; category:string; status:HelpStatus; risk:"LOW"|"MEDIUM"|"HIGH";
  purpose:string; prerequisites:string[]; steps:string[]; result:string; errors:string[]; notes?:string[];
};

const items:HelpItem[] = [
  {id:"START",title:"Bắt đầu & quản lý dự án",category:"CƠ BẢN",status:"READY",risk:"LOW",
   purpose:"Tạo, mở, lưu và phục hồi workspace HNL.",prerequisites:["Không cần AutoCAD."],
   steps:["Mở Start Center.","Chọn New Drawing / Open Project-DXF / Continue Recovery.","Theo dõi dấu ● cạnh tên file: có dấu nghĩa là chưa lưu.","Ctrl+S để lưu; nên Save As trước khi Cleanup hoặc chuyển đổi lớn."],
   result:"Workspace được tạo/mở an toàn.",errors:["Không mở được file: vào Diagnostics và copy HNL-FILE-OPEN-*.", "File DXF rỗng: kiểm tra DXF ASCII và entity được hỗ trợ."]},
  {id:"PRO2D",title:"2D Professional Tool Center",category:"CAD 2D",status:"PARTIAL",risk:"MEDIUM",
   purpose:"Gom 44 Lisp tham khảo thành 6 workflow thay vì 44 nút rời.",prerequisites:["Save As trước các thao tác batch.","AutoCAD Bridge cần Connected khi dùng Field ObjectID/Layout/Page Setup/DWG native."],
   steps:["Ribbon 2D Professional → mở Tool Center.","Text & Attribute: Find/Replace, UPPER/lower/Title Case, đánh số Attribute, Replace Block.","Field Doctor: kiểm tra Field metadata nội bộ; Field DWG thật dùng AutoCAD Bridge.","Geometry: mở Geometry Doctor/Cleanup; các lệnh MOF/BRK/C2P/APTD được quản lý trong cùng nhóm.","Quick Dimension: chọn geometry rồi Quick Dim Selection; DN/DNC nâng cao vẫn Hybrid.","Quantity / BOQ: xem Length/Area/Count theo Layer/Type; mở Ceiling Studio cho VXT/DEMTC.","Layout Automation: chuyển sang Plot/Publish/Sheet Set cho TKL/RNL/TAOKHUNG/SAP/PSL workflow.","Tab 44 Lisp để tra command, chức năng, mode Native/Hybrid/AutoCAD và priority."],
   result:"Workflow CAD 2D gọn hơn, dễ tìm lệnh và không phụ thuộc việc nhớ tên Lisp.",errors:["Lệnh AUTOCAD không chạy Standalone.","HYBRID chỉ thực hiện phần native nếu Bridge offline; phần DWG/Field/Layout native phải qua AutoCAD.","Nếu thao tác batch cho kết quả bất thường: Undo và kiểm tra Diagnostics."],
   notes:["Các Lisp được dùng như nguồn yêu cầu/chức năng. Không copy mù code bên thứ ba nếu chưa rõ giấy phép."]},
  {id:"CAD2D",title:"Vẽ & hiệu chỉnh CAD 2D",category:"CAD 2D",status:"READY",risk:"MEDIUM",
   purpose:"Vẽ và hiệu chỉnh hình học 2D phục vụ shopdrawing.",prerequisites:["Kiểm tra Layer, đơn vị và phạm vi bản vẽ."],
   steps:["Chọn Workbench HNL CAD.","Dùng Ribbon Vẽ & Chỉnh sửa.","Chọn đối tượng trước các lệnh cần selection.","Undo/Redo sau thao tác để kiểm tra kết quả.","Các lệnh chưa có geometry kernel an toàn sẽ báo Diagnostics thay vì giả thành công."],
   result:"Hình học 2D trong workspace HNL.",errors:["Trim/Extend/Fillet/Array có thể yêu cầu AutoCAD Bridge ở bản hiện tại.","Nếu nút không chạy: gửi mã HNL-CMD-*."],
   notes:["Ưu tiên độ an toàn dữ liệu hơn số lượng lệnh."]},
  {id:"CLEAN",title:"Làm sạch CAD 2D",category:"CAD 2D",status:"READY",risk:"HIGH",
   purpose:"Dọn bản vẽ trước khi gửi SketchUp/AutoCAD hoặc Publish.",prerequisites:["Save As file nguồn trước khi chạy.","Đặt tolerance phù hợp đơn vị mm."],
   steps:["Công cụ → CAD 2D ⇄ SketchUp Bridge.","Tab Làm sạch 2D.","Chọn tolerance và ngưỡng đoạn rác.","Preview & Apply Cleanup.","Đọc Before/After rồi mới xác nhận."],
   result:"Giảm line trùng, đoạn rác, Join line đồng tuyến và chuẩn hóa layer.",errors:["HNL-CLEAN-ERR: không áp dụng tiếp, giữ file gốc và gửi Diagnostic Report.","Nếu số đối tượng giảm bất thường: Undo và giảm tolerance."]},
  {id:"CAD_TO_SU",title:"CAD 2D → SketchUp",category:"SKETCHUP",status:"PARTIAL",risk:"MEDIUM",
   purpose:"Đưa linework CAD sạch sang SketchUp để tiếp tục dựng model.",prerequisites:["Cleanup trước.","Kiểm tra đơn vị mm và gốc tọa độ."],
   steps:["Mở CAD 2D ⇄ SketchUp Bridge.","Chạy Kiểm tra trước khi xuất.","Chọn Toàn Model hoặc Selected.","Tạo gói SketchUp 2D.","HNL sinh JSON vector + Ruby importer.","Trong SketchUp nhập vào group HNL_CAD_IMPORT và kiểm tra Tags/origin."],
   result:"Linework vector 2D được chuyển sang SketchUp qua gói Bridge.",errors:["HNL-SU-EXPORT-ERR: sửa geometry/layer lỗi rồi xuất lại.","Bản hiện tại chưa ghi file .SKP binary trực tiếp."],
   notes:["Layer CAD được giữ thành Tag metadata; Block có metadata để chuẩn bị Component mapping."]},
  {id:"SU_TO_CAD",title:"SketchUp → CAD 2D",category:"SKETCHUP",status:"PARTIAL",risk:"HIGH",
   purpose:"Lấy Scene/Section từ SketchUp, chiếu thành linework 2D để chỉnh tiếp trong HNL/AutoCAD và Publish.",prerequisites:["Cài HNL CAD AI Bridge v2.0.2.rbz trong SketchUp.","Nên chọn Parallel Projection và Scene/Section rõ ràng."],
   steps:["Trong SketchUp chọn Scene/Section cần xuất.","Extensions → HNL CAD AI Bridge → Export Scene/Section → HNL CAD.","Trong HNL mở Công cụ → CAD 2D ⇄ SketchUp Bridge → SketchUp → CAD.","Chọn HNL_SketchUp_Scene.json.","Có thể chạy AI gợi ý Layer/Nét; AI chỉ gợi ý metadata.","Bấm Project → CAD 2D để đưa linework vào workspace hoặc Xuất DXF 2D để tạo file CAD trung gian.","Chạy Cleanup và kiểm tra layer/nét.","Nếu AutoCAD Bridge online: gửi DXF/HNL sang AutoCAD, SaveAs DWG và Publish."],
   result:"Scene SketchUp được chiếu thành linework 2D và có thể xuất DXF.",errors:["Perspective vẫn có thể project nhưng nên dùng Parallel Projection để bản vẽ kỹ thuật ổn định.","Section crossing hiện chỉ là metadata; chưa coi là CUT contour thật.","True occlusion hidden-line và silhouette classifier vẫn cần phát triển sâu hơn.","Không raster hóa thành ảnh nếu mục tiêu là CAD editable."],
   notes:["Nên ưu tiên orthographic projection; Perspective chỉ dùng tham khảo.","Cần Link ID để Update lần sau thay vì import chồng."]},
  {id:"SU_UPDATE",title:"Đồng bộ CAD ⇄ SketchUp",category:"SKETCHUP",status:"ROADMAP",risk:"HIGH",
   purpose:"Cập nhật hai chiều mà không tạo bản trùng.",prerequisites:["Link ID bền vững cho entity/tag/component.","Có snapshot phiên trước."],
   steps:["Gắn HNL Link ID cho entity CAD và entity SketchUp.","Khi sync, tính diff Added / Updated / Deleted / Unchanged.","Hiện Preview Changes.","Cho phép Skip/Accept theo nhóm.","Commit thành một transaction; lỗi thì rollback."],
   result:"Update Existing Import thay vì import chồng model.",errors:["Xung đột khi cả CAD và SketchUp cùng sửa một đối tượng phải yêu cầu người dùng chọn nguồn ưu tiên."],
   notes:["Đây là tính năng quan trọng cho workflow lâu dài."]},
  {id:"LAYOUT",title:"Layout, Viewport & Publish",category:"PUBLISH",status:"PARTIAL",risk:"MEDIUM",
   purpose:"Chuẩn bị sheet và xuất bản vẽ.",prerequisites:["Kiểm tra khung tên, tỷ lệ, font, layer plot."],
   steps:["Tạo Layout A3/A4.","Auto Fit và khóa Viewport.","Kiểm tra khung tên.","Chạy Audit/Cleanup.","Khi AutoCAD Bridge kết nối: kiểm tra Page Setup/CTB và Publish."],
   result:"Sheet sẵn sàng in/PDF.",errors:["DWG/Layout/Field native đầy đủ cần AutoCAD Bridge."],
   notes:["Publish Center v1.8 đã có PDF/Printer/Sheet Set nội bộ. CTB/Xref/Page Setup native vẫn cần AutoCAD Bridge."]},
  {id:"PLOT_PUBLISH",title:"Plot / Publish / Sheet Set",category:"PUBLISH",status:"PARTIAL",risk:"MEDIUM",
   purpose:"Xuất PDF từ Model, publish nhiều sheet/layout, chọn máy in và quản lý Sheet Set nhẹ.",
   prerequisites:["Chạy trong EXE Electron để lấy danh sách máy in Windows.","Kiểm tra Layout/Viewport/Layer trước khi Publish.","DWG CTB/STB/PC3/PMP/.DST native cần AutoCAD Bridge."],
   steps:["Mở Ribbon Publish → Plot / Publish / Sheet Set.","Quick Plot: chọn preset A4/A3/A1, Orientation, Scale, máy in và Copies.","Dùng Printer & Nét in để chỉnh lineweight/color override riêng cho từng Layer; override chỉ ảnh hưởng bản in, không sửa Layer gốc.","Xuất PDF Model hoặc gửi trực tiếp tới máy in Windows.","Publish: bật/tắt các sheet, tìm theo số/tên/layout; danh sách render 100 sheet/trang để giảm lag.","Dùng Batch Page Setup để áp Paper/Scale/Printer/Plot Style cho nhiều sheet.","Publish Selected → 1 PDF nhiều trang.","Sheet Set: tạo từ Layout hiện tại, lưu/mở HNL Sheet Set JSON.","Nếu mở .DST, HNL nhận diện file nhưng chỉ sửa native khi AutoCAD Bridge có SheetSet API."],
   result:"PDF Model, PDF nhiều trang, Print queue và HNL Sheet Set có preflight.",
   errors:["HNL-PLOT-PDF-* khi xuất PDF lỗi.","HNL-PLOT-PRINT-* khi printer/driver lỗi.","HNL-PUBLISH-PDF-* khi publish nhiều trang lỗi.","HNL-SHEETSET-DST báo AutoCAD Bridge required khi mở .DST native."],
   notes:["Standalone multi-layout dùng viewport nội bộ để crop vùng Model khi có thể; Layout DWG phức tạp nhiều viewport, CTB/STB và Page Setup phải Native Publish qua AutoCAD để đảm bảo giống bản vẽ gốc."]},
  {id:"AI",title:"AI CAD",category:"AI",status:"PARTIAL",risk:"MEDIUM",
   purpose:"Phân tích, đề xuất và hỗ trợ thao tác CAD.",prerequisites:["Safe Mode nên bật.","AI online cần API key hợp lệ."],
   steps:["Mở AI Palette.","Mô tả yêu cầu.","Đọc kế hoạch/preview trước tác vụ thay đổi dữ liệu.","Xác nhận nếu tác vụ destructive."],
   result:"Đề xuất hoặc thao tác được hỗ trợ.",errors:["AI online lỗi: kiểm tra key/quota/network và Diagnostics.","Không dùng AI để giả kết quả khi geometry engine chưa hỗ trợ."]},
  {id:"ERROR",title:"Khi một chức năng không chạy",category:"HỖ TRỢ",status:"READY",risk:"LOW",
   purpose:"Thu thập lỗi đủ chi tiết để sửa source.",prerequisites:["Không xóa log trước khi copy."],
   steps:["Ghi tên nút/lệnh vừa dùng.","Công cụ → Trung tâm chẩn đoán lỗi.","Chọn event mới nhất.","Copy báo cáo hoặc Xuất TXT.","Gửi mã lỗi + thao tác trước lỗi + file mẫu + ảnh màn hình nếu có."],
   result:"Có dữ liệu đủ để truy nguyên lỗi.",errors:["Nếu app còn chạy, Save As file mới trước khi thử lại."]}
];

const statusLabel:Record<HelpStatus,string>={READY:"Hoạt động",PARTIAL:"Một phần",ROADMAP:"Đang phát triển",REQUIRES_AUTOCAD:"Cần AutoCAD"};
const statusClass:Record<HelpStatus,string>={READY:"text-emerald-300 border-emerald-800 bg-emerald-950/30",PARTIAL:"text-amber-300 border-amber-800 bg-amber-950/30",ROADMAP:"text-violet-300 border-violet-800 bg-violet-950/30",REQUIRES_AUTOCAD:"text-cyan-300 border-cyan-800 bg-cyan-950/30"};

export const UsageGuideModal:React.FC<{isOpen:boolean;onClose:()=>void}> = ({isOpen,onClose})=>{
  const [query,setQuery]=useState(""); const [active,setActive]=useState("START");
  const filtered=useMemo(()=>{const q=query.trim().toLowerCase();return q?items.filter(i=>[i.title,i.category,i.purpose,...i.steps,...i.errors].join(" ").toLowerCase().includes(q)):items;},[query]);
  const item=items.find(i=>i.id===active)||filtered[0]||items[0];
  if(!isOpen)return null;
  const badge=(s:HelpStatus)=><span className={`px-2 py-0.5 rounded border text-[10px] ${statusClass[s]}`}>{statusLabel[s]}</span>;
  return <div className="fixed inset-0 z-[190] bg-black/75 backdrop-blur-sm flex items-center justify-center p-3"><div className="w-full max-w-6xl h-[84vh] rounded-2xl border border-neutral-700 bg-[#17191d] shadow-2xl overflow-hidden flex flex-col">
    <div className="px-5 py-4 border-b border-neutral-800 bg-[#111317] flex items-center gap-4"><div className="min-w-0"><div className="font-bold text-white flex items-center gap-2"><BookOpen className="w-5 h-5 text-cyan-400"/> HNL Help Center</div><div className="text-xs text-neutral-500 mt-1">Hướng dẫn theo chức năng, trạng thái hỗ trợ, lỗi thường gặp và roadmap SketchUp ⇄ CAD.</div></div><div className="ml-auto w-[360px] max-w-[40vw] relative"><Search className="w-4 h-4 absolute left-3 top-2.5 text-neutral-500"/><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Tìm: SketchUp, Cleanup, Viewport, lỗi..." className="w-full pl-9 pr-3 py-2 rounded bg-neutral-900 border border-neutral-700 text-sm"/></div><button onClick={onClose} className="p-2 rounded hover:bg-neutral-800"><X className="w-5 h-5"/></button></div>
    <div className="flex flex-1 min-h-0"><aside className="w-[310px] border-r border-neutral-800 p-3 overflow-auto"><div className="text-[10px] uppercase tracking-wide text-neutral-600 px-2 pb-2">Chức năng</div>{filtered.map(i=><button key={i.id} onClick={()=>setActive(i.id)} className={`w-full text-left px-3 py-2.5 rounded-lg mb-1 border ${active===i.id?"bg-cyan-950/30 border-cyan-800":"border-transparent hover:bg-neutral-800"}`}><div className="flex items-center justify-between gap-2"><span className="text-sm text-neutral-200">{i.title}</span>{badge(i.status)}</div><div className="text-[10px] text-neutral-600 mt-1">{i.category}</div></button>)}</aside>
      <main className="flex-1 overflow-auto p-6"><div className="flex items-start justify-between gap-4"><div><div className="text-xs text-neutral-500">{item.category}</div><h2 className="text-2xl font-bold text-white mt-1">{item.title}</h2></div>{badge(item.status)}</div>
        <div className="mt-5 grid grid-cols-3 gap-3"><div className="p-3 rounded bg-[#1d2025] border border-neutral-800"><div className="text-[10px] uppercase text-neutral-600">Mục đích</div><div className="text-sm text-neutral-300 mt-1">{item.purpose}</div></div><div className="p-3 rounded bg-[#1d2025] border border-neutral-800"><div className="text-[10px] uppercase text-neutral-600">Rủi ro</div><div className={`text-sm mt-1 ${item.risk==="HIGH"?"text-red-300":item.risk==="MEDIUM"?"text-amber-300":"text-emerald-300"}`}>{item.risk}</div></div><div className="p-3 rounded bg-[#1d2025] border border-neutral-800"><div className="text-[10px] uppercase text-neutral-600">Kết quả</div><div className="text-sm text-neutral-300 mt-1">{item.result}</div></div></div>
        <Section icon={<CheckCircle2 className="w-4 h-4 text-emerald-400"/>} title="Điều kiện trước khi chạy" lines={item.prerequisites}/>
        <Section icon={<ChevronRight className="w-4 h-4 text-cyan-400"/>} title="Cách thực hiện" lines={item.steps} numbered/>
        <Section icon={<AlertTriangle className="w-4 h-4 text-amber-400"/>} title="Lỗi thường gặp / giới hạn" lines={item.errors}/>
        {item.notes?.length?<Section icon={<Wrench className="w-4 h-4 text-violet-400"/>} title="Ghi chú phát triển" lines={item.notes}/>:null}
        {item.id==="SU_TO_CAD"&&<div className="mt-5 p-4 rounded-xl border border-violet-800 bg-violet-950/20"><div className="flex gap-2 items-center font-semibold text-violet-300"><ArrowRightLeft className="w-4 h-4"/> Workflow mục tiêu</div><div className="mt-2 text-sm text-neutral-300 leading-7">SketchUp Scene/Section → Orthographic Projection → Cut / Visible / Hidden / Silhouette → Layer Mapping → Color/Linetype/Lineweight → Cleanup → DXF → AutoCAD Bridge → DWG/Layout → Publish.</div></div>}
        {item.status==="ROADMAP"&&<div className="mt-4 flex gap-2 items-center text-xs text-neutral-500"><Clock3 className="w-4 h-4"/> Đây là thiết kế/roadmap, chưa được hiển thị như chức năng đã hoàn thành.</div>}
      </main></div>
  </div></div>;
};

const Section:React.FC<{icon:React.ReactNode;title:string;lines:string[];numbered?:boolean}>=({icon,title,lines,numbered})=><section className="mt-5"><div className="flex items-center gap-2 text-sm font-semibold text-neutral-200">{icon}{title}</div><div className="mt-2 space-y-2">{lines.map((x,i)=><div key={i} className="flex gap-3 p-3 rounded bg-[#1d2025] border border-neutral-800"><div className="w-6 h-6 shrink-0 rounded-full bg-neutral-800 text-neutral-400 flex items-center justify-center text-xs">{numbered?i+1:"•"}</div><div className="text-sm text-neutral-300 leading-6">{x}</div></div>)}</div></section>;
