export type CadAliasSupport="READY"|"PARTIAL"|"HYBRID";
export interface CadAliasDefinition{
  aliases:string[];
  command:string;
  label:string;
  group:"DRAW"|"EDIT"|"DIM_TEXT";
  support:CadAliasSupport;
  note?:string;
  nativeCommand?:string;
}

export const CAD_COMMAND_ALIASES:CadAliasDefinition[]=[
  {aliases:["L","LINE"],command:"DRAW_LINE",label:"LINE",group:"DRAW",support:"READY",nativeCommand:"LINE"},
  {aliases:["PL","PLINE"],command:"DRAW_POLYLINE",label:"PLINE",group:"DRAW",support:"PARTIAL",note:"Standalone hỗ trợ multi-vertex; khi AutoCAD Connected chạy PLINE native đầy đủ.",nativeCommand:"PLINE"},
  {aliases:["C","CIRCLE"],command:"DRAW_CIRCLE",label:"CIRCLE",group:"DRAW",support:"READY",nativeCommand:"CIRCLE"},
  {aliases:["REC","RECTANG","RECTANGLE"],command:"DRAW_RECTANGLE",label:"RECTANG",group:"DRAW",support:"READY",nativeCommand:"RECTANG"},
  {aliases:["POL","POLYGON"],command:"DRAW_POLYGON",label:"POLYGON",group:"DRAW",support:"PARTIAL",nativeCommand:"POLYGON"},
  {aliases:["A","ARC"],command:"DRAW_ARC",label:"ARC",group:"DRAW",support:"PARTIAL",nativeCommand:"ARC"},
  {aliases:["H","HATCH"],command:"DRAW_HATCH",label:"HATCH",group:"DRAW",support:"PARTIAL",nativeCommand:"HATCH"},

  {aliases:["CO","CP","COPY"],command:"EDIT_COPY",label:"COPY",group:"EDIT",support:"PARTIAL",note:"Standalone dùng offset; AutoCAD Connected chạy COPY native.",nativeCommand:"COPY"},
  {aliases:["M","MOVE"],command:"EDIT_MOVE",label:"MOVE",group:"EDIT",support:"PARTIAL",note:"Standalone nhập ΔX/ΔY; AutoCAD Connected chạy MOVE native.",nativeCommand:"MOVE"},
  {aliases:["RO","ROTATE"],command:"EDIT_ROTATE",label:"ROTATE",group:"EDIT",support:"PARTIAL",note:"Standalone xoay quanh tâm selection; AutoCAD Connected chạy ROTATE native.",nativeCommand:"ROTATE"},
  {aliases:["SC","SCALE"],command:"EDIT_SCALE",label:"SCALE",group:"EDIT",support:"PARTIAL",note:"Standalone scale quanh tâm selection; AutoCAD Connected chạy SCALE native.",nativeCommand:"SCALE"},
  {aliases:["TR","TRIM"],command:"EDIT_TRIM",label:"TRIM",group:"EDIT",support:"PARTIAL",nativeCommand:"TRIM"},
  {aliases:["EX","EXTEND"],command:"EDIT_EXTEND",label:"EXTEND",group:"EDIT",support:"PARTIAL",nativeCommand:"EXTEND"},
  {aliases:["F","FILLET"],command:"EDIT_FILLET",label:"FILLET",group:"EDIT",support:"PARTIAL",nativeCommand:"FILLET"},
  {aliases:["CHA","CHAMFER"],command:"EDIT_CHAMFER",label:"CHAMFER",group:"EDIT",support:"PARTIAL",nativeCommand:"CHAMFER"},
  {aliases:["MI","MIRROR"],command:"EDIT_MIRROR",label:"MIRROR",group:"EDIT",support:"PARTIAL",note:"Standalone X/Y; AutoCAD Connected chạy MIRROR native.",nativeCommand:"MIRROR"},
  {aliases:["O","OFFSET"],command:"DRAW_OFFSET",label:"OFFSET",group:"EDIT",support:"PARTIAL",note:"Standalone LINE; AutoCAD Connected chạy OFFSET native.",nativeCommand:"OFFSET"},
  {aliases:["E","ERASE"],command:"DELETE_SELECTION",label:"ERASE",group:"EDIT",support:"READY",nativeCommand:"ERASE"},

  {aliases:["D","DIMSTYLE"],command:"QUICK_DIM_CENTER",label:"DIMSTYLE",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMSTYLE"},
  {aliases:["DLI","DIMLINEAR"],command:"QUICK_DIM_CENTER",label:"DIMLINEAR",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMLINEAR"},
  {aliases:["DAL","DIMALIGNED"],command:"QUICK_DIM_CENTER",label:"DIMALIGNED",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMALIGNED"},
  {aliases:["DAN","DIMANGULAR"],command:"QUICK_DIM_CENTER",label:"DIMANGULAR",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMANGULAR"},
  {aliases:["DRA","DIMRADIUS"],command:"QUICK_DIM_CENTER",label:"DIMRADIUS",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMRADIUS"},
  {aliases:["DDI","DIMDIAMETER"],command:"QUICK_DIM_CENTER",label:"DIMDIAMETER",group:"DIM_TEXT",support:"HYBRID",nativeCommand:"DIMDIAMETER"},
  {aliases:["DI","DIST"],command:"MEASURE_DISTANCE",label:"DIST",group:"DIM_TEXT",support:"PARTIAL",note:"Standalone đo nhanh selection; AutoCAD Connected chạy DIST native.",nativeCommand:"DIST"},
  {aliases:["DT","MT","MTEXT"],command:"DRAW_MTEXT",label:"MTEXT",group:"DIM_TEXT",support:"READY",nativeCommand:"MTEXT"},
];

const aliasMap=new Map<string,CadAliasDefinition>();
for(const d of CAD_COMMAND_ALIASES)for(const a of d.aliases)aliasMap.set(a.toUpperCase(),d);

export function resolveCadAlias(input:string){
  const normalized=input.trim().replace(/^_+/,"").toUpperCase();
  return aliasMap.get(normalized)||null;
}
