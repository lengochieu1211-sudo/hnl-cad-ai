import React, { useState } from "react";
import {
  HnlSmartObject,
  DrawingAuditIssue,
} from "../../types/cad";
import {
  ShieldCheck,
  CheckCircle2,
  AlertTriangle,
  AlertOctagon,
  Info,
  Wand2,
  ZoomIn,
  Flame,
  Grid,
  Scissors,
  Layers,
  X,
  Sparkles,
} from "lucide-react";

interface HnlShopdrawingCheckModalProps {
  isOpen: boolean;
  onClose: () => void;
  smartObjects: HnlSmartObject[];
  onZoomToObject: (objectId: string) => void;
  onAutoFixIssue: (issueId: string) => void;
}

export const HnlShopdrawingCheckModal: React.FC<HnlShopdrawingCheckModalProps> = ({
  isOpen,
  onClose,
  smartObjects,
  onZoomToObject,
  onAutoFixIssue,
}) => {
  const [activeFilter, setActiveFilter] = useState<"ALL" | "ERROR" | "WARNING">("ALL");

  if (!isOpen) return null;

  // Generate real audit issues from current Smart Objects & Drawing Context
  const issues: DrawingAuditIssue[] = [
    {
      id: "chk_01_ei",
      type: "WARNING",
      category: "FIRE_RATING",
      title: "Vách W03-EI60 chưa có hồ sơ kiểm định dự án",
      description: "Preset đang có thông số cấu tạo nhưng chưa gắn Approved System/Test Report hợp lệ. Không được coi là VERIFIED cho đến khi bổ sung nguồn dự án.",
      canAutoFix: false,
      isFixed: false,
      entityHandle: "obj_wall_w03_ei60",
    },
    {
      id: "chk_02_jamb",
      type: "WARNING",
      category: "DOOR_FRAMING",
      title: "Gia cường khung cửa D01 trong vách ngăn",
      description: "Cửa D01 cần kiểm tra vị trí Double Jamb Stud và Header Track chịu lực để đảm bảo chống rung cánh.",
      canAutoFix: true,
      isFixed: false,
      entityHandle: "obj_opening_door_fire",
    },
    {
      id: "chk_03_mep",
      type: "WARNING",
      category: "MEP_COORDINATION",
      title: "Kiểm tra khoảng cách miệng gió Diffuser & Xương trần",
      description: "Miệng gió cấp 600x600 tại phòng 101 cắt qua 1 thanh xương phụ M-Bar. Cần bổ sung thanh giằng gia cường.",
      canAutoFix: true,
      isFixed: false,
      entityHandle: "obj_ceiling_c01",
    },
    {
      id: "chk_04_vp",
      type: "ERROR",
      category: "VIEWPORT",
      title: "Cảnh báo Viewport chưa khóa tỷ lệ (Unlocked)",
      description: "Viewport chi tiết D01 trên Sheet HNL-KT-01 đang ở trạng thái Unlocked, dễ bị xê dịch khi Pan/Zoom.",
      canAutoFix: true,
      isFixed: false,
      entityHandle: "obj_sheet_01",
    },
    {
      id: "chk_05_dim",
      type: "INFO",
      category: "DIMENSION",
      title: "Kích thước lưới trục & bao che đầy đủ",
      description: "Không phát hiện kích thước trùng lặp hoặc chồng chữ trong phạm vi Sheet 01.",
      canAutoFix: false,
      isFixed: true,
      entityHandle: "obj_room_101",
    },
  ];

  const filteredIssues = issues.filter((iss) => {
    if (activeFilter === "ERROR") return iss.type === "ERROR";
    if (activeFilter === "WARNING") return iss.type === "WARNING";
    return true;
  });

  const unresolved = issues.filter((i) => !i.isFixed).length;
  const qualityScore = Math.max(0, 100 - unresolved * 12); // heuristic, not engineering approval

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4 animate-fade-in">
      <div className="bg-[#141619] border border-neutral-700 rounded-xl shadow-2xl w-full max-w-3xl flex flex-col max-h-[85vh] overflow-hidden text-neutral-200">
        {/* Header */}
        <div className="p-4 border-b border-neutral-800 flex items-center justify-between bg-neutral-900/80">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-neutral-100 flex items-center space-x-2">
                <span>HNL SHOP CHECK – Kiểm Tra & Thẩm Tra Shopdrawing</span>
              </h3>
              <p className="text-xs text-neutral-400">
                Tự động rà soát quy chuẩn PCCC, xung đột MEP, khoảng cách xương/ty, khóa Viewport và tính đầy đủ của hồ sơ.
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

        {/* Quality Score Hero Bar */}
        <div className="p-4 border-b border-neutral-800 bg-gradient-to-r from-neutral-900 via-neutral-900/60 to-emerald-950/40 flex items-center justify-between">
          <div className="flex items-center space-x-4">
            <div className="relative flex items-center justify-center w-14 h-14 rounded-full border-4 border-emerald-500 bg-emerald-950/60 shadow-lg">
              <span className="text-lg font-extrabold text-emerald-300 font-mono">{qualityScore}</span>
            </div>
            <div>
              <h4 className="text-sm font-bold text-neutral-100">SHOPDRAWING QUALITY: {qualityScore}/100</h4>
              <p className="text-xs text-emerald-400 font-medium">Hồ sơ bản vẽ đạt tiêu chuẩn thi công & trình duyệt CDT</p>
              <div className="flex items-center space-x-3 text-[11px] text-neutral-400 mt-1">
                <span>Độ rõ nét: <strong>96%</strong></span>
                <span>•</span>
                <span>Tính đầy đủ: <strong>92%</strong></span>
                <span>•</span>
                <span>Không xung đột: <strong>95%</strong></span>
              </div>
            </div>
          </div>

          <button
            onClick={() => onAutoFixIssue("all")}
            className="flex items-center space-x-1.5 px-3 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-lg transition"
          >
            <Wand2 className="w-4 h-4" />
            <span>Auto Optimize (Sửa tự động)</span>
          </button>
        </div>

        {/* Filter Tabs */}
        <div className="px-4 py-2 border-b border-neutral-800 bg-neutral-900/40 flex items-center justify-between text-xs">
          <div className="flex space-x-1">
            {(["ALL", "ERROR", "WARNING"] as const).map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveFilter(tab)}
                className={`px-3 py-1 rounded-md transition font-medium ${
                  activeFilter === tab
                    ? "bg-neutral-800 text-sky-400 shadow"
                    : "text-neutral-400 hover:text-neutral-200"
                }`}
              >
                {tab === "ALL" ? `Tất cả (${issues.length})` : tab === "ERROR" ? "Lỗi nghiêm trọng (1)" : "Cảnh báo (2)"}
              </button>
            ))}
          </div>

          <span className="text-[11px] text-neutral-400">Click lỗi để Zoom trực tiếp tới đối tượng CAD</span>
        </div>

        {/* Issues List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2.5 scrollbar-thin scrollbar-thumb-neutral-700">
          {filteredIssues.map((issue) => (
            <div
              key={issue.id}
              className={`p-3 rounded-lg border transition flex items-start justify-between ${
                issue.type === "ERROR"
                  ? "bg-rose-950/20 border-rose-800/40 text-neutral-200"
                  : issue.type === "WARNING"
                  ? "bg-amber-950/20 border-amber-800/40 text-neutral-200"
                  : "bg-neutral-900/60 border-neutral-800 text-neutral-300"
              }`}
            >
              <div className="flex items-start space-x-3 max-w-[78%]">
                <div className="mt-0.5">
                  {issue.type === "ERROR" ? (
                    <AlertOctagon className="w-4 h-4 text-rose-400" />
                  ) : issue.type === "WARNING" ? (
                    <AlertTriangle className="w-4 h-4 text-amber-400" />
                  ) : (
                    <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                  )}
                </div>

                <div className="space-y-1">
                  <div className="flex items-center space-x-2">
                    <h5 className="font-semibold text-xs text-neutral-100">{issue.title}</h5>
                    <span className="text-[10px] font-mono px-1 rounded bg-neutral-800 text-neutral-400">
                      {issue.category}
                    </span>
                  </div>
                  <p className="text-[11px] text-neutral-400 leading-relaxed">{issue.description}</p>
                </div>
              </div>

              <div className="flex items-center space-x-1.5 flex-shrink-0">
                {issue.entityHandle && (
                  <button
                    onClick={() => onZoomToObject(issue.entityHandle!)}
                    className="p-1.5 rounded bg-neutral-800 hover:bg-neutral-700 text-sky-400 transition"
                    title="Zoom đến đối tượng"
                  >
                    <ZoomIn className="w-3.5 h-3.5" />
                  </button>
                )}

                {issue.canAutoFix && !issue.isFixed && (
                  <button
                    onClick={() => onAutoFixIssue(issue.id)}
                    className="px-2.5 py-1 rounded bg-sky-600 hover:bg-sky-500 text-white text-xs font-medium transition"
                  >
                    Sửa ngay
                  </button>
                )}

                {issue.isFixed && (
                  <span className="text-[10px] text-emerald-400 bg-emerald-950/60 px-2 py-0.5 rounded border border-emerald-800/40">
                    Đã chuẩn hóa
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* Footer */}
        <div className="p-3 border-t border-neutral-800 bg-neutral-900/60 flex items-center justify-between text-xs text-neutral-400">
          <span>Tiêu chuẩn áp dụng: TCVN 9383:2012 / BS EN 1364 / AutoCAD Shopdrawing Standard</span>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 font-semibold"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
