import { HnlSheetSet } from "./plotPublishEngine";
const KEY="hnl.sheetsets.v1";
export function loadSheetSets():HnlSheetSet[]{try{const x=JSON.parse(localStorage.getItem(KEY)||"[]");return Array.isArray(x)?x:[]}catch{return[]}}
export function saveSheetSet(set:HnlSheetSet){const all=loadSheetSets();const idx=all.findIndex(x=>x.name===set.name);if(idx>=0)all[idx]=set;else all.unshift(set);localStorage.setItem(KEY,JSON.stringify(all.slice(0,30)));return set}
export function parseHnlSheetSet(text:string):HnlSheetSet{const x=JSON.parse(text);if(x?.schema!=="hnl-sheet-set"||!Array.isArray(x.sheets))throw new Error("Không phải HNL Sheet Set JSON hợp lệ.");return x}
