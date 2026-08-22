export type HealthStatus="PASS"|"FAIL"|"PARTIAL"|"AUTOCAD_REQUIRED"|"SKETCHUP_REQUIRED"|"NOT_TESTED";
export interface CommandHealth{command:string;status:HealthStatus;runs:number;failures:number;lastDurationMs?:number;lastError?:string;lastRunAt?:string;}
const KEY="hnl.command-health.v1";
export function loadCommandHealth():Record<string,CommandHealth>{try{return JSON.parse(localStorage.getItem(KEY)||"{}")}catch{return{}}}
export function saveCommandHealth(x:Record<string,CommandHealth>){try{localStorage.setItem(KEY,JSON.stringify(x))}catch{}}
export function markCommandHealth(command:string,ok:boolean,durationMs:number,error?:string,status?:HealthStatus){
 const all=loadCommandHealth();const prev=all[command]||{command,status:"NOT_TESTED",runs:0,failures:0};
 all[command]={...prev,status:status||(ok?"PASS":"FAIL"),runs:prev.runs+1,failures:prev.failures+(ok?0:1),lastDurationMs:durationMs,lastError:error,lastRunAt:new Date().toISOString()};saveCommandHealth(all);return all[command];
}
