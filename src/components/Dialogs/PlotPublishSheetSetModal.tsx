import React,{useEffect,useMemo,useState} from "react";
import { BookOpen, CheckCircle2, ChevronDown, FileDown, Layers3, Printer, Save, Settings2, Sheet, TriangleAlert, X } from "lucide-react";
import { CadEntity,CadLayer,CadLayout,CadViewport } from "../../types/cad";
import { DEFAULT_PLOT_PRESETS,HnlSheetSet,PlotPreset,makePlotHtml,preflightSheetSet,renderPlotSvg,sheetSetFromLayouts } from "../../lib/plotPublishEngine";
import { parseHnlSheetSet,saveSheetSet } from "../../lib/sheetSetEngine";
import { createPublishQueue, PublishJob } from "../../lib/publishQueue";
import { executeAutoCadAction } from "../../lib/autoCadBridge";

declare global { interface Window { electronNative?: any } }

type Diag={code:string;severity:"INFO"|"WARNING"|"ERROR"|"CRITICAL";title:string;message:string;command?:string;suggestion?:string;context?:Record<string,unknown>};
type PrinterInfo={name:string;displayName?:string;description?:string;status?:number;isDefault?:boolean};

export const PlotPublishSheetSetModal:React.FC<{
 isOpen:boolean;onClose:()=>void;entities:CadEntity[];layers:CadLayer[];layouts:CadLayout[];viewports:CadViewport[];
 autoCadConnected:boolean;onDiagnostic:(d:Diag)=>void;
}> = ({isOpen,onClose,entities,layers,layouts,viewports,autoCadConnected,onDiagnostic})=>{
 const [tab,setTab]=useState<"PLOT"|"PUBLISH"|"SHEETSET"|"DEVICE">("PLOT");
 const [printers,setPrinters]=useState<PrinterInfo[]>([]);
 const [printer,setPrinter]=useState("");
 const [presetId,setPresetId]=useState(DEFAULT_PLOT_PRESETS[0].id);
 const [lineScale,setLineScale]=useState(1);
 const [sheetSet,setSheetSet]=useState<HnlSheetSet|null>(null);
 const [copies,setCopies]=useState(1);
 const [publishJobs,setPublishJobs]=useState<PublishJob[]>([]);
 const [queueRunning,setQueueRunning]=useState(false);
 const queueCancelRef=React.useRef(false);
 const [sheetPage,setSheetPage]=useState(0);
 const [sheetFilter,setSheetFilter]=useState("");
 const [nativeDevices,setNativeDevices]=useState<string[]>([]);
 const [nativeStyles,setNativeStyles]=useState<string[]>([]);
 const [nativeLayouts,setNativeLayouts]=useState<any[]>([]);
 const [nativeDstPath,setNativeDstPath]=useState("");
 const [nativeOriginalSheets,setNativeOriginalSheets]=useState<Array<{sheetNo:string;sheetName:string}>>([]);
 const [nativeBusy,setNativeBusy]=useState(false);
 const [layerPlotOverrides,setLayerPlotOverrides]=useState<Record<string,{lineweight?:number;color?:string}>>({});
 const preset=useMemo(()=>({...DEFAULT_PLOT_PRESETS.find(p=>p.id===presetId)!,lineweightScale:lineScale}),[presetId,lineScale]);
 const styledLayers=useMemo(()=>layers.map(l=>({...l,...(layerPlotOverrides[l.name]||{})})),[layers,layerPlotOverrides]);
 useEffect(()=>{if(!isOpen)return;const run=async()=>{try{const x=await window.electronNative?.getPrinters?.();const arr=Array.isArray(x)?x:[];setPrinters(arr);const def=arr.find((p:any)=>p.isDefault);if(def&&!printer)setPrinter(def.name);}catch{}};run();},[isOpen]);
 useEffect(()=>{if(isOpen&&!sheetSet){setSheetSet(sheetSetFromLayouts(layouts,viewports,printer||undefined,"monochrome.ctb"));}},[isOpen,layouts,viewports]);

 if(!isOpen)return null;
 const svg=()=>renderPlotSvg(entities,styledLayers,preset,"HNL CAD Plot");
 const quickPdf=async()=>{
  try{
   const html=makePlotHtml(svg(),"HNL Model Plot",preset.paper,preset.orientation);
   const r=await window.electronNative?.renderPdfFromHtml?.({html,defaultName:`HNL_Model_${preset.paper}.pdf`,landscape:preset.orientation==="LANDSCAPE",pageSize:preset.paper});
   if(!r?.success){if(!r?.canceled)throw new Error(r?.error||"Không xuất được PDF.");return;}
   onDiagnostic({code:"HNL-PLOT-PDF-OK",severity:"INFO",title:"Xuất PDF Model thành công",message:`${r.filePath} (${r.bytes||0} bytes)`,command:"QUICK_PLOT_PDF",context:{preset}});
  }catch(e:any){onDiagnostic({code:"HNL-PLOT-PDF",severity:"ERROR",title:"Xuất PDF thất bại",message:e?.message||String(e),command:"QUICK_PLOT_PDF",suggestion:"Kiểm tra quyền ghi file, paper size và thử lại."});}
 };
 const physicalPrint=async()=>{
  try{
   const html=makePlotHtml(svg(),"HNL Model Plot",preset.paper,preset.orientation);
   const r=await window.electronNative?.printHtml?.({html,printerName:printer||undefined,landscape:preset.orientation==="LANDSCAPE",copies});
   if(!r?.success)throw new Error(r?.error||"Print failed");
   onDiagnostic({code:"HNL-PLOT-PRINT-OK",severity:"INFO",title:"Đã gửi lệnh in",message:`Printer: ${printer||"system dialog"}; copies ${copies}.`,command:"PRINT_MODEL"});
  }catch(e:any){onDiagnostic({code:"HNL-PLOT-PRINT",severity:"ERROR",title:"In thất bại",message:e?.message||String(e),command:"PRINT_MODEL",suggestion:"Kiểm tra máy in/driver/trạng thái Offline và thử lại."});}
 };
 const multiPdf=async()=>{
  try{
   const enabled=sheetSet?.sheets.filter(s=>s.enabled)||[];
   if(!enabled.length)throw new Error("Chưa chọn sheet nào.");
   const pages=enabled.map(s=>{const vp=viewports.find(v=>v.layoutName===s.layoutName);let win:any=undefined;if(vp&&vp.scaleFactor>0){const mw=vp.width/vp.scaleFactor,mh=vp.height/vp.scaleFactor;win={x:vp.modelCenter.x-mw/2,y:vp.modelCenter.y-mh/2,w:mw,h:mh};}const pageSvg=renderPlotSvg(entities,styledLayers,preset,s.sheetName,win);return `<section class="page"><div class="sheet-title">${s.sheetNo} — ${s.sheetName}</div>${pageSvg}</section>`;}).join("");
   const html=`<!doctype html><html><head><meta charset="utf-8"><style>@page{size:${preset.paper} ${preset.orientation.toLowerCase()};margin:8mm}body{margin:0}.page{page-break-after:always;position:relative;width:100%;height:96vh;display:flex;align-items:center;justify-content:center}.sheet-title{position:absolute;top:2mm;left:3mm;font:11px Arial;color:#444}.page svg{max-width:100%;max-height:92vh}</style></head><body>${pages}</body></html>`;
   const r=await window.electronNative?.renderPdfFromHtml?.({html,defaultName:`${sheetSet?.name||"HNL_SheetSet"}.pdf`,landscape:preset.orientation==="LANDSCAPE",pageSize:preset.paper});
   if(!r?.success){if(!r?.canceled)throw new Error(r?.error||"Publish PDF failed");return;}
   onDiagnostic({code:"HNL-PUBLISH-PDF-OK",severity:"INFO",title:"Publish PDF nhiều trang thành công",message:`${enabled.length} sheet → ${r.filePath}`,command:"PUBLISH_MULTI_PDF",context:{standaloneModelBased:true}});
  }catch(e:any){onDiagnostic({code:"HNL-PUBLISH-PDF",severity:"ERROR",title:"Publish PDF thất bại",message:e?.message||String(e),command:"PUBLISH_MULTI_PDF"});}
 };
 const openSheetSet=async()=>{
  try{
   const r=await window.electronNative?.openSheetSet?.();if(!r?.success)return;
   if(r.extension==="dst"){
     if(!autoCadConnected){onDiagnostic({code:"HNL-SHEETSET-DST",severity:"WARNING",title:"AutoCAD Sheet Set (.DST)",message:`${r.filePath}: .DST native cần AutoCAD Bridge.`,command:"OPEN_DST",suggestion:"Mở AutoCAD, load HNL Bridge rồi thử lại."});return;}
     setNativeBusy(true);
     const info=await executeAutoCadAction("GET_SHEETSET_INFO",{filePath:r.filePath});
     if(!info?.ok)throw new Error(info?.error||info?.reason||"GET_SHEETSET_INFO failed");
     const raw=Array.isArray(info?.result?.sheets)?info.result.sheets:[];
     const mapped:HnlSheetSet={schema:"hnl-sheet-set",version:1,name:info?.result?.name||"AutoCAD Sheet Set",description:info?.result?.description||"",createdAt:new Date().toISOString(),updatedAt:new Date().toISOString(),sheets:raw.map((x:any,i:number)=>({id:`dst_${i}_${x.number||i}`,sheetNo:String(x.number||i+1),sheetName:String(x.title||""),layoutName:String(x.layoutName||""),paper:"",orientation:"",scale:"",enabled:true,status:"READY",messages:[x.dwgPath?`DWG: ${x.dwgPath}`:"",x.subset?`Subset: ${x.subset}`:""].filter(Boolean)}))};
     setSheetSet(mapped);setNativeDstPath(r.filePath);setNativeOriginalSheets(mapped.sheets.map(x=>({sheetNo:x.sheetNo,sheetName:x.sheetName})));
     onDiagnostic({code:"HNL-SHEETSET-DST-OK",severity:"INFO",title:"Đã đọc AutoCAD .DST",message:`${mapped.sheets.length} sheets từ ${r.filePath}`,command:"GET_SHEETSET_INFO"});setNativeBusy(false);return;
   }
   const parsed=parseHnlSheetSet(r.content);setSheetSet(parsed);saveSheetSet(parsed);
  }catch(e:any){onDiagnostic({code:"HNL-SHEETSET-OPEN",severity:"ERROR",title:"Mở Sheet Set thất bại",message:e?.message||String(e),command:"OPEN_SHEETSET"});}
 };
 const saveSet=async()=>{
  try{if(!sheetSet)throw new Error("Không có Sheet Set.");const x={...sheetSet,updatedAt:new Date().toISOString()};saveSheetSet(x);const r=await window.electronNative?.saveFile?.({defaultName:`${x.name.replace(/[^A-Za-z0-9_-]+/g,"_")}.hnl-sheetset.json`,content:JSON.stringify(x,null,2),extDescription:"HNL Sheet Set",extension:"json"});if(r?.success)onDiagnostic({code:"HNL-SHEETSET-SAVE-OK",severity:"INFO",title:"Đã lưu HNL Sheet Set",message:r.filePath,command:"SAVE_SHEETSET"});}catch(e:any){onDiagnostic({code:"HNL-SHEETSET-SAVE",severity:"ERROR",title:"Lưu Sheet Set thất bại",message:e?.message||String(e),command:"SAVE_SHEETSET"});}
 };
 const applyNativeDstChanges=async()=>{
   try{
     if(!nativeDstPath||!sheetSet)throw new Error("Không có .DST native đang mở.");
     if(!autoCadConnected)throw new Error("AutoCAD Bridge chưa kết nối.");
     setNativeBusy(true);let changed=0;
     for(let i=0;i<sheetSet.sheets.length;i++){
       const cur=sheetSet.sheets[i],old=nativeOriginalSheets[i];if(!old)continue;
       if(cur.sheetNo!==old.sheetNo||cur.sheetName!==old.sheetName){
         const r=await executeAutoCadAction("UPDATE_SHEET",{filePath:nativeDstPath,oldNumber:old.sheetNo,number:cur.sheetNo,title:cur.sheetName});
         if(!r?.ok)throw new Error(r?.error||r?.reason||`UPDATE_SHEET failed ${old.sheetNo}`);
         changed++;
       }
     }
     setNativeOriginalSheets(sheetSet.sheets.map(x=>({sheetNo:x.sheetNo,sheetName:x.sheetName})));
     onDiagnostic({code:"HNL-SHEETSET-DST-UPDATE-OK",severity:"INFO",title:"Đã cập nhật .DST native",message:`${changed} sheet thay đổi.`,command:"UPDATE_SHEET"});
   }catch(e:any){onDiagnostic({code:"HNL-SHEETSET-DST-UPDATE",severity:"ERROR",title:"Cập nhật .DST thất bại",message:e?.message||String(e),command:"UPDATE_SHEET",suggestion:"Kiểm tra Sheet Set không bị khóa bởi client khác; bridge sẽ rollback nếu update lỗi."});}finally{setNativeBusy(false);}
 };

 const preflight=()=>{
   if(!sheetSet)return null;return preflightSheetSet(sheetSet,printers.map(p=>p.name));
 };
 const publishIndividualQueue=async()=>{
   try{
     const enabled=(sheetSet?.sheets||[]).filter(s=>s.enabled);
     if(!enabled.length)throw new Error("Chưa chọn sheet nào.");
     const folder=await window.electronNative?.chooseOutputFolder?.();
     if(!folder?.success)return;
     const jobs=createPublishQueue(enabled);
     setPublishJobs(jobs);setQueueRunning(true);queueCancelRef.current=false;
     for(let i=0;i<jobs.length;i++){
       if(queueCancelRef.current){setPublishJobs(prev=>prev.map((j,k)=>k>=i&&j.status==="PENDING"?{...j,status:"CANCELED",message:"User canceled"}:j));break;}
       const s=enabled[i];
       setPublishJobs(prev=>prev.map((j,k)=>k===i?{...j,status:"RUNNING",progress:20,startedAt:new Date().toISOString()}:j));
       const vp=viewports.find(v=>v.layoutName===s.layoutName);
       let win:any=undefined;
       if(vp&&vp.scaleFactor>0){const mw=vp.width/vp.scaleFactor,mh=vp.height/vp.scaleFactor;win={x:vp.modelCenter.x-mw/2,y:vp.modelCenter.y-mh/2,w:mw,h:mh};}
       const pageSvg=renderPlotSvg(entities,styledLayers,preset,s.sheetName,win);
       const html=makePlotHtml(pageSvg,s.sheetName,preset.paper,preset.orientation);
       const safe=`${s.sheetNo}_${s.sheetName}`.replace(/[\\/:*?"<>|]+/g,"_").replace(/\s+/g,"_").slice(0,120);
       const sep=String(folder.folderPath).includes("\\")?"\\":"/";
       const filePath=`${String(folder.folderPath).replace(/[\\\/]+$/,"")}${sep}${safe}.pdf`;
       const r=await window.electronNative?.renderPdfToPath?.({html,filePath,landscape:preset.orientation==="LANDSCAPE",pageSize:preset.paper});
       if(r?.success){
         setPublishJobs(prev=>prev.map((j,k)=>k===i?{...j,status:"DONE",progress:100,completedAt:new Date().toISOString(),message:r.filePath}:j));
       }else{
         setPublishJobs(prev=>prev.map((j,k)=>k===i?{...j,status:"FAILED",progress:100,completedAt:new Date().toISOString(),message:r?.error||"PDF failed"}:j));
       }
     }
     onDiagnostic({code:"HNL-PUBLISH-QUEUE",severity:"INFO",title:"Publish Queue kết thúc",message:`${enabled.length} sheet đã được xử lý hoặc dừng.`,command:"PUBLISH_QUEUE"});
   }catch(e:any){onDiagnostic({code:"HNL-PUBLISH-QUEUE-ERR",severity:"ERROR",title:"Publish Queue thất bại",message:e?.message||String(e),command:"PUBLISH_QUEUE"});}finally{setQueueRunning(false);}
 };

 const pf=preflight();
 const sourceSheets=(pf?.sheetSet.sheets||sheetSet?.sheets||[]);
 const filteredSheets=sourceSheets.filter(s=>!sheetFilter.trim()||`${s.sheetNo} ${s.sheetName} ${s.layoutName}`.toLowerCase().includes(sheetFilter.trim().toLowerCase()));
 const PAGE_SIZE=100;
 const pageCount=Math.max(1,Math.ceil(filteredSheets.length/PAGE_SIZE));
 const visibleSheets=filteredSheets.slice(sheetPage*PAGE_SIZE,(sheetPage+1)*PAGE_SIZE);
 const applyBatchSetup=()=>{
   if(!sheetSet)return;
   setSheetSet({...sheetSet,sheets:sheetSet.sheets.map(s=>s.enabled?{...s,paper:preset.paper,orientation:preset.orientation,scale:preset.scale,printerName:printer||s.printerName,plotStyle:preset.monochrome?"monochrome.ctb":(s.plotStyle||"color.ctb")}:s)});
   onDiagnostic({code:"HNL-PAGESETUP-BATCH",severity:"INFO",title:"Batch Page Setup",message:`Đã áp preset ${preset.name} cho các sheet đang bật.`,command:"BATCH_PAGE_SETUP"});
 };

 const refreshNative=async()=>{
   try{
     if(!autoCadConnected)throw new Error("AutoCAD Bridge chưa kết nối.");
     setNativeBusy(true);
     const dev=await executeAutoCadAction("GET_PLOT_DEVICES",{});
     const lay=await executeAutoCadAction("GET_LAYOUTS",{});
     if(!dev?.ok)throw new Error(dev?.error||dev?.reason||"GET_PLOT_DEVICES failed");
     if(!lay?.ok)throw new Error(lay?.error||lay?.reason||"GET_LAYOUTS failed");
     setNativeDevices(dev?.result?.devices||[]);
     setNativeStyles(dev?.result?.styles||[]);
     setNativeLayouts(lay?.result?.layouts||[]);
     onDiagnostic({code:"HNL-NATIVE-PLOT-INFO",severity:"INFO",title:"Đã đọc Plot Native từ AutoCAD",message:`${dev?.result?.devices?.length||0} devices, ${dev?.result?.styles?.length||0} plot styles, ${lay?.result?.layouts?.length||0} layouts.`,command:"GET_NATIVE_PLOT_INFO"});
   }catch(e:any){onDiagnostic({code:"HNL-NATIVE-PLOT-INFO-ERR",severity:"ERROR",title:"Không đọc được Plot Native",message:e?.message||String(e),command:"GET_NATIVE_PLOT_INFO"});}finally{setNativeBusy(false);}
 };
 const nativePublish=async()=>{
   try{
     if(!autoCadConnected)throw new Error("AutoCAD Bridge chưa kết nối.");
     const selected=(sheetSet?.sheets||[]).filter(s=>s.enabled).map(s=>s.layoutName);
     if(!selected.length)throw new Error("Chưa chọn Layout/Sheet.");
     const save=await window.electronNative?.chooseSavePath?.({title:"AutoCAD Native Publish PDF",defaultName:`${sheetSet?.name||"HNL_Native_Publish"}.pdf`,extension:"pdf",description:"PDF"});
     if(!save?.success)return;
     setNativeBusy(true);
     const r=await executeAutoCadAction("PUBLISH_LAYOUTS_PDF",{layouts:selected,outputPath:save.filePath});
     if(!r?.ok)throw new Error(r?.error||r?.reason||"Native publish failed");
     onDiagnostic({code:"HNL-NATIVE-PUBLISH-OK",severity:"INFO",title:"AutoCAD Native Publish hoàn tất",message:`${selected.length} layout → ${save.filePath}`,command:"PUBLISH_LAYOUTS_PDF",context:r.result});
   }catch(e:any){onDiagnostic({code:"HNL-NATIVE-PUBLISH-ERR",severity:"ERROR",title:"AutoCAD Native Publish thất bại",message:e?.message||String(e),command:"PUBLISH_LAYOUTS_PDF",suggestion:"Kiểm tra AutoCAD đang rảnh, Layout/Page Setup/PC3 hợp lệ và BACKGROUNDPLOT."});}finally{setNativeBusy(false);}
 };

 return <div className="fixed inset-0 z-[199] bg-black/75 flex items-center justify-center p-3"><div className="w-full max-w-7xl h-[88vh] rounded-2xl border border-neutral-700 bg-[#17191d] shadow-2xl flex flex-col overflow-hidden">
  <div className="px-5 py-4 bg-[#111317] border-b border-neutral-800 flex justify-between"><div><div className="font-bold text-white flex items-center gap-2"><Printer className="w-5 h-5 text-cyan-400"/> Plot / Publish / Sheet Set Professional</div><div className="text-xs text-neutral-500 mt-1">Model PDF • nhiều Layout/Sheet PDF • Printer • Plot Style • Sheet Set • Preflight</div></div><button onClick={onClose}><X/></button></div>
  <div className="p-3 flex gap-1 border-b border-neutral-800">{[["PLOT","Quick Plot"],["PUBLISH","Publish"],["SHEETSET","Sheet Set"],["DEVICE","Printer & Nét in"]].map(([k,l])=><button key={k} onClick={()=>setTab(k as any)} className={`px-4 py-2 rounded text-sm ${tab===k?"bg-cyan-500 text-black":"bg-neutral-800 text-neutral-300"}`}>{l}</button>)}</div>
  <div className="p-5 overflow-auto flex-1">
   {tab==="PLOT"&&<div className="grid grid-cols-[1fr_360px] gap-5"><div className="p-4 rounded-xl border border-neutral-800 bg-white min-h-[520px] flex items-center justify-center" dangerouslySetInnerHTML={{__html:svg()}}/><div className="space-y-4"><Panel title="Plot preset"><select value={presetId} onChange={e=>setPresetId(e.target.value)} className="w-full bg-neutral-900 border border-neutral-700 p-2 rounded text-sm">{DEFAULT_PLOT_PRESETS.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select><div className="mt-3 text-xs text-neutral-500">{preset.paper} • {preset.orientation} • {preset.scale} • {preset.monochrome?"Monochrome":"Color"}</div></Panel><Panel title="Thiết bị"><select value={printer} onChange={e=>setPrinter(e.target.value)} className="w-full bg-neutral-900 border border-neutral-700 p-2 rounded text-sm"><option value="">Windows print dialog</option>{printers.map(p=><option key={p.name} value={p.name}>{p.displayName||p.name}{p.isDefault?" (Default)":""}</option>)}</select><label className="block mt-3 text-xs text-neutral-400">Copies<input type="number" min={1} max={99} value={copies} onChange={e=>setCopies(Number(e.target.value)||1)} className="ml-3 w-20 bg-neutral-900 border border-neutral-700 rounded p-1"/></label></Panel><div className="flex gap-2"><button onClick={quickPdf} className="px-4 py-2 rounded bg-emerald-500 text-black font-semibold flex gap-2"><FileDown className="w-4 h-4"/>Xuất PDF</button><button onClick={physicalPrint} className="px-4 py-2 rounded bg-cyan-500 text-black font-semibold flex gap-2"><Printer className="w-4 h-4"/>In</button></div><div className="p-3 rounded border border-amber-800 bg-amber-950/20 text-xs text-amber-200">Standalone Plot hiện render từ entity HNL/Model. Plot Layout DWG native theo đúng viewport clipping, CTB/STB, PC3/PMP cần AutoCAD Bridge.</div></div></div>}
   {tab==="PUBLISH"&&<div className="space-y-4"><div className="flex flex-wrap gap-2 items-center"><input value={sheetFilter} onChange={e=>{setSheetFilter(e.target.value);setSheetPage(0)}} placeholder="Tìm số sheet / tên / layout..." className="w-72 bg-neutral-900 border border-neutral-700 rounded px-3 py-2 text-sm"/><button onClick={applyBatchSetup} className="px-3 py-2 rounded bg-neutral-800 border border-neutral-700 text-sm">Batch Page Setup → sheet bật</button><span className="text-xs text-neutral-500">{filteredSheets.length} sheet • trang {sheetPage+1}/{pageCount}</span><button disabled={sheetPage<=0} onClick={()=>setSheetPage(p=>Math.max(0,p-1))} className="px-2 py-1 rounded bg-neutral-800 disabled:opacity-30">‹</button><button disabled={sheetPage>=pageCount-1} onClick={()=>setSheetPage(p=>Math.min(pageCount-1,p+1))} className="px-2 py-1 rounded bg-neutral-800 disabled:opacity-30">›</button></div>{pf&&<div className={`p-4 rounded-xl border ${pf.ready?"border-emerald-800 bg-emerald-950/20":"border-amber-800 bg-amber-950/20"}`}><div className={`font-bold ${pf.ready?"text-emerald-300":"text-amber-300"}`}>{pf.ready?"READY — HNL Sheet Set":"CHECK — còn lỗi/cảnh báo"}</div><div className="text-xs text-neutral-500 mt-1">Errors {pf.errors} • Warnings {pf.warnings}. Native CTB/Xref/Page Setup vẫn cần AutoCAD Bridge.</div></div>}<div className="border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[45px_100px_1.5fr_140px_120px_120px_1fr] bg-neutral-900 p-2 text-[11px] text-neutral-500"><span>In</span><span>No.</span><span>Tên sheet</span><span>Layout</span><span>Paper</span><span>Status</span><span>Thông báo</span></div>{visibleSheets.map(s=><div key={s.id} className="grid grid-cols-[45px_100px_1.5fr_140px_120px_120px_1fr] p-2 border-t border-neutral-800 text-xs items-center"><input type="checkbox" checked={s.enabled} onChange={e=>setSheetSet(cur=>cur?{...cur,sheets:cur.sheets.map(x=>x.id===s.id?{...x,enabled:e.target.checked}:x)}:cur)}/><span>{s.sheetNo}</span><span>{s.sheetName}</span><span>{s.layoutName}</span><span>{s.paper}</span><span className={s.status==="READY"?"text-emerald-300":s.status==="ERROR"?"text-red-300":"text-amber-300"}>{s.status}</span><span className="text-neutral-500 truncate">{s.messages.join("; ")||"OK"}</span></div>)}</div><div className="flex gap-2 flex-wrap"><button onClick={multiPdf} className="px-4 py-2 rounded bg-emerald-500 text-black font-semibold">Publish Selected → 1 PDF nhiều trang</button><button disabled={queueRunning} onClick={publishIndividualQueue} className="px-4 py-2 rounded bg-sky-500 text-black font-semibold disabled:opacity-40">Publish Queue → từng PDF</button>{queueRunning&&<button onClick={()=>{queueCancelRef.current=true}} className="px-4 py-2 rounded bg-red-500/20 border border-red-700 text-red-200">Cancel Queue</button>}</div>{publishJobs.length>0&&<div className="border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[80px_1fr_100px_100px_1.2fr] bg-neutral-900 p-2 text-[10px] text-neutral-500"><span>Sheet</span><span>Tên</span><span>Status</span><span>Progress</span><span>Thông báo</span></div><div className="max-h-64 overflow-auto">{publishJobs.map(j=><div key={j.id} className="grid grid-cols-[80px_1fr_100px_100px_1.2fr] p-2 border-t border-neutral-800 text-xs"><span>{j.sheetNo}</span><span className="truncate">{j.sheetName}</span><span className={j.status==="DONE"?"text-emerald-300":j.status==="FAILED"?"text-red-300":j.status==="RUNNING"?"text-cyan-300":"text-neutral-400"}>{j.status}</span><span>{j.progress}%</span><span className="truncate text-neutral-500">{j.message||""}</span></div>)}</div></div>}{autoCadConnected&&<button disabled={nativeBusy} onClick={nativePublish} className="px-4 py-2 rounded bg-violet-500 text-black font-semibold disabled:opacity-40">AutoCAD Native Publish → PDF</button>}<div className="text-xs text-neutral-500">Danh sách Sheet chỉ render tối đa 100 dòng/trang để giảm lag. Bản Standalone dùng Model render cho từng sheet. Khi AutoCAD Bridge online nên dùng Native Publish để đảm bảo Layout/Viewport/Page Setup/CTB đúng DWG.</div></div>}
   {tab==="SHEETSET"&&<div className="space-y-4"><div className="flex gap-2"><button onClick={openSheetSet} className="px-4 py-2 rounded bg-cyan-500 text-black font-semibold flex gap-2"><BookOpen className="w-4 h-4"/>Mở Sheet Set</button><button onClick={saveSet} className="px-4 py-2 rounded bg-neutral-800 border border-neutral-700 text-neutral-200 flex gap-2"><Save className="w-4 h-4"/>Lưu HNL Sheet Set</button><button onClick={()=>setSheetSet(sheetSetFromLayouts(layouts,viewports,printer||undefined,"monochrome.ctb"))} className="px-4 py-2 rounded bg-neutral-800 border border-neutral-700">Tạo từ Layout hiện tại</button></div>{sheetSet&&<><div className="grid grid-cols-2 gap-4"><label className="text-sm text-neutral-300">Tên Sheet Set<input value={sheetSet.name} onChange={e=>setSheetSet({...sheetSet,name:e.target.value})} className="mt-1 w-full p-2 rounded bg-neutral-900 border border-neutral-700"/></label><label className="text-sm text-neutral-300">Default Plot Style<input value={sheetSet.defaultPlotStyle||""} onChange={e=>setSheetSet({...sheetSet,defaultPlotStyle:e.target.value})} className="mt-1 w-full p-2 rounded bg-neutral-900 border border-neutral-700"/></label></div><div className="p-3 rounded border border-neutral-800 bg-[#1d2025] text-sm text-neutral-300">{sheetSet.sheets.length} sheet(s). {nativeDstPath?"Đang chỉnh AutoCAD DST native qua Bridge; thay đổi chỉ commit khi bấm Apply DST Changes.":"HNL Sheet Set JSON không cần load toàn bộ DWG."}</div>{nativeDstPath&&<div className="text-xs text-violet-300 break-all">DST: {nativeDstPath}</div>}<div className="border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[110px_1fr_160px_1fr] bg-neutral-900 p-2 text-[10px] text-neutral-500"><span>Sheet No.</span><span>Title</span><span>Layout</span><span>Info</span></div><div className="max-h-[360px] overflow-auto">{sheetSet.sheets.slice(0,500).map((sh,idx)=><div key={sh.id} className="grid grid-cols-[110px_1fr_160px_1fr] p-2 border-t border-neutral-800 text-xs items-center"><input value={sh.sheetNo} onChange={e=>setSheetSet(cur=>cur?{...cur,sheets:cur.sheets.map((x,j)=>j===idx?{...x,sheetNo:e.target.value}:x)}:cur)} className="w-24 p-1 rounded bg-neutral-900 border border-neutral-700"/><input value={sh.sheetName} onChange={e=>setSheetSet(cur=>cur?{...cur,sheets:cur.sheets.map((x,j)=>j===idx?{...x,sheetName:e.target.value}:x)}:cur)} className="mr-2 p-1 rounded bg-neutral-900 border border-neutral-700"/><span className="truncate">{sh.layoutName||"-"}</span><span className="truncate text-neutral-500">{sh.messages.join("; ")}</span></div>)}</div></div>{nativeDstPath&&<button disabled={nativeBusy} onClick={applyNativeDstChanges} className="px-4 py-2 rounded bg-violet-500 text-black font-semibold disabled:opacity-40">Apply DST Changes</button>}</>}</div>}
   {tab==="DEVICE"&&<div className="grid grid-cols-2 gap-5"><Panel title="Máy in / Plotter Windows"><div className="space-y-2 max-h-[420px] overflow-auto">{printers.length?printers.map(p=><button key={p.name} onClick={()=>setPrinter(p.name)} className={`w-full text-left p-3 rounded border ${printer===p.name?"border-cyan-700 bg-cyan-950/20":"border-neutral-800 bg-[#1d2025]"}`}><div className="text-sm text-neutral-200">{p.displayName||p.name}{p.isDefault&&<span className="ml-2 text-emerald-300 text-xs">Default</span>}</div><div className="text-xs text-neutral-600 mt-1">{p.description||p.name}</div></button>):<div className="text-neutral-500">Không lấy được danh sách máy in. Tính năng này cần chạy trong EXE Electron.</div>}</div></Panel><Panel title="Nét in / Plot Style"><label className="text-sm text-neutral-300">Lineweight scale <input type="number" step=".1" min=".1" max="5" value={lineScale} onChange={e=>setLineScale(Number(e.target.value)||1)} className="ml-3 w-24 p-1 bg-neutral-900 border border-neutral-700 rounded"/></label><div className="mt-3 text-sm text-neutral-400">Standalone hỗ trợ preview màu/monochrome và lineweight từ HNL layers. CTB/STB thực là file AutoCAD và cần AutoCAD Bridge để đọc/chỉnh mapping native an toàn.</div><div className={`mt-4 p-3 rounded border ${autoCadConnected?"border-emerald-800 bg-emerald-950/20 text-emerald-300":"border-neutral-700 bg-neutral-900 text-neutral-500"}`}>{autoCadConnected?"AutoCAD Bridge Connected — có thể đọc Plot Device / Plot Style / Layout native.":"AutoCAD Bridge Offline — CTB/STB/PC3 native chưa khả dụng."}</div>{autoCadConnected&&<div className="mt-3"><button disabled={nativeBusy} onClick={refreshNative} className="px-3 py-2 rounded bg-violet-500/20 border border-violet-700 text-violet-200 text-xs">{nativeBusy?"Đang đọc AutoCAD...":"Đọc cấu hình Plot Native"}</button>{(nativeDevices.length>0||nativeStyles.length>0||nativeLayouts.length>0)&&<div className="mt-3 grid grid-cols-3 gap-2 text-xs"><div className="p-2 rounded bg-neutral-900 border border-neutral-800"><b className="text-neutral-300">Devices</b><div className="mt-1 max-h-24 overflow-auto text-neutral-500">{nativeDevices.map(x=><div key={x}>{x}</div>)}</div></div><div className="p-2 rounded bg-neutral-900 border border-neutral-800"><b className="text-neutral-300">CTB/STB</b><div className="mt-1 max-h-24 overflow-auto text-neutral-500">{nativeStyles.map(x=><div key={x}>{x}</div>)}</div></div><div className="p-2 rounded bg-neutral-900 border border-neutral-800"><b className="text-neutral-300">Layouts</b><div className="mt-1 max-h-24 overflow-auto text-neutral-500">{nativeLayouts.map((x:any)=><div key={x.name}>{x.name} • {x.device||"-"} • {x.style||"-"}</div>)}</div></div></div>}</div>}<div className="mt-4 border border-neutral-800 rounded overflow-hidden"><div className="grid grid-cols-[1fr_100px_100px] bg-neutral-900 px-2 py-2 text-[10px] text-neutral-500"><span>Layer</span><span>Lineweight</span><span>Color</span></div><div className="max-h-64 overflow-auto">{layers.slice(0,200).map(l=><div key={l.name} className="grid grid-cols-[1fr_100px_100px] px-2 py-1.5 border-t border-neutral-800 text-xs items-center"><span className="truncate">{l.name}</span><input type="number" step=".01" min="0" value={layerPlotOverrides[l.name]?.lineweight??l.lineweight??0.18} onChange={e=>setLayerPlotOverrides(o=>({...o,[l.name]:{...(o[l.name]||{}),lineweight:Number(e.target.value)}}))} className="w-20 bg-neutral-900 border border-neutral-700 rounded px-1 py-0.5"/><input type="color" value={layerPlotOverrides[l.name]?.color||l.color||"#ffffff"} onChange={e=>setLayerPlotOverrides(o=>({...o,[l.name]:{...(o[l.name]||{}),color:e.target.value}}))} className="w-10 h-6 bg-transparent"/></div>)}</div></div><button onClick={()=>setLayerPlotOverrides({})} className="mt-2 px-2 py-1 rounded bg-neutral-800 text-xs">Reset Plot Overrides</button></Panel></div>}
  </div>
 </div></div>;
};
const Panel:React.FC<{title:string;children:React.ReactNode}>=({title,children})=><div className="p-4 rounded-xl border border-neutral-800 bg-[#1d2025]"><div className="text-sm font-semibold text-neutral-200 mb-3">{title}</div>{children}</div>;
