import { CadEntity, CadLayer } from "../types/cad";

export type DoctorSeverity="INFO"|"WARNING"|"ERROR";
export interface DoctorIssue {id:string;code:string;severity:DoctorSeverity;title:string;message:string;entityIds:string[];fixable:boolean;}
export interface DoctorReport {issues:DoctorIssue[];summary:{errors:number;warnings:number;info:number;entities:number};}

const d=(a:any,b:any)=>Math.hypot((a?.x||0)-(b?.x||0),(a?.y||0)-(b?.y||0));
const segIntersect=(a:any,b:any,c:any,d0:any)=>{
  const ccw=(p:any,q:any,r:any)=>(r.y-p.y)*(q.x-p.x)>(q.y-p.y)*(r.x-p.x);
  return ccw(a,c,d0)!==ccw(b,c,d0)&&ccw(a,b,c)!==ccw(a,b,d0);
};
export function inspectGeometry(entities:CadEntity[],layers:CadLayer[],tol=0.1):DoctorReport{
  const issues:DoctorIssue[]=[]; const layerNames=new Set(layers.map(l=>l.name));
  const push=(code:string,severity:DoctorSeverity,title:string,message:string,ids:string[],fixable=false)=>issues.push({id:`${code}_${issues.length}`,code,severity,title,message,entityIds:ids,fixable});
  for(const e of entities as any[]){
    if(!layerNames.has(e.layer))push("HNL-GEO-LAYER","WARNING","Layer không tồn tại",`${e.type} ${e.id} tham chiếu layer '${e.layer}'.`,[e.id],true);
    if(e.type==="LINE"&&d(e.start,e.end)<=tol)push("HNL-GEO-ZERO","ERROR","Zero-length line",`Line dài ≤ ${tol} mm.`,[e.id],true);
    if(e.type==="POLYLINE"&&Array.isArray(e.points)){
      for(let i=1;i<e.points.length;i++)if(d(e.points[i-1],e.points[i])<=tol)push("HNL-GEO-DUPVERT","WARNING","Vertex trùng",`Polyline có vertex liên tiếp cách nhau ≤ ${tol} mm.`,[e.id],true);
      if(e.closed&&e.points.length>=4){
        outer:for(let i=0;i<e.points.length;i++){const a=e.points[i],b=e.points[(i+1)%e.points.length];for(let j=i+2;j<e.points.length;j++){if((j+1)%e.points.length===i)continue;const c=e.points[j],dd=e.points[(j+1)%e.points.length];if(segIntersect(a,b,c,dd)){push("HNL-GEO-SELF","ERROR","Polyline tự cắt",`Polyline ${e.id} có segment tự giao nhau.`,[e.id],false);break outer;}}}
      }
    }
    const pts:any[]=[]; if(e.start)pts.push(e.start,e.end);if(e.points)pts.push(...e.points);if(e.center)pts.push(e.center);if(e.position)pts.push(e.position);
    if(pts.some(p=>Math.abs(p.x)>1e8||Math.abs(p.y)>1e8))push("HNL-GEO-FAR","WARNING","Đối tượng quá xa gốc tọa độ","Tọa độ rất lớn có thể gây lỗi zoom/precision.",[e.id],false);
    if(e.type==="BLOCK_REF"&&e.scale&&((e.scale.x??1)<0||(e.scale.y??1)<0))push("HNL-GEO-NEG_SCALE","WARNING","Block scale âm","Block có mirror/negative scale, cần kiểm tra trước khi chuyển đổi.",[e.id],false);
  }
  const sig=new Map<string,string>();
  for(const e of entities as any[]){let k="";if(e.type==="LINE"){const a=`${e.start.x.toFixed(3)},${e.start.y.toFixed(3)}`,b=`${e.end.x.toFixed(3)},${e.end.y.toFixed(3)}`;k=`L:${e.layer}:${a<b?a+"|"+b:b+"|"+a}`;}if(k){if(sig.has(k))push("HNL-GEO-DUP","WARNING","Geometry trùng",`Trùng với ${sig.get(k)}.`,[sig.get(k)!,e.id],true);else sig.set(k,e.id);}}
  return {issues,summary:{errors:issues.filter(i=>i.severity==="ERROR").length,warnings:issues.filter(i=>i.severity==="WARNING").length,info:issues.filter(i=>i.severity==="INFO").length,entities:entities.length}};
}
export function applySafeDoctorFixes(entities:CadEntity[],layers:CadLayer[],report:DoctorReport,tol=0.1){
  const badZero=new Set(report.issues.filter(i=>i.code==="HNL-GEO-ZERO").flatMap(i=>i.entityIds));
  const dupRemove=new Set(report.issues.filter(i=>i.code==="HNL-GEO-DUP").flatMap(i=>i.entityIds.slice(1)));
  const valid=new Set(layers.map(l=>l.name));
  return entities.filter(e=>!badZero.has(e.id)&&!dupRemove.has(e.id)).map((e:any)=>{
    let x={...e}; if(!valid.has(x.layer))x.layer="0";
    if(x.type==="POLYLINE"&&Array.isArray(x.points)){const pts:any[]=[];for(const p of x.points){if(!pts.length||d(pts[pts.length-1],p)>tol)pts.push(p);}x={...x,points:pts};}
    return x;
  }) as CadEntity[];
}
