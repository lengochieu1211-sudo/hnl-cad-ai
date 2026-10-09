import { CadEntity, CadLayer } from "../types/cad";

export interface SuPoint3 { x:number;y:number;z:number }
export interface SuEdgeRecord {
  id:string; p1:SuPoint3; p2:SuPoint3; tag:string; hidden?:boolean; soft?:boolean; smooth?:boolean;
  sourceClass?:string; linkId?:string; sectionCrossing?:boolean;
}
export interface SuScenePackage {
  schema:"hnl-sketchup-scene"; version:number; generatedAt:string; modelName?:string; sceneName?:string;
  units:"mm"; camera:{eye:SuPoint3;target:SuPoint3;up:SuPoint3;perspective:boolean;fov?:number};
  sectionPlane?:{name?:string;plane:[number,number,number,number]}|null;
  edges:SuEdgeRecord[]; tags:Array<{name:string;visible:boolean;color?:string}>;
}
export interface ProjectionPreset {
  name:string; cutLayer:string; visibleLayer:string; hiddenLayer:string; silhouetteLayer:string;
  hiddenLinetype:string; visibleLineweight:number; hiddenLineweight:number; cutLineweight:number;
}
export interface ProjectionReport {
  sourceEdges:number; outputLines:number; hidden:number; sectionCrossings:number; skipped:number;
  warnings:string[];
}
const dot=(a:SuPoint3,b:SuPoint3)=>a.x*b.x+a.y*b.y+a.z*b.z;
const sub=(a:SuPoint3,b:SuPoint3)=>({x:a.x-b.x,y:a.y-b.y,z:a.z-b.z});
const cross=(a:SuPoint3,b:SuPoint3)=>({x:a.y*b.z-a.z*b.y,y:a.z*b.x-a.x*b.z,z:a.x*b.y-a.y*b.x});
const norm=(a:SuPoint3)=>{const l=Math.hypot(a.x,a.y,a.z)||1;return{x:a.x/l,y:a.y/l,z:a.z/l};};

export const DEFAULT_PROJECTION_PRESET:ProjectionPreset={
  name:"HNL Shopdrawing",cutLayer:"HNL-SU-CUT",visibleLayer:"HNL-SU-VISIBLE",hiddenLayer:"HNL-SU-HIDDEN",silhouetteLayer:"HNL-SU-SILHOUETTE",
  hiddenLinetype:"HIDDEN",visibleLineweight:0.18,hiddenLineweight:0.09,cutLineweight:0.35
};

export function validateSuScenePackage(data:any):string[]{
  const e:string[]=[];
  if(data?.schema!=="hnl-sketchup-scene")e.push("Schema không phải hnl-sketchup-scene.");
  if(!Array.isArray(data?.edges))e.push("Thiếu edges.");
  if(!data?.camera?.eye||!data?.camera?.target||!data?.camera?.up)e.push("Thiếu camera.");
  return e;
}
export function projectSketchUpSceneTo2D(data:SuScenePackage,preset=DEFAULT_PROJECTION_PRESET):{entities:CadEntity[];layers:CadLayer[];report:ProjectionReport}{
  const errs=validateSuScenePackage(data); if(errs.length)throw new Error(errs.join(" "));
  const view=norm(sub(data.camera.target,data.camera.eye)); let right=norm(cross(view,data.camera.up)); let up=norm(cross(right,view));
  if(Math.hypot(right.x,right.y,right.z)<1e-8){right={x:1,y:0,z:0};up={x:0,y:1,z:0};}
  const warnings:string[]=[]; if(data.camera.perspective)warnings.push("Scene đang Perspective; HNL dùng phép chiếu camera-basis 2D để tạo linework. Nên dùng Parallel Projection cho bản vẽ kỹ thuật.");
  let hidden=0,crossing=0,skipped=0;
  const entities:CadEntity[]=[];
  for(const ed of data.edges){
    if(!ed?.p1||!ed?.p2){skipped++;continue;}
    const a=sub(ed.p1,data.camera.target), b=sub(ed.p2,data.camera.target);
    const p1={x:dot(a,right),y:dot(a,up)},p2={x:dot(b,right),y:dot(b,up)};
    if(![p1.x,p1.y,p2.x,p2.y].every(Number.isFinite)){skipped++;continue;}
    const isHidden=!!ed.hidden||ed.sourceClass==="HIDDEN"; if(isHidden)hidden++;
    if(ed.sectionCrossing)crossing++;
    const layer=isHidden?preset.hiddenLayer:preset.visibleLayer;
    entities.push({id:`su_${ed.id}`,handle:String(ed.id).slice(-8).toUpperCase(),type:"LINE",layer,color:isHidden?"#777777":"#FFFFFF",
      lineweight:isHidden?preset.hiddenLineweight:preset.visibleLineweight,start:p1,end:p2, hnlLinkId:ed.linkId||ed.id, sourceTag:ed.tag, sourceClass:ed.sourceClass||"VISIBLE"} as any);
  }
  const layers:CadLayer[]=[
    {name:preset.visibleLayer,color:"#FFFFFF",lineweight:preset.visibleLineweight,linetype:"CONTINUOUS",isLocked:false,isVisible:true,isPlottable:true},
    {name:preset.hiddenLayer,color:"#777777",lineweight:preset.hiddenLineweight,linetype:preset.hiddenLinetype,isLocked:false,isVisible:true,isPlottable:true},
    {name:preset.cutLayer,color:"#FF4040",lineweight:preset.cutLineweight,linetype:"CONTINUOUS",isLocked:false,isVisible:true,isPlottable:true},
    {name:preset.silhouetteLayer,color:"#00FFFF",lineweight:0.25,linetype:"CONTINUOUS",isLocked:false,isVisible:true,isPlottable:true},
  ];
  if(crossing)warnings.push(`${crossing} cạnh cắt qua Section Plane được đánh dấu metadata; chưa coi đây là đường CUT contour thật.`);
  return {entities,layers,report:{sourceEdges:data.edges.length,outputLines:entities.length,hidden,sectionCrossings:crossing,skipped,warnings}};
}

export function mapSuTagsRuleBased(tags:string[]){
 const classify=(t:string)=>{const s=t.toUpperCase();
  if(/WALL|VACH|TƯỜNG|TUONG/.test(s))return{layer:"A-WALL",color:"#FFFFFF",lineweight:0.25};
  if(/CEIL|TRAN|TRẦN/.test(s))return{layer:"A-CEILING",color:"#00FFFF",lineweight:0.18};
  if(/DOOR|CUA|CỬA/.test(s))return{layer:"A-DOOR",color:"#FFFF00",lineweight:0.18};
  if(/WINDOW|CỬA SỔ|CUASO/.test(s))return{layer:"A-WINDOW",color:"#00FF00",lineweight:0.18};
  if(/MEP|PIPE|DUCT|ELEC|ỐNG|ONG/.test(s))return{layer:"M-MEP",color:"#FF00FF",lineweight:0.13};
  if(/FURN|NOI THAT|NỘI THẤT/.test(s))return{layer:"A-FURN",color:"#888888",lineweight:0.09};
  return{layer:`SU-${t.replace(/[^A-Za-z0-9_-]+/g,"_").slice(0,40)||"UNTAGGED"}`,color:"#BFBFBF",lineweight:0.13};};
 return tags.map(tag=>({tag,...classify(tag)}));
}

export function diffLinkIds(existing:CadEntity[],incoming:CadEntity[]){
 const a=new Map(existing.map((e:any)=>[e.hnlLinkId||e.id,e]));const b=new Map(incoming.map((e:any)=>[e.hnlLinkId||e.id,e]));
 let added=0,updated=0,deleted=0,unchanged=0;
 for(const [id,e] of b){const old:any=a.get(id);if(!old){added++;continue;} const sig=(x:any)=>JSON.stringify([x.type,x.layer,x.start,x.end,x.points,x.center,x.radius]); sig(old)===sig(e)?unchanged++:updated++;}
 for(const id of a.keys())if(!b.has(id))deleted++;
 return{added,updated,deleted,unchanged};
}


export function cadLinesToDxf(entities:CadEntity[], layers:CadLayer[], title="HNL SketchUp 2D"){
  const esc=(x:string)=>String(x||"0").replace(/[\r\n]/g," ");
  const aci=(hex?:string)=>{const h=(hex||"").toUpperCase();if(h==="#FF0000")return 1;if(h==="#FFFF00")return 2;if(h==="#00FF00")return 3;if(h==="#00FFFF")return 4;if(h==="#0000FF")return 5;if(h==="#FF00FF")return 6;if(h==="#FFFFFF")return 7;return 8;};
  const out:string[]=["0","SECTION","2","HEADER","9","$ACADVER","1","AC1015","9","$INSUNITS","70","4","0","ENDSEC","0","SECTION","2","TABLES","0","TABLE","2","LAYER","70",String(layers.length)];
  for(const l of layers){out.push("0","LAYER","2",esc(l.name),"70","0","62",String(aci(l.color)),"6",esc(l.linetype||"CONTINUOUS"));}
  out.push("0","ENDTAB","0","ENDSEC","0","SECTION","2","ENTITIES");
  for(const e of entities as any[]){
    if(e.type==="LINE")out.push("0","LINE","8",esc(e.layer),"10",String(e.start.x),"20",String(e.start.y),"30","0","11",String(e.end.x),"21",String(e.end.y),"31","0");
    else if(e.type==="POLYLINE"&&Array.isArray(e.points)){
      out.push("0","LWPOLYLINE","8",esc(e.layer),"90",String(e.points.length),"70",e.closed?"1":"0");
      for(const q of e.points)out.push("10",String(q.x),"20",String(q.y));
    } else if(e.type==="CIRCLE")out.push("0","CIRCLE","8",esc(e.layer),"10",String(e.center.x),"20",String(e.center.y),"30","0","40",String(e.radius));
  }
  out.push("0","ENDSEC","0","EOF");
  return out.join("\r\n");
}
