import React, { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle, Boxes, Building2, Calculator, CheckCircle2, FileCheck2,
  FileSearch, FolderOpen, Layers3, Library, PackagePlus, Ruler, ShieldCheck,
  Sparkles, Star, Table2, Wand2, X
} from "lucide-react";
import {
  ApprovedMaterialRecord,
  HNL_BOARD_MODULE,
  HNL_BUILTIN_LIBRARY,
  HNL_DETAIL_TEMPLATES,
  HNL_PROJECT_TEMPLATES,
  SmartCeilingConfig,
  SmartWallConfig,
  applyWallDivision,
  auditCeilingConfig,
  auditWallConfig,
  calculateCeilingBoq,
  calculateWallBoq,
  createDefaultCeilingConfig,
  resolveCeilingPreset,
  createDefaultWallConfig,
} from "../../lib/smartShopdrawingPlatform";
import { MANUFACTURER_CEILING_KNOWLEDGE } from "../../lib/manufacturerCeilingKnowledge";

type Tab = "OVERVIEW"|"LIBRARY"|"CEILING"|"WALL"|"APPROVED"|"BOQ"|"AUDIT"|"DETAIL"|"TEMPLATE"|"DWG";

interface Props {
  isOpen:boolean;
  onClose:()=>void;
  autoCadConnected:boolean;
  onBridgeAction?:(action:string,payload:any)=>Promise<any>;
  onOpenDwg?:(mode:"AUTO"|"AUTOCAD_NATIVE"|"HNL_CANVAS"|"DIRECT_DWG")=>void;
  onOpenSectionGenerator?:()=>void;
}

const loadJson=<T,>(key:string,fallback:T):T=>{
  try{return JSON.parse(localStorage.getItem(key)||"") as T}catch{return fallback}
};

export const HnlSmartShopdrawingPlatformModal:React.FC<Props>=({
  isOpen,onClose,autoCadConnected,onBridgeAction,onOpenDwg,onOpenSectionGenerator
})=>{
  const [tab,setTab]=useState<Tab>("OVERVIEW");
  const [ceiling,setCeiling]=useState<SmartCeilingConfig>(()=>createDefaultCeilingConfig());
  const [wall,setWall]=useState<SmartWallConfig>(()=>createDefaultWallConfig());
  const [approved,setApproved]=useState<ApprovedMaterialRecord[]>(()=>loadJson("hnl.approvedMaterials.v1",[]));
  const [customLibrary,setCustomLibrary]=useState<any[]>(()=>loadJson("hnl.smartLibrary.custom.v1",[]));
  const [favoriteIds,setFavoriteIds]=useState<string[]>(()=>loadJson("hnl.smartLibrary.favorites.v1",[]));
  const [recentIds,setRecentIds]=useState<string[]>(()=>loadJson("hnl.smartLibrary.recent.v1",[]));
  const [templateId,setTemplateId]=useState("AIRPORT");
  const [message,setMessage]=useState("");
  const [areaM2,setAreaM2]=useState(100);
  const [wallLengthMm,setWallLengthMm]=useState(6000);

  useEffect(()=>{localStorage.setItem("hnl.approvedMaterials.v1",JSON.stringify(approved))},[approved]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.custom.v1",JSON.stringify(customLibrary))},[customLibrary]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.favorites.v1",JSON.stringify(favoriteIds))},[favoriteIds]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.recent.v1",JSON.stringify(recentIds))},[recentIds]);

  const auditIssues=useMemo(()=>[...auditCeilingConfig(ceiling),...auditWallConfig(wall)],[ceiling,wall]);
  const boq=useMemo(()=>[...calculateCeilingBoq(areaM2,ceiling),...calculateWallBoq(wallLengthMm,wall)],[areaM2,wallLengthMm,ceiling,wall]);

  if(!isOpen)return null;

  const bridge=async(action:string,payload:any)=>{
    if(!autoCadConnected||!onBridgeAction){setMessage("Cần AutoCAD Connected cho thao tác DWG native.");return null}
    const r=await onBridgeAction(action,payload);
    setMessage(r?.ok?`${action}: OK`:`${action}: ${r?.error||r?.reason||"Bridge error"}`);
    return r;
  };

  const addApproved=()=>{
    const item:ApprovedMaterialRecord={
      id:`mat_${Date.now()}`,projectId:"CURRENT",category:"CEILING",
      manufacturer:"Knauf",systemName:"Pro",documentName:"Approved Material",
      revision:"R0",status:"APPROVED",approvedDate:new Date().toISOString().slice(0,10),
      notes:"Kiểm tra/chỉnh lại theo submittal dự án.",
      parameters:{mainSpacingMm:800,hangerSpacingMm:900,crossSpacingMm:1220/3}
    };
    setApproved(v=>[item,...v]);
  };

  const applyTemplate=()=>{
    const t=HNL_PROJECT_TEMPLATES.find(x=>x.id===templateId);
    if(!t)return;
    setCeiling(v=>({...v,mainSpacingMm:t.defaultCeilingMainMm,hangerSpacingMm:t.defaultCeilingHangerMm}));
    setWall(v=>applyWallDivision(v,t.defaultWallDivision));
    localStorage.setItem("hnl.projectTemplate.active.v1",JSON.stringify(t));
    setMessage(`Đã áp dụng template ${t.name}.`);
  };

  const applyManufacturerSystem=(systemId:string)=>{
    const preset=resolveCeilingPreset(systemId);
    setCeiling(v=>({
      ...v,
      manufacturerSystemId:systemId,
      mainSpacingMm:preset.mainSpacingMm,
      hangerSpacingMm:preset.hangerSpacingMm,
      crossSpacingMm:1220/3,
    }));
    setMessage(`${preset.manufacturer} • ${preset.systemName}: main/hanger theo dữ liệu hệ; cross giữ HNL module 1220/3 trừ khi Approved Submittal override.`);
  };

  const applyApprovedToCeiling=(item:ApprovedMaterialRecord)=>{
    const p:any=item.parameters||{};
    setCeiling(v=>({
      ...v,
      mainSpacingMm:Number(p.mainSpacingMm ?? v.mainSpacingMm),
      hangerSpacingMm:Number(p.hangerSpacingMm ?? v.hangerSpacingMm),
      crossSpacingMm:Number(p.crossSpacingMm ?? v.crossSpacingMm),
    }));
    setMessage(`Đã ưu tiên Approved Material ${item.manufacturer} ${item.systemName} ${item.revision}.`);
  };

  const applyApprovedToWall=(item:ApprovedMaterialRecord)=>{
    const p:any=item.parameters||{};
    const division=(Number(p.studDivision)===2?2:3) as 2|3;
    setWall(v=>({
      ...applyWallDivision(v,division),
      heightMm:Number(p.heightMm ?? v.heightMm),
      studProfile:String(p.studProfile ?? v.studProfile),
      trackProfile:String(p.trackProfile ?? v.trackProfile),
    }));
    setMessage(`Đã ưu tiên Approved Wall ${item.manufacturer} ${item.systemName} ${item.revision}.`);
  };

  const tabs:[Tab,string,React.ReactNode][]=[
    ["OVERVIEW","Tổng quan",<Sparkles className="w-4 h-4"/>],
    ["LIBRARY","Library",<Library className="w-4 h-4"/>],
    ["CEILING","Smart Ceiling",<Layers3 className="w-4 h-4"/>],
    ["WALL","Smart Wall",<Building2 className="w-4 h-4"/>],
    ["APPROVED","Approved",<FileCheck2 className="w-4 h-4"/>],
    ["BOQ","BOQ",<Calculator className="w-4 h-4"/>],
    ["AUDIT","Audit",<ShieldCheck className="w-4 h-4"/>],
    ["DETAIL","Detail",<Ruler className="w-4 h-4"/>],
    ["TEMPLATE","Template",<Boxes className="w-4 h-4"/>],
    ["DWG","Mở DWG",<FolderOpen className="w-4 h-4"/>],
  ];

  return <div className="fixed inset-0 z-[70] bg-black/75 backdrop-blur-sm flex items-center justify-center p-3">
    <div className="w-full max-w-7xl max-h-[94vh] bg-[#17191d] border border-neutral-700 rounded-xl shadow-2xl overflow-hidden flex flex-col">
      <div className="h-14 px-4 border-b border-neutral-800 bg-[#111317] flex items-center justify-between shrink-0">
        <div>
          <div className="text-sm font-bold text-white">HNL Smart Shopdrawing Platform <span className="text-cyan-400">v2.6</span></div>
          <div className="text-[10px] text-neutral-500">Library • Ceiling • Wall • Approved Material • BOQ • Audit • Detail • Template • DWG</div>
        </div>
        <div className="flex items-center gap-2">
          <span className={`text-[10px] px-2 py-1 rounded border ${autoCadConnected?"text-emerald-300 border-emerald-700 bg-emerald-950/30":"text-neutral-400 border-neutral-700"}`}>{autoCadConnected?"AutoCAD Connected":"Standalone"}</span>
          <button onClick={onClose} className="p-2 hover:bg-neutral-800 rounded"><X className="w-4 h-4"/></button>
        </div>
      </div>

      <div className="flex border-b border-neutral-800 overflow-x-auto shrink-0 bg-[#14161a]">
        {tabs.map(([id,label,icon])=><button key={id} onClick={()=>setTab(id)} className={`px-3 py-2.5 text-[10px] font-semibold flex items-center gap-1.5 whitespace-nowrap border-b-2 ${tab===id?"border-cyan-500 text-cyan-300 bg-cyan-950/20":"border-transparent text-neutral-500 hover:text-neutral-200"}`}>{icon}{label}</button>)}
      </div>

      <div className="flex-1 min-h-0 overflow-y-auto p-4">
        {tab==="OVERVIEW"&&<div className="grid md:grid-cols-2 xl:grid-cols-4 gap-3">
          {[
            ["Module tấm","1220 × 2440 mm","Trần: 1220/3 = 406.67"],
            ["Vách","1220/3 hoặc 1220/2","406.67 / 610 mm"],
            ["Dữ liệu hãng",`${MANUFACTURER_CEILING_KNOWLEDGE.length} hệ/reference`,"Knauf • Vĩnh Tường • Lê Trần • I.S"],
            ["Approved",`${approved.filter(x=>x.status==="APPROVED").length} bản duyệt`,"Ưu tiên cao nhất khi AI/Generator chạy"],
          ].map(([a,b,c])=><div key={a} className="p-4 rounded-xl bg-neutral-900 border border-neutral-800"><div className="text-[10px] text-neutral-500">{a}</div><div className="mt-1 text-lg font-bold text-white">{b}</div><div className="mt-1 text-[10px] text-cyan-300">{c}</div></div>)}
          <div className="md:col-span-2 xl:col-span-4 p-4 rounded-xl border border-emerald-800/60 bg-emerald-950/20 text-xs text-neutral-300">
            <b className="text-emerald-300">Thứ tự quyết định:</b> Approved Material/Submittal → Project Spec → Manufacturer current catalog → HNL project rule → AI suggestion.
            AI không được trộn bước xương giữa hai hệ hãng khác nhau.
          </div>
        </div>}

        {tab==="LIBRARY"&&<div className="space-y-4">
          <div className="flex items-center gap-3 text-[10px] text-neutral-400">
            <span>Built-in: {HNL_BUILTIN_LIBRARY.length}</span><span>Custom: {customLibrary.length}</span><span className="text-amber-300">Favorites: {favoriteIds.length}</span><span>Recent: {recentIds.length}</span>
          </div>
          <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-2">
            {[...HNL_BUILTIN_LIBRARY,...customLibrary].map((it:any)=><div key={it.id} className="p-3 bg-neutral-900 border border-neutral-800 rounded-lg">
              <div className="flex justify-between gap-2"><div><b className="text-xs text-white">{it.name}</b><div className="text-[9px] text-neutral-500">{it.category} • {it.layer}</div></div><PackagePlus className="w-4 h-4 text-cyan-400"/></div>
              <div className="text-[10px] text-neutral-400 mt-2">{it.description}</div>
              <div className="mt-2 flex items-center gap-1.5">
                <button onClick={()=>{setRecentIds(v=>[it.id,...v.filter(x=>x!==it.id)].slice(0,20));void bridge("INSERT_LIBRARY_BLOCK",{symbolKey:it.symbolKey||"CUSTOM_DWG",name:it.name,layer:it.layer,sourceDwg:it.sourceDwg||null})}} className="px-2 py-1.5 rounded bg-cyan-700/30 border border-cyan-700 text-cyan-200 text-[10px]">Chèn vào AutoCAD</button>
                <button title="Favorite" onClick={()=>setFavoriteIds(v=>v.includes(it.id)?v.filter(x=>x!==it.id):[...v,it.id])} className={`p-1.5 rounded border ${favoriteIds.includes(it.id)?"border-amber-600 text-amber-300 bg-amber-950/30":"border-neutral-700 text-neutral-500"}`}><Star className="w-3.5 h-3.5" fill={favoriteIds.includes(it.id)?"currentColor":"none"}/></button>
                {recentIds.includes(it.id)&&<span className="text-[9px] text-neutral-500">Recent</span>}
              </div>
            </div>)}
          </div>
          <button onClick={async()=>{
            const native=(window as any).electronNative; const r=await native?.selectLibraryDwg?.();
            if(r?.success&&r.filePath)setCustomLibrary(v=>[{id:`custom_${Date.now()}`,name:r.fileName||"Custom DWG Block",category:"CUSTOM",symbolKey:"CUSTOM_DWG",layer:"HNL-CUSTOM-BLOCK",description:"Block DWG do người dùng thêm.",sourceDwg:r.filePath,tags:["custom"]},...v])
          }} className="px-3 py-2 bg-neutral-800 hover:bg-neutral-700 rounded text-xs">+ Nạp block DWG riêng</button>
        </div>}

        {tab==="CEILING"&&<div className="grid lg:grid-cols-[1fr_360px] gap-4">
          <div className="space-y-3">
            <div className="grid md:grid-cols-3 gap-3">
              <label className="text-[10px]">Hệ hãng<select value={ceiling.manufacturerSystemId} onChange={e=>applyManufacturerSystem(e.target.value)} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs">
                <option value="HNL_PROJECT_RULE">HNL Project Rule</option>{MANUFACTURER_CEILING_KNOWLEDGE.map(x=><option key={x.id} value={x.id}>{x.manufacturer} — {x.systemName}</option>)}
              </select></label>
              <label className="text-[10px]">Xương chính @<input type="number" value={ceiling.mainSpacingMm} onChange={e=>setCeiling(v=>({...v,mainSpacingMm:+e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
              <label className="text-[10px]">Ty @<input type="number" value={ceiling.hangerSpacingMm} onChange={e=>setCeiling(v=>({...v,hangerSpacingMm:+e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
            </div>
            <div className="p-3 border border-cyan-800 bg-cyan-950/20 rounded">
              <div className="text-[10px] text-neutral-400">Xương phụ trần chìm theo tấm 1220×2440</div>
              <div className="text-xl font-mono font-bold text-cyan-300">1220 / 3 = {(1220/3).toFixed(2)} mm</div>
            </div>
            <div className="grid md:grid-cols-3 gap-3">
              <label className="text-[10px]">Cao độ<input type="number" value={ceiling.elevationMm} onChange={e=>setCeiling(v=>({...v,elevationMm:+e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
              <label className="text-[10px]">Góc tấm<input type="number" value={ceiling.boardDirectionDeg} onChange={e=>setCeiling(v=>({...v,boardDirectionDeg:+e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
              <label className="text-[10px]">Diện tích BOQ<input type="number" value={areaM2} onChange={e=>setAreaM2(+e.target.value)} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
            </div>
            <button onClick={()=>void bridge("CREATE_CEILING_SMART",{...ceiling,crossSpacing:1220/3,mainSpacing:ceiling.mainSpacingMm,hangerSpacing:ceiling.hangerSpacingMm,boardWidth:1220,boardLength:2440})} className="px-4 py-2 rounded bg-cyan-600 text-white text-xs font-semibold">Tạo Smart Ceiling từ Polyline đang chọn</button>
          </div>
          <div className="p-3 bg-neutral-900 border border-neutral-800 rounded-lg text-[10px] text-neutral-400">
            <b className="text-white">Generator 2.0</b><br/>Boundary → điểm xuất phát tấm → hướng tấm → xương phụ theo module → xương chính → ty → HNL metadata → BOQ/Audit.
          </div>
        </div>}

        {tab==="WALL"&&<div className="space-y-4">
          <div className="grid md:grid-cols-4 gap-3">
            <button onClick={()=>setWall(v=>applyWallDivision(v,3))} className={`p-3 rounded border text-left ${wall.studDivision===3?"border-cyan-500 bg-cyan-950/20":"border-neutral-700 bg-neutral-900"}`}><b className="text-white">1220 / 3</b><div className="text-cyan-300 font-mono">{(1220/3).toFixed(2)} mm</div></button>
            <button onClick={()=>setWall(v=>applyWallDivision(v,2))} className={`p-3 rounded border text-left ${wall.studDivision===2?"border-cyan-500 bg-cyan-950/20":"border-neutral-700 bg-neutral-900"}`}><b className="text-white">1220 / 2</b><div className="text-cyan-300 font-mono">{(1220/2).toFixed(2)} mm</div></button>
            <label className="text-[10px]">Cao vách<input type="number" value={wall.heightMm} onChange={e=>setWall(v=>({...v,heightMm:+e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
            <label className="text-[10px]">Chiều dài BOQ<input type="number" value={wallLengthMm} onChange={e=>setWallLengthMm(+e.target.value)} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label>
          </div>
          <div className="grid md:grid-cols-2 gap-3"><label className="text-[10px]">Stud<input value={wall.studProfile} onChange={e=>setWall(v=>({...v,studProfile:e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label><label className="text-[10px]">Track<input value={wall.trackProfile} onChange={e=>setWall(v=>({...v,trackProfile:e.target.value}))} className="mt-1 w-full bg-neutral-900 border border-neutral-700 rounded p-2 text-xs"/></label></div>
          <button onClick={()=>void bridge("CREATE_WALL_SYSTEM",{...wall})} className="px-4 py-2 rounded bg-cyan-600 text-white text-xs font-semibold">Tạo Smart Wall trên AutoCAD</button>
        </div>}

        {tab==="APPROVED"&&<div className="space-y-3">
          <div className="flex items-center justify-between"><div className="text-xs text-neutral-400">Approved Material/Submittal được ưu tiên cao hơn catalog và AI.</div><button onClick={addApproved} className="px-3 py-2 rounded bg-emerald-700 text-xs">+ Thêm bản duyệt</button></div>
          {approved.map((x,i)=><div key={x.id} className="p-2 bg-neutral-900 border border-neutral-800 rounded space-y-2">
            <div className="grid md:grid-cols-[120px_1fr_1fr_90px_120px] gap-2">
              <select value={x.category} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,category:e.target.value as any}:a))} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]"><option>CEILING</option><option>WALL</option><option>BOARD</option><option>FRAMING</option><option>ACCESSORY</option></select>
              <input value={x.manufacturer} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,manufacturer:e.target.value}:a))} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]"/>
              <input value={x.systemName} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,systemName:e.target.value}:a))} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]"/>
              <input value={x.revision} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,revision:e.target.value}:a))} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]"/>
              <select value={x.status} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,status:e.target.value as any}:a))} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]"><option>DRAFT</option><option>SUBMITTED</option><option>APPROVED</option><option>REJECTED</option><option>SUPERSEDED</option></select>
            </div>
            {x.category==="CEILING"&&<div className="grid md:grid-cols-3 gap-2 text-[10px]">
              <label>Main @<input type="number" value={Number((x.parameters as any)?.mainSpacingMm ?? 800)} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),mainSpacingMm:+e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"/></label>
              <label>Cross @<input type="number" step="0.01" value={Number((x.parameters as any)?.crossSpacingMm ?? 1220/3)} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),crossSpacingMm:+e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"/></label>
              <label>Hanger @<input type="number" value={Number((x.parameters as any)?.hangerSpacingMm ?? 900)} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),hangerSpacingMm:+e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"/></label>
            </div>}
            {x.category==="WALL"&&<div className="grid md:grid-cols-3 gap-2 text-[10px]">
              <label>Stud module<select value={Number((x.parameters as any)?.studDivision ?? 3)} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),studDivision:+e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"><option value={3}>1220/3 = 406.67</option><option value={2}>1220/2 = 610</option></select></label>
              <label>Stud profile<input value={String((x.parameters as any)?.studProfile ?? "C75")} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),studProfile:e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"/></label>
              <label>Track profile<input value={String((x.parameters as any)?.trackProfile ?? "U76")} onChange={e=>setApproved(v=>v.map((a,j)=>j===i?{...a,parameters:{...(a.parameters||{}),trackProfile:e.target.value}}:a))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5"/></label>
            </div>}
            <div className="flex flex-wrap items-center gap-2 text-[10px]">
              <button onClick={async()=>{const r=await (window as any).electronNative?.selectApprovedDocument?.();if(r?.success)setApproved(v=>v.map((a,j)=>j===i?{...a,sourcePath:r.filePath,documentName:r.fileName}:a))}} className="px-2 py-1 rounded bg-neutral-800">Gắn tài liệu</button>
              {x.sourcePath&&<span className="text-emerald-300 truncate max-w-[420px]">{x.documentName} • {x.sourcePath}</span>}
              {x.status==="APPROVED"&&x.category==="CEILING"&&<button onClick={()=>applyApprovedToCeiling(x)} className="px-2 py-1 rounded bg-emerald-800 text-emerald-100">Áp vào Smart Ceiling</button>}
              {x.status==="APPROVED"&&x.category==="WALL"&&<button onClick={()=>applyApprovedToWall(x)} className="px-2 py-1 rounded bg-emerald-800 text-emerald-100">Áp vào Smart Wall</button>}
              <button onClick={()=>setApproved(v=>v.filter((_,j)=>j!==i))} className="text-red-300 ml-auto">Xóa</button>
            </div>
          </div>)}
        </div>}

        {tab==="BOQ"&&<div className="space-y-3"><table className="w-full text-[10px] border-collapse"><thead><tr className="text-neutral-400"><th className="text-left p-2 border-b border-neutral-800">Code</th><th className="text-left p-2 border-b border-neutral-800">Mô tả</th><th className="text-right p-2 border-b border-neutral-800">SL</th><th className="text-left p-2 border-b border-neutral-800">ĐVT</th></tr></thead><tbody>{boq.map((x,i)=><tr key={i}><td className="p-2 border-b border-neutral-900 font-mono text-cyan-300">{x.code}</td><td className="p-2 border-b border-neutral-900">{x.description}</td><td className="p-2 border-b border-neutral-900 text-right">{x.quantity}</td><td className="p-2 border-b border-neutral-900">{x.unit}</td></tr>)}</tbody></table>
          <button onClick={()=>void bridge("GET_HNL_BOQ",{})} className="px-3 py-2 rounded bg-neutral-800 text-xs">Đọc BOQ từ Smart Object trong DWG</button></div>}

        {tab==="AUDIT"&&<div className="space-y-2">
          {auditIssues.length===0?<div className="p-4 rounded border border-emerald-800 bg-emerald-950/20 text-emerald-300 text-xs flex gap-2"><CheckCircle2 className="w-4 h-4"/>Cấu hình hiện tại không có lỗi module HNL.</div>:auditIssues.map(x=><div key={x.id} className="p-3 border border-amber-800 bg-amber-950/20 rounded text-xs"><b className="text-amber-300">{x.severity} • {x.title}</b><div className="text-neutral-400 mt-1">{x.detail}</div></div>)}
          <button onClick={()=>void bridge("AUDIT_HNL_SHOPDRAWING",{})} className="px-3 py-2 rounded bg-neutral-800 text-xs">Audit DWG native</button>
        </div>}

        {tab==="DETAIL"&&<div className="grid md:grid-cols-2 gap-3">{HNL_DETAIL_TEMPLATES.map(x=><div key={x.id} className="p-3 bg-neutral-900 border border-neutral-800 rounded"><b className="text-xs text-white">{x.name}</b><div className="text-[10px] text-neutral-500">Scale {x.scale} • {x.layers.join(" / ")}</div></div>)}<button onClick={onOpenSectionGenerator} className="md:col-span-2 px-3 py-2 rounded bg-cyan-700 text-xs">Mở Section / Detail Generator hiện có</button></div>}

        {tab==="TEMPLATE"&&<div className="space-y-3"><div className="grid md:grid-cols-2 xl:grid-cols-4 gap-2">{HNL_PROJECT_TEMPLATES.map(x=><button key={x.id} onClick={()=>setTemplateId(x.id)} className={`p-3 text-left rounded border ${templateId===x.id?"border-cyan-500 bg-cyan-950/20":"border-neutral-700 bg-neutral-900"}`}><b className="text-xs">{x.name}</b><div className="text-[10px] text-neutral-500 mt-1">{x.description}</div></button>)}</div><button onClick={applyTemplate} className="px-3 py-2 bg-cyan-700 rounded text-xs">Áp dụng Template</button></div>}

        {tab==="DWG"&&<div className="grid md:grid-cols-2 gap-4">
          <button onClick={()=>onOpenDwg?.("DIRECT_DWG")} className="p-5 text-left rounded-xl border border-fuchsia-800 bg-fuchsia-950/20"><div className="font-bold text-fuchsia-300">Direct DWG Edit • HNL</div><div className="text-[10px] text-neutral-400 mt-2">AutoCAD giữ database DWG; HNL Canvas đồng bộ entity/selection và tạo-sửa hình học native trực tiếp qua Bridge.</div></button>
          <button onClick={()=>onOpenDwg?.("AUTOCAD_NATIVE")} className="p-5 text-left rounded-xl border border-emerald-800 bg-emerald-950/20"><div className="font-bold text-emerald-300">Mở DWG bằng AutoCAD Native</div><div className="text-[10px] text-neutral-400 mt-2">Full fidelity: Dynamic Block, Field, Xref, Layout, Plot, ObjectARX. Khuyến nghị cho chỉnh sửa chính thức.</div></button>
          <button onClick={()=>onOpenDwg?.("HNL_CANVAS")} className="p-5 text-left rounded-xl border border-cyan-800 bg-cyan-950/20"><div className="font-bold text-cyan-300">Mở DWG trên HNL Canvas</div><div className="text-[10px] text-neutral-400 mt-2">AutoCAD Bridge đọc DWG nền → xuất DXF tạm → HNL Canvas mở để xem/audit/AI/chỉnh 2D nhẹ. Không ghi đè DWG gốc.</div></button>
          <div className="md:col-span-2 p-3 border border-amber-800/50 bg-amber-950/10 rounded text-[10px] text-amber-200"><AlertTriangle className="inline w-3 h-3 mr-1"/>Không tuyên bố HNL có full native DWG decoder độc lập. Khi không có AutoCAD Bridge, HNL vẫn mở DXF trực tiếp; full DWG native cần Autodesk engine.</div>
        </div>}
      </div>

      <div className="h-12 px-4 border-t border-neutral-800 bg-[#111317] flex items-center justify-between shrink-0">
        <span className="text-[10px] text-neutral-400 truncate">{message}</span>
        <button onClick={onClose} className="px-3 py-1.5 rounded bg-neutral-800 text-xs">Đóng</button>
      </div>
    </div>
  </div>
}
