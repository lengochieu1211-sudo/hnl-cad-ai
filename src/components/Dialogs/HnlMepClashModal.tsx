import React, { useState } from "react";
import { CadEntity, MepClashIssue, MepElement } from "../../types/cad";
import {
  SAMPLE_MEP_ELEMENTS,
  runMepClashDetection,
} from "../../lib/mepClashEngine";
import {
  AlertTriangle,
  CheckCircle2,
  Wrench,
  X,
  Zap,
  Activity,
  ShieldAlert,
  Flame,
  Lightbulb,
  Fan,
  HelpCircle,
  PlusCircle,
} from "lucide-react";

interface HnlMepClashModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
}

export const HnlMepClashModal: React.FC<HnlMepClashModalProps> = ({
  isOpen,
  onClose,
  entities,
}) => {
  const [mepList, setMepList] = useState<MepElement[]>(SAMPLE_MEP_ELEMENTS);
  const [resolvedIds, setResolvedIds] = useState<Set<string>>(new Set());
  const [filterSeverity, setFilterSeverity] = useState<string>("ALL");

  if (!isOpen) return null;

  const rawIssues = runMepClashDetection({ mepElements: mepList, entities });
  const issues = rawIssues.map((iss) => ({
    ...iss,
    isResolved: resolvedIds.has(iss.id),
  }));

  const handleResolveIssue = (issueId: string) => {
    setResolvedIds((prev) => new Set([...prev, issueId]));
  };

  const handleAutoFixAll = () => {
    const allIds = issues.map((i) => i.id);
    setResolvedIds(new Set(allIds));
  };

  const filteredIssues = issues.filter((iss) => {
    if (filterSeverity === "ALL") return true;
    if (filterSeverity === "UNRESOLVED") return !iss.isResolved;
    return iss.severity === filterSeverity;
  });

  const highCount = issues.filter((i) => i.severity === "HIGH" && !i.isResolved).length;
  const mediumCount = issues.filter((i) => i.severity === "MEDIUM" && !i.isResolved).length;
  const resolvedCount = issues.filter((i) => i.isResolved).length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 animate-in fade-in">
      <div className="w-full max-w-4xl bg-[#141619] border border-neutral-700 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-neutral-800 bg-[#1A1D23]">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-xl bg-amber-500/20 text-amber-400 border border-amber-500/30">
              <Zap className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>HNL MEP & Ceiling Clash Audit (Kiểm Tra Va Chạm Cơ Điện & Khung Trần)</span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300 font-mono">
                  ASTM C636
                </span>
              </h2>
              <p className="text-xs text-neutral-400">
                Tự động quét xung đột vị trí giữa Khung Xương Trần/Vách với Miệng Gió, Đèn Panel, Đầu Báo Cháy & Ống Gió
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

        {/* Overview Metric Cards */}
        <div className="grid grid-cols-4 gap-3 p-4 bg-[#181A1F] border-b border-neutral-800 text-xs">
          <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20">
            <div className="text-red-400 font-bold flex items-center justify-between">
              <span>Nghiêm trọng (High)</span>
              <ShieldAlert className="w-4 h-4" />
            </div>
            <div className="text-xl font-bold font-mono text-white mt-1">{highCount}</div>
            <div className="text-[10px] text-neutral-400">Cắt đứt xương trần chính</div>
          </div>

          <div className="p-3 rounded-xl bg-amber-500/10 border border-amber-500/20">
            <div className="text-amber-400 font-bold flex items-center justify-between">
              <span>Cảnh báo (Medium)</span>
              <AlertTriangle className="w-4 h-4" />
            </div>
            <div className="text-xl font-bold font-mono text-white mt-1">{mediumCount}</div>
            <div className="text-[10px] text-neutral-400">Xuyên vách chống cháy</div>
          </div>

          <div className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20">
            <div className="text-emerald-400 font-bold flex items-center justify-between">
              <span>Đã Xử Lý (Resolved)</span>
              <CheckCircle2 className="w-4 h-4" />
            </div>
            <div className="text-xl font-bold font-mono text-white mt-1">{resolvedCount}</div>
            <div className="text-[10px] text-neutral-400">Đã chèn khung gia cường</div>
          </div>

          <div className="p-3 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex flex-col justify-between">
            <div className="text-cyan-400 font-bold text-[11px]">Auto-Fix Toàn Bộ</div>
            <button
              onClick={handleAutoFixAll}
              className="w-full py-1.5 bg-cyan-500 hover:bg-cyan-400 text-black font-bold rounded-lg text-xs transition shadow-md shadow-cyan-500/20"
            >
              Chèn Khung Bo Viền (All)
            </button>
          </div>
        </div>

        {/* Issue List */}
        <div className="flex-1 overflow-y-auto p-6 space-y-3">
          {filteredIssues.length === 0 ? (
            <div className="py-12 text-center text-neutral-500 space-y-2">
              <CheckCircle2 className="w-12 h-12 text-emerald-400 mx-auto opacity-80" />
              <div className="text-sm font-bold text-neutral-300">Không còn va chạm cơ điện nào tồn tại!</div>
              <div className="text-xs">Hệ thống khung xương trần và thiết bị MEP đã hoàn toàn đồng bộ theo ASTM C636.</div>
            </div>
          ) : (
            filteredIssues.map((issue) => (
              <div
                key={issue.id}
                className={`p-4 rounded-xl border transition flex items-start justify-between space-x-4 ${
                  issue.isResolved
                    ? "bg-emerald-950/10 border-emerald-500/30 opacity-70"
                    : issue.severity === "HIGH"
                    ? "bg-red-950/20 border-red-500/40"
                    : "bg-amber-950/20 border-amber-500/40"
                }`}
              >
                <div className="space-y-1.5 flex-1 text-xs">
                  <div className="flex items-center space-x-2">
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold font-mono ${
                        issue.severity === "HIGH"
                          ? "bg-red-500 text-white"
                          : "bg-amber-500 text-black"
                      }`}
                    >
                      {issue.severity}
                    </span>
                    <span className="font-bold text-white text-sm">{issue.mepName}</span>
                    <span className="text-neutral-500 font-mono text-[10px]">
                      X:{issue.location.x} Y:{issue.location.y}
                    </span>
                  </div>

                  <div className="text-neutral-300">{issue.description}</div>

                  <div className="p-2.5 rounded-lg bg-black/40 border border-neutral-800 text-[11px] text-cyan-300 flex items-start space-x-2">
                    <Wrench className="w-3.5 h-3.5 mt-0.5 text-cyan-400 flex-shrink-0" />
                    <div>
                      <strong className="text-white">Giải pháp gia cường: </strong>
                      {issue.requiredReinforcement}
                    </div>
                  </div>
                </div>

                <div>
                  {issue.isResolved ? (
                    <span className="flex items-center space-x-1 text-emerald-400 text-xs font-bold px-3 py-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      <span>Đã gia cường</span>
                    </span>
                  ) : (
                    <button
                      onClick={() => handleResolveIssue(issue.id)}
                      className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-cyan-500 hover:bg-cyan-400 text-black font-bold text-xs transition shadow-md shadow-cyan-500/20"
                    >
                      <PlusCircle className="w-3.5 h-3.5" />
                      <span>Thêm Khung Bo Viền</span>
                    </button>
                  )}
                </div>
              </div>
            ))
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-neutral-800 bg-[#1A1D23] flex items-center justify-between">
          <span className="text-xs text-neutral-400">
            Tiêu chuẩn áp dụng: ASTM C636 / TCVN 9377-2:2012 / QCVN 06:2022/BXD
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
