export interface HnlCadLayerStandard {
  name: string;
  group: "CEILING" | "WALL" | "STEEL" | "ANNOTATION" | "DATA" | "HELPER";
  color: string;
  aci: number;
  lineweight: number;
  linetype: "Continuous" | "HIDDEN2" | "CENTER2" | "DASHED";
  isPlottable: boolean;
  purpose: string;
}

/**
 * HNL-native shopdrawing layers. All generated HNL geometry is BYLAYER so
 * color/lineweight/linetype can be managed centrally without exploding blocks.
 */
export const HNL_CAD_LAYER_STANDARDS: HnlCadLayerStandard[] = [
  { name:"HNL-CLG-BOARD", group:"CEILING", color:"#9AD9FF", aci:151, lineweight:0.18, linetype:"Continuous", isPlottable:true, purpose:"Biên/joint/tấm trần" },
  { name:"HNL-CLG-MAIN", group:"CEILING", color:"#FF7A00", aci:30, lineweight:0.35, linetype:"Continuous", isPlottable:true, purpose:"Xương chính trần chìm" },
  { name:"HNL-CLG-CROSS", group:"CEILING", color:"#FFD400", aci:2, lineweight:0.25, linetype:"Continuous", isPlottable:true, purpose:"Xương phụ trần chìm 1220/3" },
  { name:"HNL-CLG-HANGER", group:"CEILING", color:"#66FF66", aci:3, lineweight:0.18, linetype:"HIDDEN2", isPlottable:true, purpose:"Ty treo / điểm treo" },
  { name:"HNL-CLG-START", group:"CEILING", color:"#00E5FF", aci:4, lineweight:0.25, linetype:"CENTER2", isPlottable:true, purpose:"Điểm/hướng xuất phát tấm" },

  { name:"HNL-WALL-BOARD", group:"WALL", color:"#F2F2F2", aci:7, lineweight:0.18, linetype:"Continuous", isPlottable:true, purpose:"Biên/joint tấm vách" },
  { name:"HNL-WALL-STUD", group:"WALL", color:"#FF4DFF", aci:6, lineweight:0.25, linetype:"Continuous", isPlottable:true, purpose:"Stud đứng 1220/3 hoặc 1220/2" },
  { name:"HNL-WALL-TRACK", group:"WALL", color:"#00A8FF", aci:5, lineweight:0.35, linetype:"Continuous", isPlottable:true, purpose:"Track trên/dưới" },
  { name:"HNL-WALL-REINF", group:"WALL", color:"#FF3333", aci:1, lineweight:0.40, linetype:"Continuous", isPlottable:true, purpose:"Gia cường jamb/header/reinforcement" },

  { name:"HNL-STEEL-RHS", group:"STEEL", color:"#FF5555", aci:1, lineweight:0.35, linetype:"Continuous", isPlottable:true, purpose:"RHS/SHS/sắt hộp" },
  { name:"HNL-STEEL-PLATE", group:"STEEL", color:"#FFAA00", aci:30, lineweight:0.35, linetype:"Continuous", isPlottable:true, purpose:"Bản mã/plate" },

  { name:"HNL-ANNO-SECTION", group:"ANNOTATION", color:"#FFFFFF", aci:7, lineweight:0.35, linetype:"Continuous", isPlottable:true, purpose:"Ký hiệu mặt cắt" },
  { name:"HNL-ANNO-LEVEL", group:"ANNOTATION", color:"#00FFFF", aci:4, lineweight:0.25, linetype:"Continuous", isPlottable:true, purpose:"Ký hiệu cao độ" },
  { name:"HNL-ANNO-DETAIL", group:"ANNOTATION", color:"#FFFF00", aci:2, lineweight:0.25, linetype:"Continuous", isPlottable:true, purpose:"Ký hiệu detail/callout" },
  { name:"HNL-DATA-FIELD", group:"DATA", color:"#8AE68A", aci:92, lineweight:0.18, linetype:"Continuous", isPlottable:true, purpose:"Field/BOQ annotation" },
  { name:"HNL-NOPLOT-HELPER", group:"HELPER", color:"#777777", aci:8, lineweight:0.05, linetype:"DASHED", isPlottable:false, purpose:"Helper/construction geometry không in" },
];

export const HNL_CAD_LAYER_MAP = new Map(HNL_CAD_LAYER_STANDARDS.map((x) => [x.name, x]));

export function getHnlLayerStandard(name: string) {
  return HNL_CAD_LAYER_MAP.get(name);
}

export function aciToHex(index: number): string {
  const map: Record<number,string> = {
    1:"#FF0000", 2:"#FFFF00", 3:"#00FF00", 4:"#00FFFF", 5:"#0000FF", 6:"#FF00FF", 7:"#FFFFFF", 8:"#808080", 9:"#C0C0C0",
    30:"#FF7F00", 92:"#80E680", 151:"#99D9FF",
  };
  return map[index] || "#FFFFFF";
}

/** AutoCAD LineWeight enum is hundredths of a millimetre for normal values. */
export function cadLineweightEnumToMm(value: number): number {
  if (!Number.isFinite(value) || value < 0) return 0.25;
  return value / 100;
}
