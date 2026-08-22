import React, { useState } from "react";
import { SpreadsheetParameter } from "../../types/cad";
import { evaluateExpression } from "../../lib/spreadsheetEngine";
import {
  Table2,
  Plus,
  Trash2,
  Calculator,
  Save,
  Check,
  Zap,
} from "lucide-react";

interface HnlSpreadsheetPanelProps {
  parameters: SpreadsheetParameter[];
  onUpdateParameter: (paramId: string, newExpr: string) => void;
  onAddParameter: (name: string, expr: string, unit: string, category: any) => void;
  onDeleteParameter: (paramId: string) => void;
}

export const HnlSpreadsheetPanel: React.FC<HnlSpreadsheetPanelProps> = ({
  parameters,
  onUpdateParameter,
  onAddParameter,
  onDeleteParameter,
}) => {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [tempExpr, setTempExpr] = useState("");
  const [newName, setNewName] = useState("");
  const [newExpr, setNewExpr] = useState("");
  const [newUnit, setNewUnit] = useState("mm");
  const [isAdding, setIsAdding] = useState(false);

  const startEdit = (param: SpreadsheetParameter) => {
    setEditingId(param.id);
    setTempExpr(param.expression);
  };

  const saveEdit = (paramId: string) => {
    onUpdateParameter(paramId, tempExpr);
    setEditingId(null);
  };

  const handleCreate = () => {
    if (!newName.trim()) return;
    onAddParameter(newName.trim(), newExpr.trim() || "0", newUnit, "General");
    setNewName("");
    setNewExpr("");
    setIsAdding(false);
  };

  return (
    <div className="h-full flex flex-col bg-[#16181b] text-neutral-300 border-r border-neutral-800">
      {/* Header */}
      <div className="p-2.5 border-b border-neutral-800 bg-neutral-900/60 flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <Table2 className="w-4 h-4 text-emerald-400" />
          <span className="font-semibold text-xs text-neutral-200 tracking-wide uppercase">
            HNL Spreadsheet & Expression Engine
          </span>
        </div>

        <button
          onClick={() => setIsAdding((prev) => !prev)}
          className="flex items-center space-x-1 px-2 py-0.5 rounded bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold shadow transition"
        >
          <Plus className="w-3 h-3" />
          <span>Thêm biến</span>
        </button>
      </div>

      {/* Main Content */}
      <div className="flex-1 overflow-y-auto p-2.5 space-y-2.5 scrollbar-thin scrollbar-thumb-neutral-700">
        {isAdding && (
          <div className="p-2.5 rounded bg-neutral-900 border border-emerald-500/50 space-y-2 text-xs">
            <h5 className="font-semibold text-emerald-400">Tạo tham số / công thức mới</h5>
            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="text-[10px] text-neutral-400 block mb-0.5">Tên biến</label>
                <input
                  type="text"
                  placeholder="e.g. WallHeight"
                  value={newName}
                  onChange={(e) => setNewName(e.target.value)}
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-neutral-100 focus:border-emerald-500 focus:outline-none"
                />
              </div>
              <div>
                <label className="text-[10px] text-neutral-400 block mb-0.5">Đơn vị</label>
                <select
                  value={newUnit}
                  onChange={(e) => setNewUnit(e.target.value)}
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-neutral-100 focus:border-emerald-500 focus:outline-none"
                >
                  <option value="mm">mm</option>
                  <option value="m">m</option>
                  <option value="m²">m²</option>
                  <option value="%">%</option>
                  <option value="pcs">pcs</option>
                </select>
              </div>
            </div>
            <div>
              <label className="text-[10px] text-neutral-400 block mb-0.5">Giá trị / Công thức (Expression)</label>
              <input
                type="text"
                placeholder="e.g. 3200 hoặc TotalFloorArea * 1.05"
                value={newExpr}
                onChange={(e) => setNewExpr(e.target.value)}
                className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-neutral-100 font-mono focus:border-emerald-500 focus:outline-none"
              />
            </div>
            <div className="flex justify-end space-x-1.5 pt-1">
              <button
                onClick={() => setIsAdding(false)}
                className="px-2 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs"
              >
                Hủy
              </button>
              <button
                onClick={handleCreate}
                className="px-3 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold"
              >
                Lưu tham số
              </button>
            </div>
          </div>
        )}

        <div className="space-y-1.5">
          {parameters.map((param) => {
            const isEditing = editingId === param.id;

            return (
              <div
                key={param.id}
                className="p-2 rounded bg-neutral-900/60 border border-neutral-800 hover:border-neutral-700 transition text-xs space-y-1"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-1.5 truncate">
                    <Zap className="w-3.5 h-3.5 text-emerald-400 flex-shrink-0" />
                    <span className="font-mono font-bold text-neutral-200 truncate">{param.name}</span>
                    <span className="text-[10px] text-neutral-500 bg-neutral-800/80 px-1 rounded">
                      {param.category}
                    </span>
                  </div>

                  <div className="flex items-center space-x-1 flex-shrink-0">
                    <span className="font-mono font-semibold text-emerald-400 bg-emerald-950/60 px-1.5 py-0.5 rounded border border-emerald-800/40">
                      {param.evaluatedValue} {param.unit}
                    </span>
                    <button
                      onClick={() => onDeleteParameter(param.id)}
                      className="p-1 text-neutral-500 hover:text-rose-400 rounded hover:bg-neutral-800"
                      title="Xóa tham số"
                    >
                      <Trash2 className="w-3 h-3" />
                    </button>
                  </div>
                </div>

                <div className="flex items-center justify-between pt-0.5">
                  {isEditing ? (
                    <div className="flex items-center space-x-1 w-full">
                      <input
                        type="text"
                        value={tempExpr}
                        onChange={(e) => setTempExpr(e.target.value)}
                        className="flex-1 bg-neutral-950 border border-emerald-500 rounded px-1.5 py-0.5 text-xs text-neutral-100 font-mono focus:outline-none"
                      />
                      <button
                        onClick={() => saveEdit(param.id)}
                        className="p-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded"
                        title="Xác nhận"
                      >
                        <Check className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => startEdit(param)}
                      className="text-[11px] font-mono text-neutral-400 hover:text-sky-300 text-left truncate max-w-[85%]"
                      title="Nhấp để sửa công thức"
                    >
                      = {param.expression}
                    </button>
                  )}
                </div>

                {param.description && (
                  <p className="text-[10px] text-neutral-500 truncate">{param.description}</p>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
