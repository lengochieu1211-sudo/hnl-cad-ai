import React from "react";
import {
  HnlSmartObject,
  DependencyEdge,
} from "../../types/cad";
import {
  GitCommit,
  ArrowRight,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Cpu,
  Layers,
  Zap,
} from "lucide-react";

interface HnlDependencyPanelProps {
  smartObjects: HnlSmartObject[];
  dependencyEdges: DependencyEdge[];
  onRecomputeAll: () => void;
  onSelectObject: (id: string) => void;
}

export const HnlDependencyPanel: React.FC<HnlDependencyPanelProps> = ({
  smartObjects,
  dependencyEdges,
  onRecomputeAll,
  onSelectObject,
}) => {
  const dirtyObjects = smartObjects.filter((o) => o.dirtyFlag);

  return (
    <div className="h-full flex flex-col bg-[#16181b] text-neutral-300 border-r border-neutral-800">
      {/* Header */}
      <div className="p-2.5 border-b border-neutral-800 bg-neutral-900/60 flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <Cpu className="w-4 h-4 text-sky-400" />
          <span className="font-semibold text-xs text-neutral-200 tracking-wide uppercase">
            HNL Dependency Graph & Recompute
          </span>
        </div>

        <button
          onClick={onRecomputeAll}
          className="flex items-center space-x-1 px-2.5 py-1 rounded bg-sky-500 hover:bg-sky-600 text-white text-xs font-semibold shadow transition"
        >
          <RefreshCw className={`w-3 h-3 ${dirtyObjects.length > 0 ? "animate-spin" : ""}`} />
          <span>Recompute DAG ({dirtyObjects.length})</span>
        </button>
      </div>

      {/* Main Content */}
      <div className="flex-1 overflow-y-auto p-3 space-y-3 scrollbar-thin scrollbar-thumb-neutral-700">
        {/* Dirty Warning Alert */}
        {dirtyObjects.length > 0 ? (
          <div className="p-2.5 rounded bg-amber-950/40 border border-amber-500/40 text-xs space-y-1">
            <div className="flex items-center space-x-1.5 text-amber-300 font-semibold">
              <AlertTriangle className="w-4 h-4 text-amber-400" />
              <span>Có {dirtyObjects.length} đối tượng cần tính lại (Dirty Objects)</span>
            </div>
            <p className="text-[11px] text-neutral-300">
              Các đối tượng gốc (Room/Boundary) đã thay đổi. Nhấn Recompute để cập nhật lại hệ khung xương, ty treo, thống kê BOQ và chi tiết Shopdrawing.
            </p>
          </div>
        ) : (
          <div className="p-2.5 rounded bg-emerald-950/40 border border-emerald-500/40 text-xs flex items-center space-x-2 text-emerald-300">
            <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            <span>Dependency Graph hiện không có đối tượng cần tính lại.</span>
          </div>
        )}

        {/* FreeCAD-inspired Flow Diagram Card */}
        <div className="bg-neutral-900/50 p-2.5 rounded border border-neutral-800 space-y-2">
          <h5 className="text-[11px] font-semibold text-neutral-400 uppercase tracking-wider flex items-center space-x-1.5">
            <Zap className="w-3.5 h-3.5 text-amber-400" />
            <span>Luồng Tính Toán Phụ Thuộc (DAG Flow)</span>
          </h5>

          <div className="flex flex-col space-y-1.5 text-xs font-mono">
            <div className="flex items-center space-x-1 text-sky-300 bg-neutral-950 px-2 py-1 rounded border border-neutral-800">
              <span className="w-2 h-2 rounded-full bg-sky-400" />
              <span>Room Boundary (Mặt bằng)</span>
              <ArrowRight className="w-3 h-3 text-neutral-500 ml-auto" />
            </div>
            <div className="flex items-center space-x-1 text-cyan-300 bg-neutral-950 px-2 py-1 rounded border border-neutral-800 ml-3">
              <span className="w-2 h-2 rounded-full bg-cyan-400" />
              <span>Ceiling / Wall Smart Object</span>
              <ArrowRight className="w-3 h-3 text-neutral-500 ml-auto" />
            </div>
            <div className="flex items-center space-x-1 text-emerald-300 bg-neutral-950 px-2 py-1 rounded border border-neutral-800 ml-6">
              <span className="w-2 h-2 rounded-full bg-emerald-400" />
              <span>Framing & Hanger Generation</span>
              <ArrowRight className="w-3 h-3 text-neutral-500 ml-auto" />
            </div>
            <div className="flex items-center space-x-1 text-purple-300 bg-neutral-950 px-2 py-1 rounded border border-neutral-800 ml-9">
              <span className="w-2 h-2 rounded-full bg-purple-400" />
              <span>Quantity / BOQ & Schedules</span>
              <ArrowRight className="w-3 h-3 text-neutral-500 ml-auto" />
            </div>
            <div className="flex items-center space-x-1 text-rose-300 bg-neutral-950 px-2 py-1 rounded border border-neutral-800 ml-12">
              <span className="w-2 h-2 rounded-full bg-rose-400" />
              <span>Auto Detail, Section & Layout</span>
            </div>
          </div>
        </div>

        {/* List of Active Dependency Edges */}
        <div className="space-y-1.5">
          <h5 className="text-[11px] font-semibold text-neutral-400 uppercase tracking-wider">
            Các Liên Kết Phụ Thuộc Hiện Tại ({dependencyEdges.length})
          </h5>

          <div className="space-y-1 max-h-56 overflow-y-auto scrollbar-thin scrollbar-thumb-neutral-700">
            {dependencyEdges.map((edge, idx) => {
              const fromObj = smartObjects.find((o) => o.id === edge.fromId);
              const toObj = smartObjects.find((o) => o.id === edge.toId);

              return (
                <div
                  key={idx}
                  className="flex items-center justify-between p-1.5 rounded bg-neutral-900/70 border border-neutral-800 text-xs hover:border-neutral-700 transition"
                >
                  <div className="flex items-center space-x-1.5 truncate max-w-[70%]">
                    <button
                      onClick={() => onSelectObject(edge.fromId)}
                      className="text-sky-400 hover:underline font-mono truncate max-w-[80px]"
                      title={fromObj?.name}
                    >
                      {fromObj?.name.slice(0, 10) || edge.fromId}
                    </button>
                    <ArrowRight className="w-3 h-3 text-neutral-500 flex-shrink-0" />
                    <button
                      onClick={() => onSelectObject(edge.toId)}
                      className="text-emerald-400 hover:underline font-mono truncate max-w-[80px]"
                      title={toObj?.name}
                    >
                      {toObj?.name.slice(0, 10) || edge.toId}
                    </button>
                  </div>

                  <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-400 font-mono">
                    {edge.dependencyType}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
};
