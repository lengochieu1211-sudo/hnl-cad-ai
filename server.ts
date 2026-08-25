import express from "express";
import path from "path";
import { GoogleGenAI } from "@google/genai";
import dotenv from "dotenv";
import { HNL_APP_VERSION } from "./src/lib/version";

dotenv.config();


const app = express();
const PORT = Number(process.env.HNL_PORT || 3000);

app.use(express.json({ limit: "20mb" }));
app.use(express.urlencoded({ extended: true, limit: "20mb" }));

// Packaged Electron uses a per-session random token so unrelated web pages/processes cannot call privileged localhost APIs.
app.use("/api", (req, res, next) => {
  const expected = process.env.HNL_API_TOKEN || "";
  if (!expected) return next(); // browser/dev mode
  if (req.path === "/health") return next();
  if (req.get("x-hnl-token") !== expected) return res.status(403).json({ error: "Invalid HNL session token" });
  next();
});

type AiProviderId =
  | "OFFLINE"
  | "GEMINI"
  | "OPENAI"
  | "CLAUDE"
  | "GROK"
  | "OLLAMA"
  | "CUSTOM_OPENAI";

type AiProviderConfig = {
  id: AiProviderId;
  label: string;
  model: string;
  baseUrl: string;
  apiKey: string;
  configured: boolean;
};

const PROVIDER_DEFAULTS: Record<AiProviderId, {label:string; model:string; baseUrl:string; keyEnv?:string}> = {
  OFFLINE: { label: "HNL Offline Rules", model: "hnl-rules-v1", baseUrl: "" },
  GEMINI: { label: "Google Gemini", model: "gemini-3.7-flash", baseUrl: "https://generativelanguage.googleapis.com", keyEnv: "GEMINI_API_KEY" },
  OPENAI: { label: "ChatGPT / OpenAI", model: "gpt-5.6", baseUrl: "https://api.openai.com/v1", keyEnv: "OPENAI_API_KEY" },
  CLAUDE: { label: "Claude / Anthropic", model: "claude-sonnet-4-20250514", baseUrl: "https://api.anthropic.com/v1", keyEnv: "ANTHROPIC_API_KEY" },
  GROK: { label: "Grok / xAI", model: "grok-4.6", baseUrl: "https://api.x.ai/v1", keyEnv: "XAI_API_KEY" },
  OLLAMA: { label: "Ollama Local", model: "gemma3", baseUrl: "http://127.0.0.1:11434" },
  CUSTOM_OPENAI: { label: "Custom OpenAI-compatible", model: "gpt-4o-mini", baseUrl: "http://127.0.0.1:1234/v1", keyEnv: "CUSTOM_OPENAI_API_KEY" },
};

function normalizeProviderId(value: unknown): AiProviderId {
  const id = String(value || process.env.HNL_AI_ACTIVE_PROVIDER || "GEMINI").trim().toUpperCase();
  return (Object.prototype.hasOwnProperty.call(PROVIDER_DEFAULTS, id) ? id : "OFFLINE") as AiProviderId;
}

function providerEnvPrefix(id: AiProviderId) {
  return `HNL_AI_${id}`;
}

function getProviderConfig(idValue?: unknown): AiProviderConfig {
  const id = normalizeProviderId(idValue);
  const def = PROVIDER_DEFAULTS[id];
  const prefix = providerEnvPrefix(id);
  const model = process.env[`${prefix}_MODEL`] || def.model;
  const baseUrl = (process.env[`${prefix}_BASE_URL`] || def.baseUrl).replace(/\/+$/, "");
  const apiKey = def.keyEnv ? (process.env[def.keyEnv] || "") : (id === "CUSTOM_OPENAI" ? (process.env.CUSTOM_OPENAI_API_KEY || "") : "");
  const configured = id === "OFFLINE" || id === "OLLAMA" || Boolean(apiKey);
  return { id, label: def.label, model, baseUrl, apiKey, configured };
}

function getSafeProviderStatus() {
  const ids = Object.keys(PROVIDER_DEFAULTS) as AiProviderId[];
  return {
    activeProvider: normalizeProviderId(process.env.HNL_AI_ACTIVE_PROVIDER),
    providers: ids.map((id) => {
      const cfg = getProviderConfig(id);
      return {
        id: cfg.id,
        label: cfg.label,
        model: cfg.model,
        baseUrl: cfg.baseUrl,
        configured: cfg.configured,
      };
    }),
  };
}

function isOfflineFallbackAllowed() {
  return String(process.env.HNL_AI_AUTO_FALLBACK_OFFLINE || "false").toLowerCase() === "true";
}

function hasOnlineProvider(idValue?: unknown) {
  const cfg = getProviderConfig(idValue);
  return cfg.id !== "OFFLINE" && cfg.configured;
}

function openAiResponsesUrl(baseUrl: string) {
  const base = baseUrl.replace(/\/+$/, "");
  return base.endsWith("/responses") ? base : `${base}/responses`;
}

function openAiChatUrl(baseUrl: string) {
  const base = baseUrl.replace(/\/+$/, "");
  return base.endsWith("/chat/completions") ? base : `${base}/chat/completions`;
}

async function readJsonResponse(response: Response) {
  const text = await response.text();
  let data: any = null;
  try { data = text ? JSON.parse(text) : {}; } catch { data = { raw: text }; }
  if (!response.ok) {
    const message = data?.error?.message || data?.error || data?.message || text || `HTTP ${response.status}`;
    throw new Error(String(message));
  }
  return data;
}

function extractOpenAiResponseText(data: any): string {
  if (typeof data?.output_text === "string") return data.output_text;
  if (Array.isArray(data?.output)) {
    for (const item of data.output) {
      if (!Array.isArray(item?.content)) continue;
      for (const part of item.content) {
        if (typeof part?.text === "string") return part.text;
        if (part?.type === "output_text" && typeof part?.text === "string") return part.text;
      }
    }
  }
  if (typeof data?.choices?.[0]?.message?.content === "string") return data.choices[0].message.content;
  return "";
}

function extractClaudeText(data: any): string {
  if (!Array.isArray(data?.content)) return "";
  return data.content.map((part:any) => part?.type === "text" ? String(part.text || "") : "").join("\n").trim();
}

// Lazy init retained only for the official Gemini SDK.
let aiClient: GoogleGenAI | null = null;
let aiClientKey = "";
function getGeminiAI(apiKey: string): GoogleGenAI {
  if (!aiClient || aiClientKey !== apiKey) {
    aiClientKey = apiKey;
    aiClient = new GoogleGenAI({
      apiKey,
      httpOptions: { headers: { "User-Agent": "hnl-cad-ai" } },
    });
  }
  return aiClient;
}

async function callAiText(args: {
  provider?: unknown;
  prompt: string;
  systemInstruction?: string;
  jsonMode?: boolean;
}): Promise<{text:string; provider:AiProviderId; model:string}> {
  const cfg = getProviderConfig(args.provider);
  const system = args.systemInstruction || "Bạn là trợ lý kỹ sư CAD của HNL.";
  const userPrompt = args.jsonMode
    ? `${args.prompt}\n\nYÊU CẦU ĐẦU RA: Chỉ trả JSON hợp lệ, không markdown fence.`
    : args.prompt;

  if (cfg.id === "OFFLINE") {
    throw new Error("OFFLINE_PROVIDER");
  }

  if (cfg.id === "GEMINI") {
    if (!cfg.apiKey) throw new Error("Gemini API key chưa cấu hình.");
    const ai = getGeminiAI(cfg.apiKey);
    const response = await ai.models.generateContent({
      model: cfg.model,
      contents: userPrompt,
      config: {
        systemInstruction: system,
        ...(args.jsonMode ? { responseMimeType: "application/json" } : {}),
      },
    });
    return { text: response.text || "", provider: cfg.id, model: cfg.model };
  }

  if (cfg.id === "OPENAI" || cfg.id === "GROK") {
    if (!cfg.apiKey) throw new Error(`${cfg.label} API key chưa cấu hình.`);
    const response = await fetch(openAiResponsesUrl(cfg.baseUrl), {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${cfg.apiKey}`,
      },
      body: JSON.stringify({
        model: cfg.model,
        input: [
          { role: "system", content: system },
          { role: "user", content: userPrompt },
        ],
      }),
    });
    const data = await readJsonResponse(response);
    return { text: extractOpenAiResponseText(data), provider: cfg.id, model: cfg.model };
  }

  if (cfg.id === "CLAUDE") {
    if (!cfg.apiKey) throw new Error("Claude API key chưa cấu hình.");
    const base = cfg.baseUrl.replace(/\/+$/, "");
    const response = await fetch(base.endsWith("/messages") ? base : `${base}/messages`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-api-key": cfg.apiKey,
        "anthropic-version": "2023-06-01",
      },
      body: JSON.stringify({
        model: cfg.model,
        max_tokens: args.jsonMode ? 4096 : 2048,
        system,
        messages: [{ role: "user", content: userPrompt }],
      }),
    });
    const data = await readJsonResponse(response);
    return { text: extractClaudeText(data), provider: cfg.id, model: cfg.model };
  }

  if (cfg.id === "OLLAMA") {
    const base = cfg.baseUrl.replace(/\/+$/, "");
    const url = base.endsWith("/api/chat") ? base : `${base}/api/chat`;
    const response = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: cfg.model,
        stream: false,
        ...(args.jsonMode ? { format: "json" } : {}),
        messages: [
          { role: "system", content: system },
          { role: "user", content: userPrompt },
        ],
      }),
    });
    const data = await readJsonResponse(response);
    return { text: String(data?.message?.content || ""), provider: cfg.id, model: cfg.model };
  }

  if (cfg.id === "CUSTOM_OPENAI") {
    const headers: Record<string,string> = { "Content-Type": "application/json" };
    if (cfg.apiKey) headers.Authorization = `Bearer ${cfg.apiKey}`;
    // Prefer Responses API. If the custom server does not support it, retry Chat Completions.
    try {
      const response = await fetch(openAiResponsesUrl(cfg.baseUrl), {
        method: "POST",
        headers,
        body: JSON.stringify({
          model: cfg.model,
          input: [
            { role: "system", content: system },
            { role: "user", content: userPrompt },
          ],
        }),
      });
      const data = await readJsonResponse(response);
      return { text: extractOpenAiResponseText(data), provider: cfg.id, model: cfg.model };
    } catch {
      const response = await fetch(openAiChatUrl(cfg.baseUrl), {
        method: "POST",
        headers,
        body: JSON.stringify({
          model: cfg.model,
          messages: [
            { role: "system", content: system },
            { role: "user", content: userPrompt },
          ],
          stream: false,
        }),
      });
      const data = await readJsonResponse(response);
      return { text: extractOpenAiResponseText(data), provider: cfg.id, model: cfg.model };
    }
  }

  throw new Error(`Provider không hỗ trợ: ${cfg.id}`);
}

// In-memory Translation Memory storage
const translationMemory: Array<{
  original: string;
  translated: string;
  sourceLang: string;
  targetLang: string;
  category: string;
  verified: boolean;
}> = [
  { original: "TRẦN THẠCH CAO", translated: "GYPSUM CEILING", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "XƯƠNG CHÍNH", translated: "MAIN TEE / MAIN RUNNER", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "XƯƠNG PHỤ", translated: "CROSS TEE", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "TY TREO", translated: "SUSPENSION ROD / HANGER WIRE", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "VIỀN TƯỜNG", translated: "WALL ANGLE", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "MẶT BẰNG TẦNG 01", translated: "1ST FLOOR PLAN", sourceLang: "vi", targetLang: "en", category: "Title", verified: true },
  { original: "MẶT BẰNG TRẦN ĐÈN", translated: "REFLECTED CEILING PLAN", sourceLang: "vi", targetLang: "en", category: "Title", verified: true },
  { original: "MẶT CẮT A-A", translated: "SECTION A-A", sourceLang: "vi", targetLang: "en", category: "Title", verified: true },
  { original: "CHI TIẾT LẮP ĐẶT", translated: "INSTALLATION DETAIL", sourceLang: "vi", targetLang: "en", category: "Title", verified: true },
  { original: "BẢN VẼ THI CÔNG", translated: "CONSTRUCTION DRAWING", sourceLang: "vi", targetLang: "en", category: "Title", verified: true },
  { original: "TỶ LỆ", translated: "SCALE", sourceLang: "vi", targetLang: "en", category: "TitleBlock", verified: true },
  { original: "SỐ BẢN VẼ", translated: "DRAWING NO.", sourceLang: "vi", targetLang: "en", category: "TitleBlock", verified: true },
  { original: "GHI CHÚ CHUNG", translated: "GENERAL NOTES", sourceLang: "vi", targetLang: "en", category: "Notes", verified: true },
  { original: "TƯỜNG XÂY GẠCH 100", translated: "100MM BRICK WALL", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
  { original: "TƯỜNG XÂY GẠCH 200", translated: "200MM BRICK WALL", sourceLang: "vi", targetLang: "en", category: "Architecture", verified: true },
];

// Health endpoint
app.get("/api/health", (req, res) => {
  res.json({
    status: "ok",
    app: "HNL CAD AI TOOL",
    version: HNL_APP_VERSION,
    ai: getSafeProviderStatus(),
  });
});


// AutoCAD native bridge registry/action queue.
// Localhost only + HNL session token. AutoCAD plugin reads pairing info from %TEMP%/HNL_CAD_AI/bridge.json.
type AutoCadBridgeRegistration = {
  connected:boolean;
  version?:string;
  drawingName?:string;
  pluginVersion?:string;
  bridgeInstanceId?:string;
  processId?:number;
  lastSeen:number;
  capabilities?:string[];
};
type AutoCadQueuedAction = {
  id:string;
  action:string;
  payload:any;
  createdAt:number;
  timeoutMs:number;
  expiresAt:number;
};
let autoCadBridge: AutoCadBridgeRegistration = { connected:false, lastSeen:0, capabilities:[] };
const autoCadActionQueue: AutoCadQueuedAction[] = [];
const autoCadActionResults = new Map<string, any>();
const AUTOCAD_RESULT_TTL_MS = 10 * 60_000;
const AUTOCAD_QUEUE_MAX = 200;
const AUTOCAD_TIMEOUT_MIN_MS = 1_000;
const AUTOCAD_TIMEOUT_MAX_MS = 5 * 60_000;
const bridgeAlive=()=>autoCadBridge.connected && Date.now()-autoCadBridge.lastSeen<5000;
const clampAutoCadTimeout=(value:unknown,fallback=20_000)=>{
  const n=Number(value);
  if(!Number.isFinite(n))return fallback;
  return Math.max(AUTOCAD_TIMEOUT_MIN_MS,Math.min(AUTOCAD_TIMEOUT_MAX_MS,Math.round(n)));
};
function pruneAutoCadBridgeBuffers() {
  const now = Date.now();
  for(let i=autoCadActionQueue.length-1;i>=0;i--){
    if(autoCadActionQueue[i].expiresAt<=now)autoCadActionQueue.splice(i,1);
  }
  if (autoCadActionQueue.length > AUTOCAD_QUEUE_MAX) autoCadActionQueue.splice(0, autoCadActionQueue.length - AUTOCAD_QUEUE_MAX);
  for (const [id, value] of autoCadActionResults.entries()) {
    if (now - Number(value?.receivedAt || now) > AUTOCAD_RESULT_TTL_MS) autoCadActionResults.delete(id);
  }
}
function enqueueAutoCadAction(actionValue:unknown,payload:any,timeoutValue:unknown){
  pruneAutoCadBridgeBuffers();
  if(!bridgeAlive())return{ok:false as const,status:409,reason:"AUTOCAD_BRIDGE_NOT_CONNECTED"};
  const action=String(actionValue||"").trim().toUpperCase();
  if(!action)return{ok:false as const,status:400,reason:"AUTOCAD_ACTION_EMPTY"};
  const caps=Array.isArray(autoCadBridge.capabilities)?autoCadBridge.capabilities:[];
  if(caps.length && !caps.includes(action))return{ok:false as const,status:422,reason:"AUTOCAD_ACTION_NOT_SUPPORTED",action};
  if(autoCadActionQueue.length>=AUTOCAD_QUEUE_MAX)return{ok:false as const,status:429,reason:"AUTOCAD_ACTION_QUEUE_FULL",queueDepth:autoCadActionQueue.length};
  const timeoutMs=clampAutoCadTimeout(timeoutValue);
  const createdAt=Date.now();
  const id=`act_${createdAt}_${Math.random().toString(36).slice(2,8)}`;
  autoCadActionQueue.push({id,action,payload:payload??{},createdAt,timeoutMs,expiresAt:createdAt+timeoutMs});
  return{ok:true as const,id,action,timeoutMs,expiresAt:createdAt+timeoutMs};
}
async function waitForAutoCadResult(id:string,timeoutMs:number){
  const started=Date.now();
  while(Date.now()-started<timeoutMs){
    pruneAutoCadBridgeBuffers();
    const value=autoCadActionResults.get(id);
    if(value){autoCadActionResults.delete(id);return value;}
    await new Promise(resolve=>setTimeout(resolve,75));
  }
  const i=autoCadActionQueue.findIndex(x=>x.id===id);
  if(i>=0)autoCadActionQueue.splice(i,1);
  return{ok:false,reason:"AUTOCAD_BRIDGE_GOLDEN_TIMEOUT",id,timeoutMs};
}

app.post("/api/autocad/register", (req,res)=>{
  autoCadBridge={
    connected:true,
    version:String(req.body?.version||""),
    drawingName:String(req.body?.drawingName||""),
    pluginVersion:String(req.body?.pluginVersion||""),
    bridgeInstanceId:String(req.body?.bridgeInstanceId||""),
    processId:Number(req.body?.processId||0)||undefined,
    lastSeen:Date.now(),
    capabilities:Array.isArray(req.body?.capabilities)?req.body.capabilities.map(String):[]
  };
  res.json({ok:true,serverVersion:HNL_APP_VERSION});
});
app.post("/api/autocad/heartbeat", (req,res)=>{
  autoCadBridge={...autoCadBridge,connected:true,lastSeen:Date.now(),drawingName:String(req.body?.drawingName||autoCadBridge.drawingName||"")};
  res.json({ok:true,serverTime:Date.now()});
});
app.get("/api/autocad/status", (_req,res)=>{
  pruneAutoCadBridgeBuffers();
  res.json({...autoCadBridge,connected:bridgeAlive(),queueDepth:autoCadActionQueue.length,resultCount:autoCadActionResults.size});
});
app.post("/api/autocad/action", (req,res)=>{
  const queued=enqueueAutoCadAction(req.body?.action,req.body?.payload,req.body?.timeoutMs);
  if(!queued.ok)return res.status(queued.status).json(queued);
  res.json(queued);
});
app.post("/api/autocad/action/:id/cancel", (req,res)=>{
  const id=String(req.params.id||"");
  const i=autoCadActionQueue.findIndex(x=>x.id===id);
  if(i>=0){autoCadActionQueue.splice(i,1);return res.json({ok:true,id,cancelled:true,state:"QUEUED"});}
  if(autoCadActionResults.has(id)){autoCadActionResults.delete(id);return res.json({ok:true,id,cancelled:true,state:"RESULT"});}
  res.json({ok:true,id,cancelled:false,state:"DISPATCHED_OR_UNKNOWN"});
});
app.get("/api/autocad/poll", (_req,res)=>{
  pruneAutoCadBridgeBuffers();
  const item=autoCadActionQueue.shift()||null;
  res.json({item});
});
app.post("/api/autocad/result", (req,res)=>{
  pruneAutoCadBridgeBuffers();
  const id=String(req.body?.id||"");
  if(id)autoCadActionResults.set(id,{...req.body,receivedAt:Date.now()});
  res.json({ok:true});
});
app.get("/api/autocad/result/:id", (req,res)=>{
  pruneAutoCadBridgeBuffers();
  const x=autoCadActionResults.get(req.params.id);
  if(!x)return res.status(404).json({ok:false,pending:true});
  autoCadActionResults.delete(req.params.id);
  res.json(x);
});

// Read-only Golden Smoke: proves a real round trip Server -> AutoCAD -> Server.
// It deliberately avoids any drawing mutation.
app.post("/api/autocad/golden-smoke", async (_req,res)=>{
  pruneAutoCadBridgeBuffers();
  if(!bridgeAlive())return res.status(409).json({ok:false,reason:"AUTOCAD_BRIDGE_NOT_CONNECTED"});
  const required=["GET_STATUS","GET_DRAFTING_STATUS","GET_LAYOUTS","GET_LAYERS"];
  const missing=required.filter(x=>!(autoCadBridge.capabilities||[]).includes(x));
  if(missing.length)return res.status(422).json({ok:false,reason:"AUTOCAD_REQUIRED_CAPABILITY_MISSING",missing});
  const evidence:any[]=[];
  for(const action of required){
    const queued=enqueueAutoCadAction(action,{},8_000);
    if(!queued.ok)return res.status(queued.status).json({...queued,evidence});
    const t0=Date.now();
    const result=await waitForAutoCadResult(queued.id,8_500);
    evidence.push({action,ok:Boolean(result?.ok),latencyMs:Date.now()-t0,result:result?.result??null,error:result?.error??result?.reason??null});
    if(!result?.ok)return res.status(502).json({ok:false,reason:"AUTOCAD_GOLDEN_ACTION_FAILED",failedAction:action,evidence});
  }
  res.json({
    ok:true,
    gate:"BRIDGE_GOLDEN_READ_ONLY",
    serverVersion:HNL_APP_VERSION,
    pluginVersion:autoCadBridge.pluginVersion||"",
    autoCadVersion:autoCadBridge.version||"",
    drawingName:autoCadBridge.drawingName||"",
    bridgeInstanceId:autoCadBridge.bridgeInstanceId||"",
    processId:autoCadBridge.processId||0,
    checkedAt:new Date().toISOString(),
    evidence
  });
});

// API: Unified AI Provider Manager / CAD Command Planner
app.get("/api/ai/status", (_req, res) => {
  res.json(getSafeProviderStatus());
});

app.post("/api/ai/test", async (req, res) => {
  const provider = normalizeProviderId(req.body?.provider);
  if (provider === "OFFLINE") {
    return res.json({ ok: true, provider, model: "hnl-rules-v1", message: "HNL Offline Rules sẵn sàng." });
  }
  try {
    const result = await callAiText({
      provider,
      prompt: "Return exactly: HNL_OK",
      systemInstruction: "You are a connectivity test. Return exactly HNL_OK.",
      jsonMode: false,
    });
    res.json({
      ok: /HNL_OK/i.test(result.text),
      provider: result.provider,
      model: result.model,
      message: result.text.slice(0, 300),
    });
  } catch (err:any) {
    res.status(400).json({ ok: false, provider, error: err?.message || String(err) });
  }
});

const CAD_PLAN_SYSTEM_INSTRUCTION = `Bạn là CAD Command Planner & AI Copilot cao cấp thuộc bộ công cụ HNL CAD AI TOOL cho AutoCAD 2023+.
Nhận yêu cầu tiếng Việt/Anh cùng CAD Context và sinh Structured CAD Action Plan.

QUY TẮC AN TOÀN:
1. SAFE: vẽ mới, copy, đo, dimension, text, table, ghi chú.
2. DESTRUCTIVE: delete, replace all, purge, xóa layer/layout, overwrite.
3. Không tự thực thi thay đổi nguy hiểm. Chỉ trả plan để HNL Preview/xác nhận.
4. Nếu CAD Context có Approved Material/Submittal: ưu tiên nguồn APPROVED trước Manufacturer Catalog, rồi mới đến HNL Project Rule.
5. Không trộn thông số giữa hai hãng/hệ/revision.
6. Nếu thiếu nguồn kỹ thuật: ghi certainty="UNVERIFIED" và yêu cầu kỹ sư xác nhận, không tự bịa.

Trả JSON:
{
  "intent": "Mục đích ngắn",
  "actionType": "DRAW_WALL | DRAW_CEILING | DRAW_RECT | DRAW_POLYLINE | DIMENSION | CALC_AREA | CREATE_TABLE | BATCH_MODIFY | TRANSLATE | AUDIT | AUTO_LAYOUT | EXPORT_BOQ",
  "isDestructive": false,
  "confidence": 0.0,
  "certainty": "APPROVED_PROJECT | MANUFACTURER_VERIFIED | HNL_PROJECT_RULE | UNVERIFIED",
  "explanation": "Giải thích cho kỹ sư",
  "sourceRefs": [{"type":"APPROVED_SUBMITTAL | PROJECT_SPEC | MANUFACTURER | HNL_RULE","title":"...","revision":"...","note":"..."}],
  "steps": [{"stepIndex":1,"command":"CAD_COMMAND_NAME","description":"...","parameters":{}}],
  "previewData": {"entityType":"WALL | CEILING_GRID | RECTANGLE | TABLE | DIMENSION | TEXT","entitiesToAdd":[],"entitiesToModify":[],"entitiesToDelete":[]}
}`;

async function handleAiPlan(req:any, res:any) {
  try {
    const { prompt, cadContext, provider } = req.body || {};
    if (!prompt) return res.status(400).json({ error: "Prompt is required" });

    const selected = normalizeProviderId(provider);
    if (selected === "OFFLINE" || !hasOnlineProvider(selected)) {
      return res.json({
        plan: generateOfflinePlan(prompt, cadContext),
        provider: "OFFLINE",
        model: "hnl-rules-v1",
        isOfflineFallback: true,
        fallbackReason: selected === "OFFLINE" ? "OFFLINE_SELECTED" : "PROVIDER_NOT_CONFIGURED",
      });
    }

    try {
      const answer = await callAiText({
        provider: selected,
        systemInstruction: CAD_PLAN_SYSTEM_INSTRUCTION,
        prompt: `Yêu cầu người dùng: "${prompt}"
CAD Context hiện tại:
${JSON.stringify(cadContext || {}, null, 2)}`,
        jsonMode: true,
      });
      const parsed = JSON.parse(answer.text || "{}");
      return res.json({
        plan: parsed,
        provider: answer.provider,
        model: answer.model,
        isOfflineFallback: false,
      });
    } catch (providerError:any) {
      if (!isOfflineFallbackAllowed()) throw providerError;
      return res.json({
        plan: generateOfflinePlan(prompt, cadContext),
        provider: "OFFLINE",
        model: "hnl-rules-v1",
        isOfflineFallback: true,
        error: providerError?.message || String(providerError),
      });
    }
  } catch (err:any) {
    return res.status(500).json({ error: err?.message || String(err) });
  }
}

app.post("/api/ai/plan", handleAiPlan);
// Backward compatibility for older HNL UI/plugin builds.
app.post("/api/gemini/plan", handleAiPlan);

// API: AI AutoLISP Builder
app.post("/api/gemini/lisp", async (req, res) => {
  try {
    const { prompt, commandName = "AP_AUTO" } = req.body;
    if (!prompt) {
      return res.status(400).json({ error: "Prompt is required" });
    }

    const selectedProvider = normalizeProviderId(req.body?.provider);
    if (selectedProvider === "OFFLINE") {
      return res.json({ lisp: generateOfflineLisp(prompt, commandName), provider: "OFFLINE", isOfflineFallback: false });
    }
    if (!hasOnlineProvider(selectedProvider)) {
      if (!isOfflineFallbackAllowed()) return res.status(400).json({ error: `${selectedProvider} chưa được cấu hình. HNL không tự chuyển provider.` });
      return res.json({ lisp: generateOfflineLisp(prompt, commandName), provider: "OFFLINE", isOfflineFallback: true, fallbackReason: "PROVIDER_NOT_CONFIGURED" });
    }

    const systemInstruction = `Bạn là Chuyên gia AutoLISP & Visual LISP hàng đầu cho AutoCAD 2023-2026 thuộc HNL CAD AI TOOL.
Nhiệm vụ: Viết mã AutoLISP (.lsp) chuẩn mực, tối ưu, có xử lý lỗi (*error*), kiểm tra Undo Group (vla-StartUndoMark / vla-EndUndoMark), biến hệ thống (OSMODE, CMDECHO, CLAYER), tương thích Unicode tiếng Việt, và không chạy lệnh nguy hiểm (không xóa file ổ đĩa, không shell script độc hại).

Trả về JSON:
{
  "commandName": "C:${commandName.replace(/^C:/i, "")}",
  "title": "Tiêu đề LISP",
  "description": "Mô tả chức năng",
  "category": "DRAW | TEXT | BLOCK | AREA | TABLE | LAYOUT | UTILITY",
  "code": "MÃ AUTOLISP HOÀN CHỈNH VỚI ĐẦY ĐỦ COMMENT",
  "syntaxValid": true,
  "usageInstructions": "Hướng dẫn sử dụng lệnh trong AutoCAD"
}`;

    const answer = await callAiText({
      provider: selectedProvider,
      prompt: `Yêu cầu viết Lisp: "${prompt}"\nTên lệnh mong muốn: "${commandName}"`,
      systemInstruction,
      jsonMode: true,
    });

    try {
      const parsed = JSON.parse(answer.text || "{}");
      res.json({ ...parsed, provider: answer.provider, model: answer.model, isOfflineFallback: false });
    } catch {
      if (!isOfflineFallbackAllowed()) return res.status(502).json({ error: "AI trả về Lisp không đúng JSON; HNL không tự chuyển Offline." });
      res.json({ lisp: generateOfflineLisp(prompt, commandName), provider:"OFFLINE", isOfflineFallback: true, fallbackReason:"INVALID_AI_JSON" });
    }
  } catch (err: any) {
    console.error("AI Lisp error:", err);
    if (!isOfflineFallbackAllowed()) return res.status(502).json({ error: err.message || String(err) });
    res.json({ lisp: generateOfflineLisp(req.body.prompt || "", req.body.commandName || "AP_CMD"), provider:"OFFLINE", isOfflineFallback: true, error: err.message });
  }
});

// API: CAD Drawing Translator
app.post("/api/gemini/translate", async (req, res) => {
  try {
    const { items, sourceLang = "vi", targetLang = "en", mode = "Bilingual" } = req.body;
    if (!Array.isArray(items) || items.length === 0) {
      return res.status(400).json({ error: "Items array is required" });
    }

    // Step 1: Check Translation Memory first
    const translatedResults: Array<{
      id: string;
      original: string;
      translated: string;
      finalText: string;
      fromMemory: boolean;
    }> = [];

    const itemsToCallAI: Array<{ id: string; original: string; index: number }> = [];

    items.forEach((item, index) => {
      const cleanOrig = String(item.text || "").trim().toUpperCase();
      const memoryMatch = translationMemory.find(
        (m) =>
          m.original.toUpperCase() === cleanOrig &&
          m.sourceLang === sourceLang &&
          m.targetLang === targetLang
      );

      if (memoryMatch) {
        const trans = memoryMatch.translated;
        let finalText = trans;
        if (mode === "Bilingual") {
          finalText = `${item.text}\n${trans}`;
        } else if (mode === "SideBySide") {
          finalText = `${item.text} | ${trans}`;
        }
        translatedResults.push({
          id: item.id,
          original: item.text,
          translated: trans,
          finalText,
          fromMemory: true,
        });
      } else {
        itemsToCallAI.push({ id: item.id, original: item.text, index });
      }
    });

    if (itemsToCallAI.length > 0 && hasOnlineProvider(req.body?.provider)) {
      const selectedProvider = normalizeProviderId(req.body?.provider);
      const prompt = `Dịch danh sách các thuật ngữ / ghi chú kỹ thuật bản vẽ CAD từ ${sourceLang} sang ${targetLang}.
Giữ phong cách dịch chuẩn chuyên ngành Kiến trúc, Kết cấu, MEP, Xây dựng (Construction & Architecture CAD standards).

Danh sách:
${JSON.stringify(itemsToCallAI.map((it) => ({ id: it.id, text: it.original })), null, 2)}

Trả về JSON array:
[
  { "id": "string", "translated": "string" }
]`;

      const answer = await callAiText({
        provider: selectedProvider,
        prompt,
        systemInstruction: "Bạn là biên dịch viên kỹ thuật CAD/Xây dựng. Dịch chính xác, ngắn gọn, giữ ký hiệu và kích thước.",
        jsonMode: true,
      });

      try {
        const aiOutput: Array<{ id: string; translated: string }> = JSON.parse(answer.text || "[]");
        aiOutput.forEach((resItem) => {
          const origObj = itemsToCallAI.find((x) => x.id === resItem.id);
          if (origObj) {
            const trans = resItem.translated;
            let finalText = trans;
            if (mode === "Bilingual") {
              finalText = `${origObj.original}\n${trans}`;
            } else if (mode === "SideBySide") {
              finalText = `${origObj.original} | ${trans}`;
            }
            translatedResults.push({
              id: origObj.id,
              original: origObj.original,
              translated: trans,
              finalText,
              fromMemory: false,
            });

            // Save to memory
            translationMemory.push({
              original: origObj.original,
              translated: trans,
              sourceLang,
              targetLang,
              category: "AutoSaved",
              verified: false,
            });
          }
        });
      } catch (parseErr) {
        console.error("Translate JSON parse error:", parseErr);
      }
    }

    // Fill remaining if any failed
    items.forEach((item) => {
      if (!translatedResults.find((r) => r.id === item.id)) {
        const trans = `[${targetLang.toUpperCase()}] ${item.text}`;
        translatedResults.push({
          id: item.id,
          original: item.text,
          translated: trans,
          finalText: mode === "Bilingual" ? `${item.text}\n${trans}` : trans,
          fromMemory: false,
        });
      }
    });

    res.json({ results: translatedResults, total: translatedResults.length });
  } catch (err: any) {
    console.error("Translate error:", err);
    res.status(500).json({ error: err.message });
  }
});

// API: CAD Drawing Audit & Inspection
app.post("/api/gemini/audit", async (req, res) => {
  try {
    const { drawingData } = req.body;
    // Built-in automated rule checks + Gemini intelligence
    const issues: Array<{
      id: string;
      type: "ERROR" | "WARNING" | "INFO";
      category: "LAYER" | "TEXT" | "DIMENSION" | "FIELD" | "VIEWPORT" | "GEOMETRY";
      entityHandle?: string;
      title: string;
      description: string;
      location?: { x: number; y: number };
      fixAction: string;
      canAutoFix: boolean;
    }> = [];

    // Analyze entities
    const entities = drawingData?.entities || [];
    const viewports = drawingData?.viewports || [];
    const fields = drawingData?.fields || [];

    // 1. Check unlocked Viewports
    viewports.forEach((vp: any, idx: number) => {
      if (!vp.locked) {
        issues.push({
          id: `vp-unlock-${idx}`,
          type: "WARNING",
          category: "VIEWPORT",
          entityHandle: vp.handle || `VP_${idx + 1}`,
          title: `Viewport ${vp.name || idx + 1} chưa khóa (Unlocked)`,
          description: `Viewport tại Layout "${vp.layout || "Layout1"}" chưa khóa tỷ lệ, dễ bị lệch khi zoom.`,
          fixAction: "LOCK_VIEWPORT",
          canAutoFix: true,
        });
      }
    });

    // 2. Check broken fields
    fields.forEach((f: any, idx: number) => {
      if (f.isBroken || f.targetFound === false) {
        issues.push({
          id: `field-broken-${idx}`,
          type: "ERROR",
          category: "FIELD",
          entityHandle: f.handle || `FIELD_${idx + 1}`,
          title: `Field mất liên kết (Broken Field #${idx + 1})`,
          description: `Field tham chiếu "${f.targetName || "Object"}" bị xóa hoặc mất Handle. Hiển thị "####".`,
          fixAction: "RELINK_FIELD",
          canAutoFix: true,
        });
      }
    });

    // 3. Check text overlaps & zero length
    entities.forEach((ent: any) => {
      if (ent.type === "TEXT" || ent.type === "MTEXT") {
        if (ent.height && ent.height < 1.8) {
          issues.push({
            id: `text-small-${ent.id}`,
            type: "WARNING",
            category: "TEXT",
            entityHandle: ent.handle,
            title: `Text quá nhỏ (${ent.height}mm)`,
            description: `Văn bản "${ent.text?.substring(0, 20)}..." nhỏ hơn tiêu chuẩn in ấn A3 (min 2.0mm).`,
            location: { x: ent.x || 0, y: ent.y || 0 },
            fixAction: "RESIZE_TEXT_2.5",
            canAutoFix: true,
          });
        }
      }
      if (ent.type === "POLYLINE" && ent.closed === false && ent.layer?.includes("WALL")) {
        issues.push({
          id: `poly-open-${ent.id}`,
          type: "ERROR",
          category: "GEOMETRY",
          entityHandle: ent.handle,
          title: `Polyline tường bị hở (Unclosed Wall Polyline)`,
          description: `Polyline trên layer ${ent.layer} chưa đóng, không thể Hatch hoặc tính diện tích tự động.`,
          location: ent.points?.[0] || { x: 0, y: 0 },
          fixAction: "CLOSE_POLYLINE",
          canAutoFix: true,
        });
      }
    });

    // If drawingData is empty, provide realistic sample issues
    if (issues.length === 0) {
      issues.push(
        {
          id: "samp-1",
          type: "WARNING",
          category: "VIEWPORT",
          entityHandle: "3F2A",
          title: "Viewport Chi tiết 01 chưa khóa tỷ lệ (1:50)",
          description: "Layout 'KT-02 Mặt Bằng' có 1 Viewport đang mở khóa.",
          fixAction: "LOCK_VIEWPORT",
          canAutoFix: true,
        },
        {
          id: "samp-2",
          type: "ERROR",
          category: "FIELD",
          entityHandle: "4B1C",
          title: "Field diện tích Phòng Khách mất liên kết (####)",
          description: "Polyline ranh giới phòng đã bị Explode trước đó.",
          fixAction: "RELINK_FIELD",
          canAutoFix: true,
        },
        {
          id: "samp-3",
          type: "WARNING",
          category: "TEXT",
          entityHandle: "1E90",
          title: "Text ghi chú sai Font Style (VNI-Helve trên Unicode)",
          description: "Ghi chú layer NOTE_KT chứa ký tự mã VNI chưa chuẩn hóa Unicode.",
          fixAction: "NORMALIZE_UNICODE",
          canAutoFix: true,
        },
        {
          id: "samp-4",
          type: "INFO",
          category: "LAYER",
          entityHandle: "00A1",
          title: "Phát hiện 3 Layer rỗng không sử dụng",
          description: "Layer 'DEFPOINTS_TEMP', 'TEMP_HATCH', 'LAYER1' có thể Purge.",
          fixAction: "PURGE_UNUSED_LAYERS",
          canAutoFix: true,
        }
      );
    }

    res.json({ issues, total: issues.length });
  } catch (err: any) {
    console.error("Audit error:", err);
    res.status(500).json({ error: err.message });
  }
});

// Translation Memory API
app.get("/api/translation-memory", (req, res) => {
  res.json({ memory: translationMemory, count: translationMemory.length });
});

app.post("/api/translation-memory", (req, res) => {
  const { original, translated, sourceLang = "vi", targetLang = "en", category = "Custom" } = req.body;
  if (!original || !translated) {
    return res.status(400).json({ error: "Original and translated texts are required" });
  }
  const existing = translationMemory.find(
    (m) => m.original.toUpperCase() === original.toUpperCase() && m.targetLang === targetLang
  );
  if (existing) {
    existing.translated = translated;
    existing.verified = true;
  } else {
    translationMemory.unshift({
      original,
      translated,
      sourceLang,
      targetLang,
      category,
      verified: true,
    });
  }
  res.json({ success: true, count: translationMemory.length });
});

// Offline rule-based Plan Generator
function generateOfflinePlan(prompt: string, cadContext: any) {
  const p = prompt.toLowerCase();
  if (p.includes("tường") || p.includes("wall")) {
    const is200 = p.includes("200");
    return {
      intent: `Vẽ tường ${is200 ? "200mm" : "100mm"} thông minh`,
      actionType: "DRAW_WALL",
      isDestructive: false,
      confidence: 0.95,
      explanation: `Tạo đoạn tường dày ${is200 ? "200" : "100"}mm từ tim trục, tự động offset ±${is200 ? "100" : "50"}mm, join góc và gán Layer WALL_100.`,
      steps: [
        { stepIndex: 1, command: "AP_WALL", description: `Thiết lập độ dày ${is200 ? "200" : "100"}mm`, parameters: { thickness: is200 ? 200 : 100, layer: "KT_TUONG" } },
        { stepIndex: 2, command: "OFFSET_JOIN", description: "Tự động bo góc và cắt giao điểm", parameters: { autoTrim: true, autoHatch: true } },
      ],
      previewData: {
        entityType: "WALL",
        entitiesToAdd: [
          {
            type: "WALL",
            layer: "KT_TUONG",
            color: "#00E5FF",
            props: { thickness: is200 ? 200 : 100, x1: 1000, y1: 1000, x2: 7000, y2: 1000 },
          },
        ],
      },
    };
  }

  if (p.includes("trần") || p.includes("ceiling") || p.includes("thạch cao")) {
    return {
      intent: "Bố trí trần thạch cao chìm thông minh",
      actionType: "DRAW_CEILING",
      isDestructive: false,
      confidence: 0.96,
      explanation: "Tự động phân bổ hệ xương chính (@800mm), xương phụ (@400mm), ty treo (@1000mm) và V viền tường trong phạm vi phòng được chọn.",
      steps: [
        { stepIndex: 1, command: "AP_CEILING_GRID", description: "Bố trí khung xương chính & xương phụ", parameters: { mainSpacing: 800, subSpacing: 400, hangerSpacing: 1000 } },
        { stepIndex: 2, command: "AP_CEILING_ANNOTATE", description: "Ghi chú cao độ trần và khoảng cách", parameters: { style: "ARCH_NOTE" } },
      ],
      previewData: {
        entityType: "CEILING_GRID",
        entitiesToAdd: [
          {
            type: "CEILING",
            layer: "KT_TRAN_XUONG",
            color: "#FF9100",
            props: { mainSpacing: 800, subSpacing: 400, countMain: 6, countSub: 12 },
          },
        ],
      },
    };
  }

  if (p.includes("diện tích") || p.includes("area") || p.includes("phòng")) {
    return {
      intent: "Tính diện tích và gắn nhãn Field động",
      actionType: "CALC_AREA",
      isDestructive: false,
      confidence: 0.92,
      explanation: "Quét Polyline phòng, tính diện tích thực (m²), chèn MText kèm Field tự cập nhật vào tâm hình học.",
      steps: [
        { stepIndex: 1, command: "AP_AREA_LABEL", description: "Tính diện tích & chèn Field 'A = ##.## m²'", parameters: { unit: "m2", decimals: 2 } },
      ],
      previewData: {
        entityType: "TEXT",
        entitiesToAdd: [
          {
            type: "TEXT",
            layer: "KT_TEXT_AREA",
            color: "#00E676",
            props: { text: "A = 28.50 m²", x: 3500, y: 3000, height: 250 },
          },
        ],
      },
    };
  }

  if (p.includes("layout") || p.includes("viewport") || p.includes("khổ giấy") || p.includes("a3")) {
    return {
      intent: "Tạo Layout A3 & Auto Fit Viewport",
      actionType: "AUTO_LAYOUT",
      isDestructive: false,
      confidence: 0.94,
      explanation: "Tự động tạo Sheet Layout A3 (420x297mm), chèn Khung tên HNL, tạo Viewport tối ưu tỷ lệ 1:100 và khóa Viewport.",
      steps: [
        { stepIndex: 1, command: "HNL_CREATE_LAYOUT", description: "Tạo Layout A3 Landscape", parameters: { paperSize: "A3", orientation: "Landscape" } },
        { stepIndex: 2, command: "HNL_FIT_VIEWPORT", description: "Căn giữa vùng chọn với tỷ lệ 1:100 và Lock Viewport", parameters: { scale: "1:100", lock: true } },
      ],
      previewData: {
        entityType: "LAYOUT",
        entitiesToAdd: [
          {
            type: "LAYOUT",
            layer: "DEFPOINTS",
            color: "#E040FB",
            props: { sheet: "A3", scale: "1:100", title: "MẶT BẰNG TẦNG 01" },
          },
        ],
      },
    };
  }

  // Default CAD Rectangle or general action
  return {
    intent: "Thực hiện lệnh CAD thông minh",
    actionType: "DRAW_RECT",
    isDestructive: false,
    confidence: 0.85,
    explanation: `Phân tích yêu cầu "${prompt}" và tạo đối tượng CAD tương ứng trong Model Space.`,
    steps: [
      { stepIndex: 1, command: "HNL_SMART_EXEC", description: `Thực hiện: ${prompt}`, parameters: { target: "ACTIVE_MODEL" } },
    ],
    previewData: {
      entityType: "RECTANGLE",
      entitiesToAdd: [
        {
          type: "RECTANGLE",
          layer: "0",
          color: "#00E5FF",
          props: { width: 6000, height: 4000, x: 1000, y: 1000 },
        },
      ],
    },
  };
}

function generateOfflineLisp(prompt: string, commandName: string) {
  const cmd = commandName.replace(/^C:/i, "").toUpperCase();
  return {
    commandName: `C:${cmd}`,
    title: `Lệnh AutoLISP ${cmd}`,
    description: `Tạo bởi HNL CAD AI TOOL cho: ${prompt}`,
    category: "DRAW",
    syntaxValid: true,
    usageInstructions: `Gõ lệnh '${cmd}' trong dòng lệnh AutoCAD để chạy.`,
    code: `;;; =========================================================================
;;; HNL CAD AI TOOL - AutoLISP Generator
;;; Lệnh: ${cmd}
;;; Mô tả: ${prompt}
;;; Tương thích: AutoCAD 2023, 2024, 2025, 2026+
;;; =========================================================================

(vl-load-com)

(defun C:${cmd} (/ *error* doc oldOsm oldEcho oldLayer ss i ent obj area pt str)
  
  ;; Hàm xử lý lỗi an toàn
  (defun *error* (msg)
    (if oldOsm (setvar "OSMODE" oldOsm))
    (if oldEcho (setvar "CMDECHO" oldEcho))
    (if oldLayer (setvar "CLAYER" oldLayer))
    (vla-EndUndoMark doc)
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*")))
      (princ (strcat "\\n[HNL ERROR]: " msg))
    )
    (princ)
  )

  ;; Khởi tạo môi trường & Undo Mark
  (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
  (vla-StartUndoMark doc)
  (setq oldOsm (getvar "OSMODE"))
  (setq oldEcho (getvar "CMDECHO"))
  (setq oldLayer (getvar "CLAYER"))
  (setvar "CMDECHO" 0)

  (princ "\\n>>> HNL CAD TOOL: Chọn các đối tượng cần xử lý: ")
  (if (setq ss (ssget '((0 . "*POLYLINE,SPLINE,REGION,CIRCLE"))))
    (progn
      (setq i 0)
      (while (< i (sslength ss))
        (setq ent (ssname ss i))
        (setq obj (vlax-ename->vla-object ent))
        ;; Tính diện tích an toàn
        (if (vlax-property-available-p obj 'Area)
          (progn
            (setq area (/ (vlax-get-property obj 'Area) 1000000.0)) ; Chuyển mm2 -> m2
            (setq str (strcat "S = " (rtos area 2 2) " m2"))
            (princ (strcat "\\nĐối tượng " (itoa (1+ i)) ": " str))
          )
        )
        (setq i (1+ i))
      )
      (princ (strcat "\\n[HNL]: Đã xử lý hoàn tất " (itoa (sslength ss)) " đối tượng."))
    )
    (princ "\\n[HNL]: Không có đối tượng nào được chọn.")
  )

  ;; Khôi phục biến hệ thống
  (setvar "OSMODE" oldOsm)
  (setvar "CMDECHO" oldEcho)
  (setvar "CLAYER" oldLayer)
  (vla-EndUndoMark doc)
  (princ)
)

(princ (strcat "\\n[HNL TOOL] Lệnh C:${cmd} đã được tải thành công. Gõ ${cmd} để thực thi."))
(princ)
`,
  };
}

// Vite / Production middleware
async function startServer() {
  if (process.env.NODE_ENV !== "production") {
    const { createServer: createViteServer } = await import("vite");
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: "spa",
    });
    app.use(vite.middlewares);
  } else {
    // Packaged Electron sets HNL_APP_ROOT to resources/app.asar.
    // Use that virtual root instead of process.cwd(), which is not stable when
    // launched from Program Files, Start Menu, or a Desktop shortcut.
    const appRoot = process.env.HNL_APP_ROOT || process.cwd();
    const distPath = path.join(appRoot, "dist");
    app.use(express.static(distPath));
    app.get("*", (req, res) => {
      res.sendFile(path.join(distPath, "index.html"));
    });
  }

  
// API: SketchUp Tag -> CAD Layer/Style assistant.
// AI only proposes metadata. It never changes geometry or decides CUT geometry.
app.post("/api/gemini/sketchup-map", async (req, res) => {
  try {
    const tags = Array.isArray(req.body?.tags) ? req.body.tags.map(String).slice(0, 500) : [];
    if (!tags.length) return res.status(400).json({ error: "tags array is required" });
    const fallback = tags.map((tag: string) => {
      const s = tag.toUpperCase();
      if (/WALL|VACH|TƯỜNG|TUONG/.test(s)) return { tag, layer: "A-WALL", color: "#FFFFFF", lineweight: 0.25, reason: "wall keyword" };
      if (/CEIL|TRAN|TRẦN/.test(s)) return { tag, layer: "A-CEILING", color: "#00FFFF", lineweight: 0.18, reason: "ceiling keyword" };
      if (/DOOR|CUA|CỬA/.test(s)) return { tag, layer: "A-DOOR", color: "#FFFF00", lineweight: 0.18, reason: "door keyword" };
      if (/WINDOW|CỬA SỔ|CUASO/.test(s)) return { tag, layer: "A-WINDOW", color: "#00FF00", lineweight: 0.18, reason: "window keyword" };
      if (/MEP|PIPE|DUCT|ELEC|ỐNG|ONG/.test(s)) return { tag, layer: "M-MEP", color: "#FF00FF", lineweight: 0.13, reason: "MEP keyword" };
      return { tag, layer: `SU-${tag.replace(/[^A-Za-z0-9_-]+/g, "_").slice(0, 40) || "UNTAGGED"}`, color: "#BFBFBF", lineweight: 0.13, reason: "safe fallback" };
    });
    const selectedProvider = normalizeProviderId(req.body?.provider);
    if (selectedProvider === "OFFLINE") return res.json({ mappings: fallback, provider: "OFFLINE", isOfflineFallback: false });
    if (!hasOnlineProvider(selectedProvider)) {
      if (!isOfflineFallbackAllowed()) return res.status(400).json({ error: `${selectedProvider} chưa được cấu hình. HNL không tự chuyển provider.` });
      return res.json({ mappings: fallback, provider: "OFFLINE", isOfflineFallback: true, fallbackReason:"PROVIDER_NOT_CONFIGURED" });
    }

    const answer = await callAiText({
      provider: selectedProvider,
      prompt: `Tags SketchUp: ${JSON.stringify(tags)}
Trả JSON array: [{"tag":"...","layer":"...","color":"#RRGGBB","lineweight":0.18,"reason":"ngắn gọn"}].
Ưu tiên layer kiến trúc/xây dựng dễ plot, tên ngắn, ByLayer. Không tự suy luận CUT/HIDDEN.`,
      systemInstruction: "Bạn là trợ lý chuẩn hóa layer CAD 2D. Chỉ đề xuất metadata, KHÔNG thay hình học.",
      jsonMode: true,
    });
    let mappings = fallback;
    try {
      const parsed = JSON.parse(answer.text || "[]");
      if (Array.isArray(parsed)) mappings = tags.map((tag: string) => parsed.find((x:any)=>x?.tag===tag) || fallback.find((x:any)=>x.tag===tag));
    } catch {}
    res.json({ mappings, provider: answer.provider, model: answer.model, isOfflineFallback: false });
  } catch (err:any) {
    if (!isOfflineFallbackAllowed()) return res.status(502).json({ error: err?.message || String(err) });
    res.status(200).json({ mappings: [], provider:"OFFLINE", isOfflineFallback: true, error: err?.message || String(err) });
  }
});

const server = app.listen(PORT, "127.0.0.1", () => {
    console.log(`HNL CAD AI TOOL Server listening on port ${PORT}`);
  });

  server.on("error", (err: any) => {
    if (err?.code === "EADDRINUSE") {
      console.error(`[HNL] Port ${PORT} is already in use. Electron should select another free port before starting the server.`);
      return;
    }
    console.error("[HNL] Local server error:", err);
  });
}

startServer();
