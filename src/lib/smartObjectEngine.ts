import {
  HnlSmartObject,
  HnlRoomSmartObject,
  HnlCeilingSmartObject,
  HnlWallSmartObject,
  HnlOpeningSmartObject,
  HnlDetailSmartObject,
  HnlSectionSmartObject,
  HnlSheetSmartObject,
  ProjectTreeNode,
  Point2D,
  CadEntity,
} from "../types/cad";

// Default Initial Smart Objects for the Project
export const INITIAL_SMART_OBJECTS: HnlSmartObject[] = [
  // 1. Room 101 - Phòng Khách & Bếp (Living & Dining)
  {
    id: "obj_room_101",
    name: "Phòng 101 - Living & Kitchen",
    type: "HNL_ROOM",
    floorId: "floor_01",
    layer: "HNL_ARCH_ROOM",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    childObjectIds: ["obj_ceiling_c01", "obj_wall_w01", "obj_wall_w02", "obj_wall_w03_ei60"],
    dependencyIds: [],
    boundaryPoints: [
      { x: 500, y: 500 },
      { x: 6500, y: 500 },
      { x: 6500, y: 4500 },
      { x: 500, y: 4500 },
    ],
    roomNumber: "101",
    roomName: "Living & Kitchen Suite",
    netAreaM2: 24.0,
    perimeterM: 20.0,
    ceilingHeightMm: 2800,
    floorFinish: "Gỗ sồi Engineer Oak 15mm",
    assignedCeilingId: "obj_ceiling_c01",
    wallIds: ["obj_wall_w01", "obj_wall_w02", "obj_wall_w03_ei60"],
    properties: [
      { key: "roomNumber", label: "Mã số phòng", type: "string", value: "101", group: "General" },
      { key: "roomName", label: "Tên phòng", type: "string", value: "Living & Kitchen", group: "General" },
      { key: "netAreaM2", label: "Diện tích thông thủy (m²)", type: "number", value: 24.0, unit: "m²", isReadOnly: true, group: "Geometry" },
      { key: "perimeterM", label: "Chu vi phòng (m)", type: "number", value: 20.0, unit: "m", isReadOnly: true, group: "Geometry" },
      { key: "ceilingHeightMm", label: "Cao độ trần hoàn thiện (mm)", type: "number", value: 2800, unit: "mm", group: "General" },
    ],
  } as HnlRoomSmartObject,

  // 2. Ceiling C01 - Trần Thạch Cao Chìm Giật Cấp
  {
    id: "obj_ceiling_c01",
    name: "C01 - Trần Chìm Giật Cấp Đèn Hắt",
    type: "HNL_CEILING",
    floorId: "floor_01",
    layer: "HNL_CEILING_SUSPENDED",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_room_101",
    childObjectIds: ["obj_opening_curtain", "obj_opening_diffuser"],
    dependencyIds: ["obj_room_101"],
    boundaryPoints: [
      { x: 700, y: 700 },
      { x: 6300, y: 700 },
      { x: 6300, y: 4300 },
      { x: 700, y: 4300 },
    ],
    ceilingType: "SUSPENDED_GYPSUM",
    levelElevationMm: 2800,
    boardType: "MOISTURE_RESIST_9.5",
    boardDirectionDeg: 0,
    gridOption: "CENTER_ROOM",
    mainFrameType: "V-KEEL_38",
    mainSpacingMm: 800,
    secondaryFrameType: "M-BAR",
    secondarySpacingMm: 1220 / 3,
    hangerType: "THREADED_ROD_M6",
    hangerSpacingMm: 1000,
    perimeterType: "SHADOWLINE_Z",
    areaM2: 20.16,
    mepClashCount: 0,
    properties: [
      { key: "ceilingType", label: "Loại trần", type: "select", value: "SUSPENDED_GYPSUM", options: ["SUSPENDED_GYPSUM", "EXPOSED_GRID", "ALUMINUM_BAFFLE", "CURTAIN_STEP"], group: "General" },
      { key: "levelElevationMm", label: "Cao độ trần (mm)", type: "number", value: 2800, unit: "mm", group: "General" },
      { key: "boardType", label: "Loại tấm thạch cao", type: "select", value: "MOISTURE_RESIST_9.5", options: ["STANDARD_9.5", "MOISTURE_RESIST_9.5", "FIRE_RESIST_12.5", "ACOUSTIC_PERFORATED", "MINERAL_FIBER_15"], group: "Board" },
      { key: "mainSpacingMm", label: "Khoảng cách xương chính (mm)", type: "number", value: 800, unit: "mm", group: "Framing" },
      { key: "secondarySpacingMm", label: "Khoảng cách xương phụ (mm)", type: "number", value: 1220 / 3, unit: "mm", group: "Framing" },
      { key: "hangerSpacingMm", label: "Khoảng cách ty treo (mm)", type: "number", value: 1000, unit: "mm", group: "Framing" },
      { key: "perimeterType", label: "Thanh viền tường", type: "select", value: "SHADOWLINE_Z", options: ["SHADOWLINE_Z", "WALL_ANGLE_L20x20"], group: "Framing" },
      { key: "areaM2", label: "Diện tích trần (m²)", type: "number", value: 20.16, unit: "m²", isReadOnly: true, group: "Geometry" },
    ],
  } as HnlCeilingSmartObject,

  // 3. Wall W01 - Vách Ngăn Thường (Drywall Single Stud)
  {
    id: "obj_wall_w01",
    name: "W01 - Vách Thạch Cao Ngăn Phòng (DW1)",
    type: "HNL_WALL",
    floorId: "floor_01",
    layer: "HNL_WALL_DRYWALL",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_room_101",
    childObjectIds: [],
    dependencyIds: ["obj_room_101"],
    p1: { x: 500, y: 500 },
    p2: { x: 6500, y: 500 },
    wallType: "DRYWALL_SINGLE_STUD",
    totalThicknessMm: 100,
    studType: "C75_0.5MM",
    trackType: "U75_0.5MM",
    studSpacingMm: 1220 / 2,
    heightMm: 3200,
    boardSideA: "1x12.5mm Gyproc Tiêu chuẩn",
    boardSideB: "1x12.5mm Gyproc Tiêu chuẩn",
    insulationType: "GLASSWOOL_50MM_24KG",
    fireRating: "NONE",
    acousticRatingRw: 42,
    hasDeflectionHead: false,
    properties: [
      { key: "wallType", label: "Cấu tạo vách", type: "select", value: "DRYWALL_SINGLE_STUD", options: ["DRYWALL_SINGLE_STUD", "DRYWALL_DOUBLE_STUD", "SHAFT_WALL", "CURVED_WALL", "BRICK_200"], group: "General" },
      { key: "totalThicknessMm", label: "Tổng chiều dày vách (mm)", type: "number", value: 100, unit: "mm", group: "General" },
      { key: "heightMm", label: "Chiều cao tường (mm)", type: "number", value: 3200, unit: "mm", group: "General" },
      { key: "studSpacingMm", label: "Khoảng cách Stud (mm)", type: "number", value: 1220 / 2, unit: "mm", group: "Framing" },
      { key: "insulationType", label: "Bông cách âm/nhiệt", type: "select", value: "GLASSWOOL_50MM_24KG", options: ["ROCKWOOL_50MM_50KG", "GLASSWOOL_50MM_24KG", "NONE"], group: "Fire & Acoustic" },
      { key: "acousticRatingRw", label: "Chỉ số cách âm (Rw dB)", type: "number", value: 42, unit: "dB", isReadOnly: true, group: "Fire & Acoustic" },
    ],
  } as HnlWallSmartObject,

  // 4. Wall W03-EI60 - Vách Ngăn Chống Cháy Đã Được Kiểm Định (Verified Assembly)
  {
    id: "obj_wall_w03_ei60",
    name: "W03-EI60 - Vách PCCC Ngăn Khoang Cháy",
    type: "HNL_WALL",
    floorId: "floor_01",
    layer: "HNL_WALL_FIRE_EI60",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_room_101",
    childObjectIds: ["obj_opening_door_fire"],
    dependencyIds: ["obj_room_101"],
    p1: { x: 500, y: 4500 },
    p2: { x: 6500, y: 4500 },
    wallType: "DRYWALL_SINGLE_STUD",
    totalThicknessMm: 125,
    studType: "C75_0.5MM",
    trackType: "SLOTTED_DEFLECTION_TRACK",
    studSpacingMm: 1220 / 3,
    heightMm: 3600,
    boardSideA: "2x12.5mm Gyproc FireStop Chống Cháy",
    boardSideB: "2x12.5mm Gyproc FireStop Chống Cháy",
    insulationType: "ROCKWOOL_50MM_50KG",
    fireRating: "EI60",
    acousticRatingRw: 54,
    testedAssemblyId: "W-EI60-01",
    hasDeflectionHead: true,
    properties: [
      { key: "wallType", label: "Cấu tạo vách", type: "select", value: "DRYWALL_SINGLE_STUD", options: ["DRYWALL_SINGLE_STUD", "DRYWALL_DOUBLE_STUD", "SHAFT_WALL"], group: "General" },
      { key: "fireRating", label: "Cấp chống cháy kiểm định", type: "select", value: "EI60", options: ["EI30", "EI60", "EI90", "EI120", "NONE"], group: "Fire & Acoustic" },
      { key: "testedAssemblyId", label: "Hệ đã chứng nhận (Tested Assembly)", type: "string", value: "W-EI60-01 (CẦN GẮN TEST REPORT DỰ ÁN)", isReadOnly: true, group: "Fire & Acoustic" },
      { key: "totalThicknessMm", label: "Tổng chiều dày vách (mm)", type: "number", value: 125, unit: "mm", group: "General" },
      { key: "heightMm", label: "Chiều cao tường (mm)", type: "number", value: 3600, unit: "mm", group: "General" },
      { key: "studSpacingMm", label: "Khoảng cách Stud (mm)", type: "number", value: 1220 / 3, unit: "mm", group: "Framing" },
      { key: "insulationType", label: "Vật liệu cách âm/chống cháy", type: "select", value: "ROCKWOOL_50MM_50KG", options: ["ROCKWOOL_50MM_50KG", "GLASSWOOL_50MM_24KG", "NONE"], group: "Fire & Acoustic" },
      { key: "hasDeflectionHead", label: "Gối trượt đàn hồi dầm (Deflection Head)", type: "boolean", value: true, group: "Framing" },
    ],
  } as HnlWallSmartObject,

  // 5. Opening - Cửa Chống Cháy PCCC
  {
    id: "obj_opening_door_fire",
    name: "D01-EI60 - Cửa Chống Cháy 1 Cánh",
    type: "HNL_OPENING",
    floorId: "floor_01",
    layer: "HNL_ARCH_DOOR",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_wall_w03_ei60",
    childObjectIds: [],
    dependencyIds: ["obj_wall_w03_ei60"],
    openingType: "DOOR",
    hostWallOrCeilingId: "obj_wall_w03_ei60",
    center: { x: 2500, y: 4500 },
    widthMm: 900,
    heightMm: 2200,
    hasDoubleJambStud: true,
    hasLintelHeader: true,
    hasFirestopSeal: true,
    properties: [
      { key: "openingType", label: "Loại lỗ mở/Cửa", type: "select", value: "DOOR", options: ["DOOR", "WINDOW", "MEP_PENETRATION", "ACCESS_PANEL", "CURTAIN_BOX"], group: "General" },
      { key: "widthMm", label: "Chiều rộng lọt lòng (mm)", type: "number", value: 900, unit: "mm", group: "Geometry" },
      { key: "heightMm", label: "Chiều cao lọt lòng (mm)", type: "number", value: 2200, unit: "mm", group: "Geometry" },
      { key: "hasDoubleJambStud", label: "Gia cường Stud kép 2 bên (Double Stud)", type: "boolean", value: true, group: "Framing" },
      { key: "hasLintelHeader", label: "Đà lanh-tô chịu lực (Header Track)", type: "boolean", value: true, group: "Framing" },
      { key: "hasFirestopSeal", label: "Bơm keo chống cháy khe hở (Firestop Sealant)", type: "boolean", value: true, group: "Fire & Acoustic" },
    ],
  } as HnlOpeningSmartObject,

  // 6. Detail D01 - Chi Tiết Khe Hắt Trần
  {
    id: "obj_detail_d01",
    name: "D01 - Chi tiết Khe Hắt LED Trần C01",
    type: "HNL_DETAIL",
    floorId: "floor_01",
    layer: "HNL_ANNO_DETAIL",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_ceiling_c01",
    childObjectIds: [],
    dependencyIds: ["obj_ceiling_c01"],
    detailNumber: "01",
    sheetNumber: "A-102",
    title: "CHI TIẾT KHE HẮT ĐÈN LED TRẦN THẠCH CAO",
    sourceBoundary: { x: 700, y: 700, width: 400, height: 400 },
    targetScale: "1:10",
    referenceDoc: "HNL STANDARD DETAIL C03",
    sourceLocation: { x: 700, y: 700 },
    properties: [
      { key: "detailNumber", label: "Số hiệu chi tiết", type: "string", value: "01", group: "Documentation" },
      { key: "sheetNumber", label: "Tờ bản vẽ chứa", type: "string", value: "A-102", group: "Documentation" },
      { key: "title", label: "Tên chi tiết", type: "string", value: "CHI TIẾT KHE HẮT ĐÈN LED TRẦN THẠCH CAO", group: "Documentation" },
      { key: "targetScale", label: "Tỷ lệ trích chi tiết", type: "select", value: "1:10", options: ["1:5", "1:10", "1:20", "1:25"], group: "Documentation" },
      { key: "referenceDoc", label: "Tài liệu kỹ thuật quy chiếu", type: "string", value: "HNL STANDARD DETAIL C03", isReadOnly: true, group: "Documentation" },
    ],
  } as HnlDetailSmartObject,

  // 7. Section S01 - Mặt Cắt Qua Vách PCCC & Trần
  {
    id: "obj_section_s01",
    name: "S01 - Mặt Cắt Cấu Tạo A-A",
    type: "HNL_SECTION",
    floorId: "floor_01",
    layer: "HNL_ANNO_SECTION",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    parentObjectId: "obj_room_101",
    childObjectIds: [],
    dependencyIds: ["obj_ceiling_c01", "obj_wall_w03_ei60"],
    sectionNumber: "S01",
    sectionName: "A-A",
    p1: { x: 1000, y: 300 },
    p2: { x: 1000, y: 4800 },
    arrowDirection: 1,
    targetScale: "1:25",
    properties: [
      { key: "sectionName", label: "Tên mặt cắt", type: "string", value: "A-A", group: "Documentation" },
      { key: "targetScale", label: "Tỷ lệ mặt cắt", type: "select", value: "1:25", options: ["1:20", "1:25", "1:50"], group: "Documentation" },
    ],
  } as HnlSectionSmartObject,

  // 8. Sheet Layout - Sheet 01
  {
    id: "obj_sheet_01",
    name: "Sheet 01 - Mặt Bằng & Chi Tiết Shop",
    type: "HNL_SHEET",
    floorId: "floor_01",
    layer: "HNL_TITLE_BLOCK",
    status: "NEEDS_CONFIRMATION",
    dirtyFlag: false,
    childObjectIds: [],
    dependencyIds: ["obj_ceiling_c01", "obj_detail_d01", "obj_section_s01"],
    sheetNumber: "HNL-KT-01",
    sheetTitle: "MẶT BẰNG BỐ TRÍ TRẦN & CHI TIẾT CẤU TẠO",
    paperSize: "A3",
    viewportIds: ["vp_main", "vp_detail_01", "vp_section_01"],
    properties: [
      { key: "sheetNumber", label: "Số hiệu bản vẽ", type: "string", value: "HNL-KT-01", group: "Documentation" },
      { key: "sheetTitle", label: "Tên bản vẽ", type: "string", value: "MẶT BẰNG BỐ TRÍ TRẦN & CHI TIẾT CẤU TẠO", group: "Documentation" },
      { key: "paperSize", label: "Khổ giấy xuất", type: "select", value: "A3", options: ["A0", "A1", "A2", "A3", "A4"], group: "Documentation" },
    ],
  } as HnlSheetSmartObject,
];

// Build FreeCAD-Style Logical Tree View Structure
export function buildLogicalProjectTree(smartObjects: HnlSmartObject[]): ProjectTreeNode {
  const ceilings = smartObjects.filter((o) => o.type === "HNL_CEILING") as HnlCeilingSmartObject[];
  const walls = smartObjects.filter((o) => o.type === "HNL_WALL") as HnlWallSmartObject[];
  const details = smartObjects.filter((o) => o.type === "HNL_DETAIL") as HnlDetailSmartObject[];
  const sections = smartObjects.filter((o) => o.type === "HNL_SECTION") as HnlSectionSmartObject[];
  const sheets = smartObjects.filter((o) => o.type === "HNL_SHEET") as HnlSheetSmartObject[];

  const ceilingChildren: ProjectTreeNode[] = ceilings.map((c) => ({
    id: `node_${c.id}`,
    label: c.name,
    type: "SMART_OBJECT",
    categoryKey: "Ceiling",
    smartObjectId: c.id,
    isExpanded: true,
    isSelected: false,
    isDirty: c.dirtyFlag,
    status: c.status,
    children: [
      {
        id: `node_${c.id}_board`,
        label: `Board: ${c.boardType} (${c.areaM2} m²)`,
        type: "COMPONENT",
        categoryKey: "Board",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${c.id}_mainframe`,
        label: `Main Frame: ${c.mainFrameType} @${c.mainSpacingMm}mm`,
        type: "COMPONENT",
        categoryKey: "Main Frame",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${c.id}_secframe`,
        label: `Secondary Frame: ${c.secondaryFrameType} @${c.secondarySpacingMm}mm`,
        type: "COMPONENT",
        categoryKey: "Secondary Frame",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${c.id}_hanger`,
        label: `Hanger: ${c.hangerType} @${c.hangerSpacingMm}mm`,
        type: "COMPONENT",
        categoryKey: "Hanger",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${c.id}_openings`,
        label: `Openings & MEP Coordination (${c.mepClashCount} clashes)`,
        type: "COMPONENT",
        categoryKey: "Openings",
        isExpanded: false,
        isSelected: false,
        status: c.mepClashCount === 0 ? "VERIFIED" : "CONFLICT",
        children: [],
      },
    ],
  }));

  const wallChildren: ProjectTreeNode[] = walls.map((w) => ({
    id: `node_${w.id}`,
    label: `${w.name} (${w.fireRating !== "NONE" ? `🔥 ${w.fireRating}` : "Standard"} - ${w.totalThicknessMm}mm)`,
    type: "SMART_OBJECT",
    categoryKey: "Wall",
    smartObjectId: w.id,
    isExpanded: false,
    isSelected: false,
    isDirty: w.dirtyFlag,
    status: w.status,
    children: [
      {
        id: `node_${w.id}_stud`,
        label: `Stud Framing: ${w.studType} @${w.studSpacingMm}mm`,
        type: "COMPONENT",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${w.id}_boards`,
        label: `Layer Side A: ${w.boardSideA} | Side B: ${w.boardSideB}`,
        type: "COMPONENT",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
      {
        id: `node_${w.id}_insul`,
        label: `Insulation: ${w.insulationType} (${w.acousticRatingRw} dB)`,
        type: "COMPONENT",
        isExpanded: false,
        isSelected: false,
        children: [],
      },
    ],
  }));

  const detailChildren: ProjectTreeNode[] = details.map((d) => ({
    id: `node_${d.id}`,
    label: `${d.detailNumber} - ${d.title} (${d.targetScale})`,
    type: "SMART_OBJECT",
    categoryKey: "Details",
    smartObjectId: d.id,
    isExpanded: false,
    isSelected: false,
    isDirty: d.dirtyFlag,
    status: d.status,
    children: [],
  }));

  const sectionChildren: ProjectTreeNode[] = sections.map((s) => ({
    id: `node_${s.id}`,
    label: `Section ${s.sectionName} (${s.targetScale})`,
    type: "SMART_OBJECT",
    categoryKey: "Sections",
    smartObjectId: s.id,
    isExpanded: false,
    isSelected: false,
    isDirty: s.dirtyFlag,
    status: s.status,
    children: [],
  }));

  const sheetChildren: ProjectTreeNode[] = sheets.map((sh) => ({
    id: `node_${sh.id}`,
    label: `${sh.sheetNumber} - ${sh.sheetTitle} [${sh.paperSize}]`,
    type: "SMART_OBJECT",
    categoryKey: "Layouts",
    smartObjectId: sh.id,
    isExpanded: false,
    isSelected: false,
    isDirty: sh.dirtyFlag,
    status: sh.status,
    children: [],
  }));

  return {
    id: "root_project",
    label: "Project: HNL Villa Riverside",
    type: "PROJECT",
    isExpanded: true,
    isSelected: false,
    children: [
      {
        id: "floor_01_node",
        label: "Floor 01 (Tầng 1)",
        type: "FLOOR",
        isExpanded: true,
        isSelected: false,
        children: [
          {
            id: "cat_ceiling",
            label: `Ceiling (${ceilings.length} Systems)`,
            type: "CATEGORY",
            categoryKey: "Ceiling",
            isExpanded: true,
            isSelected: false,
            children: ceilingChildren,
          },
          {
            id: "cat_wall",
            label: `Wall (${walls.length} Partitions)`,
            type: "CATEGORY",
            categoryKey: "Wall",
            isExpanded: true,
            isSelected: false,
            children: wallChildren,
          },
          {
            id: "cat_details",
            label: `Details (${details.length} Callouts)`,
            type: "CATEGORY",
            categoryKey: "Details",
            isExpanded: true,
            isSelected: false,
            children: detailChildren,
          },
          {
            id: "cat_sections",
            label: `Sections (${sections.length} Cuts)`,
            type: "CATEGORY",
            categoryKey: "Sections",
            isExpanded: true,
            isSelected: false,
            children: sectionChildren,
          },
          {
            id: "cat_layouts",
            label: `Layouts / Sheets (${sheets.length} Sheets)`,
            type: "CATEGORY",
            categoryKey: "Layouts",
            isExpanded: true,
            isSelected: false,
            children: sheetChildren,
          },
        ],
      },
    ],
  };
}

// Calculate Quantities from Smart Objects
export function calculateSmartObjectBOQ(smartObjects: HnlSmartObject[]) {
  let totalBoardM2 = 0;
  let totalMainFrameLm = 0;
  let totalSecFrameLm = 0;
  let totalHangerPcs = 0;
  let totalWallStudLm = 0;
  let totalWallTrackLm = 0;
  let totalRockwoolM2 = 0;

  smartObjects.forEach((obj) => {
    if (obj.type === "HNL_CEILING") {
      const c = obj as HnlCeilingSmartObject;
      totalBoardM2 += c.areaM2;
      totalMainFrameLm += (c.areaM2 / (c.mainSpacingMm / 1000)) * 1.05;
      totalSecFrameLm += (c.areaM2 / (c.secondarySpacingMm / 1000)) * 1.08;
      totalHangerPcs += Math.ceil((c.areaM2 / ((c.mainSpacingMm / 1000) * (c.hangerSpacingMm / 1000))) * 1.1);
    } else if (obj.type === "HNL_WALL") {
      const w = obj as HnlWallSmartObject;
      const lengthM = Math.sqrt(Math.pow(w.p2.x - w.p1.x, 2) + Math.pow(w.p2.y - w.p1.y, 2)) / 1000;
      const wallAreaM2 = lengthM * (w.heightMm / 1000);
      const studCount = Math.ceil(lengthM / (w.studSpacingMm / 1000)) + 1;
      totalWallStudLm += studCount * (w.heightMm / 1000);
      totalWallTrackLm += lengthM * 2; // top and bottom track
      totalBoardM2 += wallAreaM2 * 2; // 2 sides
      if (w.insulationType.includes("ROCKWOOL")) {
        totalRockwoolM2 += wallAreaM2;
      }
    }
  });

  return {
    totalBoardM2: Math.round(totalBoardM2 * 100) / 100,
    totalMainFrameLm: Math.round(totalMainFrameLm * 10) / 10,
    totalSecFrameLm: Math.round(totalSecFrameLm * 10) / 10,
    totalHangerPcs,
    totalWallStudLm: Math.round(totalWallStudLm * 10) / 10,
    totalWallTrackLm: Math.round(totalWallTrackLm * 10) / 10,
    totalRockwoolM2: Math.round(totalRockwoolM2 * 100) / 100,
  };
}
