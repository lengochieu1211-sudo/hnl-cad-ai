import React, { useState } from "react";
import {
  SectionCutLine,
  GeneratedSectionData,
  CadEntity,
} from "../../types/cad";
import {
  SAMPLE_SECTION_LINES,
  generateParametricSection,
} from "../../lib/parametricSectionEngine";
import {
  Layers,
  X,
  Maximize2,
  Download,
  Plus,
  Compass,
  CheckCircle2,
  FileText,
  Sliders,
  Sparkles,
} from "lucide-react";

interface HnlSectionGeneratorModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
  onInsertToCanvas?: (sectionEntities: CadEntity[]) => void;
}

export const HnlSectionGeneratorModal: React.FC<HnlSectionGeneratorModalProps> = ({
  isOpen,
  onClose,
  entities,
  onInsertToCanvas,
}) => {
  const [selectedCutLine, setSelectedCutLine] = useState<SectionCutLine>(SAMPLE_SECTION_LINES[0]);
  const [slabElevation, setSlabElevation] = useState<number>(3600);
  const [ceilingElevation, setCeilingElevation] = useState<number>(2800);
  const [activeTab, setActiveTab] = useState<"PREVIEW" | "SPECS" | "ELEMENTS">("PREVIEW");

  if (!isOpen) return null;

  const sectionData: GeneratedSectionData = generateParametricSection({
    cutLine: selectedCutLine,
    entities,
    slabElevationMm: slabElevation,
    ceilingElevationMm: ceilingElevation,
    floorElevationMm: 0,
  });

  const svgWidth = 760;
  const svgHeight = 380;
  const scaleX = svgWidth / (sectionData.totalWidthMm + 800);
  const scaleY = (svgHeight - 80) / (slabElevation + 400);

  const toSvgX = (xMm: number) => 400 * scaleX + xMm * scaleX;
  const toSvgY = (yMm: number) => svgHeight - 40 - yMm * scaleY;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 animate-in fade-in">
      <div className="w-full max-w-5xl bg-[#141619] border border-neutral-700 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-neutral-800 bg-[#1A1D23]">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-xl bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              <Layers className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>HNL Parametric Section Generator (Trích Xuất Mặt Cắt Tự Động)</span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-cyan-500/20 text-cyan-300 font-mono">
                  LIVE 2D CAD
                </span>
              </h2>
              <p className="text-xs text-neutral-400">
                Sinh mặt cắt tham số từ dữ liệu HNL. Preset vật liệu/chống cháy phải đối chiếu Project Spec & Approved System trước khi phát hành.
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Toolbar Controls */}
        <div className="px-6 py-3 bg-[#181A1F] border-b border-neutral-800 flex flex-wrap items-center justify-between gap-3 text-xs">
          <div className="flex items-center space-x-3">
            <span className="text-neutral-400 font-medium">Vị trí mặt cắt:</span>
            <div className="flex space-x-1.5">
              {SAMPLE_SECTION_LINES.map((sec) => (
                <button
                  key={sec.id}
                  onClick={() => setSelectedCutLine(sec)}
                  className={`px-3 py-1 rounded-lg font-mono transition ${
                    selectedCutLine.id === sec.id
                      ? "bg-cyan-500 text-black font-bold shadow-md shadow-cyan-500/20"
                      : "bg-neutral-800 text-neutral-300 hover:bg-neutral-700"
                  }`}
                >
                  {sec.id}
                </button>
              ))}
            </div>
          </div>

          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-2">
              <span className="text-neutral-400">Đáy trần (FCL):</span>
              <input
                type="number"
                step="50"
                value={ceilingElevation}
                onChange={(e) => setCeilingElevation(Number(e.target.value))}
                className="w-20 px-2 py-1 bg-neutral-900 border border-neutral-700 rounded text-cyan-300 font-mono text-right"
              />
              <span className="text-neutral-500 font-mono">mm</span>
            </div>

            <div className="flex items-center space-x-2">
              <span className="text-neutral-400">Đáy sàn BTCT (SSL):</span>
              <input
                type="number"
                step="50"
                value={slabElevation}
                onChange={(e) => setSlabElevation(Number(e.target.value))}
                className="w-20 px-2 py-1 bg-neutral-900 border border-neutral-700 rounded text-amber-300 font-mono text-right"
              />
              <span className="text-neutral-500 font-mono">mm</span>
            </div>
          </div>

          {/* Navigation Tabs */}
          <div className="flex bg-neutral-900 p-0.5 rounded-lg border border-neutral-800 text-xs">
            <button
              onClick={() => setActiveTab("PREVIEW")}
              className={`px-3 py-1 rounded-md transition ${
                activeTab === "PREVIEW" ? "bg-neutral-800 text-white font-medium shadow-sm" : "text-neutral-400 hover:text-white"
              }`}
            >
              Bản vẽ 2D
            </button>
            <button
              onClick={() => setActiveTab("SPECS")}
              className={`px-3 py-1 rounded-md transition ${
                activeTab === "SPECS" ? "bg-neutral-800 text-white font-medium shadow-sm" : "text-neutral-400 hover:text-white"
              }`}
            >
              Quy cách vật liệu
            </button>
            <button
              onClick={() => setActiveTab("ELEMENTS")}
              className={`px-3 py-1 rounded-md transition ${
                activeTab === "ELEMENTS" ? "bg-neutral-800 text-white font-medium shadow-sm" : "text-neutral-400 hover:text-white"
              }`}
            >
              Danh sách chi tiết ({sectionData.elements.length})
            </button>
          </div>
        </div>

        {/* Modal Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          {activeTab === "PREVIEW" && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-2">
                  <span className="font-bold text-white text-sm">{sectionData.title}</span>
                  <span className="text-xs px-2 py-0.5 rounded bg-neutral-800 text-cyan-400 font-mono">
                    {sectionData.scale}
                  </span>
                </div>
                <div className="text-xs text-neutral-400">
                  Tổng chiều dài cắt: <strong className="text-white font-mono">{sectionData.totalWidthMm} mm</strong>
                </div>
              </div>

              {/* Interactive Vector CAD Cross Section Canvas */}
              <div className="w-full h-[380px] bg-[#0E1013] border border-neutral-800 rounded-xl overflow-hidden relative shadow-inner flex items-center justify-center">
                <svg
                  viewBox={`0 0 ${svgWidth} ${svgHeight}`}
                  className="w-full h-full select-none"
                  style={{ backgroundColor: "#0E1013" }}
                >
                  {/* Grid Lines */}
                  <defs>
                    <pattern id="secGrid" width="40" height="40" patternUnits="userSpaceOnUse">
                      <path d="M 40 0 L 0 0 0 40" fill="none" stroke="rgba(255,255,255,0.04)" strokeWidth="1" />
                    </pattern>
                  </defs>
                  <rect width={svgWidth} height={svgHeight} fill="url(#secGrid)" />

                  {/* Render Section Elements */}
                  {sectionData.elements.map((el) => {
                    const sx1 = toSvgX(el.x1);
                    const sy1 = toSvgY(el.y1);
                    const sx2 = toSvgX(el.x2);
                    const sy2 = toSvgY(el.y2);
                    const width = Math.abs(sx2 - sx1);
                    const height = Math.abs(sy1 - sy2);
                    const topY = Math.min(sy1, sy2);
                    const leftX = Math.min(sx1, sx2);

                    if (el.type === "HANGER_ROD") {
                      return (
                        <g key={el.id}>
                          <line
                            x1={sx1}
                            y1={sy1}
                            x2={sx2}
                            y2={sy2}
                            stroke="#00B0FF"
                            strokeWidth="2"
                            strokeDasharray="4 2"
                          />
                          {/* Anchor Bolt Top */}
                          <circle cx={sx2} cy={sy2} r="3" fill="#FF5252" />
                          {/* Clip bottom */}
                          <rect x={sx1 - 4} y={sy1 - 4} width="8" height="8" fill="#00E676" />
                        </g>
                      );
                    }

                    if (el.type === "LEVEL_MARK") {
                      return (
                        <g key={el.id}>
                          <line x1={sx1} y1={sy1} x2={sx2} y2={sy2} stroke={el.color || "#4FC3F7"} strokeWidth="1" strokeDasharray="6 3" />
                          <polygon
                            points={`${sx1 + 40},${sy1} ${sx1 + 30},${sy1 - 10} ${sx1 + 50},${sy1 - 10}`}
                            fill={el.color || "#4FC3F7"}
                          />
                          <text x={sx1 + 60} y={sy1 - 3} fill={el.color || "#4FC3F7"} fontSize="9" fontFamily="monospace" fontWeight="bold">
                            {el.label}
                          </text>
                        </g>
                      );
                    }

                    if (el.type === "SLAB" || el.type === "BEAM") {
                      return (
                        <g key={el.id}>
                          <rect
                            x={leftX}
                            y={topY}
                            width={width}
                            height={height}
                            fill={el.color || "#607D8B"}
                            stroke="#B0BEC5"
                            strokeWidth="1.5"
                          />
                          <text
                            x={leftX + width / 2}
                            y={topY + height / 2 + 3}
                            fill="#FFFFFF"
                            fontSize="10"
                            textAnchor="middle"
                            fontFamily="monospace"
                            fontWeight="bold"
                          >
                            {el.label}
                          </text>
                        </g>
                      );
                    }

                    if (el.type === "ROCKWOOL") {
                      return (
                        <rect
                          key={el.id}
                          x={leftX}
                          y={topY}
                          width={width}
                          height={height}
                          fill="#8D6E63"
                          stroke="#FFB74D"
                          strokeWidth="1"
                          strokeDasharray="3 3"
                        />
                      );
                    }

                    return (
                      <rect
                        key={el.id}
                        x={leftX}
                        y={topY}
                        width={Math.max(width, 2)}
                        height={Math.max(height, 2)}
                        fill={el.color || "#E0E0E0"}
                        stroke="#000"
                        strokeWidth="0.5"
                      />
                    );
                  })}
                </svg>
              </div>
            </div>
          )}

          {activeTab === "SPECS" && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
              <div className="p-4 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-3">
                <div className="flex items-center space-x-2 text-cyan-400 font-bold">
                  <CheckCircle2 className="w-4 h-4" />
                  <span>Quy cách cấu tạo Trần Thạch Cao Chìm</span>
                </div>
                <div className="space-y-2 text-neutral-300">
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Tấm thạch cao:</span>
                    <span className="font-mono text-white">Gyproc Tiêu Chuẩn 12.5mm</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Xương chính C38:</span>
                    <span className="font-mono text-amber-400">Vĩnh Tường Serra @800mm</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Xương phụ M-Bar:</span>
                    <span className="font-mono text-amber-400">Vĩnh Tường Tripflex @400mm</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Ty treo & Khóa liên kết:</span>
                    <span className="font-mono text-sky-400">Ty ren M8 + Nở đạn đính sàn</span>
                  </div>
                  <div className="flex justify-between py-1">
                    <span className="text-neutral-400">Nẹp chu vi tường:</span>
                    <span className="font-mono text-emerald-400">Nẹp viền Z-Shadowline 15mm</span>
                  </div>
                </div>
              </div>

              <div className="p-4 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-3">
                <div className="flex items-center space-x-2 text-red-400 font-bold">
                  <CheckCircle2 className="w-4 h-4" />
                  <span>Preset minh họa vách chống cháy – cần xác nhận hệ EI</span>
                </div>
                <div className="space-y-2 text-neutral-300">
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Tiêu chuẩn nghiệm thu:</span>
                    <span className="font-mono text-amber-300">Approved System / Test Report bắt buộc</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Số lớp tấm thạch cao:</span>
                    <span className="font-mono text-red-300">2 lớp Gyproc FireBloc 12.5mm / mặt</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Khung xương đứng:</span>
                    <span className="font-mono text-amber-400">V-Wall C75 dày 0.5mm @400mm</span>
                  </div>
                  <div className="flex justify-between py-1 border-b border-neutral-800">
                    <span className="text-neutral-400">Lớp bông cách âm chống cháy:</span>
                    <span className="font-mono text-amber-300">Rockwool 50mm - 60kg/m³</span>
                  </div>
                  <div className="flex justify-between py-1">
                    <span className="text-neutral-400">Chỉ số cách âm dự kiến:</span>
                    <span className="font-mono text-amber-300">Chưa xác nhận – cần Test Report</span>
                  </div>
                </div>
              </div>
            </div>
          )}

          {activeTab === "ELEMENTS" && (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs border border-neutral-800">
                <thead className="bg-[#1A1D24] text-neutral-400 font-mono">
                  <tr>
                    <th className="p-2 border-b border-neutral-800">STT</th>
                    <th className="p-2 border-b border-neutral-800">Loại cấu kiện</th>
                    <th className="p-2 border-b border-neutral-800">Tên mô tả</th>
                    <th className="p-2 border-b border-neutral-800">Vật liệu quy cách</th>
                    <th className="p-2 border-b border-neutral-800 text-right">Cao độ (mm)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-neutral-800 text-neutral-300 font-mono">
                  {sectionData.elements.map((el, i) => (
                    <tr key={el.id} className="hover:bg-neutral-800/50">
                      <td className="p-2 text-neutral-500">{i + 1}</td>
                      <td className="p-2 font-bold text-cyan-400">{el.type}</td>
                      <td className="p-2 text-white font-sans">{el.label}</td>
                      <td className="p-2 text-neutral-400 font-sans">{el.material}</td>
                      <td className="p-2 text-right text-emerald-400 font-mono">
                        {el.elevationMm !== undefined ? `+${el.elevationMm}` : "-"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="px-6 py-3 border-t border-neutral-800 bg-[#1A1D23] flex items-center justify-between">
          <div className="flex items-center space-x-2 text-xs text-neutral-400">
            <Sparkles className="w-4 h-4 text-cyan-400" />
            <span>Tự động đồng bộ với thay đổi trên Model Space thông qua DAG Dependency</span>
          </div>

          <div className="flex items-center space-x-3">
            <button
              onClick={onClose}
              className="px-4 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-neutral-300 font-medium text-xs transition"
            >
              Đóng
            </button>
            <button
              onClick={() => {
                const baseX = 10000;
                const baseY = 0;
                const generated: CadEntity[] = sectionData.elements.flatMap((el, i) => {
                  const line: CadEntity = {
                    id: `sec_${sectionData.sectionId}_${Date.now()}_${i}`,
                    handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                    type: "LINE",
                    layer: "HNL_SECTION",
                    color: el.color || "#00E5FF",
                    start: { x: baseX + el.x1, y: baseY + el.y1 },
                    end: { x: baseX + el.x2, y: baseY + el.y2 },
                  } as CadEntity;
                  const label: CadEntity = {
                    id: `sec_txt_${sectionData.sectionId}_${Date.now()}_${i}`,
                    handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                    type: "TEXT",
                    layer: "HNL_SECTION_NOTE",
                    color: "#E5E7EB",
                    position: { x: baseX + el.x2 + 120, y: baseY + el.y2 },
                    text: el.label,
                    height: 120,
                  } as CadEntity;
                  return [line, label];
                });
                if (generated.length > 0 && onInsertToCanvas) onInsertToCanvas(generated);
                onClose();
              }}
              className="flex items-center space-x-2 px-4 py-2 rounded-xl bg-cyan-500 hover:bg-cyan-400 text-black font-bold text-xs shadow-lg shadow-cyan-500/20 transition"
            >
              <Plus className="w-4 h-4" />
              <span>Chèn vào Bản Vẽ CAD</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
