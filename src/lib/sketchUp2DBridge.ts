import { CadEntity, CadLayer, Point2D } from "../types/cad";

export type CleanupSeverity = "INFO" | "WARNING" | "ERROR";
export interface CleanupOptions {
  toleranceMm: number;
  removeDuplicates: boolean;
  removeTinySegments: boolean;
  tinySegmentMm: number;
  joinCollinearLines: boolean;
  flatten2D: boolean;
  normalizeLayers: boolean;
  removeNonPlotObjects: boolean;
}
export interface CleanupIssue {
  code: string; severity: CleanupSeverity; message: string; entityIds?: string[];
}
export interface CleanupReport {
  beforeCount: number; afterCount: number; duplicateRemoved: number; tinyRemoved: number;
  joinedLines: number; normalizedLayers: number; issues: CleanupIssue[];
}
export interface SketchUpExportOptions {
  scope: "ALL" | "SELECTED";
  units: "mm" | "cm" | "m";
  layerToTag: boolean;
  preserveColors: boolean;
  blocksToComponents: boolean;
  origin: "DRAWING_ORIGIN" | "PICK_POINT";
  basePoint?: Point2D;
}
export interface SketchUpBridgePackage {
  schema: "hnl-sketchup-2d-bridge";
  version: 1;
  generatedAt: string;
  units: "mm" | "cm" | "m";
  origin: Point2D;
  tags: Array<{name:string;color?:string;visible:boolean}>;
  geometry: Array<Record<string, unknown>>;
  warnings: string[];
}

const roundTol=(v:number,t:number)=>Math.round(v/Math.max(t,1e-9))*Math.max(t,1e-9);
const pkey=(p:Point2D,t:number)=>`${roundTol(p.x,t)},${roundTol(p.y,t)}`;
const lineKey=(e:any,t:number)=>{
  const a=pkey(e.start,t), b=pkey(e.end,t); return a<b?`${a}|${b}`:`${b}|${a}`;
};
const entityKey=(e:any,t:number)=>{
  if(e.type==="LINE") return `LINE:${e.layer}:${lineKey(e,t)}`;
  if(e.type==="CIRCLE") return `CIRCLE:${e.layer}:${pkey(e.center,t)}:${roundTol(e.radius,t)}`;
  if(e.type==="RECTANGLE") return `RECT:${e.layer}:${roundTol(e.x,t)},${roundTol(e.y,t)},${roundTol(e.width,t)},${roundTol(e.height,t)}`;
  if(e.type==="POLYLINE") return `PL:${e.layer}:${(e.points||[]).map((p:Point2D)=>pkey(p,t)).join(";")}:${!!e.closed}`;
  return `${e.type}:${e.id}`;
};
const len=(e:any)=>e.type==="LINE"?Math.hypot(e.end.x-e.start.x,e.end.y-e.start.y):Infinity;

function canJoin(a:any,b:any,t:number){
  if(a.type!=="LINE"||b.type!=="LINE"||a.layer!==b.layer) return false;
  const adx=a.end.x-a.start.x, ady=a.end.y-a.start.y, bdx=b.end.x-b.start.x,bdy=b.end.y-b.start.y;
  const cross=Math.abs(adx*bdy-ady*bdx);
  const scale=Math.max(1,Math.hypot(adx,ady)*Math.hypot(bdx,bdy));
  if(cross/scale>1e-6) return false;
  return [a.start,a.end].some((p:Point2D)=>[b.start,b.end].some((q:Point2D)=>Math.hypot(p.x-q.x,p.y-q.y)<=t));
}
function joinTwo(a:any,b:any){
  const pts=[a.start,a.end,b.start,b.end];
  let best=[pts[0],pts[1]], d=-1;
  for(let i=0;i<pts.length;i++)for(let j=i+1;j<pts.length;j++){const x=Math.hypot(pts[i].x-pts[j].x,pts[i].y-pts[j].y);if(x>d){d=x;best=[pts[i],pts[j]];}}
  return {...a,start:{...best[0]},end:{...best[1]}};
}

export function cleanup2D(entities:CadEntity[], layers:CadLayer[], opts:CleanupOptions):{entities:CadEntity[];report:CleanupReport}{
  const report:CleanupReport={beforeCount:entities.length,afterCount:0,duplicateRemoved:0,tinyRemoved:0,joinedLines:0,normalizedLayers:0,issues:[]};
  let out=entities.map(e=>({...e} as CadEntity));
  if(opts.removeDuplicates){
    const seen=new Set<string>(); out=out.filter((e:any)=>{const k=entityKey(e,opts.toleranceMm);if(seen.has(k)){report.duplicateRemoved++;return false;}seen.add(k);return true;});
  }
  if(opts.removeTinySegments){
    out=out.filter((e:any)=>{if(e.type==="LINE"&&len(e)<opts.tinySegmentMm){report.tinyRemoved++;return false;}return true;});
  }
  if(opts.joinCollinearLines){
    let changed=true, guard=0;
    while(changed&&guard++<10000){changed=false;outer:for(let i=0;i<out.length;i++)for(let j=i+1;j<out.length;j++){if(canJoin(out[i],out[j],opts.toleranceMm)){out[i]=joinTwo(out[i],out[j]) as CadEntity;out.splice(j,1);report.joinedLines++;changed=true;break outer;}}}
    if(guard>=10000) report.issues.push({code:"HNL-CLEAN-GUARD",severity:"WARNING",message:"Dừng Join tự động do vượt giới hạn an toàn; hãy kiểm tra hình học phức tạp."});
  }
  if(opts.normalizeLayers){
    const valid=new Set(layers.map(l=>l.name)); out=out.map((e:any)=>{if(!valid.has(e.layer)){report.normalizedLayers++;return {...e,layer:"0"};}return e;});
  }
  report.afterCount=out.length;
  if(!out.length) report.issues.push({code:"HNL-CLEAN-EMPTY",severity:"WARNING",message:"Sau làm sạch không còn đối tượng. Không nên ghi đè file gốc."});
  return {entities:out,report};
}

export function auditForSketchUp(entities:CadEntity[], layers:CadLayer[]){
  const issues:CleanupIssue[]=[];
  const names=new Set(layers.map(l=>l.name));
  let unsupported=0, missingLayer=0, invalid=0;
  for(const e of entities as any[]){
    if(!names.has(e.layer)) missingLayer++;
    if(!["LINE","POLYLINE","RECTANGLE","CIRCLE","ARC","BLOCK_REF"].includes(e.type)) unsupported++;
    const pts:any[]=[]; if(e.start)pts.push(e.start,e.end); if(e.points)pts.push(...e.points); if(e.center)pts.push(e.center); if(e.position)pts.push(e.position);
    if(pts.some(p=>!Number.isFinite(p?.x)||!Number.isFinite(p?.y))) invalid++;
  }
  if(missingLayer)issues.push({code:"HNL-SU-LAYER",severity:"WARNING",message:`${missingLayer} đối tượng tham chiếu layer không tồn tại.`});
  if(unsupported)issues.push({code:"HNL-SU-UNSUPPORTED",severity:"INFO",message:`${unsupported} đối tượng ghi chú/hatch/dimension sẽ không đưa vào gói hình học SketchUp mặc định.`});
  if(invalid)issues.push({code:"HNL-SU-GEOM",severity:"ERROR",message:`${invalid} đối tượng có tọa độ không hợp lệ.`});
  return issues;
}

export function buildSketchUpBridgePackage(entities:CadEntity[],layers:CadLayer[],selectedIds:string[],opts:SketchUpExportOptions):SketchUpBridgePackage{
  const ids=new Set(selectedIds); const source=opts.scope==="SELECTED"?entities.filter(e=>ids.has(e.id)):entities;
  const issues=auditForSketchUp(source,layers); if(issues.some(i=>i.severity==="ERROR")) throw new Error(issues.filter(i=>i.severity==="ERROR").map(i=>i.message).join(" "));
  const supported=source.filter((e:any)=>["LINE","POLYLINE","RECTANGLE","CIRCLE","ARC","BLOCK_REF"].includes(e.type)) as any[];
  const origin=opts.basePoint||{x:0,y:0};
  const geometry=supported.map((e:any)=>({id:e.id,type:e.type,tag:opts.layerToTag?e.layer:"CAD_IMPORT",color:opts.preserveColors?e.color:undefined,
    component:opts.blocksToComponents&&e.type==="BLOCK_REF"?e.blockName:undefined,
    data:e.type==="LINE"?{start:e.start,end:e.end}:e.type==="POLYLINE"?{points:e.points,closed:e.closed}:e.type==="RECTANGLE"?{x:e.x,y:e.y,width:e.width,height:e.height}:e.type==="CIRCLE"?{center:e.center,radius:e.radius}:e.type==="BLOCK_REF"?{position:e.position,rotation:e.rotation||e.rotationDeg||0,scale:e.scale}:e
  }));
  const used=new Set(geometry.map((g:any)=>g.tag));
  return {schema:"hnl-sketchup-2d-bridge",version:1,generatedAt:new Date().toISOString(),units:opts.units,origin,
    tags:[...used].map(name=>{const l=layers.find(x=>x.name===name);return{name,color:l?.color,visible:l?.isVisible!==false}}),
    geometry,warnings:issues.filter(i=>i.severity!=="ERROR").map(i=>i.message)};
}

export function makeSketchUpRubyImporter(){
return `# HNL CAD AI - SketchUp 2D Bridge importer
# In SketchUp: Window > Ruby Console, load this file or package it as an extension.
require 'json'
module HNL
  module Cad2DBridge
    def self.import_json(path)
      data = JSON.parse(File.read(path))
      raise 'Unsupported HNL bridge schema' unless data['schema'] == 'hnl-sketchup-2d-bridge'
      model = Sketchup.active_model
      model.start_operation('HNL CAD 2D Import', true)
      root = model.active_entities.add_group
      root.name = 'HNL_CAD_IMPORT'
      tags = {}
      data['tags'].each { |t| tags[t['name']] = model.layers[t['name']] || model.layers.add(t['name']) }
      data['geometry'].each do |g|
        ents = root.entities
        obj = nil
        d = g['data'] || {}
        case g['type']
        when 'LINE'
          obj = ents.add_line([d['start']['x'],d['start']['y'],0], [d['end']['x'],d['end']['y'],0])
        when 'POLYLINE'
          pts=(d['points']||[]).map{|p| [p['x'],p['y'],0]}; obj=ents.add_curve(pts) if pts.length>1
          ents.add_line(pts[-1],pts[0]) if d['closed'] && pts.length>2
        when 'RECTANGLE'
          x=d['x'];y=d['y'];w=d['width'];h=d['height']; obj=ents.add_curve([[x,y,0],[x+w,y,0],[x+w,y+h,0],[x,y+h,0],[x,y,0]])
        when 'CIRCLE'
          obj=ents.add_circle([d['center']['x'],d['center']['y'],0],[0,0,1],d['radius'])
        end
        Array(obj).each{|o| o.layer=tags[g['tag']] if o.respond_to?(:layer=) && tags[g['tag']]}
        obj.layer=tags[g['tag']] if obj && obj.respond_to?(:layer=) && tags[g['tag']]
      end
      model.commit_operation
      model.active_view.zoom_extents
      root
    rescue => e
      model.abort_operation if model
      UI.messagebox("HNL Import error: " + e.message.to_s)
      raise
    end
  end
end
`;
}
