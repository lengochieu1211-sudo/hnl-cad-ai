import { CompareResult } from "./drawingCompare";
const KEY="hnl.revisions.v1";
export interface RevisionRecord{
 id:string;revision:string;createdAt:string;description:string;sourceName?:string;
 added:number;deleted:number;modified:number;unchanged:number;status:"DRAFT"|"ISSUED";
}
export function loadRevisions():RevisionRecord[]{try{const x=JSON.parse(localStorage.getItem(KEY)||"[]");return Array.isArray(x)?x:[]}catch{return[]}}
export function saveRevisionFromCompare(revision:string,description:string,sourceName:string,cmp:CompareResult){
 const item:RevisionRecord={id:`rev_${Date.now()}`,revision:revision.trim()||"REV",createdAt:new Date().toISOString(),description,sourceName,added:cmp.added.length,deleted:cmp.deleted.length,modified:cmp.modified.length,unchanged:cmp.unchanged,status:"DRAFT"};
 const all=loadRevisions();all.unshift(item);localStorage.setItem(KEY,JSON.stringify(all.slice(0,100)));return item;
}
export function setRevisionStatus(id:string,status:RevisionRecord["status"]){const all=loadRevisions().map(r=>r.id===id?{...r,status}:r);localStorage.setItem(KEY,JSON.stringify(all));return all}
