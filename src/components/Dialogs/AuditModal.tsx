import React, { useState } from "react";
import { AlertTriangle, CheckCircle, ShieldCheck, RefreshCw, X, Sparkles, AlertCircle } from "lucide-react";
import { DrawingAuditIssue } from "../../types/cad";

interface AuditModalProps {
  isOpen: boolean;
  onClose: () => void;
  issues: DrawingAuditIssue[];
  onFixIssue: (issueId: string) => void;
  onFixAllIssues: () => void;
}

export const AuditModal: React.FC<AuditModalProps> = ({
  isOpen,
  onClose,
  issues,
  onFixIssue,
  onFixAllIssues,
}) => {
  const [filterType, setFilterType] = useState<"ALL" | "ERROR" | "WARNING">("ALL");

  if (!isOpen) return null;

  const filteredIssues = issues.filter((iss) => {
    if (filterType === "ERROR") return iss.type === "ERROR";
    if (filterType === "WARNING") return iss.type === "WARNING";
    return true;
  });

  const errorCount = issues.filter((i) => i.type === "ERROR").length;
  const warnCount = issues.filter((i) => i.type === "WARNING").length;

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-3xl max-h-[85vh] bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-amber-500/20 border border-amber-500/40 flex items-center justify-center text-amber-400">
              <AlertTriangle className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">KIỂM TRA LỖI BẢN VẼ (CAD AUDIT & STANDARDS)</h2>
              <p className="text-xs text-neutral-400">
                Phát hiện Field hỏng (####), text chồng chéo, font lỗi TCVN3/VNI, Viewport chưa khóa
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

        {/* Summary Stats & Filters */}
        <div className="p-4 bg-[#18191C] border-b border-neutral-800 flex items-center justify-between text-xs">
          <div className="flex items-center space-x-2">
            <button
              onClick={() => setFilterType("ALL")}
              className={`px-3 py-1 rounded-md transition ${
                filterType === "ALL" ? "bg-neutral-700 text-white font-semibold" : "text-neutral-400 hover:text-white"
              }`}
            >
              Tất cả ({issues.length})
            </button>
            <button
              onClick={() => setFilterType("ERROR")}
              className={`px-3 py-1 rounded-md transition ${
                filterType === "ERROR"
                  ? "bg-red-500/20 text-red-400 font-semibold border border-red-500/40"
                  : "text-neutral-400 hover:text-white"
              }`}
            >
              Lỗi nghiêm trọng ({errorCount})
            </button>
            <button
              onClick={() => setFilterType("WARNING")}
              className={`px-3 py-1 rounded-md transition ${
                filterType === "WARNING"
                  ? "bg-amber-500/20 text-amber-400 font-semibold border border-amber-500/40"
                  : "text-neutral-400 hover:text-white"
              }`}
            >
              Cảnh báo ({warnCount})
            </button>
          </div>

          {issues.length > 0 && (
            <button
              onClick={onFixAllIssues}
              className="px-4 py-1.5 rounded-lg bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white font-semibold flex items-center space-x-1.5 shadow transition"
            >
              <Sparkles className="w-3.5 h-3.5" />
              <span>Sửa toàn bộ lỗi tự động (Auto-Fix All)</span>
            </button>
          )}
        </div>

        {/* Issues List */}
        <div className="flex-1 p-4 overflow-y-auto space-y-2.5 bg-[#141517]">
          {filteredIssues.length === 0 ? (
            <div className="py-12 flex flex-col items-center justify-center text-center space-y-3">
              <div className="w-12 h-12 rounded-full bg-emerald-500/20 border border-emerald-500/40 flex items-center justify-center text-emerald-400">
                <ShieldCheck className="w-6 h-6" />
              </div>
              <p className="font-semibold text-neutral-200 text-sm">Không còn lỗi trong phạm vi bộ kiểm tra HNL hiện tại.</p>
              <p className="text-xs text-neutral-400">Không phát hiện lỗi thuộc các rule đang bật. Đây không phải chứng nhận bản vẽ đạt toàn bộ tiêu chuẩn kỹ thuật.</p>
            </div>
          ) : (
            filteredIssues.map((iss) => (
              <div
                key={iss.id}
                className={`p-3 rounded-lg border flex items-start justify-between space-x-3 ${
                  iss.type === "ERROR"
                    ? "bg-red-500/10 border-red-500/30 text-red-200"
                    : iss.type === "WARNING"
                    ? "bg-amber-500/10 border-amber-500/30 text-amber-200"
                    : "bg-blue-500/10 border-blue-500/30 text-blue-200"
                }`}
              >
                <div className="space-y-1">
                  <div className="flex items-center space-x-2">
                    <span className="font-bold text-xs">{iss.title}</span>
                    <span className="text-[10px] font-mono px-1.5 py-0.2 rounded bg-black/40">
                      {iss.category}
                    </span>
                    {iss.affectedEntityHandles && (
                      <span className="text-[10px] text-neutral-400 font-mono">
                        Handles: {iss.affectedEntityHandles.join(", ")}
                      </span>
                    )}
                  </div>
                  <p className="text-xs opacity-90">{iss.description}</p>
                </div>

                {iss.canAutoFix && (
                  <button
                    onClick={() => onFixIssue(iss.id)}
                    className="px-3 py-1.5 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/40 text-xs font-semibold shrink-0 transition"
                  >
                    Sửa ngay
                  </button>
                )}
              </div>
            ))
          )}
        </div>

        {/* Footer */}
        <div className="h-14 px-6 bg-[#141517] border-t border-neutral-800 flex items-center justify-between text-xs text-neutral-400">
          <span>Tiêu chuẩn dự án: <strong>TCVN 9377:2012 / HNL CAD Standard</strong></span>
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
