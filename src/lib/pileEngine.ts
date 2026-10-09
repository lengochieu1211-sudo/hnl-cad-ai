import { CadEntity } from '../types/cad';

export type PileType = 'PHC' | 'PC' | 'SQUARE_RC' | 'CUSTOM';
export type PileInput = {
  type: PileType;
  diameterOrWidth: number;
  length: number;
  rows: number;
  cols: number;
  spacingX: number;
  spacingY: number;
  startX: number;
  startY: number;
  manufacturer: string;
  standard: string;
  verified: boolean;
};

export type PileScheduleRow = {
  id: string;
  x: number;
  y: number;
  type: PileType;
  size: number;
  length: number;
};

export function generatePilePlan(input: PileInput): { entities: CadEntity[]; schedule: PileScheduleRow[] } {
  const entities: CadEntity[] = [];
  const schedule: PileScheduleRow[] = [];
  let index = 1;
  for (let r = 0; r < input.rows; r++) {
    for (let c = 0; c < input.cols; c++) {
      const x = input.startX + c * input.spacingX;
      const y = input.startY + r * input.spacingY;
      const pileId = `P${String(index).padStart(3, '0')}`;
      schedule.push({ id: pileId, x, y, type: input.type, size: input.diameterOrWidth, length: input.length });
      if (input.type === 'SQUARE_RC') {
        const half = input.diameterOrWidth / 2;
        entities.push({
          id: `pile_${Date.now()}_${index}`,
          handle: `P${index.toString(16).toUpperCase()}`,
          type: 'POLYLINE', layer: 'KC_COC', color: '#4DD0E1', closed: true,
          points: [
            { x: x-half, y: y-half }, { x: x+half, y: y-half },
            { x: x+half, y: y+half }, { x: x-half, y: y+half }, { x: x-half, y: y-half }
          ]
        } as any);
      } else {
        entities.push({
          id: `pile_${Date.now()}_${index}`,
          handle: `P${index.toString(16).toUpperCase()}`,
          type: 'CIRCLE', layer: 'KC_COC', color: '#4DD0E1', center: { x, y }, radius: input.diameterOrWidth / 2
        } as any);
      }
      entities.push({
        id: `pile_tag_${Date.now()}_${index}`,
        handle: `PT${index.toString(16).toUpperCase()}`,
        type: 'TEXT', layer: 'KC_COC_TAG', color: '#FFFFFF', position: { x: x + input.diameterOrWidth * 0.6, y }, text: pileId, height: Math.max(120, input.diameterOrWidth * 0.25), rotationDeg: 0
      } as any);
      index++;
    }
  }
  return { entities, schedule };
}
