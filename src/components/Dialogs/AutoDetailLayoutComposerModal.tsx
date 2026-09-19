import React, { useState, useEffect, useRef, useMemo } from "react";
import {
  Wand2,
  Layers,
  Sparkles,
  Maximize2,
  CheckCircle2,
  AlertTriangle,
  FileImage,
  Upload,
  Sliders,
  Eye,
  Check,
  X,
  Plus,
  Trash2,
  ArrowRight,
  Move,
  Lock,
  Grid,
  ChevronRight,
  RefreshCw,
  Award,
  Zap,
  Layout,
  Scissors,
  BookOpen,
  FileText,
  Search,
} from "lucide-react";
import {
  CadEntity,
  CadLayer,
  CadLayout,
  CadViewport,
  AutoDetailProposal,
  AutoSectionProposal,
  DetailTemplateItem,
  LayoutQualityMetrics,
  ReferenceLayoutAnalysis,
  SheetSetProposal,
} from "../../types/cad";
import {
  STANDARD_DETAIL_TEMPLATES,
  REFERENCE_LAYOUT_STYLES,
  analyzeCadComplexity,
  scoreLayoutQuality,
  generateAutoSheetSet,
  createDetailAndSectionCalloutEntities,
} from "../../lib/detailLayoutComposerEngine";

interface AutoDetailLayoutComposerModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
  layers: CadLayer[];
  onApplyComposerToDrawing: (
    newLayouts: CadLayout[],
    newViewports: CadViewport[],
    newCalloutEntities: CadEntity[]
  ) => void;
}

export const AutoDetailLayoutComposerModal: React.FC<AutoDetailLayoutComposerModalProps> = ({
  isOpen,
  onClose,
  entities,
  layers,
  onApplyComposerToDrawing,
}) => {
  const [activeTab, setActiveTab] = useState<"SCANNER" | "REFERENCE" | "ANNOTATION" | "PREVIEW">("SCANNER");

  // Proposed Details & Sections State
  const [proposedDetails, setProposedDetails] = useState<AutoDetailProposal[]>([]);
  const [proposedSections, setProposedSections] = useState<AutoSectionProposal[]>([]);
  const [selectedDetailId, setSelectedDetailId] = useState<string | null>(null);

  // Layout & Sheet Config
  const [paperSize, setPaperSize] = useState<"A0" | "A1" | "A2" | "A3" | "A4">("A1");
  const [orientation, setOrientation] = useState<"Landscape" | "Portrait">("Landscape");
  const [selectedStyleIndex, setSelectedStyleIndex] = useState<number>(0);
  const [autoSheetSplitEnabled, setAutoSheetSplitEnabled] = useState<boolean>(true);

  // Smart Annotation Config
  const [autoDimsEnabled, setAutoDimsEnabled] = useState(true);
  const [dimHierarchy, setDimHierarchy] = useState({
    overall: true,
    grid: true,
    openings: true,
    details: true,
  });
  const [antiCollisionEnabled, setAntiCollisionEnabled] = useState(true);

  // Sheet Preview Interactive Viewports State
  const [previewViewports, setPreviewViewports] = useState<
    Array<{
      id: string;
      title: string;
      type: "MAIN_PLAN" | "SECTION" | "DETAIL" | "NOTES";
      scale: string;
      x: number;
      y: number;
      width: number;
      height: number;
      detailNumber?: string;
    }>
  >([]);

  const [activeSheetIndex, setActiveSheetIndex] = useState(0);

  // Initialize or scan when modal opens
  useEffect(() => {
    if (isOpen) {
      const scanResult = analyzeCadComplexity(entities);
      setProposedDetails(scanResult.proposedDetails);
      setProposedSections(scanResult.proposedSections);
      if (scanResult.proposedDetails.length > 0) {
        setSelectedDetailId(scanResult.proposedDetails[0].id);
      }
    }
  }, [isOpen, entities]);

  // Compute paper dimensions in mm
  const paperDimensions = useMemo(() => {
    const dims: Record<string, { w: number; h: number }> = {
      A0: { w: 1189, h: 841 },
      A1: { w: 841, h: 594 },
      A2: { w: 594, h: 420 },
      A3: { w: 420, h: 297 },
      A4: { w: 297, h: 210 },
    };
    const base = dims[paperSize] || dims.A1;
    return orientation === "Landscape" ? { widthMm: base.w, heightMm: base.h } : { widthMm: base.h, heightMm: base.w };
  }, [paperSize, orientation]);

  // Generate Sheet Sets or Viewports
  const sheetSets = useMemo<SheetSetProposal[]>(() => {
    return generateAutoSheetSet(paperSize, orientation, proposedDetails, proposedSections);
  }, [paperSize, orientation, proposedDetails, proposedSections]);

  // Update preview viewports when active sheet changes or proposals change
  useEffect(() => {
    if (sheetSets.length > 0 && sheetSets[activeSheetIndex]) {
      const currentSheet = sheetSets[activeSheetIndex];
      const vps = currentSheet.viewports.map((v, i) => ({
        id: `vp_prev_${i}`,
        title: v.title,
        type: v.type,
        scale: v.scale,
        x: v.paperPosition.x,
        y: v.paperPosition.y,
        width: v.paperPosition.width,
        height: v.paperPosition.height,
        detailNumber: v.detailNumber,
      }));
      setPreviewViewports(vps);
    }
  }, [sheetSets, activeSheetIndex]);

  // Real-time Layout Quality Score calculation
  const qualityMetrics = useMemo<LayoutQualityMetrics>(() => {
    return scoreLayoutQuality(
      paperSize,
      previewViewports,
      paperDimensions.widthMm,
      paperDimensions.heightMm,
      10
    );
  }, [paperSize, previewViewports, paperDimensions]);

  // Toggle detail proposal selection
  const handleToggleDetail = (id: string) => {
    setProposedDetails((prev) =>
      prev.map((d) => (d.id === id ? { ...d, isSelected: !d.isSelected } : d))
    );
  };

  // Toggle section proposal selection
  const handleToggleSection = (id: string) => {
    setProposedSections((prev) =>
      prev.map((s) => (s.id === id ? { ...s, isSelected: !s.isSelected } : s))
    );
  };

  // Change scale of detail
  const handleDetailScaleChange = (id: string, scale: string) => {
    setProposedDetails((prev) =>
      prev.map((d) => (d.id === id ? { ...d, recommendedScale: scale } : d))
    );
  };

  // Change extraction type (extract geometry vs generate from standard template)
  const handleDetailTypeChange = (id: string, extType: "EXTRACT_GEOMETRY" | "GENERATE_FROM_TEMPLATE", templateId?: string) => {
    const tmpl = STANDARD_DETAIL_TEMPLATES.find((t) => t.id === templateId);
    setProposedDetails((prev) =>
      prev.map((d) =>
        d.id === id
          ? {
              ...d,
              extractionType: extType,
              templateId: templateId || d.templateId,
              templateName: tmpl ? tmpl.name : d.templateName,
            }
          : d
      )
    );
  };

  // Auto Optimize Layout button action
  const handleAutoOptimize = () => {
    // Mathematically re-arrange viewports neatly with balanced margins and alignments
    const margin = 20;
    const w = paperDimensions.widthMm;
    const h = paperDimensions.heightMm;

    if (paperSize === "A1") {
      const mainW = Math.round((w - margin * 3) * 0.6);
      const rightW = Math.round((w - margin * 3) * 0.4);
      const topH = Math.round((h - margin * 3) * 0.68);
      const btmH = Math.round((h - margin * 3) * 0.32);

      const optimized = previewViewports.map((vp) => {
        if (vp.type === "MAIN_PLAN") {
          return { ...vp, x: margin, y: margin, width: mainW, height: topH };
        } else if (vp.type === "SECTION") {
          return { ...vp, x: margin + mainW + margin, y: margin, width: rightW, height: Math.round(topH / 2 - 10) };
        } else if (vp.type === "DETAIL") {
          const detailIndex = previewViewports.filter((v) => v.type === "DETAIL").indexOf(vp);
          const detW = Math.round((w - margin * 4) / 4);
          return {
            ...vp,
            x: margin + detailIndex * (detW + 10),
            y: margin + topH + 15,
            width: detW,
            height: btmH,
          };
        }
        return vp;
      });
      setPreviewViewports(optimized);
    } else {
      // A3 layout optimization
      const halfW = Math.round((w - margin * 3) / 2);
      const halfH = Math.round((h - margin * 3) / 2);
      const optimized = previewViewports.map((vp, idx) => {
        if (vp.type === "MAIN_PLAN") {
          return { ...vp, x: margin, y: margin, width: halfW + 40, height: h - margin * 2 };
        } else {
          return {
            ...vp,
            x: margin + halfW + 50,
            y: margin + idx * (halfH - 10),
            width: halfW - 50,
            height: halfH - 20,
          };
        }
      });
      setPreviewViewports(optimized);
    }
  };

  // Apply Everything To Drawing (Execute)
  const handleApplyToAutocad = () => {
    const generatedLayouts: CadLayout[] = [];
    const generatedViewports: CadViewport[] = [];

    sheetSets.forEach((sheet) => {
      const layoutId = `layout_ai_${sheet.sheetNumber}_${Date.now()}`;
      const layout: CadLayout = {
        id: layoutId,
        name: `${sheet.sheetNumber}_${sheet.sheetTitle.replace(/[^a-zA-Z0-9]/g, "_").substring(0, 15)}`,
        paperSize: sheet.paperSize,
        orientation: sheet.orientation,
        widthMm: paperDimensions.widthMm,
        heightMm: paperDimensions.heightMm,
        marginMm: 10,
        titleBlockBlockName: `HNL_TITLE_${sheet.paperSize}`,
        drawingName: sheet.sheetTitle,
        drawingNo: sheet.sheetNumber,
        scale: sheet.viewports[0]?.scale || "1:50",
        status: "READY",
      };
      generatedLayouts.push(layout);

      sheet.viewports.forEach((vp, vpIdx) => {
        const viewportEntity: CadViewport = {
          id: `vp_ai_${sheet.sheetNumber}_${vpIdx}_${Date.now()}`,
          type: "VIEWPORT",
          layoutName: layout.name,
          x: vp.paperPosition.x,
          y: vp.paperPosition.y,
          width: vp.paperPosition.width,
          height: vp.paperPosition.height,
          modelCenter: vp.modelCenter,
          scale: vp.scale,
          scaleFactor: vp.scale === "1:10" ? 0.1 : vp.scale === "1:20" ? 0.05 : vp.scale === "1:25" ? 0.04 : 0.02,
          locked: true,
          title: vp.title,
          detailNumber: vp.detailNumber,
        };
        generatedViewports.push(viewportEntity);
      });
    });

    // Generate callouts on Model Space
    const calloutEntities = createDetailAndSectionCalloutEntities(
      proposedDetails,
      proposedSections,
      sheetSets[0]?.sheetNumber || "A-101"
    );

    onApplyComposerToDrawing(generatedLayouts, generatedViewports, calloutEntities);
    onClose();
  };

  if (!isOpen) return null;

  const currentSelectedDetail = proposedDetails.find((d) => d.id === selectedDetailId);

  return (
    <div className="fixed inset-0 z-50 bg-black/80 backdrop-blur-md flex items-center justify-center p-3 sm:p-6 select-none animate-in fade-in duration-200">
      <div className="w-full max-w-6xl h-[92vh] bg-[#1A1B1E] rounded-2xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col font-sans text-neutral-200">
        {/* Modal Top Header */}
        <div className="h-16 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3.5">
            <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-600 flex items-center justify-center text-white shadow-lg shadow-cyan-900/30">
              <Sparkles className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <h1 className="font-bold text-white text-base tracking-wide">
                  AI AUTO DETAIL & LAYOUT COMPOSER
                </h1>
                <span className="px-2 py-0.5 rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/40 text-[10px] font-semibold">
                  Shopdrawing Engine Pro
                </span>
              </div>
              <p className="text-xs text-neutral-400">
                Tự động nhận diện cấu tạo phức tạp, trích Detail 1:10/1:20, đề xuất Mặt cắt và bố trí Sheet hoàn chỉnh
              </p>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            {/* Quick Layout Score Pill */}
            <div className="px-3 py-1.5 rounded-xl bg-[#22242A] border border-neutral-700 flex items-center space-x-2">
              <Award className="w-4 h-4 text-amber-400" />
              <span className="text-xs text-neutral-300">Điểm bố cục:</span>
              <span className={`font-bold text-xs ${qualityMetrics.overallScore >= 90 ? "text-emerald-400" : "text-amber-400"}`}>
                {qualityMetrics.overallScore}/100
              </span>
            </div>

            <button
              onClick={onClose}
              className="p-2 rounded-xl hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Studio Sub-Navigation Tabs */}
        <div className="h-11 px-6 bg-[#18191C] border-b border-neutral-800 flex items-center justify-between text-xs">
          <div className="flex items-center space-x-1">
            <button
              onClick={() => setActiveTab("SCANNER")}
              className={`px-4 py-2 rounded-lg font-semibold flex items-center space-x-2 transition ${
                activeTab === "SCANNER"
                  ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40 shadow"
                  : "text-neutral-400 hover:text-white hover:bg-neutral-800"
              }`}
            >
              <Search className="w-3.5 h-3.5" />
              <span>1. Tự trích Chi tiết & Mặt cắt ({proposedDetails.filter((d) => d.isSelected).length} Detail, {proposedSections.filter((s) => s.isSelected).length} Section)</span>
            </button>

            <button
              onClick={() => setActiveTab("REFERENCE")}
              className={`px-4 py-2 rounded-lg font-semibold flex items-center space-x-2 transition ${
                activeTab === "REFERENCE"
                  ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40 shadow"
                  : "text-neutral-400 hover:text-white hover:bg-neutral-800"
              }`}
            >
              <FileImage className="w-3.5 h-3.5" />
              <span>2. Bố cục từ ảnh mẫu (Reference Layout)</span>
            </button>

            <button
              onClick={() => setActiveTab("ANNOTATION")}
              className={`px-4 py-2 rounded-lg font-semibold flex items-center space-x-2 transition ${
                activeTab === "ANNOTATION"
                  ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40 shadow"
                  : "text-neutral-400 hover:text-white hover:bg-neutral-800"
              }`}
            >
              <Sliders className="w-3.5 h-3.5" />
              <span>3. Smart Annotation & Dim</span>
            </button>

            <button
              onClick={() => setActiveTab("PREVIEW")}
              className={`px-4 py-2 rounded-lg font-semibold flex items-center space-x-2 transition ${
                activeTab === "PREVIEW"
                  ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40 shadow"
                  : "text-neutral-400 hover:text-white hover:bg-neutral-800"
              }`}
            >
              <Eye className="w-3.5 h-3.5" />
              <span>4. Xem trước toàn Sheet & Chấm điểm Layout</span>
            </button>
          </div>

          <div className="flex items-center space-x-2 text-[11px] text-neutral-400">
            <span>Khổ giấy:</span>
            <select
              value={paperSize}
              onChange={(e) => setPaperSize(e.target.value as any)}
              className="bg-[#22242A] text-neutral-200 px-2 py-1 rounded border border-neutral-700 font-bold focus:outline-none"
            >
              <option value="A1">A1 (841 x 594 mm) - Khuyên dùng</option>
              <option value="A0">A0 (1189 x 841 mm)</option>
              <option value="A2">A2 (594 x 420 mm)</option>
              <option value="A3">A3 (420 x 297 mm)</option>
              <option value="A4">A4 (297 x 210 mm)</option>
            </select>
          </div>
        </div>

        {/* Tab 1: AI Auto Detail & Section Scanner */}
        {activeTab === "SCANNER" && (
          <div className="flex-1 flex overflow-hidden">
            {/* Left Column: Proposals List */}
            <div className="w-80 bg-[#161719] border-r border-neutral-800 p-4 flex flex-col space-y-4 overflow-y-auto">
              <div className="flex items-center justify-between">
                <span className="font-bold text-xs text-neutral-200 uppercase tracking-wider">
                  VÙNG TRÍCH CHI TIẾT ĐỀ XUẤT ({proposedDetails.length})
                </span>
                <span className="text-[10px] text-cyan-400 font-mono">Độ phức tạp: 93%</span>
              </div>

              {/* Details List */}
              <div className="space-y-2.5">
                {proposedDetails.map((det) => {
                  const isSelected = det.isSelected;
                  const isHighlighted = det.id === selectedDetailId;
                  return (
                    <div
                      key={det.id}
                      onClick={() => setSelectedDetailId(det.id)}
                      className={`p-3 rounded-xl border transition cursor-pointer flex flex-col space-y-2 ${
                        isHighlighted
                          ? "bg-cyan-500/10 border-cyan-500 text-white shadow-md"
                          : isSelected
                          ? "bg-[#202226] border-neutral-700 text-neutral-300 hover:border-neutral-600"
                          : "bg-[#1A1B1E] border-neutral-800 text-neutral-500 opacity-60"
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-2">
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={(e) => {
                              e.stopPropagation();
                              handleToggleDetail(det.id);
                            }}
                            className="rounded text-cyan-500 focus:ring-0 cursor-pointer"
                          />
                          <span className="font-bold text-xs text-cyan-400">DETAIL {det.detailNumber}</span>
                        </div>
                        <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-black/40 text-amber-300">
                          {det.recommendedScale}
                        </span>
                      </div>

                      <div className="text-xs font-semibold">{det.title}</div>
                      <div className="text-[11px] text-neutral-400 leading-tight">{det.explanation}</div>

                      <div className="flex items-center justify-between pt-1 border-t border-neutral-800/80 text-[10px]">
                        <span className="text-neutral-400">
                          Loại: <strong>{det.extractionType === "GENERATE_FROM_TEMPLATE" ? "AI Dựng mẫu" : "Trích hình học"}</strong>
                        </span>
                        <span className="text-emerald-400">Độ phức tạp: {det.complexityScore}%</span>
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Section Proposals */}
              <div className="pt-3 border-t border-neutral-800 space-y-2">
                <span className="font-bold text-xs text-neutral-200 uppercase tracking-wider">
                  MẶT CẮT ĐỀ XUẤT ({proposedSections.length})
                </span>

                <div className="space-y-2">
                  {proposedSections.map((sec) => (
                    <div
                      key={sec.id}
                      className={`p-2.5 rounded-xl border flex items-center justify-between ${
                        sec.isSelected ? "bg-[#202226] border-neutral-700 text-neutral-200" : "bg-[#1A1B1E] border-neutral-800 text-neutral-500 opacity-60"
                      }`}
                    >
                      <div className="flex items-center space-x-2">
                        <input
                          type="checkbox"
                          checked={sec.isSelected}
                          onChange={() => handleToggleSection(sec.id)}
                          className="rounded text-cyan-500 focus:ring-0 cursor-pointer"
                        />
                        <div>
                          <div className="font-bold text-xs text-amber-400">SECTION {sec.sectionName}</div>
                          <div className="text-[11px] text-neutral-400">{sec.title} (TL {sec.recommendedScale})</div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Right Column: Interactive Detail Inspector & Geometry Preview */}
            <div className="flex-1 bg-[#1A1B1E] p-6 flex flex-col space-y-5 overflow-y-auto">
              {currentSelectedDetail ? (
                <div className="space-y-5">
                  <div className="p-5 rounded-2xl bg-[#202226] border border-neutral-700 flex flex-col space-y-4 shadow-xl">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-3">
                        <div className="w-8 h-8 rounded-lg bg-cyan-500/20 border border-cyan-500/40 flex items-center justify-center text-cyan-400 font-bold text-xs">
                          {currentSelectedDetail.detailNumber}
                        </div>
                        <div>
                          <h3 className="font-bold text-sm text-white">{currentSelectedDetail.title}</h3>
                          <p className="text-xs text-neutral-400">
                            Tọa độ tâm Model: X={Math.round(currentSelectedDetail.center.x)}, Y={Math.round(currentSelectedDetail.center.y)} mm
                          </p>
                        </div>
                      </div>

                      <div className="flex items-center space-x-2">
                        <span className="text-xs text-neutral-400">Tỷ lệ trích:</span>
                        <select
                          value={currentSelectedDetail.recommendedScale}
                          onChange={(e) => handleDetailScaleChange(currentSelectedDetail.id, e.target.value)}
                          className="bg-[#161719] text-cyan-300 font-bold px-3 py-1.5 rounded-lg border border-neutral-700 text-xs"
                        >
                          <option value="1:5">1:5 (Rất chi tiết)</option>
                          <option value="1:10">1:10 (Tiêu chuẩn Shopdrawing)</option>
                          <option value="1:20">1:20 (Chi tiết vừa)</option>
                          <option value="1:25">1:25 (Chi tiết bộ phận)</option>
                        </select>
                      </div>
                    </div>

                    {/* Mode Choice: Extract geometry vs Generate from template */}
                    <div className="grid grid-cols-2 gap-3 pt-2">
                      <div
                        onClick={() => handleDetailTypeChange(currentSelectedDetail.id, "EXTRACT_GEOMETRY")}
                        className={`p-3 rounded-xl border cursor-pointer transition ${
                          currentSelectedDetail.extractionType === "EXTRACT_GEOMETRY"
                            ? "bg-cyan-500/20 border-cyan-500 text-white"
                            : "bg-[#161719] border-neutral-800 text-neutral-400 hover:border-neutral-700"
                        }`}
                      >
                        <div className="font-bold text-xs text-cyan-400">1. Trích nguyên hình học thực (Extract)</div>
                        <div className="text-[11px] text-neutral-400 mt-1">
                          Zoom chính xác vùng hình học từ Model Space phóng to lên tỷ lệ {currentSelectedDetail.recommendedScale}
                        </div>
                      </div>

                      <div
                        onClick={() => handleDetailTypeChange(currentSelectedDetail.id, "GENERATE_FROM_TEMPLATE")}
                        className={`p-3 rounded-xl border cursor-pointer transition ${
                          currentSelectedDetail.extractionType === "GENERATE_FROM_TEMPLATE"
                            ? "bg-cyan-500/20 border-cyan-500 text-white"
                            : "bg-[#161719] border-neutral-800 text-neutral-400 hover:border-neutral-700"
                        }`}
                      >
                        <div className="font-bold text-xs text-amber-400">2. Dựng chi tiết chuẩn mẫu AI (Generate)</div>
                        <div className="text-[11px] text-neutral-400 mt-1">
                          Áp dụng thư viện chi tiết cấu tạo kiến trúc HNL với đầy đủ ghi chú vật liệu và dim chi tiết
                        </div>
                      </div>
                    </div>

                    {/* Detail Template Selector if in GENERATE mode */}
                    {currentSelectedDetail.extractionType === "GENERATE_FROM_TEMPLATE" && (
                      <div className="space-y-2 pt-2 border-t border-neutral-800">
                        <label className="text-xs font-semibold text-neutral-300 flex items-center space-x-1.5">
                          <BookOpen className="w-3.5 h-3.5 text-cyan-400" />
                          <span>Chọn mẫu chi tiết trong Thư viện Tiêu chuẩn HNL:</span>
                        </label>

                        <div className="grid grid-cols-2 gap-2">
                          {STANDARD_DETAIL_TEMPLATES.map((tmpl) => {
                            const isMatch = currentSelectedDetail.templateId === tmpl.id;
                            return (
                              <div
                                key={tmpl.id}
                                onClick={() => handleDetailTypeChange(currentSelectedDetail.id, "GENERATE_FROM_TEMPLATE", tmpl.id)}
                                className={`p-2.5 rounded-xl border cursor-pointer transition ${
                                  isMatch
                                    ? "bg-amber-500/20 border-amber-500 text-white shadow"
                                    : "bg-[#161719] border-neutral-800 text-neutral-300 hover:border-neutral-700"
                                }`}
                              >
                                <div className="flex items-center justify-between">
                                  <span className="font-bold text-xs text-amber-300">{tmpl.name}</span>
                                  <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-black/40 text-emerald-400">
                                    Khớp {tmpl.similarityScore}%
                                  </span>
                                </div>
                                <div className="text-[10px] text-neutral-400 mt-1 line-clamp-1">{tmpl.description}</div>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Feature recognition preview badge */}
                  <div className="p-4 rounded-xl bg-[#202226] border border-neutral-700 space-y-2">
                    <span className="text-xs font-bold text-neutral-300">Dấu hiệu nhận diện hình học AI (CAD Vision):</span>
                    <div className="flex flex-wrap gap-2">
                      {currentSelectedDetail.detectedFeatures.map((feat, i) => (
                        <span
                          key={i}
                          className="px-2.5 py-1 rounded-lg bg-black/30 border border-neutral-700 text-cyan-300 text-xs flex items-center space-x-1"
                        >
                          <CheckCircle2 className="w-3 h-3 text-cyan-400" />
                          <span>{feat}</span>
                        </span>
                      ))}
                    </div>
                  </div>
                </div>
              ) : (
                <div className="flex-1 flex flex-col items-center justify-center text-center text-neutral-500">
                  <Wand2 className="w-12 h-12 mb-3 text-neutral-600" />
                  <p className="text-sm font-semibold">Chọn một Detail đề xuất bên trái để cấu hình</p>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Tab 2: Reference Layout / Template Style */}
        {activeTab === "REFERENCE" && (
          <div className="flex-1 p-6 overflow-y-auto space-y-6">
            <div className="grid grid-cols-2 gap-6">
              {/* Pre-trained Presentation Styles */}
              <div className="space-y-4">
                <h3 className="font-bold text-sm text-neutral-200 uppercase tracking-wider flex items-center space-x-2">
                  <Layout className="w-4 h-4 text-cyan-400" />
                  <span>PHONG CÁCH BỐ CỤC CHUẨN HNL (PRESENTATION STYLES)</span>
                </h3>

                <div className="space-y-3">
                  {REFERENCE_LAYOUT_STYLES.map((style, idx) => (
                    <div
                      key={style.templateName}
                      onClick={() => {
                        setSelectedStyleIndex(idx);
                        setPaperSize(style.paperSize);
                      }}
                      className={`p-4 rounded-2xl border cursor-pointer transition ${
                        selectedStyleIndex === idx
                          ? "bg-cyan-500/20 border-cyan-500 text-white shadow-lg"
                          : "bg-[#202226] border-neutral-700 text-neutral-300 hover:border-neutral-600"
                      }`}
                    >
                      <div className="flex items-center justify-between mb-2">
                        <span className="font-bold text-sm text-cyan-300">{style.templateName}</span>
                        <span className="px-2 py-0.5 rounded bg-black/40 text-xs font-mono font-bold text-amber-400">
                          Khổ {style.paperSize}
                        </span>
                      </div>
                      <p className="text-xs text-neutral-400 mb-3">
                        Tỷ lệ phân bổ: Mặt bằng chính {Math.round(style.mainViewRatio * 100)}%, Mặt cắt {Math.round(style.sectionRatio * 100)}%, Detail {Math.round(style.detailRatio * 100)}%
                      </p>
                      <div className="flex items-center space-x-2 text-[11px] text-neutral-400">
                        <span>Số lượng khung nhìn: <strong>{style.viewportGrid.length} Viewports</strong></span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* Upload Reference Image / PDF dropzone */}
              <div className="space-y-4">
                <h3 className="font-bold text-sm text-neutral-200 uppercase tracking-wider flex items-center space-x-2">
                  <Upload className="w-4 h-4 text-amber-400" />
                  <span>HỌC BỐ CỤC TỪ ẢNH / PDF THAM KHẢO</span>
                </h3>

                <div className="p-8 rounded-2xl border-2 border-dashed border-neutral-700 bg-[#202226]/60 flex flex-col items-center justify-center text-center space-y-3 hover:border-cyan-500/50 transition cursor-pointer">
                  <div className="w-12 h-12 rounded-full bg-cyan-500/10 flex items-center justify-center text-cyan-400">
                    <FileImage className="w-6 h-6" />
                  </div>
                  <div>
                    <p className="font-bold text-sm text-neutral-200">Kéo thả ảnh bản vẽ mẫu hoặc bấm để tải lên</p>
                    <p className="text-xs text-neutral-400 mt-1">Hỗ trợ file PNG, JPG, PDF bản vẽ Shopdrawing mẫu</p>
                  </div>
                  <span className="px-3 py-1 rounded-lg bg-neutral-800 text-neutral-300 text-xs border border-neutral-700">
                    Chọn tệp tham khảo
                  </span>
                </div>

                <div className="p-4 rounded-xl bg-[#202226] border border-neutral-800 text-xs space-y-1.5">
                  <div className="font-bold text-neutral-300">Quy trình AI Vision Layout:</div>
                  <p className="text-neutral-400 text-[11px]">
                    1. AI quét cấu trúc lưới (Grid structure) và phát hiện vị trí Mặt bằng, Mặt cắt, Detail, Khung tên.
                  </p>
                  <p className="text-neutral-400 text-[11px]">
                    2. Tự động ánh xạ vùng vẽ Model của bạn vào đúng tỷ lệ và vị trí của bản vẽ tham khảo mà không sao chép nội dung bản quyền.
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 3: Smart Annotation & Dimensioning */}
        {activeTab === "ANNOTATION" && (
          <div className="flex-1 p-6 overflow-y-auto space-y-6">
            <div className="grid grid-cols-2 gap-6">
              {/* Hierarchy Checklist */}
              <div className="p-5 rounded-2xl bg-[#202226] border border-neutral-700 space-y-4">
                <h3 className="font-bold text-sm text-neutral-200 uppercase tracking-wider flex items-center space-x-2">
                  <Sliders className="w-4 h-4 text-cyan-400" />
                  <span>PHÂN CẤP KÍCH THƯỚC TỰ ĐỘNG (AUTO DIMENSION)</span>
                </h3>

                <div className="space-y-3">
                  <label className="flex items-center space-x-3 p-3 rounded-xl bg-[#161719] border border-neutral-800 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={dimHierarchy.overall}
                      onChange={(e) => setDimHierarchy({ ...dimHierarchy, overall: e.target.checked })}
                      className="rounded text-cyan-500 focus:ring-0"
                    />
                    <div>
                      <div className="font-bold text-xs text-neutral-200">1. Kích thước tổng thể (Overall Dimension)</div>
                      <div className="text-[11px] text-neutral-400">Chiều dài và chiều rộng phủ bì toàn bộ công trình</div>
                    </div>
                  </label>

                  <label className="flex items-center space-x-3 p-3 rounded-xl bg-[#161719] border border-neutral-800 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={dimHierarchy.grid}
                      onChange={(e) => setDimHierarchy({ ...dimHierarchy, grid: e.target.checked })}
                      className="rounded text-cyan-500 focus:ring-0"
                    />
                    <div>
                      <div className="font-bold text-xs text-neutral-200">2. Kích thước tim trục (Grid Dimension)</div>
                      <div className="text-[11px] text-neutral-400">Khoảng cách giữa các trục định vị cột và tường chính</div>
                    </div>
                  </label>

                  <label className="flex items-center space-x-3 p-3 rounded-xl bg-[#161719] border border-neutral-800 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={dimHierarchy.openings}
                      onChange={(e) => setDimHierarchy({ ...dimHierarchy, openings: e.target.checked })}
                      className="rounded text-cyan-500 focus:ring-0"
                    />
                    <div>
                      <div className="font-bold text-xs text-neutral-200">3. Kích thước ô mở cửa & vách (Opening Dimension)</div>
                      <div className="text-[11px] text-neutral-400">Thông thủy cửa đi, cửa sổ, khoảng cách từ góc tường</div>
                    </div>
                  </label>

                  <label className="flex items-center space-x-3 p-3 rounded-xl bg-[#161719] border border-neutral-800 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={dimHierarchy.details}
                      onChange={(e) => setDimHierarchy({ ...dimHierarchy, details: e.target.checked })}
                      className="rounded text-cyan-500 focus:ring-0"
                    />
                    <div>
                      <div className="font-bold text-xs text-neutral-200">4. Kích thước cấu tạo Detail (Component Dimension)</div>
                      <div className="text-[11px] text-neutral-400">Chiều dày lớp vật liệu, nẹp Z, khe hở 10-25mm</div>
                    </div>
                  </label>
                </div>
              </div>

              {/* Anti-Collision & Smart Annotation Engine */}
              <div className="p-5 rounded-2xl bg-[#202226] border border-neutral-700 space-y-4">
                <h3 className="font-bold text-sm text-neutral-200 uppercase tracking-wider flex items-center space-x-2">
                  <Zap className="w-4 h-4 text-emerald-400" />
                  <span>THUẬT TOÁN CHỐNG CHỒNG CHÉO (COLLISION DETECTION)</span>
                </h3>

                <div className="p-4 rounded-xl bg-[#161719] border border-neutral-800 space-y-3">
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="font-bold text-xs text-emerald-400">Tự động nắn tuyến Leader & Text</div>
                      <div className="text-[11px] text-neutral-400">Chống đè text lên đường nét bản vẽ, tự uốn góc mũi tên</div>
                    </div>
                    <input
                      type="checkbox"
                      checked={antiCollisionEnabled}
                      onChange={(e) => setAntiCollisionEnabled(e.target.checked)}
                      className="rounded text-cyan-500 focus:ring-0"
                    />
                  </div>

                  <div className="pt-2 border-t border-neutral-800 space-y-1.5 text-[11px] text-neutral-400">
                    <div className="flex items-center space-x-1.5 text-neutral-300 font-semibold">
                      <CheckCircle2 className="w-3.5 h-3.5 text-cyan-400" />
                      <span>Nguồn dữ liệu chú thích ưu tiên:</span>
                    </div>
                    <ul className="list-disc pl-5 space-y-1 text-neutral-400">
                      <li>Layer đối tượng (KT_TUONG, KT_TRAN, KT_CUA)</li>
                      <li>Thuộc tính Block Attribute & Dynamic Properties</li>
                      <li>Tiêu chuẩn vật liệu HNL Architecture Standard</li>
                      <li>Gắn nhãn cảnh báo <em>"Cần xác nhận"</em> nếu độ tin cậy dưới 85%</li>
                    </ul>
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Tab 4: Interactive Sheet Preview & Quality Studio */}
        {activeTab === "PREVIEW" && (
          <div className="flex-1 flex flex-col overflow-hidden bg-[#141517]">
            {/* Top Toolbar for Preview */}
            <div className="h-10 px-6 bg-[#18191C] border-b border-neutral-800 flex items-center justify-between text-xs">
              <div className="flex items-center space-x-2">
                <span className="text-neutral-400 font-semibold">Chọn Sheet:</span>
                {sheetSets.map((sheet, sIdx) => (
                  <button
                    key={sheet.sheetNumber}
                    onClick={() => setActiveSheetIndex(sIdx)}
                    className={`px-3 py-1 rounded-md font-semibold transition ${
                      activeSheetIndex === sIdx
                        ? "bg-cyan-500 text-black shadow font-bold"
                        : "bg-[#22242A] text-neutral-300 hover:text-white"
                    }`}
                  >
                    {sheet.sheetNumber} - {sheet.sheetTitle.substring(0, 18)}...
                  </button>
                ))}
              </div>

              <div className="flex items-center space-x-3">
                <button
                  onClick={handleAutoOptimize}
                  className="px-3.5 py-1 rounded-lg bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white font-bold flex items-center space-x-1.5 shadow transition"
                >
                  <Sparkles className="w-3.5 h-3.5" />
                  <span>⚡ TỰ ĐỘNG TỐI ƯU BỐ CỤC (AUTO OPTIMIZE)</span>
                </button>
              </div>
            </div>

            {/* Middle Main Preview Stage */}
            <div className="flex-1 flex overflow-hidden">
              {/* Sheet Canvas Simulation */}
              <div className="flex-1 p-6 flex items-center justify-center bg-[#0F1012] overflow-auto">
                {/* Simulated Paper Sheet */}
                <div
                  className="relative bg-white text-black rounded shadow-2xl border-4 border-neutral-700 flex flex-col justify-between overflow-hidden"
                  style={{
                    width: orientation === "Landscape" ? "640px" : "450px",
                    height: orientation === "Landscape" ? "450px" : "640px",
                  }}
                >
                  {/* Paper Border & Margins (10mm simulated) */}
                  <div className="absolute inset-2 border-2 border-black pointer-events-none" />

                  {/* Render Mock Viewports on Sheet */}
                  {previewViewports.map((vp) => {
                    // Normalize position percentages based on paper width & height
                    const leftPct = (vp.x / paperDimensions.widthMm) * 100;
                    const topPct = (vp.y / paperDimensions.heightMm) * 100;
                    const widthPct = (vp.width / paperDimensions.widthMm) * 100;
                    const heightPct = (vp.height / paperDimensions.heightMm) * 100;

                    return (
                      <div
                        key={vp.id}
                        className={`absolute border flex flex-col justify-between p-1.5 transition overflow-hidden ${
                          vp.type === "MAIN_PLAN"
                            ? "bg-cyan-50/70 border-cyan-700"
                            : vp.type === "SECTION"
                            ? "bg-amber-50/70 border-amber-700"
                            : "bg-emerald-50/70 border-emerald-700"
                        }`}
                        style={{
                          left: `${leftPct}%`,
                          top: `${topPct}%`,
                          width: `${widthPct}%`,
                          height: `${heightPct}%`,
                        }}
                      >
                        {/* Mock Drawing Geometry Graphic */}
                        <div className="flex-1 flex flex-col items-center justify-center opacity-70">
                          {vp.type === "MAIN_PLAN" ? (
                            <div className="w-full h-full border border-dashed border-neutral-400 p-2 flex flex-col justify-between text-[8px] text-neutral-600 font-mono">
                              <div className="flex justify-between">
                                <span>TRỤC 1</span>
                                <span>TRỤC 2</span>
                                <span>TRỤC 3</span>
                              </div>
                              <div className="text-center font-bold text-neutral-800">
                                MẶT BẰNG THI CÔNG NỘI THẤT & TRẦN
                              </div>
                              <div className="flex justify-between text-[7px]">
                                <span>S = 81.60 m²</span>
                                <span>TL: {vp.scale}</span>
                              </div>
                            </div>
                          ) : vp.type === "SECTION" ? (
                            <div className="w-full h-full border border-neutral-400 p-1 flex flex-col items-center justify-center text-[8px] text-amber-900 font-mono">
                              <span className="font-bold">{vp.title}</span>
                              <span className="text-[7px]">Cắt qua trần & dầm sàn</span>
                            </div>
                          ) : (
                            <div className="w-full h-full border border-neutral-400 p-1 flex flex-col items-center justify-center text-[7px] text-emerald-900 font-mono">
                              <span className="font-bold">{vp.title}</span>
                              <span className="text-[6px]">TL: {vp.scale}</span>
                            </div>
                          )}
                        </div>

                        {/* Viewport Title & Bubble Tag */}
                        <div className="h-4 bg-black/80 text-white px-1.5 rounded flex items-center justify-between text-[7px] font-bold">
                          <span className="truncate">{vp.title}</span>
                          <span className="text-cyan-300 font-mono">{vp.scale}</span>
                        </div>
                      </div>
                    );
                  })}

                  {/* Title Block Bottom-Right */}
                  <div className="absolute bottom-2 right-2 w-48 h-16 border-2 border-black bg-neutral-100 flex flex-col justify-between p-1 text-[7px] font-sans">
                    <div className="flex justify-between border-b border-black pb-0.5">
                      <span className="font-bold text-cyan-900">HNL ARCHITECTURE & DESIGN</span>
                      <span className="font-mono">HNL-DWG-2026</span>
                    </div>
                    <div className="font-bold text-[8px] truncate">
                      {sheetSets[activeSheetIndex]?.sheetTitle || "MẶT BẰNG & CHI TIẾT"}
                    </div>
                    <div className="flex justify-between text-[6px] text-neutral-600">
                      <span>TỶ LỆ: 1:50 / 1:10</span>
                      <span className="font-bold text-black">SHEET: {sheetSets[activeSheetIndex]?.sheetNumber || "A-101"}</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Right Quality Score Inspector Sidebar */}
              <div className="w-80 bg-[#161719] border-l border-neutral-800 p-4 flex flex-col space-y-4 overflow-y-auto">
                <div className="p-4 rounded-2xl bg-[#202226] border border-neutral-700 text-center space-y-2">
                  <span className="text-xs font-semibold text-neutral-400">CHỈ SỐ CHẤT LƯỢNG BẢN VẼ (AUDIT SCORE)</span>
                  <div className="text-4xl font-extrabold text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 to-emerald-400">
                    {qualityMetrics.overallScore} / 100
                  </div>
                  <p className="text-[11px] text-emerald-400 font-semibold">
                    Đạt chuẩn hồ sơ thi công chuyên nghiệp HNL
                  </p>
                </div>

                {/* Score Breakdown Bars */}
                <div className="space-y-2 text-xs">
                  <div className="space-y-1">
                    <div className="flex justify-between text-[11px]">
                      <span className="text-neutral-400">Độ rõ ràng (Readability):</span>
                      <span className="font-bold text-cyan-400">{qualityMetrics.readabilityScore}%</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-neutral-800 overflow-hidden">
                      <div className="h-full bg-cyan-400 rounded-full" style={{ width: `${qualityMetrics.readabilityScore}%` }} />
                    </div>
                  </div>

                  <div className="space-y-1">
                    <div className="flex justify-between text-[11px]">
                      <span className="text-neutral-400">Căn lề & Trực giao (Alignment):</span>
                      <span className="font-bold text-cyan-400">{qualityMetrics.alignmentScore}%</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-neutral-800 overflow-hidden">
                      <div className="h-full bg-cyan-400 rounded-full" style={{ width: `${qualityMetrics.alignmentScore}%` }} />
                    </div>
                  </div>

                  <div className="space-y-1">
                    <div className="flex justify-between text-[11px]">
                      <span className="text-neutral-400">Khoảng trắng (White Space):</span>
                      <span className="font-bold text-cyan-400">{qualityMetrics.whiteSpaceScore}%</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-neutral-800 overflow-hidden">
                      <div className="h-full bg-cyan-400 rounded-full" style={{ width: `${qualityMetrics.whiteSpaceScore}%` }} />
                    </div>
                  </div>

                  <div className="space-y-1">
                    <div className="flex justify-between text-[11px]">
                      <span className="text-neutral-400">Cân bằng Viewport (Balance):</span>
                      <span className="font-bold text-cyan-400">{qualityMetrics.viewportBalanceScore}%</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-neutral-800 overflow-hidden">
                      <div className="h-full bg-cyan-400 rounded-full" style={{ width: `${qualityMetrics.viewportBalanceScore}%` }} />
                    </div>
                  </div>
                </div>

                {/* Audit Warnings & Recommendations */}
                <div className="space-y-2 pt-2 border-t border-neutral-800 text-xs">
                  <span className="font-bold text-neutral-300">Đánh giá & Khuyến nghị AI:</span>
                  {qualityMetrics.recommendations.map((rec, i) => (
                    <div key={i} className="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-[11px]">
                      {rec}
                    </div>
                  ))}
                  {qualityMetrics.warnings.map((warn, i) => (
                    <div key={i} className="p-2 rounded-lg bg-amber-500/10 border border-amber-500/30 text-amber-300 text-[11px]">
                      {warn}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Modal Bottom Action Bar */}
        <div className="h-16 px-6 bg-[#141517] border-t border-neutral-800 flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-5 py-2.5 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs font-semibold transition"
          >
            Hủy bỏ
          </button>

          <div className="flex items-center space-x-3">
            {activeTab !== "PREVIEW" ? (
              <button
                onClick={() => setActiveTab("PREVIEW")}
                className="px-5 py-2.5 rounded-xl bg-neutral-700 hover:bg-neutral-600 text-white text-xs font-bold flex items-center space-x-1.5 transition"
              >
                <span>Tiếp tục: Xem trước Sheet</span>
                <ChevronRight className="w-4 h-4" />
              </button>
            ) : null}

            <button
              onClick={handleApplyToAutocad}
              className="px-6 py-2.5 rounded-xl bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-bold text-xs flex items-center space-x-2 shadow-lg shadow-cyan-900/40 transition"
            >
              <CheckCircle2 className="w-4 h-4" />
              <span>ÁP DỤNG LAYOUT VÀO BẢN VẼ HNL (AUTO-CAD PLUGIN CẦN KẾT NỐI)</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
