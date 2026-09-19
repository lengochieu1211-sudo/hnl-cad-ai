import React,{useEffect,useMemo,useState} from "react";
import { BookOpen, Calculator, ChevronRight, CircleGauge, FileInput, FolderInput, Hash, Play, Replace, Ruler, Search, Upload, X } from "lucide-react";
import { CadEntity,CadLayer } from "../../types/cad";
import { hnlFieldAudit,quantitySummary,quickDimensionBounds,renumberBlockAttribute,replaceBlockNames,smartFindReplace,transformTextCase } from "../../lib/lispInspiredEngine";
import { buildLispGuide,LISP_FEATURE_CATALOG,LISP_SOURCE_GROUP_LABELS,LISP_SOURCE_GROUP_ORDER,LispCenter,LispFeatureItem,LispSourceGroup,primaryLispCommand } from "../../lib/lispFeatureCatalog";
import { CANONICAL_LISP_TOOLS } from "../../lib/lispCanonicalTools";

type Diag={code:string;severity:"INFO"|"WARNING"|"ERROR"|"CRITICAL";title:string;message:string;command?:string;suggestion?:string;context?:Record<string,unknown>};
type CenterKey="TEXT"|"BLOCK"|"FIELD"|"GEOMETRY"|"DIMENSION"|"LAYER"|"QUANTITY"|"SHOPDRAWING"|"LAYOUT"|"TOOLS"|"SOURCES";

export const LispInspiredToolCenterModal:React.FC<{
 isOpen:boolean;onClose:()=>void;entities:CadEntity[];layers:CadLayer[];selectedIds:string[];initialTab?:CenterKey;
 autoCadConnected:boolean;
 onApplyEntities:(next:CadEntity[],summary:string)=>void;
 onOpenProfessionalAudit:()=>void;onOpenPlotPublish:()=>void;onOpenCeiling:()=>void;onOpenLispBuilder:()=>void;
 onBridgeAction?:(action:string,payload?:any)=>Promise<any>;
 onDiagnostic:(d:Diag)=>void;
}> = ({isOpen,onClose,entities,layers,selectedIds,initialTab,autoCadConnected,onApplyEntities,onOpenProfessionalAudit,onOpenPlotPublish,onOpenCeiling,onOpenLispBuilder,onBridgeAction,onDiagnostic})=>{
 const [tab,setTab]=useState<CenterKey>("TOOLS");
 useEffect(()=>{if(isOpen&&initialTab)setTab(initialTab)},[isOpen,initialTab]);
 const [findText,setFindText]=useState("");const [replaceText,setReplaceText]=useState("");const [replaceScope,setReplaceScope]=useState<"ALL"|"SELECTED">("SELECTED");
 const [attrTag,setAttrTag]=useState("NO");const [attrPrefix,setAttrPrefix]=useState("");const [attrStart,setAttrStart]=useState(1);
 const [blockFrom,setBlockFrom]=useState("");const [blockTo,setBlockTo]=useState("");
 const [query,setQuery]=useState("");
 const [sourceQuery,setSourceQuery]=useState("");
 const [sourceGroupFilter,setSourceGroupFilter]=useState<"ALL"|LispSourceGroup>("ALL");
 const [lispFiles,setLispFiles]=useState<Array<{path:string;name:string;commands:string[];sizeBytes?:number;bundled?:boolean;origin?:string}>>(()=>{try{return JSON.parse(localStorage.getItem("hnl.lispSourceIndex.v1")||"[]")}catch{return []}});
 const [bundledLispStatus,setBundledLispStatus]=useState<{lispCount:number;expectedLispCount:number;complete:boolean;root?:string;legacyArx?:any[]} | null>(null);
 const [selectedSource,setSelectedSource]=useState<LispFeatureItem|null>(null);
 const [lispMessage,setLispMessage]=useState("");
 useEffect(()=>{localStorage.setItem("hnl.lispSourceIndex.v1",JSON.stringify(lispFiles.filter(x=>!x.bundled)))},[lispFiles]);
 useEffect(()=>{
   if(!isOpen)return;
   const loadBundled=async()=>{
     const native=(window as any).electronNative;
     const r=await native?.getBundledLispIndex?.();
     if(!r?.success)return;
     setBundledLispStatus({lispCount:r.lispCount||0,expectedLispCount:r.expectedLispCount||44,complete:Boolean(r.complete),root:r.root,legacyArx:r.legacyArx||[]});
     const bundled=Array.isArray(r.items)?r.items:[];
     setLispFiles(prev=>{
       const custom=prev.filter(x=>!x.bundled);
       const byPath=new Map<string,any>();
       for(const x of [...bundled,...custom])byPath.set(x.path,x);
       return [...byPath.values()];
     });
   };
   void loadBundled();
 },[isOpen]);
 const fieldAudit=useMemo(()=>hnlFieldAudit(entities),[entities]);
 const quantities=useMemo(()=>quantitySummary(entities),[entities]);
 const filteredTools=useMemo(()=>{const q=query.trim().toLowerCase();return CANONICAL_LISP_TOOLS.filter(x=>!q||`${x.id} ${x.name} ${x.summary} ${x.sources.join(" ")} ${x.center} ${x.mode}`.toLowerCase().includes(q));},[query]);
 const filteredSources=useMemo(()=>{
   const q=sourceQuery.trim().toLowerCase();
   return LISP_FEATURE_CATALOG.filter(x=>
     (sourceGroupFilter==="ALL"||x.sourceGroup===sourceGroupFilter) &&
     (!q||`${x.commands} ${x.name} ${x.summary} ${x.sourceGroup} ${x.sourceFile} ${x.mode}`.toLowerCase().includes(q))
   );
 },[sourceQuery,sourceGroupFilter]);
 const sourceCommands=(f:LispFeatureItem)=>f.commands.split("/").map(x=>x.trim().toUpperCase()).filter(x=>x&&!x.includes("-"));
 const matchedFiles=(f:LispFeatureItem)=>{
   const exact=lispFiles.filter(file=>String(file.name||"").toLowerCase()===f.sourceFile.toLowerCase());
   if(exact.length)return exact;
   return lispFiles.filter(file=>file.commands?.some(c=>sourceCommands(f).includes(String(c).toUpperCase())));
 };
 const importLisp=async(kind:"FILES"|"FOLDER")=>{
   const native=(window as any).electronNative;
   const r=kind==="FOLDER"?await native?.selectLispFolder?.():await native?.selectLispFiles?.();
   if(!r?.success){if(!r?.canceled)setLispMessage("Không đọc được Lisp nguồn.");return;}
   const merged=[...lispFiles];
   for(const item of r.items||[]){
     const i=merged.findIndex(x=>x.path===item.path);
     if(i>=0)merged[i]=item;else merged.push(item);
   }
   setLispFiles(merged);
   setLispMessage(`Đã lập chỉ mục ${r.items?.length||0} file Lisp${r.root?` từ ${r.root}`:""}.`);
 };
 const loadLisp=async(f:LispFeatureItem,run:boolean)=>{
   if(!autoCadConnected||!onBridgeAction){setLispMessage("Cần AutoCAD + HNL Bridge Connected.");return;}
   const files=matchedFiles(f);
   if(!files.length){setLispMessage(`Chưa tìm thấy file .lsp định nghĩa ${f.commands}. Hãy Nạp file/thư mục Lisp nguồn.`);return;}
   const file=files[0];
   const cmd=run?primaryLispCommand(f.commands):"";
   try{
     const payload={filePath:file.path,sourceFile:f.sourceFile,fileName:file.name,bundled:Boolean(file.bundled),runCommand:cmd};
     const r=await onBridgeAction("LOAD_LISP_FILE",payload);
     if(r?.ok===false){
       const msg=String(r?.error||r?.reason||r?.detail?.reason||"Bridge LOAD_LISP_FILE failed");
       throw new Error(msg==="AUTOCAD_BRIDGE_TIMEOUT"
         ? "AutoCAD đang bận hoặc đang chờ lệnh. Nhấn Esc trong AutoCAD rồi Nạp lại Lisp."
         : msg);
     }
     setLispMessage(run
       ? `Đã gửi LOAD + ${cmd}. Xem AutoCAD Command Line để xác nhận kết quả.`
       : `Đã gửi LOAD ${file.name}. Xem AutoCAD Command Line để xác nhận.`);
     onDiagnostic({code:"HNL-LISP-LOAD",severity:"INFO",title:"Legacy Lisp",message:`${run?"LOAD+RUN":"LOAD"} ${file.name}${cmd?` → ${cmd}`:""}`,command:"LOAD_LISP_FILE",context:{file:file.name,bundled:Boolean(file.bundled),path:file.path,bridgeResult:r?.result||r}});
   }catch(e:any){
     setLispMessage(e?.message||String(e));
     onDiagnostic({code:"HNL-LISP-LOAD-ERR",severity:"ERROR",title:"Nạp Lisp thất bại",message:e?.message||String(e),command:"LOAD_LISP_FILE",context:{file:file.name,bundled:Boolean(file.bundled),path:file.path,commands:f.commands}});
   }
 };
 if(!isOpen)return null;
 const apply=(next:CadEntity[],summary:string,code:string,command:string)=>{onApplyEntities(next,summary);onDiagnostic({code,severity:"INFO",title:"2D Professional Tool",message:summary,command});};
 const doReplace=()=>{try{if(!findText)throw new Error("Chưa nhập nội dung cần tìm.");if(replaceScope==="SELECTED"&&!selectedIds.length)throw new Error("Chưa chọn đối tượng.");const r=smartFindReplace(entities,[{find:findText,replace:replaceText}],selectedIds,replaceScope,false);apply(r.entities,`Find/Replace: ${r.changed} đối tượng thay đổi.`,"HNL-TEXT-FRM","SMART_FIND_REPLACE");}catch(e:any){onDiagnostic({code:"HNL-TEXT-FRM-ERR",severity:"ERROR",title:"Find/Replace thất bại",message:e?.message||String(e),command:"SMART_FIND_REPLACE"});}};
 const doCase=(mode:"UPPER"|"LOWER"|"TITLE")=>{try{if(!selectedIds.length)throw new Error("Chọn Text/MText/MLeader/Block trước.");const r=transformTextCase(entities,selectedIds,mode);apply(r.entities,`${mode}: ${r.changed} đối tượng.`,"HNL-TEXT-CASE","TEXT_CASE");}catch(e:any){onDiagnostic({code:"HNL-TEXT-CASE-ERR",severity:"ERROR",title:"Đổi chữ thất bại",message:e?.message||String(e),command:"TEXT_CASE"});}};
 const renumber=()=>{try{if(!selectedIds.length)throw new Error("Chọn Block Attribute cần đánh số.");const r=renumberBlockAttribute(entities,selectedIds,attrTag,attrStart,attrPrefix);apply(r.entities,`Attribute ${attrTag}: đã đánh số ${r.changed} block.`,"HNL-ATTR-INC","ATTRIBUTE_RENUMBER");}catch(e:any){onDiagnostic({code:"HNL-ATTR-INC-ERR",severity:"ERROR",title:"Đánh số Attribute thất bại",message:e?.message||String(e),command:"ATTRIBUTE_RENUMBER"});}};
 const replaceBlock=()=>{try{if(!blockFrom||!blockTo)throw new Error("Nhập tên block nguồn và block đích.");const r=replaceBlockNames(entities,selectedIds,blockFrom,blockTo);apply(r.entities,`Replace Block: ${r.changed} instance ${blockFrom} → ${blockTo}.`,"HNL-BLOCK-RBL1","REPLACE_BLOCK");}catch(e:any){onDiagnostic({code:"HNL-BLOCK-RBL1-ERR",severity:"ERROR",title:"Replace Block thất bại",message:e?.message||String(e),command:"REPLACE_BLOCK"});}};
 const quickDim=()=>{try{if(!selectedIds.length)throw new Error("Chưa chọn geometry.");const layer=layers.find(l=>/DIM/i.test(l.name))?.name||layers.find(l=>l.name==="0")?.name||layers[0]?.name||"0";const r=quickDimensionBounds(entities,selectedIds,layer);apply(r.entities,`Quick Dimension: tạo ${r.created.length} dim bbox trên layer ${layer}.`,"HNL-DIM-DN","QUICK_DIM_BOUNDS");}catch(e:any){onDiagnostic({code:"HNL-DIM-DN-ERR",severity:"ERROR",title:"Quick Dimension thất bại",message:e?.message||String(e),command:"QUICK_DIM_BOUNDS"});}};
 const tabs:Array<[CenterKey,string]>=[
   ["TEXT","Text"],
   ["BLOCK","Block / Attribute"],
   ["FIELD","Field / Links"],
   ["GEOMETRY","Geometry"],
   ["DIMENSION","Dimension"],
   ["LAYER","Layer / Data"],
   ["QUANTITY","Quantity / BOQ"],
   ["SHOPDRAWING","Shopdrawing"],
   ["LAYOUT","Layout / Publish"],
   ["TOOLS","Công cụ chuẩn"],
   ["SOURCES","44 Lisp nguồn"],
 ];
 const count=(centers:LispCenter[])=>LISP_FEATURE_CATALOG.filter(x=>centers.includes(x.center)).length;
 return <div className="fixed inset-0 z-[200] bg-black/80 flex items-center justify-center p-3"><div className="w-full max-w-7xl h-[88vh] bg-[#17191d] border border-neutral-700 rounded-2xl shadow-2xl flex flex-col overflow-hidden">
  <div className="px-5 py-4 bg-[#111317] border-b border-neutral-800 flex justify-between"><div><div className="font-bold text-white flex items-center gap-2"><CircleGauge className="w-5 h-5 text-cyan-400"/> HNL 2D Professional Tool Center</div><div className="text-xs text-neutral-500 mt-1">Tái cấu trúc từ 44 Lisp: Native HNL trước, Hybrid khi cần DWG/Field/Layout native, AutoCAD Bridge cho ObjectID/CHSPACE/Page Setup.</div></div><button onClick={onClose}><X/></button></div>
  <div className="p-3 flex flex-wrap gap-1 border-b border-neutral-800">{tabs.map(([k,l])=><button key={k} onClick={()=>setTab(k)} className={`px-4 py-2 rounded text-sm whitespace-nowrap ${tab===k?"bg-cyan-500 text-black":"bg-neutral-800 text-neutral-300"}`}>{l}</button>)}</div>
  <div className="p-5 overflow-auto flex-1">
   {tab==="TEXT"&&<div className="grid grid-cols-2 gap-5">
    <Panel title={`Smart Text • ${count(["TEXT"])} Lisp nguồn`}><div className="grid grid-cols-2 gap-2"><input value={findText} onChange={e=>setFindText(e.target.value)} placeholder="Tìm..." className="bg-neutral-900 border border-neutral-700 rounded p-2"/><input value={replaceText} onChange={e=>setReplaceText(e.target.value)} placeholder="Thay bằng..." className="bg-neutral-900 border border-neutral-700 rounded p-2"/></div><div className="mt-2 flex gap-2 items-center"><select value={replaceScope} onChange={e=>setReplaceScope(e.target.value as any)} className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"><option value="SELECTED">Selected ({selectedIds.length})</option><option value="ALL">All drawing</option></select><button onClick={doReplace} className="px-3 py-2 rounded bg-cyan-500 text-black font-semibold flex gap-2"><Replace className="w-4 h-4"/>Find / Replace</button></div><div className="mt-4 flex gap-2"><button onClick={()=>doCase("UPPER")} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">UPPER</button><button onClick={()=>doCase("LOWER")} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">lower</button><button onClick={()=>doCase("TITLE")} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">Title Case</button></div></Panel>
    <Panel title="5 Lisp nguồn Text"><FeatureList centers={["TEXT"]}/></Panel>
   </div>}
   {tab==="BLOCK"&&<div className="grid grid-cols-2 gap-5">
    <Panel title={`Block / Attribute • ${count(["BLOCK"])} Lisp nguồn`}><div className="grid grid-cols-3 gap-2"><input value={attrTag} onChange={e=>setAttrTag(e.target.value)} placeholder="TAG" className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"/><input value={attrPrefix} onChange={e=>setAttrPrefix(e.target.value)} placeholder="Prefix" className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"/><input type="number" value={attrStart} onChange={e=>setAttrStart(Number(e.target.value)||1)} className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"/></div><button onClick={renumber} className="mt-2 px-3 py-2 rounded bg-emerald-500 text-black font-semibold flex gap-2"><Hash className="w-4 h-4"/>Đánh số Attribute</button><div className="mt-4 grid grid-cols-2 gap-2"><input value={blockFrom} onChange={e=>setBlockFrom(e.target.value)} placeholder="Block nguồn" className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"/><input value={blockTo} onChange={e=>setBlockTo(e.target.value)} placeholder="Block đích" className="bg-neutral-900 border border-neutral-700 rounded p-2 text-sm"/></div><button onClick={replaceBlock} className="mt-2 px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">Replace Block</button></Panel>
    <Panel title="7 Lisp nguồn Block / Attribute"><FeatureList centers={["BLOCK"]}/></Panel>
   </div>}
   {tab==="FIELD"&&<div className="space-y-4"><div className="grid grid-cols-3 gap-3"><Stat k="HNL Fields" v={fieldAudit.fieldCount}/><Stat k="Broken HNL Field" v={fieldAudit.brokenCount}/><Stat k="AutoCAD Bridge" v={autoCadConnected?"CONNECTED":"OFFLINE"}/></div><Panel title={`Field Doctor • ${count(["FIELD"])} Lisp nguồn`}><div className="text-sm text-neutral-300 leading-7">Chỉ gom OFT, CFM/APFIELD/CFE/CFA/CFL/CFS, AF/AFM/LF/LFM/BF và FIELDOBJECTS. Đây là nhóm cần ObjectID/Field native, không trộn với Quantity thường.</div>{fieldAudit.brokenCount>0&&<div className="mt-3 text-xs text-amber-300">Broken IDs: {fieldAudit.brokenIds.slice(0,30).join(", ")}</div>}<button onClick={onOpenProfessionalAudit} className="mt-3 px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">Mở Professional Audit Center</button></Panel></div>}
   {tab==="GEOMETRY"&&<div className="grid grid-cols-2 gap-5"><Panel title={`Geometry / Polyline • ${count(["GEOMETRY"])} Lisp nguồn`}><div className="text-sm text-neutral-300">MOF, BRK, C2P, APTD, INT, IPMX. Không đưa Field/Dim/Layer vào nhóm Geometry chỉ vì thao tác trên đường.</div><button onClick={onOpenProfessionalAudit} className="mt-3 px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-sm">Geometry Doctor</button></Panel><Panel title="6 Lisp nguồn Geometry"><FeatureList centers={["GEOMETRY"]}/></Panel></div>}
   {tab==="DIMENSION"&&<div className="grid grid-cols-2 gap-5"><Panel title={`Quick Dimension • ${count(["DIMENSION"])} Lisp nguồn`}><div className="text-sm text-neutral-300">Chỉ có 3 nguồn: DN/DNC, JD, TKD. Quick Dim là workflow HNL chính; 3 Lisp này ở tab nguồn chỉ để tương thích và tham khảo.</div><button onClick={quickDim} className="mt-3 px-4 py-2 rounded bg-cyan-500 text-black font-semibold flex gap-2"><Ruler className="w-4 h-4"/>Quick Dim Selection</button></Panel><Panel title="3 Lisp nguồn Dimension"><FeatureList centers={["DIMENSION"]}/></Panel></div>}
   {tab==="LAYER"&&<div className="grid grid-cols-2 gap-5"><Panel title={`Layer / Properties • ${count(["LAYER"])} Lisp nguồn`}><div className="text-sm text-neutral-300 leading-7">Chỉ gồm TLE (Layer từ Excel) và DM/RDM/FDM (đổi/tìm/khôi phục màu). Không còn để TLE trong BOQ hoặc Color Manager trong Block.</div></Panel><Panel title="2 Lisp nguồn Layer"><FeatureList centers={["LAYER"]}/></Panel></div>}
   {tab==="QUANTITY"&&<div className="space-y-4"><div className="flex gap-3 items-center"><span className="text-xs text-neutral-500">Nguồn Quantity thuần: TKT + CalculateAreaPerimeter. Field Quantity đã chuyển về Field; DEMTC chuyển về Shopdrawing.</span></div><div className="border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[1fr_130px_100px_140px_140px] bg-neutral-900 p-2 text-[10px] text-neutral-500"><span>Layer</span><span>Type</span><span>Count</span><span>Length (m)</span><span>Area (m²)</span></div><div className="max-h-[360px] overflow-auto">{quantities.slice(0,500).map((r,i)=><div key={`${r.layer}_${r.type}_${i}`} className="grid grid-cols-[1fr_130px_100px_140px_140px] p-2 border-t border-neutral-800 text-xs"><span>{r.layer}</span><span>{r.type}</span><span>{r.count}</span><span>{(r.lengthMm/1000).toFixed(3)}</span><span>{(r.areaMm2/1e6).toFixed(3)}</span></div>)}</div></div><Panel title="2 Lisp nguồn Quantity / BOQ"><FeatureList centers={["QUANTITY"]}/></Panel></div>}
   {tab==="SHOPDRAWING"&&<div className="grid grid-cols-2 gap-5"><Panel title={`Shopdrawing / Detail / Ceiling • ${count(["SHOPDRAWING"])} Lisp nguồn`}><div className="text-sm text-neutral-300 leading-7">Chỉ gồm VXT, DEMTC và TCD. TCD được chuyển khỏi Layout vì mục đích chính là trích chi tiết shopdrawing; Layout chỉ là một môi trường xuất.</div><button onClick={onOpenCeiling} className="mt-3 px-4 py-2 rounded bg-cyan-700 text-white font-semibold">Mở Shopdrawing / Ceiling Studio</button></Panel><Panel title="3 Lisp nguồn Shopdrawing"><FeatureList centers={["SHOPDRAWING"]}/></Panel></div>}
   {tab==="LAYOUT"&&<div className="grid grid-cols-2 gap-5"><Panel title={`Layout / Viewport / Publish • ${count(["LAYOUT"])} Lisp nguồn`}><div className="text-sm text-neutral-300 leading-7">11 Lisp đúng nhóm Layout: MS2PS, CLM, RNL, TAOKHUNG, APT, CVP1/CVP2, DMBV, SAP, LL/BT, PSL, TKL. Không còn TCD.</div><button onClick={onOpenPlotPublish} className="mt-3 px-4 py-2 rounded bg-sky-500 text-black font-semibold">Mở Plot / Publish / Sheet Set</button></Panel><Panel title="AutoCAD Native"><div className={`p-3 rounded border ${autoCadConnected?"border-emerald-800 text-emerald-300":"border-neutral-700 text-neutral-500"}`}>{autoCadConnected?"Bridge Connected: CHSPACE/Page Setup/Viewport/PSLTSCALE native có thể dùng bridge.":"Bridge Offline: các lệnh Layout native không chạy đầy đủ trong Standalone."}</div></Panel></div>}
   {tab==="TOOLS"&&<div><div className="flex gap-2 items-center mb-4"><div className="relative w-96"><Search className="absolute left-3 top-2.5 w-4 h-4 text-neutral-500"/><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Tìm công cụ chuẩn hoặc lệnh Lisp cũ..." className="w-full pl-9 pr-3 py-2 rounded bg-neutral-900 border border-neutral-700"/></div><span className="text-xs text-neutral-500">{filteredTools.length}/{CANONICAL_LISP_TOOLS.length} công cụ chuẩn</span></div><div className="border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[190px_160px_110px_70px_1fr_1.3fr] bg-neutral-900 p-2 text-[10px] text-neutral-500"><span>Công cụ chuẩn</span><span>Nhóm</span><span>Mode</span><span>Priority</span><span>Chức năng</span><span>Lệnh Lisp đã gộp</span></div>{filteredTools.map(t=><div key={t.id} className="grid grid-cols-[190px_160px_110px_70px_1fr_1.3fr] p-2 border-t border-neutral-800 text-xs items-start"><span className="text-cyan-300 font-semibold">{t.name}</span><span>{t.center}</span><span className={t.mode==="NATIVE"||t.mode==="NATIVE_AI"?"text-emerald-300":t.mode==="HYBRID"?"text-amber-300":"text-violet-300"}>{t.mode}</span><span>{t.priority}</span><span className="text-neutral-400">{t.summary}</span><span className="font-mono text-neutral-500 break-words">{t.sources.join(" • ")}</span></div>)}</div><div className="mt-3 p-3 rounded border border-neutral-800 bg-[#1d2025] text-xs text-neutral-400">Đây là danh sách dùng trong UI chính. Những Lisp trùng mục tiêu đã được gộp vào một công cụ chuẩn; tên lệnh cũ chỉ là alias/nguồn tham khảo.</div></div>}
   {tab==="SOURCES"&&<div className="space-y-4">
    <div className={`rounded-xl border p-3 text-xs ${bundledLispStatus?.complete?"border-emerald-800/60 bg-emerald-950/15":"border-amber-900/60 bg-amber-950/15"}`}>
      <b className={bundledLispStatus?.complete?"text-emerald-300":"text-amber-300"}>
        {bundledLispStatus?.complete
          ? `✓ 44 Lisp nguồn của bạn đã được tích hợp vào HNL (${bundledLispStatus.lispCount}/44).`
          : `Bộ Lisp tích hợp chưa đầy đủ (${bundledLispStatus?.lispCount||0}/${bundledLispStatus?.expectedLispCount||44}).`}
      </b>
      <div className="mt-1 text-neutral-500">
        Installer và AutoCAD plugin bundle lấy đúng <code>AI.rar</code> người dùng cung cấp. 44 Lisp được đóng sẵn nhưng mặc định KHÔNG tự LOAD. Khi bạn bấm Nạp + Chạy, HNL chỉ LOAD đúng Lisp cần dùng rồi chạy command. Nạp file/thư mục bên dưới chỉ dành cho Lisp bổ sung ngoài bộ 44.
      </div>
      {bundledLispStatus?.legacyArx?.length?<div className="mt-1 text-amber-500">GeomProps2021x64.arx được giữ làm nguồn legacy nhưng không auto-load trên AutoCAD 2023–2027.</div>:null}
      <div className="mt-1 text-cyan-400">Chế độ mặc định: ON-DEMAND (nhẹ máy) • HNLLISPSTATUS / HNLLISPRELOAD / HNLLISPAUTOON / HNLLISPAUTOOFF</div>
    </div>

    <div className="flex flex-wrap gap-2 items-center">
      <div className="relative w-80"><Search className="absolute left-3 top-2.5 w-4 h-4 text-neutral-500"/><input value={sourceQuery} onChange={e=>setSourceQuery(e.target.value)} placeholder="Tìm BRK, TKL, Field..." className="w-full pl-9 pr-3 py-2 rounded bg-neutral-900 border border-neutral-700"/></div>
      <span className="text-xs text-neutral-500">{filteredSources.length}/44 Lisp • {lispFiles.filter(x=>x.bundled).length} tích hợp • {lispFiles.filter(x=>!x.bundled).length} bổ sung</span>
      <button onClick={()=>void (window as any).electronNative?.revealBundledLispRoot?.()} className="ml-auto px-3 py-2 rounded bg-emerald-950/30 border border-emerald-800 hover:bg-emerald-900/40 text-xs">Mở thư mục 44 Lisp</button>
      <button onClick={()=>void importLisp("FILES")} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-xs flex gap-1.5 items-center"><FileInput className="w-4 h-4"/>Nạp Lisp bổ sung</button>
      <button onClick={()=>void importLisp("FOLDER")} className="px-3 py-2 rounded bg-cyan-900/40 border border-cyan-700 hover:bg-cyan-900/60 text-xs flex gap-1.5 items-center"><FolderInput className="w-4 h-4"/>Nạp thư mục bổ sung</button>
      <button onClick={onOpenLispBuilder} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 hover:bg-neutral-700 text-xs">AI Lisp Builder</button>
    </div>

    <div className="flex flex-wrap gap-1.5">
      <button onClick={()=>setSourceGroupFilter("ALL")} className={`px-2.5 py-1.5 rounded text-[10px] border ${sourceGroupFilter==="ALL"?"bg-cyan-600 text-black border-cyan-500":"bg-neutral-900 text-neutral-400 border-neutral-800"}`}>Tất cả 44</button>
      {LISP_SOURCE_GROUP_ORDER.map(g=>{
        const n=LISP_FEATURE_CATALOG.filter(x=>x.sourceGroup===g).length;
        return <button key={g} onClick={()=>setSourceGroupFilter(g)} className={`px-2.5 py-1.5 rounded text-[10px] border ${sourceGroupFilter===g?"bg-cyan-600 text-black border-cyan-500":"bg-neutral-900 text-neutral-400 border-neutral-800"}`}>{LISP_SOURCE_GROUP_LABELS[g]} ({n})</button>
      })}
    </div>

    {lispMessage&&<div className="px-3 py-2 rounded border border-neutral-800 bg-neutral-900/60 text-xs text-neutral-300">{lispMessage}</div>}

    <div className="border border-neutral-800 rounded overflow-hidden">
      <div className="grid grid-cols-[135px_125px_150px_100px_80px_105px_1fr_220px] bg-neutral-900 p-2 text-[10px] text-neutral-500">
        <span>Command</span><span>Tên</span><span>Nhóm chuẩn</span><span>Mode</span><span>Priority</span><span>Match</span><span>File gốc / Chức năng</span><span>Thao tác</span>
      </div>
      {filteredSources.map((f,i)=>{
        const files=matchedFiles(f); const found=files.length>0;
        return <div key={`${f.commands}_${i}`} className="grid grid-cols-[135px_125px_150px_100px_80px_105px_1fr_220px] p-2 border-t border-neutral-800 text-xs items-center gap-1">
          <button onClick={()=>setSelectedSource(f)} className="font-mono text-cyan-300 break-words text-left hover:underline">{files[0]?.commands?.length?files[0].commands.join("/") : f.commands}</button>
          <span>{f.name}</span>
          <span className="text-sky-300">{LISP_SOURCE_GROUP_LABELS[f.sourceGroup]}</span>
          <span className={f.mode==="NATIVE"||f.mode==="NATIVE_AI"?"text-emerald-300":f.mode==="HYBRID"?"text-amber-300":"text-violet-300"}>{f.mode}</span>
          <span>{f.priority}</span>
          <span className={found?"text-emerald-300":"text-red-300"}>{found?`✓ ${files.length}${files.some(x=>x.bundled)?" • HNL":""}`:"CHƯA MATCH"}</span>
          <span className="text-neutral-400"><span className="block text-[10px] text-neutral-600 break-all">{f.sourceFile}</span><span className="block mt-1">{f.summary}</span></span>
          <span className="flex gap-1 flex-wrap">
            <button onClick={()=>setSelectedSource(f)} className="px-2 py-1 rounded border border-neutral-700 hover:bg-neutral-800 flex items-center gap-1"><BookOpen className="w-3 h-3"/>Hướng dẫn</button>
            <button disabled={!found||!autoCadConnected} onClick={()=>void loadLisp(f,false)} className="px-2 py-1 rounded border border-neutral-700 hover:bg-neutral-800 disabled:opacity-30"><Upload className="w-3 h-3 inline mr-1"/>Nạp</button>
            <button disabled={!found||!autoCadConnected} onClick={()=>void loadLisp(f,true)} className="px-2 py-1 rounded bg-cyan-700 hover:bg-cyan-600 disabled:opacity-30"><Play className="w-3 h-3 inline mr-1"/>Nạp + Chạy</button>
          </span>
        </div>
      })}
    </div>

    {selectedSource&&(()=>{
      const g=buildLispGuide(selectedSource); const files=matchedFiles(selectedSource);
      return <div className="rounded-xl border border-cyan-800/60 bg-[#12161a] p-4">
        <div className="flex items-start justify-between gap-3">
          <div><div className="text-[10px] uppercase tracking-wide text-cyan-500">Hướng dẫn chi tiết Lisp nguồn</div><div className="text-lg font-bold text-white mt-1">{selectedSource.commands} • {selectedSource.name}</div><div className="text-xs text-neutral-500 mt-1">{g.whenToUse}</div></div>
          <button onClick={()=>setSelectedSource(null)} className="p-1 text-neutral-500"><X className="w-4 h-4"/></button>
        </div>
        <div className="grid md:grid-cols-3 gap-3 mt-4">
          <GuideBox title="Cần chọn gì" lines={[g.selection]}/>
          <GuideBox title="Chế độ" lines={[`${selectedSource.mode} • ${autoCadConnected?"AutoCAD Bridge đang Connected":"Bridge đang Offline"}`]}/>
          <GuideBox title="File nguồn" lines={files.length?files.map(x=>x.name):[`Catalog: ${selectedSource.sourceFile}`,"Chưa tìm thấy .lsp tương ứng."]}/>
        </div>
        <GuideBlock title="Trước khi chạy" lines={g.prerequisites}/>
        <GuideBlock title="Cách chạy từng bước" lines={g.steps} numbered/>
        <GuideBlock title="Kết quả mong đợi" lines={[g.expected]}/>
        <GuideBlock title="Lỗi thường gặp" lines={g.commonErrors}/>
        <div className="mt-3 text-[10px] text-neutral-600">Quan trọng: trạng thái “Đã gửi LOAD” không đồng nghĩa Lisp đã chạy thành công. AutoCAD Command Line là nguồn xác nhận cuối cho SECURELOAD, DCL, Excel/COM và dependency.</div>
      </div>
    })()}
   </div>}
  </div>
 </div></div>;
};
const Panel:React.FC<{title:string;children:React.ReactNode}>=({title,children})=><div className="p-4 rounded-xl border border-neutral-800 bg-[#1d2025]"><div className="font-semibold text-neutral-200 mb-3">{title}</div>{children}</div>;
const Stat:React.FC<{k:string;v:any}>=({k,v})=><div className="p-3 rounded border border-neutral-800 bg-[#1d2025]"><div className="text-[10px] uppercase text-neutral-600">{k}</div><div className="text-xl font-bold mt-1">{v}</div></div>;
const GuideBox:React.FC<{title:string;lines:string[]}>=({title,lines})=><div className="p-3 rounded border border-neutral-800 bg-neutral-900/50"><div className="text-[10px] uppercase text-neutral-600">{title}</div>{lines.map((x,i)=><div key={i} className="text-xs text-neutral-300 mt-1 break-words">{x}</div>)}</div>;
const GuideBlock:React.FC<{title:string;lines:string[];numbered?:boolean}>=({title,lines,numbered})=><div className="mt-4"><div className="text-xs font-semibold text-neutral-200">{title}</div><div className="mt-2 space-y-1.5">{lines.map((x,i)=><div key={i} className="flex gap-2 text-xs text-neutral-400 leading-5"><span className="w-5 shrink-0 text-cyan-500">{numbered?`${i+1}.`:"•"}</span><span>{x}</span></div>)}</div></div>;
const FeatureList:React.FC<{centers:LispCenter[]}>=({centers})=><div className="space-y-2">{LISP_FEATURE_CATALOG.filter(x=>centers.includes(x.center)).map((x,i)=><div key={i} className="p-2 rounded border border-neutral-800 bg-neutral-900/40"><div className="text-xs text-cyan-300 font-mono">{x.commands}</div><div className="text-xs text-neutral-400 mt-1">{x.summary}</div></div>)}</div>;
