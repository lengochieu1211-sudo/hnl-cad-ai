export type CeilingKnowledgeBrand = "HNL" | "KNAUF" | "VINH_TUONG" | "LE_TRAN" | "IS_DS";

export type SourceConfidence =
  | "MANUFACTURER_CURRENT_PAGE"
  | "MANUFACTURER_TECHNICAL_DOC"
  | "MANUFACTURER_LEGACY_DOC"
  | "MANUFACTURER_PROJECT_REFERENCE"
  | "HNL_PROJECT_RULE";

export interface CeilingKnowledgeSource {
  title: string;
  url: string;
  publishedOrRevision?: string;
  checkedOn: string;
  confidence: SourceConfidence;
  notes?: string;
}

export interface CeilingProfileSpec {
  role: "MAIN" | "CROSS" | "WALL_ANGLE" | "HANGER" | "OTHER";
  name: string;
  lengthMm?: number;
  widthMm?: number;
  heightMm?: number;
  thicknessMm?: number;
  coating?: string;
}

export interface CeilingSpacingSpec {
  mainMm?: number;
  mainMaxMm?: number;
  crossMm?: number;
  crossMaxMm?: number;
  hangerMm?: number;
  hangerMaxMm?: number;
  firstHangerMaxMm?: number;
  wallToMainMaxMm?: number;
  status: "PUBLISHED" | "PARTIAL" | "NOT_PUBLISHED_ON_PUBLIC_PAGE" | "PROJECT_RULE";
  notes?: string;
}

export interface ManufacturerCeilingKnowledge {
  id: string;
  brand: CeilingKnowledgeBrand;
  manufacturer: string;
  systemName: string;
  systemType: "CONCEALED" | "EXPOSED" | "DRYWALL" | "REFERENCE";
  standards: string[];
  profiles: CeilingProfileSpec[];
  spacing?: CeilingSpacingSpec;
  boardRules?: Array<{
    boardSizeMm?: [number, number];
    boardThicknessMm?: number[];
    layers?: number;
    note: string;
  }>;
  accessories?: string[];
  applications?: string[];
  warnings?: string[];
  sources: CeilingKnowledgeSource[];
}

const CHECKED_ON = "2026-08-23";

/**
 * HNL engineering rules explicitly separated from manufacturer catalog facts.
 * These are project/user rules and MUST NOT be presented as a manufacturer requirement.
 */
export const HNL_BOARD_MODULE_RULES = {
  concealedCeiling: {
    defaultBoardMm: [1220, 2440] as [number, number],
    crossRunnerDivision: 3,
    crossRunnerSpacingMm: 1220 / 3,
    formula: "1220 / 3",
    note: "Quy tắc HNL/project: tấm trần chìm 1220x2440, xương phụ theo module 1220/3 = 406.67mm. Không làm tròn 400mm.",
  },
  drywallWall: {
    defaultBoardMm: [1220, 2440] as [number, number],
    studDivisions: [3, 2],
    studSpacingMm: [1220 / 3, 1220 / 2],
    formulas: ["1220 / 3", "1220 / 2"],
    note: "Quy tắc HNL/project cho vách: stud theo 1220/3 = 406.67mm hoặc 1220/2 = 610mm.",
  },
};

export const MANUFACTURER_CEILING_KNOWLEDGE: ManufacturerCeilingKnowledge[] = [
  // ----------------------------------------------------------------
  // KNAUF VIETNAM
  // ----------------------------------------------------------------
  {
    id: "KNAUF_ULTRA_CONCEALED",
    brand: "KNAUF",
    manufacturer: "Knauf Vietnam",
    systemName: "Ultra concealed ceiling system",
    systemType: "CONCEALED",
    standards: ["ASTM C635", "TCVN 7470 (khung xương cá được Knauf mô tả trên trang khung kim loại)"],
    profiles: [],
    spacing: {
      mainMaxMm: 1200,
      crossMaxMm: 406,
      status: "PUBLISHED",
      notes: "Knauf công bố bảng hệ Ultra: thanh chính tối đa 1200mm, thanh phụ tối đa 406mm.",
    },
    boardRules: [
      { boardSizeMm: [1220, 2440], boardThicknessMm: [9, 12.7], layers: 1, note: "Knauf công bố hệ Ultra dùng tấm 9.0/12.7mm; kích thước tấm 1220x2440 có trong danh mục tấm." },
    ],
    warnings: ["Khoảng cách ty treo không được suy đoán từ khoảng cách thanh chính; phải đối chiếu tài liệu hệ/approved submittal."],
    sources: [
      {
        title: "Knauf Việt Nam – Tấm thạch cao / bảng giải pháp hệ trần",
        url: "https://knauf.com/vi-VN/ung-dung/tran/tran-thach-cao/tam-thach-cao",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },
  {
    id: "KNAUF_PRO_CONCEALED",
    brand: "KNAUF",
    manufacturer: "Knauf Vietnam",
    systemName: "Pro concealed ceiling system",
    systemType: "CONCEALED",
    standards: ["ASTM C635"],
    profiles: [],
    spacing: {
      mainMaxMm: 1100,
      crossMaxMm: 406,
      status: "PUBLISHED",
      notes: "Knauf công bố Pro: thanh chính tối đa 1100mm, thanh phụ tối đa 406mm.",
    },
    boardRules: [
      { boardSizeMm: [1220, 2440], boardThicknessMm: [9, 12.7], layers: 1, note: "Hệ Pro công bố 1 lớp tấm; loại/chiều dày phải chọn theo hệ phê duyệt." },
    ],
    sources: [
      {
        title: "Knauf Việt Nam – Tấm thạch cao / bảng giải pháp hệ trần",
        url: "https://knauf.com/vi-VN/ung-dung/tran/tran-thach-cao/tam-thach-cao",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
      {
        title: "Knauf – Pro V technical information",
        url: "https://knauf.com/api/download-center/v1/assets/2cf1357f-1d57-460a-86d5-5a0ce571f903?country=vn&download=true",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_TECHNICAL_DOC",
      },
    ],
  },
  {
    id: "KNAUF_XTRA_CONCEALED",
    brand: "KNAUF",
    manufacturer: "Knauf Vietnam",
    systemName: "Xtra concealed ceiling system",
    systemType: "CONCEALED",
    standards: ["ASTM C635"],
    profiles: [],
    spacing: {
      mainMaxMm: 1000,
      crossMaxMm: 406,
      status: "PUBLISHED",
      notes: "Knauf công bố Xtra: thanh chính tối đa 1000mm, thanh phụ tối đa 406mm.",
    },
    boardRules: [
      { boardSizeMm: [1220, 2440], boardThicknessMm: [9], layers: 1, note: "Bảng công bố hệ Xtra dùng tấm 9mm." },
    ],
    sources: [
      {
        title: "Knauf Việt Nam – Tấm thạch cao / bảng giải pháp hệ trần",
        url: "https://knauf.com/vi-VN/ung-dung/tran/tran-thach-cao/tam-thach-cao",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },
  {
    id: "KNAUF_PRO_V_PROFILE",
    brand: "KNAUF",
    manufacturer: "Knauf Vietnam",
    systemName: "Pro V concealed ceiling framing",
    systemType: "CONCEALED",
    standards: ["ASTM C635"],
    profiles: [
      { role:"MAIN", name:"Pro V main runner", lengthMm:3660, widthMm:20, heightMm:28, thicknessMm:0.72 },
      { role:"CROSS", name:"Pro C", lengthMm:4000, widthMm:35, heightMm:14, thicknessMm:0.4 },
      { role:"WALL_ANGLE", name:"V32", lengthMm:4000, widthMm:20, heightMm:20, thicknessMm:0.32 },
    ],
    spacing: {
      crossMaxMm: 406,
      status: "PARTIAL",
      notes: "Technical sheet tổng hợp các hệ Ultra/Pro/Xtra; Pro được công bố main max 1100mm và cross max 406mm. Profile Pro V/Pro C/V32 ghi riêng trong sheet.",
    },
    sources: [
      {
        title: "Knauf – Pro V technical information",
        url: "https://knauf.com/api/download-center/v1/assets/2cf1357f-1d57-460a-86d5-5a0ce571f903?country=vn&download=true",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_TECHNICAL_DOC",
      },
    ],
  },
  {
    id: "KNAUF_T3_EXPOSED",
    brand: "KNAUF",
    manufacturer: "Knauf Vietnam",
    systemName: "T3 exposed ceiling grid",
    systemType: "EXPOSED",
    standards: ["ASTM C635", "TCVN 12694"],
    profiles: [
      { role:"MAIN", name:"T3 main runner" },
      { role:"CROSS", name:"T3 long cross tee" },
      { role:"CROSS", name:"T3 short cross tee" },
    ],
    spacing: {
      mainMm: 1220,
      crossMm: 610,
      hangerMaxMm: 1220,
      firstHangerMaxMm: 610,
      status: "PUBLISHED",
      notes: "Knauf công bố hệ T3: main 1220; cross dài 610; cross ngắn 610; ty tối đa 1220; ty đầu tiên đến tường ≤610.",
    },
    sources: [
      {
        title: "Knauf Việt Nam – Trần nổi / T3 installation guidance",
        url: "https://knauf.com/vi-VN/ung-dung/tran/tran-thach-cao/tran-noi",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },

  // ----------------------------------------------------------------
  // VINH TUONG / SAINT-GOBAIN
  // ----------------------------------------------------------------
  {
    id: "VT_TIKA_GYPCEIL_2019",
    brand: "VINH_TUONG",
    manufacturer: "Vĩnh Tường / Saint-Gobain",
    systemName: "Vĩnh Tường TIKA – GypCeil / GypCeil Aqua",
    systemType: "CONCEALED",
    standards: ["ASTM C635"],
    profiles: [
      { role:"MAIN", name:"VTC-TIKA 4000", lengthMm:4000, widthMm:35, heightMm:14.5 },
      { role:"CROSS", name:"VTC-TIKA 4000", lengthMm:4000, widthMm:35, heightMm:14.5 },
      { role:"WALL_ANGLE", name:"VTC18/22-0.32", lengthMm:4000, widthMm:18, heightMm:22, thicknessMm:0.32 },
    ],
    spacing: {
      mainMm: 800,
      crossMm: 406,
      status: "PUBLISHED",
      notes: "Brochure hệ giải pháp TIKA ghi khẩu độ 800x406mm. Không tự suy ra bước ty từ hình nếu không có chú thích rõ.",
    },
    boardRules: [
      { boardThicknessMm:[9], layers:1, note:"Brochure nêu Gyproc tiêu chuẩn 9mm cho khu vực khô và Gyproc chịu ẩm 9mm cho khu vực ẩm." },
    ],
    accessories: ["Pát 2 lỗ", "Ty ren/ty dây", "Tắc kê thép", "Tăng đơ", "Khóa liên kết", "Vít liên kết khung", "Bột Gyp-Filler", "Băng giấy/băng lưới"],
    sources: [
      {
        title: "Vĩnh Tường – Brochure khung trần chìm TIKA",
        url: "https://vinhtuong.com/sites/default/files/2020-02/brochure_khung-tran-chim-vinh-tuong-tika_apr2019.pdf",
        publishedOrRevision: "2019",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_TECHNICAL_DOC",
        notes: "Catalog theo revision; HNL phải ưu tiên approved submittal dự án nếu khác.",
      },
    ],
  },
  {
    id: "VT_OMEGA_LEGACY",
    brand: "VINH_TUONG",
    manufacturer: "Vĩnh Tường",
    systemName: "Vĩnh Tường OMEGA – legacy technical table",
    systemType: "CONCEALED",
    standards: ["ASTM C635-07"],
    profiles: [
      { role:"MAIN", name:"VTC-OMEGA200", lengthMm:3660, widthMm:20.5, heightMm:30, thicknessMm:0.50 },
      { role:"MAIN", name:"VTC-OMEGA204", lengthMm:3660, widthMm:37, heightMm:23, thicknessMm:0.44 },
      { role:"WALL_ANGLE", name:"VTC20/22-0.4", widthMm:20, heightMm:22, thicknessMm:0.40 },
    ],
    spacing: {
      crossMm: 406,
      mainMaxMm: 1200,
      hangerMaxMm: 1200,
      status: "PUBLISHED",
      notes: "Bảng chất lượng legacy công bố thanh phụ 406mm, thanh chính ≤1200mm, treo ty ≤1200mm.",
    },
    warnings: ["Tài liệu legacy; không dùng làm mặc định cho hệ ALPHA/BASI/TIKA hiện hành nếu chưa đối chiếu tài liệu dự án."],
    sources: [
      {
        title: "Vĩnh Tường – Bảng tiêu chuẩn chất lượng sản phẩm (legacy)",
        url: "https://vinhtuong.com/Data/Sites/1/media/tieu-chuan-ve-chat-luong-san-pham/dbd1022a-24d8-40b9-be59-eea52d11a26c.pdf",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_LEGACY_DOC",
      },
    ],
  },
  {
    id: "VT_CURRENT_CONCEALED_FAMILIES",
    brand: "VINH_TUONG",
    manufacturer: "Vĩnh Tường / Saint-Gobain",
    systemName: "Current concealed ceiling families",
    systemType: "REFERENCE",
    standards: ["ASTM C635 (ALPHA page states compliance)"],
    profiles: [],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "Trang danh mục hiện hành liệt kê ALPHA, BASI Plus, TIKA, EKO Plus; không dùng một bước chung cho tất cả hệ.",
    },
    applications: ["Trần trang trí", "Chống ẩm", "Tiêu âm", "Các hệ chức năng tùy tấm/khung đồng bộ"],
    sources: [
      {
        title: "Vĩnh Tường – Danh sách khung trần chìm",
        url: "https://vinhtuong.com/danh-sach-san-pham-cau-kien?product_component_tags=16",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
      {
        title: "Vĩnh Tường – ALPHA",
        url: "https://vinhtuong.com/vinh-tuong-alpha",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },

  // ----------------------------------------------------------------
  // LE TRAN
  // ----------------------------------------------------------------
  {
    id: "LETRAN_MACROTEK",
    brand: "LE_TRAN",
    manufacturer: "Lê Trần",
    systemName: "MacroTEK concealed ceiling",
    systemType: "CONCEALED",
    standards: ["ASTM C635", "ISO 9001:2015"],
    profiles: [
      { role:"MAIN", name:"MacroTEK Ultra 500", lengthMm:4000, widthMm:37, heightMm:15, thicknessMm:0.50 },
      { role:"CROSS", name:"MacroTEK Ultra 500", lengthMm:4000, widthMm:37, heightMm:15, thicknessMm:0.50 },
      { role:"MAIN", name:"MacroTEK S600", lengthMm:4000, widthMm:35, heightMm:14, thicknessMm:0.60 },
      { role:"MAIN", name:"MacroTEK S500", lengthMm:4000, widthMm:35, heightMm:14, thicknessMm:0.50 },
      { role:"MAIN", name:"MacroTEK S400", lengthMm:4000, widthMm:35, heightMm:14, thicknessMm:0.40 },
      { role:"WALL_ANGLE", name:"MacroTEK wall angle", lengthMm:4000, widthMm:21, heightMm:21 },
    ],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "Trang sản phẩm công bố profile/kết cấu/phụ kiện nhưng không công bố khẩu độ lắp đặt. HNL không tự gán bước hãng.",
    },
    accessories: ["Ty treo", "Tắc kê", "Bát treo", "Tăng đơ", "Khóa liên kết", "Khớp nối thanh chính", "Khớp nối thanh phụ"],
    applications: ["Hệ trần chìm đồng dạng", "Thi công nhanh", "Trần yêu cầu thẩm mỹ/chống cháy/cách âm tùy cấu tạo"],
    sources: [
      {
        title: "Lê Trần – MacroTEK",
        url: "https://letran.vn/khung-tran-chim-2/macrotek",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },
  {
    id: "LETRAN_CHANNELTEK",
    brand: "LE_TRAN",
    manufacturer: "Lê Trần",
    systemName: "ChannelTEK concealed ceiling",
    systemType: "CONCEALED",
    standards: ["ASTM", "ISO 9001:2015"],
    profiles: [
      { role:"MAIN", name:"ChannelTEK Ultra 38", lengthMm:3660, widthMm:20, heightMm:38, thicknessMm:0.80 },
      { role:"CROSS", name:"MacroTEK Ultra", lengthMm:4000, widthMm:37, heightMm:15, thicknessMm:0.50 },
      { role:"MAIN", name:"ChannelTEK Plus 38", lengthMm:3660, widthMm:20, heightMm:38, thicknessMm:0.72 },
      { role:"MAIN", name:"ChannelTEK Pro 38", lengthMm:3660, widthMm:20, heightMm:38, thicknessMm:0.60 },
      { role:"MAIN", name:"ChannelTEK Ultra 28", lengthMm:3660, widthMm:20, heightMm:28, thicknessMm:0.80 },
    ],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "ChannelTEK là hệ răng cưa/gài trực tiếp; trang công khai chưa công bố khẩu độ nên HNL chỉ dùng profile đã xác minh.",
    },
    accessories: ["Ty ren"],
    applications: ["Trần diện tích lớn", "Khu vực có áp lực gió mạnh", "Khu vực có rung động", "Yêu cầu kỹ thuật cao"],
    sources: [
      {
        title: "Lê Trần – ChannelTEK",
        url: "https://letran.vn/khung-tran-chim-2/channeltek",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },
  {
    id: "LETRAN_GYPLINE",
    brand: "LE_TRAN",
    manufacturer: "Lê Trần",
    systemName: "GypLINE concealed ceiling",
    systemType: "CONCEALED",
    standards: ["ASTM C635"],
    profiles: [
      { role:"MAIN", name:"GypLINE U", lengthMm:4000, widthMm:35, heightMm:14 },
      { role:"CROSS", name:"GypLINE U", lengthMm:4000, widthMm:35, heightMm:14 },
      { role:"WALL_ANGLE", name:"GypLINE V", lengthMm:4000, widthMm:21, heightMm:21 },
    ],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "GypLINE phù hợp diện tích vừa/nhỏ; public page không nêu khẩu độ.",
    },
    applications: ["Nhà ở", "Công trình thương mại", "Trần chìm diện tích vừa và nhỏ"],
    sources: [
      {
        title: "Lê Trần – GypLINE",
        url: "https://letran.vn/san-pham-kenh-phan-phoi/gypline",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_CURRENT_PAGE",
      },
    ],
  },

  // ----------------------------------------------------------------
  // I.S / D&S
  // ----------------------------------------------------------------
  {
    id: "IS_DS_CONCEALED_FAMILIES",
    brand: "IS_DS",
    manufacturer: "I.S Corporation / D&S",
    systemName: "D&S concealed ceiling framing families",
    systemType: "REFERENCE",
    standards: [],
    profiles: [],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "Hồ sơ năng lực I.S công bố danh mục hệ trần chìm D&S nhưng không nêu khẩu độ trong phần dữ liệu web đã xác minh.",
    },
    applications: [
      "D&S MONO",
      "D&S PARA 1",
      "D&S PARA 3",
      "D&S PARA 5",
      "D&S GAMMA 2",
      "D&S GAMMA 6",
      "D&S SUPER 8",
      "D&S SUPER 10",
    ],
    warnings: ["Không suy ra bước xương/ty từ tên PARA 1/3/5; cần catalog/shopdrawing/approved material cụ thể."],
    sources: [
      {
        title: "I.S Corporation – Company Profile 2020",
        url: "https://isco.com.vn/pdf/0-ho-so-nang-luc-cong-ty-is---r2020_vi_1604493019.pdf",
        publishedOrRevision: "R2020",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_TECHNICAL_DOC",
      },
    ],
  },
  {
    id: "IS_DS_PARA5_PROJECT_REFERENCES",
    brand: "IS_DS",
    manufacturer: "I.S Corporation / D&S",
    systemName: "D&S PARA 5 – verified project usage references",
    systemType: "REFERENCE",
    standards: [],
    profiles: [],
    spacing: {
      status: "NOT_PUBLISHED_ON_PUBLIC_PAGE",
      notes: "Project references prove actual product usage, NOT installation spacing.",
    },
    applications: [
      "Becamex International Hospital: Boral gypsum + D&S Para 5 concealed ceiling",
      "Ho Tram Casino: USG Fiberock 6mm + D&S Para 5",
      "Nisshin Technomic Premix Factory: Knauf 12.5mm + 9.5mm two-layer ceiling + D&S Para 5",
      "Mekophar: Boral gypsum + D&S Para 5",
    ],
    sources: [
      {
        title: "I.S – Becamex International Hospital",
        url: "https://www.isco.com.vn/vi/du-an/thuong-mai-2/benh-vien-quoc-te-becamex.html",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_PROJECT_REFERENCE",
      },
      {
        title: "I.S – Ho Tram Casino",
        url: "https://isco.com.vn/vi/du-an/nha-may/ho-tram-casino.html",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_PROJECT_REFERENCE",
      },
      {
        title: "I.S – Vietnam Nisshin Technomic Premix New Factory",
        url: "https://www.isco.com.vn/en/projects/nha-may-1/vietnam-nisshin-technomic-premix-new-factory/vietnam-nisshin-technomic-premix-new-factory.html",
        checkedOn: CHECKED_ON,
        confidence: "MANUFACTURER_PROJECT_REFERENCE",
      },
    ],
  },
];

export const MANUFACTURER_CEILING_BRANDS = [
  { id:"ALL", label:"Tất cả" },
  { id:"KNAUF", label:"Knauf" },
  { id:"VINH_TUONG", label:"Vĩnh Tường" },
  { id:"LE_TRAN", label:"Lê Trần" },
  { id:"IS_DS", label:"I.S / D&S" },
] as const;

export function getManufacturerCeilingAiContext() {
  return {
    hnlProjectRules: HNL_BOARD_MODULE_RULES,
    manufacturerSystems: MANUFACTURER_CEILING_KNOWLEDGE.map((item) => ({
      id: item.id,
      brand: item.brand,
      manufacturer: item.manufacturer,
      systemName: item.systemName,
      systemType: item.systemType,
      standards: item.standards,
      profiles: item.profiles,
      spacing: item.spacing,
      boardRules: item.boardRules,
      applications: item.applications,
      warnings: item.warnings,
      sources: item.sources.map((s) => ({
        title: s.title,
        confidence: s.confidence,
        revision: s.publishedOrRevision,
      })),
    })),
    interpretationRule:
      "Không trộn bước lắp đặt giữa các hãng/hệ/revision. HNL project rule 1220/3=406.67mm phải được ghi rõ là project rule; manufacturer values giữ nguyên số công bố (ví dụ 406mm). Khi thiếu số hãng, yêu cầu Approved Material/Catalog, không tự suy đoán.",
  };
}
