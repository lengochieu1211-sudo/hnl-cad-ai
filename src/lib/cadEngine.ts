import {
  CadEntity,
  CadWall,
  CadCeilingGrid,
  CadPolyline,
  CadViewport,
  Point2D,
  CadTable,
} from "../types/cad";

// Calculates Euclidean distance
export function distance2D(p1: Point2D, p2: Point2D): number {
  return Math.sqrt(Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2));
}

// Calculate Polygon Area (Shoelace Formula) in square millimeters
export function calculatePolygonArea(points: Point2D[]): number {
  if (points.length < 3) return 0;
  let area = 0;
  for (let i = 0; i < points.length; i++) {
    const j = (i + 1) % points.length;
    area += points[i].x * points[j].y;
    area -= points[j].x * points[i].y;
  }
  return Math.abs(area) / 2;
}

// Calculate Polygon Perimeter in millimeters
export function calculatePolygonPerimeter(points: Point2D[], closed: boolean = true): number {
  if (points.length < 2) return 0;
  let perimeter = 0;
  for (let i = 0; i < points.length - 1; i++) {
    perimeter += distance2D(points[i], points[i + 1]);
  }
  if (closed && points.length > 2) {
    perimeter += distance2D(points[points.length - 1], points[0]);
  }
  return perimeter;
}

// Calculate Centroid of Polygon
export function calculateCentroid(points: Point2D[]): Point2D {
  if (points.length === 0) return { x: 0, y: 0 };
  let cx = 0;
  let cy = 0;
  let factor = 0;
  for (let i = 0; i < points.length; i++) {
    const j = (i + 1) % points.length;
    const f = points[i].x * points[j].y - points[j].x * points[i].y;
    cx += (points[i].x + points[j].x) * f;
    cy += (points[i].y + points[j].y) * f;
    factor += f;
  }
  factor *= 3;
  if (Math.abs(factor) < 0.0001) {
    // Simple average fallback
    const sum = points.reduce((acc, p) => ({ x: acc.x + p.x, y: acc.y + p.y }), { x: 0, y: 0 });
    return { x: sum.x / points.length, y: sum.y / points.length };
  }
  return { x: cx / factor, y: cy / factor };
}

// Smart Wall Generator: From center line or 2 points, generates wall geometry with offset ±thickness/2
export function createSmartWall(
  p1: Point2D,
  p2: Point2D,
  thickness: number = 100,
  wallType: "BRICK_100" | "BRICK_200" | "DRYWALL" = "BRICK_100",
  layer: string = "KT_TUONG"
): CadWall {
  return {
    id: `wall_${Date.now()}_${Math.floor(Math.random() * 1000)}`,
    handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
    type: "WALL",
    layer,
    color: thickness === 200 ? "#00E5FF" : "#FF9100",
    p1,
    p2,
    thickness,
    wallType,
    hatchPattern: "ANSI31",
  };
}

// Smart Ceiling Grid Generator: Generates main tees, cross tees, hangers, wall angles inside boundary
export function generateCeilingGridLines(ceiling: CadCeilingGrid): {
  mainTees: Array<{ start: Point2D; end: Point2D }>;
  crossTees: Array<{ start: Point2D; end: Point2D }>;
  hangers: Point2D[];
  wallAngles: Point2D[];
} {
  const pts = ceiling.boundary;
  if (pts.length < 3) return { mainTees: [], crossTees: [], hangers: [], wallAngles: [] };

  const minX = Math.min(...pts.map((p) => p.x));
  const maxX = Math.max(...pts.map((p) => p.x));
  const minY = Math.min(...pts.map((p) => p.y));
  const maxY = Math.max(...pts.map((p) => p.y));

  const mainSpacing = ceiling.mainSpacing || 800;
  const subSpacing = ceiling.subSpacing || 400;
  const hangerSpacing = ceiling.hangerSpacing || 1000;

  const mainTees: Array<{ start: Point2D; end: Point2D }> = [];
  const crossTees: Array<{ start: Point2D; end: Point2D }> = [];
  const hangers: Point2D[] = [];

  // Main tees running horizontally along Y axis steps
  for (let y = minY + mainSpacing; y < maxY; y += mainSpacing) {
    mainTees.push({
      start: { x: minX, y },
      end: { x: maxX, y },
    });

    // Hangers along main tees
    for (let hx = minX + hangerSpacing / 2; hx < maxX; hx += hangerSpacing) {
      hangers.push({ x: hx, y });
    }
  }

  // Cross tees running vertically along X axis steps
  for (let x = minX + subSpacing; x < maxX; x += subSpacing) {
    crossTees.push({
      start: { x, y: minY },
      end: { x, y: maxY },
    });
  }

  return {
    mainTees,
    crossTees,
    hangers,
    wallAngles: pts,
  };
}

// Auto Fit Viewport Scale Optimizer
// Given bounding box of Model detail and paper dimensions (A3: 420x297, A4: 297x210, etc.)
export function calculateOptimalViewportScale(
  modelWidth: number,
  modelHeight: number,
  paperWidthMm: number,
  paperHeightMm: number,
  marginMm: number = 30
): {
  recommendedScale: string;
  scaleFactor: number;
  allEvaluations: Array<{ scale: string; fitStatus: "TOO_BIG" | "OPTIMAL" | "TOO_SMALL"; fitPercentage: number }>;
} {
  const availableWidth = paperWidthMm - marginMm * 2;
  const availableHeight = paperHeightMm - marginMm * 2;

  const standardScales = [
    { name: "1:10", factor: 0.1 },
    { name: "1:20", factor: 0.05 },
    { name: "1:25", factor: 0.04 },
    { name: "1:50", factor: 0.02 },
    { name: "1:75", factor: 1 / 75 },
    { name: "1:100", factor: 0.01 },
    { name: "1:150", factor: 1 / 150 },
    { name: "1:200", factor: 0.005 },
    { name: "1:500", factor: 0.002 },
  ];

  const allEvaluations = standardScales.map((s) => {
    const drawnW = modelWidth * s.factor;
    const drawnH = modelHeight * s.factor;
    const ratioW = drawnW / availableWidth;
    const ratioH = drawnH / availableHeight;
    const maxRatio = Math.max(ratioW, ratioH);

    let fitStatus: "TOO_BIG" | "OPTIMAL" | "TOO_SMALL" = "OPTIMAL";
    if (maxRatio > 1.0) {
      fitStatus = "TOO_BIG";
    } else if (maxRatio < 0.45) {
      fitStatus = "TOO_SMALL";
    }

    return {
      scale: s.name,
      factor: s.factor,
      fitStatus,
      fitPercentage: Math.round(maxRatio * 100),
    };
  });

  // Pick the best optimal (largest scale that maxRatio <= 0.85)
  const candidates = allEvaluations.filter((e) => e.fitStatus === "OPTIMAL" || e.fitPercentage <= 90);
  const best = candidates.length > 0 ? candidates[0] : allEvaluations[allEvaluations.length - 1];

  return {
    recommendedScale: best.scale,
    scaleFactor: standardScales.find((s) => s.name === best.scale)?.factor || 0.01,
    allEvaluations,
  };
}

// Block Similarity Engine
// Compares block geometry & attribute profiles and returns similarity 0.0 - 1.0
export function computeBlockSimilarity(
  blockA: { name: string; attrKeys: string[]; tagCount: number; dynamicProps?: string[] },
  blockB: { name: string; attrKeys: string[]; tagCount: number; dynamicProps?: string[] }
): number {
  if (blockA.name === blockB.name) return 1.0;

  // Name fuzzy match
  let score = 0;
  const cleanA = blockA.name.toLowerCase().replace(/[^a-z0-9]/g, "");
  const cleanB = blockB.name.toLowerCase().replace(/[^a-z0-9]/g, "");
  if (cleanA.includes(cleanB) || cleanB.includes(cleanA)) {
    score += 0.4;
  }

  // Attribute keys overlap (Jaccard similarity)
  const setA = new Set(blockA.attrKeys);
  const setB = new Set(blockB.attrKeys);
  const intersection = new Set([...setA].filter((x) => setB.has(x)));
  const union = new Set([...setA, ...setB]);
  if (union.size > 0) {
    score += (intersection.size / union.size) * 0.4;
  } else {
    score += 0.2;
  }

  // Tag count diff
  const tagDiff = Math.abs(blockA.tagCount - blockB.tagCount);
  if (tagDiff === 0) score += 0.2;
  else if (tagDiff <= 2) score += 0.1;

  return Math.min(1.0, Math.round(score * 100) / 100);
}

// Vietnamese Text Encoding & Standardization Tool
// Normalizes TCVN3 / VNI-Windows / Unicode and trims excessive whitespace
const TCVN3_MAP: Record<string, string> = {
  a\u0300: "\u00e0",
  "\u00b8": "\u00e1",
  "\u00b5": "\u00e0",
  "\u00b6": "\u1ea3",
  "\u00b7": "\u00e3",
  "\u00b9": "\u1ea1",
  "\u00a8": "\u0103",
  "\u00be": "\u1eaf",
  "\u00bb": "\u1eb1",
  "\u00bc": "\u1eb3",
  "\u00bd": "\u1eb5",
  "\u00c6": "\u1eb7",
  "\u00a9": "\u00e2",
  "\u00ca": "\u1ea5",
  "\u00c7": "\u1ea7",
  "\u00c8": "\u1ea9",
  "\u00c9": "\u1eab",
  "\u00cb": "\u1ead",
  "\u00d0": "\u00e9",
  "\u00cc": "\u00e8",
  "\u00ce": "\u1ebb",
  "\u00cf": "\u1ebd",
  "\u00d1": "\u1eb9",
  "\u00aa": "\u00ea",
  "\u00d6": "\u1ebf",
  "\u00d2": "\u1ec1",
  "\u00d3": "\u1ec3",
  "\u00d4": "\u1ec5",
  "\u00d5": "\u1ec7",
  "\u00dc": "\u00ed",
  "\u00d7": "\u00ec",
  "\u00d8": "\u1ec9",
  "\u00de": "\u0129",
  "\u00df": "\u1ecb",
  "\u00e3": "\u00f3",
  "\u00e0": "\u00f2",
  "\u00e1": "\u1ecf",
  "\u00e2": "\u00f5",
  "\u00e4": "\u1ecd",
  "\u00ab": "\u00f4",
  "\u00e8": "\u1ed1",
  "\u00e5": "\u1ed3",
  "\u00e6": "\u1ed5",
  "\u00e7": "\u1ed7",
  "\u00e9": "\u1ed9",
  "\u00ac": "\u01a1",
  "\u00ed": "\u1edb",
  "\u00ea": "\u1edd",
  "\u00eb": "\u1edf",
  "\u00ec": "\u1ee1",
  "\u00ee": "\u1ee3",
  "\u00f3": "\u00fa",
  "\u00ef": "\u00f9",
  "\u00f1": "\u1ee7",
  "\u00f2": "\u0169",
  "\u00f4": "\u1ee5",
  "\u00ad": "\u01b0",
  "\u00f8": "\u1ee9",
  "\u00f5": "\u1eeb",
  "\u00f6": "\u1eed",
  "\u00f7": "\u1eef",
  "\u00f9": "\u1ef1",
  "\u00fd": "\u00fd",
  "\u00fa": "\u1ef3",
  "\u00fb": "\u1ef7",
  "\u00fc": "\u1ef9",
  "\u00fe": "\u1ef5",
  "\u00ae": "\u0111",
};

export function normalizeVietnameseCadText(text: string, options?: { toUpperCase?: boolean; trimWhitespace?: boolean }): string {
  if (!text) return "";
  let result = text;

  // Convert known TCVN3 characters to Unicode
  for (const [tcvn, uni] of Object.entries(TCVN3_MAP)) {
    result = result.split(tcvn).join(uni);
  }

  // Remove redundant whitespace
  if (options?.trimWhitespace !== false) {
    result = result.replace(/\s+/g, " ").trim();
  }

  if (options?.toUpperCase) {
    result = result.toUpperCase();
  }

  return result;
}

export const normalizeVietnameseText = normalizeVietnameseCadText;

// Generate Downloadable Excel CSV for BOQ
export function generateBoqCsv(table: CadTable): string {
  const lines: string[] = [];
  lines.push(`"${table.title || "BẢNG THỐNG KÊ VẬT TƯ & DIỆN TÍCH HNL CAD"}"`);
  lines.push(`"Ngày xuất: ${new Date().toLocaleString("vi-VN")}"`);
  lines.push("");

  // Headers
  lines.push(table.headers.map((h) => `"${h.replace(/"/g, '""')}"`).join(","));

  // Rows
  table.rows.forEach((row) => {
    lines.push(row.map((cell) => `"${String(cell || "").replace(/"/g, '""')}"`).join(","));
  });

  // Subtotal if exists
  if (table.subtotalRow) {
    lines.push(table.subtotalRow.map((cell) => `"${String(cell || "").replace(/"/g, '""')}"`).join(","));
  }

  return lines.join("\r\n");
}
