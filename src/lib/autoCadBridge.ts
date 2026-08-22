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
export async function executeAutoCadAction(action: string, payload?: unknown) {
  const bridge = (window as any).hnlAutoCadBridge;
  if (bridge && typeof bridge.executeAction === 'function') return bridge.executeAction(action,payload??{});
  try{
    const r=await fetch("/api/autocad/action",{method:"POST",headers:{"Content-Type":"application/json",...headers()},body:JSON.stringify({action,payload:payload??{}})});
    const x=await r.json();if(!x?.ok)return x;
    for(let i=0;i<120;i++){await new Promise(res=>setTimeout(res,100));const rr=await fetch(`/api/autocad/result/${encodeURIComponent(x.id)}`,{headers:headers() as any});if(rr.ok)return rr.json();}
    return{ok:false,reason:"AUTOCAD_BRIDGE_TIMEOUT",id:x.id};
  }catch(e:any){return{ok:false,reason:"AUTOCAD_BRIDGE_REQUEST_FAILED",error:e?.message||String(e)}}
}
