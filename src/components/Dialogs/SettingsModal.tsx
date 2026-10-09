import React, { useEffect, useState } from "react";
import { Check, Settings, Shield, X } from "lucide-react";
import { AiProviderManager } from "./AiProviderManager";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  safeMode: boolean;
  onSafeModeChange: (value: boolean) => void;
}

export const SettingsModal:React.FC<SettingsModalProps>=({isOpen,onClose,safeMode,onSafeModeChange})=>{
  const [wallThickDefault,setWallThickDefault]=useState(100);
  const [ceilingMainSpacing,setCeilingMainSpacing]=useState(800);
  const [ceilingCrossSpacing,setCeilingCrossSpacing]=useState(400);
  const [savedMessage,setSavedMessage]=useState("");

  useEffect(()=>{
    if(!isOpen)return;
    try{
      const raw=localStorage.getItem("hnl.settings.v1");
      const cfg=raw?JSON.parse(raw):{};
      if(Number.isFinite(cfg.wallThickDefault))setWallThickDefault(cfg.wallThickDefault);
      if(Number.isFinite(cfg.ceilingMainSpacing))setCeilingMainSpacing(cfg.ceilingMainSpacing);
      if(Number.isFinite(cfg.ceilingCrossSpacing))setCeilingCrossSpacing(cfg.ceilingCrossSpacing);
    }catch{}
  },[isOpen]);

  if(!isOpen)return null;

  const handleSave=()=>{
    const old=(()=>{try{return JSON.parse(localStorage.getItem("hnl.settings.v1")||"{}")}catch{return {}}})();
    localStorage.setItem("hnl.settings.v1",JSON.stringify({...old,safeMode,wallThickDefault,ceilingMainSpacing,ceilingCrossSpacing}));
    onSafeModeChange(safeMode);
    setSavedMessage("Đã lưu cấu hình HNL.");
    setTimeout(()=>setSavedMessage(""),1500);
  };

  return <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
    <div className="w-full max-w-4xl max-h-[90vh] bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
      <div className="h-14 px-5 bg-[#141517] border-b border-neutral-800 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded bg-neutral-800 border border-neutral-700 flex items-center justify-center text-cyan-400"><Settings className="w-5 h-5"/></div>
          <div><h2 className="font-bold text-white text-sm">CÀI ĐẶT HNL CAD AI</h2><p className="text-[11px] text-neutral-400">AI Provider • An toàn • Thiết lập shopdrawing</p></div>
        </div>
        <button onClick={onClose} className="p-1.5 rounded hover:bg-neutral-800 text-neutral-400"><X className="w-5 h-5"/></button>
      </div>

      <div className="p-5 space-y-5 text-xs overflow-y-auto min-h-0">
        <AiProviderManager/>

        <div className="p-3 bg-[#18191C] rounded-lg border border-neutral-800">
          <div className="flex items-center justify-between gap-4">
            <div>
              <div className="font-semibold text-neutral-200 flex items-center gap-2"><Shield className="w-4 h-4 text-emerald-400"/>Safe Execution Mode</div>
              <div className="text-[10px] text-neutral-500 mt-1">Các thao tác xóa/thay đổi hàng loạt phải Preview và xác nhận.</div>
            </div>
            <input type="checkbox" checked={safeMode} onChange={(e)=>onSafeModeChange(e.target.checked)} className="w-4 h-4"/>
          </div>
        </div>

        <div className="space-y-3">
          <div className="font-semibold text-neutral-300">Thông số mặc định công cụ vẽ nhanh</div>
          <div className="grid md:grid-cols-3 gap-3">
            <label className="space-y-1"><span className="text-[10px] text-neutral-400">Độ dày tường</span>
              <select value={wallThickDefault} onChange={(e)=>setWallThickDefault(Number(e.target.value))} className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2">
                <option value={100}>100 mm</option><option value={150}>150 mm</option><option value={200}>200 mm</option>
              </select>
            </label>
            <label className="space-y-1"><span className="text-[10px] text-neutral-400">Xương chính @</span><input type="number" value={ceilingMainSpacing} onChange={(e)=>setCeilingMainSpacing(Number(e.target.value))} className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2"/></label>
            <label className="space-y-1"><span className="text-[10px] text-neutral-400">Xương phụ @</span><input type="number" value={ceilingCrossSpacing} onChange={(e)=>setCeilingCrossSpacing(Number(e.target.value))} className="w-full bg-[#25272C] border border-neutral-700 rounded px-2.5 py-2"/></label>
          </div>
        </div>
      </div>

      <div className="h-14 px-5 bg-[#141517] border-t border-neutral-800 flex items-center justify-between shrink-0">
        <span className="text-[10px] text-emerald-400">{savedMessage}</span>
        <div className="flex gap-2">
          <button onClick={onClose} className="px-4 py-2 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-300">Đóng</button>
          <button onClick={handleSave} className="px-4 py-2 rounded bg-cyan-600 hover:bg-cyan-500 text-white font-semibold flex items-center gap-2"><Check className="w-4 h-4"/>Lưu HNL</button>
        </div>
      </div>
    </div>
  </div>;
};
