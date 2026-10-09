import {
  CadEntity,
  CadCeilingGrid,
  CadWall,
  Point2D,
  MepElement,
  MepClashIssue,
} from "../types/cad";
import { distance2D } from "./cadEngine";

export const SAMPLE_MEP_ELEMENTS: MepElement[] = [
  {
    id: "MEP_DIFFUSER_01",
    type: "DIFFUSER_LINEAR",
    name: "Miệng gió cấp tuyến tính Linear Diffuser (1200x150)",
    position: { x: 2400, y: 2000 },
    widthMm: 150,
    lengthMm: 1200,
    elevationMm: 2800,
    service: "HVAC",
  },
  {
    id: "MEP_TROFFER_01",
    type: "TROFFER_600",
    name: "Đèn LED Panel âm trần (600x600)",
    position: { x: 1200, y: 2400 },
    widthMm: 600,
    lengthMm: 600,
    elevationMm: 2800,
    service: "LIGHTING",
  },
  {
    id: "MEP_DOWNLIGHT_01",
    type: "DOWNLIGHT",
    name: "Đèn LED Downlight âm trần D125 (12W)",
    position: { x: 3600, y: 1600 },
    widthMm: 125,
    lengthMm: 125,
    elevationMm: 2800,
    service: "LIGHTING",
  },
  {
    id: "MEP_SPRINKLER_01",
    type: "SPRINKLER",
    name: "Đầu phun chữa cháy Sprinkler tự động Pendent K80",
    position: { x: 2000, y: 3200 },
    widthMm: 80,
    lengthMm: 80,
    elevationMm: 2800,
    service: "FIRE_FIGHTING",
  },
  {
    id: "MEP_DUCT_01",
    type: "HVAC_DUCT",
    name: "Ống gió mềm cấp lạnh D200 (Flexible Duct)",
    position: { x: 2400, y: 2800 },
    widthMm: 250,
    lengthMm: 1500,
    elevationMm: 3100,
    service: "HVAC",
  },
];

/**
 * Runs intelligent clash detection between MEP equipment and Gypsum Ceiling/Wall Framing
 */
export function runMepClashDetection({
  mepElements,
  entities,
}: {
  mepElements: MepElement[];
  entities: CadEntity[];
}): MepClashIssue[] {
  const issues: MepClashIssue[] = [];

  mepElements.forEach((mep) => {
    // Check clash with ceiling grids
    const ceilings = entities.filter((e) => e.type === "CEILING_GRID") as CadCeilingGrid[];
    ceilings.forEach((clg) => {
      // Check if MEP is inside ceiling boundary
      const x = clg.x ?? (clg.boundary && clg.boundary.length > 0 ? Math.min(...clg.boundary.map((p) => p.x)) : 0);
      const y = clg.y ?? (clg.boundary && clg.boundary.length > 0 ? Math.min(...clg.boundary.map((p) => p.y)) : 0);
      const width = clg.width ?? (clg.boundary && clg.boundary.length > 0 ? Math.max(...clg.boundary.map((p) => p.x)) - x : 4000);
      const height = clg.height ?? (clg.boundary && clg.boundary.length > 0 ? Math.max(...clg.boundary.map((p) => p.y)) - y : 3000);

      const clgMinX = Math.min(x, x + width);
      const clgMaxX = Math.max(x, x + width);
      const clgMinY = Math.min(y, y + height);
      const clgMaxY = Math.max(y, y + height);

      const inBounds =
        mep.position.x >= clgMinX &&
        mep.position.x <= clgMaxX &&
        mep.position.y >= clgMinY &&
        mep.position.y <= clgMaxY;

      if (inBounds) {
        // Linear Diffusers cut through main/cross keels
        if (mep.type === "DIFFUSER_LINEAR" || mep.type === "TROFFER_600") {
          const cutSize = Math.max(mep.widthMm, mep.lengthMm);
          if (cutSize > 300) {
            issues.push({
              id: `CLASH_${mep.id}_${clg.id}`,
              mepId: mep.id,
              mepName: mep.name,
              smartObjectId: clg.id,
              smartObjectName: `Hệ trần ${clg.id} (Cao độ +${clg.elevation || clg.levelElevation || 2800})`,
              clashType: "FRAME_INTERFERENCE",
              severity: "HIGH",
              location: mep.position,
              description: `Miệng thiết bị (${mep.widthMm}x${mep.lengthMm}mm) cắt đứt thanh xương trần chính/phụ.`,
              requiredReinforcement:
                "Gia cường khung viền bo (Double Trimmer Keel) C38 & bổ sung 04 ty treo M8 cách mép lỗ mở 150mm theo ASTM C636.",
              isResolved: false,
              autoFixAction: "ADD_TRIMMER_KEEL",
            });
          }
        }

        // Downlight clearance check
        if (mep.type === "DOWNLIGHT") {
          issues.push({
            id: `CLASH_${mep.id}_${clg.id}`,
            mepId: mep.id,
            mepName: mep.name,
            smartObjectId: clg.id,
            smartObjectName: `Hệ trần ${clg.id}`,
            clashType: "FRAME_INTERFERENCE",
            severity: "LOW",
            location: mep.position,
            description: "Khoét lỗ D125 sát xương phụ M-Bar.",
            requiredReinforcement: "Dịch chuyển nhẹ tâm đèn 35mm hoặc cắt kẹp gia cường thanh U.",
            isResolved: false,
            autoFixAction: "SHIFT_HANGER_ROD",
          });
        }
      }
    });

    // Check clash with wall framing
    const walls = entities.filter((e) => e.type === "WALL") as CadWall[];
    walls.forEach((wall) => {
      const dist = distance2D(mep.position, wall.p1);
      if (dist < 300 && mep.type === "HVAC_DUCT") {
        issues.push({
          id: `CLASH_WALL_${mep.id}_${wall.id}`,
          mepId: mep.id,
          mepName: mep.name,
          smartObjectId: wall.id,
          smartObjectName: `Vách ngăn ${wall.id}`,
          clashType: "INSUFFICIENT_CLEARANCE",
          severity: "MEDIUM",
          location: mep.position,
          description: "Ống gió xuyên qua vách thạch cao chống cháy EI.",
          requiredReinforcement: "Bọc cổ ống ngăn cháy lan (Fire Collar) & vữa chống cháy Promat đạt EI60.",
          isResolved: false,
          autoFixAction: "ADD_SUSPENSION_BRIDGE",
        });
      }
    });
  });

  return issues;
}
