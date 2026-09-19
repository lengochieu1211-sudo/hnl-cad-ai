export type PublishJobStatus="PENDING"|"RUNNING"|"DONE"|"FAILED"|"CANCELED";
export interface PublishJob{id:string;sheetId:string;sheetNo:string;sheetName:string;status:PublishJobStatus;progress:number;message?:string;startedAt?:string;completedAt?:string}
export function createPublishQueue(sheets:Array<{id:string;sheetNo:string;sheetName:string}>):PublishJob[]{
 return sheets.map(s=>({id:`job_${s.id}_${Date.now()}`,sheetId:s.id,sheetNo:s.sheetNo,sheetName:s.sheetName,status:"PENDING",progress:0}));
}
