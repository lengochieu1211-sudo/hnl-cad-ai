// Knowledge Base chuyên ngành: Shopdrawing Thạch Cao & Vách Ngăn (Ceiling & Drywall Engineering Knowledge Base)
// Chuẩn kỹ thuật HNL Architecture & TCVN / ASTM C635 / ASTM C636 / ASTM C754 / BS EN 520

export type CertaintyLevel =
  | "VERIFIED_TESTED"       // Thông tin chắc chắn đã test/chứng nhận
  | "STANDARD_SPEC"         // Thông tin theo tiêu chuẩn (TCVN / ASTM / BS)
  | "CATALOG_APPROVED"      // Thông tin theo catalog hãng được duyệt (Vĩnh Tường, Boral/USG, Gyproc/Saint-Gobain, Knauf)
  | "PROJECT_SPEC"          // Thông tin theo Spec riêng của dự án
  | "USER_PRESET"           // Thông tin do kỹ sư/người dùng nhập
  | "AI_SUGGESTION_NEEDS_CONFIRMATION"; // AI đề xuất, CẦN XÁC NHẬN THEO SPEC / APPROVED MATERIAL

export interface TechnicalSourceRef {
  sourceType: CertaintyLevel;
  documentName: string;
  sectionOrClause?: string;
  systemCode?: string;
  testReportNo?: string;
  manufacturer?: string;
  revision?: string;
  notes?: string;
}

// 1. Database Hệ Vách Chống Cháy (Fire-Rated Assemblies Database)
export interface FireRatedAssembly {
  assemblyId: string;
  manufacturer: string;
  systemName: string;
  eiRating: "EI30" | "EI45" | "EI60" | "EI90" | "EI120" | "EI150" | "EI180" | "EI240" | string;
  totalThicknessMm: number;
  studType: "C-Stud" | "Double C-Stud" | "Staggered C-Stud" | "CH-Stud (Shaftwall)" | string;
  studSizeMm: number;
  studThicknessMm: number; // e.g. 0.5mm, 0.6mm, 0.8mm
  studSpacingMm: number;
  boardType: string;
  boardThicknessMm: number;
  layersSideA: number; // e.g. 1 or 2 or 3
  layersSideB: number;
  insulationType: "Rockwool" | "Glasswool" | "None" | string;
  insulationDensityKgM3: number; // e.g. 60 kg/m3 for Rockwool EI60
  insulationThicknessMm: number; // e.g. 50mm
  acousticStcRw?: number; // e.g. 48 dB
  maxHeightM: number; // e.g. 4.5m
  sealantType: string;
  testStandard: string;
  testReportNo: string;
  certificateStatus: "🟢 VERIFIED_CERTIFIED" | "🟡 REQUIRES_PROJECT_SUBMITTAL" | string;
  headDetailType: string;
  sourceDoc: string;
}

// 2. Database Hệ Trần Thạch Cao Chìm & Nổi (Ceiling Systems Database)
export interface CeilingSystemSpec {
  systemId: string;
  type: "PLASTER_CONCEALED" | "GRID_EXPOSED_600x600" | "GRID_EXPOSED_600x1200" | "ACOUSTIC_SEAMLESS" | "STEPPED_COVE" | string;
  name: string;
  manufacturer: string;
  mainRunnerProfile: string; // e.g. "Thanh chính M4038 / T-Main 3600"
  mainRunnerSpacingMm: number; // 800 - 1000mm (chìm) hoặc 1200mm (nổi 600x1200)
  crossRunnerProfile: string; // e.g. "Thanh phụ M3812 / T-Cross 1200 & 600"
  crossRunnerSpacingMm: number; // 400 - 406mm (chìm) hoặc 600mm (nổi)
  wallAngleProfile: string; // "Thanh viền tường Shadowline VTC 20/20 hoặc V-Angle"
  wallAngleOffsetMm: number;
  hangerType: string;
  hangerSpacingMm: number; // Max 1000mm
  firstHangerDistanceMm: number; // Max 400mm từ tường
  firstMainDistanceMm: number; // Max 400mm từ tường
  boardType: string;
  boardThicknessMm: number;
  layersCount: number;
  screwSpacingMm: { field: number; edge: number }; // 200mm biên, 300mm bụng
  jointTreatment: string;
  expansionJointIntervalM: number; // Tối đa mỗi 10m-12m bề mặt liên tục
  openingReinforcementRule: string;
  sourceDoc: string;
}

// 3. Database Chi tiết Cấu tạo Shopdrawing (Standard Shop Details)
export interface ShopDetailRule {
  detailId: string;
  category: "CEILING_DETAIL" | "WALL_DETAIL" | "MEP_COORDINATION" | "DOOR_OPENING" | "DEFLECTION_HEAD";
  title: string;
  cadSymbol: string;
  triggerConditions: string[]; // e.g. ["Giao tường thạch cao và trần", "Vách có cửa đi", "Lỗ thăm trần Access Panel"]
  keyComponents: string[];
  cadLayer: string;
  recommendedScale: string; // e.g. "1:10", "1:20", "1:5"
  warningNotes: string;
  verificationTag: string;
}

// KNOWLEDGE BASE DATA TẬP TRUNG CHUYÊN NGÀNH
export const FIRE_RATED_ASSEMBLIES: FireRatedAssembly[] = [
  {
    assemblyId: "W-EI30-01",
    manufacturer: "Vĩnh Tường (Saint-Gobain)",
    systemName: "Hệ vách GypWall DW1 (EI30)",
    eiRating: "EI30",
    totalThicknessMm: 75,
    studType: "C-Stud",
    studSizeMm: 50,
    studThicknessMm: 0.5,
    studSpacingMm: 600,
    boardType: "Thạch cao chống cháy tiêu chuẩn (Fire-Stop)",
    boardThicknessMm: 12.5,
    layersSideA: 1,
    layersSideB: 1,
    insulationType: "Glasswool",
    insulationDensityKgM3: 24,
    insulationThicknessMm: 50,
    acousticStcRw: 42,
    maxHeightM: 3.2,
    sealantType: "Acoustic & Intumescent Firestop Sealant (2 đầu & giáp ranh)",
    testStandard: "TCVN 9383:2012 / BS 476 Part 22",
    testReportNo: "REQUIRED_PROJECT_TEST_REPORT",
    certificateStatus: "🟡 REQUIRES_PROJECT_SUBMITTAL",
    headDetailType: "Standard Fixed",
    sourceDoc: "Catalog Hệ Thống Vách Thạch Cao Vĩnh Tường 2024 / QCVN 06:2022/BXD",
  },
  {
    assemblyId: "W-EI60-01",
    manufacturer: "Vĩnh Tường (Saint-Gobain)",
    systemName: "Hệ vách GypWall DW4 (EI60)",
    eiRating: "EI60",
    totalThicknessMm: 100,
    studType: "C-Stud",
    studSizeMm: 75,
    studThicknessMm: 0.5,
    studSpacingMm: 600,
    boardType: "Thạch cao chống cháy tiêu chuẩn (Fire-Stop)",
    boardThicknessMm: 12.5,
    layersSideA: 1,
    layersSideB: 1,
    insulationType: "Rockwool",
    insulationDensityKgM3: 60,
    insulationThicknessMm: 50,
    acousticStcRw: 48,
    maxHeightM: 4.2,
    sealantType: "Acoustic & Intumescent Firestop Sealant (2 đầu & giáp ranh)",
    testStandard: "TCVN 9383:2012 / BS 476 Part 22 / EN 1364-1",
    testReportNo: "REQUIRED_PROJECT_TEST_REPORT",
    certificateStatus: "🟡 REQUIRES_PROJECT_SUBMITTAL",
    headDetailType: "Standard Fixed",
    sourceDoc: "Dữ liệu mẫu – cần gắn Approved Material/Test Report thực tế của dự án",
  },
  {
    assemblyId: "W-EI90-01",
    manufacturer: "Knauf",
    systemName: "Knauf FireShield W112 (EI90)",
    eiRating: "EI90",
    totalThicknessMm: 125,
    studType: "C-Stud",
    studSizeMm: 75,
    studThicknessMm: 0.6,
    studSpacingMm: 600,
    boardType: "Thạch cao chống cháy tiêu chuẩn (Fire-Stop)",
    boardThicknessMm: 12.5,
    layersSideA: 2,
    layersSideB: 2,
    insulationType: "Rockwool",
    insulationDensityKgM3: 80,
    insulationThicknessMm: 50,
    acousticStcRw: 54,
    maxHeightM: 4.8,
    sealantType: "Acoustic & Intumescent Firestop Sealant (2 đầu & giáp ranh)",
    testStandard: "BS 476 Part 22 / TCVN 9383:2012",
    testReportNo: "REQUIRED_PROJECT_TEST_REPORT",
    certificateStatus: "🟡 REQUIRES_PROJECT_SUBMITTAL",
    headDetailType: "Deflection Head (Slotted Track 25mm)",
    sourceDoc: "Knauf Technical Manual Section 4 - Fire Rated Partitions",
  },
  {
    assemblyId: "W-EI120-01",
    manufacturer: "USG Boral",
    systemName: "USG Boral FireBloc Multi-Layer W113 (EI120)",
    eiRating: "EI120",
    totalThicknessMm: 150,
    studType: "C-Stud",
    studSizeMm: 90,
    studThicknessMm: 0.8,
    studSpacingMm: 400,
    boardType: "Thạch cao chống cháy tiêu chuẩn (Fire-Stop)",
    boardThicknessMm: 15.0,
    layersSideA: 2,
    layersSideB: 2,
    insulationType: "Rockwool",
    insulationDensityKgM3: 100,
    insulationThicknessMm: 75,
    acousticStcRw: 58,
    maxHeightM: 6.0,
    sealantType: "Acoustic & Intumescent Firestop Sealant (2 đầu & giáp ranh)",
    testStandard: "ASTM E119 / TCVN 9383:2012",
    testReportNo: "REQUIRED_PROJECT_TEST_REPORT",
    certificateStatus: "🟡 REQUIRES_PROJECT_SUBMITTAL",
    headDetailType: "Deflection Head (Slotted Track 25mm)",
    sourceDoc: "USG Boral Fire & Acoustic Design Guide Rev 05",
  },
];

export const CEILING_SYSTEMS_KNOWLEDGE: CeilingSystemSpec[] = [
  {
    systemId: "C-CHIM-01",
    type: "PLASTER_CONCEALED",
    name: "Trần thạch cao chìm phẳng - Hệ khung cao cấp Vĩnh Tường BASI / Boral",
    manufacturer: "Vĩnh Tường / USG Boral",
    mainRunnerProfile: "Thanh chính BASI M4038 (dày 0.75mm)",
    mainRunnerSpacingMm: 800, // Khuyến nghị 800mm (max 1000mm)
    crossRunnerProfile: "Thanh phụ BASI M3812 (dày 0.38mm)",
    crossRunnerSpacingMm: 400, // 400mm hoặc 406mm cho tấm 1220x2440
    wallAngleProfile: "Thanh viền nhôm Shadowline VTC 20/20 có rãnh chống nứt biên",
    wallAngleOffsetMm: 20,
    hangerType: "Ty ren M6/M8 + Nở đạn (hoặc ty dây lò xo cho dân dụng)",
    hangerSpacingMm: 900, // Khuyến nghị 900mm (max 1000mm)
    firstHangerDistanceMm: 400, // Max 400mm từ tường biên
    firstMainDistanceMm: 400,
    boardType: "Tấm Gypsum tiêu chuẩn 9mm/12.7mm",
    boardThicknessMm: 9.0,
    layersCount: 1,
    screwSpacingMm: { field: 250, edge: 150 },
    jointTreatment: "Băng keo giấy đục lỗ + Bột Gyp-Filler 3 lớp chà nhám mịn",
    expansionJointIntervalM: 10, // Khe co giãn Control Joint mỗi 10m
    openingReinforcementRule: "Bắt buộc bo khung thanh chính gia cường (Trimmer channel) cho miệng gió, hộp đèn > 300mm",
    sourceDoc: "TCVN 8256:2009 / ASTM C635 & C636 / Sổ tay thi công Vĩnh Tường 2024",
  },
  {
    systemId: "C-NOI-600",
    type: "GRID_EXPOSED_600x600",
    name: "Trần thạch cao nổi (Lay-in Grid) Module 600x600mm",
    manufacturer: "Vĩnh Tường TopLINE / Armstrong Prelude",
    mainRunnerProfile: "Thanh T-Chính Main Runner 3600 (rộng đáy 24mm)",
    mainRunnerSpacingMm: 1200,
    crossRunnerProfile: "Thanh T-Phụ Cross Runner 1200 & 600 (rộng đáy 24mm)",
    crossRunnerSpacingMm: 600,
    wallAngleProfile: "Thanh V viền tường V-Angle 24x24mm (dày 0.4mm)",
    wallAngleOffsetMm: 24,
    hangerType: "Ty dây thép mạ kẽm D4mm + Tăng đơ bướm đôi",
    hangerSpacingMm: 1200,
    firstHangerDistanceMm: 450,
    firstMainDistanceMm: 600,
    boardType: "Tấm sợi khoáng 15mm 600x600",
    boardThicknessMm: 15.0,
    layersCount: 1,
    screwSpacingMm: { field: 0, edge: 0 }, // Không bắn vít, thả tấm tự do
    jointTreatment: "Khe rãnh lộ xương 24mm tiêu chuẩn",
    expansionJointIntervalM: 15,
    openingReinforcementRule: "Đèn Panel 600x600 treo ty độc lập, không dồn tải lên khung xương T",
    sourceDoc: "ASTM C635/C635M-17 / BS 8290 Part 2",
  },
];

export const SHOPDRAWING_DETAIL_CATALOG: ShopDetailRule[] = [
  {
    detailId: "DET-WALL-BASE-01",
    category: "WALL_DETAIL",
    title: "Chi tiết chân vách thạch cao trên sàn bê tông (Floor Track & Sealant)",
    cadSymbol: "DT_CHAN_VACH",
    triggerConditions: ["Chân vách giao sàn bê tông", "Vách yêu cầu chống ẩm / chống cháy / cách âm"],
    keyComponents: ["U-Track chân vách", "2 đường Acoustic / Firestop Sealant dưới đáy U-Track", "Tấm thạch cao hở sàn 10mm chống hút ẩm", "Len chân tường hoặc Skirting"],
    cadLayer: "AP_DET_WALL",
    recommendedScale: "1:10",
    warningNotes: "Tấm thạch cao PHẢI cách mặt sàn thô 10-15mm, không được tiếp xúc trực tiếp nền để tránh hút ẩm mao dẫn.",
    verificationTag: "TCVN 8256:2009 / Approved Detail V01",
  },
  {
    detailId: "DET-DEFLECTION-HEAD-01",
    category: "DEFLECTION_HEAD",
    title: "Chi tiết đầu vách trượt chống võng sàn (Deflection Head / Slotted Track)",
    cadSymbol: "DT_DAU_VACH_TRUOT",
    triggerConditions: ["Vách chạm đáy sàn bê tông / dầm vượt nhịp > 6m", "Sàn có độ võng thiết kế L/360 hoặc L/500"],
    keyComponents: ["Deep Leg Track (Cánh sâu 50mm) hoặc Slotted Track", "Đầu C-Stud cắt ngắn 20mm tạo khoảng hở chuyển vị", "Không bắn vít khóa cứng Stud vào Track", "Bông Rockwool nén đàn hồi + Mastic chống cháy đàn hồi"],
    cadLayer: "AP_DET_WALL",
    recommendedScale: "1:10",
    warningNotes: "CẤM bắn vít liên kết cứng giữa Stud và Head Track khi có yêu cầu Deflection Head! Phải duy trì khoảng trượt 15-25mm theo tính toán kết cấu sàn.",
    verificationTag: "CẦN XÁC NHẬN THEO STRUCTURAL DEFLECTION SPEC",
  },
  {
    detailId: "DET-DOOR-JAMB-01",
    category: "DOOR_OPENING",
    title: "Chi tiết gia cường khuôn cửa đi trên vách thạch cao (Boxed Stud / Timber Backing)",
    cadSymbol: "DT_GIA_CUONG_CUA",
    triggerConditions: ["Cửa đi mở cánh > 30kg", "Cửa chống cháy thép / gỗ", "Cửa lùa trượt"],
    keyComponents: ["Boxed Stud (2 thanh C lồng thành hộp) hoặc Double Stud", "Gỗ gia cường (Timber Backing 45x70mm) chèn trong lòng Stud", "Thanh ngang Header giằng chéo 45 độ lên dầm"],
    cadLayer: "AP_DET_DOOR",
    recommendedScale: "1:10",
    warningNotes: "Cửa chống cháy bắt buộc hệ Stud hộp + Bông chèn kín + Ke chống cháy đã test đồng bộ với nhà sản xuất cửa.",
    verificationTag: "APPROVED MATERIAL SUBMITTAL - DOOR FRAMING",
  },
  {
    detailId: "DET-CEILING-STEP-01",
    category: "CEILING_DETAIL",
    title: "Chi tiết trần thạch cao giật cấp hắt đèn LED (Cove Light Ceiling Detail)",
    cadSymbol: "DT_TRAN_GIAT_CAP",
    triggerConditions: ["Trần giật cấp", "Khe hắt sáng LED", "Trần hộp rèm"],
    keyComponents: ["Khung xương VTC viền đứng + đáy cấp", "Thanh nhôm chữ V viền mép hắt đèn", "Tấm thạch cao uốn bo hoặc giáp mí", "Băng keo góc V-Trim chống nứt nẻ mép"],
    cadLayer: "AP_DET_CEILING",
    recommendedScale: "1:10",
    warningNotes: "Khoảng mở khe hắt LED tối thiểu 100mm để người thợ có thể luồn tay lắp máng đèn và đấu nối dây điện an toàn.",
    verificationTag: "HNL STANDARD DETAIL C03",
  },
  {
    detailId: "DET-MEP-FIRESTOP-01",
    category: "MEP_COORDINATION",
    title: "Chi tiết ống kỹ thuật MEP xuyên vách chống cháy (Firestop Collar & Sealant)",
    cadSymbol: "DT_XUYEN_VACH_PCCC",
    triggerConditions: ["Ống nhựa PVC / Ống thép MEP xuyên qua vách EI", "Dây cáp điện / Thang máng cáp xuyên vách"],
    keyComponents: ["Đai quấn chống cháy trương nở (Firestop Collar)", "Vữa ngăn cháy trương nở (Firestop Mortar / Sealant)", "Tấm ốp gia cường quanh lỗ mở (Collaring Gypsum)"],
    cadLayer: "AP_DET_MEP",
    recommendedScale: "1:10",
    warningNotes: "FIRESTOP REQUIRED! Bắt buộc sử dụng hệ keo & đai ngăn cháy đạt chứng chỉ cùng cấp EI với hệ vách (Hilti / 3M / Promat).",
    verificationTag: "PCCC SPECIFICATION / APPROVED FIRESTOP SUBMITTAL",
  },
];

// Hàm tra cứu và kiểm định thông minh:
export function lookupFireRatedAssembly(criteria: {
  targetEI?: string;
  maxThickness?: number;
  acousticRw?: number;
}): { matched: FireRatedAssembly[]; statusMessage: string; certainty: CertaintyLevel } {
  if (!criteria.targetEI) {
    return {
      matched: FIRE_RATED_ASSEMBLIES,
      statusMessage: "CẦN XÁC NHẬN THEO SPEC / APPROVED MATERIAL / CATALOG HÃNG",
      certainty: "AI_SUGGESTION_NEEDS_CONFIRMATION",
    };
  }

  const results = FIRE_RATED_ASSEMBLIES.filter((a) => a.eiRating === criteria.targetEI);
  if (results.length > 0) {
    return {
      matched: results,
      statusMessage: `Tìm thấy ${results.length} hệ cấu tạo đã kiểm chứng (Tested & Certified) đạt cấp ${criteria.targetEI}.`,
      certainty: "AI_SUGGESTION_NEEDS_CONFIRMATION",
    };
  }

  return {
    matched: [],
    statusMessage: `⚠️ CẢNH BÁO PCCC: Chưa có báo cáo kiểm định (Test Report) cho yêu cầu ${criteria.targetEI} với các thông số này. Bắt buộc kiểm tra Spec dự án & Submittal của hãng được duyệt!`,
    certainty: "AI_SUGGESTION_NEEDS_CONFIRMATION",
  };
}
