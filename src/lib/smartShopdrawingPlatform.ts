import { HNL_BOARD_MODULE_RULES, MANUFACTURER_CEILING_KNOWLEDGE } from "./manufacturerCeilingKnowledge";

export const HNL_BOARD_MODULE = {
  boardWidthMm: 1220,
  boardLengthMm: 2440,
  ceilingCross: {
    division: 3,
    spacingMm: 1220 / 3,
    formula: "1220 / 3",
  },
  wallStud: {
    allowedDivisions: [3, 2] as const,
    spacingsMm: [1220 / 3, 1220 / 2],
    formulas: ["1220 / 3", "1220 / 2"],
  },
};

export type LibraryCategory =
  | "ANNOTATION"
  | "CEILING"
  | "WALL"
  | "STEEL"
  | "MEP_REFERENCE"
  | "CUSTOM";

export interface HnlLibraryItem {
  id: string;
  name: string;
  category: LibraryCategory;
  symbolKey: string;
  layer: string;
  description: string;
  annotative?: boolean;
  dynamic?: boolean;
  sourceDwg?: string;
  tags: string[];
}

export const HNL_BUILTIN_LIBRARY: HnlLibraryItem[] = [
  { id:"lib_section", name:"Ký hiệu mặt cắt", category:"ANNOTATION", symbolKey:"SECTION_MARK", layer:"HNL-ANNO-SECTION", description:"Mặt cắt A-A/B-B, mũi tên hai đầu.", annotative:true, tags:["section","mặt cắt","ký hiệu"] },
  { id:"lib_level", name:"Ký hiệu cao độ", category:"ANNOTATION", symbolKey:"LEVEL_MARK", layer:"HNL-ANNO-LEVEL", description:"Cao độ ±0.000 / +3.600.", annotative:true, tags:["level","cao độ","elevation"] },
  { id:"lib_detail", name:"Ký hiệu chi tiết", category:"ANNOTATION", symbolKey:"DETAIL_MARK", layer:"HNL-ANNO-DETAIL", description:"Detail callout / số chi tiết.", annotative:true, tags:["detail","chi tiết"] },
  { id:"lib_board_start", name:"Điểm xuất phát tấm trần", category:"CEILING", symbolKey:"BOARD_START", layer:"HNL-CLG-BOARD", description:"Điểm gốc và hướng chạy tấm 1220×2440.", dynamic:true, tags:["tấm","trần","start","1220","2440"] },
  { id:"lib_clg_main", name:"Xương chính trần", category:"CEILING", symbolKey:"CEILING_MAIN", layer:"HNL-CLG-MAIN", description:"Ký hiệu/đại diện xương chính.", tags:["main","xương chính","trần"] },
  { id:"lib_clg_cross", name:"Xương phụ trần", category:"CEILING", symbolKey:"CEILING_CROSS", layer:"HNL-CLG-CROSS", description:"Xương phụ theo module tấm; mặc định HNL 1220/3.", tags:["cross","xương phụ","406.67"] },
  { id:"lib_clg_hanger", name:"Ty treo trần", category:"CEILING", symbolKey:"CEILING_HANGER", layer:"HNL-CLG-HANGER", description:"Ty ren/ty dây + điểm treo.", tags:["ty","hanger","trần"] },
  { id:"lib_wall_stud", name:"Stud đứng vách", category:"WALL", symbolKey:"WALL_STUD", layer:"HNL-WALL-STUD", description:"Stud đứng; module HNL 1220/3 hoặc 1220/2.", tags:["stud","vách","610","406.67"] },
  { id:"lib_wall_track", name:"Track ngang vách", category:"WALL", symbolKey:"WALL_TRACK", layer:"HNL-WALL-TRACK", description:"Track trên/dưới.", tags:["track","vách","u track"] },
  { id:"lib_door_jamb", name:"Gia cường jamb cửa", category:"WALL", symbolKey:"DOOR_JAMB", layer:"HNL-WALL-REINF", description:"Stud kép/khung gia cường cửa.", tags:["door","jamb","cửa","gia cường"] },
  { id:"lib_rhs", name:"Sắt hộp RHS/SHS", category:"STEEL", symbolKey:"STEEL_RHS", layer:"HNL-STEEL-RHS", description:"Ký hiệu sắt hộp 20x40/30x60/40x80...", dynamic:true, tags:["rhs","shs","sắt hộp","steel"] },
  { id:"lib_plate", name:"Bản mã", category:"STEEL", symbolKey:"STEEL_PLATE", layer:"HNL-STEEL-PLATE", description:"Bản mã/plate liên kết.", tags:["plate","bản mã"] },
];

export type ApprovalStatus = "DRAFT" | "SUBMITTED" | "APPROVED" | "REJECTED" | "SUPERSEDED";

export interface ApprovedMaterialRecord {
  id: string;
  projectId: string;
  category: "CEILING" | "WALL" | "BOARD" | "FRAMING" | "ACCESSORY" | "OTHER";
  manufacturer: string;
  systemName: string;
  documentName: string;
  revision: string;
  status: ApprovalStatus;
  approvedDate?: string;
  notes?: string;
  sourcePath?: string;
  sourceUrl?: string;
  parameters?: Record<string, string | number | boolean>;
}

export const APPROVAL_PRECEDENCE = [
  "PROJECT_APPROVED_SUBMITTAL",
  "PROJECT_SPEC",
  "MANUFACTURER_CURRENT_CATALOG",
  "MANUFACTURER_TECHNICAL_DOC",
  "HNL_PROJECT_RULE",
  "AI_SUGGESTION",
] as const;

export function resolveCeilingPreset(systemId: string) {
  const system = MANUFACTURER_CEILING_KNOWLEDGE.find((x) => x.id === systemId);
  const spacing = system?.spacing;
  return {
    systemId,
    manufacturer: system?.manufacturer || "HNL",
    systemName: system?.systemName || "HNL Project Module",
    boardWidthMm: 1220,
    boardLengthMm: 2440,
    crossSpacingMm: HNL_BOARD_MODULE.ceilingCross.spacingMm,
    manufacturerCrossPublishedMm: spacing?.crossMm ?? spacing?.crossMaxMm ?? null,
    mainSpacingMm: spacing?.mainMm ?? spacing?.mainMaxMm ?? 800,
    hangerSpacingMm: spacing?.hangerMm ?? spacing?.hangerMaxMm ?? 900,
    sourceStatus: spacing?.status || "PROJECT_RULE",
    note:
      "HNL project module keeps cross-runner at 1220/3=406.67mm unless an Approved Material/Submittal explicitly overrides the project rule.",
  };
}

export interface SmartCeilingConfig {
  id: string;
  name: string;
  manufacturerSystemId: string;
  boardWidthMm: number;
  boardLengthMm: number;
  boardDirectionDeg: number;
  crossDivision: 3;
  crossSpacingMm: number;
  mainSpacingMm: number;
  hangerSpacingMm: number;
  elevationMm: number;
  startMode: "PICK_POINT" | "LEFT_BOTTOM" | "CENTER";
  avoidMep: boolean;
}

export function createDefaultCeilingConfig(): SmartCeilingConfig {
  return {
    id: `ceiling_${Date.now()}`,
    name: "HNL Smart Ceiling",
    manufacturerSystemId: "HNL_PROJECT_RULE",
    boardWidthMm: 1220,
    boardLengthMm: 2440,
    boardDirectionDeg: 0,
    crossDivision: 3,
    crossSpacingMm: 1220 / 3,
    mainSpacingMm: 800,
    hangerSpacingMm: 900,
    elevationMm: 2800,
    startMode: "PICK_POINT",
    avoidMep: true,
  };
}

export interface SmartWallConfig {
  id: string;
  name: string;
  boardWidthMm: number;
  boardLengthMm: number;
  studDivision: 3 | 2;
  studSpacingMm: number;
  heightMm: number;
  studProfile: string;
  trackProfile: string;
  layersSideA: number;
  layersSideB: number;
  includeStartStud: boolean;
  includeEndStud: boolean;
  reinforceOpenings: boolean;
}

export function createDefaultWallConfig(): SmartWallConfig {
  return {
    id: `wall_${Date.now()}`,
    name: "HNL Smart Wall",
    boardWidthMm: 1220,
    boardLengthMm: 2440,
    studDivision: 3,
    studSpacingMm: 1220 / 3,
    heightMm: 3000,
    studProfile: "C75",
    trackProfile: "U76",
    layersSideA: 1,
    layersSideB: 1,
    includeStartStud: true,
    includeEndStud: true,
    reinforceOpenings: true,
  };
}

export function applyWallDivision(cfg: SmartWallConfig, division: 3 | 2): SmartWallConfig {
  return { ...cfg, studDivision: division, studSpacingMm: cfg.boardWidthMm / division };
}

export interface SmartBoqLine {
  code: string;
  description: string;
  unit: "m²" | "m" | "pcs";
  quantity: number;
  source: string;
}

export function calculateCeilingBoq(areaM2: number, cfg: SmartCeilingConfig): SmartBoqLine[] {
  const mainLm = areaM2 / (cfg.mainSpacingMm / 1000);
  const crossLm = areaM2 / (cfg.crossSpacingMm / 1000);
  const hangerPcs = Math.ceil(areaM2 / ((cfg.mainSpacingMm / 1000) * (cfg.hangerSpacingMm / 1000)));
  return [
    { code:"CLG-BOARD", description:`Tấm ${cfg.boardWidthMm}x${cfg.boardLengthMm}`, unit:"m²", quantity:+(areaM2*1.05).toFixed(2), source:cfg.id },
    { code:"CLG-MAIN", description:"Xương chính", unit:"m", quantity:+(mainLm*1.05).toFixed(2), source:cfg.id },
    { code:"CLG-CROSS", description:`Xương phụ @${cfg.crossSpacingMm.toFixed(2)}`, unit:"m", quantity:+(crossLm*1.08).toFixed(2), source:cfg.id },
    { code:"CLG-HANGER", description:`Ty treo @${cfg.hangerSpacingMm}`, unit:"pcs", quantity:Math.ceil(hangerPcs*1.08), source:cfg.id },
  ];
}

export function calculateWallBoq(lengthMm: number, cfg: SmartWallConfig): SmartBoqLine[] {
  const lengthM = lengthMm / 1000;
  const heightM = cfg.heightMm / 1000;
  const studCount = Math.ceil(lengthMm / cfg.studSpacingMm) + 1;
  const boardArea = lengthM * heightM * (cfg.layersSideA + cfg.layersSideB);
  return [
    { code:"WALL-BOARD", description:`Tấm vách ${cfg.boardWidthMm}x${cfg.boardLengthMm}`, unit:"m²", quantity:+(boardArea*1.05).toFixed(2), source:cfg.id },
    { code:"WALL-STUD", description:`Stud ${cfg.studProfile} @${cfg.studSpacingMm.toFixed(2)}`, unit:"m", quantity:+(studCount*heightM*1.03).toFixed(2), source:cfg.id },
    { code:"WALL-TRACK", description:`Track ${cfg.trackProfile} trên + dưới`, unit:"m", quantity:+(lengthM*2*1.03).toFixed(2), source:cfg.id },
  ];
}

export interface ShopAuditIssue {
  id: string;
  severity: "ERROR" | "WARNING" | "INFO";
  category: string;
  title: string;
  detail: string;
  source?: string;
}

export function auditCeilingConfig(cfg: SmartCeilingConfig): ShopAuditIssue[] {
  const issues: ShopAuditIssue[] = [];
  const exact = cfg.boardWidthMm / 3;
  if (Math.abs(cfg.crossSpacingMm - exact) > 0.05) {
    issues.push({
      id:"CLG_MODULE",
      severity:"ERROR",
      category:"Ceiling",
      title:"Xương phụ lệch module tấm",
      detail:`HNL project rule yêu cầu ${cfg.boardWidthMm}/3 = ${exact.toFixed(2)}mm; hiện tại ${cfg.crossSpacingMm.toFixed(2)}mm.`,
      source:"HNL_PROJECT_RULE",
    });
  }
  if (cfg.hangerSpacingMm <= 0 || cfg.mainSpacingMm <= 0) {
    issues.push({ id:"CLG_SPACING_ZERO", severity:"ERROR", category:"Ceiling", title:"Khoảng cách không hợp lệ", detail:"Main/Hanger spacing phải > 0." });
  }
  return issues;
}

export function auditWallConfig(cfg: SmartWallConfig): ShopAuditIssue[] {
  const allowed = [cfg.boardWidthMm / 3, cfg.boardWidthMm / 2];
  const valid = allowed.some((x) => Math.abs(x - cfg.studSpacingMm) <= 0.05);
  return valid ? [] : [{
    id:"WALL_MODULE",
    severity:"ERROR",
    category:"Wall",
    title:"Stud không theo module HNL",
    detail:`Chỉ dùng ${cfg.boardWidthMm}/3 = ${(cfg.boardWidthMm/3).toFixed(2)}mm hoặc ${cfg.boardWidthMm}/2 = ${(cfg.boardWidthMm/2).toFixed(2)}mm, trừ khi Approved Submittal yêu cầu khác.`,
    source:"HNL_PROJECT_RULE",
  }];
}

export interface HnlProjectTemplate {
  id: string;
  name: string;
  description: string;
  defaultCeilingMainMm: number;
  defaultCeilingHangerMm: number;
  defaultWallDivision: 3 | 2;
  layerPrefix: string;
  approvedMaterialRequired: boolean;
}

export const HNL_PROJECT_TEMPLATES: HnlProjectTemplate[] = [
  { id:"AIRPORT", name:"Sân bay", description:"Shopdrawing quy mô lớn, kiểm soát submittal/MEP chặt.", defaultCeilingMainMm:800, defaultCeilingHangerMm:900, defaultWallDivision:3, layerPrefix:"HNL-LTIA", approvedMaterialRequired:true },
  { id:"HOTEL", name:"Khách sạn", description:"Nhiều phòng lặp, ưu tiên module và detail tiêu chuẩn.", defaultCeilingMainMm:800, defaultCeilingHangerMm:900, defaultWallDivision:2, layerPrefix:"HNL-HTL", approvedMaterialRequired:true },
  { id:"CONDO", name:"Chung cư", description:"Tối ưu lặp căn/phòng và BOQ.", defaultCeilingMainMm:800, defaultCeilingHangerMm:900, defaultWallDivision:2, layerPrefix:"HNL-APT", approvedMaterialRequired:false },
  { id:"FACTORY", name:"Nhà máy", description:"Ưu tiên diện tích lớn, cao độ, MEP và khung phụ.", defaultCeilingMainMm:1000, defaultCeilingHangerMm:1000, defaultWallDivision:3, layerPrefix:"HNL-FAC", approvedMaterialRequired:true },
];

export const HNL_DETAIL_TEMPLATES = [
  { id:"SEC_CEILING_WALL", name:"Mặt cắt trần – vách", scale:"1:10", layers:["board","main","cross","hanger","stud","track"] },
  { id:"DET_CEILING_EDGE", name:"Chi tiết viền trần", scale:"1:5", layers:["board","perimeter","wall"] },
  { id:"DET_WALL_HEAD", name:"Chi tiết đầu vách", scale:"1:5", layers:["stud","track","sealant","slab"] },
  { id:"DET_DOOR_JAMB", name:"Chi tiết jamb cửa", scale:"1:5", layers:["doubleStud","track","board"] },
];

export function getSmartShopdrawingAiContext(approvedMaterials: ApprovedMaterialRecord[] = []) {
  return {
    boardModule: HNL_BOARD_MODULE,
    approvalPrecedence: APPROVAL_PRECEDENCE,
    approvedMaterials: approvedMaterials.filter((x) => x.status === "APPROVED"),
    manufacturerKnowledge: HNL_BOARD_MODULE_RULES,
    rule:
      "Approved project material/submittal has highest priority. Do not mix manufacturer systems. HNL wall module is 1220/3 or 1220/2; HNL concealed ceiling cross module is 1220/3.",
  };
}
