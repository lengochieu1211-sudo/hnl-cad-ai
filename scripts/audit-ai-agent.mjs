import fs from "node:fs";
const read=(p)=>fs.readFileSync(p,"utf8").replace(/\r\n?/g,"\n");
const checks=[]; const check=(name,ok,detail="")=>checks.push({name,ok:Boolean(ok),detail});
const server=read("server.ts");
const palette=read("src/components/Palette/HnlPalette.tsx");
const provider=read("src/lib/aiProviderCatalog.ts");
const agent=read("src/lib/aiCadAgent.ts");
const app=read("src/App.tsx");
const electron=read("electron/main.cjs");
const workflow=read(".github/workflows/build-windows.yml");
const buildAutoCad=read("scripts/build-autocad-bundle.mjs");

for(const id of ["GEMINI","OPENAI","CLAUDE","GROK","GROQ","OPENROUTER","MISTRAL","HUGGINGFACE","OLLAMA","CUSTOM_OPENAI"]){
  check(`PROVIDER_${id}`, provider.includes(`id:\"${id}\"`) && server.includes(`${id}:`) && electron.includes(`'${id}'`));
}
check("FREE_PROVIDER_GROQ",provider.includes('https://api.groq.com/openai/v1')&&server.includes('GROQ_API_KEY'));
check("FREE_PROVIDER_OPENROUTER",provider.includes('https://openrouter.ai/api/v1')&&server.includes('OPENROUTER_API_KEY'));
check("FREE_PROVIDER_MISTRAL",provider.includes('https://api.mistral.ai/v1')&&server.includes('MISTRAL_API_KEY'));
check("FREE_PROVIDER_HF",provider.includes('https://router.huggingface.co/v1')&&server.includes('HF_TOKEN'));
check("PALETTE_PROVIDER_MODEL",palette.includes('activeProvider')&&palette.includes('activeModel')&&palette.includes('hnl-ai-models-'));
check("PALETTE_THREE_MODES",palette.includes('"ASK" | "PREVIEW" | "AGENT"')&&palette.includes('🤖 Agent CAD')&&palette.includes('👁 Preview'));
check("REQUEST_PROVIDER_MODEL",palette.includes('provider: activeProvider, model: activeModel, mode: aiMode')&&server.includes('model: req.body?.model'));
check("ASK_ENDPOINT",server.includes('app.post("/api/ai/chat"')&&palette.includes('fetch("/api/ai/chat"'));
check("PLAN_MODEL_OVERRIDE",server.includes('const { prompt, cadContext, provider, model }')&&server.includes('model,\n        systemInstruction: CAD_PLAN_SYSTEM_INSTRUCTION'));
check("AGENT_WHITELIST",agent.includes('AI_NATIVE_CREATE_TYPES')&&agent.includes('validateAiPlanForExecution')&&agent.includes('entitiesToDelete'));
check("NO_RAW_COMMAND_AGENT",!agent.includes('EXECUTE_COMMAND')&&app.includes('AI never gets raw EXECUTE_COMMAND access here'));
check("NATIVE_CREATE_GATE",app.includes('executeAutoCadAction("CREATE_NATIVE_ENTITY"')&&app.includes('AI Agent rollback'));
check("DESTRUCTIVE_BLOCK",agent.includes('plan.isDestructive')&&agent.includes('Agent Safe Mode chặn'));
check("PREVIEW_NO_EXECUTE",palette.includes('allowExecute: aiMode === "AGENT"')&&palette.includes('Preview only'));
check("SAFE_KEY_STORAGE",electron.includes('safeStorage.encryptString')&&electron.includes('secrets.OPENROUTER')&&electron.includes('secrets.HUGGINGFACE'));
check("BRIDGE_GATE_RETAINED",workflow.includes('npm run audit:bridge')&&(workflow.includes('CS8604')||buildAutoCad.includes('CS8604')));

const failed=checks.filter(x=>!x.ok);
for(const c of checks) console.log(`${c.ok?"PASS":"FAIL"} ${c.name}${c.detail?` — ${c.detail}`:""}`);
console.log(`HNL AI CAD AGENT STATIC: ${checks.length-failed.length}/${checks.length} PASS`);
if(failed.length) process.exit(1);
