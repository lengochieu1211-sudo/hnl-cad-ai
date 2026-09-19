import React, { useState } from "react";
import { HnlModuleItem } from "../../types/cad";
import {
  Package,
  CheckCircle2,
  XCircle,
  ShieldAlert,
  Cpu,
  RefreshCw,
  X,
  Search,
  Sliders,
  Sparkles,
} from "lucide-react";

interface HnlAddonManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
  modules: HnlModuleItem[];
  onToggleModule: (moduleId: string) => void;
  isSafeMode: boolean;
  onToggleSafeMode: () => void;
}

export const HnlAddonManagerModal: React.FC<HnlAddonManagerModalProps> = ({
  isOpen,
  onClose,
  modules,
  onToggleModule,
  isSafeMode,
  onToggleSafeMode,
}) => {
  const [searchTerm, setSearchTerm] = useState("");
  const [activeTab, setActiveTab] = useState<"ALL" | "ENABLED" | "CORE">("ALL");

  if (!isOpen) return null;

  const filteredModules = modules.filter((m) => {
    const matchSearch =
      m.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      m.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
      m.workbench.toLowerCase().includes(searchTerm.toLowerCase());

    if (activeTab === "ENABLED") return matchSearch && m.isEnabled;
    if (activeTab === "CORE") return matchSearch && m.isCore;
    return matchSearch;
  });

  const totalLoadedMemory = modules
    .filter((m) => m.isEnabled)
    .reduce((sum, m) => sum + m.memoryWeightMb, 0);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4 animate-fade-in">
      <div className="bg-[#141619] border border-neutral-700 rounded-xl shadow-2xl w-full max-w-3xl flex flex-col max-h-[85vh] overflow-hidden text-neutral-200">
        {/* Header */}
        <div className="p-4 border-b border-neutral-800 flex items-center justify-between bg-neutral-900/80">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-lg bg-sky-500/20 text-sky-400 border border-sky-500/30">
              <Package className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-neutral-100 flex items-center space-x-2">
                <span>HNL Addon & Module Manager</span>
                <span className="text-[10px] bg-sky-950 text-sky-400 px-2 py-0.5 rounded-full border border-sky-800">
                  FreeCAD Architecture
                </span>
              </h3>
              <p className="text-xs text-neutral-400">
                Bật / Tắt các Workbench & Module để tối ưu hóa hiệu năng RAM và thời gian nạp plugin vào AutoCAD.
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-neutral-400 hover:text-neutral-100 hover:bg-neutral-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Toolbar & Safe Mode Control */}
        <div className="p-3 border-b border-neutral-800 bg-neutral-900/40 flex flex-wrap items-center justify-between gap-2 text-xs">
          <div className="flex items-center space-x-2">
            <div className="relative w-56">
              <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-neutral-500" />
              <input
                type="text"
                placeholder="Tìm kiếm module..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-full bg-neutral-950 border border-neutral-700 rounded-lg pl-8 pr-2.5 py-1.5 text-xs text-neutral-200 focus:border-sky-500 focus:outline-none"
              />
            </div>

            <div className="flex bg-neutral-950 rounded-lg p-0.5 border border-neutral-800">
              {(["ALL", "ENABLED", "CORE"] as const).map((tab) => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={`px-2.5 py-1 rounded-md transition font-medium text-[11px] ${
                    activeTab === tab
                      ? "bg-sky-600 text-white shadow"
                      : "text-neutral-400 hover:text-neutral-200"
                  }`}
                >
                  {tab === "ALL" ? "Tất cả" : tab === "ENABLED" ? "Đang bật" : "Core"}
                </button>
              ))}
            </div>
          </div>

          {/* Safe Mode Toggle */}
          <div className="flex items-center space-x-2">
            <button
              onClick={onToggleSafeMode}
              className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg border text-xs font-semibold transition ${
                isSafeMode
                  ? "bg-amber-500/20 text-amber-300 border-amber-500/50"
                  : "bg-neutral-800 hover:bg-neutral-700 text-neutral-300 border-neutral-700"
              }`}
            >
              <ShieldAlert className={`w-3.5 h-3.5 ${isSafeMode ? "text-amber-400" : "text-neutral-400"}`} />
              <span>{isSafeMode ? "Safe Mode: ON" : "Safe Mode: OFF"}</span>
            </button>

            <div className="flex items-center space-x-1 text-neutral-400 bg-neutral-950 px-2 py-1.5 rounded-lg border border-neutral-800">
              <Cpu className="w-3.5 h-3.5 text-sky-400" />
              <span>RAM: <strong>~{totalLoadedMemory} MB</strong></span>
            </div>
          </div>
        </div>

        {/* Modules List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2.5 scrollbar-thin scrollbar-thumb-neutral-700">
          {filteredModules.map((mod) => (
            <div
              key={mod.id}
              className={`p-3 rounded-lg border transition flex items-start justify-between ${
                mod.isEnabled
                  ? "bg-neutral-900/80 border-neutral-700/80"
                  : "bg-neutral-950/50 border-neutral-800/60 opacity-60"
              }`}
            >
              <div className="space-y-1 max-w-[80%]">
                <div className="flex items-center space-x-2">
                  <h4 className="font-semibold text-sm text-neutral-100">{mod.name}</h4>
                  <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400 border border-neutral-700">
                    {mod.workbench}
                  </span>
                  <span className="text-[10px] text-neutral-500">v{mod.version}</span>
                  {mod.isCore && (
                    <span className="text-[10px] bg-emerald-950 text-emerald-400 px-1.5 py-0.5 rounded border border-emerald-800">
                      Bắt buộc (Core)
                    </span>
                  )}
                </div>

                <p className="text-xs text-neutral-300 leading-relaxed">{mod.description}</p>

                <div className="flex items-center space-x-2 pt-1">
                  <span className="text-[10px] text-neutral-500">Tác giả: {mod.author}</span>
                  <span className="text-[10px] text-neutral-500">•</span>
                  <span className="text-[10px] text-neutral-400">RAM: ~{mod.memoryWeightMb} MB</span>
                </div>
              </div>

              <div className="flex items-center space-x-2">
                <button
                  disabled={mod.isCore}
                  onClick={() => onToggleModule(mod.id)}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition flex items-center space-x-1.5 ${
                    mod.isEnabled
                      ? "bg-sky-600 hover:bg-sky-500 text-white"
                      : "bg-neutral-800 hover:bg-neutral-700 text-neutral-300"
                  } disabled:opacity-40 disabled:cursor-not-allowed`}
                >
                  {mod.isEnabled ? (
                    <>
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>Đã nạp</span>
                    </>
                  ) : (
                    <>
                      <XCircle className="w-3.5 h-3.5" />
                      <span>Đã tắt</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          ))}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-neutral-800 bg-neutral-900/60 flex items-center justify-between text-xs text-neutral-400">
          <span>Tổng số {modules.length} module | {modules.filter(m => m.isEnabled).length} đang hoạt động</span>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white font-semibold shadow transition"
          >
            Đóng & Áp dụng
          </button>
        </div>
      </div>
    </div>
  );
};
