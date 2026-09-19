import React, { useState, useMemo } from "react";
import {
  ProjectTreeNode,
  HnlSmartObject,
  CadLayer,
  CadEntity,
} from "../../types/cad";
import { INITIAL_LAYERS } from "../../lib/initialData";
import {
  FolderTree,
  ChevronRight,
  ChevronDown,
  Layers,
  Box,
  Flame,
  Grid,
  Scissors,
  FileText,
  AlertTriangle,
  CheckCircle2,
  HelpCircle,
  Plus,
  RefreshCw,
  Eye,
  EyeOff,
  Lock,
  Unlock,
  Sliders,
  Zap,
  Search,
  X,
  Printer,
} from "lucide-react";

interface HnlProjectTreePanelProps {
  treeRoot: ProjectTreeNode;
  smartObjects: HnlSmartObject[];
  selectedObjectId: string | null;
  onSelectObject: (id: string | null) => void;
  onRecomputeAll: () => void;
  onRecomputeObject: (id: string) => void;
  onAddNewSmartObject: (type: "CEILING" | "WALL" | "DETAIL" | "SECTION") => void;
  layers?: CadLayer[];
  entities?: CadEntity[];
  onToggleLayerVisible?: (layerName: string) => void;
  onToggleLayerLock?: (layerName: string) => void;
  onToggleAllLayersVisible?: (visible: boolean) => void;
  onToggleAllLayersLock?: (locked: boolean) => void;
}

export const HnlProjectTreePanel: React.FC<HnlProjectTreePanelProps> = ({
  treeRoot,
  smartObjects,
  selectedObjectId,
  onSelectObject,
  onRecomputeAll,
  onRecomputeObject,
  onAddNewSmartObject,
  layers = INITIAL_LAYERS,
  entities = [],
  onToggleLayerVisible,
  onToggleLayerLock,
  onToggleAllLayersVisible,
  onToggleAllLayersLock,
}) => {
  const [expandedNodes, setExpandedNodes] = useState<Record<string, boolean>>({
    root_project: true,
    floor_01_node: true,
    cat_ceiling: true,
    cat_wall: true,
    cat_details: true,
    cat_sections: true,
    cat_layouts: true,
    node_obj_ceiling_c01: true,
    node_obj_wall_w03_ei60: true,
  });

  // Quick Actions Section State
  const [isQuickActionsOpen, setIsQuickActionsOpen] = useState<boolean>(true);
  const [isAddMenuOpen, setIsAddMenuOpen] = useState<boolean>(false);
  const [layerSearchQuery, setLayerSearchQuery] = useState<string>("");

  // Internal layer state fallback if callbacks are not passed
  const [localLayers, setLocalLayers] = useState<CadLayer[]>(layers);

  const activeLayers = layers && layers.length > 0 ? layers : localLayers;

  const handleToggleVisible = (layerName: string) => {
    if (onToggleLayerVisible) {
      onToggleLayerVisible(layerName);
    } else {
      setLocalLayers((prev) =>
        prev.map((l) => (l.name === layerName ? { ...l, isVisible: !l.isVisible } : l))
      );
    }
  };

  const handleToggleLock = (layerName: string) => {
    if (onToggleLayerLock) {
      onToggleLayerLock(layerName);
    } else {
      setLocalLayers((prev) =>
        prev.map((l) => (l.name === layerName ? { ...l, isLocked: !l.isLocked } : l))
      );
    }
  };

  const handleToggleAllVisible = (visible: boolean) => {
    if (onToggleAllLayersVisible) {
      onToggleAllLayersVisible(visible);
    } else {
      setLocalLayers((prev) => prev.map((l) => ({ ...l, isVisible: visible })));
    }
  };

  const handleToggleAllLock = (locked: boolean) => {
    if (onToggleAllLayersLock) {
      onToggleAllLayersLock(locked);
    } else {
      setLocalLayers((prev) => prev.map((l) => ({ ...l, isLocked: locked })));
    }
  };

  const toggleExpand = (nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setExpandedNodes((prev) => ({ ...prev, [nodeId]: !prev[nodeId] }));
  };

  const dirtyCount = smartObjects.filter((o) => o.dirtyFlag).length;

  // Compute entity count per layer
  const entityCountByLayer = useMemo(() => {
    const counts: Record<string, number> = {};
    entities.forEach((ent) => {
      counts[ent.layer] = (counts[ent.layer] || 0) + 1;
    });
    return counts;
  }, [entities]);

  // Filtered layers based on search query
  const filteredLayers = useMemo(() => {
    if (!layerSearchQuery.trim()) return activeLayers;
    const q = layerSearchQuery.toLowerCase();
    return activeLayers.filter((l) => l.name.toLowerCase().includes(q));
  }, [activeLayers, layerSearchQuery]);

  const visibleLayerCount = activeLayers.filter((l) => l.isVisible).length;
  const lockedLayerCount = activeLayers.filter((l) => l.isLocked).length;

  const renderNodeIcon = (node: ProjectTreeNode) => {
    if (node.type === "PROJECT") return <FolderTree className="w-4 h-4 text-sky-400" />;
    if (node.type === "FLOOR") return <Layers className="w-4 h-4 text-emerald-400" />;
    if (node.type === "CATEGORY") {
      if (node.categoryKey === "Ceiling") return <Grid className="w-4 h-4 text-cyan-400" />;
      if (node.categoryKey === "Wall") return <Flame className="w-4 h-4 text-amber-400" />;
      if (node.categoryKey === "Details") return <Scissors className="w-4 h-4 text-purple-400" />;
      if (node.categoryKey === "Sections") return <Sliders className="w-4 h-4 text-blue-400" />;
      if (node.categoryKey === "Layouts") return <FileText className="w-4 h-4 text-rose-400" />;
      return <Box className="w-4 h-4 text-neutral-400" />;
    }
    if (node.type === "SMART_OBJECT") {
      if (node.categoryKey === "Wall") return <Flame className="w-3.5 h-3.5 text-amber-400" />;
      return <Box className="w-3.5 h-3.5 text-sky-400" />;
    }
    return <div className="w-1.5 h-1.5 rounded-full bg-neutral-500 ml-1 mr-0.5" />;
  };

  const renderStatusBadge = (status?: "VERIFIED" | "NEEDS_CONFIRMATION" | "CONFLICT") => {
    if (status === "VERIFIED") {
      return (
        <span className="flex items-center text-[10px] text-emerald-400 bg-emerald-950/60 px-1 rounded border border-emerald-800/40" title="Đã có chứng chỉ kiểm định">
          <CheckCircle2 className="w-2.5 h-2.5 mr-0.5" />
          VERIFIED
        </span>
      );
    }
    if (status === "NEEDS_CONFIRMATION") {
      return (
        <span className="flex items-center text-[10px] text-amber-400 bg-amber-950/60 px-1 rounded border border-amber-800/40" title="AI đề xuất - Cần kỹ sư xác nhận">
          <HelpCircle className="w-2.5 h-2.5 mr-0.5" />
          CONFIRM
        </span>
      );
    }
    if (status === "CONFLICT") {
      return (
        <span className="flex items-center text-[10px] text-rose-400 bg-rose-950/60 px-1 rounded border border-rose-800/40" title="Cảnh báo xung đột hoặc chưa có chứng nhận">
          <AlertTriangle className="w-2.5 h-2.5 mr-0.5" />
          CONFLICT
        </span>
      );
    }
    return null;
  };

  const renderTree = (node: ProjectTreeNode, depth: number = 0) => {
    const hasChildren = node.children && node.children.length > 0;
    const isExpanded = expandedNodes[node.id] ?? false;
    const isSelected = node.smartObjectId === selectedObjectId;

    // Find layer associated with smart object if applicable
    const smartObj = smartObjects.find((so) => so.id === node.smartObjectId);
    const layerObj = smartObj ? activeLayers.find((l) => l.name === smartObj.layer) : null;

    return (
      <div key={node.id} className="select-none">
        <div
          onClick={() => {
            if (node.smartObjectId) {
              onSelectObject(node.smartObjectId);
            }
          }}
          className={`flex items-center justify-between py-1 px-1.5 rounded cursor-pointer transition text-xs group ${
            isSelected
              ? "bg-sky-500/25 text-sky-200 border border-sky-500/40"
              : "hover:bg-neutral-800/70 text-neutral-300"
          }`}
          style={{ paddingLeft: `${depth * 14 + 6}px` }}
        >
          <div className="flex items-center space-x-1.5 truncate">
            {hasChildren ? (
              <button
                onClick={(e) => toggleExpand(node.id, e)}
                className="p-0.5 hover:bg-neutral-700 rounded text-neutral-400 hover:text-neutral-200 transition"
              >
                {isExpanded ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5" />}
              </button>
            ) : (
              <span className="w-3.5" />
            )}

            {renderNodeIcon(node)}

            <span className={`truncate font-mono ${node.type === "PROJECT" || node.type === "FLOOR" ? "font-bold text-neutral-100" : ""}`}>
              {node.label}
            </span>
          </div>

          <div className="flex items-center space-x-1 flex-shrink-0 ml-2">
            {layerObj && (
              <div className="flex items-center space-x-0.5 opacity-80 group-hover:opacity-100">
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleToggleVisible(layerObj.name);
                  }}
                  className={`p-0.5 rounded transition ${
                    layerObj.isVisible ? "text-cyan-400 hover:text-cyan-200" : "text-neutral-600 hover:text-neutral-400"
                  }`}
                  title={`Ẩn/Hiện Layer: ${layerObj.name}`}
                >
                  {layerObj.isVisible ? <Eye className="w-3 h-3" /> : <EyeOff className="w-3 h-3" />}
                </button>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    handleToggleLock(layerObj.name);
                  }}
                  className={`p-0.5 rounded transition ${
                    layerObj.isLocked ? "text-amber-400 hover:text-amber-200" : "text-neutral-600 hover:text-neutral-400"
                  }`}
                  title={`Khóa/Mở Layer: ${layerObj.name}`}
                >
                  {layerObj.isLocked ? <Lock className="w-3 h-3" /> : <Unlock className="w-3 h-3" />}
                </button>
              </div>
            )}

            {node.isDirty && (
              <span
                onClick={(e) => {
                  e.stopPropagation();
                  if (node.smartObjectId) onRecomputeObject(node.smartObjectId);
                }}
                className="text-[10px] bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 px-1 py-0.5 rounded border border-amber-500/40 cursor-pointer animate-pulse"
                title="Đối tượng thay đổi - Nhấp để tính toán lại (Recompute)"
              >
                UPDATE
              </span>
            )}
            {renderStatusBadge(node.status)}
          </div>
        </div>

        {hasChildren && isExpanded && (
          <div className="border-l border-neutral-800 ml-4">
            {node.children.map((child) => renderTree(child, depth + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <div className="h-full flex flex-col bg-[#16181b] text-neutral-300 border-r border-neutral-800">
      {/* Top Header */}
      <div className="p-2.5 border-b border-neutral-800 flex items-center justify-between bg-neutral-900/60">
        <div className="flex items-center space-x-2">
          <FolderTree className="w-4 h-4 text-sky-400" />
          <span className="font-semibold text-xs text-neutral-200 tracking-wide uppercase">
            HNL Project Tree (Logical Model)
          </span>
        </div>

        <div className="flex items-center space-x-1">
          {dirtyCount > 0 && (
            <button
              onClick={onRecomputeAll}
              className="flex items-center space-x-1 px-2 py-0.5 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/40 text-[11px] font-medium transition"
              title="Tính lại toàn bộ các đối tượng bị thay đổi (Recompute)"
            >
              <RefreshCw className="w-3 h-3 animate-spin" />
              <span>Recompute ({dirtyCount})</span>
            </button>
          )}

          <div className="relative">
            <button
              onClick={() => setIsAddMenuOpen((v) => !v)}
              aria-expanded={isAddMenuOpen}
              className="p-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-300 transition"
              title="Thêm Smart Object mới"
            >
              <Plus className="w-3.5 h-3.5" />
            </button>
            {isAddMenuOpen && <div className="absolute right-0 top-full mt-1 flex flex-col bg-neutral-900 border border-neutral-700 rounded shadow-2xl p-1 z-50 w-44">
              <button
                onClick={() => { onAddNewSmartObject("CEILING"); setIsAddMenuOpen(false); }}
                className="text-left px-2 py-1 text-xs text-neutral-200 hover:bg-sky-500/20 hover:text-sky-300 rounded flex items-center space-x-1.5"
              >
                <Grid className="w-3.5 h-3.5 text-cyan-400" />
                <span>+ Thêm HNL Ceiling</span>
              </button>
              <button
                onClick={() => { onAddNewSmartObject("WALL"); setIsAddMenuOpen(false); }}
                className="text-left px-2 py-1 text-xs text-neutral-200 hover:bg-amber-500/20 hover:text-amber-300 rounded flex items-center space-x-1.5"
              >
                <Flame className="w-3.5 h-3.5 text-amber-400" />
                <span>+ Thêm HNL Wall (EI)</span>
              </button>
              <button
                onClick={() => { onAddNewSmartObject("DETAIL"); setIsAddMenuOpen(false); }}
                className="text-left px-2 py-1 text-xs text-neutral-200 hover:bg-purple-500/20 hover:text-purple-300 rounded flex items-center space-x-1.5"
              >
                <Scissors className="w-3.5 h-3.5 text-purple-400" />
                <span>+ Thêm HNL Detail</span>
              </button>
              <button
                onClick={() => { onAddNewSmartObject("SECTION"); setIsAddMenuOpen(false); }}
                className="text-left px-2 py-1 text-xs text-neutral-200 hover:bg-blue-500/20 hover:text-blue-300 rounded flex items-center space-x-1.5"
              >
                <Sliders className="w-3.5 h-3.5 text-blue-400" />
                <span>+ Thêm HNL Section</span>
              </button>
            </div>}
          </div>
        </div>
      </div>

      {/* Quick Actions Section (Layer Visibility & Lock Controls) */}
      <div className="border-b border-neutral-800 bg-neutral-900/40">
        <div
          onClick={() => setIsQuickActionsOpen((prev) => !prev)}
          className="p-2 flex items-center justify-between cursor-pointer hover:bg-neutral-800/50 transition select-none"
        >
          <div className="flex items-center space-x-2">
            <Zap className="w-3.5 h-3.5 text-amber-400 animate-pulse" />
            <span className="font-semibold text-xs text-neutral-200 tracking-wider uppercase">
              Quick Actions
            </span>
            <span className="text-[10px] text-sky-400 bg-sky-950/70 px-1.5 py-0.5 rounded border border-sky-800/50 font-mono">
              {visibleLayerCount}/{activeLayers.length} Visible
            </span>
          </div>

          <div className="flex items-center space-x-1 text-neutral-400">
            {lockedLayerCount > 0 && (
              <span className="text-[10px] text-amber-400 bg-amber-950/60 px-1 rounded border border-amber-800/40 font-mono">
                {lockedLayerCount} Locked
              </span>
            )}
            {isQuickActionsOpen ? (
              <ChevronDown className="w-3.5 h-3.5" />
            ) : (
              <ChevronRight className="w-3.5 h-3.5" />
            )}
          </div>
        </div>

        {isQuickActionsOpen && (
          <div className="p-2 pt-0 space-y-2 border-t border-neutral-800/60 bg-[#121417]">
            {/* Global Batch Action Buttons */}
            <div className="flex items-center justify-between pt-2 gap-1 text-[11px]">
              <div className="flex items-center space-x-1">
                <button
                  onClick={() => handleToggleAllVisible(true)}
                  className="flex items-center space-x-1 px-2 py-1 rounded bg-cyan-500/15 hover:bg-cyan-500/25 text-cyan-300 border border-cyan-500/30 transition text-[10px] font-medium"
                  title="Bật hiển thị tất cả các Layer"
                >
                  <Eye className="w-3 h-3 text-cyan-400" />
                  <span>Hiện tất cả</span>
                </button>
                <button
                  onClick={() => handleToggleAllVisible(false)}
                  className="flex items-center space-x-1 px-2 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-400 hover:text-neutral-200 border border-neutral-700 transition text-[10px] font-medium"
                  title="Ẩn tất cả các Layer"
                >
                  <EyeOff className="w-3 h-3" />
                  <span>Ẩn tất cả</span>
                </button>
              </div>

              <div className="flex items-center space-x-1">
                <button
                  onClick={() => handleToggleAllLock(true)}
                  className="flex items-center space-x-1 px-2 py-1 rounded bg-amber-500/15 hover:bg-amber-500/25 text-amber-300 border border-amber-500/30 transition text-[10px] font-medium"
                  title="Khóa tất cả các Layer"
                >
                  <Lock className="w-3 h-3 text-amber-400" />
                  <span>Khóa</span>
                </button>
                <button
                  onClick={() => handleToggleAllLock(false)}
                  className="flex items-center space-x-1 px-2 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-400 hover:text-neutral-200 border border-neutral-700 transition text-[10px] font-medium"
                  title="Mở khóa tất cả các Layer"
                >
                  <Unlock className="w-3 h-3" />
                  <span>Mở khóa</span>
                </button>
              </div>
            </div>

            {/* Layer Filter Search Bar */}
            <div className="relative">
              <Search className="w-3 h-3 absolute left-2 top-2 text-neutral-500 pointer-events-none" />
              <input
                type="text"
                value={layerSearchQuery}
                onChange={(e) => setLayerSearchQuery(e.target.value)}
                placeholder="Lọc Layer (VD: KT_TUONG, TRAN...)..."
                className="w-full bg-neutral-900 border border-neutral-800 focus:border-sky-500/60 rounded pl-7 pr-6 py-1 text-[11px] text-neutral-200 placeholder-neutral-500 focus:outline-none transition"
              />
              {layerSearchQuery && (
                <button
                  onClick={() => setLayerSearchQuery("")}
                  className="absolute right-1.5 top-1.5 text-neutral-500 hover:text-neutral-300 p-0.5"
                >
                  <X className="w-3 h-3" />
                </button>
              )}
            </div>

            {/* Layer Quick Control List */}
            <div className="max-h-44 overflow-y-auto space-y-1 pr-0.5 scrollbar-thin scrollbar-thumb-neutral-700">
              {filteredLayers.map((layer) => {
                const entCount = entityCountByLayer[layer.name] || 0;
                return (
                  <div
                    key={layer.name}
                    className={`flex items-center justify-between py-1 px-2 rounded text-xs transition border ${
                      !layer.isVisible
                        ? "bg-neutral-900/40 border-neutral-800/50 opacity-60"
                        : layer.isLocked
                        ? "bg-amber-950/20 border-amber-800/30 text-amber-200"
                        : "bg-neutral-800/40 border-neutral-800/80 hover:border-neutral-700 text-neutral-200"
                    }`}
                  >
                    <div className="flex items-center space-x-2 truncate min-w-0 pr-1">
                      {/* Color Preview Swatch */}
                      <span
                        className="w-2.5 h-2.5 rounded-full flex-shrink-0 border border-white/20 shadow-sm"
                        style={{ backgroundColor: layer.color || "#FFFFFF" }}
                        title={`Màu Layer: ${layer.color}`}
                      />

                      {/* Layer Name */}
                      <span
                        className={`font-mono text-[11px] truncate ${
                          !layer.isVisible ? "line-through text-neutral-500" : "font-medium"
                        }`}
                        title={`Layer: ${layer.name}`}
                      >
                        {layer.name}
                      </span>

                      {/* Entity count badge */}
                      {entCount > 0 && (
                        <span className="text-[9px] text-neutral-400 bg-neutral-800 px-1 rounded font-mono flex-shrink-0">
                          {entCount}
                        </span>
                      )}
                    </div>

                    {/* Interactive Action Controls */}
                    <div className="flex items-center space-x-1 flex-shrink-0">
                      {/* Toggle Visibility */}
                      <button
                        onClick={() => handleToggleVisible(layer.name)}
                        className={`p-1 rounded transition border ${
                          layer.isVisible
                            ? "bg-cyan-500/20 text-cyan-300 border-cyan-500/40 hover:bg-cyan-500/30"
                            : "bg-neutral-900 text-neutral-500 border-neutral-800 hover:text-neutral-300"
                        }`}
                        title={layer.isVisible ? `Ẩn Layer ${layer.name}` : `Hiện Layer ${layer.name}`}
                      >
                        {layer.isVisible ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
                      </button>

                      {/* Toggle Lock */}
                      <button
                        onClick={() => handleToggleLock(layer.name)}
                        className={`p-1 rounded transition border ${
                          layer.isLocked
                            ? "bg-amber-500/20 text-amber-300 border-amber-500/40 hover:bg-amber-500/30"
                            : "bg-neutral-900 text-neutral-500 border-neutral-800 hover:text-neutral-300"
                        }`}
                        title={layer.isLocked ? `Mở khóa Layer ${layer.name}` : `Khóa Layer ${layer.name}`}
                      >
                        {layer.isLocked ? <Lock className="w-3.5 h-3.5" /> : <Unlock className="w-3.5 h-3.5" />}
                      </button>
                    </div>
                  </div>
                );
              })}

              {filteredLayers.length === 0 && (
                <div className="py-3 text-center text-xs text-neutral-500 font-mono">
                  Không tìm thấy layer nào với từ khóa "{layerSearchQuery}"
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Tree Content */}
      <div className="flex-1 overflow-y-auto p-1.5 space-y-0.5 scrollbar-thin scrollbar-thumb-neutral-700">
        <div className="text-[10px] font-mono text-neutral-500 px-1 py-0.5 uppercase tracking-wider flex items-center justify-between">
          <span>Hierarchy Tree</span>
          <span>{smartObjects.length} Nodes</span>
        </div>
        {renderTree(treeRoot)}
      </div>

      {/* Bottom Summary Bar */}
      <div className="p-2 border-t border-neutral-800 bg-neutral-900/40 text-[11px] text-neutral-400 flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <span>Smart Objects: <strong>{smartObjects.length}</strong></span>
          <span>•</span>
          <span className="text-emerald-400">Verified: {smartObjects.filter(o => o.status === "VERIFIED").length}</span>
        </div>
        <div className="flex items-center space-x-1 text-sky-400">
          <Eye className="w-3 h-3" />
          <span>Dữ liệu nội bộ</span>
        </div>
      </div>
    </div>
  );
};

