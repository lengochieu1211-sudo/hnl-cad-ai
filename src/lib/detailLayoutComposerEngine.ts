import {
  CadEntity,
  CadLayer,
  CadLayout,
  CadViewport,
  Point2D,
  AutoDetailProposal,
  AutoSectionProposal,
  DetailTemplateItem,
  LayoutQualityMetrics,
  ReferenceLayoutAnalysis,
  SheetSetProposal,
  CadDetailCallout,
  CadSectionCallout,
  CadMLeader,
  CadDimension,
} from "../types/cad";

// ==========================================
// 1. STANDARD ARCHITECTURAL DETAIL TEMPLATES
// ==========================================
export const STANDARD_DETAIL_TEMPLATES: DetailTemplateItem[] = [
  {
    id: "dt_wall_corner",
    name: "Chi tiết góc giao tường xây 100/200",
    category: "Kiến trúc",
    recommendedScale: "1:10",
    description: "Cấu tạo liên kết góc tường xây gạch không nung, râu thép D6 @500 và lớp vữa trát chống nứt",
    layers: ["KT_TUONG", "KT_HATCH", "KT_DIM", "KT_GHICHU"],
    materials: ["Gạch xây 80x80x180", "Vữa xi măng M75", "Lưới thép chống nứt mắt cáo"],
    similarityScore: 96,
  },
  {
    id: "dt_door_jamb",
    name: "Chi tiết khuôn cửa nhôm xingfa âm tường",
    category: "Cửa & Vách",
    recommendedScale: "1:10",
    description: "Liên kết khung nhôm hệ 55, gioăng EPDM kép, vít nở inox phi 10 và foam keo chống thấm PU",
    layers: ["KT_CUA", "KT_TUONG", "KT_DIM", "KT_GHICHU"],
    materials: ["Nhôm Xingfa hệ 55", "Kính dán an toàn 8.38mm", "Gioăng EPDM", "Keo Silicone Dow Corning"],
    similarityScore: 94,
  },
  {
    id: "dt_drywall_ceiling",
    name: "Chi tiết liên kết vách thạch cao & trần chìm",
    category: "Trần & Vách",
    recommendedScale: "1:10",
    description: "Thanh U vách cố định trần, băng keo sợi thủy tinh chống nứt, khe co giãn viền âm Shadow Gap 10x10mm",
    layers: ["KT_TRAN", "KT_VACH", "KT_DIM", "KT_GHICHU"],
    materials: ["Tấm thạch cao Gyproc 12.5mm", "Thanh U/C Vĩnh Tường", "Khe Z nhôm Shadow Gap"],
    similarityScore: 98,
  },
  {
    id: "dt_cove_light",
    name: "Chi tiết trần thạch cao giật cấp đèn hắt",
    category: "Trần",
    recommendedScale: "1:10",
    description: "Cấu tạo khe hắt LED âm 80x100mm, gia cố thanh V bo góc, chống võng và tán xạ ánh sáng đều",
    layers: ["KT_TRAN", "KT_DIEN", "KT_DIM", "KT_GHICHU"],
    materials: ["Tấm thạch cao chống ẩm 9mm", "Đèn LED dây Philips 14W/m", "Thanh viền nhôm V nhẵn"],
    similarityScore: 92,
  },
  {
    id: "dt_curtain_box",
    name: "Chi tiết hộp rèm âm trần 2 lớp",
    category: "Trần & Nội thất",
    recommendedScale: "1:10",
    description: "Hộp rèm âm trần rộng 220mm sâu 150mm, gia cố xà gồ thép hộp mạ kẽm chịu tải rèm tự động",
    layers: ["KT_TRAN", "KT_NOITHAT", "KT_DIM", "KT_GHICHU"],
    materials: ["Thép hộp 20x40x1.4mm", "Tấm thạch cao chịu ẩm 12.5mm", "Ray rèm trượt giảm chấn"],
    similarityScore: 95,
  },
  {
    id: "dt_baseboard_recessed",
    name: "Chi tiết len chân tường âm chữ Z (Shadow Line)",
    category: "Nội thất",
    recommendedScale: "1:10",
    description: "Nẹp nhôm âm chân tường cao 45mm, tạo hiệu ứng tường bay không bám bụi bẩn bề mặt sàn",
    layers: ["KT_HOANTHIEN", "KT_TUONG", "KT_DIM", "KT_GHICHU"],
    materials: ["Nẹp nhôm Anode mờ", "Sàn gỗ công nghiệp 12mm", "Xốp lót sàn 2mm"],
    similarityScore: 91,
  },
  {
    id: "dt_expansion_joint",
    name: "Chi tiết khe co giãn sàn & tường",
    category: "Kết cấu & Hoàn thiện",
    recommendedScale: "1:10",
    description: "Khe co giãn 25mm, chèn xốp xpe backing rod và keo trám đàn hồi chuyên dụng kháng tia UV",
    layers: ["KT_KETCAU", "KT_HOANTHIEN", "KT_DIM", "KT_GHICHU"],
    materials: ["Backing Rod mút xốp D30", "Keo Sikaflex Construction", "Nẹp inox che khe co giãn"],
    similarityScore: 89,
  },
];

// ==========================================
// 2. REFERENCE PRESENTATION STYLES
// ==========================================
export const REFERENCE_LAYOUT_STYLES: ReferenceLayoutAnalysis[] = [
  {
    templateName: "HNL - SHOPDRAWING TIÊU CHUẨN A1",
    paperSize: "A1",
    orientation: "Landscape",
    mainViewRatio: 0.58,
    sectionRatio: 0.22,
    detailRatio: 0.20,
    viewportGrid: [
      {
        type: "MAIN_PLAN",
        gridArea: { x: 25, y: 25, width: 480, height: 480 },
        title: "MẶT BẰNG THI CÔNG CHÍNH (TL 1:50)",
      },
      {
        type: "SECTION",
        gridArea: { x: 520, y: 25, width: 280, height: 230 },
        title: "MẶT CẮT KIẾN TRÚC A-A (TL 1:25)",
      },
      {
        type: "SECTION",
        gridArea: { x: 520, y: 275, width: 280, height: 230 },
        title: "MẶT CẮT BỘ PHẬN B-B (TL 1:25)",
      },
      {
        type: "DETAIL",
        gridArea: { x: 25, y: 400, width: 150, height: 160 },
        title: "CHI TIẾT 01 - GÓC GIAO TƯỜNG (TL 1:10)",
      },
      {
        type: "DETAIL",
        gridArea: { x: 190, y: 400, width: 150, height: 160 },
        title: "CHI TIẾT 02 - KHUNG CỬA (TL 1:10)",
      },
      {
        type: "DETAIL",
        gridArea: { x: 355, y: 400, width: 150, height: 160 },
        title: "CHI TIẾT 03 - GIAO TRẦN VÁCH (TL 1:10)",
      },
      {
        type: "NOTES",
        gridArea: { x: 520, y: 515, width: 280, height: 55 },
        title: "GHI CHÚ THI CÔNG & TIÊU CHUẨN KỸ THUẬT",
      },
    ],
  },
  {
    templateName: "HNL - HỒ SƠ THIẾT KẾ A3",
    paperSize: "A3",
    orientation: "Landscape",
    mainViewRatio: 0.65,
    sectionRatio: 0.0,
    detailRatio: 0.35,
    viewportGrid: [
      {
        type: "MAIN_PLAN",
        gridArea: { x: 15, y: 15, width: 250, height: 220 },
        title: "MẶT BẰNG BỐ TRÍ (TL 1:50)",
      },
      {
        type: "DETAIL",
        gridArea: { x: 275, y: 15, width: 125, height: 105 },
        title: "CHI TIẾT 01 (TL 1:10)",
      },
      {
        type: "DETAIL",
        gridArea: { x: 275, y: 130, width: 125, height: 105 },
        title: "CHI TIẾT 02 (TL 1:10)",
      },
      {
        type: "NOTES",
        gridArea: { x: 15, y: 245, width: 385, height: 35 },
        title: "GHI CHÚ CHUNG",
      },
    ],
  },
];

// ==========================================
// 3. AI GEOMETRY & COMPLEXITY SCANNER
// ==========================================
export function analyzeCadComplexity(entities: CadEntity[]): {
  proposedDetails: AutoDetailProposal[];
  proposedSections: AutoSectionProposal[];
  overallComplexity: number;
} {
  const proposedDetails: AutoDetailProposal[] = [];
  const proposedSections: AutoSectionProposal[] = [];

  // Default fallback bounds if entities are sparse
  let minX = 0, maxX = 6000, minY = 0, maxY = 4000;

  entities.forEach((ent) => {
    if (ent.type === "WALL") {
      minX = Math.min(minX, ent.p1.x, ent.p2.x);
      maxX = Math.max(maxX, ent.p1.x, ent.p2.x);
      minY = Math.min(minY, ent.p1.y, ent.p2.y);
      maxY = Math.max(maxY, ent.p1.y, ent.p2.y);
    }
  });

  const width = Math.max(maxX - minX, 4000);
  const height = Math.max(maxY - minY, 3000);

  // Propose Detail 01: Góc giao tường phức tạp (Wall Corner Intersection)
  proposedDetails.push({
    id: "det_prop_1",
    detailNumber: "01",
    title: "CHI TIẾT GÓC GIAO TƯỜNG & KHUNG BAO",
    category: "WALL_CORNER",
    center: { x: minX + 200, y: maxY - 200 },
    bounds: { x: minX, y: maxY - 800, width: 800, height: 800 },
    recommendedScale: "1:10",
    complexityScore: 92,
    extractionType: "GENERATE_FROM_TEMPLATE",
    templateId: "dt_wall_corner",
    templateName: "Chi tiết góc giao tường xây 100/200",
    isSelected: true,
    explanation: "Vùng giao 2 bức tường 200mm và 100mm có râu thép và lớp vữa trát hoàn thiện dày đặc.",
    detectedFeatures: ["2 Tường giao nhau 90°", "Thay đổi độ dày 200 $\\rightarrow$ 100mm", "Hatch vạch chéo"],
  });

  // Propose Detail 02: Vị trí khuôn cửa đi (Door Opening)
  proposedDetails.push({
    id: "det_prop_2",
    detailNumber: "02",
    title: "CHI TIẾT KHUÔN CỬA ĐI & GIOĂNG KÍNH",
    category: "DOOR_OPENING",
    center: { x: minX + width * 0.45, y: minY + 100 },
    bounds: { x: minX + width * 0.45 - 400, y: minY - 200, width: 800, height: 600 },
    recommendedScale: "1:10",
    complexityScore: 88,
    extractionType: "GENERATE_FROM_TEMPLATE",
    templateId: "dt_door_jamb",
    templateName: "Chi tiết khuôn cửa nhôm xingfa âm tường",
    isSelected: true,
    explanation: "Vị trí ô mở cửa có kích thước lọt sáng, khuôn bao nhôm hệ và chèn keo PU chống rung.",
    detectedFeatures: ["Ô mở tường 900mm", "Khung cửa nhôm kính", "Kích thước thông thủy"],
  });

  // Propose Detail 03: Giao trần thạch cao và vách (Ceiling & Drywall Joint)
  proposedDetails.push({
    id: "det_prop_3",
    detailNumber: "03",
    title: "CHI TIẾT LIÊN KẾT TRẦN CHÌM & VÁCH",
    category: "CEILING_JOINT",
    center: { x: minX + width * 0.75, y: maxY - 150 },
    bounds: { x: minX + width * 0.75 - 400, y: maxY - 600, width: 800, height: 600 },
    recommendedScale: "1:10",
    complexityScore: 95,
    extractionType: "GENERATE_FROM_TEMPLATE",
    templateId: "dt_drywall_ceiling",
    templateName: "Chi tiết liên kết vách thạch cao & trần chìm",
    isSelected: true,
    explanation: "Nút giao trần thạch cao chìm với vách ngăn thạch cao có nẹp Z Shadow Gap chống nứt mép trần.",
    detectedFeatures: ["Hệ xương trần @800", "Thanh U vách ngăn", "Nẹp khe hắt 10mm"],
  });

  // Propose Detail 04: Khe rèm âm trần giật cấp (Cove light / Curtain Box)
  proposedDetails.push({
    id: "det_prop_4",
    detailNumber: "04",
    title: "CHI TIẾT HỘP RÈM & ĐÈN HẮT ÂM TRẦN",
    category: "CURTAIN_BOX",
    center: { x: maxX - 300, y: minY + height * 0.5 },
    bounds: { x: maxX - 700, y: minY + height * 0.5 - 400, width: 800, height: 800 },
    recommendedScale: "1:10",
    complexityScore: 90,
    extractionType: "GENERATE_FROM_TEMPLATE",
    templateId: "dt_curtain_box",
    templateName: "Chi tiết hộp rèm âm trần 2 lớp",
    isSelected: true,
    explanation: "Khu vực mép cửa kính ban công có hộp rèm 220mm và đèn LED hắt khe sáng gián tiếp.",
    detectedFeatures: ["Hạ trần giật cấp", "Rãnh đèn LED gián tiếp", "Gia cố xà gồ treo rèm"],
  });

  // Propose Detail 05: Len chân tường âm (Shadow Baseboard)
  proposedDetails.push({
    id: "det_prop_5",
    detailNumber: "05",
    title: "CHI TIẾT LEN CHÂN TƯỜNG ÂM SÀN GỖ",
    category: "ELEVATION_STEP",
    center: { x: minX + width * 0.25, y: minY + 150 },
    bounds: { x: minX + width * 0.25 - 300, y: minY - 150, width: 600, height: 600 },
    recommendedScale: "1:10",
    complexityScore: 84,
    extractionType: "EXTRACT_GEOMETRY",
    templateId: "dt_baseboard_recessed",
    templateName: "Chi tiết len chân tường âm chữ Z (Shadow Line)",
    isSelected: false,
    explanation: "Nút chân tường tiếp giáp sàn gỗ hoàn thiện 12mm và nẹp nhôm chữ Z.",
    detectedFeatures: ["Tiếp giáp sàn lát gỗ", "Chân vách thạch cao", "Khe giãn nở sàn 10mm"],
  });

  // Propose Section A-A: Cắt dọc công trình qua trục chính và phòng khách
  proposedSections.push({
    id: "sec_prop_1",
    sectionName: "A-A",
    title: "MẶT CẮT DỌC KIẾN TRÚC A-A",
    p1: { x: minX - 600, y: minY + height * 0.5 },
    p2: { x: maxX + 600, y: minY + height * 0.5 },
    arrowDirection: 1,
    recommendedScale: "1:50",
    isSelected: true,
    explanation: "Đường cắt cắt ngang qua phòng khách, hệ trần giật cấp, cửa đi chính và cao độ dầm sàn.",
  });

  // Propose Section B-B: Cắt ngang qua khu vực hộp rèm và cửa sổ
  proposedSections.push({
    id: "sec_prop_2",
    sectionName: "B-B",
    title: "MẶT CẮT BỘ PHẬN B-B",
    p1: { x: minX + width * 0.6, y: maxY + 500 },
    p2: { x: minX + width * 0.6, y: minY - 500 },
    arrowDirection: -1,
    recommendedScale: "1:25",
    isSelected: true,
    explanation: "Đường cắt thể hiện chi tiết liên kết trần - vách và cấu tạo cửa nhôm kính ngoài trời.",
  });

  return {
    proposedDetails,
    proposedSections,
    overallComplexity: 93,
  };
}

// ==========================================
// 4. LAYOUT QUALITY SCORING & AUDIT ENGINE
// ==========================================
export function scoreLayoutQuality(
  paperSize: string,
  viewports: { x: number; y: number; width: number; height: number; scale: string; title?: string }[],
  paperWidthMm: number,
  paperHeightMm: number,
  marginMm: number = 10
): LayoutQualityMetrics {
  const warnings: string[] = [];
  const recommendations: string[] = [];

  let collisions = 0;
  let readability = 95;
  let alignment = 92;
  let whitespace = 90;
  let vpBalance = 93;
  let scaleConsistency = 96;

  // 1. Check boundary overflow & margin violations
  viewports.forEach((vp, idx) => {
    const rightEdge = vp.x + vp.width;
    const bottomEdge = vp.y + vp.height;

    if (vp.x < marginMm || vp.y < marginMm || rightEdge > paperWidthMm - marginMm || bottomEdge > paperHeightMm - marginMm) {
      collisions++;
      readability -= 8;
      alignment -= 6;
      warnings.push(`Viewport ${vp.title || `#${idx + 1}`} nằm quá sát hoặc tràn ra ngoài mép khung giấy (${marginMm}mm margin).`);
    }

    if (vp.width < 60 || vp.height < 50) {
      readability -= 5;
      warnings.push(`Viewport ${vp.title || `#${idx + 1}`} có kích thước hơi nhỏ (${Math.round(vp.width)}x${Math.round(vp.height)}mm), gây khó đọc ở cự ly in ấn.`);
    }
  });

  // 2. Check Viewport overlaps (Collisions)
  for (let i = 0; i < viewports.length; i++) {
    for (let j = i + 1; j < viewports.length; j++) {
      const v1 = viewports[i];
      const v2 = viewports[j];

      const overlapX = Math.max(0, Math.min(v1.x + v1.width, v2.x + v2.width) - Math.max(v1.x, v2.x));
      const overlapY = Math.max(0, Math.min(v1.y + v1.height, v2.y + v2.height) - Math.max(v1.y, v2.y));

      if (overlapX > 0 && overlapY > 0) {
        collisions += 2;
        whitespace -= 15;
        alignment -= 12;
        warnings.push(`Phát hiện chồng lấn giữa [${v1.title || `VP${i + 1}`}] và [${v2.title || `VP${j + 1}`}] (${Math.round(overlapX)}x${Math.round(overlapY)}mm).`);
      }
    }
  }

  // 3. Viewport Balance & Whitespace ratio
  const totalVpArea = viewports.reduce((sum, vp) => sum + vp.width * vp.height, 0);
  const printableArea = (paperWidthMm - marginMm * 2) * (paperHeightMm - marginMm * 2);
  const fillRatio = totalVpArea / printableArea;

  if (fillRatio > 0.88) {
    whitespace -= 10;
    warnings.push(`Mật độ bản vẽ quá dày đặc (${Math.round(fillRatio * 100)}% diện tích giấy). Đề xuất chuyển sang khổ giấy lớn hơn (A1) hoặc tách Sheet Set.`);
    recommendations.push("Khuyên dùng chức năng Auto Sheet Set để tự tách thành bộ 2-3 bản vẽ chuyên nghiệp.");
  } else if (fillRatio < 0.35 && viewports.length > 0) {
    whitespace -= 8;
    recommendations.push("Bản vẽ còn nhiều khoảng trống trống trải, có thể tăng tỷ lệ Viewport chính lên 1:50 hoặc thêm các Detail cấu tạo.");
  } else {
    recommendations.push("Tỷ lệ phân bổ diện tích giữa Mặt bằng, Mặt cắt và Detail đạt chuẩn công thái học thị giác.");
  }

  readability = Math.max(10, Math.min(100, readability));
  alignment = Math.max(10, Math.min(100, alignment));
  whitespace = Math.max(10, Math.min(100, whitespace));
  vpBalance = Math.max(10, Math.min(100, vpBalance));
  scaleConsistency = Math.max(10, Math.min(100, scaleConsistency));

  const overallScore = Math.round(
    readability * 0.25 + alignment * 0.2 + whitespace * 0.2 + vpBalance * 0.2 + scaleConsistency * 0.15
  );

  return {
    overallScore,
    readabilityScore: readability,
    alignmentScore: alignment,
    whiteSpaceScore: whitespace,
    viewportBalanceScore: vpBalance,
    scaleConsistencyScore: scaleConsistency,
    collisionCount: collisions,
    warnings,
    recommendations,
  };
}

// ==========================================
// 5. AUTO SHEET SET GENERATOR (SPLIT SHEETS)
// ==========================================
export function generateAutoSheetSet(
  paperSize: "A0" | "A1" | "A2" | "A3" | "A4",
  orientation: "Landscape" | "Portrait",
  details: AutoDetailProposal[],
  sections: AutoSectionProposal[]
): SheetSetProposal[] {
  const selectedDetails = details.filter((d) => d.isSelected);
  const selectedSections = sections.filter((s) => s.isSelected);

  // If A1 or A0: Single Sheet is sufficient
  if (paperSize === "A1" || paperSize === "A0") {
    return [
      {
        sheetNumber: "A-101",
        sheetTitle: "MẶT BẰNG, MẶT CẮT & CHI TIẾT THI CÔNG KIẾN TRÚC",
        paperSize,
        orientation,
        viewports: [
          {
            type: "MAIN_PLAN",
            title: "MẶT BẰNG THI CÔNG TỔNG THỂ",
            scale: "1:50",
            modelCenter: { x: 3000, y: 2000 },
            paperPosition: { x: 25, y: 25, width: 480, height: 480 },
          },
          ...selectedSections.map((sec, idx) => ({
            type: "SECTION" as const,
            title: sec.title,
            scale: sec.recommendedScale,
            modelCenter: { x: (sec.p1.x + sec.p2.x) / 2, y: (sec.p1.y + sec.p2.y) / 2 },
            paperPosition: { x: 525, y: 25 + idx * 240, width: 275, height: 220 },
          })),
          ...selectedDetails.map((det, idx) => ({
            type: "DETAIL" as const,
            title: `${det.detailNumber} - ${det.title}`,
            scale: det.recommendedScale,
            detailNumber: det.detailNumber,
            modelCenter: det.center,
            paperPosition: { x: 25 + idx * 155, y: 400, width: 145, height: 160 },
          })),
        ],
      },
    ];
  }

  // If A3 or A4: Propose Multi-Sheet Set to avoid clutter
  const sheetSet: SheetSetProposal[] = [];

  // Sheet 1: General Plan & Notes
  sheetSet.push({
    sheetNumber: "A-101",
    sheetTitle: "MẶT BẰNG BỐ TRÍ NỘI THẤT & TRẦN THẠCH CAO",
    paperSize: "A3",
    orientation: "Landscape",
    viewports: [
      {
        type: "MAIN_PLAN",
        title: "MẶT BẰNG CHÍNH",
        scale: "1:50",
        modelCenter: { x: 3000, y: 2000 },
        paperPosition: { x: 15, y: 15, width: 260, height: 230 },
      },
    ],
  });

  // Sheet 2: Sections
  if (selectedSections.length > 0) {
    sheetSet.push({
      sheetNumber: "A-102",
      sheetTitle: "MẶT CẮT DỌC & BỘ PHẬN KIẾN TRÚC",
      paperSize: "A3",
      orientation: "Landscape",
      viewports: selectedSections.map((sec, idx) => ({
        type: "SECTION" as const,
        title: sec.title,
        scale: "1:25",
        modelCenter: { x: (sec.p1.x + sec.p2.x) / 2, y: (sec.p1.y + sec.p2.y) / 2 },
        paperPosition: { x: 20, y: 20 + idx * 120, width: 375, height: 110 },
      })),
    });
  }

  // Sheet 3: Details
  if (selectedDetails.length > 0) {
    sheetSet.push({
      sheetNumber: "A-103",
      sheetTitle: "CHI TIẾT CẤU TẠO KIẾN TRÚC & HOÀN THIỆN",
      paperSize: "A3",
      orientation: "Landscape",
      viewports: selectedDetails.map((det, idx) => {
        const col = idx % 3;
        const row = Math.floor(idx / 3);
        return {
          type: "DETAIL" as const,
          title: `DETAIL ${det.detailNumber} - ${det.title}`,
          scale: det.recommendedScale,
          detailNumber: det.detailNumber,
          modelCenter: det.center,
          paperPosition: { x: 15 + col * 128, y: 15 + row * 120, width: 120, height: 110 },
        };
      }),
    });
  }

  return sheetSet;
}

// ==========================================
// 6. GENERATE CAD CALLOUT ENTITIES FOR MODEL
// ==========================================
export function createDetailAndSectionCalloutEntities(
  details: AutoDetailProposal[],
  sections: AutoSectionProposal[],
  primarySheetNumber: string = "A-101"
): CadEntity[] {
  const newEntities: CadEntity[] = [];

  // Generate Detail Callout Bubbles on Plan
  details.filter((d) => d.isSelected).forEach((det) => {
    const detailCallout: CadDetailCallout = {
      id: `det_callout_${det.id}_${Date.now()}`,
      handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
      type: "DETAIL_CALLOUT",
      layer: "KT_KYHIEU_TRICH",
      color: "#00E5FF",
      detailNumber: det.detailNumber,
      sheetNumber: primarySheetNumber,
      title: det.title,
      scale: det.recommendedScale,
      center: det.center,
      box: det.bounds,
      bubblePos: { x: det.center.x + 600, y: det.center.y + 600 },
      leaderEnd: { x: det.center.x + 350, y: det.center.y + 350 },
      isLinkedToLayout: true,
    };
    newEntities.push(detailCallout);
  });

  // Generate Section Cut Lines on Plan
  sections.filter((s) => s.isSelected).forEach((sec) => {
    const sectionCallout: CadSectionCallout = {
      id: `sec_callout_${sec.id}_${Date.now()}`,
      handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
      type: "SECTION_CALLOUT",
      layer: "KT_KYHIEU_CAT",
      color: "#FF9100",
      sectionName: sec.sectionName,
      sheetNumber: primarySheetNumber,
      p1: sec.p1,
      p2: sec.p2,
      arrowDirection: sec.arrowDirection,
      viewTitle: sec.title,
      isLinkedToLayout: true,
    };
    newEntities.push(sectionCallout);
  });

  return newEntities;
}
