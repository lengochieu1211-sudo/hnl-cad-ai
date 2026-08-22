import { CadEntity, CadDimension } from "../types/cad";

export type ReplacePair={find:string;replace:string};
const inScope=(id:string,selectedIds:string[],scope:"ALL"|"SELECTED")=>scope==="ALL"||selectedIds.includes(id);

export function smartFindReplace(entities:CadEntity[],pairs:ReplacePair[],selectedIds:string[],scope:"ALL"|"SELECTED",caseSensitive=false){
  let changed=0;
  const apply=(text:string)=>{
    let out=text;
    for(const p of pairs){
      if(!p.find)continue;
      if(caseSensitive)out=out.split(p.find).join(p.replace);
      else out=out.replace(new RegExp(p.find.replace(/[.*+?^${}()|[\]\\]/g,"\\$&"),"gi"),p.replace);
    }
    return out;
  };
  const next=entities.map((e:any)=>{
    if(!inScope(e.id,selectedIds,scope))return e;
    if((e.type==="TEXT"||e.type==="MTEXT"||e.type==="MLEADER")&&typeof e.text==="string"){
      const text=apply(e.text);if(text!==e.text){changed++;return{...e,text};}
    }
    if(e.type==="BLOCK_REF"&&e.attributes){
      const attrs={...e.attributes};let hit=false;
      for(const k of Object.keys(attrs)){const n=apply(String(attrs[k]));if(n!==attrs[k]){attrs[k]=n;hit=true;}}
      if(hit){changed++;return{...e,attributes:attrs};}
    }
    return e;
  }) as CadEntity[];
  return{entities:next,changed};
}

export function transformTextCase(entities:CadEntity[],selectedIds:string[],mode:"UPPER"|"LOWER"|"TITLE"){
  const title=(s:string)=>s.toLowerCase().replace(/(^|\s)\S/g,m=>m.toUpperCase());
  let changed=0;
  const fn=(s:string)=>mode==="UPPER"?s.toUpperCase():mode==="LOWER"?s.toLowerCase():title(s);
  const next=entities.map((e:any)=>{
    if(!selectedIds.includes(e.id))return e;
    if((e.type==="TEXT"||e.type==="MTEXT"||e.type==="MLEADER")&&typeof e.text==="string"){changed++;return{...e,text:fn(e.text)}}
    if(e.type==="BLOCK_REF"&&e.attributes){const a:any={};for(const [k,v] of Object.entries(e.attributes))a[k]=fn(String(v));changed++;return{...e,attributes:a}}
    return e;
  }) as CadEntity[];
  return{entities:next,changed};
}

export function renumberBlockAttribute(entities:CadEntity[],selectedIds:string[],tag:string,start=1,prefix=""){
  let n=start,changed=0;
  const next=entities.map((e:any)=>{
    if(!selectedIds.includes(e.id)||e.type!=="BLOCK_REF"||!e.attributes||!(tag in e.attributes))return e;
    const attributes={...e.attributes,[tag]:`${prefix}${n++}`};changed++;return{...e,attributes};
  }) as CadEntity[];
  return{entities:next,changed};
}

export function replaceBlockNames(entities:CadEntity[],selectedIds:string[],fromName:string,toName:string){
  let changed=0;
  const next=entities.map((e:any)=>{
    if(!inScope(e.id,selectedIds,selectedIds.length?"SELECTED":"ALL")||e.type!=="BLOCK_REF"||e.blockName!==fromName)return e;
    changed++;return{...e,blockName:toName};
  }) as CadEntity[];
  return{entities:next,changed};
}

const pts=(e:any)=>{
  if(e.start&&e.end)return[e.start,e.end];
  if(e.p1&&e.p2)return[e.p1,e.p2];
  if(Array.isArray(e.points))return e.points;
  if(e.center&&Number.isFinite(e.radius))return[{x:e.center.x-e.radius,y:e.center.y-e.radius},{x:e.center.x+e.radius,y:e.center.y+e.radius}];
  if(e.position)return[e.position];
  if(Number.isFinite(e.x)&&Number.isFinite(e.y)&&Number.isFinite(e.width)&&Number.isFinite(e.height))return[{x:e.x,y:e.y},{x:e.x+e.width,y:e.y+e.height}];
  return[];
};
export function quickDimensionBounds(entities:CadEntity[],selectedIds:string[],layer="HNL-DIM"){
  const selected=entities.filter(e=>selectedIds.includes(e.id));const p=selected.flatMap(pts);
  if(!p.length)throw new Error("Chưa chọn geometry có thể lấy bounding box.");
  const minX=Math.min(...p.map(q=>q.x)),maxX=Math.max(...p.map(q=>q.x)),minY=Math.min(...p.map(q=>q.y)),maxY=Math.max(...p.map(q=>q.y));
  const pad=Math.max(250,Math.max(maxX-minX,maxY-minY)*0.05);
  const base=Date.now();
  const h:CadDimension={id:`dim_h_${base}`,handle:base.toString(16).slice(-6).toUpperCase(),type:"DIMENSION",layer,p1:{x:minX,y:minY},p2:{x:maxX,y:minY},dimLineOffset:-pad,measurement:maxX-minX,dimStyle:"HNL-QUICK"};
  const v:CadDimension={id:`dim_v_${base}`,handle:(base+1).toString(16).slice(-6).toUpperCase(),type:"DIMENSION",layer,p1:{x:minX,y:minY},p2:{x:minX,y:maxY},dimLineOffset:-pad,measurement:maxY-minY,dimStyle:"HNL-QUICK"};
  return{entities:[...entities,h,v],created:[h,v],bounds:{minX,minY,maxX,maxY}};
}

export function hnlFieldAudit(entities:CadEntity[]){
  const fields=(entities as any[]).filter(e=>(e.type==="TEXT"||e.type==="MTEXT")&&e.hasField);
  const broken=fields.filter(e=>!e.fieldFormula||!String(e.fieldFormula).trim());
  return{fieldCount:fields.length,brokenCount:broken.length,brokenIds:broken.map(e=>e.id)};
}

const dist=(a:any,b:any)=>Math.hypot(b.x-a.x,b.y-a.y);
const polyArea=(ps:any[])=>Math.abs(ps.reduce((s,p,i)=>{const q=ps[(i+1)%ps.length];return s+p.x*q.y-q.x*p.y},0))/2;
export function quantitySummary(entities:CadEntity[]){
  const rows=new Map<string,{layer:string;type:string;count:number;lengthMm:number;areaMm2:number}>();
  for(const e of entities as any[]){
    const key=`${e.layer}|${e.type}`,r=rows.get(key)||{layer:e.layer,type:e.type,count:0,lengthMm:0,areaMm2:0};r.count++;
    if(e.type==="LINE")r.lengthMm+=dist(e.start,e.end);
    else if(e.type==="POLYLINE"&&e.points?.length){for(let i=1;i<e.points.length;i++)r.lengthMm+=dist(e.points[i-1],e.points[i]);if(e.closed){r.lengthMm+=dist(e.points[e.points.length-1],e.points[0]);r.areaMm2+=polyArea(e.points)}}
    else if(e.type==="RECTANGLE"){r.lengthMm+=2*(Math.abs(e.width)+Math.abs(e.height));r.areaMm2+=Math.abs(e.width*e.height)}
    else if(e.type==="CIRCLE"){r.lengthMm+=2*Math.PI*Math.abs(e.radius);r.areaMm2+=Math.PI*e.radius*e.radius}
    rows.set(key,r);
  }
  return[...rows.values()].sort((a,b)=>a.layer.localeCompare(b.layer)||a.type.localeCompare(b.type));
}
