import React, { useState } from "react";
import { FileSpreadsheet, Download, Check, X, Layers, CheckSquare } from "lucide-react";
import { CadEntity } from "../../types/cad";

interface ExcelExportModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
}

export const ExcelExportModal: React.FC<ExcelExportModalProps> = ({
  isOpen,
  onClose,
  entities,
}) => {
  const [includeBlocks, setIncludeBlocks] = useState(true);
  const [includeRooms, setIncludeRooms] = useState(true);
  const [includeMaterials, setIncludeMaterials] = useState(true);
  const [projectName, setProjectName] = useState("Dự án Biệt thự HNL Riverside Villa - Giai đoạn 1");

  if (!isOpen) return null;

  const handleExportCsv = () => {
    let csvContent = `HNL CAD AI TOOL - BẢNG TỔNG HỢP KHỐI LƯỢNG BOQ\n`;
    csvContent += `Dự án: ${projectName}\n`;
    csvContent += `Ngày xuất: ${new Date().toLocaleDateString("vi-VN")}\n\n`;

    if (includeBlocks) {
      csvContent += `--- 1. THỐNG KÊ THIẾT BỊ & BLOCK ---\n`;
      csvContent += `STT,Tên thiết bị,Layer,Số lượng,Đơn vị,Đơn giá ước tính (VNĐ),Thành tiền (VNĐ)\n`;
      const blockCounts: Record<string, number> = {};
      entities.forEach((e) => {
        if (e.type === "BLOCK_REF") {
          const name = (e as any).blockName || "Block";
          blockCounts[name] = (blockCounts[name] || 0) + 1;
        }
      });
      let bIdx = 1;
      let totalBlockCost = 0;
      Object.entries(blockCounts).forEach(([name, count]) => {
        const price = name.includes("DOWNLIGHT") ? 250000 : name.includes("PANEL") ? 650000 : 1200000;
        const total = price * count;
        totalBlockCost += total;
        csvContent += `${bIdx++},"${name}",KT_THIETBI,${count},Cái,${price.toLocaleString("vi-VN")},${total.toLocaleString("vi-VN")}\n`;
      });
      csvContent += `,,,,,TỔNG THIẾT BỊ,${totalBlockCost.toLocaleString("vi-VN")}\n\n`;
    }

    if (includeRooms) {
      csvContent += `--- 2. THỐNG KÊ DIỆN TÍCH PHÒNG ---\n`;
      csvContent += `STT,Tên phòng,Diện tích (m²),Chu vi (m),Vật liệu lát sàn\n`;
      csvContent += `1,Phòng Khách,32.40,22.80,Sàn gỗ công nghiệp 12mm\n`;
      csvContent += `2,Phòng Ngủ Master,24.50,20.00,Sàn gỗ Chiu Liu tự nhiên\n`;
      csvContent += `3,Bếp & Phòng Ăn,18.20,17.40,Gạch Granite 800x800\n`;
      csvContent += `4,WC Master,6.50,10.40,Gạch chống trơn 300x300\n\n`;
    }

    if (includeMaterials) {
      csvContent += `--- 3. KHỐI LƯỢNG XÂY & TRẦN THẠCH CAO ---\n`;
      csvContent += `STT,Hạng mục,Độ dày,Khối lượng,Đơn vị\n`;
      csvContent += `1,Xây tường gạch ống 100,100mm,68.60,m²\n`;
      csvContent += `2,Xây tường gạch ống 200,200mm,50.40,m²\n`;
      csvContent += `3,Trần thạch cao khung chìm Vĩnh Tường,9mm,81.60,m²\n\n`;
    }

    const blob = new Blob(["\uFEFF" + csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `BOQ_HNL_${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-lg bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-emerald-600 flex items-center justify-center text-white">
              <FileSpreadsheet className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">XUẤT BẢNG KHỐI LƯỢNG BOQ (EXCEL)</h2>
              <p className="text-xs text-neutral-400">Xuất file CSV / XLSX tương thích Microsoft Excel</p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Settings Body */}
        <div className="p-6 space-y-4 text-xs">
          <div className="space-y-1.5">
            <label className="text-neutral-400 font-semibold">Tên công trình / Dự án:</label>
            <input
              type="text"
              value={projectName}
              onChange={(e) => setProjectName(e.target.value)}
              className="w-full bg-[#25272C] text-neutral-200 px-3 py-2 rounded-lg border border-neutral-700 focus:outline-none focus:border-cyan-500"
            />
          </div>

          <div className="space-y-2">
            <label className="text-neutral-400 font-semibold">Các hạng mục xuất:</label>
            <div className="space-y-2 bg-[#18191C] p-3 rounded-lg border border-neutral-800">
              <label className="flex items-center space-x-2 text-neutral-200 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeBlocks}
                  onChange={(e) => setIncludeBlocks(e.target.checked)}
                  className="rounded text-cyan-500 focus:ring-0"
                />
                <span>Thống kê Block, Thiết bị đèn & Chi tiết nội thất</span>
              </label>

              <label className="flex items-center space-x-2 text-neutral-200 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeRooms}
                  onChange={(e) => setIncludeRooms(e.target.checked)}
                  className="rounded text-cyan-500 focus:ring-0"
                />
                <span>Bảng kê Diện tích & Chu vi phòng</span>
              </label>

              <label className="flex items-center space-x-2 text-neutral-200 cursor-pointer">
                <input
                  type="checkbox"
                  checked={includeMaterials}
                  onChange={(e) => setIncludeMaterials(e.target.checked)}
                  className="rounded text-cyan-500 focus:ring-0"
                />
                <span>Khối lượng Tường xây & Trần thạch cao</span>
              </label>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="h-14 px-6 bg-[#141517] border-t border-neutral-800 flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs transition"
          >
            Đóng
          </button>
          <button
            onClick={handleExportCsv}
            className="px-5 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs flex items-center space-x-2 shadow transition"
          >
            <Download className="w-4 h-4" />
            <span>Tải Bảng Excel (CSV/XLSX)</span>
          </button>
        </div>
      </div>
    </div>
  );
};
