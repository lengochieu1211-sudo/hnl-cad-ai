export type AiProviderId =
  | "OFFLINE"
  | "GEMINI"
  | "OPENAI"
  | "CLAUDE"
  | "GROK"
  | "GROQ"
  | "OPENROUTER"
  | "MISTRAL"
  | "HUGGINGFACE"
  | "OLLAMA"
  | "CUSTOM_OPENAI";

export type AiProviderEntry = {
  id: AiProviderId;
  name: string;
  shortName: string;
  kind: "offline" | "online" | "local";
  description: string;
  defaultModel: string;
  modelSuggestions: string[];
  defaultBaseUrl: string;
  needsKey: boolean;
  freeTier?: boolean;
};

export const AI_PROVIDERS: AiProviderEntry[] = [
  { id:"OFFLINE", name:"HNL Nội bộ", shortName:"Offline", kind:"offline", description:"Rule engine cục bộ. Không Internet, không API key.", defaultModel:"hnl-rules-v1", modelSuggestions:["hnl-rules-v1"], defaultBaseUrl:"", needsKey:false, freeTier:true },
  { id:"GEMINI", name:"Google Gemini", shortName:"Gemini", kind:"online", description:"Gemini Developer API. Có free tier tùy tài khoản/quota.", defaultModel:"gemini-3.7-flash", modelSuggestions:["gemini-3.7-flash","gemini-3.1-flash-lite"], defaultBaseUrl:"https://generativelanguage.googleapis.com", needsKey:true, freeTier:true },
  { id:"OPENAI", name:"ChatGPT / OpenAI", shortName:"OpenAI", kind:"online", description:"OpenAI Responses API.", defaultModel:"gpt-5.6", modelSuggestions:["gpt-5.6","gpt-5-mini"], defaultBaseUrl:"https://api.openai.com/v1", needsKey:true },
  { id:"CLAUDE", name:"Claude / Anthropic", shortName:"Claude", kind:"online", description:"Anthropic Messages API.", defaultModel:"claude-sonnet-4-20250514", modelSuggestions:["claude-sonnet-4-20250514"], defaultBaseUrl:"https://api.anthropic.com/v1", needsKey:true },
  { id:"GROK", name:"Grok / xAI", shortName:"Grok", kind:"online", description:"xAI Responses API.", defaultModel:"grok-4.6", modelSuggestions:["grok-4.6"], defaultBaseUrl:"https://api.x.ai/v1", needsKey:true },
  { id:"GROQ", name:"GroqCloud", shortName:"Groq", kind:"online", description:"OpenAI-compatible, tốc độ cao. Có Free/Developer quota tùy tài khoản.", defaultModel:"openai/gpt-oss-120b", modelSuggestions:["openai/gpt-oss-120b","openai/gpt-oss-20b","llama-3.3-70b-versatile","llama-3.1-8b-instant"], defaultBaseUrl:"https://api.groq.com/openai/v1", needsKey:true, freeTier:true },
  { id:"OPENROUTER", name:"OpenRouter", shortName:"OpenRouter", kind:"online", description:"OpenAI-compatible router; hỗ trợ nhiều model :free.", defaultModel:"openai/gpt-oss-20b:free", modelSuggestions:["openai/gpt-oss-20b:free","openai/gpt-oss-120b:free"], defaultBaseUrl:"https://openrouter.ai/api/v1", needsKey:true, freeTier:true },
  { id:"MISTRAL", name:"Mistral AI", shortName:"Mistral", kind:"online", description:"Mistral API. Free mode cho thử nghiệm/prototyping, quota giới hạn.", defaultModel:"mistral-small-latest", modelSuggestions:["mistral-small-latest","mistral-medium-latest"], defaultBaseUrl:"https://api.mistral.ai/v1", needsKey:true, freeTier:true },
  { id:"HUGGINGFACE", name:"Hugging Face Inference", shortName:"HF", kind:"online", description:"Inference Providers qua OpenAI-compatible router; có free inference credits.", defaultModel:"openai/gpt-oss-120b:fastest", modelSuggestions:["openai/gpt-oss-120b:fastest","openai/gpt-oss-20b:fastest","deepseek-ai/DeepSeek-R1:fastest"], defaultBaseUrl:"https://router.huggingface.co/v1", needsKey:true, freeTier:true },
  { id:"OLLAMA", name:"Ollama Local", shortName:"Ollama", kind:"local", description:"LLM chạy trên PC qua Ollama; miễn phí/offline.", defaultModel:"gemma3", modelSuggestions:["gemma3","qwen3","llama3.2"], defaultBaseUrl:"http://127.0.0.1:11434", needsKey:false, freeTier:true },
  { id:"CUSTOM_OPENAI", name:"Custom OpenAI-compatible", shortName:"Custom", kind:"local", description:"LM Studio, LiteLLM hoặc endpoint OpenAI-compatible khác.", defaultModel:"gpt-4o-mini", modelSuggestions:["gpt-4o-mini"], defaultBaseUrl:"http://127.0.0.1:1234/v1", needsKey:false },
];

export const AI_PROVIDER_MAP = Object.fromEntries(AI_PROVIDERS.map((p)=>[p.id,p])) as Record<AiProviderId,AiProviderEntry>;

export const DEFAULT_AI_PROVIDER_CONFIG:any = {
  activeProvider:"GEMINI",
  autoFallbackOffline:false,
  contextOnly:true,
  previewBeforeExecute:true,
  providers:Object.fromEntries(AI_PROVIDERS.map((p)=>[p.id,{model:p.defaultModel,baseUrl:p.defaultBaseUrl}])),
  configured:{OFFLINE:true,OLLAMA:true},
};
