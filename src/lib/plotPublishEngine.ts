import { CadEntity, CadLayer, CadLayout, CadViewport } from "../types/cad";

export type PlotAreaMode="EXTENTS"|"WINDOW"|"DISPLAY"|"LAYOUT";
export interface PlotPreset{
  id:string;name:string;paper:"A4"|"A3"|"A2"|"A1"|"A0";orientation:"PORTRAIT"|"LANDSCAPE";
  scale:string;center:boolean;monochrome:boolean;lineweightScale:number;background:"WHITE"|"BLACK";
}
export interface PlotSheet{
  id:string;sheetNo:string;sheetName:string;layoutName:string;paper:string;orientation:string;scale:string;
  printerName?:string;plotStyle?:string;enabled:boolean;status:"READY"|"CHECK"|"ERROR";messages:string[];
}
export interface HnlSheetSet{
  schema:"hnl-sheet-set";version:1;name:string;description?:string;createdAt:string;updatedAt:string;
  defaultPrinter?:string;defaultPlotStyle?:string;sheets:PlotSheet[];
}
export const DEFAULT_PLOT_PRESETS:PlotPreset[]=[
  {id:"A3_MONO_1_1",name:"A3 Monochrome 1:1",paper:"A3",orientation:"LANDSCAPE",scale:"1:1",center:true,monochrome:true,lineweightScale:1,background:"WHITE"},
  {id:"A3_MONO_1_50",name:"A3 Monochrome 1:50",paper:"A3",orientation:"LANDSCAPE",scale:"1:50",center:true,monochrome:true,lineweightScale:1,background:"WHITE"},
  {id:"A1_MONO_1_100",name:"A1 Monochrome 1:100",paper:"A1",orientation:"LANDSCAPE",scale:"1:100",center:true,monochrome:true,lineweightScale:1,background:"WHITE"},
];
const boundsOf=(entities:CadEntity[])=>{
 const xs:number[]=[],ys:number[]=[];
 for(const e of entities as any[]){
  if(e.start){xs.push(e.start.x,e.end.x);ys.push(e.start.y,e.end.y)}
  else if(e.points){for(const p of e.points){xs.push(p.x);ys.push(p.y)}}
  else if(e.center){xs.push(e.center.x-e.radius,e.center.x+e.radius);ys.push(e.center.y-e.radius,e.center.y+e.radius)}
  else if(e.x!==undefined&&e.width!==undefined){xs.push(e.x,e.x+e.width);ys.push(e.y,e.y+e.height)}
  else if(e.position){xs.push(e.position.x);ys.push(e.position.y)}
 }
 if(!xs.length)return{x:0,y:0,w:1000,h:1000};
 const minX=Math.min(...xs),maxX=Math.max(...xs),minY=Math.min(...ys),maxY=Math.max(...ys);
 return{x:minX,y:minY,w:Math.max(1,maxX-minX),h:Math.max(1,maxY-minY)};
};
const esc=(s:any)=>String(s??"").replace(/[&<>"]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]||c));
export function renderPlotSvg(entities:CadEntity[],layers:CadLayer[],preset:PlotPreset,title="HNL CAD Plot",plotWindow?:{x:number;y:number;w:number;h:number}){
 const b=plotWindow||boundsOf(entities), pad=Math.max(b.w,b.h)*.03, vb=[b.x-pad,-(b.y+b.h+pad),b.w+2*pad,b.h+2*pad];
 const layerMap=new Map(layers.map(l=>[l.name,l]));
 const sw=(e:any)=>Math.max(.1,((e.lineweight??layerMap.get(e.layer)?.lineweight??.18)*4)*(preset.lineweightScale||1));
 const stroke=(e:any)=>preset.monochrome?"#000":(e.color||layerMap.get(e.layer)?.color||"#000");
 const els:string[]=[];
 for(const e of entities as any[]){
  const common=`fill="none" stroke="${stroke(e)}" stroke-width="${sw(e)}" vector-effect="non-scaling-stroke"`;
  if(e.type==="LINE")els.push(`<line x1="${e.start.x}" y1="${-e.start.y}" x2="${e.end.x}" y2="${-e.end.y}" ${common}/>`);
  else if(e.type==="POLYLINE"&&e.points?.length){const pts=e.points.map((p:any)=>`${p.x},${-p.y}`).join(" ");els.push(`<polyline points="${pts}${e.closed?" "+e.points[0].x+","+(-e.points[0].y):""}" ${common}/>`) }
  else if(e.type==="RECTANGLE")els.push(`<rect x="${e.x}" y="${-(e.y+e.height)}" width="${e.width}" height="${e.height}" ${common}/>`);
  else if(e.type==="CIRCLE")els.push(`<circle cx="${e.center.x}" cy="${-e.center.y}" r="${e.radius}" ${common}/>`);
  else if((e.type==="TEXT"||e.type==="MTEXT")&&e.position)els.push(`<text x="${e.position.x}" y="${-e.position.y}" font-size="${Math.max(100,e.height||250)}" fill="${preset.monochrome?"#000":stroke(e)}">${esc(e.text)}</text>`);
 }
 return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb.join(" ")}" width="100%" height="100%" preserveAspectRatio="xMidYMid meet"><rect x="${vb[0]}" y="${vb[1]}" width="${vb[2]}" height="${vb[3]}" fill="${preset.background==="BLACK"?"#000":"#fff"}"/>${els.join("")}</svg>`;
}
export function makePlotHtml(svg:string,title:string,paper:string,orientation:string){
 return `<!doctype html><html><head><meta charset="utf-8"><title>${esc(title)}</title><style>@page{size:${paper} ${orientation.toLowerCase()};margin:8mm}html,body{margin:0;padding:0;width:100%;height:100%;background:white}body{display:flex;align-items:center;justify-content:center}svg{max-width:100%;max-height:100%}</style></head><body>${svg}</body></html>`;
}
export function sheetSetFromLayouts(layouts:CadLayout[],viewports:CadViewport[],defaultPrinter?:string,defaultPlotStyle="monochrome.ctb"):HnlSheetSet{
 const now=new Date().toISOString();
 return{schema:"hnl-sheet-set",version:1,name:"HNL Sheet Set",createdAt:now,updatedAt:now,defaultPrinter,defaultPlotStyle,
 sheets:layouts.map((l,i)=>{const vp=viewports.filter(v=>v.layoutName===l.name);const messages:string[]=[];if(!vp.length)messages.push("Không có Viewport.");if(vp.some(v=>!v.locked))messages.push("Có Viewport chưa khóa.");return{id:l.id,sheetNo:l.drawingNo||String(i+1).padStart(2,"0"),sheetName:l.drawingName||l.name,layoutName:l.name,paper:l.paperSize,orientation:l.orientation,scale:l.scale,printerName:defaultPrinter,plotStyle:defaultPlotStyle,enabled:true,status:messages.length?"CHECK":"READY",messages};})};
}
export function preflightSheetSet(set:HnlSheetSet,availablePrinters:string[]){
 const seen=new Set<string>();let errors=0,warnings=0;
 const sheets=set.sheets.map(s=>{const messages=[...s.messages];let status:PlotSheet["status"]="READY";if(seen.has(s.sheetNo)){messages.push("Trùng số sheet.");status="ERROR";errors++;}seen.add(s.sheetNo);if(s.printerName&&availablePrinters.length&&!availablePrinters.includes(s.printerName)){messages.push("Printer/plotter không tồn tại trên máy.");if(status!=="ERROR")status="CHECK";warnings++;}if(!s.paper){messages.push("Thiếu paper size.");status="ERROR";errors++;}return{...s,status,messages};});
 return{sheetSet:{...set,sheets,updatedAt:new Date().toISOString()},errors,warnings,ready:sheets.filter(s=>s.enabled).every(s=>s.status==="READY")};
}
