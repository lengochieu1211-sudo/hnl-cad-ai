import {
  HnlSmartObject,
  DependencyEdge,
  RecomputeBatchResult,
} from "../types/cad";

export interface DependencyGraphState {
  edges: DependencyEdge[];
  dirtyObjectIds: Set<string>;
}

// Initial Dependency Relationships matching FreeCAD DAG model
export const INITIAL_DEPENDENCY_EDGES: DependencyEdge[] = [
  { fromId: "obj_room_101", toId: "obj_ceiling_c01", dependencyType: "BOUNDARY" },
  { fromId: "obj_room_101", toId: "obj_wall_w01", dependencyType: "BOUNDARY" },
  { fromId: "obj_room_101", toId: "obj_wall_w03_ei60", dependencyType: "BOUNDARY" },
  { fromId: "obj_wall_w03_ei60", toId: "obj_opening_door_fire", dependencyType: "FRAMING" },
  { fromId: "obj_ceiling_c01", toId: "obj_detail_d01", dependencyType: "DETAIL" },
  { fromId: "obj_ceiling_c01", toId: "obj_section_s01", dependencyType: "DETAIL" },
  { fromId: "obj_wall_w03_ei60", toId: "obj_section_s01", dependencyType: "DETAIL" },
  { fromId: "obj_ceiling_c01", toId: "obj_sheet_01", dependencyType: "LAYOUT" },
  { fromId: "obj_detail_d01", toId: "obj_sheet_01", dependencyType: "LAYOUT" },
  { fromId: "obj_section_s01", toId: "obj_sheet_01", dependencyType: "LAYOUT" },
];

// Mark an object as modified and recursively propagate dirty flags to all downstream children
export function markObjectDirty(
  modifiedId: string,
  edges: DependencyEdge[],
  currentSmartObjects: HnlSmartObject[]
): { updatedObjects: HnlSmartObject[]; affectedIds: string[] } {
  const affected = new Set<string>();
  affected.add(modifiedId);

  // Breadth-first propagation of dirty flags
  const queue = [modifiedId];
  while (queue.length > 0) {
    const curr = queue.shift()!;
    const children = edges.filter((e) => e.fromId === curr).map((e) => e.toId);
    for (const childId of children) {
      if (!affected.has(childId)) {
        affected.add(childId);
        queue.push(childId);
      }
    }
  }

  const affectedList = Array.from(affected);
  const updatedObjects = currentSmartObjects.map((obj) => {
    if (affected.has(obj.id)) {
      return {
        ...obj,
        dirtyFlag: true,
      };
    }
    return obj;
  });

  return { updatedObjects, affectedIds: affectedList };
}

// Perform Selective Recompute on only dirty objects
export function recomputeDirtyObjects(
  smartObjects: HnlSmartObject[],
  targetObjectId?: string
): { updatedObjects: HnlSmartObject[]; result: RecomputeBatchResult } {
  const startTime = performance.now();
  const warnings: string[] = [];
  const recomputedIds: string[] = [];

  const updatedObjects: HnlSmartObject[] = smartObjects.map((obj) => {
    const shouldRecompute = targetObjectId ? obj.id === targetObjectId : obj.dirtyFlag;

    if (shouldRecompute) {
      recomputedIds.push(obj.id);

      // Specific parametric recompute calculations based on type
      let updatedObj: any = { ...obj, dirtyFlag: false, lastRecomputedAt: new Date().toISOString() };

      if (obj.type === "HNL_CEILING") {
        const widthM = 5.6;
        const heightM = 3.6;
        const newArea = widthM * heightM;
        updatedObj = {
          ...updatedObj,
          areaM2: Math.round(newArea * 100) / 100,
        };
      } else if (obj.type === "HNL_WALL") {
        const w = obj as any;
        if (w.fireRating === "EI60" && !w.testedAssemblyId) {
          warnings.push(`Vách ${w.name}: Cần đối chiếu chứng nhận Test Assembly trước khi xuất Shopdrawing.`);
        }
      }

      return updatedObj as HnlSmartObject;
    }
    return obj;
  });

  const durationMs = Math.round((performance.now() - startTime) * 10) / 10;

  return {
    updatedObjects,
    result: {
      updatedObjectIds: recomputedIds,
      recomputedCount: recomputedIds.length,
      durationMs,
      warnings,
    },
  };
}
