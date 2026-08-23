import React, { useEffect, useMemo, useState } from "react";
import { Bot, CheckCircle2, Cloud, Cpu, Key, Laptop, RefreshCw, ShieldCheck, Wifi, XCircle } from "lucide-react";

type ProviderId = "OFFLINE" | "GEMINI" | "OPENAI" | "CLAUDE" | "GROK" | "OLLAMA" | "CUSTOM_OPENAI";

type ProviderEntry = {
  id: ProviderId;
  name: string;
  kind: "offline" | "online" | "local";
  description: string;
  defaultModel: string;
  defaultBaseUrl: string;
  needsKey: boolean;
};

const PROVIDERS: ProviderEntry[] = [
  { id:"OFFLINE", name:"HNL Nội bộ", kind:"offline", description:"Rule engine cục bộ. Không Internet, không API key.", defaultModel:"hnl-rules-v1", defaultBaseUrl:"", needsKey:false },
  { id:"GEMINI", name:"Gemini", kind:"online", description:"Google Gemini API.", defaultModel:"gemini-3.7-flash", defaultBaseUrl:"https://generativelanguage.googleapis.com", needsKey:true },
  { id:"OPENAI", name:"ChatGPT / OpenAI", kind:"online", description:"OpenAI Responses API.", defaultModel:"gpt-5.6", defaultBaseUrl:"https://api.openai.com/v1", needsKey:true },
  { id:"CLAUDE", name:"Claude", kind:"online", description:"Anthropic Messages API.", defaultModel:"claude-sonnet-4-20250514", defaultBaseUrl:"https://api.anthropic.com/v1", needsKey:true },
  { id:"GROK", name:"Grok / xAI", kind:"online", description:"xAI Responses API.", defaultModel:"grok-4.6", defaultBaseUrl:"https://api.x.ai/v1", needsKey:true },
  { id:"OLLAMA", name:"Ollama Local", kind:"local", description:"LLM chạy trên PC qua Ollama.", defaultModel:"gemma3", defaultBaseUrl:"http://127.0.0.1:11434", needsKey:false },
  { id:"CUSTOM_OPENAI", name:"Custom API", kind:"local", description:"Endpoint OpenAI-compatible: LM Studio, LiteLLM, server riêng...", defaultModel:"gpt-4o-mini", defaultBaseUrl:"http://127.0.0.1:1234/v1", needsKey:false },
];

const DEFAULT_CFG:any = {
  activeProvider:"GEMINI",
  autoFallbackOffline:true,
  contextOnly:true,
  previewBeforeExecute:true,
  providers:Object.fromEntries(PROVIDERS.map((p)=>[p.id,{model:p.defaultModel,baseUrl:p.defaultBaseUrl}])),
  configured:{OFFLINE:true,OLLAMA:true},
};

const headers = () => {
  const token=(window as any).electronNative?.sessionToken || "";
  return {"Content-Type":"application/json", ...(token?{"x-hnl-token":token}:{})};
};

export const AiProviderManager:React.FC = () => {
  const [cfg,setCfg]=useState<any>(DEFAULT_CFG);
  const [active,setActive]=useState<ProviderId>("GEMINI");
  const [apiKey,setApiKey]=useState("");
  const [clearKey,setClearKey]=useState(false);
  const [busy,setBusy]=useState(false);
  const [message,setMessage]=useState("");
  const [testStatus,setTestStatus]=useState<"idle"|"ok"|"error">("idle");

  const provider=useMemo(()=>PROVIDERS.find((p)=>p.id===active) || PROVIDERS[0],[active]);
  const providerCfg=cfg.providers?.[active] || {model:provider.defaultModel,baseUrl:provider.defaultBaseUrl};

  useEffect(()=>{
    let disposed=false;
    const load=async()=>{
      try{
        const native=(window as any).electronNative;
        const result=await native?.getAIProviderConfig?.();
        if(!disposed && result?.success && result.config){
          const merged={...DEFAULT_CFG,...result.config,providers:{...DEFAULT_CFG.providers,...(result.config.providers||{})},configured:{...DEFAULT_CFG.configured,...(result.config.configured||{})}};
          setCfg(merged);
          setActive((merged.activeProvider || "GEMINI") as ProviderId);
          return;
        }
        const raw=localStorage.getItem("hnl.ai.providers.v2");
        if(raw && !disposed){
          const local=JSON.parse(raw);
          const merged={...DEFAULT_CFG,...local,providers:{...DEFAULT_CFG.providers,...(local.providers||{})}};
          setCfg(merged);setActive((merged.activeProvider || "GEMINI") as ProviderId);
        }
      }catch{}
    };
    void load();
    return()=>{disposed=true};
  },[]);

  const updateProvider=(patch:any)=>{
    setCfg((prev:any)=>({...prev,providers:{...prev.providers,[active]:{...(prev.providers?.[active]||{}),...patch}}}));
  };

  const save=async(testAfter=false)=>{
    setBusy(true);setMessage("");setTestStatus("idle");
    try{
      const next={...cfg,activeProvider:active};
      localStorage.setItem("hnl.ai.providers.v2",JSON.stringify(next));
      const native=(window as any).electronNative;
      if(native?.saveAIProviderConfig){
        const result=await native.saveAIProviderConfig({
          activeProvider:active,
          provider:active,
          model:providerCfg.model,
          baseUrl:providerCfg.baseUrl,
          apiKey,
          clearKey,
          autoFallbackOffline:next.autoFallbackOffline,
          contextOnly:next.contextOnly,
          previewBeforeExecute:next.previewBeforeExecute,
        });
        if(!result?.success)throw new Error(result?.error || "Không lưu được provider.");
        const merged={...next,...result.config,providers:{...next.providers,...(result.config?.providers||{})},configured:{...next.configured,...(result.config?.configured||{})}};
        setCfg(merged);
      }
      setApiKey("");setClearKey(false);setMessage("Đã lưu cấu hình AI an toàn.");
      if(testAfter){
        const res=await fetch("/api/ai/test",{method:"POST",headers:headers(),body:JSON.stringify({provider:active})});
        const data=await res.json();
        if(!res.ok || !data?.ok)throw new Error(data?.error || data?.message || "Provider không phản hồi.");
        setTestStatus("ok");setMessage(`${provider.name}: kết nối OK • ${data.model || providerCfg.model}`);
      }
    }catch(err:any){
      setTestStatus("error");setMessage(err?.message || String(err));
    }finally{setBusy(false)}
  };

  return <div className="space-y-4">
    <div className="flex items-center justify-between">
      <div>
        <div className="font-semibold text-neutral-200 flex items-center gap-2"><Bot className="w-4 h-4 text-cyan-400"/>AI Provider Manager</div>
        <div className="text-[11px] text-neutral-500 mt-1">Một cấu hình dùng chung cho HNL Desktop, AutoCAD Palette và HNL AI.</div>
      </div>
      <div className={`px-2 py-1 rounded border text-[10px] ${testStatus==="ok"?"border-emerald-700 text-emerald-300 bg-emerald-950/30":testStatus==="error"?"border-red-800 text-red-300 bg-red-950/30":"border-neutral-700 text-neutral-400 bg-neutral-900"}`}>
        {testStatus==="ok"?"Kết nối OK":testStatus==="error"?"Có lỗi":"Chưa kiểm tra"}
      </div>
    </div>

    <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
      {PROVIDERS.map((p)=>{
        const configured = p.id==="OFFLINE" || p.id==="OLLAMA" || Boolean(cfg.configured?.[p.id]);
        return <button key={p.id} type="button" onClick={()=>{setActive(p.id);setApiKey("");setClearKey(false);setTestStatus("idle")}}
          className={`text-left p-2.5 rounded-lg border transition ${active===p.id?"bg-cyan-500/15 border-cyan-500":"bg-[#25272C] border-neutral-700 hover:border-neutral-500"}`}>
          <div className="flex items-center justify-between gap-1">
            <span className="font-semibold text-neutral-100 text-[11px]">{p.name}</span>
            {p.kind==="offline"?<ShieldCheck className="w-3.5 h-3.5 text-emerald-400"/>:p.kind==="local"?<Laptop className="w-3.5 h-3.5 text-purple-400"/>:<Cloud className="w-3.5 h-3.5 text-sky-400"/>}
          </div>
          <div className="text-[9px] text-neutral-500 mt-1 line-clamp-2">{p.description}</div>
          <div className={`text-[9px] mt-1 ${configured?"text-emerald-400":"text-amber-400"}`}>{configured?"Sẵn sàng/có cấu hình":"Chưa có API key"}</div>
        </button>
      })}
    </div>

    <div className="p-3 rounded-lg bg-[#18191C] border border-neutral-800 space-y-3">
      <div className="grid md:grid-cols-2 gap-3">
        <label className="space-y-1">
          <span className="text-[10px] text-neutral-400">Model</span>
          <input value={providerCfg.model || ""} disabled={active==="OFFLINE"} onChange={(e)=>updateProvider({model:e.target.value})}
            className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2 text-xs text-neutral-200 disabled:opacity-50"/>
        </label>
        <label className="space-y-1">
          <span className="text-[10px] text-neutral-400">Base URL</span>
          <input value={providerCfg.baseUrl || ""} disabled={active==="OFFLINE" || active==="GEMINI"} onChange={(e)=>updateProvider({baseUrl:e.target.value})}
            className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2 text-xs text-neutral-200 disabled:opacity-50"/>
        </label>
      </div>

      {provider.needsKey || active==="CUSTOM_OPENAI" ? <div className="grid md:grid-cols-[1fr_auto] gap-2 items-end">
        <label className="space-y-1">
          <span className="text-[10px] text-neutral-400 flex items-center gap-1"><Key className="w-3 h-3"/>API Key {active==="CUSTOM_OPENAI"?"(nếu endpoint yêu cầu)":""}</span>
          <input type="password" value={apiKey} onChange={(e)=>setApiKey(e.target.value)} placeholder="Nhập key mới; để trống nếu giữ key hiện tại"
            className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2 text-xs text-neutral-200"/>
        </label>
        <label className="h-9 flex items-center gap-2 px-2 text-[10px] text-neutral-400">
          <input type="checkbox" checked={clearKey} onChange={(e)=>setClearKey(e.target.checked)}/> Xóa key đã lưu
        </label>
      </div>:null}

      <div className="grid md:grid-cols-3 gap-2 text-[10px]">
        <label className="flex items-center gap-2 bg-neutral-900/60 border border-neutral-800 rounded p-2"><input type="checkbox" checked={cfg.contextOnly!==false} onChange={(e)=>setCfg((v:any)=>({...v,contextOnly:e.target.checked}))}/>Chỉ gửi CAD Context cần thiết</label>
        <label className="flex items-center gap-2 bg-neutral-900/60 border border-neutral-800 rounded p-2"><input type="checkbox" checked={cfg.previewBeforeExecute!==false} onChange={(e)=>setCfg((v:any)=>({...v,previewBeforeExecute:e.target.checked}))}/>Preview trước khi thực thi</label>
        <label className="flex items-center gap-2 bg-neutral-900/60 border border-neutral-800 rounded p-2"><input type="checkbox" checked={cfg.autoFallbackOffline!==false} onChange={(e)=>setCfg((v:any)=>({...v,autoFallbackOffline:e.target.checked}))}/>Fallback Offline khi lỗi mạng</label>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" disabled={busy} onClick={()=>void save(false)} className="px-3 py-2 rounded bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white text-[11px] font-semibold">Lưu Provider</button>
        <button type="button" disabled={busy} onClick={()=>void save(true)} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700 disabled:opacity-50 text-neutral-200 text-[11px] flex items-center gap-1"><RefreshCw className={`w-3.5 h-3.5 ${busy?"animate-spin":""}`}/>Lưu & kiểm tra</button>
        {message && <span className={`text-[10px] ${testStatus==="error"?"text-red-300":"text-emerald-300"}`}>{message}</span>}
      </div>

      <div className="text-[9px] text-neutral-500 flex items-start gap-1.5">
        <ShieldCheck className="w-3.5 h-3.5 mt-0.5 shrink-0 text-emerald-500"/>
        API key trong EXE được mã hóa bằng Electron safeStorage/Windows DPAPI. HNL không ghi key vào source, DWG hay JSON project.
      </div>
    </div>
  </div>;
};
