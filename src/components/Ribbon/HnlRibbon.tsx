import React, { useState } from "react";
import {
  Wand2,
  Layers,
  Square,
  Maximize,
  Scissors,
  Grid,
  Type,
  Link,
  BarChart3,
  Calculator,
  Table as TableIcon,
  Layout as LayoutIcon,
  Eye,
  Languages,
  Bot,
  Code2,
  FolderOpen,
  Settings,
  Plus,
  Play,
  FileSpreadsheet,
  CheckSquare,
  Sparkles,
  AlertTriangle,
  RotateCcw,
  RefreshCw,
  Search,
  Download,
  Copy,
  ChevronDown,
  Laptop,
  Monitor,
  CornerDownRight,
  AlignLeft,
  ListPlus,
  Zap,
  CircleDot,
  Link2,
  Save,
  Bug,
} from "lucide-react";
import { HnlLogo } from "../Brand/HnlLogo";

interface HnlRibbonProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
  onExecuteCommand: (commandKey: string, params?: any) => void;
  onOpenCommandCenter: () => void;
  onToggleAiPalette: () => void;
  onOpenLispBuilder: () => void;
  onOpenTableBuilder: () => void;
  onOpenAuditModal: () => void;
  onOpenExcelExport: () => void;
  onOpenNetPluginExporter: () => void;
  onOpenSettings: () => void;
  onOpenDiagnostics?: () => void;
  onOpenProfessionalAudit?: () => void;
  onOpenPlotPublish?: () => void;
  onOpen2DProfessional?: () => void;
  onOpenUsageGuide?: () => void;
  onOpenSketchUpBridge?: () => void;
  onOpenAutoDetailComposer: () => void;
  onOpenStandaloneExeBuilder: () => void;
  onOpenDrywallCeilingStudio: () => void;
  onOpenWindowsCompat?: () => void;
  onOpenAddonManager?: () => void;
  onOpenShopCheck?: () => void;
  onOpenSectionGenerator?: () => void;
  onOpenMepClash?: () => void;
  onOpenMultiExport?: () => void;
  onOpenBuildingCode?: () => void;
  onToggleSpreadsheet?: () => void;
  onToggleProjectTree?: () => void;
  onRecomputeDAG?: () => void;
  selectedWorkbench?: string;
  onChangeWorkbench?: (wb: string) => void;
  isAiPaletteOpen: boolean;
  canUndo: boolean;
  canRedo: boolean;
  onUndo: () => void;
  onRedo: () => void;
  connectionStatus?: { connected: boolean; version?: string; drawingName?: string; source: string };
  lastAutosaveAt?: string | null;
  documentName?: string;
  isDirty?: boolean;
}

export const HnlRibbon: React.FC<HnlRibbonProps> = ({
  activeTab,
  onTabChange,
  onExecuteCommand,
  onOpenCommandCenter,
  onToggleAiPalette,
  onOpenLispBuilder,
  onOpenTableBuilder,
  onOpenAuditModal,
  onOpenExcelExport,
  onOpenNetPluginExporter,
  onOpenSettings,
  onOpenDiagnostics,
  onOpenProfessionalAudit,
  onOpenPlotPublish,
  onOpen2DProfessional,
  onOpenUsageGuide,
  onOpenSketchUpBridge,
  onOpenAutoDetailComposer,
  onOpenStandaloneExeBuilder,
  onOpenDrywallCeilingStudio,
  onOpenWindowsCompat,
  onOpenAddonManager,
  onOpenShopCheck,
  onOpenSectionGenerator,
  onOpenMepClash,
  onOpenMultiExport,
  onOpenBuildingCode,
  onToggleSpreadsheet,
  onToggleProjectTree,
  onRecomputeDAG,
  selectedWorkbench,
  onChangeWorkbench,
  isAiPaletteOpen,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  connectionStatus,
  lastAutosaveAt,
  documentName,
  isDirty,
}) => {
  const ribbonGroups = [
    { id: "HOME", label: "Home", tabs: [["VE_NHANH", "Draw"], ["CHINH_SUA", "Modify"]] },
    { id: "PRO2D", label: "2D Professional", tabs: [
      ["PRO_2D_CENTER", "Tool Center"],
      ["TEXT_NOTE", "Text & Attribute"],
      ["FIELD", "Field"],
      ["BLOCK", "Block"],
      ["THONG_KE", "Quantity"],
      ["DIEN_TICH", "Area & Length"],
      ["BANG", "Table"],
      ["DICH_THUAT", "Translation"]
    ] },
    { id: "SKETCHUP", label: "SketchUp", tabs: [["SKETCHUP_BRIDGE", "CAD ⇄ SketchUp"]] },
    { id: "LAYOUT_PUBLISH", label: "Layout & Publish", tabs: [["LAYOUT", "Layout"], ["VIEWPORT", "Viewport"], ["PUBLISH_CENTER", "Plot / Publish / Sheet Set"]] },
    { id: "AI_AUTO", label: "AI & Legacy", tabs: [["AI_CAD", "AI CAD"], ["LISP", "Legacy Lisp"], ["THU_VIEN", "Thư viện"]] },
    { id: "ENGINEERING", label: "Kỹ thuật", tabs: [["PILE", "Cọc & Nền móng"], ["CAI_DAT", "Cài đặt"]] },
  ];
  const activeGroup = ribbonGroups.find((group) => group.tabs.some(([id]) => id === activeTab)) || ribbonGroups[0];
  const [toolsOpen, setToolsOpen] = useState(false);

  return (
    <div className="w-full bg-[#1E1F22] border-b border-neutral-800 text-neutral-200 select-none shadow-md flex flex-col">
      {/* Top Application Bar with Brand, Undo/Redo, Quick Search & AI Trigger */}
      <div className="min-h-10 px-2 xl:px-4 bg-[#141517] border-b border-neutral-800 flex items-center gap-2 justify-between text-xs overflow-x-auto scrollbar-none">
        <div className="flex items-center space-x-3 shrink-0">
          <div className="flex items-center space-x-2">
            <HnlLogo size="sm" showSubtitle={false} />
            <div className="hidden lg:flex items-center gap-1.5 px-2 py-1 rounded-md bg-[#202226] border border-neutral-800 max-w-[260px]" title={documentName}>
              <Save className={`w-3.5 h-3.5 ${isDirty ? "text-amber-400" : "text-emerald-400"}`} />
              <span className="truncate text-[11px] text-neutral-300">{documentName || "Untitled.hnl.json"}</span>
              {isDirty && <span className="text-amber-400 text-[10px] font-bold">●</span>}
            </div>
            <button onClick={() => onExecuteCommand("AUTOCAD_BRIDGE_STATUS")} className={`px-1.5 py-0.5 rounded text-[10px] border font-mono flex items-center gap-1 ${connectionStatus?.connected ? "bg-emerald-950/40 border-emerald-700 text-emerald-300" : "bg-neutral-800 border-neutral-700 text-neutral-400"}`} title="Trạng thái AutoCAD Bridge">
              <Link2 className="w-3 h-3" />
              {connectionStatus?.connected ? `AutoCAD ${connectionStatus.version || ""} • ${connectionStatus.drawingName || "Connected"}` : "Standalone Workspace • AutoCAD plugin chưa kết nối"}
            </button>
          </div>

          <div className="h-4 w-[1px] bg-neutral-800 mx-2" />

          {/* Undo / Redo */}
          <div className="flex items-center space-x-1">
            <button
              onClick={onUndo}
              disabled={!canUndo}
              title="Undo (Ctrl+Z)"
              className={`p-1.5 rounded transition ${
                canUndo ? "hover:bg-neutral-800 text-neutral-200" : "text-neutral-600 cursor-not-allowed"
              }`}
            >
              <RotateCcw className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={onRedo}
              disabled={!canRedo}
              title="Redo (Ctrl+Y)"
              className={`p-1.5 rounded transition ${
                canRedo ? "hover:bg-neutral-800 text-neutral-200" : "text-neutral-600 cursor-not-allowed"
              }`}
            >
              <RefreshCw className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* Global Command Center Search Bar (Ctrl + Space) */}
        <button
          onClick={onOpenCommandCenter}
          className="flex items-center space-x-2 bg-[#1E1F22] hover:bg-[#25272C] text-neutral-400 hover:text-neutral-200 px-3 py-1.5 rounded-md border border-neutral-700/70 w-[clamp(240px,24vw,360px)] shrink-0 transition justify-between"
        >
          <div className="flex items-center space-x-2 truncate">
            <Search className="w-3.5 h-3.5 text-cyan-400" />
            <span>Tìm kiếm lệnh CAD (vd: mleader, vách, trần...)...</span>
          </div>
          <kbd className="bg-neutral-800 px-1.5 py-0.5 rounded text-[10px] font-mono border border-neutral-700 text-neutral-400">
            Ctrl+Space
          </kbd>
        </button>

        <div className="hidden 2xl:flex items-center gap-1 text-[10px] text-neutral-500 shrink-0" title={lastAutosaveAt ? `AutoSave: ${new Date(lastAutosaveAt).toLocaleString()}` : "Chưa AutoSave"}><Save className="w-3 h-3 text-emerald-500"/><span>{lastAutosaveAt ? "AutoSave OK" : "AutoSave"}</span></div>

        {/* Action Buttons: Workbench Selector, FreeCAD Project Tree, Shop Check, Addon Manager, Drywall Studio, AI */}
        <div className="flex items-center space-x-1.5 shrink-0">
          {/* FreeCAD-style Workbench Selector */}
          {onChangeWorkbench && (
            <div className="flex items-center bg-neutral-900 border border-neutral-700 rounded px-1.5 py-0.5 space-x-1">
              <span className="text-[10px] text-sky-400 font-mono font-bold">WB:</span>
              <select
                value={selectedWorkbench || "HNL_CAD"}
                onChange={(e) => onChangeWorkbench(e.target.value)}
                className="bg-transparent text-xs text-neutral-100 font-semibold focus:outline-none cursor-pointer"
              >
                <option value="HNL_CAD" className="bg-neutral-900 text-neutral-100">HNL CAD 2D/3D</option>
                <option value="HNL_CEILING" className="bg-neutral-900 text-neutral-100">HNL Ceiling (Trần)</option>
                <option value="HNL_WALL" className="bg-neutral-900 text-neutral-100">HNL Wall (Vách/EI)</option>
                <option value="HNL_SHOPDRAWING" className="bg-neutral-900 text-neutral-100">HNL Shopdrawing</option>
                <option value="HNL_QUANTITY" className="bg-neutral-900 text-neutral-100">HNL Quantity & BOQ</option>
                <option value="HNL_LAYOUT" className="bg-neutral-900 text-neutral-100">HNL Layout & Sheet</option>
                <option value="HNL_AI" className="bg-neutral-900 text-neutral-100">HNL AI Copilot</option>
              </select>
            </div>
          )}

          {onToggleProjectTree && (
            <button onClick={onToggleProjectTree} className="hidden xl:flex items-center gap-1 px-2 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-sky-300 border border-neutral-700 text-xs" title="Cây dự án">
              <Layers className="w-3.5 h-3.5" /><span>Project</span>
            </button>
          )}
          <div className="relative">
            <button onClick={() => setToolsOpen((v) => !v)} className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200 border border-neutral-700 text-xs font-semibold" title="Công cụ HNL">
              <Settings className="w-3.5 h-3.5" /><span>Công cụ</span><ChevronDown className="w-3 h-3" />
            </button>
            {toolsOpen && (
              <div className="absolute right-0 top-8 z-[80] w-56 rounded-lg border border-neutral-700 bg-[#1b1d20] shadow-2xl p-1 text-xs">
                {onToggleSpreadsheet && <button onClick={() => { onToggleSpreadsheet(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">Spreadsheet tham số</button>}
                {onOpenShopCheck && <button onClick={() => { onOpenShopCheck(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">HNL Shop Check</button>}
                {onOpenDrywallCeilingStudio && <button onClick={() => { onOpenDrywallCeilingStudio(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">Thạch cao & PCCC</button>}
                {onOpenAutoDetailComposer && <button onClick={() => { onOpenAutoDetailComposer(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">Auto Detail & Layout</button>}
                {onOpenAddonManager && <button onClick={() => { onOpenAddonManager(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">Addon Manager</button>}
                {onOpenWindowsCompat && <button onClick={() => { onOpenWindowsCompat(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700">Windows Compatibility</button>}
                {onOpenSketchUpBridge && <button onClick={() => { onOpenSketchUpBridge(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-violet-300">CAD 2D ⇄ SketchUp Bridge</button>}
                {onOpenUsageGuide && <button onClick={() => { onOpenUsageGuide(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-emerald-300">Hướng dẫn sử dụng</button>}
                {onOpenProfessionalAudit && <button onClick={() => { onOpenProfessionalAudit(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-amber-300">Professional Audit Center</button>}
                {onOpen2DProfessional && <button onClick={() => { onOpen2DProfessional(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-fuchsia-300">2D Professional Tool Center</button>}
                {onOpenPlotPublish && <button onClick={() => { onOpenPlotPublish(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-sky-300">Plot / Publish / Sheet Set</button>}
                {onOpenDiagnostics && <button onClick={() => { onOpenDiagnostics(); setToolsOpen(false); }} className="w-full text-left px-3 py-2 rounded hover:bg-neutral-700 text-cyan-300 flex items-center gap-2"><Bug className="w-3.5 h-3.5"/> Trung tâm chẩn đoán lỗi</button>}
              </div>
            )}
          </div>
          <button
            onClick={onToggleAiPalette}
            className={`flex items-center space-x-1.5 px-2.5 py-1 rounded font-medium transition shadow text-xs ${
              isAiPaletteOpen
                ? "bg-cyan-500 text-black shadow-cyan-500/20"
                : "bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 border border-cyan-500/40"
            }`}
          >
            <Bot className="w-3.5 h-3.5" />
            <span>AI Palette</span>
          </button>
        </div>
      </div>

      {/* Ribbon Navigation: 6 primary groups + contextual subtabs */}
      <div className="bg-[#1E1F22] border-b border-neutral-800">
        <div className="flex items-center px-2 overflow-x-auto scrollbar-none">
          {ribbonGroups.map((group) => {
            const isActive = group.id === activeGroup.id;
            return <button key={group.id} onClick={() => onTabChange(group.tabs[0][0])} className={`px-4 py-2 text-[13px] font-semibold border-b-2 whitespace-nowrap transition ${isActive ? "border-cyan-400 text-cyan-300 bg-[#25272C]" : "border-transparent text-neutral-400 hover:text-neutral-100 hover:bg-neutral-800/40"}`}>{group.label}</button>;
          })}
        </div>
        <div className="flex items-center px-3 min-h-8 bg-[#202226] gap-1 overflow-x-auto scrollbar-none">
          {activeGroup.tabs.map(([id, label]) => <button key={id} onClick={() => onTabChange(id)} className={`px-3 py-1 rounded text-xs whitespace-nowrap ${activeTab === id ? "bg-cyan-500/15 text-cyan-300 border border-cyan-500/30" : "text-neutral-400 hover:bg-neutral-700 hover:text-neutral-200 border border-transparent"}`}>{label}</button>)}
        </div>
      </div>

      {/* Active Ribbon Panel Toolbar */}
      <div className="min-h-24 px-3 xl:px-4 py-2 bg-[#25272C] flex items-center space-x-4 overflow-x-auto text-[12px]">
        {activeTab === "VE_NHANH" && (
          <>
            {/* Nhóm Smart Wall */}
            <div className="flex flex-col items-center border-r border-neutral-700/60 pr-3">
              <div className="flex items-center space-x-2">
                <button
                  onClick={() => onExecuteCommand("SMART_WALL_100")}
                  className="flex flex-col items-center justify-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition group"
                  title="Vẽ tường 100mm: chọn 2 điểm tim, tự offset ±50mm, join góc và hatch"
                >
                  <Square className="w-5 h-5 text-cyan-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Tường 100</span>
                </button>
                <button
                  onClick={() => onExecuteCommand("SMART_WALL_200")}
                  className="flex flex-col items-center justify-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition group"
                  title="Vẽ tường 200mm: chọn 2 điểm tim, tự offset ±100mm, join góc và hatch"
                >
                  <Square className="w-5 h-5 text-amber-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Tường 200</span>
                </button>
              </div>
              <span className="text-[10px] text-neutral-500 mt-1 font-mono">TƯỜNG THÔNG MINH</span>
            </div>

            {/* Nhóm Smart Ceiling & Drywall Studio */}
            <div className="flex flex-col items-center border-r border-neutral-700/60 pr-3">
              <div className="flex items-center space-x-2">
                <button
                  onClick={() => onExecuteCommand("SMART_CEILING")}
                  className="flex flex-col items-center justify-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition group"
                  title="Tự động phân bổ hệ xương chính (@800), xương phụ (@400), ty treo và viền tường trong phòng"
                >
                  <Grid className="w-5 h-5 text-amber-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Trần thạch cao</span>
                </button>
                <button
                  onClick={onOpenDrywallCeilingStudio}
                  className="flex flex-col items-center justify-center w-20 h-14 p-1 rounded bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-300 transition group"
                  title="Mở Studio Trần Vách PCCC (EI30-EI120, Deflection Head, Grid AI)"
                >
                  <Layers className="w-5 h-5 text-amber-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Drywall Studio</span>
                </button>
                <button
                  onClick={onOpenSectionGenerator || (() => onExecuteCommand("OPEN_SECTION_GEN"))}
                  className="flex flex-col items-center justify-center w-20 h-14 p-1 rounded bg-sky-500/10 hover:bg-sky-500/20 border border-sky-500/30 text-sky-300 transition group"
                  title="Tạo mặt cắt chi tiết A-A / B-B tham số tự động (Parametric Cross Section Engine)"
                >
                  <Maximize className="w-5 h-5 text-sky-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Mặt Cắt A-A</span>
                </button>
                <button
                  onClick={onOpenMepClash || (() => onExecuteCommand("OPEN_MEP_CLASH"))}
                  className="flex flex-col items-center justify-center w-20 h-14 p-1 rounded bg-red-500/10 hover:bg-red-500/20 border border-red-500/30 text-red-300 transition group"
                  title="Kiểm tra va chạm MEP (Ống gió, đèn, PCCC) với Khung xương trần/vách"
                >
                  <AlertTriangle className="w-5 h-5 text-red-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">Clash MEP</span>
                </button>
              </div>
              <span className="text-[10px] text-neutral-500 mt-1 font-mono">TRẦN & VÁCH SHOP</span>
            </div>

            {/* Nhóm Ghi Chú MLeader Ưu Tiên */}
            <div className="flex flex-col items-center border-r border-neutral-700/60 pr-3">
              <div className="flex items-center space-x-2">
                <button
                  onClick={() => onExecuteCommand("DRAW_MLEADER")}
                  className="flex flex-col items-center justify-center w-24 h-14 p-1 rounded bg-cyan-500/10 hover:bg-cyan-500/20 border border-cyan-500/30 text-cyan-300 transition group"
                  title="Vẽ Multileader (MLeader) chú thích shopdrawing chuẩn mũi tên & vai hạ cánh"
                >
                  <CornerDownRight className="w-5 h-5 text-cyan-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">MLeader Ghi Chú</span>
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_MLEADER_MATERIAL")}
                  className="flex flex-col items-center justify-center w-24 h-14 p-1 rounded bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 text-amber-300 transition group"
                  title="MLeader chú thích cấu tạo vật liệu vách/trần thạch cao chống cháy"
                >
                  <ListPlus className="w-5 h-5 text-amber-400 group-hover:scale-110 transition" />
                  <span className="mt-1 text-[11px] font-semibold text-center leading-tight">MLeader Vật Liệu</span>
                </button>
              </div>
              <span className="text-[10px] text-cyan-400 mt-1 font-mono">ƯU TIÊN MULTILEADER</span>
            </div>

            {/* Lệnh vẽ cơ bản */}
            <div className="flex flex-col items-center border-r border-neutral-700/60 pr-3">
              <div className="grid grid-cols-3 gap-1">
                <button
                  onClick={() => onExecuteCommand("DRAW_RECTANGLE")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Vẽ hình chữ nhật (RECTANG)"
                >
                  Rect
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_LINE")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Vẽ đoạn thẳng (LINE)"
                >
                  Line
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_POLYLINE")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Vẽ Polyline (PLINE)"
                >
                  Pline
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_CIRCLE")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Vẽ hình tròn (CIRCLE)"
                >
                  Circle
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_HATCH")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Đổ vật liệu (HATCH)"
                >
                  Hatch
                </button>
                <button
                  onClick={() => onExecuteCommand("DRAW_OFFSET")}
                  className="px-2.5 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200"
                  title="Sao chép song song (OFFSET)"
                >
                  Offset
                </button>
              </div>
              <span className="text-[10px] text-neutral-500 mt-1 font-mono">CÔNG CỤ CƠ BẢN</span>
            </div>
          </>
        )}

        {activeTab === "CHINH_SUA" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("EDIT_TRIM")}
              className="flex flex-col items-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Cắt đối tượng giao nhau (TRIM)"
            >
              <Scissors className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px]">Trim</span>
            </button>
            <button
              onClick={() => onExecuteCommand("EDIT_EXTEND")}
              className="flex flex-col items-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Kéo dài tới biên (EXTEND)"
            >
              <Maximize className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px]">Extend</span>
            </button>
            <button
              onClick={() => onExecuteCommand("EDIT_FILLET")}
              className="flex flex-col items-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Bo tròn góc (FILLET)"
            >
              <Square className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px]">Fillet</span>
            </button>
            <button
              onClick={() => onExecuteCommand("EDIT_JOIN")}
              className="flex flex-col items-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Nối các đoạn thành Polyline kín (JOIN)"
            >
              <Link className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px]">Join</span>
            </button>
            <button
              onClick={() => onExecuteCommand("EDIT_ARRAY")}
              className="flex flex-col items-center w-16 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Nhân bản đối tượng theo lưới (ARRAY)"
            >
              <Grid className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px]">Array</span>
            </button>
          </div>
        )}

        {activeTab === "BLOCK" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("BLOCK_SIMILARITY")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-neutral-800/80 hover:bg-neutral-700 text-cyan-300 transition"
              title="Tìm & đề xuất gom các Block hình học giống nhau 95-99%"
            >
              <Sparkles className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Block Similarity</span>
            </button>
            <button
              onClick={() => onExecuteCommand("BLOCK_COUNT")}
              className="flex flex-col items-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Đếm số lượng Block theo tên & Layer"
            >
              <Calculator className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Đếm Block</span>
            </button>
            <button
              onClick={() => onExecuteCommand("BLOCK_SYNC_ATTRIB")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Đồng bộ thuộc tính Attribute (ATTSYNC)"
            >
              <RefreshCw className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Đồng bộ Attribute</span>
            </button>
            <button
              onClick={() => onExecuteCommand("BLOCK_PURGE_UNUSED")}
              className="flex flex-col items-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-red-300 transition"
              title="Purge sạch các Block không dùng trong bản vẽ"
            >
              <Scissors className="w-5 h-5 text-red-400" />
              <span className="mt-1 text-[11px] text-center">Purge Block</span>
            </button>
          </div>
        )}

        {activeTab === "TEXT_NOTE" && (
          <div className="flex items-center space-x-3">
            {/* Nhóm Multileader Ưu Tiên */}
            <div className="flex items-center space-x-2 border-r border-neutral-700/60 pr-3">
              <button
                onClick={() => onExecuteCommand("DRAW_MLEADER")}
                className="flex flex-col items-center w-28 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
                title="Vẽ Multileader (MLEADER): Nhấp điểm chỉ mũi tên -> Nhấp điểm đặt Text ghi chú"
              >
                <CornerDownRight className="w-5 h-5 text-cyan-400" />
                <span className="mt-1 text-[11px] font-semibold text-center">Vẽ MLeader (ML)</span>
              </button>
              <button
                onClick={() => onExecuteCommand("DRAW_MLEADER_MATERIAL")}
                className="flex flex-col items-center w-32 h-14 p-1 rounded bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 transition border border-amber-500/30"
                title="MLeader chú thích cấu tạo vách thạch cao EI30-EI120 / Trần giật cấp"
              >
                <ListPlus className="w-5 h-5 text-amber-400" />
                <span className="mt-1 text-[11px] font-semibold text-center">MLeader Cấu Tạo</span>
              </button>
              <button
                onClick={() => onExecuteCommand("MLEADER_ALIGN")}
                className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
                title="Căn thẳng hàng thẳng cột tất cả các đường Multileader (MLEADERALIGN)"
              >
                <AlignLeft className="w-5 h-5 text-emerald-400" />
                <span className="mt-1 text-[11px] text-center">Căn Hàng MLeader</span>
              </button>
              <button
                onClick={() => onExecuteCommand("TEXT2MLEADER")}
                className="flex flex-col items-center w-32 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
                title="Tự động gom Text/MText rời rạc và mũi tên thành 1 Multileader chuẩn"
              >
                <Zap className="w-5 h-5 text-amber-400" />
                <span className="mt-1 text-[11px] text-center">Text sang MLeader</span>
              </button>
            </div>

            {/* Xử lý Text & Unicode */}
            <div className="flex items-center space-x-2">
              <button
                onClick={() => onExecuteCommand("TEXT_FIX_UNICODE")}
                className="flex flex-col items-center w-28 h-14 p-1 rounded bg-neutral-800/80 hover:bg-neutral-700 text-cyan-300 transition"
                title="Chuyển đổi bảng mã TCVN3 / VNI-Windows sang Unicode chuẩn"
              >
                <Type className="w-5 h-5 text-cyan-400" />
                <span className="mt-1 text-[11px] font-semibold text-center">Sửa TCVN3/VNI</span>
              </button>
              <button
                onClick={() => onExecuteCommand("TEXT_UPPERCASE")}
                className="flex flex-col items-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
                title="Chuyển tất cả Text được chọn thành IN HOA"
              >
                <Type className="w-5 h-5 text-neutral-300" />
                <span className="mt-1 text-[11px] text-center">Đổi IN HOA</span>
              </button>
              <button
                onClick={() => onExecuteCommand("TEXT_FIND_REPLACE")}
                className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
                title="Tìm kiếm và thay thế nội dung Text hàng loạt"
              >
                <Search className="w-5 h-5 text-neutral-300" />
                <span className="mt-1 text-[11px] text-center">Tìm & Thay thế</span>
              </button>
              <button
                onClick={() => onExecuteCommand("TEXT_TRIM_SPACE")}
                className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
                title="Xóa các dấu cách thừa giữa các từ"
              >
                <Scissors className="w-5 h-5 text-neutral-300" />
                <span className="mt-1 text-[11px] text-center">Xóa cách thừa</span>
              </button>
            </div>
          </div>
        )}

        {activeTab === "FIELD" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("FIELD_SCAN_BROKEN")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 transition border border-amber-500/30"
              title="Quét & tô sáng các Field bị lỗi hiển thị '####'"
            >
              <AlertTriangle className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Field Lỗi (####)</span>
            </button>
            <button
              onClick={() => onExecuteCommand("FIELD_RELINK")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tự động khôi phục liên kết Field với Object"
            >
              <Link className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] text-center">Relink Field</span>
            </button>
            <button
              onClick={() => onExecuteCommand("FIELD_INSERT_AREA")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tạo Text gắn Field Diện tích tự cập nhật khi Polyline thay đổi"
            >
              <Calculator className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Field Diện tích</span>
            </button>
          </div>
        )}

        {activeTab === "THONG_KE" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenTableBuilder}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
              title="Tạo bảng thống kê tự động từ Block, Polyline, Text"
            >
              <TableIcon className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Table Builder</span>
            </button>
            <button
              onClick={onOpenMultiExport || onOpenExcelExport}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-300 transition border border-emerald-500/40"
              title="Bộ máy xuất bản đa định dạng: AutoCAD DXF R2000, Excel BOQ CSV, PDF Layout & Net Plugin"
            >
              <Download className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Xuất DXF/BOQ/PDF</span>
            </button>
            <button
              onClick={onOpenBuildingCode || (() => onExecuteCommand("OPEN_BUILDING_CODE"))}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 transition border border-amber-500/40"
              title="Tra cứu Tiêu Chuẩn Xây Dựng TCVN 9377 / ASTM C636 & Tính toán tải trọng ty treo trần"
            >
              <Calculator className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">TCVN / ASTM Code</span>
            </button>
            <button
              onClick={() => onExecuteCommand("COUNT_LIGHTS")}
              className="flex flex-col items-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Thống kê tất cả đèn theo chủng loại và công suất"
            >
              <BarChart3 className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">Đếm Đèn</span>
            </button>
          </div>
        )}

        {activeTab === "DIEN_TICH" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("CALC_AREA_TOTAL")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-300 transition border border-emerald-500/40"
              title="Tính tổng diện tích các Polyline được chọn và ghi ra bản vẽ"
            >
              <Calculator className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Tổng Diện Tích</span>
            </button>
            <button
              onClick={() => onExecuteCommand("LABEL_ROOM_AREAS")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tự động gắn nhãn tên phòng và diện tích vào tâm hình học"
            >
              <Type className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] text-center">Ghi Nhãn Phòng</span>
            </button>
            <button
              onClick={() => onExecuteCommand("CALC_PERIMETER")}
              className="flex flex-col items-center w-20 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tính tổng chu vi / chiều dài Polyline"
            >
              <Maximize className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">Chu vi</span>
            </button>
          </div>
        )}

        {activeTab === "BANG" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenTableBuilder}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
            >
              <TableIcon className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Tạo Bảng CAD</span>
            </button>
            <button
              onClick={() => onExecuteCommand("TABLE_SYNC_DRAWING")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Cập nhật lại số liệu trong bảng khi bản vẽ thay đổi"
            >
              <RefreshCw className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Cập Nhật Bảng</span>
            </button>
            <button
              onClick={onOpenExcelExport}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
            >
              <FileSpreadsheet className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Xuất Bảng Excel</span>
            </button>
          </div>
        )}

        {activeTab === "LAYOUT" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenAutoDetailComposer}
              className="flex flex-col items-center w-36 h-14 p-1 rounded bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-bold transition shadow-md"
              title="AI tự trích chi tiết, tạo mặt cắt và dàn trang Sheet Shopdrawing tự động"
            >
              <Sparkles className="w-5 h-5 text-white" />
              <span className="mt-1 text-[11px] text-center font-bold">Auto Detail & Layout</span>
            </button>
            <button
              onClick={() => onExecuteCommand("AUTO_LAYOUT_A3")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
              title="Tạo Layout A3 chuẩn HNL, chèn Khung tên & Viewport tối ưu"
            >
              <LayoutIcon className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Tạo Layout A3</span>
            </button>
            <button
              onClick={() => onExecuteCommand("AUTO_LAYOUT_A4")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
            >
              <LayoutIcon className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">Tạo Layout A4</span>
            </button>
            <button
              onClick={() => onExecuteCommand("LAYOUT_RECOGNIZE_TITLEBLOCK")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tự động nhận diện khung tên, số bản vẽ, người vẽ"
            >
              <CheckSquare className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Nhận Diện Khung</span>
            </button>
          </div>
        )}

        {activeTab === "VIEWPORT" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("VIEWPORT_AUTO_FIT")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
              title="Tự động tính toán tỷ lệ Viewport tối ưu (1:50 / 1:75 / 1:100)"
            >
              <Maximize className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Auto Fit Tỷ Lệ</span>
            </button>
            <button
              onClick={() => onExecuteCommand("VIEWPORT_LOCK_ALL")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Khóa tất cả Viewport trên bản vẽ để chống lệch tỷ lệ"
            >
              <CheckSquare className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Khóa Tất Cả VP</span>
            </button>
            <button
              onClick={() => onExecuteCommand("VIEWPORT_MULTI_ARRANGE")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Tự động bố trí nhiều chi tiết (Mặt bằng + Mặt cắt + Detail)"
            >
              <Grid className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Bố Trí Đa VP</span>
            </button>
          </div>
        )}

        {activeTab === "DICH_THUAT" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("TRANSLATE_VI_EN_BILINGUAL")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
              title="Dịch Việt -> Anh dạng Song ngữ (Tiếng Việt trên, Tiếng Anh dưới)"
            >
              <Languages className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Song Ngữ (Vi-En)</span>
            </button>
            <button
              onClick={() => onExecuteCommand("TRANSLATE_REPLACE_EN")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Thay thế chữ gốc thành tiếng Anh"
            >
              <Languages className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">Thay Thành Tiếng Anh</span>
            </button>
            <button
              onClick={() => onExecuteCommand("TRANSLATE_MEMORY_MANAGE")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Quản lý từ điển Translation Memory đã lưu"
            >
              <FolderOpen className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Translation Memory</span>
            </button>
          </div>
        )}

        {activeTab === "AI_CAD" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenAutoDetailComposer}
              className="flex flex-col items-center w-36 h-14 p-1 rounded bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-bold transition shadow-md"
              title="AI tự trích chi tiết, tạo mặt cắt và dàn trang Sheet Shopdrawing tự động"
            >
              <Sparkles className="w-5 h-5 text-white" />
              <span className="mt-1 text-[11px] text-center font-bold">Auto Detail Composer</span>
            </button>
            <button
              onClick={onToggleAiPalette}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-neutral-800 hover:bg-neutral-700 text-cyan-300 transition border border-neutral-700"
            >
              <Bot className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] text-center">Mở AI Chat</span>
            </button>
            <button
              onClick={onOpenLispBuilder}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-neutral-800 hover:bg-neutral-700 text-cyan-300 transition border border-neutral-700"
              title="Tự động viết lệnh AutoLISP theo yêu cầu bằng AI"
            >
              <Code2 className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">AI Lisp Builder</span>
            </button>
            <button
              onClick={() => onExecuteCommand("AI_EXPLAIN_SELECTION")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="AI đọc metadata CAD và giải thích các đối tượng đang chọn"
            >
              <Eye className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">AI Đọc Bản Vẽ</span>
            </button>
          </div>
        )}

        {activeTab === "LISP" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenLispBuilder}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
            >
              <Code2 className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Tạo Lisp Mới (AI)</span>
            </button>
            <button
              onClick={() => onExecuteCommand("LISP_RUN_APAREA")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
            >
              <Play className="w-5 h-5 text-emerald-400" />
              <span className="mt-1 text-[11px] text-center">Chạy C:APAREA</span>
            </button>
            <button
              onClick={() => onExecuteCommand("LISP_RUN_APWALL")}
              className="flex flex-col items-center w-24 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
            >
              <Play className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Chạy C:APWALL</span>
            </button>
          </div>
        )}

        {activeTab === "THU_VIEN" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={() => onExecuteCommand("OPEN_BLOCK_LIBRARY")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 transition border border-cyan-500/40"
            >
              <FolderOpen className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] font-semibold text-center">Thư Viện Block</span>
            </button>
            <button
              onClick={() => onExecuteCommand("SCAN_DWG_FOLDER")}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-neutral-200 transition"
              title="Quét thư mục chứa các file .DWG/.DXF để tự tạo thư viện"
            >
              <Search className="w-5 h-5 text-neutral-300" />
              <span className="mt-1 text-[11px] text-center">Quét Thư Mục DWG</span>
            </button>
          </div>
        )}


        {activeTab === "PILE" && (
          <>
            <div className="flex flex-col items-center border-r border-neutral-700/60 pr-3">
              <div className="flex items-center space-x-2">
                <button onClick={() => onExecuteCommand("OPEN_PILE_STUDIO")} className="flex flex-col items-center justify-center w-24 h-14 p-1 rounded bg-cyan-500/10 hover:bg-cyan-500/20 border border-cyan-500/30 text-cyan-300" title="HNL Pile Studio: bố trí cọc tham số, tag và schedule">
                  <CircleDot className="w-5 h-5 text-cyan-400"/><span className="mt-1 text-[11px] font-semibold">Pile Studio</span>
                </button>
                <button onClick={() => onExecuteCommand("OPEN_TABLE_BUILDER")} className="flex flex-col items-center justify-center w-24 h-14 p-1 rounded hover:bg-neutral-700" title="Tạo bảng thống kê cọc từ dữ liệu HNL">
                  <TableIcon className="w-5 h-5 text-emerald-400"/><span className="mt-1 text-[11px] font-semibold">Pile Schedule</span>
                </button>
                <button onClick={() => onExecuteCommand("OPEN_BUILDING_CODE")} className="flex flex-col items-center justify-center w-24 h-14 p-1 rounded hover:bg-neutral-700" title="Kiểm tra tiêu chuẩn / nguồn kỹ thuật dự án">
                  <CheckSquare className="w-5 h-5 text-amber-400"/><span className="mt-1 text-[11px] font-semibold">Tiêu chuẩn</span>
                </button>
              </div><span className="text-[10px] text-neutral-500 mt-1 font-mono">SMART PILE • KHÔNG SUY ĐOÁN CAPACITY</span>
            </div>
          </>
        )}
        {activeTab === "PRO_2D_CENTER" && (
          <div className="flex items-center gap-3">
            <button onClick={() => onOpen2DProfessional?.()} className="px-4 py-3 rounded-lg bg-fuchsia-500/15 border border-fuchsia-500/40 text-fuchsia-200 font-semibold">Mở 2D Professional Tool Center</button>
            <button onClick={() => onExecuteCommand("SMART_TEXT_CENTER")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Text & Attribute</button>
            <button onClick={() => onExecuteCommand("FIELD_DOCTOR_CENTER")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Field Doctor</button>
            <button onClick={() => onExecuteCommand("QUICK_DIM_CENTER")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Quick Dim</button>
            <button onClick={() => onExecuteCommand("QUANTITY_CENTER")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Quantity / BOQ</button>
            <div className="text-xs text-neutral-500 max-w-xs">44 Lisp được gom theo workflow; không tạo 44 nút rời.</div>
          </div>
        )}
                {activeTab === "SKETCHUP_BRIDGE" && (
          <div className="flex items-center gap-3">
            <button onClick={() => onOpenSketchUpBridge?.()} className="px-4 py-3 rounded-lg bg-violet-500/15 border border-violet-500/40 text-violet-200 font-semibold">Mở CAD ⇄ SketchUp Bridge</button>
            <div className="text-xs text-neutral-500 max-w-md">Clean • CAD→SketchUp • SketchUp→CAD • DXF • AI Layer Mapping • Link ID</div>
          </div>
        )}
        {activeTab === "PUBLISH_CENTER" && (
          <div className="flex items-center gap-3">
            <button onClick={() => onOpenPlotPublish?.()} className="px-4 py-3 rounded-lg bg-sky-500/15 border border-sky-500/40 text-sky-200 font-semibold">Plot / Publish / Sheet Set</button>
            <button onClick={() => onExecuteCommand("QUICK_PLOT_PDF")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Model → PDF</button>
            <button onClick={() => onExecuteCommand("PUBLISH_MULTI_PDF")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Multi-Sheet PDF</button>
            <button onClick={() => onExecuteCommand("SHEETSET_MANAGER")} className="px-3 py-2 rounded bg-neutral-800 hover:bg-neutral-700">Sheet Set</button>
          </div>
        )}
{activeTab === "CAI_DAT" && (
          <div className="flex items-center space-x-3">
            <button
              onClick={onOpenSettings}
              className="flex flex-col items-center w-24 h-14 p-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition"
            >
              <Settings className="w-5 h-5 text-cyan-400" />
              <span className="mt-1 text-[11px] text-center">AI Provider & CAD</span>
            </button>
            <button
              onClick={onOpenAuditModal}
              className="flex flex-col items-center w-28 h-14 p-1 rounded hover:bg-neutral-700 text-amber-300 transition"
            >
              <AlertTriangle className="w-5 h-5 text-amber-400" />
              <span className="mt-1 text-[11px] text-center">Tiêu Chuẩn Dự Án</span>
            </button>
            <button
              onClick={onOpenStandaloneExeBuilder}
              className="flex flex-col items-center w-36 h-14 p-1 rounded bg-gradient-to-r from-emerald-600 to-teal-600 text-white font-semibold transition shadow hover:brightness-110"
              title="Đóng gói và tạo file .EXE chạy độc lập cho Windows (Portable & Setup)"
            >
              <Laptop className="w-5 h-5" />
              <span className="mt-1 text-[11px] text-center">Đóng Gói .EXE Độc Lập</span>
            </button>
            <button
              onClick={onOpenNetPluginExporter}
              className="flex flex-col items-center w-36 h-14 p-1 rounded bg-gradient-to-r from-blue-600 to-cyan-600 text-white font-semibold transition shadow hover:brightness-110"
              title="Tải bộ cài đặt .NET C# Plugin (.bundle / Setup.exe) cho AutoCAD 2023+"
            >
              <Download className="w-5 h-5" />
              <span className="mt-1 text-[11px] text-center">Bộ Cài .NET Plugin</span>
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
