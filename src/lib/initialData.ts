import {
  CadEntity,
  CadLayer,
  CadLayout,
  CadViewport,
  BlockLibraryItem,
  LispScriptItem,
  ProjectStandard,
  TranslationMemoryItem,
  DrawingAuditIssue,
} from "../types/cad";

export const INITIAL_LAYERS: CadLayer[] = [
  { name: "0", color: "#FFFFFF", lineweight: 0.25, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "DEFPOINTS", color: "#808080", lineweight: 0.05, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: false },
  { name: "KT_TRUC", color: "#E040FB", lineweight: 0.13, linetype: "Center2", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_COT", color: "#FF5252", lineweight: 0.35, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_TUONG", color: "#00E5FF", lineweight: 0.3, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_TRAN_XUONG", color: "#FFB300", lineweight: 0.18, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_TRAN_TY", color: "#76FF03", lineweight: 0.15, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_CUA", color: "#40C4FF", lineweight: 0.2, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_DEN", color: "#FFFF00", lineweight: 0.25, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_DIM", color: "#00E676", lineweight: 0.15, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_TEXT", color: "#FFFFFF", lineweight: 0.2, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_KHUNG_TEN", color: "#651FFF", lineweight: 0.5, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: true },
  { name: "KT_VIEWPORT", color: "#00E5FF", lineweight: 0.1, linetype: "Continuous", isLocked: false, isVisible: true, isPlottable: false },
];

export const INITIAL_ENTITIES: CadEntity[] = [
  // Room 1: Living Room / Phòng Khách (0,0 to 6000, 4500)
  {
    id: "wall_1",
    handle: "1A01",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#00E5FF",
    p1: { x: 0, y: 0 },
    p2: { x: 6000, y: 0 },
    thickness: 200,
    wallType: "BRICK_200",
  },
  {
    id: "wall_2",
    handle: "1A02",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#00E5FF",
    p1: { x: 6000, y: 0 },
    p2: { x: 6000, y: 4500 },
    thickness: 200,
    wallType: "BRICK_200",
  },
  {
    id: "wall_3",
    handle: "1A03",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#00E5FF",
    p1: { x: 6000, y: 4500 },
    p2: { x: 0, y: 4500 },
    thickness: 200,
    wallType: "BRICK_200",
  },
  {
    id: "wall_4",
    handle: "1A04",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#00E5FF",
    p1: { x: 0, y: 4500 },
    p2: { x: 0, y: 0 },
    thickness: 200,
    wallType: "BRICK_200",
  },

  // Partition Wall for Bedroom / Phòng Ngủ (6000 to 10000, 0 to 4500)
  {
    id: "wall_5",
    handle: "1A05",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#FF9100",
    p1: { x: 6000, y: 0 },
    p2: { x: 10000, y: 0 },
    thickness: 100,
    wallType: "BRICK_100",
  },
  {
    id: "wall_6",
    handle: "1A06",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#FF9100",
    p1: { x: 10000, y: 0 },
    p2: { x: 10000, y: 4500 },
    thickness: 200,
    wallType: "BRICK_200",
  },
  {
    id: "wall_7",
    handle: "1A07",
    type: "WALL",
    layer: "KT_TUONG",
    color: "#FF9100",
    p1: { x: 10000, y: 4500 },
    p2: { x: 6000, y: 4500 },
    thickness: 200,
    wallType: "BRICK_200",
  },

  // Gypsum Ceiling Grid inside Living Room
  {
    id: "ceil_1",
    handle: "2B01",
    type: "CEILING_GRID",
    layer: "KT_TRAN_XUONG",
    color: "#FFB300",
    boundary: [
      { x: 150, y: 150 },
      { x: 5850, y: 150 },
      { x: 5850, y: 4350 },
      { x: 150, y: 4350 },
    ],
    mainSpacing: 800,
    subSpacing: 400,
    hangerSpacing: 1000,
    wallAngleOffset: 25,
    ceilingType: "SUSPENDED_GYPSUM",
    levelElevation: 2800,
  },

  // Lights (Blocks) in Living Room
  {
    id: "blk_light_1",
    handle: "3C01",
    type: "BLOCK_REF",
    blockName: "DEN_DOWNLIGHT_D90",
    category: "MEP",
    layer: "KT_DEN",
    color: "#FFFF00",
    position: { x: 1500, y: 1500 },
    scale: { x: 1, y: 1 },
    rotation: 0,
    attributes: { TYPE: "DL-12W", MODEL: "PHILIPS-D90", POWER: "12W" },
  },
  {
    id: "blk_light_2",
    handle: "3C02",
    type: "BLOCK_REF",
    blockName: "DEN_DOWNLIGHT_D90",
    category: "MEP",
    layer: "KT_DEN",
    color: "#FFFF00",
    position: { x: 4500, y: 1500 },
    scale: { x: 1, y: 1 },
    rotation: 0,
    attributes: { TYPE: "DL-12W", MODEL: "PHILIPS-D90", POWER: "12W" },
  },
  {
    id: "blk_light_3",
    handle: "3C03",
    type: "BLOCK_REF",
    blockName: "DEN_DOWNLIGHT_D90",
    category: "MEP",
    layer: "KT_DEN",
    color: "#FFFF00",
    position: { x: 1500, y: 3000 },
    scale: { x: 1, y: 1 },
    rotation: 0,
    attributes: { TYPE: "DL-12W", MODEL: "PHILIPS-D90", POWER: "12W" },
  },
  {
    id: "blk_light_4",
    handle: "3C04",
    type: "BLOCK_REF",
    blockName: "DEN_DOWNLIGHT_D90",
    category: "MEP",
    layer: "KT_DEN",
    color: "#FFFF00",
    position: { x: 4500, y: 3000 },
    scale: { x: 1, y: 1 },
    rotation: 0,
    attributes: { TYPE: "DL-12W", MODEL: "PHILIPS-D90", POWER: "12W" },
  },
  {
    id: "blk_light_panel",
    handle: "3C05",
    type: "BLOCK_REF",
    blockName: "DEN_PANEL_600x600",
    category: "MEP",
    layer: "KT_DEN",
    color: "#FFFF00",
    position: { x: 3000, y: 2250 },
    scale: { x: 1, y: 1 },
    rotation: 0,
    attributes: { TYPE: "PANEL-48W", MODEL: "RANGDONG-6060", POWER: "48W" },
  },

  // Room Text & Annotations with Field Simulation
  {
    id: "txt_room1",
    handle: "4D01",
    type: "TEXT",
    layer: "KT_TEXT",
    color: "#00E676",
    text: "PHÒNG KHÁCH (LIVING ROOM)",
    position: { x: 3000, y: 1200 },
    height: 250,
    hasField: true,
    fieldFormula: "FIELD(AREA: poly_room1)",
  },
  {
    id: "txt_area1",
    handle: "4D02",
    type: "TEXT",
    layer: "KT_TEXT",
    color: "#00E676",
    text: "S = 27.00 m²  |  H = +2.800m",
    position: { x: 3000, y: 900 },
    height: 200,
  },
  {
    id: "txt_room2",
    handle: "4D03",
    type: "TEXT",
    layer: "KT_TEXT",
    color: "#00E676",
    text: "PHÒNG NGỦ 01 (BEDROOM 01)",
    position: { x: 8000, y: 2250 },
    height: 250,
  },
  {
    id: "txt_area2",
    handle: "4D04",
    type: "TEXT",
    layer: "KT_TEXT",
    color: "#00E676",
    text: "S = 18.00 m²  |  H = +2.800m",
    position: { x: 8000, y: 1950 },
    height: 200,
  },

  // Dimensions
  {
    id: "dim_1",
    handle: "5E01",
    type: "DIMENSION",
    layer: "KT_DIM",
    color: "#00E676",
    p1: { x: 0, y: -400 },
    p2: { x: 6000, y: -400 },
    dimLineOffset: 400,
    measurement: 6000,
  },
  {
    id: "dim_2",
    handle: "5E02",
    type: "DIMENSION",
    layer: "KT_DIM",
    color: "#00E676",
    p1: { x: 6000, y: -400 },
    p2: { x: 10000, y: -400 },
    dimLineOffset: 400,
    measurement: 4000,
  },
  {
    id: "dim_total",
    handle: "5E03",
    type: "DIMENSION",
    layer: "KT_DIM",
    color: "#00E676",
    p1: { x: 0, y: -900 },
    p2: { x: 10000, y: -900 },
    dimLineOffset: 900,
    measurement: 10000,
  },
  {
    id: "dim_height",
    handle: "5E04",
    type: "DIMENSION",
    layer: "KT_DIM",
    color: "#00E676",
    p1: { x: -400, y: 0 },
    p2: { x: -400, y: 4500 },
    dimLineOffset: 400,
    measurement: 4500,
  },

  // Multileader (MLeader) Annotations - Priority Annotation Standard
  {
    id: "mld_drywall_ei60",
    handle: "6F01",
    type: "MLEADER",
    layer: "KT_TEXT",
    color: "#00E5FF",
    leaderPoints: [
      { x: 6000, y: 2250 },
      { x: 5200, y: 3500 },
    ],
    text: "VÁCH THẠCH CAO PCCC EI60 (DW4)\n- Khung C75 @400mm + U76 sàn/trần\n- Lõi bông Rockwool 50mm (60kg/m³)\n- 2 mặt ốp 2 lớp tấm Fire-Stop 12.5mm",
    textPosition: { x: 5200, y: 3500 },
    landingDistance: 450,
  },
  {
    id: "mld_ceiling_spec",
    handle: "6F02",
    type: "MLEADER",
    layer: "KT_TEXT",
    color: "#FFB300",
    leaderPoints: [
      { x: 3000, y: 3000 },
      { x: 2200, y: 4000 },
    ],
    text: "TRẦN THẠCH CAO CHÌM (TC-01)\n- Xương chính C-Channel @800mm\n- Xương phụ Furring @400mm\n- Ty ren M8 @1000mm + Tấm chống ẩm 9.5mm",
    textPosition: { x: 2200, y: 4000 },
    landingDistance: 420,
  },
  {
    id: "mld_light_spec",
    handle: "6F03",
    type: "MLEADER",
    layer: "KT_TEXT",
    color: "#FFFF00",
    leaderPoints: [
      { x: 4500, y: 3000 },
      { x: 5400, y: 3800 },
    ],
    text: "ĐÈN DOWNLIGHT LED (DL-01)\n- Philips D90mm - 12W 4000K\n- Lắp âm trần thạch cao",
    textPosition: { x: 5400, y: 3800 },
    landingDistance: 350,
  },
];

export const INITIAL_LAYOUTS: CadLayout[] = [
  {
    id: "layout_a3_kt01",
    name: "KT-01 MẶT BẰNG",
    paperSize: "A3",
    orientation: "Landscape",
    widthMm: 420,
    heightMm: 297,
    marginMm: 15,
    titleBlockName: "HNL_TITLE_A3",
    drawingName: "MẶT BẰNG BỐ TRÍ TRẦN & ĐÈN TẦNG 1",
    drawingNo: "HNL-KT-01",
    scale: "1:100",
    revision: "REV 01",
    date: "2026-08-21",
  },
  {
    id: "layout_a3_kt02",
    name: "KT-02 CHI TIẾT TRẦN",
    paperSize: "A3",
    orientation: "Landscape",
    widthMm: 420,
    heightMm: 297,
    marginMm: 15,
    titleBlockName: "HNL_TITLE_A3",
    drawingName: "CHI TIẾT LẮP ĐẶT KHUNG XƯƠNG THẠCH CAO",
    drawingNo: "HNL-KT-02",
    scale: "1:20",
    revision: "REV 00",
    date: "2026-08-21",
  },
];

export const INITIAL_VIEWPORTS: CadViewport[] = [
  {
    id: "vp_main_1",
    handle: "VP01",
    type: "VIEWPORT",
    layoutName: "KT-01 MẶT BẰNG",
    layer: "KT_VIEWPORT",
    x: 30,
    y: 35,
    width: 250,
    height: 180,
    modelCenter: { x: 5000, y: 2250 },
    scale: "1:100",
    scaleFactor: 0.01,
    locked: true,
    title: "MẶT BẰNG TẦNG 01",
    detailNumber: "01",
  },
  {
    id: "vp_detail_1",
    handle: "VP02",
    type: "VIEWPORT",
    layoutName: "KT-01 MẶT BẰNG",
    layer: "KT_VIEWPORT",
    x: 290,
    y: 120,
    width: 100,
    height: 95,
    modelCenter: { x: 3000, y: 2250 },
    scale: "1:50",
    scaleFactor: 0.02,
    locked: false,
    title: "CHI TIẾT ĐÈN PANEL",
    detailNumber: "02",
  },
];

export const INITIAL_BLOCK_LIBRARY: BlockLibraryItem[] = [
  {
    id: "blk_lib_1",
    name: "DEN_DOWNLIGHT_D90",
    category: "MEP",
    tags: ["đèn", "downlight", "chiếu sáng", "d90", "philips"],
    defaultAttributes: { TYPE: "DL-12W", POWER: "12W", CRI: "90" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_2",
    name: "DEN_PANEL_600x600",
    category: "MEP",
    tags: ["đèn", "panel", "600x600", "trần thạch cao"],
    defaultAttributes: { TYPE: "PANEL-48W", LUMEN: "4800lm" },
    isDynamic: false,
    unit: "mm",
  },
  {
    id: "blk_lib_3",
    name: "CUA_DI_1_CANH_900",
    category: "Kiến trúc",
    tags: ["cửa", "cửa đi", "door", "1 cánh", "d900"],
    defaultAttributes: { MA_CUA: "D1", KICH_THUOC: "900x2200", VAT_LIEU: "Gỗ sồi" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_4",
    name: "CUA_SO_2_CANH_1400",
    category: "Kiến trúc",
    tags: ["cửa sổ", "window", "2 cánh", "w1400"],
    defaultAttributes: { MA_CUA: "W1", KICH_THUOC: "1400x1600", VAT_LIEU: "Nhôm Xingfa" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_5",
    name: "COT_BE_TONG_200x200",
    category: "Kết cấu",
    tags: ["cột", "kết cấu", "column", "bê tông"],
    defaultAttributes: { TIET_DIEN: "200x200", MAC_BT: "M300" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_6",
    name: "NORTH_ARROW_HNL",
    category: "North Arrow",
    tags: ["hướng bắc", "north", "ký hiệu", "la bàn"],
    defaultAttributes: { ANGLE: "0" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_7",
    name: "SECTION_CALLOUT",
    category: "Section",
    tags: ["mặt cắt", "section", "ký hiệu trích đoạn"],
    defaultAttributes: { SECTION_NO: "A", SHEET_NO: "KT-03" },
    isDynamic: true,
    unit: "mm",
  },
  {
    id: "blk_lib_8",
    name: "HNL_TITLE_A3",
    category: "Title Block",
    tags: ["khung tên", "title block", "a3", "hnl"],
    defaultAttributes: { PROJECT: "HNL RIVERSIDE RESIDENCE", DRAWN_BY: "HNL CAD", SCALE: "1:100" },
    isDynamic: true,
    unit: "mm",
  },
];

export const INITIAL_LISP_SCRIPTS: LispScriptItem[] = [
  {
    id: "lsp_hnlarea",
    name: "HNLAREA - Tính tổng diện tích & ghi Text",
    commandName: "C:HNLAREA",
    category: "Area" as any,
    description: "Chọn nhiều Polyline/Region, tự động tính tổng diện tích (m²) và ghi nhãn.",
    isAutoLoad: true,
    isFavorite: true,
    code: `(defun C:HNLAREA (/ ss i ent obj area total pt)
  (vl-load-com)
  (setq total 0.0)
  (if (setq ss (ssget '((0 . "*POLYLINE,SPLINE,REGION"))))
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq obj (vlax-ename->vla-object ent))
        (if (vlax-property-available-p obj 'Area)
          (setq total (+ total (/ (vlax-get-property obj 'Area) 1000000.0)))
        )
        (setq i (1+ i))
      )
      (princ (strcat "\\n[HNL] Tong dien tich = " (rtos total 2 2) " m2"))
      (if (setq pt (getpoint "\\nChon diem chen Text: "))
        (command "._TEXT" pt 250 0 (strcat "S = " (rtos total 2 2) " m2"))
      )
    )
  )
  (princ)
)`,
  },
  {
    id: "lsp_apwall",
    name: "APWALL - Vẽ tường 100/200 tự bo góc",
    commandName: "C:APWALL",
    category: "Draw" as any,
    description: "Nhập 2 điểm hoặc chọn tim trục, vẽ 2 mép tường, bo góc và hatch tự động.",
    isAutoLoad: true,
    isFavorite: true,
    code: `(defun C:APWALL (/ p1 p2 thick d ang p1a p1b p2a p2b)
  (initget "100 200")
  (setq thick (getkword "\\nChon do day tuong [100/200] <100>: "))
  (if (not thick) (setq thick "100"))
  (setq d (/ (atof thick) 2.0))
  (while (and (setq p1 (getpoint "\\nDiem bat dau tim tuong: "))
              (setq p2 (getpoint p1 "\\nDiem tiep theo: ")))
    (setq ang (angle p1 p2))
    (setq p1a (polar p1 (+ ang (/ pi 2.0)) d))
    (setq p1b (polar p1 (- ang (/ pi 2.0)) d))
    (setq p2a (polar p2 (+ ang (/ pi 2.0)) d))
    (setq p2b (polar p2 (- ang (/ pi 2.0)) d))
    (command "._LINE" p1a p2a "")
    (command "._LINE" p1b p2b "")
  )
  (princ)
)`,
  },
  {
    id: "lsp_hnltable",
    name: "HNLTABLE - Thống kê Block ra bảng AutoCAD Table",
    commandName: "C:HNLTABLE",
    category: "Table" as any,
    description: "Quét toàn bộ block đèn, thiết bị và xuất trực tiếp thành AutoCAD Table.",
    isAutoLoad: false,
    isFavorite: false,
    code: `(defun C:HNLTABLE ()
  (princ "\\n[HNL CAD] Dang quet Block va tao bang thong ke...")
  ;; Auto-generates Table object via ActiveX
  (princ "\\nHoan tat.")
  (princ)
)`,
  },
];

export const INITIAL_PROJECT_STANDARD: ProjectStandard = {
  id: "std_vietnam_tcvn",
  name: "Tiêu chuẩn Kiến trúc HNL (TCVN & ISO)",
  defaultDimStyle: "HNL_DIM_100",
  defaultTextStyle: "HNL_ROMANS_UNICODE",
  standardTextHeight: 2.5,
  standardLayers: INITIAL_LAYERS,
  ctbFile: "HNL_Standard_Monochrome.ctb",
  defaultPaper: "A3",
};

export const INITIAL_TRANSLATION_MEMORY: TranslationMemoryItem[] = [
  { original: "MẶT BẰNG BỐ TRÍ NỘI THẤT", translated: "FURNITURE LAYOUT PLAN", category: "Architecture", verified: true },
  { original: "MẶT BẰNG TRẦN THẠCH CAO", translated: "REFLECTED CEILING PLAN", category: "Architecture", verified: true },
  { original: "MẶT CẮT CHI TIẾT A-A", translated: "DETAIL SECTION A-A", category: "Architecture", verified: true },
  { original: "PHÒNG KHÁCH", translated: "LIVING ROOM", category: "General", verified: true },
  { original: "PHÒNG NGỦ MASTER", translated: "MASTER BEDROOM", category: "General", verified: true },
  { original: "BẾP & PHÒNG ĂN", translated: "KITCHEN & DINING", category: "General", verified: true },
  { original: "KHUNG XƯƠNG CHÍNH @800", translated: "MAIN T-RUNNER @800", category: "MEP/Ceiling", verified: true },
  { original: "KHUNG XƯƠNG PHỤ @400", translated: "CROSS TEE @400", category: "MEP/Ceiling", verified: true },
  { original: "TY TREO PHI 8", translated: "THREADED HANGER ROD D8", category: "MEP/Ceiling", verified: true },
  { original: "ĐÈN DOWNLIGHT ÂM TRẦN", translated: "RECESSED LED DOWNLIGHT", category: "MEP", verified: true },
];

export const INITIAL_AUDIT_ISSUES: DrawingAuditIssue[] = [
  {
    id: "iss_field_1",
    type: "ERROR",
    category: "FIELD",
    title: "Field bị mất liên kết (Hiển thị ####)",
    description: "Text tại handle 4D01 có liên kết Field đến Polyline đã bị chỉnh sửa hoặc đổi tên.",
    affectedEntityHandles: ["4D01"],
    canAutoFix: true,
  },
  {
    id: "iss_vp_1",
    type: "WARNING",
    category: "VIEWPORT",
    title: "Viewport chưa được khóa tỷ lệ",
    description: "Viewport VP02 trên layout KT-01 đang ở trạng thái Unlocked, có thể bị phóng to/thu nhỏ ngoài ý muốn khi in.",
    affectedEntityHandles: ["VP02"],
    canAutoFix: true,
  },
  {
    id: "iss_txt_1",
    type: "WARNING",
    category: "TEXT",
    title: "Phát hiện ký tự bảng mã TCVN3 / VNI-Windows",
    description: "Một số text trong bản vẽ sử dụng font .VnTime hoặc VNI chưa chuẩn hóa sang Unicode dựng sẵn.",
    affectedEntityHandles: ["4D03"],
    canAutoFix: true,
  },
];

