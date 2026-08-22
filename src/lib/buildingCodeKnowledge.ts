import { BuildingCodeStandard } from "../types/cad";

// IMPORTANT: Seed/demo knowledge only. Do not treat these presets as normative clauses.
// Production mode must load the actual licensed/current standard and project-approved documents with clause-level traceability.
export const VIETNAM_BUILDING_STANDARDS: BuildingCodeStandard[] = [
  {
    codeId: "QCVN_06_2022_BXD",
    title: "QCVN 06:2022/BXD & Sửa đổi 1:2023 - An toàn cháy cho nhà và công trình",
    authority: "Bộ Xây Dựng (BXD)",
    category: "FIRE_SAFETY",
    summary:
      "DỮ LIỆU MẪU – cần kiểm tra văn bản gốc hiện hành. Quy định bắt buộc về giới hạn chịu lửa của tường ngăn cháy, vách ngăn hành lang thoát nạn, buồng đệm thang máy và hệ trần bảo vệ kết cấu thép/gỗ.",
    keyRules: [
      {
        ruleName: "Vách ngăn hành lang thoát nạn (EI 30 - EI 45)",
        requirement: "Khung C75 dày 0.5mm @ 400/600mm + 01 lớp tấm Gyproc FireBloc 12.5mm mỗi mặt + Bông khoáng Rockwool 50mm (40kg/m³).",
        allowedValues: "EI 30 hoặc EI 45",
        referenceSection: "Bảng 1 - Phân loại bậc chịu lửa & Giới hạn chịu lửa bộ phận nhà",
      },
      {
        ruleName: "Vách ngăn giữa các căn hộ / Buồng thang bộ (EI 60)",
        requirement: "Khung V-Wall C75 @ 400mm + 02 lớp tấm thạch cao chống cháy FireBloc 12.5mm mỗi mặt + Rockwool 50mm (60kg/m³).",
        allowedValues: "EI 60 (Tính toàn vẹn E & Tính cách nhiệt I >= 60 phút)",
        referenceSection: "Mục 2.4 - Ngăn chặn cháy lan theo mặt bằng",
      },
      {
        ruleName: "Vách buồng đệm thang máy & Gian lánh nạn (EI 120 - EI 150)",
        requirement: "Khung đôi Double Stud C75 hoặc C100 @ 300mm + 02 lớp tấm FireBloc 15mm mỗi mặt + 2 lớp Rockwool 50mm (80kg/m³).",
        allowedValues: "EI 120 đến EI 150",
        referenceSection: "Mục 3.2 - Gian lánh nạn & Hành lang thoát hiểm tầng hầm",
      },
      {
        ruleName: "Màng trần ngăn cháy bảo vệ dầm sàn (REI 60)",
        requirement: "Hệ khung trần chìm Vĩnh Tường Serra + Ty ren M8 @ 800mm + 02 lớp tấm FireBloc 15mm.",
        allowedValues: "REI 60",
        referenceSection: "Mục 2.3.2 - Màng trần treo chịu lửa",
      },
    ],
  },
  {
    codeId: "TCVN_9377_2_2012",
    title: "TCVN 9377-2:2012 & ASTM C636 - Công tác hoàn thiện - Thi công và nghiệm thu trần thạch cao",
    authority: "Viện KHCN Xây Dựng (IBST) / BXD",
    category: "INSTALLATION_GUIDE",
    summary:
      "DỮ LIỆU MẪU – cần kiểm tra văn bản gốc/catalog hệ. Tiêu chuẩn kỹ thuật thi công lắp đặt khung xương, ty treo, khoảng cách nẹp viền và kiểm tra độ võng của trần chìm, trần nổi.",
    keyRules: [
      {
        ruleName: "Khoảng cách ty treo trần chìm (Hanger Spacing)",
        requirement: "Khoảng cách tối đa giữa các điểm treo ty ren là 1000mm. Khuyến nghị chuẩn Shopdrawing: 800mm.",
        allowedValues: "Max 1000mm (Chuẩn: 800 - 900mm)",
        referenceSection: "Mục 5.2.1 - Định vị và khoan cấy ty treo",
      },
      {
        ruleName: "Bước xương chính C38 (Main Keel Pitch)",
        requirement: "Khoảng cách giữa các thanh xương chính không vượt quá 1000mm. Khoảng cách thanh chính đầu tiên cách tường <= 300mm.",
        allowedValues: "800mm - 1000mm (Đầu tiên <= 300mm)",
        referenceSection: "Mục 5.2.3 - Khẩu độ thanh chính",
      },
      {
        ruleName: "Bước xương phụ M-Bar / Omega (Cross Keel Pitch)",
        requirement: "Đối với tấm thạch cao 9.5mm / 12.5mm: bước xương phụ là 406mm (chuẩn tấm 1220x2440mm) hoặc 400mm (chuẩn tấm 1200x2400mm).",
        allowedValues: "400mm hoặc 406mm (Dưới ẩm: 300mm)",
        referenceSection: "Mục 5.2.4 - Lắp đặt thanh phụ",
      },
      {
        ruleName: "Độ võng cho phép của hệ trần (Allowable Deflection)",
        requirement: "Độ võng cho phép dưới tải trọng tĩnh không được vượt quá L/360 khẩu độ nhịp.",
        allowedValues: "Deflection <= L / 360",
        referenceSection: "Mục 6.3 - Kiểm tra nghiệm thu bề mặt",
      },
    ],
  },
  {
    codeId: "TCVN_ACOUSTIC_STC",
    title: "Tiêu chuẩn Thiết kế Cách âm & Âm học Công trình (TCVN & ISO 140)",
    authority: "Viện Kiến Trúc Quốc Gia / BXD",
    category: "ACOUSTIC",
    summary: "DỮ LIỆU MẪU – chỉ tham khảo, phải dùng yêu cầu dự án/test report. Quy chuẩn độ cách âm không khí (Sound Transmission Class - STC / Rw) cho phòng ngủ, phòng khách sạn, phòng họp và rạp chiếu phim.",
    keyRules: [
      {
        ruleName: "Vách ngăn phòng ngủ chung cư / Khách sạn 4-5 sao",
        requirement: "Yêu cầu STC >= 50 dB. Cấu tạo: Vách C75 + 2 lớp tấm 12.5mm mỗi bên + Rockwool 50mm 50kg/m³.",
        allowedValues: "STC 50 - 54 dB",
        referenceSection: "Bảng tiêu chuẩn cách âm phòng lưu trú",
      },
      {
        ruleName: "Vách ngăn phòng họp VIP / Phòng Giám đốc",
        requirement: "Yêu cầu STC >= 48 dB để đảm bảo tính bảo mật đàm thoại nội bộ.",
        allowedValues: "STC >= 48 dB",
        referenceSection: "Tiêu chuẩn văn phòng hạng A",
      },
      {
        ruleName: "Trần tiêu âm giảm vọng (NRC - Noise Reduction Coefficient)",
        requirement: "Sử dụng tấm thạch cao đục lỗ Gyptone / Eurocoustic với hệ số hấp thụ âm NRC >= 0.65.",
        allowedValues: "NRC >= 0.65 (Đục lỗ tiêu âm)",
        referenceSection: "Âm học hội trường & Không gian mở",
      },
    ],
  },
];

/**
 * Calculates theoretical hanger spacing and maximum load capacity per ASTM C635
 */
export function calculateHangerLoadCapacity({
  ceilingAreaM2,
  numberOfBoardLayers,
  boardThicknessMm,
  hasInsulation,
  mepExtraLoadKgM2 = 5,
}: {
  ceilingAreaM2: number;
  numberOfBoardLayers: number;
  boardThicknessMm: number;
  hasInsulation: boolean;
  mepExtraLoadKgM2?: number;
}) {
  // Density: Gypsum board approx 8.5 kg/m2 for 12.5mm
  const boardWeightPerLayer = (boardThicknessMm / 12.5) * 8.5;
  const totalBoardWeight = boardWeightPerLayer * numberOfBoardLayers;
  const frameWeight = 2.2; // Keels approx 2.2 kg/m2
  const insulationWeight = hasInsulation ? 3.0 : 0; // Rockwool approx 3 kg/m2
  const totalDeadLoadKgM2 = totalBoardWeight + frameWeight + insulationWeight + mepExtraLoadKgM2;

  // Placeholder capacity for UI demonstration only. Production must obtain anchor/rod/system design capacity from approved manufacturer data and governing design standard.
  const rodSafeCapacityKg = 120;
  const recommendedHangerSpacingMm = totalDeadLoadKgM2 > 25 ? 800 : 900;
  const rodsPerM2 = 1 / ((recommendedHangerSpacingMm / 1000) * (recommendedHangerSpacingMm / 1000));
  const loadPerHangerKg = totalDeadLoadKgM2 / rodsPerM2;
  const safetyFactor = rodSafeCapacityKg / loadPerHangerKg;

  return {
    totalDeadLoadKgM2: Math.round(totalDeadLoadKgM2 * 10) / 10,
    recommendedHangerSpacingMm,
    loadPerHangerKg: Math.round(loadPerHangerKg * 10) / 10,
    safetyFactor: Math.round(safetyFactor * 10) / 10,
    isCompliantASTM: false, // never auto-certify compliance from placeholder data
    requiresEngineeringVerification: true,
  };
}
