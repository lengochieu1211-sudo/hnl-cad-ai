export type AutoCadBridgeStatus = {
  connected: boolean;
  version?: string;
  drawingName?: string;
  pluginVersion?: string;
  capabilities?: string[];
  source: 'standalone' | 'autocad-plugin';
  lastCheckedAt: number;
};
const headers=()=>{const t=(window as any).electronNative?.sessionToken||"";return t?{"x-hnl-token":t}:{}};
export async function detectAutoCadBridge(): Promise<AutoCadBridgeStatus> {
  const bridge = (window as any).hnlAutoCadBridge;
  if (bridge && typeof bridge.getStatus === 'function') {
    try { const status=await bridge.getStatus(); return {connected:Boolean(status?.connected),version:status?.version,drawingName:status?.drawingName,pluginVersion:status?.pluginVersion,capabilities:status?.capabilities,source:'autocad-plugin',lastCheckedAt:Date.now()}; } catch {}
  }
  try{
    const r=await fetch("/api/autocad/status",{headers:headers() as any});
    if(r.ok){const s=await r.json();return{connected:Boolean(s.connected),version:s.version,drawingName:s.drawingName,pluginVersion:s.pluginVersion,capabilities:s.capabilities||[],source:s.connected?'autocad-plugin':'standalone',lastCheckedAt:Date.now()};}
  }catch{}
  return { connected: false, source: 'standalone', lastCheckedAt: Date.now() };
}
const ACTION_TIMEOUT_MS:Record<string,number>={
  OPEN_DWG:60000, CONVERT_DWG_TO_DXF_PREVIEW:90000, INSPECT_LIBRARY_DWG:60000,
  IMPORT_LIBRARY_DEFINITION:60000, SAVE_AS_DWG:60000, SAVE_DXF_AS_DWG:90000,
  PLOT_CURRENT_PDF:90000, PUBLISH_LAYOUTS_PDF:180000, GET_MODELSPACE_SNAPSHOT:60000
};
export async function executeAutoCadAction(action: string, payload?: unknown) {
  const bridge = (window as any).hnlAutoCadBridge;
  if (bridge && typeof bridge.executeAction === 'function') return bridge.executeAction(action,payload??{});
  try{
    const r=await fetch("/api/autocad/action",{method:"POST",headers:{"Content-Type":"application/json",...headers()},body:JSON.stringify({action,payload:payload??{},timeoutMs:ACTION_TIMEOUT_MS[String(action||"").toUpperCase()]||20000})});
    const x=await r.json();if(!x?.ok)return x;
    const timeoutMs=ACTION_TIMEOUT_MS[String(action||"").toUpperCase()]||20000;
    const started=Date.now();
    while(Date.now()-started<timeoutMs){await new Promise(res=>setTimeout(res,125));const rr=await fetch(`/api/autocad/result/${encodeURIComponent(x.id)}`,{headers:headers() as any});if(rr.ok)return rr.json();}
    try{await fetch(`/api/autocad/action/${encodeURIComponent(x.id)}/cancel`,{method:"POST",headers:headers() as any});}catch{}
    return{ok:false,reason:"AUTOCAD_BRIDGE_TIMEOUT",id:x.id,action,timeoutMs};
  }catch(e:any){return{ok:false,reason:"AUTOCAD_BRIDGE_REQUEST_FAILED",error:e?.message||String(e)}}
}

export async function runAutoCadBridgeGoldenSmoke(){
  try{
    const r=await fetch("/api/autocad/golden-smoke",{method:"POST",headers:headers() as any});
    const data=await r.json().catch(()=>({ok:false,reason:`HTTP_${r.status}`}));
    return{...data,httpStatus:r.status};
  }catch(e:any){
    return{ok:false,reason:"AUTOCAD_GOLDEN_REQUEST_FAILED",error:e?.message||String(e)};
  }
}
