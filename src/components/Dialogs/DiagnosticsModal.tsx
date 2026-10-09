import React, { useMemo, useState } from "react";
import { Activity, AlertTriangle, Bug, CheckCircle2, Clipboard, Download, Trash2, X } from "lucide-react";
import { DiagnosticEvent, diagnosticsToText } from "../../lib/diagnostics";
import { requestHnlInput } from "../../lib/uiPrompt";
import { runAutoCadBridgeGoldenSmoke } from "../../lib/autoCadBridge";

interface Props {
  isOpen: boolean;
  events: DiagnosticEvent[];
  onClose: () => void;
  onClear: () => void;
}

export const DiagnosticsModal: React.FC<Props> = ({ isOpen, events, onClose, onClear }) => {
  const [expanded, setExpanded] = useState<string | null>(null);
  const [bridgeGoldenBusy,setBridgeGoldenBusy]=useState(false);
  const [bridgeGolden,setBridgeGolden]=useState<any>(null);
  const counts = useMemo(() => ({
    critical: events.filter(e => e.severity === "CRITICAL").length,
    error: events.filter(e => e.severity === "ERROR").length,
    warning: events.filter(e => e.severity === "WARNING").length,
  }), [events]);
  if (!isOpen) return null;

  const copy = async () => {
    const text = diagnosticsToText(events);
    try { await navigator.clipboard.writeText(text); alert("Đã copy báo cáo chẩn đoán. Có thể dán trực tiếp để gửi kiểm tra."); }
    catch { void requestHnlInput({title:"Copy báo cáo chẩn đoán",label:"Clipboard không khả dụng. Chọn nội dung bên dưới và Ctrl+C.",defaultValue:text,multiline:true,readOnly:true,okText:"Đóng"}); }
  };
  const download = () => {
    const blob = new Blob([diagnosticsToText(events)], { type: "text/plain;charset=utf-8" });
    const a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = `HNL_Diagnostic_${new Date().toISOString().replace(/[:.]/g,"-")}.txt`; a.click(); URL.revokeObjectURL(a.href);
  };
  const runBridgeGolden=async()=>{
    setBridgeGoldenBusy(true);
    try{setBridgeGolden(await runAutoCadBridgeGoldenSmoke());}
    finally{setBridgeGoldenBusy(false);}
  };

  return <div className="fixed inset-0 z-[200] bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
    <div className="w-full max-w-5xl max-h-[88vh] flex flex-col rounded-2xl border border-neutral-700 bg-[#17191d] shadow-2xl overflow-hidden">
      <div className="px-5 py-4 border-b border-neutral-800 flex items-center justify-between bg-[#111317]">
        <div><div className="flex items-center gap-2 text-white font-bold"><Bug className="w-5 h-5 text-cyan-400"/> Trung tâm chẩn đoán lỗi</div><div className="text-xs text-neutral-400 mt-1">Mỗi lỗi có mã, chức năng, nguyên nhân và dữ liệu kỹ thuật để chỉnh sửa chính xác.</div></div>
        <button onClick={onClose} className="p-2 hover:bg-neutral-800 rounded"><X className="w-5 h-5"/></button>
      </div>
      <div className="px-5 py-3 border-b border-neutral-800 flex flex-wrap items-center gap-2 text-xs">
        <span className="px-2 py-1 rounded bg-red-950/40 border border-red-800 text-red-300">Critical: {counts.critical}</span>
        <span className="px-2 py-1 rounded bg-rose-950/40 border border-rose-800 text-rose-300">Error: {counts.error}</span>
        <span className="px-2 py-1 rounded bg-amber-950/40 border border-amber-800 text-amber-300">Warning: {counts.warning}</span>
        <span className="ml-auto text-neutral-500">Tổng {events.length} sự kiện</span>
        <button disabled={bridgeGoldenBusy} onClick={()=>void runBridgeGolden()} className="px-3 py-1.5 rounded bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white font-semibold flex items-center gap-1"><Activity className="w-3.5 h-3.5"/> {bridgeGoldenBusy?"Đang test Bridge...":"Golden Test Bridge"}</button>
        <button onClick={copy} className="px-3 py-1.5 rounded bg-cyan-600 hover:bg-cyan-500 text-black font-semibold flex items-center gap-1"><Clipboard className="w-3.5 h-3.5"/> Copy báo cáo</button>
        <button onClick={download} className="px-3 py-1.5 rounded bg-neutral-700 hover:bg-neutral-600 flex items-center gap-1"><Download className="w-3.5 h-3.5"/> Xuất TXT</button>
        <button onClick={onClear} className="px-3 py-1.5 rounded bg-neutral-800 hover:bg-red-950/50 text-neutral-300 flex items-center gap-1"><Trash2 className="w-3.5 h-3.5"/> Xóa log</button>
      </div>
      <div className="overflow-auto p-4 space-y-2">
        {bridgeGolden&&<div className={`rounded-lg border p-3 ${bridgeGolden.ok?"border-emerald-700 bg-emerald-950/20":"border-red-800 bg-red-950/20"}`}>
          <div className={`font-bold text-sm ${bridgeGolden.ok?"text-emerald-300":"text-red-300"}`}>{bridgeGolden.ok?"BRIDGE GOLDEN PASS":"BRIDGE GOLDEN FAIL"}</div>
          <div className="text-[11px] text-neutral-400 mt-1">Plugin {bridgeGolden.pluginVersion||"?"} • AutoCAD {bridgeGolden.autoCadVersion||"?"} • {bridgeGolden.drawingName||"(no drawing)"}</div>
          <pre className="mt-2 max-h-48 overflow-auto rounded bg-black/40 p-2 text-[10px] text-neutral-400">{JSON.stringify(bridgeGolden,null,2)}</pre>
        </div>}
        {events.length === 0 ? <div className="py-16 text-center text-neutral-500"><CheckCircle2 className="w-10 h-10 mx-auto mb-3 text-emerald-500"/>Chưa ghi nhận lỗi trong phiên làm việc.</div> : events.map(e => <div key={e.id} className="rounded-lg border border-neutral-800 bg-[#1d1f24] overflow-hidden">
          <button className="w-full px-4 py-3 text-left flex items-start gap-3 hover:bg-neutral-800/60" onClick={() => setExpanded(expanded === e.id ? null : e.id)}>
            <AlertTriangle className={`w-4 h-4 mt-0.5 shrink-0 ${e.severity === "CRITICAL" || e.severity === "ERROR" ? "text-red-400" : e.severity === "WARNING" ? "text-amber-400" : "text-sky-400"}`}/>
            <div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><span className="font-mono text-[11px] text-cyan-300">{e.code}</span><span className="font-semibold text-sm text-neutral-100">{e.title}</span>{e.command && <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400">{e.command}</span>}</div><div className="text-xs text-neutral-400 mt-1 truncate">{e.message}</div></div>
            <div className="text-[10px] text-neutral-600 whitespace-nowrap">{new Date(e.timestamp).toLocaleString()}</div>
          </button>
          {expanded === e.id && <div className="px-4 pb-4 border-t border-neutral-800 text-xs space-y-2 pt-3">
            <div><b className="text-neutral-300">Chi tiết:</b> <span className="text-neutral-400">{e.message}</span></div>
            {e.cause && <div><b className="text-neutral-300">Nguyên nhân kỹ thuật:</b> <span className="text-red-300 font-mono">{e.cause}</span></div>}
            {e.suggestion && <div><b className="text-neutral-300">Đề xuất xử lý:</b> <span className="text-emerald-300">{e.suggestion}</span></div>}
            {e.context && <pre className="p-3 rounded bg-black/40 text-[11px] text-neutral-400 overflow-auto">{JSON.stringify(e.context, null, 2)}</pre>}
            {e.stack && <details><summary className="cursor-pointer text-neutral-500">Stack kỹ thuật</summary><pre className="mt-2 p-3 rounded bg-black/50 text-[10px] text-neutral-500 overflow-auto whitespace-pre-wrap">{e.stack}</pre></details>}
          </div>}
        </div>)}
      </div>
    </div>
  </div>;
};
