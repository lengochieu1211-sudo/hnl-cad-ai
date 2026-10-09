import React, { useState, useEffect, useRef } from "react";
import { Search, Command, ArrowRight, CornerDownLeft, Sparkles, Hash } from "lucide-react";
import { CANONICAL_LISP_TOOLS } from "../../lib/lispCanonicalTools";
import { AUTOCAD_2023_COMMAND_KNOWLEDGE } from "../../lib/autocad2023CommandKnowledge";

interface CommandItem {
  id: string;
  title: string;
  category: string;
  shortcut?: string;
  keywords: string[];
  description: string;
  actionKey: string;
}

interface CommandSearchModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectCommand: (actionKey: string) => void;
}

const BASE_COMMAND_DATABASE: CommandItem[] = [
  // Classic AutoCAD Commands & Shortcuts
  {
    id: "cmd_draw_line",
    title: "Vẽ đường thẳng (Line)",
    category: "Vẽ cơ bản",
    shortcut: "L",
    keywords: ["l", "line", "vẽ line", "đoạn thẳng"],
    description: "Tạo các đoạn thẳng liên tiếp bằng cách chọn điểm gốc và điểm tiếp theo",
    actionKey: "DRAW_LINE",
  },
  {
    id: "cmd_draw_rect",
    title: "Vẽ hình chữ nhật (Rectangle)",
    category: "Vẽ cơ bản",
    shortcut: "REC",
    keywords: ["rec", "rect", "rectangle", "hình chữ nhật", "khung"],
    description: "Vẽ hình chữ nhật từ hai điểm góc chéo đối diện",
    actionKey: "DRAW_RECTANGLE",
  },
  {
    id: "cmd_draw_mleader",
    title: "Vẽ chú thích Multileader",
    category: "Chú thích",
    shortcut: "ML",
    keywords: ["ml", "mleader", "multileader", "chú thích", "mũi tên", "lead"],
    description: "Tạo đường dóng ghi chú kỹ thuật có mũi tên chỉ định và nội dung Text",
    actionKey: "DRAW_MLEADER",
  },
  {
    id: "cmd_dt_1",
    title: "Tính diện tích Polyline (Area)",
    category: "Diện tích",
    shortcut: "AA",
    keywords: ["aa", "area", "diện tích", "dien tich", "polyline", "s", "m2", "đo"],
    description: "Đo diện tích hình khép kín và hiển thị kết quả mm² hoặc m²",
    actionKey: "CALC_AREA_TOTAL",
  },
  {
    id: "cmd_dt_2",
    title: "Tổng diện tích nhiều đối tượng",
    category: "Diện tích",
    shortcut: "APAREASUM",
    keywords: ["diện tích", "tổng diện tích", "sum area", "toan bo", "nhieu phong"],
    description: "Cộng dồn diện tích tất cả các phòng được chọn",
    actionKey: "CALC_AREA_TOTAL",
  },
  {
    id: "cmd_dt_3",
    title: "Ghi diện tích vào bản vẽ (Field động)",
    category: "Diện tích & Field",
    shortcut: "APAREALABEL",
    keywords: ["diện tích", "ghi", "nhãn", "field", "text", "tâm phòng"],
    description: "Tạo Text A = ##.## m² tại tâm phòng, tự cập nhật khi co giãn phòng",
    actionKey: "LABEL_ROOM_AREAS",
  },
  {
    id: "cmd_dt_4",
    title: "Tạo bảng diện tích & phòng",
    category: "Bảng",
    shortcut: "APAREATABLE",
    keywords: ["diện tích", "bảng", "table", "thống kê", "danh sách phòng"],
    description: "Tạo bảng phòng & diện tích trong HNL; AutoCAD Table thật cần plugin",
    actionKey: "OPEN_TABLE_BUILDER",
  },
  {
    id: "cmd_dt_5",
    title: "Xuất Excel thống kê diện tích (BOQ)",
    category: "Thống kê",
    shortcut: "APEXCELEXPORT",
    keywords: ["diện tích", "xuất excel", "boq", "khối lượng", "csv", "xlsx"],
    description: "Tải file Excel báo cáo khối lượng vật tư và diện tích",
    actionKey: "OPEN_EXCEL_EXPORT",
  },
  {
    id: "cmd_tuong_100",
    title: "Vẽ tường 100mm thông minh",
    category: "Vẽ nhanh",
    shortcut: "W100",
    keywords: ["w100", "tường", "tuong", "100", "wall", "vẽ nhanh", "bo góc"],
    description: "Vẽ tim trục, tự offset ±50mm, join góc và hatch",
    actionKey: "SMART_WALL_100",
  },
  {
    id: "cmd_tuong_200",
    title: "Vẽ tường 200mm thông minh",
    category: "Vẽ nhanh",
    shortcut: "W200",
    keywords: ["w200", "tường", "tuong", "200", "wall", "chịu lực", "xây"],
    description: "Vẽ tim trục, tự offset ±100mm, join góc và hatch",
    actionKey: "SMART_WALL_200",
  },
  {
    id: "cmd_direct_dwg_edit",
    title: "Direct DWG Edit • HNL",
    category: "DWG / AutoCAD Bridge",
    shortcut: "",
    keywords: ["direct dwg","live sync","dwg edit","chỉnh dwg","autocad bridge"],
    description: "Mở DWG thật và thao tác từ HNL Canvas trong khi AutoCAD giữ database native.",
    actionKey: "OPEN_DIRECT_DWG",
  },
  {
    id: "cmd_smart_shopdrawing_platform",
    title: "HNL Smart Shopdrawing Platform",
    category: "Shopdrawing / HNL",
    shortcut: "HNL",
    keywords: ["library","block","ceiling","wall","approved material","boq","audit","detail","template","dwg","trần","vách","thư viện","shopdrawing"],
    description: "Smart Library + Smart Ceiling + Smart Wall + Approved Material + BOQ + Audit + Detail + Template + Open DWG.",
    actionKey: "SMART_SHOPDRAWING",
  },
  {
    id: "cmd_tran_thach_cao",
    title: "Bố trí trần thạch cao chìm",
    category: "Smart Draw",
    shortcut: "APCEILING",
    keywords: ["trần", "tran", "thạch cao", "gypsum", "xương", "ty treo", "đèn"],
    description: "Tự động rải xương chính, xương phụ theo tấm 1220/3 = 406.67mm, ty treo theo cấu hình",
    actionKey: "SMART_CEILING",
  },
  {
    id: "cmd_block_library",
    title: "Thư viện Block & Thiết bị",
    category: "Block",
    shortcut: "B",
    keywords: ["b", "block", "i", "insert", "thư viện", "đèn", "thiết bị"],
    description: "Mở thư viện Block thông minh và chèn thiết bị vào bản vẽ",
    actionKey: "OPEN_BLOCK_LIBRARY",
  },
  {
    id: "cmd_block_sim",
    title: "Block Similarity - Tìm block giống nhau",
    category: "Block",
    shortcut: "APBLOCKSIM",
    keywords: ["block", "giống nhau", "similarity", "trùng", "gom nhóm", "hợp nhất"],
    description: "Nhận diện block hình học gần giống nhau dù khác tên",
    actionKey: "BLOCK_SIMILARITY",
  },
  {
    id: "cmd_translate_bi",
    title: "Dịch thuật bản vẽ Song Ngữ (Việt - Anh)",
    category: "Dịch thuật",
    shortcut: "APTRANSLATE",
    keywords: ["dịch", "dich", "translate", "tiếng anh", "english", "song ngữ"],
    description: "Dịch text và ghi chú kỹ thuật, giữ tiếng Việt phía trên",
    actionKey: "TRANSLATE_VI_EN_BILINGUAL",
  },
  {
    id: "cmd_drywall_ceiling_kb",
    title: "Knowledge Base Shopdrawing Thạch Cao & PCCC",
    category: "AI CAD / Shopdrawing",
    shortcut: "HNLDRYWALL",
    keywords: ["thạch cao", "vách", "trần", "pccc", "ei30", "ei60", "ei90", "ei120", "deflection", "mep", "rockwool", "chia ô", "lay-in"],
    description: "Tra cứu cấu tạo chuẩn, kiểm tra PCCC, tính toán chia ô trần nổi và kiểm tra xung đột MEP",
    actionKey: "OPEN_DRYWALL_STUDIO",
  },
  {
    id: "cmd_auto_detail_composer",
    title: "AI Auto Detail & Layout Composer",
    category: "AI CAD / Layout",
    shortcut: "APAUTODETAIL",
    keywords: ["detail", "chi tiết", "trích chi tiết", "mặt cắt", "section", "shopdrawing", "composer", "dàn trang", "sheet"],
    description: "AI tự động phân tích độ phức tạp bản vẽ, trích Detail 1:10, mặt cắt & dàn trang Sheet A1/A3 hoàn chỉnh",
    actionKey: "AI_AUTO_DETAIL",
  },
  {
    id: "cmd_build_exe",
    title: "Đóng Gói Ứng Dụng Chạy Độc Lập (.EXE Standalone)",
    category: "Cài đặt & Build",
    shortcut: "APEXE",
    keywords: ["exe", "build", "độc lập", "doc lap", "standalone", "electron", "installer", "portable", "windows"],
    description: "Mở hướng dẫn/cấu hình build bộ cài .EXE Windows 64-bit cho ứng dụng Standalone",
    actionKey: "OPEN_STANDALONE_EXE_BUILDER",
  },
  {
    id: "cmd_layout_a3",
    title: "Tạo Layout A3 & Auto Fit Viewport",
    category: "Layout",
    shortcut: "APLAYOUT",
    keywords: ["layout", "viewport", "a3", "tỷ lệ", "khung tên", "fit"],
    description: "Tự động chọn khổ giấy A3, tính tỷ lệ tối ưu và khóa Viewport",
    actionKey: "AUTO_LAYOUT_A3",
  },
  {
    id: "cmd_audit_draw",
    title: "Kiểm tra lỗi bản vẽ (CAD Audit)",
    category: "Audit",
    shortcut: "APAUDIT",
    keywords: ["audit", "kiểm tra", "lỗi", "field", "viewport", "layer"],
    description: "Quét toàn bộ drawing tìm field hỏng, text đè nhau, polyline hở",
    actionKey: "OPEN_AUDIT_MODAL",
  },
  {
    id: "cmd_ai_lisp",
    title: "AI AutoLISP Builder",
    category: "AI",
    shortcut: "APLISP",
    keywords: ["lisp", "viết lisp", "ai lisp", "tạo lệnh", "code"],
    description: "Tự động sinh mã AutoLISP chuẩn AutoCAD từ mô tả tiếng Việt",
    actionKey: "OPEN_LISP_BUILDER",
  },
  {
    id: "cmd_section_gen",
    title: "Tạo Mặt Cắt Tham Số Tự Động (Parametric Cross Section)",
    category: "Shopdrawing",
    shortcut: "APSECTION",
    keywords: ["mặt cắt", "mat cat", "section", "cross section", "a-a", "b-b", "trần vách", "tham số"],
    description: "Tạo mặt cắt chi tiết A-A hoặc B-B dọc theo đường cắt chỉ định với đầy đủ khung xương và ty treo",
    actionKey: "OPEN_SECTION_GEN",
  },
  {
    id: "cmd_mep_clash",
    title: "Kiểm Tra Va Chạm MEP & Gia Cố Khung Xương (Clash Detection)",
    category: "Shopdrawing / MEP",
    shortcut: "APCLASH",
    keywords: ["clash", "va chạm", "mep", "xung đột", "ống gió", "đèn", "pccc", "gia cố"],
    description: "Phát hiện xung đột giữa ống gió HVAC, đèn downlight, sprinkler PCCC với khung xương và tự sinh thanh gia cố",
    actionKey: "OPEN_MEP_CLASH",
  },
  {
    id: "cmd_multi_export",
    title: "Bộ Máy Xuất Bản Đa Định Dạng (DXF / Excel BOQ / PDF / Net)",
    category: "Xuất bản",
    shortcut: "APEXPORT",
    keywords: ["xuất", "export", "dxf", "excel", "boq", "pdf", "net", "autocad 2000"],
    description: "Xuất file AutoCAD DXF chuẩn R2000, bảng BOQ CSV và bản vẽ PDF Layout sẵn sàng in ấn",
    actionKey: "OPEN_MULTI_EXPORT",
  },
  {
    id: "cmd_building_code",
    title: "Tra Cứu Tiêu Chuẩn Xây Dựng TCVN 9377 / ASTM C636 & Tính Tải Trọng",
    category: "Tiêu chuẩn & Tính toán",
    shortcut: "APSTANDARD",
    keywords: ["tiêu chuẩn", "tieu chuan", "tcvn", "astm", "qcvn 06", "tải trọng", "ty treo", "chống cháy", "ei60"],
    description: "Tra cứu quy chuẩn PCCC QCVN 06:2022, TCVN 9377:2012, ASTM C636 và tính toán sức chịu tải ty treo trần",
    actionKey: "OPEN_BUILDING_CODE",
  },
];

const proActionByCenter:Record<string,string>={
  TEXT:"SMART_TEXT_CENTER",
  BLOCK:"BLOCK_ATTRIBUTE_CENTER",
  FIELD:"FIELD_DOCTOR_CENTER",
  GEOMETRY:"GEOMETRY_TOOL_CENTER",
  DIMENSION:"QUICK_DIM_CENTER",
  LAYER:"LAYER_DATA_CENTER",
  QUANTITY:"QUANTITY_CENTER",
  SHOPDRAWING:"SHOPDRAWING_2D_CENTER",
  LAYOUT:"LAYOUT_AUTOMATION_CENTER",
  TOOLS:"OPEN_2D_PRO_CENTER"
};
const PRO_2D_COMMANDS:CommandItem[]=CANONICAL_LISP_TOOLS.map(t=>({
  id:`pro2d_${t.id.toLowerCase()}`,
  title:t.name,
  category:"2D Professional",
  shortcut:t.sources[0]?.split("/")[0],
  keywords:[t.id,t.name,t.summary,...t.sources].map(x=>String(x).toLowerCase()),
  description:`${t.summary} • ${t.mode} • ${t.sources.join(", ")}`,
  actionKey:proActionByCenter[t.center]||"OPEN_2D_PRO_CENTER",
}));
const AUTOCAD_2023_COMMANDS:CommandItem[]=AUTOCAD_2023_COMMAND_KNOWLEDGE.map((item)=>({
  id:`acad23_${item.id.toLowerCase()}`,
  title:`AutoCAD 2023 • ${item.command} — ${item.title}`,
  category:`AutoCAD 2023 / ${item.category}`,
  shortcut:item.shortcut || item.aliases[0],
  keywords:[
    item.command,
    ...item.aliases,
    item.title,
    item.description,
    ...(item.options || []),
    ...item.workflow,
  ].map((x)=>String(x).toLowerCase()),
  description:`${item.description} • ${item.workflow.join(" → ")}${item.options?.length ? ` • Options: ${item.options.join(", ")}` : ""}`,
  actionKey:item.nativeAction ? `NATIVE:${item.nativeAction}` : "OPEN_USAGE_GUIDE",
}));

const COMMAND_DATABASE:CommandItem[]=[
  ...AUTOCAD_2023_COMMANDS,
  ...BASE_COMMAND_DATABASE,
  ...PRO_2D_COMMANDS,
];

export const CommandSearchModal: React.FC<CommandSearchModalProps> = ({
  isOpen,
  onClose,
  onSelectCommand,
}) => {
  const [query, setQuery] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (isOpen) {
      setQuery("");
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  // Global Ctrl + Space hotkey
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.code === "Space") {
        e.preventDefault();
        // Toggle search modal
        if (isOpen) onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const filtered = COMMAND_DATABASE.filter((cmd) => {
    if (!query) return true;
    const q = query.toLowerCase();
    return (
      cmd.title.toLowerCase().includes(q) ||
      cmd.category.toLowerCase().includes(q) ||
      cmd.shortcut?.toLowerCase().includes(q) ||
      cmd.keywords.some((k) => k.toLowerCase().includes(q))
    );
  });

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % Math.max(1, filtered.length));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelectedIndex((prev) => (prev - 1 + filtered.length) % Math.max(1, filtered.length));
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (filtered[selectedIndex]) {
        onSelectCommand(filtered[selectedIndex].actionKey);
        onClose();
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      onClose();
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-start justify-center pt-20 px-4">
      <div className="w-full max-w-2xl bg-[#1E1F22] rounded-xl border border-neutral-700/80 shadow-2xl overflow-hidden flex flex-col">
        {/* Search Input Bar */}
        <div className="flex items-center px-4 py-3 border-b border-neutral-800 bg-[#141517]">
          <Search className="w-5 h-5 text-cyan-400 mr-3" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setSelectedIndex(0);
            }}
            onKeyDown={handleKeyDown}
            placeholder="Tìm kiếm lệnh CAD (vd: 'diện tích', 'tường 100', 'layout', 'lisp')..."
            className="flex-1 bg-transparent text-neutral-100 placeholder-neutral-500 text-sm focus:outline-none"
          />
          <div className="flex items-center space-x-1 text-neutral-500 text-xs">
            <kbd className="px-1.5 py-0.5 rounded bg-neutral-800 border border-neutral-700 font-mono text-[10px]">
              ESC
            </kbd>
          </div>
        </div>

        {/* Results List */}
        <div className="max-h-96 overflow-y-auto p-2 space-y-1">
          {filtered.length === 0 ? (
            <div className="py-8 text-center text-neutral-400 text-xs space-y-1">
              <p>Không tìm thấy lệnh khớp với "{query}"</p>
              <p className="text-neutral-500">Thử tìm kiếm với: "diện tích", "tường", "trần", "block", "field"</p>
            </div>
          ) : (
            filtered.map((item, idx) => {
              const isSelected = idx === selectedIndex;
              return (
                <div
                  key={item.id}
                  onClick={() => {
                    onSelectCommand(item.actionKey);
                    onClose();
                  }}
                  onMouseEnter={() => setSelectedIndex(idx)}
                  className={`p-3 rounded-lg flex items-center justify-between cursor-pointer transition ${
                    isSelected ? "bg-cyan-500/20 border border-cyan-500/40 text-white" : "hover:bg-neutral-800/60 text-neutral-300"
                  }`}
                >
                  <div className="flex items-center space-x-3">
                    <div
                      className={`w-8 h-8 rounded-md flex items-center justify-center font-mono font-bold text-xs ${
                        isSelected ? "bg-cyan-500 text-black" : "bg-neutral-800 text-neutral-400"
                      }`}
                    >
                      {item.shortcut ? item.shortcut.substring(0, 3) : "CMD"}
                    </div>
                    <div>
                      <div className="font-semibold text-xs flex items-center space-x-2">
                        <span>{item.title}</span>
                        {item.shortcut && (
                          <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-black/40 text-cyan-400">
                            {item.shortcut}
                          </span>
                        )}
                      </div>
                      <div className="text-[11px] text-neutral-400">{item.description}</div>
                    </div>
                  </div>

                  <div className="flex items-center space-x-2 text-xs">
                    <span className="text-[10px] text-neutral-500 font-medium px-2 py-0.5 rounded bg-neutral-800">
                      {item.category}
                    </span>
                    {isSelected && <CornerDownLeft className="w-4 h-4 text-cyan-400" />}
                  </div>
                </div>
              );
            })
          )}
        </div>

        {/* Footer Hotkey Hints */}
        <div className="px-4 py-2 bg-[#141517] border-t border-neutral-800 text-[11px] text-neutral-500 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <span>
              <strong className="text-neutral-400">↑↓</strong> để điều hướng
            </span>
            <span>
              <strong className="text-neutral-400">ENTER</strong> để thực thi
            </span>
            <span>
              <strong className="text-neutral-400">ESC</strong> để đóng
            </span>
          </div>
          <span className="text-sky-400 font-medium">HNL CAD COMMAND ENGINE</span>
        </div>
      </div>
    </div>
  );
};
