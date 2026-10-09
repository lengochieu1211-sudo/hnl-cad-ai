import { inspectGeometry } from "./geometryDoctor";
import { compareDrawings } from "./drawingCompare";
import { cleanup2D } from "./sketchUp2DBridge";
import { CadEntity,CadLayer } from "../types/cad";
export interface SelfTestResult{name:string;status:"PASS"|"FAIL";detail:string;durationMs:number;}
export function runCoreSelfTests():SelfTestResult[]{
 const tests:Array<[string,()=>string]>=[
  ["Geometry Doctor detects zero line",()=>{const l:any={id:"z",handle:"1",type:"LINE",layer:"0",start:{x:0,y:0},end:{x:0,y:0}};const layers:any=[{name:"0",color:"#fff",lineweight:0.1,linetype:"CONTINUOUS",isLocked:false,isVisible:true,isPlottable:true}];const r=inspectGeometry([l],layers);if(!r.issues.some(i=>i.code==="HNL-GEO-ZERO"))throw new Error("zero line not detected");return "HNL-GEO-ZERO detected";}],
  ["Drawing Compare detects modified",()=>{const a:any=[{id:"1",handle:"A",type:"LINE",layer:"0",start:{x:0,y:0},end:{x:1,y:0}}],b:any=[{...a[0],end:{x:2,y:0}}];const r=compareDrawings(a,b);if(r.modified.length!==1)throw new Error("modified count != 1");return "1 modified";}],
  ["Cleanup removes duplicate",()=>{const e:any={id:"1",handle:"A",type:"LINE",layer:"0",start:{x:0,y:0},end:{x:10,y:0}};const e2:any={...e,id:"2",handle:"B"};const layers:any=[{name:"0",color:"#fff",lineweight:0.1,linetype:"CONTINUOUS",isLocked:false,isVisible:true,isPlottable:true}];const r=cleanup2D([e,e2],layers,{toleranceMm:.1,removeDuplicates:true,removeTinySegments:false,tinySegmentMm:.5,joinCollinearLines:false,flatten2D:true,normalizeLayers:true,removeNonPlotObjects:false});if(r.entities.length!==1)throw new Error("duplicate not removed");return "duplicate removed";}],
 ];
 return tests.map(([name,fn])=>{const t=performance.now();try{return{name,status:"PASS" as const,detail:fn(),durationMs:Math.round(performance.now()-t)}}catch(e:any){return{name,status:"FAIL" as const,detail:e?.message||String(e),durationMs:Math.round(performance.now()-t)}}});
}
