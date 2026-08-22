import React, { useState } from "react";
import { CadEntity } from "../../types/cad";
import {
  generateAutoCadDxf,
  generateExcelBoqCsv,
  downloadFile,
} from "../../lib/exportEngine";
import {
  Download,
  FileCode,
  FileSpreadsheet,
  Printer,
  X,
  CheckCircle,
  FileCheck,
  Layers,
  Sparkles,
} from "lucide-react";

interface HnlExportModalProps {
  isOpen: boolean;
  onClose: () => void;
  entities: CadEntity[];
  drawingName?: string;
}

export const HnlExportModal: React.FC<HnlExportModalProps> = ({
  isOpen,
  onClose,
  entities,
  drawingName = "HNL_SHOPDRAWING_CEILING_WALL",
}) => {
  const [selectedFormat, setSelectedFormat] = useState<"DXF" | "EXCEL" | "PDF">("DXF");
  const [exportSuccess, setExportSuccess] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleExportDxf = () => {
    const dxfString = generateAutoCadDxf(entities, drawingName);
    downloadFile(`${drawingName}.dxf`, dxfString, "application/dxf");
    setExportSuccess(`Đã xuất file AutoCAD DXF: ${drawingName}.dxf`);
  };

  const handleExportExcel = () => {
    const csvContent = generateExcelBoqCsv(entities);
    downloadFile(`${drawingName}_BOQ.csv`, "\uFEFF" + csvContent, "text/csv;charset=utf-8;");
    setExportSuccess(`Đã xuất bảng dự toán bóc tách khối lượng Excel: ${drawingName}_BOQ.csv`);
  };

  const handleExportPdf = () => {
    window.print();
    setExportSuccess(`Đã khởi tạo lệnh in ấn PDF Khổ A1/A2/A3`);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 animate-in fade-in">
      <div className="w-full max-w-2xl bg-[#141619] border border-neutral-700 rounded-2xl shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-neutral-800 bg-[#1A1D23]">
          <div className="flex items-center space-x-3">
            <div className="p-2 rounded-xl bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              <Download className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>HNL Multi-Format Export Center (Xuất Bản Vẽ & Dự Toán)</span>
              </h2>
              <p className="text-xs text-neutral-400">
                Xuất file định dạng chuẩn cho AutoCAD, BricsCAD, Microsoft Excel và Hồ sơ in ấn
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

        {/* Export Options Grid */}
        <div className="p-6 space-y-4">
          <div className="grid grid-cols-3 gap-3">
            {/* 1. DXF Option */}
            <button
              onClick={() => setSelectedFormat("DXF")}
              className={`p-4 rounded-xl border text-left transition flex flex-col justify-between space-y-2 ${
                selectedFormat === "DXF"
                  ? "bg-cyan-500/10 border-cyan-500 text-white shadow-lg shadow-cyan-500/10"
                  : "bg-neutral-900/60 border-neutral-800 text-neutral-400 hover:border-neutral-700"
              }`}
            >
              <div className="flex items-center justify-between">
                <FileCode className={`w-6 h-6 ${selectedFormat === "DXF" ? "text-cyan-400" : "text-neutral-500"}`} />
                {selectedFormat === "DXF" && <CheckCircle className="w-4 h-4 text-cyan-400" />}
              </div>
              <div>
                <div className="font-bold text-sm text-white">AutoCAD DXF</div>
                <div className="text-[11px] text-neutral-400 mt-0.5">
                  Chuẩn AutoCAD 2000+ với đầy đủ Layers phân màu
                </div>
              </div>
            </button>

            {/* 2. Excel BOQ Option */}
            <button
              onClick={() => setSelectedFormat("EXCEL")}
              className={`p-4 rounded-xl border text-left transition flex flex-col justify-between space-y-2 ${
                selectedFormat === "EXCEL"
                  ? "bg-emerald-500/10 border-emerald-500 text-white shadow-lg shadow-emerald-500/10"
                  : "bg-neutral-900/60 border-neutral-800 text-neutral-400 hover:border-neutral-700"
              }`}
            >
              <div className="flex items-center justify-between">
                <FileSpreadsheet className={`w-6 h-6 ${selectedFormat === "EXCEL" ? "text-emerald-400" : "text-neutral-500"}`} />
                {selectedFormat === "EXCEL" && <CheckCircle className="w-4 h-4 text-emerald-400" />}
              </div>
              <div>
                <div className="font-bold text-sm text-white">Excel BOQ (.CSV)</div>
                <div className="text-[11px] text-neutral-400 mt-0.5">
                  Bảng bóc tách khối lượng vật tư có công thức giá
                </div>
              </div>
            </button>

            {/* 3. PDF Layout Option */}
            <button
              onClick={() => setSelectedFormat("PDF")}
              className={`p-4 rounded-xl border text-left transition flex flex-col justify-between space-y-2 ${
                selectedFormat === "PDF"
                  ? "bg-purple-500/10 border-purple-500 text-white shadow-lg shadow-purple-500/10"
                  : "bg-neutral-900/60 border-neutral-800 text-neutral-400 hover:border-neutral-700"
              }`}
            >
              <div className="flex items-center justify-between">
                <Printer className={`w-6 h-6 ${selectedFormat === "PDF" ? "text-purple-400" : "text-neutral-500"}`} />
                {selectedFormat === "PDF" && <CheckCircle className="w-4 h-4 text-purple-400" />}
              </div>
              <div>
                <div className="font-bold text-sm text-white">PDF / Print Layout</div>
                <div className="text-[11px] text-neutral-400 mt-0.5">
                  In hàng loạt Khung tên A1/A2/A3 tỷ lệ 1:50, 1:100
                </div>
              </div>
            </button>
          </div>

          {/* Details for Selected Format */}
          <div className="p-4 rounded-xl bg-neutral-900/80 border border-neutral-800 text-xs space-y-2">
            <div className="flex items-center space-x-2 text-white font-bold">
              <Sparkles className="w-4 h-4 text-cyan-400" />
              <span>
                {selectedFormat === "DXF" && "Chi tiết cấu hình xuất file AutoCAD (.DXF)"}
                {selectedFormat === "EXCEL" && "Chi tiết bảng dự toán bóc tách (.CSV / Excel)"}
                {selectedFormat === "PDF" && "Chi tiết khung tên bản vẽ kỹ thuật (.PDF)"}
              </span>
            </div>

            <ul className="text-neutral-400 space-y-1 pl-4 list-disc">
              {selectedFormat === "DXF" && (
                <>
                  <li>Bao gồm {entities.length} thực thể CAD (Walls, Ceilings, Lines, Dimensions, Text).</li>
                  <li>Tự động phân lớp Layer: <code>TUONG_220</code>, <code>TRAN_THACH_CAO</code>, <code>KHUNG_XUONG_CHINH</code>, <code>MEP_THIETBI</code>.</li>
                  <li>Xuất DXF R2000 để tăng khả năng tương thích; cần kiểm tra lại file trên phần mềm CAD đích trước khi phát hành.</li>
                </>
              )}
              {selectedFormat === "EXCEL" && (
                <>
                  <li>Tính toán tự động diện tích trần thạch cao, chiều dài xương chính/phụ, ty treo M8.</li>
                  <li>Tích hợp hệ số hao hụt vật liệu 5% - 10% theo tiêu chuẩn định mức Bộ Xây Dựng.</li>
                  <li>Đơn giá tham khảo Saint-Gobain Gyproc / Vĩnh Tường mới nhất.</li>
                </>
              )}
              {selectedFormat === "PDF" && (
                <>
                  <li>In trực tiếp khổ giấy A1/A2/A3 với khung tên tiêu chuẩn Việt Nam.</li>
                  <li>Độ phân giải vector sắc nét, không bị vỡ hạt chữ nét vẽ.</li>
                </>
              )}
            </ul>
          </div>

          {/* Success Banner */}
          {exportSuccess && (
            <div className="p-3 rounded-xl bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs flex items-center space-x-2 animate-in fade-in">
              <CheckCircle className="w-4 h-4 flex-shrink-0" />
              <span>{exportSuccess}</span>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="px-6 py-3 border-t border-neutral-800 bg-[#1A1D23] flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-xl bg-neutral-800 hover:bg-neutral-700 text-neutral-300 font-medium text-xs transition"
          >
            Đóng
          </button>

          <div className="flex items-center space-x-3">
            {selectedFormat === "DXF" && (
              <button
                onClick={handleExportDxf}
                className="flex items-center space-x-2 px-5 py-2.5 rounded-xl bg-cyan-500 hover:bg-cyan-400 text-black font-bold text-xs shadow-lg shadow-cyan-500/20 transition"
              >
                <Download className="w-4 h-4" />
                <span>Tải File AutoCAD (.DXF)</span>
              </button>
            )}

            {selectedFormat === "EXCEL" && (
              <button
                onClick={handleExportExcel}
                className="flex items-center space-x-2 px-5 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-black font-bold text-xs shadow-lg shadow-emerald-500/20 transition"
              >
                <Download className="w-4 h-4" />
                <span>Tải Bảng Dự Toán Excel (.CSV)</span>
              </button>
            )}

            {selectedFormat === "PDF" && (
              <button
                onClick={handleExportPdf}
                className="flex items-center space-x-2 px-5 py-2.5 rounded-xl bg-purple-500 hover:bg-purple-400 text-white font-bold text-xs shadow-lg shadow-purple-500/20 transition"
              >
                <Printer className="w-4 h-4" />
                <span>In Bản Vẽ PDF</span>
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
