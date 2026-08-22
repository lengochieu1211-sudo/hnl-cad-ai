import { SpreadsheetParameter } from "../types/cad";

export const INITIAL_SPREADSHEET_PARAMETERS: SpreadsheetParameter[] = [
  {
    id: "param_board_w",
    name: "BoardWidth",
    expression: "1200",
    evaluatedValue: 1200,
    unit: "mm",
    description: "Chiều rộng tấm thạch cao tiêu chuẩn",
    category: "Ceiling",
  },
  {
    id: "param_board_l",
    name: "BoardLength",
    expression: "2400",
    evaluatedValue: 2400,
    unit: "mm",
    description: "Chiều dài tấm thạch cao tiêu chuẩn",
    category: "Ceiling",
  },
  {
    id: "param_main_spacing",
    name: "MainFrameSpacing",
    expression: "800",
    evaluatedValue: 800,
    unit: "mm",
    description: "Khoảng cách trục xương chính trần chìm",
    category: "Ceiling",
  },
  {
    id: "param_stud_spacing",
    name: "StudSpacing",
    expression: "400",
    evaluatedValue: 400,
    unit: "mm",
    description: "Khoảng cách Stud vách chống cháy EI60",
    category: "Wall",
  },
  {
    id: "param_waste_factor",
    name: "WasteFactor",
    expression: "1.08",
    evaluatedValue: 1.08,
    unit: "%",
    description: "Hệ số hao hụt cắt tấm & khung biên (8%)",
    category: "Cost",
  },
  {
    id: "param_total_area",
    name: "TotalFloorArea",
    expression: "24.0",
    evaluatedValue: 24.0,
    unit: "m²",
    description: "Tổng diện tích sàn tầng 1",
    category: "General",
  },
  {
    id: "param_calc_board_qty",
    name: "CeilingBoardGross",
    expression: "TotalFloorArea * WasteFactor",
    evaluatedValue: 25.92,
    unit: "m²",
    description: "Tổng diện tích tấm thạch cao tính cả hao hụt",
    category: "Cost",
  },
];

// Simple & safe mathematical expression parser for CAD parameters
export function evaluateExpression(
  expr: string,
  params: Record<string, number>
): number {
  try {
    let sanitized = expr.trim();
    // Replace parameter variable names with their numerical values
    for (const [key, val] of Object.entries(params)) {
      const regex = new RegExp(`\\b${key}\\b`, "g");
      sanitized = sanitized.replace(regex, val.toString());
    }

    // Only allow safe math tokens
    if (!/^[\d\s+\-*/().%]+$/.test(sanitized)) {
      return NaN;
    }

    // eslint-disable-next-line no-new-func
    const result = Function(`"use strict"; return (${sanitized});`)();
    return typeof result === "number" && !isNaN(result) ? Math.round(result * 100) / 100 : NaN;
  } catch {
    return NaN;
  }
}
