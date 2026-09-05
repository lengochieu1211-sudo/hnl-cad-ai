import type { AICommandPlan } from "../types/cad";

export const AI_NATIVE_CREATE_TYPES = new Set(["LINE","WALL","CIRCLE","POLYLINE","RECTANGLE","TEXT","MTEXT"] as const);
export const AI_AGENT_SAFE_ACTIONS = new Set([
  "DRAW_WALL","DRAW_CEILING","DRAW_LINE","DRAW_CIRCLE","DRAW_RECT","DRAW_POLYLINE","ADD_TEXT","SET_LAYER","AUTO_LAYOUT","CALC_AREA","TRANSLATE",
  "DRAW_MLEADER","ALIGN_MLEADER","CONVERT_MLEADER",
]);

const finite = (value: unknown) => typeof value === "number" && Number.isFinite(value);
const point = (value: any) => value && finite(value.x) && finite(value.y);

function validateEntity(raw:any): string | null {
  if (!raw || typeof raw !== "object") return "Entity preview không hợp lệ.";
  const type = String(raw.type || "").toUpperCase();
  if (!AI_NATIVE_CREATE_TYPES.has(type as any)) return `Entity type ${type || "(trống)"} không nằm trong AI whitelist.`;
  if (raw.layer != null && (typeof raw.layer !== "string" || raw.layer.length > 255)) return "Layer AI không hợp lệ.";
  if (type === "LINE" && (!point(raw.start) || !point(raw.end))) return "LINE thiếu start/end hợp lệ.";
  if (type === "WALL" && (!point(raw.p1) || !point(raw.p2) || !finite(raw.thickness) || raw.thickness <= 0)) return "WALL thiếu p1/p2/thickness hợp lệ.";
  if (type === "CIRCLE" && (!point(raw.center) || !finite(raw.radius) || raw.radius <= 0)) return "CIRCLE thiếu center/radius hợp lệ.";
  if (type === "RECTANGLE" && (!finite(raw.x) || !finite(raw.y) || !finite(raw.width) || !finite(raw.height) || raw.width === 0 || raw.height === 0)) return "RECTANGLE thiếu x/y/width/height hợp lệ.";
  if (type === "POLYLINE") {
    if (!Array.isArray(raw.points) || raw.points.length < 2 || raw.points.length > 500 || raw.points.some((p:any)=>!point(p))) return "POLYLINE cần 2-500 điểm hợp lệ.";
  }
  if ((type === "TEXT" || type === "MTEXT") && (!point(raw.position) || typeof raw.text !== "string" || raw.text.length > 20000)) return `${type} thiếu position/text hợp lệ.`;
  return null;
}

export type AiAgentExecutionCheck = { ok:true; entities:any[] } | { ok:false; reason:string };

export function validateAiPlanForExecution(plan: AICommandPlan): AiAgentExecutionCheck {
  if (!plan || typeof plan !== "object") return { ok:false, reason:"AI plan trống." };
  const actionType = String(plan.actionType || "").toUpperCase();
  if (plan.isDestructive) return { ok:false, reason:"AI plan được đánh dấu DESTRUCTIVE; HNL chặn Agent Execute." };
  if (!AI_AGENT_SAFE_ACTIONS.has(actionType)) return { ok:false, reason:`Action ${actionType || "(trống)"} chưa có executor AI an toàn.` };
  const preview:any = plan.previewData || {};
  if (Array.isArray(preview.entitiesToDelete) && preview.entitiesToDelete.length > 0) return { ok:false, reason:"AI plan yêu cầu xóa entity; Agent Safe Mode chặn." };

  const nativeCreateActions = new Set(["DRAW_LINE","DRAW_CIRCLE","DRAW_RECT","DRAW_POLYLINE","ADD_TEXT"]);
  if (!nativeCreateActions.has(actionType)) return { ok:true, entities:[] };

  const entities = Array.isArray(preview.entitiesToAdd) ? preview.entitiesToAdd : [];
  if (entities.length < 1) return { ok:false, reason:`${actionType} chưa có previewData.entitiesToAdd; không được thực thi mù.` };
  if (entities.length > 100) return { ok:false, reason:"Một lượt AI Agent chỉ được tạo tối đa 100 entity." };
  for (const entity of entities) {
    const error = validateEntity(entity);
    if (error) return { ok:false, reason:error };
  }
  return { ok:true, entities };
}
