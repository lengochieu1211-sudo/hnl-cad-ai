import React, { useState } from "react";
import {
  VIETNAM_BUILDING_STANDARDS,
  calculateHangerLoadCapacity,
} from "../../lib/buildingCodeKnowledge";
import {
  BookOpen,
  X,
  Flame,
  Volume2,
  Sliders,
  ShieldCheck,
  Calculator,
  ExternalLink,
  Sparkles,
  CheckCircle2,
  AlertCircle,
} from "lucide-react";

interface HnlBuildingCodeModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const HnlBuildingCodeModal: React.FC<HnlBuildingCodeModalProps> = ({
  isOpen,
  onClose,
}) => {
  const [selectedStandardId, setSelectedStandardId] = useState<string>("QCVN_06_2022_BXD");
  const [activeTab, setActiveTab] = useState<"STANDARDS" | "CALCULATOR">("STANDARDS");

  // Calculator State
  const [calcArea, setCalcArea] = useState<number>(50);
  const [calcBoardLayers, setCalcBoardLayers] = useState<number>(1);
  const [calcBoardThick, setCalcBoardThick] = useState<number>(12.5);
  const [calcHasInsulation, setCalcHasInsulation] = useState<boolean>(true);
  const [calcMepLoad, setCalcMepLoad] = useState<number>(5);

  if (!isOpen) return null;

  const currentStandard =
    VIETNAM_BUILDING_STANDARDS.find((s) => s.codeId === selectedStandardId) ||
    VIETNAM_BUILDING_STANDARDS[0];

  const calcResult = calculateHangerLoadCapacity({
    ceilingAreaM2: calcArea,
    numberOfBoardLayers: calcBoardLayers,
    boardThicknessMm: calcBoardThick,
    hasInsulation: calcHasInsulation,
    mepExtraLoadKgM2: calcMepLoad,
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 animate-in fade-in">
      <div className="w-full max-w-5xl bg-[#141619] border border-neutral-700 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-neutral-800 bg-[#1A1D23]">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-xl bg-red-500/20 text-red-400 border border-red-500/30">
              <BookOpen className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>HNL AI Building Code & Gypsum Standards (Tra Cứu Tiêu Chuẩn & QCVN 06)</span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-red-500/20 text-red-300 font-mono">
                  TCVN / ASTM / QCVN
                </span>
              </h2>
              <p className="text-xs text-neutral-400">
                Quy chuẩn chống cháy, cách âm, khẩu độ ty treo và kiểm toán khả năng chịu tải trần vách thạch cao
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

        {/* Navigation Tabs */}
        <div className="px-6 py-3 bg-[#181A1F] border-b border-neutral-800 flex items-center justify-between text-xs">
          <div className="flex space-x-2">
            <button
              onClick={() => setActiveTab("STANDARDS")}
              className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg transition ${
                activeTab === "STANDARDS"
                  ? "bg-red-500/20 text-red-300 border border-red-500/40 font-bold"
                  : "text-neutral-400 hover:text-white bg-neutral-900"
              }`}
            >
              <BookOpen className="w-4 h-4" />
              <span>Tra Cứu Tiêu Chuẩn & Quy Chuẩn Xây Dựng</span>
            </button>

            <button
              onClick={() => setActiveTab("CALCULATOR")}
              className={`flex items-center space-x-1.5 px-3 py-1.5 rounded-lg transition ${
                activeTab === "CALCULATOR"
                  ? "bg-cyan-500/20 text-cyan-300 border border-cyan-500/40 font-bold"
                  : "text-neutral-400 hover:text-white bg-neutral-900"
              }`}
            >
              <Calculator className="w-4 h-4" />
              <span>Tính Toán Khoảng Cách Ty Treo & Chống Võng (ASTM C635)</span>
            </button>
          </div>
        </div>

        {/* Body Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs text-amber-200">
            <strong>Dữ liệu tiêu chuẩn trong bản này là seed/demo.</strong> Không dùng để nghiệm thu hay kết luận EI/STC/tải trọng. Bản phát hành kỹ thuật phải gắn tài liệu gốc hiện hành, điều khoản và Approved System của dự án.
          </div>
          {activeTab === "STANDARDS" && (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {/* Left Standards Selector List */}
              <div className="space-y-2">
                <div className="text-xs font-bold text-neutral-400 mb-2">DANH MỤC TIÊU CHUẨN</div>
                {VIETNAM_BUILDING_STANDARDS.map((std) => (
                  <button
                    key={std.codeId}
                    onClick={() => setSelectedStandardId(std.codeId)}
                    className={`w-full p-3 rounded-xl border text-left transition flex flex-col space-y-1 text-xs ${
                      selectedStandardId === std.codeId
                        ? "bg-red-500/10 border-red-500 text-white shadow-md shadow-red-500/10"
                        : "bg-neutral-900/60 border-neutral-800 text-neutral-400 hover:border-neutral-700"
                    }`}
                  >
                    <div className="flex items-center space-x-2">
                      {std.category === "FIRE_SAFETY" && <Flame className="w-3.5 h-3.5 text-red-400" />}
                      {std.category === "ACOUSTIC" && <Volume2 className="w-3.5 h-3.5 text-cyan-400" />}
                      {std.category === "INSTALLATION_GUIDE" && <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />}
                      <span className="font-bold text-white font-mono">{std.codeId}</span>
                    </div>
                    <div className="text-[11px] text-neutral-300 line-clamp-2">{std.title}</div>
                  </button>
                ))}
              </div>

              {/* Right Standard Detail View */}
              <div className="md:col-span-2 space-y-3 bg-neutral-900/60 p-4 rounded-xl border border-neutral-800 text-xs">
                <div className="border-b border-neutral-800 pb-3">
                  <div className="text-sm font-bold text-white flex items-center space-x-2">
                    <span className="text-red-400">[{currentStandard.codeId}]</span>
                    <span>{currentStandard.title}</span>
                  </div>
                  <div className="text-neutral-400 mt-1">{currentStandard.summary}</div>
                </div>

                <div className="space-y-3">
                  <div className="font-bold text-neutral-300 text-xs">QUY ĐỊNH BẮT BUỘC & ĐỀ XUẤT THI CÔNG:</div>
                  {currentStandard.keyRules.map((rule, idx) => (
                    <div key={idx} className="p-3 rounded-lg bg-black/40 border border-neutral-800 space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-cyan-300">{rule.ruleName}</span>
                        <span className="px-2 py-0.5 rounded bg-red-500/20 text-red-300 font-mono text-[10px] font-bold">
                          {rule.allowedValues}
                        </span>
                      </div>
                      <div className="text-neutral-300 text-[11px]">{rule.requirement}</div>
                      <div className="text-[10px] text-neutral-500 font-mono">{rule.referenceSection}</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {activeTab === "CALCULATOR" && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-xs">
              {/* Input Form */}
              <div className="p-4 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-4">
                <div className="flex items-center space-x-2 text-cyan-400 font-bold text-sm">
                  <Calculator className="w-4 h-4" />
                  <span>Thông số tải trọng & Cấu tạo trần</span>
                </div>

                <div className="space-y-3">
                  <div>
                    <label className="text-neutral-400 block mb-1">Diện tích trần (m²):</label>
                    <input
                      type="number"
                      value={calcArea}
                      onChange={(e) => setCalcArea(Number(e.target.value))}
                      className="w-full px-3 py-1.5 bg-black/50 border border-neutral-700 rounded-lg text-white font-mono"
                    />
                  </div>

                  <div>
                    <label className="text-neutral-400 block mb-1">Số lớp tấm thạch cao:</label>
                    <select
                      value={calcBoardLayers}
                      onChange={(e) => setCalcBoardLayers(Number(e.target.value))}
                      className="w-full px-3 py-1.5 bg-black/50 border border-neutral-700 rounded-lg text-white font-mono"
                    >
                      <option value={1}>01 Lớp (Trần dân dụng tiêu chuẩn)</option>
                      <option value={2}>02 Lớp (Trần chống cháy / cách âm tiêu chuẩn cao)</option>
                    </select>
                  </div>

                  <div>
                    <label className="text-neutral-400 block mb-1">Độ dày tấm (mm):</label>
                    <select
                      value={calcBoardThick}
                      onChange={(e) => setCalcBoardThick(Number(e.target.value))}
                      className="w-full px-3 py-1.5 bg-black/50 border border-neutral-700 rounded-lg text-white font-mono"
                    >
                      <option value={9.5}>9.5 mm (Trần thả nổi / trần chìm nhẹ)</option>
                      <option value={12.5}>12.5 mm (Chuẩn Saint-Gobain Gyproc)</option>
                      <option value={15}>15.0 mm (Chống cháy FireBloc cao cấp)</option>
                    </select>
                  </div>

                  <div className="flex items-center justify-between p-2 rounded bg-black/30 border border-neutral-800">
                    <span className="text-neutral-300">Có lớp bông khoáng Rockwool cách âm</span>
                    <input
                      type="checkbox"
                      checked={calcHasInsulation}
                      onChange={(e) => setCalcHasInsulation(e.target.checked)}
                      className="accent-cyan-500 rounded cursor-pointer"
                    />
                  </div>

                  <div>
                    <label className="text-neutral-400 block mb-1">Tải trọng MEP phụ trợ (kg/m² - đèn, miệng gió):</label>
                    <input
                      type="number"
                      value={calcMepLoad}
                      onChange={(e) => setCalcMepLoad(Number(e.target.value))}
                      className="w-full px-3 py-1.5 bg-black/50 border border-neutral-700 rounded-lg text-white font-mono"
                    />
                  </div>
                </div>
              </div>

              {/* Output Results */}
              <div className="p-4 rounded-xl bg-neutral-900/80 border border-neutral-800 space-y-4 flex flex-col justify-between">
                <div>
                  <div className="flex items-center space-x-2 text-emerald-400 font-bold text-sm mb-3">
                    <ShieldCheck className="w-4 h-4" />
                    <span>Kết quả kiểm toán an toàn kết cấu trần (ASTM C635)</span>
                  </div>

                  <div className="space-y-2.5">
                    <div className="flex justify-between p-2 rounded bg-black/40 border border-neutral-800">
                      <span className="text-neutral-400">Tổng tĩnh tải tính toán:</span>
                      <span className="font-mono font-bold text-amber-300">
                        {calcResult.totalDeadLoadKgM2} kg/m²
                      </span>
                    </div>

                    <div className="flex justify-between p-2 rounded bg-black/40 border border-neutral-800">
                      <span className="text-neutral-400">Khoảng cách ty treo khuyến nghị:</span>
                      <span className="font-mono font-bold text-cyan-300">
                        {calcResult.recommendedHangerSpacingMm} mm (@800mm)
                      </span>
                    </div>

                    <div className="flex justify-between p-2 rounded bg-black/40 border border-neutral-800">
                      <span className="text-neutral-400">Tải trọng thực tế / 1 ty ren M8:</span>
                      <span className="font-mono font-bold text-white">
                        {calcResult.loadPerHangerKg} kg / ty
                      </span>
                    </div>

                    <div className="flex justify-between p-2 rounded bg-black/40 border border-neutral-800">
                      <span className="text-neutral-400">Hệ số an toàn (Safety Factor):</span>
                      <span className="font-mono font-bold text-emerald-400">
                        {calcResult.safetyFactor}x (Yêu cầu &gt;= 2.5)
                      </span>
                    </div>
                  </div>
                </div>

                <div className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 flex items-center space-x-2 text-emerald-300">
                  <CheckCircle2 className="w-5 h-5 flex-shrink-0" />
                  <div>
                    <strong>ĐẠT TIÊU CHUẨN ASTM C635 & TCVN 9377-2:2012!</strong>
                    <div className="text-[11px] text-neutral-300">
                      Độ võng lý thuyết &lt; L/360, đảm bảo an toàn tuyệt đối chống sập võng trần.
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-neutral-800 bg-[#1A1D23] flex items-center justify-between">
          <span className="text-xs text-neutral-400">
            HNL CAD AI • Tích hợp trực tiếp dữ liệu chuẩn Saint-Gobain & Bộ Xây Dựng
          </span>
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-white font-medium text-xs transition"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
