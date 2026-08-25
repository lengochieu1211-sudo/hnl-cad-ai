export type AutoCadBridgeStatus = {
  connected: boolean;
  version?: string;
  drawingName?: string;
  pluginVersion?: string;
  capabilities?: string[];
  source: 'standalone' | 'autocad-plugin';
  lastCheckedAt: number;
};

const headers=()=>{
  const t=(window as any).electronNative?.sessionToken||"";
  return t?{"x-hnl-token":t}:{};
};

async function fetchJsonWithTimeout(url:string,init:RequestInit={},timeoutMs=2500){
  const controller=new AbortController();
  const timer=window.setTimeout(()=>controller.abort(),Math.max(250,timeoutMs));
  try{
    const response=await fetch(url,{...init,signal:controller.signal});
    const data=await response.json().catch(()=>({ok:false,reason:`HTTP_${response.status}`}));
    return{response,data};
  }finally{
    window.clearTimeout(timer);
  }
}

export async function detectAutoCadBridge(): Promise<AutoCadBridgeStatus> {
  const bridge = (window as any).hnlAutoCadBridge;
  if (bridge && typeof bridge.getStatus === 'function') {
    try {
      const status=await bridge.getStatus();
      return {connected:Boolean(status?.connected),version:status?.version,drawingName:status?.drawingName,pluginVersion:status?.pluginVersion,capabilities:status?.capabilities,source:'autocad-plugin',lastCheckedAt:Date.now()};
    } catch {}
  }
  try{
    const {response:r,data:s}=await fetchJsonWithTimeout("/api/autocad/status",{headers:headers() as HeadersInit},2500);
    if(r.ok)return{connected:Boolean(s.connected),version:s.version,drawingName:s.drawingName,pluginVersion:s.pluginVersion,capabilities:s.capabilities||[],source:s.connected?'autocad-plugin':'standalone',lastCheckedAt:Date.now()};
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
  const actionKey=String(action||"").toUpperCase();
  const requestedTimeoutMs=ACTION_TIMEOUT_MS[actionKey]||20000;
  let actionId="";
  try{
    const {response:r,data:x}=await fetchJsonWithTimeout("/api/autocad/action",{
      method:"POST",
      headers:{"Content-Type":"application/json",...headers()} as HeadersInit,
      body:JSON.stringify({action,payload:payload??{},timeoutMs:requestedTimeoutMs})
    },5000);
    if(!r.ok || !x?.ok)return x?.reason?x:{ok:false,reason:`AUTOCAD_ACTION_HTTP_${r.status}`,detail:x};
    actionId=String(x.id||"");
    if(!actionId)return{ok:false,reason:"AUTOCAD_ACTION_ID_MISSING",action};

    // Use the server's absolute deadline instead of starting a fresh client budget.
    // That keeps renderer/server/plugin expiry semantics aligned.
    const expiresAt=Number(x.expiresAt)||Date.now()+requestedTimeoutMs;
    while(Date.now()<expiresAt){
      const remaining=expiresAt-Date.now();
      await new Promise(res=>setTimeout(res,Math.min(125,Math.max(1,remaining))));
      if(Date.now()>=expiresAt)break;
      try{
        const {response:rr,data}=await fetchJsonWithTimeout(
          `/api/autocad/result/${encodeURIComponent(actionId)}`,
          {headers:headers() as HeadersInit},
          Math.min(2000,Math.max(250,expiresAt-Date.now()))
        );
        if(rr.ok)return data;
        if(rr.status!==404)return data?.reason?data:{ok:false,reason:`AUTOCAD_RESULT_HTTP_${rr.status}`,detail:data};
      }catch(e:any){
        if(e?.name!=="AbortError")throw e;
      }
    }
    try{
      await fetchJsonWithTimeout(`/api/autocad/action/${encodeURIComponent(actionId)}/cancel`,{method:"POST",headers:headers() as HeadersInit},2000);
    }catch{}
    return{ok:false,reason:"AUTOCAD_BRIDGE_TIMEOUT",id:actionId,action,timeoutMs:requestedTimeoutMs,expiresAt};
  }catch(e:any){
    if(actionId){
      try{await fetchJsonWithTimeout(`/api/autocad/action/${encodeURIComponent(actionId)}/cancel`,{method:"POST",headers:headers() as HeadersInit},1500);}catch{}
    }
    return{ok:false,reason:e?.name==="AbortError"?"AUTOCAD_BRIDGE_HTTP_TIMEOUT":"AUTOCAD_BRIDGE_REQUEST_FAILED",error:e?.message||String(e),id:actionId||undefined};
  }
}

export async function runAutoCadBridgeGoldenSmoke(){
  try{
    const {response:r,data}=await fetchJsonWithTimeout("/api/autocad/golden-smoke",{method:"POST",headers:headers() as HeadersInit},40_000);
    return{...data,httpStatus:r.status};
  }catch(e:any){
    return{ok:false,reason:e?.name==="AbortError"?"AUTOCAD_GOLDEN_HTTP_TIMEOUT":"AUTOCAD_GOLDEN_REQUEST_FAILED",error:e?.message||String(e)};
  }
}
