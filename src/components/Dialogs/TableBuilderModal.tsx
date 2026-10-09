import React, { useState } from "react";
import { Table as TableIcon, X, Plus, Trash2, Check, Download, Layers, Grid } from "lucide-react";
import { CadEntity, CadTable } from "../../types/cad";

interface TableBuilderModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
  onInsertTable: (table: CadTable) => void;
}

export const TableBuilderModal: React.FC<TableBuilderModalProps> = ({
  isOpen,
  onClose,
  entities,
  onInsertTable,
}) => {
  const [sourceType, setSourceType] = useState<"BLOCKS" | "ROOMS" | "WALLS">("BLOCKS");
  const [tableTitle, setTableTitle] = useState("BẢNG THỐNG KÊ THIẾT BỊ & CHI TIẾT");

  if (!isOpen) return null;

  // Build rows dynamically based on current CAD entities
  let headers: string[] = [];
  let rows: string[][] = [];

  if (sourceType === "BLOCKS") {
    headers = ["STT", "Tên Block", "Loại", "Layer", "Số lượng", "Đơn vị"];
    const blockCounts: Record<string, { count: number; layer: string }> = {};
    entities.forEach((ent) => {
      if (ent.type === "BLOCK_REF") {
        const blk = ent as any;
        const name = blk.blockName || "UNNAMED";
        if (!blockCounts[name]) {
          blockCounts[name] = { count: 0, layer: blk.layer || "0" };
        }
        blockCounts[name].count += 1;
      }
    });

    rows = Object.entries(blockCounts).map(([name, data], idx) => [
      String(idx + 1),
      name,
      name.includes("DEN") ? "Chiếu sáng" : "Kiến trúc",
      data.layer,
      String(data.count),
      "Cái",
    ]);
  } else if (sourceType === "ROOMS") {
    headers = ["STT", "Tên phòng", "Diện tích (m²)", "Chu vi (m)", "Loại phòng", "Ghi chú"];
    rows = [
      ["1", "Phòng Khách", "32.40", "22.80", "Sinh hoạt", "Lát gỗ"],
      ["2", "Phòng Ngủ Master", "24.50", "20.00", "Nghỉ ngơi", "Lát gỗ"],
      ["3", "Bếp & Ăn", "18.20", "17.40", "Nấu nướng", "Gạch chống trượt"],
      ["4", "WC Master", "6.50", "10.40", "Vệ sinh", "Chống thấm"],
    ];
  } else {
    headers = ["STT", "Chủng loại", "Độ dày (mm)", "Tổng chiều dài (m)", "Khối lượng xây (m²)", "Layer"];
    rows = [
      ["1", "Tường xây gạch tuynel 100", "100", "24.5", "68.6", "KT_TUONG"],
      ["2", "Tường xây gạch tuynel 200", "200", "18.0", "50.4", "KT_TUONG"],
      ["3", "Khung xương trần chìm @800", "0.6", "36.0", "36.0", "KT_TRAN_XUONGCHINH"],
      ["4", "Khung xương trần phụ @406.67 (1220/3)", "0.6", "72.0", "72.0", "KT_TRAN_XUONGPHU"],
    ];
  }

  const handleCreateCadTable = () => {
    const newTable: CadTable = {
      id: `tbl_${Date.now()}`,
      handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
      type: "TABLE",
      layer: "KT_BANG_THONGKE",
      color: "#00E5FF",
      position: { x: 8000, y: 3000 },
      title: tableTitle,
      headers,
      rows,
      columnWidths: headers.map(() => 1200),
      rowHeight: 400,
    };
    onInsertTable(newTable);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-4xl max-h-[85vh] bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-gradient-to-r from-cyan-600 to-emerald-500 flex items-center justify-center text-white">
              <TableIcon className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">HNL TABLE BUILDER</h2>
              <p className="text-xs text-neutral-400">
                Bóc tách đối tượng và tạo bảng dữ liệu trong bản vẽ HNL; chèn AutoCAD Table thật cần plugin AutoCAD
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

        {/* Controls */}
        <div className="p-4 bg-[#18191C] border-b border-neutral-800 flex items-center justify-between text-xs">
          <div className="flex items-center space-x-3">
            <span className="text-neutral-400 font-semibold">Nguồn dữ liệu:</span>
            <div className="flex bg-[#25272C] p-0.5 rounded-lg border border-neutral-700">
              <button
                onClick={() => {
                  setSourceType("BLOCKS");
                  setTableTitle("BẢNG THỐNG KÊ THIẾT BỊ & CHI TIẾT BLOCK");
                }}
                className={`px-3 py-1 rounded-md transition ${
                  sourceType === "BLOCKS" ? "bg-cyan-500 text-black font-semibold" : "text-neutral-400 hover:text-white"
                }`}
              >
                Block & Thiết bị
              </button>
              <button
                onClick={() => {
                  setSourceType("ROOMS");
                  setTableTitle("BẢNG THỐNG KÊ DIỆN TÍCH PHÒNG");
                }}
                className={`px-3 py-1 rounded-md transition ${
                  sourceType === "ROOMS" ? "bg-cyan-500 text-black font-semibold" : "text-neutral-400 hover:text-white"
                }`}
              >
                Diện tích phòng
              </button>
              <button
                onClick={() => {
                  setSourceType("WALLS");
                  setTableTitle("BẢNG KHỐI LƯỢNG TƯỜNG & TRẦN");
                }}
                className={`px-3 py-1 rounded-md transition ${
                  sourceType === "WALLS" ? "bg-cyan-500 text-black font-semibold" : "text-neutral-400 hover:text-white"
                }`}
              >
                Tường & Trần
              </button>
            </div>
          </div>

          <div className="flex items-center space-x-2">
            <input
              type="text"
              value={tableTitle}
              onChange={(e) => setTableTitle(e.target.value)}
              className="bg-[#25272C] text-neutral-200 px-3 py-1.5 rounded border border-neutral-700 w-72 focus:outline-none focus:border-cyan-500"
            />
          </div>
        </div>

        {/* Live Table Preview */}
        <div className="flex-1 p-6 overflow-auto bg-[#141517]">
          <div className="bg-[#1E1F22] rounded-lg border border-neutral-700 overflow-hidden shadow">
            <div className="p-3 bg-[#25272C] border-b border-neutral-700 text-center font-bold text-cyan-400 text-xs uppercase tracking-wider">
              {tableTitle}
            </div>

            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="bg-neutral-800/90 text-neutral-300 border-b border-neutral-700 font-semibold">
                  {headers.map((h, i) => (
                    <th key={i} className="p-2.5 border-r border-neutral-700 last:border-r-0">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-800 text-neutral-200 font-mono text-[11px]">
                {rows.map((row, rIdx) => (
                  <tr key={rIdx} className="hover:bg-neutral-800/40">
                    {row.map((cell, cIdx) => (
                      <td key={cIdx} className="p-2.5 border-r border-neutral-800 last:border-r-0">
                        {cell}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Footer Actions */}
        <div className="h-14 px-6 bg-[#141517] border-t border-neutral-800 flex items-center justify-between">
          <div className="text-xs text-neutral-400">
            Tạo bảng HNL tại góc tọa độ (8000, 3000) trong Model Space nội bộ
          </div>

          <div className="flex items-center space-x-3">
            <button
              onClick={onClose}
              className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs transition"
            >
              Hủy
            </button>
            <button
              onClick={handleCreateCadTable}
              className="px-5 py-2 rounded-lg bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-semibold text-xs flex items-center space-x-2 shadow transition"
            >
              <Check className="w-4 h-4" />
              <span>Chèn bảng vào bản vẽ HNL</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
