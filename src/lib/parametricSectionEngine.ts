import {
  CadEntity,
  CadWall,
  CadCeilingGrid,
  Point2D,
  SectionCutLine,
  GeneratedSectionData,
  DetailedSectionElement,
} from "../types/cad";
import { distance2D } from "./cadEngine";

export const SAMPLE_SECTION_LINES: SectionCutLine[] = [
  {
    id: "SEC_AA",
    name: "MẶT CẮT A-A (CẮT TRẦN THẠCH CAO & VÁCH EI60)",
    p1: { x: 0, y: 3000 },
    p2: { x: 8000, y: 3000 },
    viewDirection: "UP",
    floorId: "Floor_1",
    depthMm: 1500,
  },
  {
    id: "SEC_BB",
    name: "MẶT CẮT B-B (CHI TIẾT KHE RÈM & ĐÈN HẮT)",
    p1: { x: 4000, y: 0 },
    p2: { x: 4000, y: 6000 },
    viewDirection: "RIGHT",
    floorId: "Floor_1",
    depthMm: 1200,
  },
];

/**
 * Generates a full parametric 2D cross-section drawing from a section cut line and CAD entities
 */
export function generateParametricSection({
  cutLine,
  entities,
  slabElevationMm = 3600,
  ceilingElevationMm = 2800,
  floorElevationMm = 0,
}: {
  cutLine: SectionCutLine;
  entities: CadEntity[];
  slabElevationMm?: number;
  ceilingElevationMm?: number;
  floorElevationMm?: number;
}): GeneratedSectionData {
  const cutLength = distance2D(cutLine.p1, cutLine.p2);
  const elements: DetailedSectionElement[] = [];

  const totalWidth = Math.max(cutLength, 6000);
  const slabThick = 150;
  const beamDepth = 350;
  const beamWidth = 250;

  // 1. Concrete Slab (Sàn bê tông cốt thép)
  elements.push({
    id: "slab_top",
    type: "SLAB",
    x1: 0,
    y1: slabElevationMm,
    x2: totalWidth,
    y2: slabElevationMm + slabThick,
    thicknessMm: slabThick,
    label: "Sàn BTCT D150 (Concrete Slab)",
    material: "Bê tông cốt thép mác 300",
    elevationMm: slabElevationMm,
    color: "#78909C",
  });

  // 2. Concrete Beams at ends and middle
  elements.push(
    {
      id: "beam_left",
      type: "BEAM",
      x1: 0,
      y1: slabElevationMm - beamDepth,
      x2: beamWidth,
      y2: slabElevationMm,
      thicknessMm: beamWidth,
      label: "Dầm BTCT D250x350 (Trục A)",
      material: "Bê tông dầm",
      color: "#546E7A",
    },
    {
      id: "beam_right",
      type: "BEAM",
      x1: totalWidth - beamWidth,
      y1: slabElevationMm - beamDepth,
      x2: totalWidth,
      y2: slabElevationMm,
      thicknessMm: beamWidth,
      label: "Dầm BTCT D250x350 (Trục B)",
      material: "Bê tông dầm",
      color: "#546E7A",
    }
  );

  // 3. Ground Floor Slab (Sàn hoàn thiện)
  elements.push({
    id: "floor_slab",
    type: "SLAB",
    x1: 0,
    y1: floorElevationMm - 100,
    x2: totalWidth,
    y2: floorElevationMm,
    thicknessMm: 100,
    label: "Sàn hoàn thiện lát gạch Porcelain 600x600",
    material: "Gạch ceramic + vữa lót",
    elevationMm: floorElevationMm,
    color: "#9E9E9E",
  });

  // 4. Suspended Gypsum Ceiling Board (Tấm thạch cao 9.5mm / 12.5mm)
  const boardThick = 12.5;
  const ceilingStart = 300;
  const ceilingEnd = totalWidth - 300;

  // Main flat ceiling
  elements.push({
    id: "ceiling_board",
    type: "WALL_BOARD",
    x1: ceilingStart,
    y1: ceilingElevationMm - boardThick,
    x2: ceilingEnd,
    y2: ceilingElevationMm,
    thicknessMm: boardThick,
    label: "Tấm thạch cao Gyproc Tiêu chuẩn 12.5mm",
    material: "Thạch cao Gyproc Saint-Gobain",
    elevationMm: ceilingElevationMm,
    color: "#E0E0E0",
  });

  // 5. Shadowline / Z-Perimeter Profile at Wall junctions
  elements.push(
    {
      id: "shadowline_left",
      type: "SHADOWLINE",
      x1: ceilingStart - 20,
      y1: ceilingElevationMm - 15,
      x2: ceilingStart,
      y2: ceilingElevationMm + 15,
      label: "Nẹp viền Z-Shadowline 15mm tạo khe hở bóng đổ",
      material: "Nhôm sơn tĩnh điện Z-profile",
      color: "#00E676",
    },
    {
      id: "shadowline_right",
      type: "SHADOWLINE",
      x1: ceilingEnd,
      y1: ceilingElevationMm - 15,
      x2: ceilingEnd + 20,
      y2: ceilingElevationMm + 15,
      label: "Nẹp viền Z-Shadowline 15mm",
      material: "Nhôm sơn tĩnh điện Z-profile",
      color: "#00E676",
    }
  );

  // 6. Primary Keel (Xương chính C-keel C38 @ 800mm) & Secondary M-Bar @ 400mm
  const mainKeelSpacing = 800;
  for (let x = ceilingStart + 400; x < ceilingEnd; x += mainKeelSpacing) {
    elements.push({
      id: `main_keel_${x}`,
      type: "CEILING_MAIN",
      x1: x - 19,
      y1: ceilingElevationMm + 25,
      x2: x + 19,
      y2: ceilingElevationMm + 63,
      thicknessMm: 38,
      label: "Xương chính C38 (C-Channel Keel)",
      material: "Thép mạ hợp kim nhôm kẽm Vĩnh Tường SERRA",
      color: "#FFB300",
    });

    // 7. Threaded Suspension Rods (Ty ren M6/M8 + Nở đạn đính sàn BTCT)
    elements.push({
      id: `hanger_${x}`,
      type: "HANGER_ROD",
      x1: x,
      y1: ceilingElevationMm + 63,
      x2: x,
      y2: slabElevationMm,
      thicknessMm: 8,
      label: "Ty treo thép M8 + Nở đạn (Drop-in Anchor)",
      material: "Thép mạ kẽm ren suốt M8",
      color: "#00B0FF",
    });
  }

  // Secondary M-Bars @ 400mm
  const crossKeelSpacing = 400;
  for (let x = ceilingStart + 200; x < ceilingEnd; x += crossKeelSpacing) {
    elements.push({
      id: `cross_bar_${x}`,
      type: "CEILING_CROSS",
      x1: x - 17,
      y1: ceilingElevationMm,
      x2: x + 17,
      y2: ceilingElevationMm + 25,
      thicknessMm: 34,
      label: "Xương phụ M-Bar Omega @ 400mm",
      material: "Thép định hình mạ kẽm Vĩnh Tường TRIPFLEX",
      color: "#FFD54F",
    });
  }

  // 8. Drywall Partition (Vách ngăn thạch cao chống cháy EI60) at cut location
  const wallX = totalWidth * 0.65;
  const studThick = 75; // C-Stud 75
  elements.push(
    // Left Boards (2 layers 12.5mm)
    {
      id: "wall_board_left",
      type: "WALL_BOARD",
      x1: wallX - studThick / 2 - 25,
      y1: floorElevationMm,
      x2: wallX - studThick / 2,
      y2: slabElevationMm - 20, // Deflection gap
      thicknessMm: 25,
      label: "2 lớp tấm Gyproc Chống cháy FireBloc 12.5mm",
      material: "Tấm thạch cao chống cháy Gyproc FireBloc",
      color: "#EF5350",
    },
    // C-Stud Framing
    {
      id: "wall_stud",
      type: "WALL_STUD",
      x1: wallX - studThick / 2,
      y1: floorElevationMm,
      x2: wallX + studThick / 2,
      y2: slabElevationMm - 20,
      thicknessMm: studThick,
      label: "Khung xương đứng V-Wall C75 dày 0.5mm @ 400mm",
      material: "Khung thép mạ hợp kim kẽm C75 Vĩnh Tường",
      color: "#FFA000",
    },
    // Rockwool 50mm Insulation
    {
      id: "wall_rockwool",
      type: "ROCKWOOL",
      x1: wallX - studThick / 2 + 10,
      y1: floorElevationMm + 50,
      x2: wallX + studThick / 2 - 10,
      y2: slabElevationMm - 50,
      thicknessMm: 50,
      label: "Bông khoáng cách âm chống cháy Rockwool 50mm (60kg/m³)",
      material: "Bông khoáng sợi thủy tinh Rockwool",
      color: "#8D6E63",
    },
    // Right Boards (2 layers 12.5mm)
    {
      id: "wall_board_right",
      type: "WALL_BOARD",
      x1: wallX + studThick / 2,
      y1: floorElevationMm,
      x2: wallX + studThick / 2 + 25,
      y2: slabElevationMm - 20,
      thicknessMm: 25,
      label: "2 lớp tấm Gyproc Chống cháy FireBloc 12.5mm",
      material: "Tấm thạch cao chống cháy Gyproc FireBloc",
      color: "#EF5350",
    }
  );

  // 9. Level Elevation Markers
  elements.push(
    {
      id: "lvl_floor",
      type: "LEVEL_MARK",
      x1: -120,
      y1: floorElevationMm,
      x2: totalWidth + 120,
      y2: floorElevationMm,
      label: "CAO ĐỘ HOÀN THIỆN FFL: ±0.000",
      material: "Mặt sàn",
      elevationMm: floorElevationMm,
      color: "#4FC3F7",
    },
    {
      id: "lvl_ceiling",
      type: "LEVEL_MARK",
      x1: -120,
      y1: ceilingElevationMm,
      x2: totalWidth + 120,
      y2: ceilingElevationMm,
      label: "CAO ĐỘ ĐÁY TRẦN FCL: +2.800",
      material: "Đáy trần",
      elevationMm: ceilingElevationMm,
      color: "#81C784",
    },
    {
      id: "lvl_slab",
      type: "LEVEL_MARK",
      x1: -120,
      y1: slabElevationMm,
      x2: totalWidth + 120,
      y2: slabElevationMm,
      label: "CAO ĐỘ ĐÁY SÀN BTCT SSL: +3.600",
      material: "Đáy sàn",
      elevationMm: slabElevationMm,
      color: "#FF8A65",
    }
  );

  return {
    sectionId: cutLine.id,
    title: cutLine.name,
    scale: "TỈ LỆ 1:20 (CHI TIẾT THI CÔNG TRẦN VÁCH)",
    elements,
    totalWidthMm: totalWidth,
    slabElevationMm,
    ceilingElevationMm,
    wallHeightMm: slabElevationMm - floorElevationMm,
    insulationSpecs: "Preset minh họa Rockwool 50mm / 60kg/m³ – STC/EI phải lấy từ Approved System/Test Report dự án",
    framingSpecs: "Preset minh họa khung trần/vách – khoảng cách và cấu tạo phải lấy từ Project Spec/Approved Material",
  };
}
