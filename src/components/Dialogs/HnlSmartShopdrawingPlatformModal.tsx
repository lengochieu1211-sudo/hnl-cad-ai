import React, { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle, Boxes, Building2, Calculator, CheckCircle2, Database,
  ExternalLink, FileCheck2, FilePlus2, FolderInput, FolderOpen, Layers3,
  Library, RefreshCw, Ruler, Search, ShieldCheck, SlidersHorizontal,
  Sparkles, Star, Trash2, X
} from "lucide-react";
import {
  ApprovedMaterialRecord,
  HNL_BOARD_MODULE,
  HNL_BUILTIN_LIBRARY,
  HNL_DETAIL_TEMPLATES,
  HNL_PROJECT_TEMPLATES,
  HNL_LIBRARY_CATEGORIES,
  HNL_LIBRARY_SCOPES,
  getDefaultLibraryLayer,
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
import { getHnlLayerStandard, HNL_CAD_LAYER_STANDARDS } from "../../lib/hnlCadStandards";

type Tab = "OVERVIEW"|"LIBRARY"|"CEILING"|"WALL"|"APPROVED"|"BOQ"|"AUDIT"|"DETAIL"|"TEMPLATE"|"DWG";

interface Props {
  isOpen:boolean;
  initialTab?:"OVERVIEW"|"LIBRARY";
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
  isOpen,initialTab="OVERVIEW",onClose,autoCadConnected,onBridgeAction,onOpenDwg,onOpenSectionGenerator
})=>{
  const [tab,setTab]=useState<Tab>(initialTab);
  const [ceiling,setCeiling]=useState<SmartCeilingConfig>(()=>createDefaultCeilingConfig());
  const [wall,setWall]=useState<SmartWallConfig>(()=>createDefaultWallConfig());
  const [approved,setApproved]=useState<ApprovedMaterialRecord[]>(()=>loadJson("hnl.approvedMaterials.v1",[]));
  const [customLibrary,setCustomLibrary]=useState<any[]>(()=>loadJson("hnl.smartLibrary.custom.v1",[]));
  const [favoriteIds,setFavoriteIds]=useState<string[]>(()=>loadJson("hnl.smartLibrary.favorites.v1",[]));
  const [recentIds,setRecentIds]=useState<string[]>(()=>loadJson("hnl.smartLibrary.recent.v1",[]));
  const [managedLibrary,setManagedLibrary]=useState<any[]>([]);
  const [libraryRoot,setLibraryRoot]=useState("");
  const [librarySearch,setLibrarySearch]=useState("");
  const [libraryScope,setLibraryScope]=useState("ALL");
  const [libraryCategory,setLibraryCategory]=useState("ALL");
  const [libraryOnlyFavorites,setLibraryOnlyFavorites]=useState(false);
  const [importScope,setImportScope]=useState("MY_LIBRARY");
  const [importCategory,setImportCategory]=useState("CUSTOM");
  const [importStorageMode,setImportStorageMode]=useState<"COPY"|"LINK">("COPY");
  const [selectedLibraryId,setSelectedLibraryId]=useState<string|null>(null);
  const [insertScale,setInsertScale]=useState(1);
  const [insertRotation,setInsertRotation]=useState(0);
  const [lastDynamic,setLastDynamic]=useState<any|null>(null);
  const [templateId,setTemplateId]=useState("AIRPORT");
  const [message,setMessage]=useState("");
  const [areaM2,setAreaM2]=useState(100);
  const [wallLengthMm,setWallLengthMm]=useState(6000);

  useEffect(()=>{localStorage.setItem("hnl.approvedMaterials.v1",JSON.stringify(approved))},[approved]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.custom.v1",JSON.stringify(customLibrary))},[customLibrary]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.favorites.v1",JSON.stringify(favoriteIds))},[favoriteIds]);
  useEffect(()=>{localStorage.setItem("hnl.smartLibrary.recent.v1",JSON.stringify(recentIds))},[recentIds]);
  useEffect(()=>{ if(isOpen) setTab(initialTab); },[isOpen,initialTab]);

  const reloadLibrary=async()=>{
    const native=(window as any).electronNative;
    const r=await native?.getLibraryIndex?.();
    if(r?.success){
      setManagedLibrary(Array.isArray(r.items)?r.items:[]);
      setLibraryRoot(String(r.root||""));
    }
  };
  useEffect(()=>{ if(isOpen) void reloadLibrary(); },[isOpen]);

  const auditIssues=useMemo(()=>[...auditCeilingConfig(ceiling),...auditWallConfig(wall)],[ceiling,wall]);
  const boq=useMemo(()=>[...calculateCeilingBoq(areaM2,ceiling),...calculateWallBoq(wallLengthMm,wall)],[areaM2,wallLengthMm,ceiling,wall]);

  const normalizedBuiltins = HNL_BUILTIN_LIBRARY.map((it:any)=>({
    ...it, scope:"HNL_STANDARD", storageMode:"BUILTIN",
    dynamicState:"STATIC",
    favorite:favoriteIds.includes(it.id),
  }));
  const normalizedLegacy = customLibrary.map((it:any)=>({
    ...it, scope:it.scope||"MY_LIBRARY", storageMode:it.storageMode||"LINK",
    dynamicState:it.dynamicState||"UNKNOWN", favorite:favoriteIds.includes(it.id),
  }));
  const allLibraryItems:any[]=[...normalizedBuiltins,...managedLibrary,...normalizedLegacy];
  const filteredLibraryItems=allLibraryItems.filter((it:any)=>{
    if(libraryScope!=="ALL" && it.scope!==libraryScope)return false;
    if(libraryCategory!=="ALL" && it.category!==libraryCategory)return false;
    if(libraryOnlyFavorites && !(it.favorite||favoriteIds.includes(it.id)))return false;
    const q=librarySearch.trim().toLowerCase();
    if(!q)return true;
    return [it.name,it.fileName,it.category,it.scope,it.layer,it.description,...(it.tags||[])]
      .filter(Boolean).join(" ").toLowerCase().includes(q);
  });
  const selectedLibrary=allLibraryItems.find((x:any)=>x.id===selectedLibraryId)||null;

  const importLibrary=async(sourceType:"FILES"|"FOLDER")=>{
    const native=(window as any).electronNative;
    const r=await native?.importLibraryItems?.({
      sourceType,scope:importScope,category:importCategory,storageMode:importStorageMode
    });
    if(!r?.success){ if(!r?.canceled)setMessage(r?.error||"Không nạp được thư viện."); return; }
    await reloadLibrary();
    setMessage(`Library: nạp ${r.imported?.length||0} DWG • trùng ${r.duplicates||0}${r.errors?.length?` • lỗi ${r.errors.length}`:""}.`);
  };

  const updateManagedItem=async(id:string,patch:any)=>{
    const native=(window as any).electronNative;
    const r=await native?.updateLibraryItem?.({id,patch});
    if(r?.success)await reloadLibrary();
    return r;
  };

  const inspectLibraryItem=async(it:any)=>{
    if(!it?.sourceDwg){setMessage("Block built-in không cần đọc file DWG.");return;}
    if(!autoCadConnected){setMessage("Cần AutoCAD Connected để đọc block definition/Dynamic Block.");return;}
    const r=await bridge("INSPECT_LIBRARY_DWG",{filePath:it.sourceDwg});
    const data=r?.result||r;
    if(!r?.ok && !Array.isArray(data?.definitions))return;
    const defs=Array.isArray(data?.definitions)?data.definitions:[];
    const dynamicCount=defs.filter((d:any)=>d.isDynamic).length;
    const state=defs.length===0?"STATIC":dynamicCount===0?"STATIC":dynamicCount===defs.length?"DYNAMIC":"MIXED";
    if(managedLibrary.some((x:any)=>x.id===it.id)){
      await updateManagedItem(it.id,{
        definitions:defs,dynamicState:state,
        selectedDefinition:it.selectedDefinition||(defs.length===1?defs[0].name:null)
      });
    }
    setMessage(`Đã đọc ${defs.length} definitions • Dynamic ${dynamicCount} • Model ${data?.modelEntityCount??0} entities.`);
  };

  const insertLibraryItem=async(it:any)=>{
    if(!autoCadConnected){setMessage("Cần AutoCAD + HNL Bridge Connected để chèn block.");return;}
    try{
      const std=await bridge("ENSURE_HNL_STANDARDS",{});
      if(std && std.ok===false)return;

      let definitions=Array.isArray(it.definitions)?it.definitions:[];
      let modelEntityCount:number|undefined=undefined;

      // Imported DWG: inspect automatically so we never silently insert an empty whole-drawing block.
      if(it.sourceDwg && definitions.length===0){
        setMessage("Đang đọc DWG thư viện để xác định block definition...");
        const inspect=await bridge("INSPECT_LIBRARY_DWG",{filePath:it.sourceDwg});
        if(!inspect?.ok)return;
        const info=inspect.result||inspect;
        definitions=Array.isArray(info?.definitions)?info.definitions:[];
        modelEntityCount=Number(info?.modelEntityCount||0);

        if(managedLibrary.some((x:any)=>x.id===it.id)){
          const dyn=definitions.filter((d:any)=>d.isDynamic).length;
          await updateManagedItem(it.id,{
            definitions,
            dynamicState:definitions.length===0?"STATIC":dyn===0?"STATIC":dyn===definitions.length?"DYNAMIC":"MIXED",
            selectedDefinition:definitions.length===1?definitions[0].name:null
          });
        }
      }

      let def=it.selectedDefinition||(definitions.length===1?definitions[0].name:null);

      if(it.sourceDwg && definitions.length>1 && !def){
        setMessage(`DWG có ${definitions.length} block definitions. Hãy chọn đúng "Block definition" ở panel bên phải rồi bấm Chèn.`);
        return;
      }

      if(it.sourceDwg && definitions.length===0 && modelEntityCount===0){
        setMessage("DWG thư viện không có geometry Model Space và không có block definition để chèn.");
        return;
      }

      const action=it.sourceDwg&&def?"IMPORT_LIBRARY_DEFINITION":"INSERT_LIBRARY_BLOCK";
      const payload=action==="IMPORT_LIBRARY_DEFINITION"
        ? {filePath:it.sourceDwg,definitionName:def,layer:it.layer||getDefaultLibraryLayer(it.category),scale:insertScale,rotationDeg:insertRotation}
        : {symbolKey:it.symbolKey||"CUSTOM_DWG",name:it.name,layer:it.layer||getDefaultLibraryLayer(it.category),sourceDwg:it.sourceDwg||null,scale:insertScale,rotationDeg:insertRotation};

      setMessage("Đang gửi block sang AutoCAD...");
      const r=await bridge(action,payload);
      if(!r?.ok)return;
      const data=r.result||r;

      setRecentIds(v=>[it.id,...v.filter(x=>x!==it.id)].slice(0,30));
      if(managedLibrary.some((x:any)=>x.id===it.id)){
        await updateManagedItem(it.id,{recentAt:new Date().toISOString()});
      }

      if(data?.awaitingPoint||data?.queued){
        setLastDynamic(null);
        setMessage(`✓ Đã gửi ${it.name} sang AutoCAD. Chuyển sang AutoCAD và chọn điểm chèn trên bản vẽ. Nếu không thấy prompt, gõ HNLINSERTPENDING.`);
        return;
      }

      setLastDynamic(data?.isDynamicBlock?{
        handle:data.handle,blockName:data.blockName,properties:data.dynamicProperties||[]
      }:null);
      setMessage(data?.isDynamicBlock
        ? `Đã chèn Dynamic Block ${data.blockName} • ${data.dynamicProperties?.length||0} properties.`
        : `Đã chèn block ${data.blockName||it.name}.`);
    }catch(e:any){
      setMessage(`Chèn Library lỗi: ${e?.message||String(e)}`);
    }
  };

  const toggleFavorite=async(it:any)=>{
    const active=Boolean(it.favorite||favoriteIds.includes(it.id));
    setFavoriteIds(v=>active?v.filter(x=>x!==it.id):[...v,it.id]);
    if(managedLibrary.some((x:any)=>x.id===it.id))await updateManagedItem(it.id,{favorite:!active});
  };

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
          <div className="rounded-xl border border-cyan-800/60 bg-cyan-950/15 p-3">
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="text-sm font-bold text-white flex items-center gap-2"><Library className="w-4 h-4 text-cyan-400"/>HNL Library Manager</div>
                <div className="text-[10px] text-neutral-400 mt-1">DWG • Dynamic Block native • COPY/LINK • Layer/Color/Linetype/Lineweight • Favorite/Recent.</div>
                {libraryRoot&&<div className="text-[9px] text-neutral-600 font-mono truncate mt-1">{libraryRoot}</div>}
              </div>
              <div className="flex gap-1.5 shrink-0">
                <button onClick={()=>void (window as any).electronNative?.openLibraryRoot?.()} className="p-2 rounded bg-neutral-800" title="Mở kho"><FolderOpen className="w-3.5 h-3.5"/></button>
                <button onClick={()=>void reloadLibrary()} className="p-2 rounded bg-neutral-800" title="Refresh"><RefreshCw className="w-3.5 h-3.5"/></button>
              </div>
            </div>
          </div>

          <div className="rounded-lg border border-neutral-800 bg-neutral-900/60 p-3 space-y-2">
            <div className="grid md:grid-cols-4 gap-2">
              <label className="text-[9px] text-neutral-500">Kho
                <select value={importScope} onChange={e=>setImportScope(e.target.value)} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                  {HNL_LIBRARY_SCOPES.filter(x=>x.id!=="HNL_STANDARD").map(x=><option key={x.id} value={x.id}>{x.label}</option>)}
                </select>
              </label>
              <label className="text-[9px] text-neutral-500">Nhóm
                <select value={importCategory} onChange={e=>setImportCategory(e.target.value)} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                  {HNL_LIBRARY_CATEGORIES.map(x=><option key={x.id} value={x.id}>{x.label}</option>)}
                </select>
              </label>
              <label className="text-[9px] text-neutral-500">Lưu file
                <select value={importStorageMode} onChange={e=>setImportStorageMode(e.target.value as "COPY"|"LINK")} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                  <option value="COPY">COPY vào kho HNL</option><option value="LINK">LINK file gốc</option>
                </select>
              </label>
              <div className="flex items-end gap-1.5">
                <button onClick={()=>void importLibrary("FILES")} className="flex-1 px-2 py-2 rounded bg-cyan-700 text-[10px] flex items-center justify-center gap-1"><FilePlus2 className="w-3.5 h-3.5"/>Nạp DWG</button>
                <button onClick={()=>void importLibrary("FOLDER")} className="flex-1 px-2 py-2 rounded bg-cyan-950 border border-cyan-700 text-[10px] flex items-center justify-center gap-1"><FolderInput className="w-3.5 h-3.5"/>Thư mục</button>
              </div>
            </div>
            <div className="text-[9px] text-neutral-500">Mặc định COPY để thư viện không mất liên kết khi file gốc bị di chuyển. LINK dành cho project/network library.</div>
          </div>

          <div className="grid lg:grid-cols-[minmax(0,1fr)_330px] gap-4">
            <div className="space-y-3 min-w-0">
              <div className="grid md:grid-cols-[1fr_145px_155px_auto] gap-2">
                <div className="relative">
                  <Search className="absolute left-2.5 top-2.5 w-3.5 h-3.5 text-neutral-600"/>
                  <input value={librarySearch} onChange={e=>setLibrarySearch(e.target.value)} placeholder="Tìm block / tag / layer..."
                    className="w-full bg-neutral-950 border border-neutral-700 rounded pl-8 pr-2 py-2 text-[10px]"/>
                </div>
                <select value={libraryScope} onChange={e=>setLibraryScope(e.target.value)} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                  <option value="ALL">Tất cả kho</option>{HNL_LIBRARY_SCOPES.map(x=><option key={x.id} value={x.id}>{x.label}</option>)}
                </select>
                <select value={libraryCategory} onChange={e=>setLibraryCategory(e.target.value)} className="bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                  <option value="ALL">Tất cả nhóm</option>{HNL_LIBRARY_CATEGORIES.map(x=><option key={x.id} value={x.id}>{x.label}</option>)}
                </select>
                <button onClick={()=>setLibraryOnlyFavorites(v=>!v)} className={`px-2 py-2 rounded border text-[10px] flex items-center gap-1 ${libraryOnlyFavorites?"border-amber-600 bg-amber-950/30 text-amber-300":"border-neutral-700 text-neutral-500"}`}><Star className="w-3 h-3"/>Fav</button>
              </div>

              <div className="flex flex-wrap gap-3 text-[9px] text-neutral-500">
                <span>Hiện <b className="text-neutral-200">{filteredLibraryItems.length}</b></span>
                <span>Managed <b className="text-neutral-200">{managedLibrary.length}</b></span>
                <span>Built-in <b className="text-neutral-200">{HNL_BUILTIN_LIBRARY.length}</b></span>
                <span>Dynamic <b className="text-emerald-300">{allLibraryItems.filter((x:any)=>x.dynamicState==="DYNAMIC").length}</b></span>
              </div>

              <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-2">
                {filteredLibraryItems.map((it:any)=>{
                  const std=getHnlLayerStandard(it.layer||getDefaultLibraryLayer(it.category));
                  const selected=selectedLibraryId===it.id;
                  return <button key={it.id} onClick={()=>setSelectedLibraryId(it.id)}
                    className={`text-left p-3 rounded-lg border ${selected?"border-cyan-500 bg-cyan-950/20":"border-neutral-800 bg-neutral-900 hover:border-neutral-600"}`}>
                    <div className="flex gap-2">
                      <div className="w-11 h-11 rounded border border-neutral-700 bg-neutral-950 flex items-center justify-center shrink-0"
                        style={{boxShadow:`inset 0 -3px 0 ${std?.color||"#4b5563"}`}}>
                        {it.dynamicState==="DYNAMIC"?<SlidersHorizontal className="w-5 h-5 text-emerald-400"/>:<Database className="w-5 h-5 text-cyan-400"/>}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-1"><b className="text-[11px] text-white truncate">{it.name}</b><Star className={`w-3 h-3 shrink-0 ${it.favorite||favoriteIds.includes(it.id)?"text-amber-300 fill-amber-300":"text-neutral-700"}`}/></div>
                        <div className="text-[9px] text-neutral-500 truncate">{it.scope||"MY_LIBRARY"} • {it.category}</div>
                        <div className="text-[9px] font-mono truncate mt-1" style={{color:std?.color||"#a3a3a3"}}>{it.layer||"—"} • {std?.linetype||"ByLayer"} • {std?.lineweight?.toFixed(2)||"—"}mm</div>
                        <div className={`text-[9px] mt-1 ${it.dynamicState==="DYNAMIC"?"text-emerald-300":it.dynamicState==="MIXED"?"text-amber-300":"text-neutral-600"}`}>{it.dynamicState||"UNKNOWN"}{it.definitions?.length?` • ${it.definitions.length} defs`:""}</div>
                      </div>
                    </div>
                  </button>
                })}
              </div>
            </div>

            <div className="rounded-xl border border-neutral-800 bg-[#121417] p-3 h-fit">
              {!selectedLibrary?<div className="py-12 text-center text-[10px] text-neutral-600">Chọn block để quản lý/chèn.</div>:<>
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0"><div className="text-sm font-bold text-white truncate">{selectedLibrary.name}</div><div className="text-[9px] text-neutral-500">{selectedLibrary.scope} • {selectedLibrary.storageMode}</div></div>
                  <button onClick={()=>void toggleFavorite(selectedLibrary)} className="p-1.5 rounded border border-neutral-700"><Star className={`w-4 h-4 ${selectedLibrary.favorite||favoriteIds.includes(selectedLibrary.id)?"text-amber-300 fill-amber-300":"text-neutral-500"}`}/></button>
                </div>

                <div className="mt-3 space-y-2">
                  <label className="block text-[9px] text-neutral-500">Layer khi chèn
                    <select value={selectedLibrary.layer||getDefaultLibraryLayer(selectedLibrary.category)}
                      onChange={e=>managedLibrary.some((x:any)=>x.id===selectedLibrary.id)&&void updateManagedItem(selectedLibrary.id,{layer:e.target.value})}
                      className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                      {HNL_CAD_LAYER_STANDARDS.map(x=><option key={x.name} value={x.name}>{x.name} • {x.linetype} • {x.lineweight.toFixed(2)}mm</option>)}
                    </select>
                  </label>

                  {(()=>{
                    const std=getHnlLayerStandard(selectedLibrary.layer||getDefaultLibraryLayer(selectedLibrary.category));
                    return <div className="grid grid-cols-3 gap-1 text-[9px]">
                      <div className="p-2 bg-neutral-900 rounded"><span className="text-neutral-600">Color</span><br/><b style={{color:std?.color||"#fff"}}>ACI {std?.aci??"—"}</b></div>
                      <div className="p-2 bg-neutral-900 rounded"><span className="text-neutral-600">Linetype</span><br/><b>{std?.linetype||"—"}</b></div>
                      <div className="p-2 bg-neutral-900 rounded"><span className="text-neutral-600">Weight</span><br/><b>{std?.lineweight?.toFixed(2)||"—"} mm</b></div>
                    </div>
                  })()}

                  <div className="grid grid-cols-2 gap-2 text-[9px]">
                    <label className="text-neutral-500">Scale<input type="number" step="0.1" min="0.001" value={insertScale} onChange={e=>setInsertScale(Math.max(.001,+e.target.value||1))} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2"/></label>
                    <label className="text-neutral-500">Rotation °<input type="number" value={insertRotation} onChange={e=>setInsertRotation(+e.target.value||0)} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2"/></label>
                  </div>

                  {selectedLibrary.sourceDwg&&<>
                    <div className="p-2 bg-neutral-900 rounded break-all text-[9px] text-neutral-600">{selectedLibrary.sourceDwg}</div>
                    <div className="flex gap-1.5">
                      <button onClick={()=>void inspectLibraryItem(selectedLibrary)} className="flex-1 px-2 py-1.5 rounded bg-neutral-800 text-[9px] flex items-center justify-center gap-1"><RefreshCw className="w-3 h-3"/>Đọc DWG / Dynamic</button>
                      <button onClick={()=>void (window as any).electronNative?.revealLibraryItem?.(selectedLibrary.sourceDwg)} className="px-2 py-1.5 rounded bg-neutral-800" title="Hiện trong Explorer"><ExternalLink className="w-3 h-3"/></button>
                    </div>
                  </>}

                  {Array.isArray(selectedLibrary.definitions)&&selectedLibrary.definitions.length>0&&<label className="block text-[9px] text-neutral-500">Block definition
                    <select value={selectedLibrary.selectedDefinition||""}
                      onChange={e=>managedLibrary.some((x:any)=>x.id===selectedLibrary.id)&&void updateManagedItem(selectedLibrary.id,{selectedDefinition:e.target.value||null})}
                      className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-2 text-[10px]">
                      <option value="">— Chọn block definition —</option>
                      {selectedLibrary.definitions.map((d:any)=><option key={d.name} value={d.name}>{d.isDynamic?"◆ Dynamic":"□ Static"} • {d.name} • {d.entityCount} ent</option>)}
                    </select>
                  </label>}

                  <button onClick={()=>void insertLibraryItem(selectedLibrary)} className="w-full px-3 py-2.5 rounded bg-cyan-600 text-white text-[10px] font-bold">Chèn vào AutoCAD</button>
                  <div className="text-[9px] text-neutral-600 leading-4">Sau khi bấm Chèn, AutoCAD sẽ yêu cầu chọn điểm chèn. HNL không giữ request chờ điểm nên không còn timeout 12 giây.</div>

                  {selectedLibrary.storageMode==="COPY"&&managedLibrary.some((x:any)=>x.id===selectedLibrary.id)&&<button onClick={async()=>{
                    if(!window.confirm("Xóa mục khỏi HNL Library và xóa managed copy nếu không còn nơi khác dùng?"))return;
                    await (window as any).electronNative?.removeLibraryItem?.({id:selectedLibrary.id,deleteManagedFile:true});
                    setSelectedLibraryId(null);await reloadLibrary();
                  }} className="w-full px-2 py-1.5 rounded border border-red-900 text-red-400 text-[9px] flex items-center justify-center gap-1"><Trash2 className="w-3 h-3"/>Xóa khỏi Library</button>}
                </div>
              </>}
            </div>
          </div>

          {lastDynamic&&<div className="rounded-xl border border-emerald-800/60 bg-emerald-950/15 p-3">
            <div className="flex justify-between gap-3"><div><b className="text-xs text-emerald-300">Dynamic Block: {lastDynamic.blockName}</b><div className="text-[9px] text-neutral-500">Handle {lastDynamic.handle} • chỉnh property native.</div></div><button onClick={()=>setLastDynamic(null)}><X className="w-3 h-3"/></button></div>
            <div className="grid md:grid-cols-2 xl:grid-cols-4 gap-2 mt-2">
              {(lastDynamic.properties||[]).map((prop:any)=><label key={prop.name} className="text-[9px] text-neutral-500">{prop.name}
                {Array.isArray(prop.allowedValues)&&prop.allowedValues.length>0?
                  <select defaultValue={String(prop.value)} disabled={prop.readOnly} onChange={async e=>{await bridge("SET_DYNAMIC_BLOCK_PROPERTIES",{handle:lastDynamic.handle,properties:{[prop.name]:e.target.value}})}} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5 text-[10px]">{prop.allowedValues.map((v:any)=><option key={String(v)} value={String(v)}>{String(v)}</option>)}</select>
                  :<input defaultValue={String(prop.value??"")} disabled={prop.readOnly} onBlur={async e=>{if(prop.readOnly)return;const raw=e.target.value,n=Number(raw);await bridge("SET_DYNAMIC_BLOCK_PROPERTIES",{handle:lastDynamic.handle,properties:{[prop.name]:Number.isFinite(n)&&raw.trim()!==""?n:raw}})}} className="mt-1 w-full bg-neutral-950 border border-neutral-700 rounded p-1.5 text-[10px]"/>}
              </label>)}
            </div>
          </div>}
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
