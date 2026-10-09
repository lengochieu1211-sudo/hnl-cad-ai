import { CadEntity } from "../types/cad";
export interface CompareResult{added:CadEntity[];deleted:CadEntity[];modified:Array<{before:CadEntity;after:CadEntity}>;unchanged:number;}
const key=(e:any)=>e.hnlLinkId||e.handle||e.id;
const sig=(e:any)=>JSON.stringify([e.type,e.layer,e.start,e.end,e.points,e.center,e.radius,e.position,e.text,e.blockName,e.rotation,e.scale]);
export function compareDrawings(a:CadEntity[],b:CadEntity[]):CompareResult{
 const ma=new Map(a.map(e=>[key(e),e])),mb=new Map(b.map(e=>[key(e),e]));const added:CadEntity[]=[],deleted:CadEntity[]=[],modified:any[]=[];let unchanged=0;
 for(const [k,e] of mb){const old=ma.get(k);if(!old)added.push(e);else if(sig(old)!==sig(e))modified.push({before:old,after:e});else unchanged++;}
 for(const [k,e] of ma)if(!mb.has(k))deleted.push(e);
 return{added,deleted,modified,unchanged};
}
