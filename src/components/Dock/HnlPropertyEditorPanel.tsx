import React, { useState, useEffect } from "react";
import {
  HnlSmartObject,
  SmartObjectProperty,
} from "../../types/cad";
import {
  Sliders,
  CheckCircle2,
  AlertTriangle,
  HelpCircle,
  Flame,
  Grid,
  Box,
  Layers,
  FileCheck,
  RefreshCw,
  ExternalLink,
} from "lucide-react";

interface HnlPropertyEditorPanelProps {
  selectedObject: HnlSmartObject | null;
  onUpdateProperty: (objectId: string, propertyKey: string, newValue: any) => void;
  onRecomputeObject: (objectId: string) => void;
}

export const HnlPropertyEditorPanel: React.FC<HnlPropertyEditorPanelProps> = ({
  selectedObject,
  onUpdateProperty,
  onRecomputeObject,
}) => {
  const [localProperties, setLocalProperties] = useState<Record<string, any>>({});

  useEffect(() => {
    if (selectedObject) {
      const initialMap: Record<string, any> = {};
      selectedObject.properties.forEach((p) => {
        initialMap[p.key] = p.value;
      });
      setLocalProperties(initialMap);
    }
  }, [selectedObject]);

  if (!selectedObject) {
    return (
      <div className="h-full flex flex-col items-center justify-center p-6 text-center text-neutral-400 bg-[#16181b]">
        <Sliders className="w-8 h-8 text-neutral-600 mb-2 stroke-[1.5]" />
        <p className="text-xs font-medium text-neutral-300">Không có đối tượng được chọn</p>
        <p className="text-[11px] text-neutral-500 mt-1">
          Chọn một Smart Object trên Project Tree hoặc nhấp vào bản vẽ để xem & chỉnh sửa thuộc tính tham số.
        </p>
      </div>
    );
  }

  // Group properties
  const groups: Record<string, SmartObjectProperty[]> = {};
  selectedObject.properties.forEach((p) => {
    const groupName = p.group || "General";
    if (!groups[groupName]) groups[groupName] = [];
    groups[groupName].push(p);
  });

  const handleFieldChange = (key: string, val: any) => {
    setLocalProperties((prev) => ({ ...prev, [key]: val }));
    onUpdateProperty(selectedObject.id, key, val);
  };

  return (
    <div className="h-full flex flex-col bg-[#16181b] text-neutral-300 border-r border-neutral-800">
      {/* Top Header */}
      <div className="p-2.5 border-b border-neutral-800 bg-neutral-900/60 flex items-center justify-between">
        <div className="flex items-center space-x-2 truncate">
          {selectedObject.type === "HNL_WALL" ? (
            <Flame className="w-4 h-4 text-amber-400 flex-shrink-0" />
          ) : selectedObject.type === "HNL_CEILING" ? (
            <Grid className="w-4 h-4 text-cyan-400 flex-shrink-0" />
          ) : (
            <Box className="w-4 h-4 text-sky-400 flex-shrink-0" />
          )}
          <div className="truncate">
            <h4 className="text-xs font-semibold text-neutral-100 truncate">{selectedObject.name}</h4>
            <span className="text-[10px] text-neutral-400 font-mono">{selectedObject.type}</span>
          </div>
        </div>

        <div className="flex items-center space-x-1.5 flex-shrink-0">
          {selectedObject.dirtyFlag ? (
            <button
              onClick={() => onRecomputeObject(selectedObject.id)}
              className="flex items-center space-x-1 px-2 py-0.5 rounded bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/40 text-[11px] font-medium transition animate-pulse"
              title="Tính lại hình học & vật tư của đối tượng này"
            >
              <RefreshCw className="w-3 h-3" />
              <span>Update</span>
            </button>
          ) : (
            <span className="text-[10px] text-emerald-400 bg-emerald-950/60 px-1.5 py-0.5 rounded border border-emerald-800/40 flex items-center space-x-1">
              <CheckCircle2 className="w-3 h-3" />
              <span>Synced</span>
            </span>
          )}
        </div>
      </div>

      {/* Property Groups List */}
      <div className="flex-1 overflow-y-auto p-2.5 space-y-3.5 scrollbar-thin scrollbar-thumb-neutral-700">
        {Object.entries(groups).map(([groupName, props]) => (
          <div key={groupName} className="bg-neutral-900/40 rounded border border-neutral-800/70 p-2 space-y-2">
            <h5 className="text-[11px] font-semibold text-neutral-400 uppercase tracking-wider flex items-center justify-between">
              <span>{groupName}</span>
              <span className="text-[10px] text-neutral-600 font-mono">{props.length} fields</span>
            </h5>

            <div className="space-y-1.5">
              {props.map((prop) => {
                const currentValue = localProperties[prop.key] !== undefined ? localProperties[prop.key] : prop.value;

                return (
                  <div key={prop.key} className="flex items-center justify-between text-xs py-0.5">
                    <label className="text-neutral-300 font-medium truncate max-w-[55%]" title={prop.label}>
                      {prop.label}
                    </label>

                    <div className="w-[45%] flex items-center justify-end">
                      {prop.type === "select" ? (
                        <select
                          value={currentValue}
                          disabled={prop.isReadOnly}
                          onChange={(e) => handleFieldChange(prop.key, e.target.value)}
                          className="w-full bg-neutral-950 border border-neutral-700 rounded px-1.5 py-0.5 text-xs text-neutral-200 focus:border-sky-500 focus:outline-none disabled:opacity-50"
                        >
                          {prop.options?.map((opt) => (
                            <option key={opt} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      ) : prop.type === "number" ? (
                        <div className="flex items-center space-x-1 w-full justify-end">
                          <input
                            type="number"
                            value={currentValue}
                            disabled={prop.isReadOnly}
                            onChange={(e) => handleFieldChange(prop.key, parseFloat(e.target.value) || 0)}
                            className="w-20 bg-neutral-950 border border-neutral-700 rounded px-1.5 py-0.5 text-xs text-right text-neutral-200 focus:border-sky-500 focus:outline-none disabled:opacity-50"
                          />
                          {prop.unit && <span className="text-[11px] text-neutral-400 font-mono">{prop.unit}</span>}
                        </div>
                      ) : prop.type === "boolean" ? (
                        <input
                          type="checkbox"
                          checked={Boolean(currentValue)}
                          disabled={prop.isReadOnly}
                          onChange={(e) => handleFieldChange(prop.key, e.target.checked)}
                          className="rounded bg-neutral-950 border-neutral-700 text-sky-500 focus:ring-sky-500 h-4 w-4"
                        />
                      ) : (
                        <input
                          type="text"
                          value={currentValue}
                          disabled={prop.isReadOnly}
                          onChange={(e) => handleFieldChange(prop.key, e.target.value)}
                          className="w-full bg-neutral-950 border border-neutral-700 rounded px-1.5 py-0.5 text-xs text-neutral-200 focus:border-sky-500 focus:outline-none disabled:opacity-50"
                        />
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        ))}

        {/* Tested Assembly Reference Card (For Walls & Ceilings) */}
        {selectedObject.type === "HNL_WALL" && (selectedObject as any).fireRating !== "NONE" && (
          <div className="p-2.5 rounded bg-gradient-to-br from-amber-950/30 to-neutral-900 border border-amber-800/40 text-xs space-y-1.5">
            <div className="flex items-center justify-between text-amber-300 font-semibold">
              <span className="flex items-center space-x-1">
                <FileCheck className="w-3.5 h-3.5 text-amber-400" />
                <span>Tested Assembly Database</span>
              </span>
              <span className="text-[10px] text-amber-300 bg-amber-950/80 px-1 rounded border border-amber-700/50">
                🟡 CẦN XÁC NHẬN
              </span>
            </div>
            <p className="text-[11px] text-neutral-300 leading-relaxed">
              Dữ liệu hiện tại là preset minh họa. Chỉ đánh dấu VERIFIED sau khi gắn đúng <strong>Approved System / Test Report / Revision</strong> của dự án. Không suy EI từ số lớp tấm.
            </p>
          </div>
        )}
      </div>

      {/* Footer Info */}
      <div className="p-2 border-t border-neutral-800 bg-neutral-900/40 text-[11px] text-neutral-400 flex items-center justify-between">
        <span className="truncate">Layer: <strong className="text-neutral-300">{selectedObject.layer}</strong></span>
        <span className="text-neutral-500">Standalone • Plugin AutoCAD 2023+ cần build riêng</span>
      </div>
    </div>
  );
};
