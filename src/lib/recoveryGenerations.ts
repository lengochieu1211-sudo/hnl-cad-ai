const KEY="hnl.recovery.generations.v1"; const MAX=5;
export interface RecoveryGeneration{savedAt:string;label:string;snapshot:any}
export function saveRecoveryGeneration(snapshot:any,label="AutoSave"){try{const all=loadRecoveryGenerations();all.unshift({savedAt:new Date().toISOString(),label,snapshot});localStorage.setItem(KEY,JSON.stringify(all.slice(0,MAX)));return all[0].savedAt}catch{return null}}
export function loadRecoveryGenerations():RecoveryGeneration[]{try{const a=JSON.parse(localStorage.getItem(KEY)||"[]");return Array.isArray(a)?a:[]}catch{return[]}}
export function clearRecoveryGenerations(){localStorage.removeItem(KEY)}
