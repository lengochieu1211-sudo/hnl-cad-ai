import React, { useEffect, useRef, useState } from "react";
import {
  Bot,
  Send,
  Sparkles,
  Layers,
  Box,
  Code2,
  Languages,
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Minimize2,
  Maximize2,
  CheckCircle,
  Play,
  Copy,
  Plus,
  Trash2,
  Search,
  Grid,
  Square,
  FileSpreadsheet,
} from "lucide-react";
import {
  CadEntity,
  BlockLibraryItem,
  LispScriptItem,
  TranslationMemoryItem,
  DrawingAuditIssue,
  AICommandPlan,
} from "../../types/cad";
import { getManufacturerCeilingAiContext } from "../../lib/manufacturerCeilingKnowledge";
import { getSmartShopdrawingAiContext } from "../../lib/smartShopdrawingPlatform";

interface HnlPaletteProps {
  isOpen: boolean;
  onToggle: () => void;
  dockPosition: "left" | "right";
  onSetDockPosition: (pos: "left" | "right") => void;
  entities: CadEntity[];
  selectedEntities: CadEntity[];
  blockLibrary: BlockLibraryItem[];
  lispScripts: LispScriptItem[];
  translationMemory: TranslationMemoryItem[];
  auditIssues: DrawingAuditIssue[];
  onExecutePlan: (plan: AICommandPlan) => void;
  onAddBlockToDrawing: (block: BlockLibraryItem) => void;
  onRunLisp: (lisp: LispScriptItem) => void;
  onFixAuditIssue: (issueId: string) => void;
  onTranslateDrawing: (mode: "Bilingual" | "Replace" | "SideBySide", targetLang: string) => void;
  onOpenAutoDetailComposer?: () => void;
  onOpenDrywallStudio?: () => void;
}

export const HnlPalette: React.FC<HnlPaletteProps> = ({
  isOpen,
  onToggle,
  dockPosition,
  onSetDockPosition,
  entities,
  selectedEntities,
  blockLibrary,
  lispScripts,
  translationMemory,
  auditIssues,
  onExecutePlan,
  onAddBlockToDrawing,
  onRunLisp,
  onFixAuditIssue,
  onTranslateDrawing,
  onOpenAutoDetailComposer,
  onOpenDrywallStudio,
}) => {
  const [activeTab, setActiveTab] = useState<"AI_CHAT" | "PRESETS" | "BLOCKS" | "LISP" | "TRANSLATE" | "AUDIT">("AI_CHAT");
  const [chatInput, setChatInput] = useState("");
  const aiInputRef = useRef<HTMLTextAreaElement | null>(null);
  const [isAiLoading, setIsAiLoading] = useState(false);
  const [messages, setMessages] = useState<
    Array<{
      id: string;
      sender: "user" | "ai";
      text: string;
      plan?: AICommandPlan;
      timestamp: string;
    }>
  >([
    {
      id: "msg_welcome",
      sender: "ai",
      text: "Xin chào! Tôi là HNL CAD AI Copilot. Tôi có thể giúp bạn vẽ nhanh (Tường, Trần vách thạch cao PCCC EI30-EI120, MLeader chú thích), thống kê khối lượng, tạo layout tỷ lệ tối ưu, dịch bản vẽ và viết mã AutoLISP.",
      timestamp: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
    },
  ]);

  const [searchBlockQuery, setSearchBlockQuery] = useState("");
  const [selectedBlockCategory, setSelectedBlockCategory] = useState<string>("All");
  const [showAiQuickTools, setShowAiQuickTools] = useState(false);

  useEffect(() => {
    if (!isOpen || activeTab !== "AI_CHAT") return;
    const timer = window.setTimeout(() => aiInputRef.current?.focus(), 80);
    return () => window.clearTimeout(timer);
  }, [isOpen, activeTab]);

  if (!isOpen) {
    return (
      <div
        className="w-9 h-full flex-shrink-0 bg-[#17191c] border-neutral-800 flex items-center justify-center z-30"
        style={{
          borderLeftWidth: dockPosition === "right" ? 1 : 0,
          borderRightWidth: dockPosition === "left" ? 1 : 0,
        }}
      >
        <button
          type="button"
          onClick={onToggle}
          onPointerDown={(e) => e.stopPropagation()}
          className="pointer-events-auto h-[150px] w-8 rounded-md bg-[#25272C] text-cyan-400 shadow-xl border border-neutral-700 hover:bg-neutral-700 transition flex flex-col items-center justify-center gap-2"
          title="Mở HNL CAD AI Palette"
          aria-label="Mở HNL CAD AI Palette"
        >
          <Sparkles className="w-4 h-4 text-cyan-400" />
          <span className="text-[10px] font-bold [writing-mode:vertical-rl] tracking-wider text-cyan-400">HNL AI</span>
        </button>
      </div>
    );
  }

  // Handle AI Chat Submit
  const handleSendMessage = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!chatInput.trim() || isAiLoading) return;

    const userText = chatInput.trim();
    setChatInput("");
    const userMsgId = `usr_${Date.now()}`;
    const timestamp = new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });

    setMessages((prev) => [
      ...prev,
      { id: userMsgId, sender: "user", text: userText, timestamp },
    ]);

    setIsAiLoading(true);

    // Build CAD Context
    let approvedMaterials:any[] = [];
    try { approvedMaterials = JSON.parse(localStorage.getItem("hnl.approvedMaterials.v1") || "[]"); } catch {}
    const cadContext = {
      totalEntities: entities.length,
      selectionCount: selectedEntities.length,
      selectedTypes: selectedEntities.map((e) => e.type),
      wallCount: entities.filter((e) => e.type === "WALL").length,
      lightCount: entities.filter((e) => e.type === "BLOCK_REF" && (e as any).blockName?.includes("DEN")).length,
      activeUnits: "mm",
      ceilingManufacturerKnowledge: getManufacturerCeilingAiContext(),
      smartShopdrawingKnowledge: getSmartShopdrawingAiContext(approvedMaterials),
    };

    try {
      const res = await fetch("/api/ai/plan", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(((window as any).electronNative?.sessionToken) ? { "x-hnl-token": (window as any).electronNative.sessionToken } : {}) },
        body: JSON.stringify({ prompt: userText, cadContext }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) throw new Error(data?.error || data?.reason || `HTTP ${res.status}`);
      const plan: AICommandPlan | undefined = data?.plan;
      if (!plan || typeof plan !== "object") throw new Error("AI server không trả về Command Plan hợp lệ.");

      setMessages((prev) => [
        ...prev,
        {
          id: `ai_${Date.now()}`,
          sender: "ai",
          text: `${data?.provider ? `[${data.provider}${data.model ? ` • ${data.model}` : ""}] ` : ""}${plan.explanation || `Đã phân tích yêu cầu: ${plan.intent || userText}`}${Array.isArray((plan as any).sourceRefs) && (plan as any).sourceRefs.length ? `\nNguồn: ${(plan as any).sourceRefs.map((src:any)=>`${src.title || src.type}${src.revision ? ` (${src.revision})` : ""}`).join(" • ")}` : ""}${(plan as any).certainty ? `\nMức xác minh: ${(plan as any).certainty}` : ""}`,
          plan,
          timestamp: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
        },
      ]);
    } catch (err: any) {
      setMessages((prev) => [
        ...prev,
        {
          id: `ai_err_${Date.now()}`,
          sender: "ai",
          text: `Lỗi kết nối AI: ${err.message}. Đã bật chế độ phân tích Offline an toàn.`,
          timestamp: new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }),
        },
      ]);
    } finally {
      setIsAiLoading(false);
    }
  };

  // Quick Prompt Chips
  const quickPrompts = [
    "Vẽ MLeader chú thích vách thạch cao EI60",
    "Bố trí trần thạch cao chìm phòng khách",
    "Vẽ MLeader cấu tạo trần giật cấp có đèn hắt",
    "Tính tổng diện tích và gắn Field động (m²)",
    "Căn thẳng hàng các đường MLeader",
    "Tạo Layout A3 tỷ lệ tối ưu 1:100",
    "Dịch toàn bộ ghi chú sang tiếng Anh (Song ngữ)",
  ];

  const filteredBlocks = blockLibrary.filter((blk) => {
    const matchesCat = selectedBlockCategory === "All" || blk.category === selectedBlockCategory;
    const matchesQuery =
      !searchBlockQuery ||
      blk.name.toLowerCase().includes(searchBlockQuery.toLowerCase()) ||
      blk.tags.some((t) => t.toLowerCase().includes(searchBlockQuery.toLowerCase()));
    return matchesCat && matchesQuery;
  });

  return (
    <div
      className={`w-[clamp(280px,22vw,340px)] h-full bg-[#1E1F22] border-${
        dockPosition === "right" ? "l" : "r"
      } border-neutral-800 flex flex-col shadow-2xl z-30`}
    >
      {/* Palette Header */}
      <div className="h-11 px-3 bg-[#141517] border-b border-neutral-800 flex items-center justify-between text-xs">
        <div className="flex items-center space-x-2">
          <Sparkles className="w-4 h-4 text-cyan-400" />
          <span className="font-bold text-neutral-200 tracking-wide">HNL CAD PALETTE</span>
        </div>

        <div className="flex items-center space-x-1 text-neutral-400">
          <button
            onClick={() => onSetDockPosition(dockPosition === "right" ? "left" : "right")}
            className="p-1 hover:bg-neutral-800 rounded transition"
            title={`Chuyển sang ${dockPosition === "right" ? "Bên Trái" : "Bên Phải"}`}
          >
            {dockPosition === "right" ? <ChevronLeft className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5" />}
          </button>
          <button
            onClick={onToggle}
            className="p-1 hover:bg-neutral-800 hover:text-white rounded transition"
            title="Thu gọn Palette"
          >
            <Minimize2 className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Palette Tab Bar — 2 rows x 3 columns to avoid horizontal clipping at 280-340px */}
      <div className="grid grid-cols-3 gap-px bg-neutral-800 border-b border-neutral-800 text-[10px] font-medium p-px">
        {([
          ["AI_CHAT", "AI", <Bot className="w-3.5 h-3.5" />],
          ["PRESETS", "Vẽ nhanh", <Square className="w-3.5 h-3.5" />],
          ["BLOCKS", "Block", <Box className="w-3.5 h-3.5" />],
          ["LISP", "Lisp", <Code2 className="w-3.5 h-3.5" />],
          ["TRANSLATE", "Dịch", <Languages className="w-3.5 h-3.5" />],
          ["AUDIT", `Audit ${auditIssues.length}`, <AlertTriangle className="w-3.5 h-3.5" />],
        ] as const).map(([id, label, icon]) => (
          <button
            key={id}
            onClick={() => setActiveTab(id)}
            className={`min-w-0 px-1.5 py-1.5 flex items-center justify-center gap-1 transition ${
              activeTab === id
                ? id === "AUDIT"
                  ? "bg-[#25272C] text-amber-300"
                  : "bg-[#25272C] text-cyan-300"
                : "bg-[#1E1F22] text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800"
            }`}
            title={label}
          >
            {icon}
            <span className="truncate">{label}</span>
          </button>
        ))}
      </div>

      {/* Tab Content Area */}
      <div className={`flex-1 min-h-0 p-3 text-xs ${activeTab === "AI_CHAT" ? "overflow-hidden" : "overflow-y-auto"}`}>
        {/* TAB 1: AI CAD ASSISTANT CHAT */}
        {activeTab === "AI_CHAT" && (
          <div className="flex flex-col h-full min-h-0 space-y-3">
            {/* Live CAD Context Inspector Badge */}
            <div className="shrink-0 bg-[#25272C] px-2.5 py-2 rounded-lg border border-neutral-700/60 text-[10px] flex items-center justify-between gap-2">
              <span className="flex items-center gap-1 text-cyan-400 font-semibold shrink-0">
                <Layers className="w-3.5 h-3.5" />
                CAD
              </span>
              <div className="min-w-0 flex-1 flex items-center justify-end gap-2 text-neutral-300 font-mono">
                <span>Chọn <b className="text-cyan-400">{selectedEntities.length}</b></span>
                <span>Tường <b>{entities.filter((e) => e.type === "WALL").length}</b></span>
                <span>Block <b>{entities.filter((e) => e.type === "BLOCK_REF").length}</b></span>
              </div>
              <span className="w-2 h-2 rounded-full bg-emerald-400 shrink-0" title="CAD Context đang đồng bộ" />
            </div>

            <button
              type="button"
              onClick={() => setShowAiQuickTools((v) => !v)}
              className="shrink-0 w-full flex items-center justify-between px-2.5 py-1.5 rounded bg-neutral-900/70 border border-neutral-800 text-[10px] text-neutral-400 hover:text-neutral-200"
            >
              <span>Công cụ nhanh & gợi ý</span>
              <ChevronRight className={`w-3.5 h-3.5 transition-transform ${showAiQuickTools ? "rotate-90" : ""}`} />
            </button>

            {showAiQuickTools && (
              <div className="shrink-0 max-h-[210px] overflow-y-auto space-y-2 pr-1">
            {/* AI Auto Detail & Layout Composer Quick Launcher */}
            {onOpenAutoDetailComposer && (
              <div
                onClick={onOpenAutoDetailComposer}
                className="p-2.5 rounded-xl bg-gradient-to-r from-cyan-900/40 via-blue-900/30 to-indigo-900/40 border border-cyan-500/40 cursor-pointer hover:border-cyan-400 transition shadow-lg flex items-center justify-between group"
              >
                <div className="flex items-center space-x-2.5">
                  <div className="w-7 h-7 rounded-lg bg-cyan-500/20 border border-cyan-500/40 flex items-center justify-center text-cyan-400 group-hover:scale-105 transition">
                    <Sparkles className="w-3.5 h-3.5" />
                  </div>
                  <div>
                    <div className="font-bold text-[11px] text-white">AI Auto Detail & Layout</div>
                    <div className="text-[9px] text-cyan-300">Trích Detail 1:10 & dàn Sheet A1/A3</div>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-cyan-400 group-hover:translate-x-0.5 transition" />
              </div>
            )}

            {/* Drywall & Ceiling Studio Launcher */}
            {onOpenDrywallStudio && (
              <div
                onClick={onOpenDrywallStudio}
                className="p-2.5 rounded-xl bg-gradient-to-r from-amber-950/50 via-orange-950/40 to-neutral-900 border border-amber-500/40 cursor-pointer hover:border-amber-400 transition shadow-lg flex items-center justify-between group"
              >
                <div className="flex items-center space-x-2.5">
                  <div className="w-7 h-7 rounded-lg bg-amber-500/20 border border-amber-500/40 flex items-center justify-center text-amber-400 group-hover:scale-105 transition">
                    <Layers className="w-3.5 h-3.5" />
                  </div>
                  <div>
                    <div className="font-bold text-[11px] text-white">Shopdrawing Thạch Cao & PCCC</div>
                    <div className="text-[9px] text-amber-300">Cấu tạo EI30-EI120, Deflection & Chia ô</div>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-amber-400 group-hover:translate-x-0.5 transition" />
              </div>
            )}

            {/* Quick Prompt Chips */}
            <div className="flex gap-1.5 overflow-x-auto scrollbar-none pb-1">
              {quickPrompts.slice(0, 3).map((prompt, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    setChatInput(prompt);
                  }}
                  className="shrink-0 whitespace-nowrap px-2 py-1 rounded-full bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-[10px] border border-neutral-700/80 transition"
                >
                  ✨ {prompt}
                </button>
              ))}
            </div>

              </div>
            )}

            {/* Messages Stream */}
            <div className="flex-1 min-h-[72px] overflow-y-auto space-y-3 pr-1 overscroll-contain">
              {messages.map((msg) => (
                <div
                  key={msg.id}
                  className={`flex flex-col space-y-1 ${
                    msg.sender === "user" ? "items-end" : "items-start"
                  }`}
                >
                  <div className="text-[10px] text-neutral-500">{msg.timestamp}</div>
                  <div
                    className={`p-2.5 rounded-xl max-w-[90%] leading-relaxed ${
                      msg.sender === "user"
                        ? "bg-cyan-600 text-white rounded-br-none"
                        : "bg-[#25272C] text-neutral-200 border border-neutral-700/80 rounded-bl-none shadow"
                    }`}
                  >
                    <p>{msg.text}</p>

                    {/* AI Structured Command Plan Preview */}
                    {msg.plan && (
                      <div className="mt-2.5 p-2 bg-neutral-900/90 rounded-lg border border-neutral-700 text-[11px] space-y-2">
                        <div className="flex items-center justify-between font-semibold">
                          <span className="text-cyan-400 font-mono">
                            {msg.plan.intent}
                          </span>
                          <span
                            className={`px-1.5 py-0.5 rounded text-[9px] font-bold ${
                              msg.plan.isDestructive
                                ? "bg-red-500/20 text-red-400 border border-red-500/40"
                                : "bg-emerald-500/20 text-emerald-400 border border-emerald-500/40"
                            }`}
                          >
                            {msg.plan.isDestructive ? "DESTRUCTIVE (CẦN XÁC NHẬN)" : "SAFE COMMAND"}
                          </span>
                        </div>

                        {/* Step list */}
                        <div className="space-y-1 text-neutral-400 text-[10px]">
                          {msg.plan.steps.map((step, sIdx) => (
                            <div key={sIdx} className="flex items-start space-x-1.5">
                              <span className="text-cyan-400 font-bold">{step.stepIndex}.</span>
                              <span className="text-neutral-300 font-mono">[{step.command}]</span>
                              <span>{step.description}</span>
                            </div>
                          ))}
                        </div>

                        {/* Confirm Execution Button */}
                        <button
                          onClick={() => onExecutePlan(msg.plan!)}
                          className="w-full mt-2 py-1.5 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-semibold rounded-md flex items-center justify-center space-x-1.5 shadow transition"
                        >
                          <Play className="w-3.5 h-3.5 fill-current" />
                          <span>Thực thi vào CAD (Execute)</span>
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              ))}
              {isAiLoading && (
                <div className="flex items-center space-x-2 text-cyan-400 text-[11px] italic animate-pulse">
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>AI đang phân tích CAD Context & lập kế hoạch tác vụ...</span>
                </div>
              )}
            </div>

            {/* Chat Input Box — isolate it from CAD keyboard/mouse handlers */}
            <form
              onSubmit={handleSendMessage}
              onPointerDown={(e) => e.stopPropagation()}
              onMouseDown={(e) => e.stopPropagation()}
              className="sticky bottom-0 z-20 mt-auto shrink-0 flex items-end space-x-1.5 border-t border-neutral-800 pt-2 bg-[#1E1F22]"
            >
              <textarea
                ref={aiInputRef}
                rows={2}
                value={chatInput}
                onChange={(e) => setChatInput(e.target.value)}
                onPointerDown={(e) => e.stopPropagation()}
                onMouseDown={(e) => e.stopPropagation()}
                onKeyDown={(e) => {
                  e.stopPropagation();
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    void handleSendMessage();
                  }
                }}
                placeholder="Nhập yêu cầu AI… Enter = gửi, Shift+Enter = xuống dòng"
                className="min-w-0 flex-1 resize-none select-text bg-[#25272C] text-neutral-100 placeholder-neutral-500 px-3 py-2 rounded-lg border border-neutral-700 text-xs leading-4 focus:outline-none focus:ring-1 focus:ring-cyan-500 focus:border-cyan-500"
                aria-label="Nhập yêu cầu HNL CAD AI"
                data-hnl-ai-input="true"
              />
              <button
                type="submit"
                disabled={isAiLoading || !chatInput.trim()}
                className="shrink-0 p-2.5 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-lg transition"
                title="Gửi AI"
              >
                <Send className="w-4 h-4" />
              </button>
            </form>
          </div>
        )}

        {/* TAB 2: SMART DRAW PRESETS */}
        {activeTab === "PRESETS" && (
          <div className="space-y-3">
            <span className="text-[11px] font-semibold text-neutral-400 uppercase tracking-wider">
              Preset Vẽ Nhanh HNL CAD (Ưu Tiên MLeader)
            </span>
            <div className="grid grid-cols-2 gap-2">
              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Vẽ Multileader chú thích cấu tạo vách PCCC",
                    actionType: "DRAW_MLEADER",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Tạo Multileader chú thích cấu tạo vách thạch cao chống cháy EI60",
                    steps: [{ stepIndex: 1, command: "HNL_MLEADER_MATERIAL", description: "Vẽ MLeader vật liệu EI60" }],
                  })
                }
                className="p-2.5 bg-cyan-950/40 hover:bg-cyan-900/50 rounded-lg border border-cyan-500/40 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-cyan-400">MLeader PCCC EI60</span>
                <span className="text-[10px] text-cyan-200/80">Khung C75 + Rockwool + Gyproc Fire-Stop</span>
              </div>

              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Căn thẳng hàng các đường Multileader",
                    actionType: "ALIGN_MLEADER",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Căn thẳng hàng trục đứng và vai gập cho tất cả MLeader",
                    steps: [{ stepIndex: 1, command: "HNL_MLEADER_ALIGN", description: "Căn hàng MLeader" }],
                  })
                }
                className="p-2.5 bg-emerald-950/40 hover:bg-emerald-900/50 rounded-lg border border-emerald-500/40 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-emerald-400">Căn Hàng MLeader</span>
                <span className="text-[10px] text-emerald-200/80">MLEADERALIGN thẳng hàng chuẩn Shop</span>
              </div>

              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Bố trí trần thạch cao chìm",
                    actionType: "DRAW_CEILING",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Khung xương @800x400 + Ty @1000",
                    steps: [{ stepIndex: 1, command: "HNL_CEILING", description: "Bố trí trần chìm" }],
                  })
                }
                className="p-2.5 bg-[#25272C] hover:bg-neutral-700 rounded-lg border border-neutral-700 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-amber-400">Trần Thạch Cao</span>
                <span className="text-[10px] text-neutral-400">Xương chính, phụ, ty treo</span>
              </div>

              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Vẽ tường 100mm",
                    actionType: "DRAW_WALL",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Tạo tường 100 tự bo góc và hatch",
                    steps: [{ stepIndex: 1, command: "HNL_WALL_100", description: "Vẽ tường 100" }],
                  })
                }
                className="p-2.5 bg-[#25272C] hover:bg-neutral-700 rounded-lg border border-neutral-700 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-cyan-300">Tường 100</span>
                <span className="text-[10px] text-neutral-400">Offset ±50mm, layer KT_TUONG</span>
              </div>

              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Tính diện tích và dán Field",
                    actionType: "CALC_AREA",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Tính m² và gắn Field tự cập nhật",
                    steps: [{ stepIndex: 1, command: "HNL_AREA_LABEL", description: "Gắn Field diện tích" }],
                  })
                }
                className="p-2.5 bg-[#25272C] hover:bg-neutral-700 rounded-lg border border-neutral-700 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-purple-400">Nhãn Diện Tích</span>
                <span className="text-[10px] text-neutral-400">A = ##.## m² động</span>
              </div>

              <div
                onClick={() =>
                  onExecutePlan({
                    intent: "Chuyển Text rời rạc sang MLeader",
                    actionType: "CONVERT_MLEADER",
                    isDestructive: false,
                    confidence: 1,
                    explanation: "Gom Text và Leader thành 1 đối tượng MLEADER",
                    steps: [{ stepIndex: 1, command: "HNL_TEXT2MLEADER", description: "Text -> MLeader" }],
                  })
                }
                className="p-2.5 bg-[#25272C] hover:bg-neutral-700 rounded-lg border border-neutral-700 cursor-pointer transition flex flex-col space-y-1"
              >
                <span className="font-bold text-rose-400">Text -&gt; MLeader</span>
                <span className="text-[10px] text-neutral-400">Gộp thành Multileader</span>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: BLOCK LIBRARY MANAGER */}
        {activeTab === "BLOCKS" && (
          <div className="space-y-3">
            {/* Search and Category Filter */}
            <div className="space-y-2">
              <div className="relative">
                <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-neutral-400" />
                <input
                  type="text"
                  placeholder="Tìm block (vd: đèn, cửa, cột)..."
                  value={searchBlockQuery}
                  onChange={(e) => setSearchBlockQuery(e.target.value)}
                  className="w-full bg-[#25272C] text-neutral-200 placeholder-neutral-500 pl-8 pr-3 py-1.5 rounded-lg border border-neutral-700 text-xs focus:outline-none focus:border-cyan-500"
                />
              </div>

              <div className="flex items-center space-x-1 overflow-x-auto pb-1 scrollbar-none text-[10px]">
                {["All", "MEP", "Kiến trúc", "Kết cấu", "Title Block", "Section"].map((cat) => (
                  <button
                    key={cat}
                    onClick={() => setSelectedBlockCategory(cat)}
                    className={`px-2 py-1 rounded whitespace-nowrap transition ${
                      selectedBlockCategory === cat
                        ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40"
                        : "bg-neutral-800 text-neutral-400 hover:text-neutral-200"
                    }`}
                  >
                    {cat}
                  </button>
                ))}
              </div>
            </div>

            {/* Block Items Grid */}
            <div className="space-y-2">
              {filteredBlocks.map((blk) => (
                <div
                  key={blk.id}
                  className="p-2.5 bg-[#25272C] hover:bg-neutral-700/80 rounded-lg border border-neutral-700/80 flex items-center justify-between transition group"
                >
                  <div className="flex items-center space-x-2.5">
                    <div className="w-8 h-8 rounded bg-neutral-800 border border-neutral-600 flex items-center justify-center text-cyan-400">
                      <Box className="w-4 h-4" />
                    </div>
                    <div>
                      <div className="font-semibold text-neutral-200">{blk.name}</div>
                      <div className="text-[10px] text-neutral-400 flex items-center space-x-1.5">
                        <span className="text-cyan-400">{blk.category}</span>
                        <span>•</span>
                        <span>{blk.isDynamic ? "Dynamic" : "Static"}</span>
                      </div>
                    </div>
                  </div>

                  <button
                    onClick={() => onAddBlockToDrawing(blk)}
                    className="px-2.5 py-1 rounded bg-cyan-600 hover:bg-cyan-500 text-white font-medium text-[11px] shadow transition"
                  >
                    Chèn
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* TAB 4: LISP MANAGER */}
        {activeTab === "LISP" && (
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-semibold text-neutral-400 uppercase">
                Tập Lệnh AutoLISP (.lsp)
              </span>
              <span className="text-[10px] text-cyan-400">{lispScripts.length} lệnh đã nạp</span>
            </div>

            <div className="space-y-2">
              {lispScripts.map((lsp) => (
                <div
                  key={lsp.id}
                  className="p-2.5 bg-[#25272C] rounded-lg border border-neutral-700 flex flex-col space-y-2"
                >
                  <div className="flex items-center justify-between">
                    <div className="font-bold text-cyan-400 font-mono">{lsp.commandName}</div>
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400">
                      {lsp.category}
                    </span>
                  </div>
                  <p className="text-[11px] text-neutral-300">{lsp.description}</p>

                  <div className="flex items-center space-x-2 pt-1 border-t border-neutral-800">
                    <button
                      onClick={() => onRunLisp(lsp)}
                      className="flex-1 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-[11px] flex items-center justify-center space-x-1 transition"
                    >
                      <Play className="w-3 h-3 fill-current" />
                      <span>Chạy lệnh</span>
                    </button>
                    <button
                      onClick={() => {
                        navigator.clipboard.writeText(lsp.code);
                        alert(`Đã sao chép mã ${lsp.commandName} vào Clipboard!`);
                      }}
                      className="p-1.5 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-300 transition"
                      title="Sao chép code AutoLISP"
                    >
                      <Copy className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* TAB 5: DRAWING TRANSLATOR & MEMORY */}
        {activeTab === "TRANSLATE" && (
          <div className="space-y-3">
            <span className="text-[11px] font-semibold text-neutral-400 uppercase">
              Dịch Thuật Bản Vẽ Kỹ Thuật
            </span>

            <div className="p-3 bg-[#25272C] rounded-lg border border-neutral-700 space-y-3">
              <div className="text-[11px] text-neutral-300">
                Tự động dịch Text, MText, MLeader, Attribute theo đúng chuẩn chuyên ngành Kiến trúc, Kết cấu, MEP.
              </div>

              <div className="space-y-1.5">
                <button
                  onClick={() => onTranslateDrawing("Bilingual", "en")}
                  className="w-full py-1.5 bg-cyan-600 hover:bg-cyan-500 text-white font-semibold rounded transition text-center"
                >
                  Dịch Song Ngữ (Việt trên - Anh dưới)
                </button>
                <button
                  onClick={() => onTranslateDrawing("Replace", "en")}
                  className="w-full py-1.5 bg-neutral-800 hover:bg-neutral-700 text-neutral-200 rounded transition text-center"
                >
                  Thay thế toàn bộ bằng Tiếng Anh
                </button>
                <button
                  onClick={() => onTranslateDrawing("SideBySide", "zh")}
                  className="w-full py-1.5 bg-neutral-800 hover:bg-neutral-700 text-neutral-200 rounded transition text-center"
                >
                  Dịch Việt - Trung (Side by Side)
                </button>
              </div>
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-[11px] text-neutral-400 font-semibold">
                <span>TRANSLATION MEMORY ({translationMemory.length})</span>
              </div>
              <div className="max-h-48 overflow-y-auto space-y-1 text-[10px]">
                {translationMemory.map((item, idx) => (
                  <div key={idx} className="p-1.5 bg-neutral-800/80 rounded border border-neutral-700/60">
                    <div className="text-neutral-400">{item.original}</div>
                    <div className="text-cyan-400 font-semibold">→ {item.translated}</div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* TAB 6: CAD AUDIT & DRAWING ISSUES */}
        {activeTab === "AUDIT" && (
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-semibold text-neutral-400 uppercase">
                Phát Hiện Lỗi Bản Vẽ
              </span>
              <span className="text-[10px] text-amber-400 font-bold">{auditIssues.length} vấn đề</span>
            </div>

            <div className="space-y-2">
              {auditIssues.map((issue) => (
                <div
                  key={issue.id}
                  className={`p-2.5 rounded-lg border flex flex-col space-y-1.5 ${
                    issue.type === "ERROR"
                      ? "bg-red-500/10 border-red-500/30 text-red-200"
                      : issue.type === "WARNING"
                      ? "bg-amber-500/10 border-amber-500/30 text-amber-200"
                      : "bg-blue-500/10 border-blue-500/30 text-blue-200"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold">{issue.title}</span>
                    <span className="text-[9px] font-mono px-1 py-0.5 rounded bg-black/40">
                      {issue.category}
                    </span>
                  </div>
                  <p className="text-[10px] opacity-80">{issue.description}</p>

                  {issue.canAutoFix && (
                    <button
                      onClick={() => onFixAuditIssue(issue.id)}
                      className="mt-1 py-1 px-2.5 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/40 text-[10px] font-semibold flex items-center justify-center space-x-1 self-end transition"
                    >
                      <CheckCircle className="w-3 h-3" />
                      <span>Tự động sửa lỗi (Auto-Fix)</span>
                    </button>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
