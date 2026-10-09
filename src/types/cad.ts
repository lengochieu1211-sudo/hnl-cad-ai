export type CadEntityType =
  | "LINE"
  | "POLYLINE"
  | "RECTANGLE"
  | "CIRCLE"
  | "ARC"
  | "WALL"
  | "CEILING_GRID"
  | "DOOR"
  | "WINDOW"
  | "TEXT"
  | "MTEXT"
  | "MLEADER"
  | "DIMENSION"
  | "HATCH"
  | "BLOCK_REF"
  | "TABLE"
  | "VIEWPORT"
  | "TITLE_BLOCK"
  | "DETAIL_CALLOUT"
  | "SECTION_CALLOUT"
  | "MLEADER";

export interface Point2D {
  x: number;
  y: number;
}

export interface CadLayer {
  name: string;
  color: string;
  lineweight: number;
  linetype: string;
  isLocked: boolean;
  isVisible: boolean;
  isPlottable: boolean;
}

export interface CadEntityBase {
  id: string;
  handle: string;
  type: CadEntityType;
  layer: string;
  color?: string;
  lineweight?: number;
  isSelected?: boolean;
  isHighlighted?: boolean;
  isGhostPreview?: boolean;
}

export interface CadLine extends CadEntityBase {
  type: "LINE";
  start: Point2D;
  end: Point2D;
}

export interface CadPolyline extends CadEntityBase {
  type: "POLYLINE";
  points: Point2D[];
  closed: boolean;
  area?: number;
  length?: number;
}

export interface CadRectangle extends CadEntityBase {
  type: "RECTANGLE";
  x: number;
  y: number;
  width: number;
  height: number;
  rotation?: number;
}

export interface CadCircle extends CadEntityBase {
  type: "CIRCLE";
  center: Point2D;
  radius: number;
}

export interface CadWall extends CadEntityBase {
  type: "WALL";
  p1: Point2D;
  p2: Point2D;
  thickness: number; // 100 or 200
  hatchPattern?: string;
  wallType?: "BRICK_100" | "BRICK_200" | "DRYWALL";
}

export interface CadCeilingGrid extends CadEntityBase {
  type: "CEILING_GRID";
  boundary?: Point2D[];
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  elevation?: number;
  gridType?: string;
  gridAngle?: number;
  originX?: number;
  originY?: number;
  primaryColor?: string;
  mainSpacing?: number; // e.g. 800mm
  subSpacing?: number; // e.g. 400mm
  mainTeeSpacing?: number;
  crossTeeSpacing?: number;
  hangerSpacing?: number; // e.g. 1000mm
  wallAngleOffset?: number; // 25mm
  ceilingType?: "SUSPENDED_GYPSUM" | "DROP_CEILING" | "ALUMINUM";
  levelElevation?: number; // e.g. 2800mm
  rotationDeg?: number;
  panelSize?: { width: number; height: number };
}

export interface CadBlockRef extends CadEntityBase {
  type: "BLOCK_REF";
  blockName: string;
  category?: string;
  position: Point2D;
  scale?: { x: number; y: number; z?: number };
  rotation?: number;
  rotationDeg?: number;
  attributes?: Record<string, string>;
  dynamicProperties?: Record<string, any>;
}

export interface CadText extends CadEntityBase {
  type: "TEXT" | "MTEXT";
  text: string;
  position: Point2D;
  height: number;
  rotation?: number;
  style?: string;
  hasField?: boolean;
  fieldFormula?: string;
  translatedText?: string;
}

export interface CadDimension extends CadEntityBase {
  type: "DIMENSION";
  p1: Point2D;
  p2: Point2D;
  dimLineOffset?: number;
  measurement: number;
  overrideText?: string;
  dimStyle?: string;
}

export interface CadTable extends CadEntityBase {
  type: "TABLE";
  position: Point2D;
  title: string;
  headers: string[];
  rows: string[][];
  subtotalRow?: string[];
  columnWidths?: number[];
  rowHeight?: number;
}

export interface CadViewport extends Partial<CadEntityBase> {
  type: "VIEWPORT";
  layoutName: string;
  x: number;
  y: number;
  width: number;
  height: number;
  modelCenter: Point2D;
  scale: string; // e.g. "1:100", "1:50", "1:20"
  scaleFactor: number;
  locked: boolean;
  title?: string;
  detailNumber?: string;
}

export interface CadDetailCallout extends CadEntityBase {
  type: "DETAIL_CALLOUT";
  detailNumber: string; // "01", "02"
  sheetNumber: string; // "A-102"
  title: string;
  scale: string; // "1:10", "1:20"
  center: Point2D;
  box: { x: number; y: number; width: number; height: number };
  bubblePos: Point2D;
  leaderEnd?: Point2D;
  isLinkedToLayout?: boolean;
}

export interface CadSectionCallout extends CadEntityBase {
  type: "SECTION_CALLOUT";
  sectionName: string; // "A-A", "B-B"
  sheetNumber: string; // "A-102"
  p1: Point2D;
  p2: Point2D;
  arrowDirection: 1 | -1;
  viewTitle?: string;
  isLinkedToLayout?: boolean;
}

export interface CadMLeader extends CadEntityBase {
  type: "MLEADER";
  leaderPoints: Point2D[];
  text: string;
  textPosition: Point2D;
  landingDistance?: number;
  needsConfirmation?: boolean;
  category?: string;
}

export interface CadLayout {
  id: string;
  name: string;
  paperSize: "A0" | "A1" | "A2" | "A3" | "A4" | string;
  orientation: "Landscape" | "Portrait" | "LANDSCAPE" | "PORTRAIT";
  widthMm: number;
  heightMm: number;
  marginMm: number;
  titleBlockName?: string;
  titleBlockBlockName?: string;
  drawingName: string;
  drawingNo: string;
  scale: string;
  revision?: string;
  date?: string;
  status?: string;
}

export type CadEntity =
  | CadLine
  | CadPolyline
  | CadRectangle
  | CadCircle
  | CadWall
  | CadCeilingGrid
  | CadBlockRef
  | CadText
  | CadDimension
  | CadTable
  | CadViewport
  | CadDetailCallout
  | CadSectionCallout
  | CadMLeader;

// ==========================================
// AI AUTO DETAIL & LAYOUT COMPOSER TYPES
// ==========================================

export type DetailExtractionType = "EXTRACT_GEOMETRY" | "GENERATE_FROM_TEMPLATE";

export interface AutoDetailProposal {
  id: string;
  detailNumber: string; // e.g. "01", "02"
  title: string;
  category: "WALL_CORNER" | "DOOR_OPENING" | "CEILING_JOINT" | "ELEVATION_STEP" | "CURTAIN_BOX" | "COMPLEX_JUNCTION" | "CUSTOM";
  center: Point2D;
  bounds: { x: number; y: number; width: number; height: number };
  recommendedScale: string; // "1:10" | "1:20" | "1:25"
  complexityScore: number; // 0 to 100
  extractionType: DetailExtractionType;
  templateId?: string;
  templateName?: string;
  isSelected: boolean;
  explanation: string;
  detectedFeatures: string[];
}

export interface AutoSectionProposal {
  id: string;
  sectionName: string; // "A-A", "B-B"
  title: string;
  p1: Point2D;
  p2: Point2D;
  arrowDirection: 1 | -1;
  recommendedScale: string; // "1:50" | "1:25"
  isSelected: boolean;
  explanation: string;
}

export interface DetailTemplateItem {
  id: string;
  name: string;
  category: string;
  recommendedScale: string;
  description: string;
  layers: string[];
  thumbnailSvg?: string;
  similarityScore?: number;
  materials: string[];
}

export interface LayoutQualityMetrics {
  overallScore: number; // 0 to 100
  readabilityScore: number; // 0 to 100
  alignmentScore: number; // 0 to 100
  whiteSpaceScore: number; // 0 to 100
  viewportBalanceScore: number; // 0 to 100
  scaleConsistencyScore: number; // 0 to 100
  collisionCount: number;
  warnings: string[];
  recommendations: string[];
}

export interface ReferenceLayoutAnalysis {
  templateName: string;
  paperSize: "A0" | "A1" | "A2" | "A3" | "A4";
  orientation: "Landscape" | "Portrait";
  mainViewRatio: number; // e.g. 0.55
  sectionRatio: number; // e.g. 0.25
  detailRatio: number; // e.g. 0.20
  viewportGrid: {
    type: "MAIN_PLAN" | "SECTION" | "DETAIL" | "NOTES" | "TITLE_BLOCK";
    gridArea: { x: number; y: number; width: number; height: number };
    title: string;
  }[];
}

export interface SheetSetProposal {
  sheetNumber: string; // e.g. "A-101"
  sheetTitle: string; // e.g. "MẶT BẰNG TỔNG THỂ KIẾN TRÚC"
  paperSize: "A0" | "A1" | "A2" | "A3" | "A4";
  orientation: "Landscape" | "Portrait";
  viewports: {
    type: "MAIN_PLAN" | "SECTION" | "DETAIL";
    title: string;
    scale: string;
    detailNumber?: string;
    modelCenter: Point2D;
    paperPosition: { x: number; y: number; width: number; height: number };
  }[];
}

export interface BlockLibraryItem {
  id: string;
  name: string;
  category: string;
  tags: string[];
  svgIconPath?: string;
  defaultAttributes?: Record<string, string>;
  isDynamic?: boolean;
  unit?: "mm" | "m";
}

export interface LispScriptItem {
  id: string;
  name?: string;
  commandName: string; // e.g. "C:APAREA"
  category: "Draw" | "Text" | "Block" | "Field" | "Layout" | "Viewport" | "Dimension" | "Quantity" | "Area" | "Table" | "Other" | string;
  description: string;
  code: string;
  isAutoLoad?: boolean;
  autoload?: boolean;
  isFavorite?: boolean;
  lastModified?: string;
}

export interface TranslationMemoryItem {
  id?: string;
  original: string;
  translated: string;
  sourceLang?: string;
  targetLang?: string;
  category?: string;
  verified?: boolean;
}

export interface DrawingAuditIssue {
  id: string;
  type: "ERROR" | "WARNING" | "INFO";
  category: "LAYER" | "TEXT" | "DIMENSION" | "FIELD" | "VIEWPORT" | "GEOMETRY" | string;
  entityHandle?: string;
  affectedEntityHandles?: string[];
  title: string;
  description: string;
  location?: Point2D;
  fixAction?: string;
  canAutoFix: boolean;
  isFixed?: boolean;
}

export interface AICommandPlan {
  intent: string;
  actionType: string;
  isDestructive: boolean;
  confidence: number;
  explanation: string;
  steps: Array<{
    stepIndex: number;
    command: string;
    description: string;
    parameters?: Record<string, any>;
  }>;
  previewData?: {
    entityType: string;
    entitiesToAdd: any[];
    entitiesToModify?: any[];
    entitiesToDelete?: any[];
  };
}

export interface ProjectStandard {
  id: string;
  name: string;
  defaultDimStyle: string;
  defaultTextStyle: string;
  standardTextHeight: number;
  standardLayers: CadLayer[];
  ctbFile: string;
  defaultPaper: "A3" | "A4" | "A2" | "A1";
}

// =======================================================
// HNL WORKBENCHES / MODULE ARCHITECTURE (FreeCAD Style)
// =======================================================
export type HnlWorkbench =
  | "HNL_CAD"
  | "HNL_BLOCK"
  | "HNL_TEXT"
  | "HNL_FIELD"
  | "HNL_QUANTITY"
  | "HNL_SHOPDRAWING"
  | "HNL_CEILING"
  | "HNL_WALL"
  | "HNL_DETAIL"
  | "HNL_LAYOUT"
  | "HNL_TRANSLATE"
  | "HNL_AI"
  | "HNL_AUTOMATION"
  | "HNL_LIBRARY";

export interface HnlModuleItem {
  id: string;
  name: string;
  workbench: HnlWorkbench;
  description: string;
  version: string;
  isEnabled: boolean;
  isCore: boolean;
  author: string;
  tags: string[];
  memoryWeightMb: number;
}

// =======================================================
// SMART OBJECT MODEL (Parametric & Recomputable)
// =======================================================
export type SmartObjectType =
  | "HNL_ROOM"
  | "HNL_CEILING"
  | "HNL_WALL"
  | "HNL_OPENING"
  | "HNL_DETAIL"
  | "HNL_SECTION"
  | "HNL_SHEET"
  | "HNL_VIEW";

export interface SmartObjectProperty {
  key: string;
  label: string;
  type: "string" | "number" | "boolean" | "select" | "expression" | "color" | "points";
  value: any;
  options?: string[];
  unit?: string;
  isReadOnly?: boolean;
  group?: "General" | "Geometry" | "Framing" | "Board" | "Fire & Acoustic" | "MEP" | "Documentation";
  expression?: string;
}

export interface SmartObjectBase {
  id: string;
  name: string; // e.g. "C01", "W03-EI60", "Room 101"
  type: SmartObjectType;
  floorId: string;
  layer: string;
  isLocked?: boolean;
  dirtyFlag: boolean; // Needs recompute
  status: "VERIFIED" | "NEEDS_CONFIRMATION" | "CONFLICT";
  properties: SmartObjectProperty[];
  parentObjectId?: string;
  childObjectIds: string[];
  dependencyIds: string[]; // Objects that when changed trigger recompute on this object
  lastRecomputedAt?: string;
}

export interface HnlRoomSmartObject extends SmartObjectBase {
  type: "HNL_ROOM";
  boundaryPoints: Point2D[];
  roomNumber: string;
  roomName: string;
  netAreaM2: number;
  perimeterM: number;
  ceilingHeightMm: number;
  floorFinish: string;
  assignedCeilingId?: string;
  wallIds: string[];
}

export interface HnlCeilingSmartObject extends SmartObjectBase {
  type: "HNL_CEILING";
  ceilingType: "SUSPENDED_GYPSUM" | "EXPOSED_GRID" | "ALUMINUM_BAFFLE" | "CURTAIN_STEP";
  boundaryPoints: Point2D[];
  levelElevationMm: number;
  boardType: "STANDARD_9.5" | "MOISTURE_RESIST_9.5" | "FIRE_RESIST_12.5" | "ACOUSTIC_PERFORATED" | "MINERAL_FIBER_15";
  boardDirectionDeg: number;
  gridOption?: "CENTER_ROOM" | "ARCH_AXIS" | "PRIORITIZE_MEP" | "MIN_CUT_TILES";
  mainFrameType: "V-KEEL_38" | "T-BAR_24" | "C-CHANNEL_50";
  mainSpacingMm: number;
  secondaryFrameType: "M-BAR" | "CROSS_TEE_1200" | "CROSS_TEE_600";
  secondarySpacingMm: number;
  hangerType: "THREADED_ROD_M6" | "STEEL_WIRE_4MM" | "SPRING_CLIP_HANGER";
  hangerSpacingMm: number;
  perimeterType: "SHADOWLINE_Z" | "WALL_ANGLE_L20x20";
  areaM2: number;
  mepClashCount: number;
}

export interface HnlWallSmartObject extends SmartObjectBase {
  type: "HNL_WALL";
  p1: Point2D;
  p2: Point2D;
  wallType: "DRYWALL_SINGLE_STUD" | "DRYWALL_DOUBLE_STUD" | "SHAFT_WALL" | "CURVED_WALL" | "BRICK_200";
  totalThicknessMm: number;
  studType: "C75_0.5MM" | "C50_0.5MM" | "C100_0.6MM" | "DOUBLE_C75";
  trackType: "U75_0.5MM" | "U50_0.5MM" | "U100_0.6MM" | "SLOTTED_DEFLECTION_TRACK";
  studSpacingMm: number;
  heightMm: number;
  boardSideA: string; // e.g. "2x12.5mm Gyproc FireStop"
  boardSideB: string; // e.g. "2x12.5mm Gyproc FireStop"
  insulationType: "ROCKWOOL_50MM_50KG" | "GLASSWOOL_50MM_24KG" | "NONE";
  fireRating: "EI30" | "EI60" | "EI90" | "EI120" | "NONE";
  acousticRatingRw: number; // e.g. 52 dB
  testedAssemblyId?: string;
  hasDeflectionHead: boolean;
}

export interface HnlOpeningSmartObject extends SmartObjectBase {
  type: "HNL_OPENING";
  openingType: "DOOR" | "WINDOW" | "MEP_PENETRATION" | "ACCESS_PANEL" | "CURTAIN_BOX";
  hostWallOrCeilingId: string;
  center: Point2D;
  widthMm: number;
  heightMm: number;
  hasDoubleJambStud: boolean;
  hasLintelHeader: boolean;
  hasFirestopSeal: boolean;
}

export interface HnlDetailSmartObject extends SmartObjectBase {
  type: "HNL_DETAIL";
  detailNumber: string; // e.g. "D01"
  sheetNumber: string; // e.g. "A-102"
  title: string;
  sourceBoundary: { x: number; y: number; width: number; height: number };
  targetScale: string; // "1:10", "1:20"
  referenceDoc?: string;
  sourceLocation: Point2D;
}

export interface HnlSectionSmartObject extends SmartObjectBase {
  type: "HNL_SECTION";
  sectionNumber: string; // e.g. "S01"
  sectionName: string; // "A-A"
  p1: Point2D;
  p2: Point2D;
  arrowDirection: 1 | -1;
  targetScale: string; // "1:25", "1:50"
}

export interface HnlSheetSmartObject extends SmartObjectBase {
  type: "HNL_SHEET";
  sheetNumber: string; // e.g. "A-101"
  sheetTitle: string; // e.g. "MẶT BẰNG TRẦN & CHI TIẾT ĐIỂN HÌNH"
  paperSize: "A0" | "A1" | "A2" | "A3" | "A4";
  viewportIds: string[];
}

export type HnlSmartObject =
  | HnlRoomSmartObject
  | HnlCeilingSmartObject
  | HnlWallSmartObject
  | HnlOpeningSmartObject
  | HnlDetailSmartObject
  | HnlSectionSmartObject
  | HnlSheetSmartObject;

// =======================================================
// FREECAD-STYLE LOGICAL PROJECT TREE
// =======================================================
export interface ProjectTreeNode {
  id: string;
  label: string;
  type: "PROJECT" | "FLOOR" | "CATEGORY" | "SMART_OBJECT" | "COMPONENT" | "MEP";
  categoryKey?: "Ceiling" | "Wall" | "Details" | "Sections" | "Layouts" | "MEP" | "Board" | "Main Frame" | "Secondary Frame" | "Hanger" | "Openings";
  smartObjectId?: string;
  isExpanded: boolean;
  isSelected: boolean;
  isDirty?: boolean;
  status?: "VERIFIED" | "NEEDS_CONFIRMATION" | "CONFLICT";
  iconName?: string;
  children: ProjectTreeNode[];
}

// =======================================================
// PARAMETER SPREADSHEET & EXPRESSION ENGINE
// =======================================================
export interface SpreadsheetParameter {
  id: string;
  name: string; // e.g. "BoardWidth", "StudSpacing", "WasteFactor"
  expression: string; // e.g. "1200", "NetArea * 1.05"
  evaluatedValue: number | string;
  unit: string; // "mm", "m2", "%", "pcs"
  description: string;
  category: "General" | "Ceiling" | "Wall" | "Cost";
}

// =======================================================
// DEPENDENCY GRAPH & RECOMPUTE ENGINE
// =======================================================
export interface DependencyEdge {
  fromId: string; // e.g. "Room_101"
  toId: string; // e.g. "Ceiling_C01"
  dependencyType: "BOUNDARY" | "FRAMING" | "QUANTITY" | "DETAIL" | "LAYOUT";
}

export interface RecomputeBatchResult {
  updatedObjectIds: string[];
  recomputedCount: number;
  durationMs: number;
  warnings: string[];
}

// =======================================================
// OBJECT SNAP (OSNAP) & OBJECT TRACKING (OTRACK) ENGINE
// =======================================================
export type OsnapMode =
  | "ENDPOINT"
  | "MIDPOINT"
  | "CENTER"
  | "INTERSECTION"
  | "PERPENDICULAR"
  | "NEAREST"
  | "QUADRANT"
  | "EXTENSION"
  | "NODE";

export interface OsnapPoint {
  point: Point2D;
  mode: OsnapMode;
  entityId?: string;
  sourceDescription: string;
  distanceToMouse: number;
  screenPos: Point2D;
  guideLineStart?: Point2D;
  guideLineEnd?: Point2D;
  guideAngleDeg?: number;
}

export interface OsnapSettings {
  enabled: boolean;
  trackingEnabled: boolean; // Object Snap Tracking [F11]
  apertureSizePx: number; // e.g. 15px
  modes: Record<OsnapMode, boolean>;
  showTooltips: boolean;
  showSnapMarker: boolean;
  showAlignmentGuides: boolean;
}

// =======================================================
// DYNAMIC INPUT (DYNMODE - F12)
// =======================================================
export interface DynamicInputState {
  enabled: boolean;
  activeField: "LENGTH" | "ANGLE" | "COORDS";
  lengthInput: string;
  angleInput: string;
  coordXInput: string;
  coordYInput: string;
  lockedLength: number | null;
  lockedAngle: number | null;
}

// =======================================================
// PARAMETRIC LIVE SECTION ENGINE
// =======================================================
export interface SectionCutLine {
  id: string;
  name: string; // e.g. "MẶT CẮT A-A"
  p1: Point2D;
  p2: Point2D;
  viewDirection: "UP" | "DOWN" | "LEFT" | "RIGHT";
  floorId: string;
  depthMm: number;
}

export interface DetailedSectionElement {
  id: string;
  type: "SLAB" | "BEAM" | "WALL_STUD" | "WALL_BOARD" | "CEILING_MAIN" | "CEILING_CROSS" | "HANGER_ROD" | "SHADOWLINE" | "ROCKWOOL" | "MEP_ITEM" | "DIMENSION" | "LEVEL_MARK";
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  thicknessMm?: number;
  label: string;
  material: string;
  elevationMm?: number;
  color?: string;
}

export interface GeneratedSectionData {
  sectionId: string;
  title: string;
  scale: string;
  elements: DetailedSectionElement[];
  totalWidthMm: number;
  slabElevationMm: number;
  ceilingElevationMm: number;
  wallHeightMm: number;
  insulationSpecs: string;
  framingSpecs: string;
}

// =======================================================
// MEP CLASH DETECTION & REINFORCEMENT
// =======================================================
export type MepDeviceType = "DIFFUSER_LINEAR" | "DIFFUSER_SQUARE" | "DOWNLIGHT" | "TROFFER_600" | "SPRINKLER" | "HVAC_DUCT" | "CABLE_TRAY";

export interface MepElement {
  id: string;
  type: MepDeviceType;
  name: string;
  position: Point2D; // Center point
  widthMm: number;
  lengthMm: number;
  elevationMm: number;
  service: "HVAC" | "LIGHTING" | "FIRE_FIGHTING" | "ELECTRICAL";
}

export interface MepClashIssue {
  id: string;
  mepId: string;
  mepName: string;
  smartObjectId: string;
  smartObjectName: string;
  clashType: "FRAME_INTERFERENCE" | "HANGER_CONFLICT" | "INSUFFICIENT_CLEARANCE";
  severity: "HIGH" | "MEDIUM" | "LOW";
  location: Point2D;
  description: string;
  requiredReinforcement: string;
  isResolved: boolean;
  autoFixAction: "ADD_TRIMMER_KEEL" | "SHIFT_HANGER_ROD" | "ADD_SUSPENSION_BRIDGE";
}

// =======================================================
// BUILDING CODE & SPECIFICATION KNOWLEDGE
// =======================================================
export interface BuildingCodeStandard {
  codeId: string;
  title: string;
  authority: string; // e.g. "Bộ Xây Dựng (BXD)" | "ASTM International"
  summary: string;
  category: "FIRE_SAFETY" | "ACOUSTIC" | "STRUCTURAL_DEFLECTION" | "INSTALLATION_GUIDE";
  keyRules: {
    ruleName: string;
    requirement: string;
    allowedValues: string;
    referenceSection: string;
  }[];
}

