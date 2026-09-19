import React,{useEffect,useRef} from "react";
import { Terminal, X } from "lucide-react";
import { resolveCadAlias } from "../../lib/cadCommandAliases";

export const CadCommandLine:React.FC<{
  visible:boolean;
  draft:string;
  setDraft:(v:string)=>void;
  history:string[];
  onExecute:(command:string)=>void;
  onClose:()=>void;
}> = ({visible,draft,setDraft,history,onExecute,onClose})=>{
  const ref=useRef<HTMLInputElement|null>(null);
  useEffect(()=>{
    const focus=()=>ref.current?.focus();
    window.addEventListener("hnl-focus-command-line",focus as EventListener);
    return()=>window.removeEventListener("hnl-focus-command-line",focus as EventListener);
  },[]);
  if(!visible)return null;
  const execute=()=>{
    const q=draft.trim();if(!q)return;
    onExecute(q);setDraft("");
  };
  const resolved=resolveCadAlias(draft);
  return <div className="h-9 shrink-0 bg-[#111317] border-t border-neutral-800 border-b border-neutral-800 px-2 flex items-center gap-2 text-xs">
    <Terminal className="w-4 h-4 text-cyan-400 shrink-0"/>
    <span className="font-mono text-neutral-500 shrink-0">Command:</span>
    <input id="hnl-command-input" ref={ref} value={draft} onChange={e=>setDraft(e.target.value.toUpperCase())}
      onKeyDown={e=>{
        if(e.key==="Enter"||e.key===" "){e.preventDefault();execute();}
        else if(e.key==="Escape"){setDraft("");(e.currentTarget as HTMLInputElement).blur();}
        else if(e.key==="ArrowUp"&&history.length){e.preventDefault();setDraft(history[0].split(" → ")[0]||"");}
      }}
      spellCheck={false} autoComplete="off"
      className="min-w-0 flex-1 bg-transparent outline-none font-mono text-neutral-100 placeholder-neutral-600"
      placeholder="Gõ lệnh AutoCAD: L, PL, C, REC, CO, M, RO, SC, TR, O, DLI, MT..."/>
    {draft&&<span className={`hidden md:inline px-2 py-0.5 rounded border ${resolved?(resolved.support==="READY"?"border-emerald-800 text-emerald-300":resolved.support==="PARTIAL"?"border-amber-800 text-amber-300":"border-sky-800 text-sky-300"):"border-neutral-700 text-neutral-500"}`}>{resolved?`${resolved.label} • ${resolved.support}`:"Unknown command"}</span>}
    {history[0]&&<span className="hidden xl:block max-w-[360px] truncate text-neutral-600 font-mono">{history[0]}</span>}
    <span className="hidden lg:inline text-neutral-600">Ctrl+9</span>
    <button onClick={onClose} title="Ẩn Command Line (Ctrl+9)" className="p-1 rounded hover:bg-neutral-800 text-neutral-500"><X className="w-3.5 h-3.5"/></button>
  </div>
};
