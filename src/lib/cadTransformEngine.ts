import { CadEntity } from "../types/cad";

type P={x:number;y:number};
const pt=(p:any)=>p&&Number.isFinite(p.x)&&Number.isFinite(p.y);
const mapPoint=(p:P,fn:(p:P)=>P)=>fn({x:p.x,y:p.y});

export function entityPoints(e:CadEntity):P[]{
  const x:any=e, out:P[]=[];
  for(const k of ["p1","p2","start","end","center","position","textPosition","insertPoint","basePoint"]) if(pt(x[k])) out.push(x[k]);
  for(const k of ["points","boundary","leaderPoints"]) if(Array.isArray(x[k])) out.push(...x[k].filter(pt));
  if(Number.isFinite(x.x)&&Number.isFinite(x.y))out.push({x:x.x,y:x.y});
  return out;
}

export function selectionCenter(entities:CadEntity[],ids:string[]):P{
  const ps=entities.filter(e=>ids.includes(e.id)).flatMap(entityPoints);
  if(!ps.length)return{x:0,y:0};
  return{x:(Math.min(...ps.map(p=>p.x))+Math.max(...ps.map(p=>p.x)))/2,y:(Math.min(...ps.map(p=>p.y))+Math.max(...ps.map(p=>p.y)))/2};
}

export function transformEntity(entity:CadEntity,fn:(p:P)=>P,scaleRadius=1,rotationDelta=0):CadEntity{
  const e:any=structuredClone(entity as any);
  for(const k of ["p1","p2","start","end","center","position","textPosition","insertPoint","basePoint"]) if(pt(e[k])) e[k]=mapPoint(e[k],fn);
  for(const k of ["points","boundary","leaderPoints"]) if(Array.isArray(e[k])) e[k]=e[k].map((p:any)=>pt(p)?mapPoint(p,fn):p);
  if(Number.isFinite(e.x)&&Number.isFinite(e.y)){const p=fn({x:e.x,y:e.y});e.x=p.x;e.y=p.y;}
  if(Number.isFinite(e.radius))e.radius=Math.abs(e.radius*scaleRadius);
  if(Number.isFinite(e.width))e.width*=Math.abs(scaleRadius);
  if(Number.isFinite(e.height))e.height*=Math.abs(scaleRadius);
  if(Number.isFinite(e.thickness))e.thickness*=Math.abs(scaleRadius);
  if(Number.isFinite(e.rotationDeg))e.rotationDeg=(e.rotationDeg+rotationDelta)%360;
  return e as CadEntity;
}

export const translateSelected=(entities:CadEntity[],ids:string[],dx:number,dy:number)=>
  entities.map(e=>ids.includes(e.id)?transformEntity(e,p=>({x:p.x+dx,y:p.y+dy})):e);

export function rotateSelected(entities:CadEntity[],ids:string[],degrees:number){
  const c=selectionCenter(entities,ids),a=degrees*Math.PI/180,cs=Math.cos(a),sn=Math.sin(a);
  return entities.map(e=>ids.includes(e.id)?transformEntity(e,p=>{const x=p.x-c.x,y=p.y-c.y;return{x:c.x+x*cs-y*sn,y:c.y+x*sn+y*cs}},1,degrees):e);
}

export function scaleSelected(entities:CadEntity[],ids:string[],factor:number){
  const c=selectionCenter(entities,ids);
  return entities.map(e=>ids.includes(e.id)?transformEntity(e,p=>({x:c.x+(p.x-c.x)*factor,y:c.y+(p.y-c.y)*factor}),factor):e);
}

export function mirrorSelected(entities:CadEntity[],ids:string[],axis:"X"|"Y"){
  const c=selectionCenter(entities,ids);
  return entities.map(e=>ids.includes(e.id)?transformEntity(e,p=>axis==="X"?{x:p.x,y:2*c.y-p.y}:{x:2*c.x-p.x,y:p.y}):e);
}
