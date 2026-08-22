import { CadEntity } from "../types/cad";

export const HNL_CLIPBOARD_SCHEMA = "hnl-cad-entity-clipboard-v1";

const offsetPoint=(p:any,dx:number,dy:number)=>p&&Number.isFinite(p.x)&&Number.isFinite(p.y)?{...p,x:p.x+dx,y:p.y+dy}:p;
const freshId=(prefix="ent")=>`${prefix}_${Date.now()}_${Math.random().toString(36).slice(2,8)}`;
const freshHandle=()=>Math.random().toString(16).slice(2,8).toUpperCase();

export function cloneEntityForPaste(entity:CadEntity,dx=250,dy=-250):CadEntity{
  const e:any=structuredClone(entity as any);
  e.id=freshId(String(e.type||"ent").toLowerCase());
  e.handle=freshHandle();
  for(const key of ["p1","p2","start","end","center","position","textPosition","insertPoint","basePoint"]){
    if(e[key]) e[key]=offsetPoint(e[key],dx,dy);
  }
  for(const key of ["points","boundary","leaderPoints"]){
    if(Array.isArray(e[key])) e[key]=e[key].map((p:any)=>offsetPoint(p,dx,dy));
  }
  if(Number.isFinite(e.x)) e.x+=dx;
  if(Number.isFinite(e.y)) e.y+=dy;
  // Preserve HNL link lineage but never duplicate a persistent external link id.
  if("hnlLinkId" in e) delete e.hnlLinkId;
  if("persistentId" in e) delete e.persistentId;
  return e as CadEntity;
}

export function serializeEntityClipboard(entities:CadEntity[]){
  return JSON.stringify({schema:HNL_CLIPBOARD_SCHEMA,version:1,copiedAt:new Date().toISOString(),entities},null,2);
}

export function parseEntityClipboard(text:string):CadEntity[]|null{
  try{
    const x=JSON.parse(text);
    if(x?.schema!==HNL_CLIPBOARD_SCHEMA||!Array.isArray(x.entities))return null;
    return x.entities as CadEntity[];
  }catch{return null}
}
