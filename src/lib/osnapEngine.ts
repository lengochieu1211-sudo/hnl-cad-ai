import {
  CadEntity,
  CadWall,
  CadLine,
  CadPolyline,
  CadRectangle,
  CadCircle,
  Point2D,
  OsnapMode,
  OsnapPoint,
  OsnapSettings,
} from "../types/cad";

export const DEFAULT_OSNAP_SETTINGS: OsnapSettings = {
  enabled: true,
  trackingEnabled: true,
  apertureSizePx: 16,
  modes: {
    ENDPOINT: true,
    MIDPOINT: true,
    CENTER: true,
    INTERSECTION: true,
    PERPENDICULAR: true,
    NEAREST: false, // Default false like AutoCAD to avoid noise unless enabled
    QUADRANT: true,
    EXTENSION: true,
    NODE: true,
  },
  showTooltips: true,
  showSnapMarker: true,
  showAlignmentGuides: true,
};

// Helper: Distance between two 2D points
export function distance2D(p1: Point2D, p2: Point2D): number {
  return Math.hypot(p2.x - p1.x, p2.y - p1.y);
}

// Helper: Project point onto line segment
export function projectPointOntoSegment(
  p: Point2D,
  a: Point2D,
  b: Point2D
): { point: Point2D; t: number; distance: number } {
  const l2 = Math.pow(b.x - a.x, 2) + Math.pow(b.y - a.y, 2);
  if (l2 === 0) {
    return { point: { ...a }, t: 0, distance: distance2D(p, a) };
  }
  let t = ((p.x - a.x) * (b.x - a.x) + (p.y - a.y) * (b.y - a.y)) / l2;
  t = Math.max(0, Math.min(1, t));
  const proj = {
    x: Math.round(a.x + t * (b.x - a.x)),
    y: Math.round(a.y + t * (b.y - a.y)),
  };
  return { point: proj, t, distance: distance2D(p, proj) };
}

// Helper: Line segment intersection calculation
export function getLineIntersection(
  p1: Point2D,
  p2: Point2D,
  p3: Point2D,
  p4: Point2D
): Point2D | null {
  const d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
  if (Math.abs(d) < 1e-6) return null; // Parallel or collinear

  const u = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
  const v = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;

  if (u >= -0.01 && u <= 1.01 && v >= -0.01 && v <= 1.01) {
    return {
      x: Math.round(p1.x + u * (p2.x - p1.x)),
      y: Math.round(p1.y + u * (p2.y - p1.y)),
    };
  }
  return null;
}

export interface SegmentInfo {
  p1: Point2D;
  p2: Point2D;
  entityId: string;
  desc: string;
}

/**
 * Extracts line segments from CAD entities for intersection & nearest/perp checks
 */
export function extractSegmentsFromEntities(entities: CadEntity[]): SegmentInfo[] {
  const segments: SegmentInfo[] = [];

  entities.forEach((ent) => {
    switch (ent.type) {
      case "WALL": {
        const wall = ent as CadWall;
        segments.push({
          p1: wall.p1,
          p2: wall.p2,
          entityId: wall.id,
          desc: `Tường (${wall.thickness}mm)`,
        });
        break;
      }
      case "LINE": {
        const line = ent as any;
        if (line.start && line.end) {
          segments.push({
            p1: line.start,
            p2: line.end,
            entityId: line.id,
            desc: "Đường thẳng (Line)",
          });
        }
        break;
      }
      case "RECTANGLE": {
        const rect = ent as any;
        const p1 = { x: rect.x, y: rect.y };
        const p2 = { x: rect.x + rect.width, y: rect.y };
        const p3 = { x: rect.x + rect.width, y: rect.y + rect.height };
        const p4 = { x: rect.x, y: rect.y + rect.height };
        segments.push(
          { p1, p2, entityId: rect.id, desc: "Cạnh HCN" },
          { p1: p2, p2: p3, entityId: rect.id, desc: "Cạnh HCN" },
          { p1: p3, p2: p4, entityId: rect.id, desc: "Cạnh HCN" },
          { p1: p4, p2: p1, entityId: rect.id, desc: "Cạnh HCN" }
        );
        break;
      }
      case "POLYLINE": {
        const poly = ent as any;
        if (poly.points && poly.points.length > 1) {
          for (let i = 0; i < poly.points.length - 1; i++) {
            segments.push({
              p1: poly.points[i],
              p2: poly.points[i + 1],
              entityId: poly.id,
              desc: `Polyline (${i + 1})`,
            });
          }
          if (poly.closed && poly.points.length > 2) {
            segments.push({
              p1: poly.points[poly.points.length - 1],
              p2: poly.points[0],
              entityId: poly.id,
              desc: "Polyline (Khép kín)",
            });
          }
        }
        break;
      }
    }
  });

  return segments;
}

/**
 * Searches for the best real-time Osnap point near the mouse pointer
 */
export function findBestOsnapPoint({
  rawMousePos,
  entities,
  viewTransform,
  settings,
  activeDrawingOrigin,
}: {
  rawMousePos: Point2D; // In CAD world coords
  entities: CadEntity[];
  viewTransform: { scale: number; offsetX: number; offsetY: number };
  settings: OsnapSettings;
  activeDrawingOrigin?: Point2D | null; // e.g. previous point in Line/Wall tool
}): OsnapPoint | null {
  if (!settings.enabled) return null;

  const worldToScreen = (p: Point2D): Point2D => ({
    x: p.x * viewTransform.scale + viewTransform.offsetX,
    y: viewTransform.offsetY - p.y * viewTransform.scale,
  });

  const mouseScreen = worldToScreen(rawMousePos);
  const aperturePx = settings.apertureSizePx || 16;
  const candidates: OsnapPoint[] = [];

  const segments = extractSegmentsFromEntities(entities);

  // 1. ENDPOINTS & MIDPOINTS
  entities.forEach((ent) => {
    switch (ent.type) {
      case "WALL": {
        const wall = ent as CadWall;
        if (settings.modes.ENDPOINT) {
          candidates.push(
            {
              point: wall.p1,
              mode: "ENDPOINT",
              entityId: wall.id,
              sourceDescription: `Điểm đầu Tường (${wall.p1.x}, ${wall.p1.y})`,
              distanceToMouse: distance2D(rawMousePos, wall.p1),
              screenPos: worldToScreen(wall.p1),
            },
            {
              point: wall.p2,
              mode: "ENDPOINT",
              entityId: wall.id,
              sourceDescription: `Điểm cuối Tường (${wall.p2.x}, ${wall.p2.y})`,
              distanceToMouse: distance2D(rawMousePos, wall.p2),
              screenPos: worldToScreen(wall.p2),
            }
          );
        }
        if (settings.modes.MIDPOINT) {
          const mid = {
            x: Math.round((wall.p1.x + wall.p2.x) / 2),
            y: Math.round((wall.p1.y + wall.p2.y) / 2),
          };
          candidates.push({
            point: mid,
            mode: "MIDPOINT",
            entityId: wall.id,
            sourceDescription: `Trung điểm Tường (${mid.x}, ${mid.y})`,
            distanceToMouse: distance2D(rawMousePos, mid),
            screenPos: worldToScreen(mid),
          });
        }
        break;
      }

      case "LINE": {
        const line = ent as any;
        if (line.start && line.end) {
          if (settings.modes.ENDPOINT) {
            candidates.push(
              {
                point: line.start,
                mode: "ENDPOINT",
                entityId: line.id,
                sourceDescription: `Điểm đầu Line (${line.start.x}, ${line.start.y})`,
                distanceToMouse: distance2D(rawMousePos, line.start),
                screenPos: worldToScreen(line.start),
              },
              {
                point: line.end,
                mode: "ENDPOINT",
                entityId: line.id,
                sourceDescription: `Điểm cuối Line (${line.end.x}, ${line.end.y})`,
                distanceToMouse: distance2D(rawMousePos, line.end),
                screenPos: worldToScreen(line.end),
              }
            );
          }
          if (settings.modes.MIDPOINT) {
            const mid = {
              x: Math.round((line.start.x + line.end.x) / 2),
              y: Math.round((line.start.y + line.end.y) / 2),
            };
            candidates.push({
              point: mid,
              mode: "MIDPOINT",
              entityId: line.id,
              sourceDescription: `Trung điểm Line (${mid.x}, ${mid.y})`,
              distanceToMouse: distance2D(rawMousePos, mid),
              screenPos: worldToScreen(mid),
            });
          }
        }
        break;
      }

      case "RECTANGLE": {
        const rect = ent as any;
        const corners = [
          { x: rect.x, y: rect.y },
          { x: rect.x + rect.width, y: rect.y },
          { x: rect.x + rect.width, y: rect.y + rect.height },
          { x: rect.x, y: rect.y + rect.height },
        ];
        if (settings.modes.ENDPOINT) {
          corners.forEach((c, idx) => {
            candidates.push({
              point: c,
              mode: "ENDPOINT",
              entityId: rect.id,
              sourceDescription: `Góc HCN #${idx + 1} (${c.x}, ${c.y})`,
              distanceToMouse: distance2D(rawMousePos, c),
              screenPos: worldToScreen(c),
            });
          });
        }
        if (settings.modes.MIDPOINT) {
          for (let i = 0; i < 4; i++) {
            const c1 = corners[i];
            const c2 = corners[(i + 1) % 4];
            const mid = {
              x: Math.round((c1.x + c2.x) / 2),
              y: Math.round((c1.y + c2.y) / 2),
            };
            candidates.push({
              point: mid,
              mode: "MIDPOINT",
              entityId: rect.id,
              sourceDescription: `Trung điểm cạnh HCN (${mid.x}, ${mid.y})`,
              distanceToMouse: distance2D(rawMousePos, mid),
              screenPos: worldToScreen(mid),
            });
          }
        }
        if (settings.modes.CENTER) {
          const center = {
            x: Math.round(rect.x + rect.width / 2),
            y: Math.round(rect.y + rect.height / 2),
          };
          candidates.push({
            point: center,
            mode: "CENTER",
            entityId: rect.id,
            sourceDescription: `Tâm HCN (${center.x}, ${center.y})`,
            distanceToMouse: distance2D(rawMousePos, center),
            screenPos: worldToScreen(center),
          });
        }
        break;
      }

      case "CIRCLE": {
        const circ = ent as any;
        if (settings.modes.CENTER && circ.center) {
          candidates.push({
            point: circ.center,
            mode: "CENTER",
            entityId: circ.id,
            sourceDescription: `Tâm đường tròn R=${circ.radius} (${circ.center.x}, ${circ.center.y})`,
            distanceToMouse: distance2D(rawMousePos, circ.center),
            screenPos: worldToScreen(circ.center),
          });
        }
        if (settings.modes.QUADRANT && circ.center && circ.radius) {
          const quads = [
            { x: circ.center.x + circ.radius, y: circ.center.y },
            { x: circ.center.x, y: circ.center.y + circ.radius },
            { x: circ.center.x - circ.radius, y: circ.center.y },
            { x: circ.center.x, y: circ.center.y - circ.radius },
          ];
          quads.forEach((q, idx) => {
            candidates.push({
              point: q,
              mode: "QUADRANT",
              entityId: circ.id,
              sourceDescription: `Góc phần tư ${idx * 90}° (${q.x}, ${q.y})`,
              distanceToMouse: distance2D(rawMousePos, q),
              screenPos: worldToScreen(q),
            });
          });
        }
        break;
      }

      case "POLYLINE": {
        const poly = ent as any;
        if (poly.points && poly.points.length > 0) {
          if (settings.modes.ENDPOINT) {
            poly.points.forEach((pt: Point2D, idx: number) => {
              candidates.push({
                point: pt,
                mode: "ENDPOINT",
                entityId: poly.id,
                sourceDescription: `Đỉnh Polyline #${idx + 1} (${pt.x}, ${pt.y})`,
                distanceToMouse: distance2D(rawMousePos, pt),
                screenPos: worldToScreen(pt),
              });
            });
          }
          if (settings.modes.MIDPOINT && poly.points.length > 1) {
            for (let i = 0; i < poly.points.length - 1; i++) {
              const mid = {
                x: Math.round((poly.points[i].x + poly.points[i + 1].x) / 2),
                y: Math.round((poly.points[i].y + poly.points[i + 1].y) / 2),
              };
              candidates.push({
                point: mid,
                mode: "MIDPOINT",
                entityId: poly.id,
                sourceDescription: `Trung đoạn Polyline #${i + 1}`,
                distanceToMouse: distance2D(rawMousePos, mid),
                screenPos: worldToScreen(mid),
              });
            }
          }
        }
        break;
      }

      case "DIMENSION": {
        const dim = ent as any;
        if (settings.modes.ENDPOINT && dim.defPoint1 && dim.defPoint2) {
          candidates.push(
            {
              point: dim.defPoint1,
              mode: "ENDPOINT",
              entityId: dim.id,
              sourceDescription: `Chân Dim 1 (${dim.defPoint1.x}, ${dim.defPoint1.y})`,
              distanceToMouse: distance2D(rawMousePos, dim.defPoint1),
              screenPos: worldToScreen(dim.defPoint1),
            },
            {
              point: dim.defPoint2,
              mode: "ENDPOINT",
              entityId: dim.id,
              sourceDescription: `Chân Dim 2 (${dim.defPoint2.x}, ${dim.defPoint2.y})`,
              distanceToMouse: distance2D(rawMousePos, dim.defPoint2),
              screenPos: worldToScreen(dim.defPoint2),
            }
          );
        }
        break;
      }

      case "MLEADER": {
        const mld = ent as any;
        if (settings.modes.ENDPOINT && mld.leaderPoints && mld.leaderPoints.length > 0) {
          mld.leaderPoints.forEach((pt: Point2D, i: number) => {
            candidates.push({
              point: pt,
              mode: "ENDPOINT",
              entityId: mld.id,
              sourceDescription: i === 0 ? "Mũi tên MLeader" : `Điểm ngoặt MLeader #${i}`,
              distanceToMouse: distance2D(rawMousePos, pt),
              screenPos: worldToScreen(pt),
            });
          });
        }
        break;
      }
    }
  });

  // 2. INTERSECTIONS (Intersections between segments)
  if (settings.modes.INTERSECTION && segments.length > 1) {
    for (let i = 0; i < segments.length; i++) {
      for (let j = i + 1; j < segments.length; j++) {
        if (segments[i].entityId === segments[j].entityId) continue;
        const inter = getLineIntersection(
          segments[i].p1,
          segments[i].p2,
          segments[j].p1,
          segments[j].p2
        );
        if (inter) {
          candidates.push({
            point: inter,
            mode: "INTERSECTION",
            sourceDescription: `Giao điểm [${segments[i].desc} ✕ ${segments[j].desc}]`,
            distanceToMouse: distance2D(rawMousePos, inter),
            screenPos: worldToScreen(inter),
          });
        }
      }
    }
  }

  // 3. PERPENDICULAR (from active drawing origin to visible segments)
  if (settings.modes.PERPENDICULAR && activeDrawingOrigin && segments.length > 0) {
    segments.forEach((seg) => {
      const proj = projectPointOntoSegment(activeDrawingOrigin, seg.p1, seg.p2);
      if (proj.t > 0.05 && proj.t < 0.95) {
        candidates.push({
          point: proj.point,
          mode: "PERPENDICULAR",
          entityId: seg.entityId,
          sourceDescription: `Vuông góc tới ${seg.desc}`,
          distanceToMouse: distance2D(rawMousePos, proj.point),
          screenPos: worldToScreen(proj.point),
          guideLineStart: activeDrawingOrigin,
          guideLineEnd: proj.point,
        });
      }
    });
  }

  // 4. EXTENSION / ALIGNMENT TRACKING (OTRACK)
  if (settings.trackingEnabled && settings.modes.EXTENSION && activeDrawingOrigin) {
    // Check horizontal, vertical alignment with endpoints of existing geometry
    candidates.forEach((cand) => {
      if (cand.mode === "ENDPOINT" || cand.mode === "MIDPOINT") {
        // Horizontal tracking alignment (same Y)
        const horizAlignedPoint: Point2D = { x: rawMousePos.x, y: cand.point.y };
        const horizDistScreen = Math.abs(mouseScreen.y - cand.screenPos.y);
        if (horizDistScreen < aperturePx) {
          candidates.push({
            point: horizAlignedPoint,
            mode: "EXTENSION",
            entityId: cand.entityId,
            sourceDescription: `Gióng ngang Y = ${cand.point.y}mm`,
            distanceToMouse: distance2D(rawMousePos, horizAlignedPoint),
            screenPos: worldToScreen(horizAlignedPoint),
            guideLineStart: cand.point,
            guideLineEnd: horizAlignedPoint,
            guideAngleDeg: 0,
          });
        }

        // Vertical tracking alignment (same X)
        const vertAlignedPoint: Point2D = { x: cand.point.x, y: rawMousePos.y };
        const vertDistScreen = Math.abs(mouseScreen.x - cand.screenPos.x);
        if (vertDistScreen < aperturePx) {
          candidates.push({
            point: vertAlignedPoint,
            mode: "EXTENSION",
            entityId: cand.entityId,
            sourceDescription: `Gióng dọc X = ${cand.point.x}mm`,
            distanceToMouse: distance2D(rawMousePos, vertAlignedPoint),
            screenPos: worldToScreen(vertAlignedPoint),
            guideLineStart: cand.point,
            guideLineEnd: vertAlignedPoint,
            guideAngleDeg: 90,
          });
        }
      }
    });
  }

  // 5. NEAREST (Along segment if no discrete point found and mode enabled)
  if (settings.modes.NEAREST && segments.length > 0) {
    segments.forEach((seg) => {
      const proj = projectPointOntoSegment(rawMousePos, seg.p1, seg.p2);
      if (proj.t >= 0 && proj.t <= 1) {
        candidates.push({
          point: proj.point,
          mode: "NEAREST",
          entityId: seg.entityId,
          sourceDescription: `Điểm gần nhất trên ${seg.desc}`,
          distanceToMouse: distance2D(rawMousePos, proj.point),
          screenPos: worldToScreen(proj.point),
        });
      }
    });
  }

  // Filter candidates by screen aperture distance
  const validCandidates = candidates.filter((c) => {
    const screenDist = Math.hypot(mouseScreen.x - c.screenPos.x, mouseScreen.y - c.screenPos.y);
    return screenDist <= aperturePx;
  });

  if (validCandidates.length === 0) return null;

  // Priority order: ENDPOINT > INTERSECTION > MIDPOINT > CENTER > QUADRANT > PERPENDICULAR > EXTENSION > NEAREST
  const priorityMap: Record<OsnapMode, number> = {
    ENDPOINT: 10,
    INTERSECTION: 9,
    MIDPOINT: 8,
    CENTER: 7,
    QUADRANT: 6,
    PERPENDICULAR: 5,
    NODE: 4,
    EXTENSION: 3,
    NEAREST: 1,
  };

  validCandidates.sort((a, b) => {
    const screenDistA = Math.hypot(mouseScreen.x - a.screenPos.x, mouseScreen.y - a.screenPos.y);
    const screenDistB = Math.hypot(mouseScreen.x - b.screenPos.x, mouseScreen.y - b.screenPos.y);

    const scoreA = (priorityMap[a.mode] || 0) * 100 - screenDistA;
    const scoreB = (priorityMap[b.mode] || 0) * 100 - screenDistB;
    return scoreB - scoreA;
  });

  return validCandidates[0];
}

/**
 * Draws the AutoCAD-style Geometric Osnap Glyphs and Alignment Guides on Canvas
 */
export function drawOsnapMarker(
  ctx: CanvasRenderingContext2D,
  snap: OsnapPoint,
  viewTransform: { scale: number; offsetX: number; offsetY: number },
  activeOriginPoint?: Point2D | null
) {
  const sx = snap.screenPos.x;
  const sy = snap.screenPos.y;
  const size = 12; // Marker visual half-size in px

  ctx.save();

  // 1. Draw Visual Alignment / Tracking Guide Lines if present
  if (snap.guideLineStart) {
    const gStart = {
      x: snap.guideLineStart.x * viewTransform.scale + viewTransform.offsetX,
      y: viewTransform.offsetY - snap.guideLineStart.y * viewTransform.scale,
    };

    ctx.strokeStyle = "#00E676"; // AutoCAD Green
    ctx.lineWidth = 1.25;
    ctx.setLineDash([4, 4]);

    ctx.beginPath();
    ctx.moveTo(gStart.x, gStart.y);
    ctx.lineTo(sx, sy);
    ctx.stroke();
    ctx.setLineDash([]);

    // Small '+' anchor marker at alignment origin
    ctx.strokeStyle = "#00E676";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(gStart.x - 4, gStart.y);
    ctx.lineTo(gStart.x + 4, gStart.y);
    ctx.moveTo(gStart.x, gStart.y - 4);
    ctx.lineTo(gStart.x, gStart.y + 4);
    ctx.stroke();
  }

  // 2. Magnetic Halo Glow & Pulsing Ring
  ctx.shadowColor = "#00E676";
  ctx.shadowBlur = 8;
  ctx.strokeStyle = "#00E676";
  ctx.lineWidth = 2;

  // 3. Draw Specific Geometric Snap Glyphs according to standard CAD conventions
  switch (snap.mode) {
    case "ENDPOINT": {
      // Square: □
      ctx.strokeRect(sx - size / 2, sy - size / 2, size, size);
      break;
    }

    case "MIDPOINT": {
      // Triangle: △
      ctx.beginPath();
      ctx.moveTo(sx, sy - size * 0.7);
      ctx.lineTo(sx + size * 0.65, sy + size * 0.5);
      ctx.lineTo(sx - size * 0.65, sy + size * 0.5);
      ctx.closePath();
      ctx.stroke();
      break;
    }

    case "CENTER": {
      // Circle with Center Cross: ⊕
      ctx.beginPath();
      ctx.arc(sx, sy, size * 0.6, 0, Math.PI * 2);
      ctx.moveTo(sx - size * 0.8, sy);
      ctx.lineTo(sx + size * 0.8, sy);
      ctx.moveTo(sx, sy - size * 0.8);
      ctx.lineTo(sx, sy + size * 0.8);
      ctx.stroke();
      break;
    }

    case "INTERSECTION": {
      // 'X' Marker: ✕
      ctx.beginPath();
      ctx.moveTo(sx - size / 2, sy - size / 2);
      ctx.lineTo(sx + size / 2, sy + size / 2);
      ctx.moveTo(sx + size / 2, sy - size / 2);
      ctx.lineTo(sx - size / 2, sy + size / 2);
      ctx.stroke();
      break;
    }

    case "PERPENDICULAR": {
      // Right Angle Marker: ⊾
      ctx.beginPath();
      ctx.moveTo(sx - size / 2, sy - size / 2);
      ctx.lineTo(sx - size / 2, sy + size / 2);
      ctx.lineTo(sx + size / 2, sy + size / 2);
      ctx.stroke();
      // Inner square for right-angle
      ctx.strokeRect(sx - size / 2, sy + size / 2 - 5, 5, 5);
      break;
    }

    case "QUADRANT": {
      // Diamond: ◇
      ctx.beginPath();
      ctx.moveTo(sx, sy - size * 0.7);
      ctx.lineTo(sx + size * 0.7, sy);
      ctx.lineTo(sx, sy + size * 0.7);
      ctx.lineTo(sx - size * 0.7, sy);
      ctx.closePath();
      ctx.stroke();
      break;
    }

    case "EXTENSION": {
      // Small 'X' with dashed line: ✛
      ctx.beginPath();
      ctx.arc(sx, sy, 3, 0, Math.PI * 2);
      ctx.fillStyle = "#00E676";
      ctx.fill();
      break;
    }

    case "NEAREST": {
      // Hourglass: ⧖
      ctx.beginPath();
      ctx.moveTo(sx - size / 2, sy - size / 2);
      ctx.lineTo(sx + size / 2, sy - size / 2);
      ctx.lineTo(sx - size / 2, sy + size / 2);
      ctx.lineTo(sx + size / 2, sy + size / 2);
      ctx.closePath();
      ctx.stroke();
      break;
    }

    default: {
      ctx.strokeRect(sx - 4, sy - 4, 8, 8);
      break;
    }
  }

  // 4. Measure & Angle Guide Tag when actively drawing from origin
  if (activeOriginPoint) {
    const dist = distance2D(activeOriginPoint, snap.point);
    const dx = snap.point.x - activeOriginPoint.x;
    const dy = snap.point.y - activeOriginPoint.y;
    let angleDeg = (Math.atan2(dy, dx) * 180) / Math.PI;
    if (angleDeg < 0) angleDeg += 360;

    const midScreenX = (activeOriginPoint.x * viewTransform.scale + viewTransform.offsetX + sx) / 2;
    const midScreenY = (viewTransform.offsetY - activeOriginPoint.y * viewTransform.scale + sy) / 2;

    // Measurement badge
    const measureText = `L: ${dist.toFixed(0)}mm  ∠${angleDeg.toFixed(1)}°`;
    ctx.font = "bold 11px monospace";
    ctx.fillStyle = "rgba(20, 22, 26, 0.85)";
    const textWidth = ctx.measureText(measureText).width;
    ctx.fillRect(midScreenX - textWidth / 2 - 5, midScreenY - 14, textWidth + 10, 18);
    ctx.strokeStyle = "#00E676";
    ctx.lineWidth = 1;
    ctx.strokeRect(midScreenX - textWidth / 2 - 5, midScreenY - 14, textWidth + 10, 18);

    ctx.fillStyle = "#00E676";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(measureText, midScreenX, midScreenY - 5);
  }

  // 5. Tooltip Tag beside cursor
  const tagText = `${snap.mode}: ${snap.point.x}, ${snap.point.y}`;
  ctx.font = "10px sans-serif";
  ctx.textBaseline = "top";
  ctx.textAlign = "left";
  const tagWidth = ctx.measureText(tagText).width;

  const tagX = sx + 14;
  const tagY = sy - 8;

  ctx.fillStyle = "rgba(17, 18, 20, 0.9)";
  ctx.fillRect(tagX - 2, tagY - 2, tagWidth + 8, 16);
  ctx.strokeStyle = "#00E676";
  ctx.lineWidth = 1;
  ctx.strokeRect(tagX - 2, tagY - 2, tagWidth + 8, 16);

  ctx.fillStyle = "#FFFFFF";
  ctx.fillText(tagText, tagX + 2, tagY + 1);

  ctx.restore();
}
