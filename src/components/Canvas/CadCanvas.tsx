import React, { useRef, useEffect, useState, useCallback } from "react";
import {
  CadEntity,
  CadLayer,
  CadLayout,
  CadViewport,
  Point2D,
  CadWall,
  CadCeilingGrid,
  OsnapPoint,
  OsnapSettings,
  DynamicInputState,
} from "../../types/cad";
import { generateCeilingGridLines } from "../../lib/cadEngine";
import {
  DEFAULT_OSNAP_SETTINGS,
  findBestOsnapPoint,
  drawOsnapMarker,
} from "../../lib/osnapEngine";
import { OsnapIndicator } from "./OsnapIndicator";
import { DynamicInputHUD } from "./DynamicInputHUD";
import {
  Maximize2,
  ZoomIn,
  ZoomOut,
  Crosshair,
  Layers,
  Lock,
  Unlock,
  Move,
  Grid as GridIcon,
  Eye,
  CheckCircle,
  Magnet,
} from "lucide-react";

interface CadCanvasProps {
  entities: CadEntity[];
  layers: CadLayer[];
  activeLayout: CadLayout | null; // null = Model Space
  viewports: CadViewport[];
  selectedEntityIds: string[];
  ghostPreviewEntities?: CadEntity[];
  onSelectEntity: (id: string, multiSelect: boolean) => void;
  onClearSelection: () => void;
  onAddEntity?: (entity: CadEntity) => void;
  currentTool?: string; // "SELECT" | "LINE" | "WALL_100" | "WALL_200" | "CEILING" | "RECTANGLE" | "AREA_PICK"
  onToolComplete?: () => void;
}

export const CadCanvas: React.FC<CadCanvasProps> = ({
  entities,
  layers,
  activeLayout,
  viewports,
  selectedEntityIds,
  ghostPreviewEntities = [],
  onSelectEntity,
  onClearSelection,
  onAddEntity,
  currentTool = "SELECT",
  onToolComplete,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);

  // View transform state: scale and pan offset
  const [viewTransform, setViewTransform] = useState({
    scale: 0.08, // 1 screen pixel = 12.5 CAD mm
    offsetX: 180,
    offsetY: 480,
  });

  const [mousePosCad, setMousePosCad] = useState<Point2D>({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const [panStart, setPanStart] = useState<Point2D>({ x: 0, y: 0 });
  const [isBoxSelecting, setIsBoxSelecting] = useState(false);
  const [selectionBoxStart, setSelectionBoxStart] = useState<Point2D>({ x: 0, y: 0 });
  const [selectionBoxCurrent, setSelectionBoxCurrent] = useState<Point2D>({ x: 0, y: 0 });
  const [isGridSnap, setIsGridSnap] = useState(false); // Off by default to prioritize precise Osnap
  const [isOrtho, setIsOrtho] = useState(false);

  // Real-time Object Snap (Osnap) State
  const [osnapSettings, setOsnapSettings] = useState<OsnapSettings>(DEFAULT_OSNAP_SETTINGS);
  const [activeSnap, setActiveSnap] = useState<OsnapPoint | null>(null);

  // Dynamic Input (DYNMODE - F12)
  const [mouseScreenPos, setMouseScreenPos] = useState<Point2D>({ x: 0, y: 0 });
  const [dynInput, setDynInput] = useState<DynamicInputState>({
    enabled: true,
    activeField: "LENGTH",
    lengthInput: "",
    angleInput: "",
    coordXInput: "",
    coordYInput: "",
    lockedLength: null,
    lockedAngle: null,
  });

  // Temporary points for interactive drawing tools
  const [tempPoints, setTempPoints] = useState<Point2D[]>([]);

  // Convert screen coordinates to Raw CAD World coordinates
  const screenToWorldRaw = useCallback(
    (screenX: number, screenY: number): Point2D => {
      const x = (screenX - viewTransform.offsetX) / viewTransform.scale;
      // In CAD, Y is positive UP
      const y = (viewTransform.offsetY - screenY) / viewTransform.scale;
      return { x: Math.round(x), y: Math.round(y) };
    },
    [viewTransform]
  );

  // Convert screen coordinates to CAD World coordinates (with grid snap if active)
  const screenToWorld = useCallback(
    (screenX: number, screenY: number): Point2D => {
      const raw = screenToWorldRaw(screenX, screenY);
      if (isGridSnap) {
        const snapGrid = 100; // 100mm snap
        return {
          x: Math.round(raw.x / snapGrid) * snapGrid,
          y: Math.round(raw.y / snapGrid) * snapGrid,
        };
      }
      return raw;
    },
    [screenToWorldRaw, isGridSnap]
  );

  // Convert CAD World coordinates to screen coordinates
  const worldToScreen = useCallback(
    (wx: number, wy: number): Point2D => {
      const sx = wx * viewTransform.scale + viewTransform.offsetX;
      const sy = viewTransform.offsetY - wy * viewTransform.scale;
      return { x: sx, y: sy };
    },
    [viewTransform]
  );

  // Global Keyboard shortcuts for CAD functions (F3 Osnap, F11 OTrack, F8 Ortho, F9 Grid, F12 DynMode)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "F3") {
        e.preventDefault();
        setOsnapSettings((prev) => ({ ...prev, enabled: !prev.enabled }));
      } else if (e.key === "F11") {
        e.preventDefault();
        setOsnapSettings((prev) => ({ ...prev, trackingEnabled: !prev.trackingEnabled }));
      } else if (e.key === "F8") {
        e.preventDefault();
        setIsOrtho((prev) => !prev);
      } else if (e.key === "F9") {
        e.preventDefault();
        setIsGridSnap((prev) => !prev);
      } else if (e.key === "F12") {
        e.preventDefault();
        setDynInput((prev) => ({ ...prev, enabled: !prev.enabled }));
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  // Auto Zoom Extents
  const handleZoomExtents = useCallback(() => {
    if (!canvasRef.current) return;
    const canvas = canvasRef.current;

    if (activeLayout) {
      // Fit sheet
      const scaleX = (canvas.width * 0.75) / activeLayout.widthMm;
      const scaleY = (canvas.height * 0.75) / activeLayout.heightMm;
      const s = Math.min(scaleX, scaleY);
      setViewTransform({
        scale: s,
        offsetX: canvas.width / 2 - (activeLayout.widthMm * s) / 2,
        offsetY: canvas.height / 2 + (activeLayout.heightMm * s) / 2,
      });
      return;
    }

    if (entities.length === 0) {
      setViewTransform({ scale: 0.08, offsetX: 200, offsetY: 400 });
      return;
    }

    let minX = 0;
    let maxX = 10000;
    let minY = 0;
    let maxY = 4500;

    entities.forEach((ent) => {
      if (ent.type === "WALL") {
        minX = Math.min(minX, ent.p1.x, ent.p2.x);
        maxX = Math.max(maxX, ent.p1.x, ent.p2.x);
        minY = Math.min(minY, ent.p1.y, ent.p2.y);
        maxY = Math.max(maxY, ent.p1.y, ent.p2.y);
      }
    });

    const w = Math.max(maxX - minX, 2000);
    const h = Math.max(maxY - minY, 2000);
    const padding = 120;
    const scaleX = (canvas.width - padding * 2) / w;
    const scaleY = (canvas.height - padding * 2) / h;
    const newScale = Math.min(scaleX, scaleY, 0.2);

    setViewTransform({
      scale: newScale,
      offsetX: canvas.width / 2 - ((minX + maxX) / 2) * newScale,
      offsetY: canvas.height / 2 + ((minY + maxY) / 2) * newScale,
    });
  }, [activeLayout, entities]);

  // Handle Resize
  useEffect(() => {
    const handleResize = () => {
      if (containerRef.current && canvasRef.current) {
        canvasRef.current.width = containerRef.current.clientWidth;
        canvasRef.current.height = containerRef.current.clientHeight;
      }
    };
    handleResize();
    window.addEventListener("resize", handleResize);
    return () => window.removeEventListener("resize", handleResize);
  }, []);

  // Main Render Loop
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Clear Screen (AutoCAD Dark Charcoal background #1E1F22 or Paper White)
    const isPaperSpace = Boolean(activeLayout);
    ctx.fillStyle = isPaperSpace ? "#2B2D30" : "#1A1B1E";
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Draw Grid
    drawGrid(ctx, canvas.width, canvas.height, viewTransform, isPaperSpace);

    if (isPaperSpace && activeLayout) {
      // Draw Sheet Outline (A3/A4) with Shadow
      drawPaperSheet(ctx, activeLayout, viewTransform);

      // Render Viewports on Sheet
      viewports.forEach((vp) => {
        if (vp.layoutName === activeLayout.name) {
          drawViewportOnSheet(ctx, vp, entities, viewTransform);
        }
      });
    } else {
      // MODEL SPACE RENDERING
      // 1. Draw UCS Origin Icon
      drawUcsIcon(ctx, viewTransform);

      // 2. Draw Base Entities
      entities.forEach((entity) => {
        const layerObj = layers.find((l) => l.name === entity.layer);
        if (layerObj && !layerObj.isVisible) return;
        const isSelected = selectedEntityIds.includes(entity.id);
        drawEntity(ctx, entity, viewTransform, isSelected, false);
      });

      // 3. Draw Ghost Preview Entities (AI Action preview)
      ghostPreviewEntities.forEach((ghost) => {
        drawEntity(ctx, ghost, viewTransform, false, true);
      });

      // 4. Draw interactive tool preview if in drawing mode
      if (tempPoints.length > 0) {
        drawToolPreview(ctx, currentTool, tempPoints, mousePosCad, viewTransform);
      }

      // 5. Draw Real-time Osnap Indicator Marker & Alignment Guides
      if (activeSnap && osnapSettings.enabled && osnapSettings.showSnapMarker) {
        drawOsnapMarker(ctx, activeSnap, viewTransform, tempPoints[0] || null);
      }
    }

    // 6. Draw Selection Window Box
    if (isBoxSelecting) {
      const isCrossing = selectionBoxCurrent.x < selectionBoxStart.x;
      const x = Math.min(selectionBoxStart.x, selectionBoxCurrent.x);
      const y = Math.min(selectionBoxStart.y, selectionBoxCurrent.y);
      const w = Math.abs(selectionBoxCurrent.x - selectionBoxStart.x);
      const h = Math.abs(selectionBoxCurrent.y - selectionBoxStart.y);

      ctx.fillStyle = isCrossing ? "rgba(0, 230, 118, 0.15)" : "rgba(33, 150, 243, 0.15)";
      ctx.fillRect(x, y, w, h);
      ctx.strokeStyle = isCrossing ? "#00E676" : "#2196F3";
      ctx.lineWidth = 1;
      if (isCrossing) {
        ctx.setLineDash([4, 4]);
      } else {
        ctx.setLineDash([]);
      }
      ctx.strokeRect(x, y, w, h);
      ctx.setLineDash([]);
    }
  }, [
    entities,
    viewports,
    activeLayout,
    selectedEntityIds,
    ghostPreviewEntities,
    viewTransform,
    isBoxSelecting,
    selectionBoxStart,
    selectionBoxCurrent,
    tempPoints,
    mousePosCad,
    currentTool,
    activeSnap,
    osnapSettings,
  ]);

  // Helper Drawing Functions
  function drawGrid(
    ctx: CanvasRenderingContext2D,
    width: number,
    height: number,
    vt: { scale: number; offsetX: number; offsetY: number },
    isPaper: boolean
  ) {
    if (isPaper) return;
    const gridSpacingCad = vt.scale > 0.05 ? 1000 : 5000;
    const gridPixelSpacing = gridSpacingCad * vt.scale;

    ctx.strokeStyle = "#25272C";
    ctx.lineWidth = 1;

    const startX = vt.offsetX % gridPixelSpacing;
    const startY = vt.offsetY % gridPixelSpacing;

    ctx.beginPath();
    for (let x = startX; x < width; x += gridPixelSpacing) {
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
    }
    for (let y = startY; y < height; y += gridPixelSpacing) {
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
    }
    ctx.stroke();
  }

  function drawUcsIcon(ctx: CanvasRenderingContext2D, vt: { scale: number; offsetX: number; offsetY: number }) {
    const origin = { x: vt.offsetX, y: vt.offsetY };
    const len = 40;

    // X Axis (Red)
    ctx.strokeStyle = "#FF5252";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(origin.x, origin.y);
    ctx.lineTo(origin.x + len, origin.y);
    ctx.stroke();

    // Y Axis (Green)
    ctx.strokeStyle = "#00E676";
    ctx.beginPath();
    ctx.moveTo(origin.x, origin.y);
    ctx.lineTo(origin.x, origin.y - len);
    ctx.stroke();

    // Label
    ctx.font = "10px sans-serif";
    ctx.fillStyle = "#FF5252";
    ctx.fillText("X", origin.x + len + 4, origin.y + 3);
    ctx.fillStyle = "#00E676";
    ctx.fillText("Y", origin.x - 3, origin.y - len - 4);
    ctx.fillStyle = "#888";
    ctx.fillText("WCS", origin.x + 5, origin.y - 5);
  }

  function drawPaperSheet(ctx: CanvasRenderingContext2D, layout: CadLayout, vt: { scale: number; offsetX: number; offsetY: number }) {
    const p1 = { x: vt.offsetX, y: vt.offsetY - layout.heightMm * vt.scale };
    const w = layout.widthMm * vt.scale;
    const h = layout.heightMm * vt.scale;

    // Sheet Shadow
    ctx.fillStyle = "rgba(0,0,0,0.5)";
    ctx.fillRect(p1.x + 8, p1.y + 8, w, h);

    // Sheet White Canvas
    ctx.fillStyle = "#FFFFFF";
    ctx.fillRect(p1.x, p1.y, w, h);

    // Printable Margin
    const marginPx = layout.marginMm * vt.scale;
    ctx.strokeStyle = "#1A1A1A";
    ctx.lineWidth = 2;
    ctx.strokeRect(p1.x + marginPx, p1.y + marginPx, w - marginPx * 2, h - marginPx * 2);

    // Title Block Area (Bottom Right)
    const tbW = 140 * vt.scale;
    const tbH = 50 * vt.scale;
    const tbX = p1.x + w - marginPx - tbW;
    const tbY = p1.y + h - marginPx - tbH;

    ctx.fillStyle = "#F8F9FA";
    ctx.fillRect(tbX, tbY, tbW, tbH);
    ctx.strokeStyle = "#000000";
    ctx.lineWidth = 1.5;
    ctx.strokeRect(tbX, tbY, tbW, tbH);

    // Title Block Text
    ctx.fillStyle = "#000000";
    ctx.font = `bold ${Math.max(10, 12 * vt.scale)}px sans-serif`;
    ctx.fillText("HNL ARCHITECTURE & CAD AI", tbX + 8, tbY + 16 * vt.scale);
    ctx.font = `${Math.max(8, 10 * vt.scale)}px sans-serif`;
    ctx.fillText(layout.drawingName, tbX + 8, tbY + 28 * vt.scale);
    ctx.fillText(`TL: ${layout.scale} | ${layout.drawingNo}`, tbX + 8, tbY + 40 * vt.scale);
  }

  function drawViewportOnSheet(
    ctx: CanvasRenderingContext2D,
    vp: CadViewport,
    modelEntities: CadEntity[],
    vt: { scale: number; offsetX: number; offsetY: number }
  ) {
    const vpX = vt.offsetX + vp.x * vt.scale;
    const vpY = vt.offsetY - (vp.y + vp.height) * vt.scale;
    const vpW = vp.width * vt.scale;
    const vpH = vp.height * vt.scale;

    // Viewport Border
    ctx.strokeStyle = vp.locked ? "#00E5FF" : "#FF9100";
    ctx.lineWidth = 1.5;
    ctx.setLineDash([4, 2]);
    ctx.strokeRect(vpX, vpY, vpW, vpH);
    ctx.setLineDash([]);

    // Viewport Tag
    ctx.fillStyle = vp.locked ? "#00E5FF" : "#FF9100";
    ctx.font = "9px monospace";
    ctx.fillText(`VP: ${vp.title || vp.scale} [${vp.locked ? "LOCKED" : "UNLOCKED"}]`, vpX + 4, vpY - 4);

    // Clip and draw model content inside viewport
    ctx.save();
    ctx.beginPath();
    ctx.rect(vpX, vpY, vpW, vpH);
    ctx.clip();

    // Model transform inside viewport
    const vpModelScale = vt.scale * vp.scaleFactor * 100;
    const vpCenterScreenX = vpX + vpW / 2;
    const vpCenterScreenY = vpY + vpH / 2;

    const innerTransform = {
      scale: vpModelScale,
      offsetX: vpCenterScreenX - vp.modelCenter.x * vpModelScale,
      offsetY: vpCenterScreenY + vp.modelCenter.y * vpModelScale,
    };

    modelEntities.forEach((ent) => {
      const layerObj = layers.find((l) => l.name === ent.layer);
      if (layerObj && !layerObj.isVisible) return;
      drawEntity(ctx, ent, innerTransform, false, false);
    });

    ctx.restore();
  }

  function drawEntity(
    ctx: CanvasRenderingContext2D,
    entity: CadEntity,
    vt: { scale: number; offsetX: number; offsetY: number },
    isSelected: boolean,
    isGhost: boolean
  ) {
    ctx.save();

    const color = isGhost
      ? "#00E5FF"
      : isSelected
      ? "#00E5FF"
      : entity.color || "#FFFFFF";

    ctx.strokeStyle = color;
    ctx.fillStyle = color;
    ctx.lineWidth = isSelected ? 3 : (entity.lineweight || 0.25) * 10 * vt.scale + 1;

    if (isGhost) {
      ctx.setLineDash([6, 3]);
      ctx.shadowColor = "#00E5FF";
      ctx.shadowBlur = 8;
    }

    switch (entity.type) {
      case "WALL": {
        const wall = entity as CadWall;
        const s1 = { x: wall.p1.x * vt.scale + vt.offsetX, y: vt.offsetY - wall.p1.y * vt.scale };
        const s2 = { x: wall.p2.x * vt.scale + vt.offsetX, y: vt.offsetY - wall.p2.y * vt.scale };
        const thickPx = wall.thickness * vt.scale;

        const dx = s2.x - s1.x;
        const dy = s2.y - s1.y;
        const len = Math.sqrt(dx * dx + dy * dy);
        if (len > 0) {
          const nx = (-dy / len) * (thickPx / 2);
          const ny = (dx / len) * (thickPx / 2);

          // Wall Boundary
          ctx.beginPath();
          ctx.moveTo(s1.x + nx, s1.y + ny);
          ctx.lineTo(s2.x + nx, s2.y + ny);
          ctx.lineTo(s2.x - nx, s2.y - ny);
          ctx.lineTo(s1.x - nx, s1.y - ny);
          ctx.closePath();

          // Wall Fill Hatch
          ctx.fillStyle = wall.thickness === 200 ? "rgba(0, 229, 255, 0.18)" : "rgba(255, 145, 0, 0.18)";
          ctx.fill();
          ctx.stroke();

          // Centerline (Dash-Dot)
          ctx.beginPath();
          ctx.strokeStyle = "rgba(255,255,255,0.3)";
          ctx.lineWidth = 1;
          ctx.setLineDash([8, 4, 2, 4]);
          ctx.moveTo(s1.x, s1.y);
          ctx.lineTo(s2.x, s2.y);
          ctx.stroke();
        }
        break;
      }

      case "CEILING_GRID": {
        const ceil = entity as CadCeilingGrid;
        const gridData = generateCeilingGridLines(ceil);

        // Main Tees (Orange)
        ctx.strokeStyle = isGhost ? "#00E5FF" : "#FF9100";
        ctx.lineWidth = 1.2;
        gridData.mainTees.forEach((line) => {
          const p1 = { x: line.start.x * vt.scale + vt.offsetX, y: vt.offsetY - line.start.y * vt.scale };
          const p2 = { x: line.end.x * vt.scale + vt.offsetX, y: vt.offsetY - line.end.y * vt.scale };
          ctx.beginPath();
          ctx.moveTo(p1.x, p1.y);
          ctx.lineTo(p2.x, p2.y);
          ctx.stroke();
        });

        // Cross Tees (Gold)
        ctx.strokeStyle = isGhost ? "#00E5FF" : "rgba(255, 215, 64, 0.8)";
        ctx.lineWidth = 0.8;
        gridData.crossTees.forEach((line) => {
          const p1 = { x: line.start.x * vt.scale + vt.offsetX, y: vt.offsetY - line.start.y * vt.scale };
          const p2 = { x: line.end.x * vt.scale + vt.offsetX, y: vt.offsetY - line.end.y * vt.scale };
          ctx.beginPath();
          ctx.moveTo(p1.x, p1.y);
          ctx.lineTo(p2.x, p2.y);
          ctx.stroke();
        });

        // Hanger Dots (Cyan)
        ctx.fillStyle = "#00E5FF";
        gridData.hangers.forEach((h) => {
          const hp = { x: h.x * vt.scale + vt.offsetX, y: vt.offsetY - h.y * vt.scale };
          ctx.beginPath();
          ctx.arc(hp.x, hp.y, Math.max(2, 2.5 * vt.scale), 0, Math.PI * 2);
          ctx.fill();
        });

        // Boundary Wall Angle
        if (ceil.boundary.length > 2) {
          ctx.strokeStyle = "#FFFFFF";
          ctx.lineWidth = 1.5;
          ctx.beginPath();
          ceil.boundary.forEach((pt, i) => {
            const sp = { x: pt.x * vt.scale + vt.offsetX, y: vt.offsetY - pt.y * vt.scale };
            if (i === 0) ctx.moveTo(sp.x, sp.y);
            else ctx.lineTo(sp.x, sp.y);
          });
          ctx.closePath();
          ctx.stroke();
        }
        break;
      }

      case "BLOCK_REF": {
        const blk = entity as any;
        const bp = { x: blk.position.x * vt.scale + vt.offsetX, y: vt.offsetY - blk.position.y * vt.scale };

        if (blk.blockName?.includes("DOWNLIGHT")) {
          // Circular Light with cross
          const r = Math.max(4, 90 * vt.scale * 1.5);
          ctx.beginPath();
          ctx.arc(bp.x, bp.y, r, 0, Math.PI * 2);
          ctx.fillStyle = "rgba(255, 255, 0, 0.2)";
          ctx.fill();
          ctx.stroke();

          // Cross inside
          ctx.beginPath();
          ctx.moveTo(bp.x - r, bp.y);
          ctx.lineTo(bp.x + r, bp.y);
          ctx.moveTo(bp.x, bp.y - r);
          ctx.lineTo(bp.x, bp.y + r);
          ctx.stroke();
        } else if (blk.blockName?.includes("PANEL")) {
          // 600x600 Rect Light
          const w = 600 * vt.scale;
          ctx.beginPath();
          ctx.rect(bp.x - w / 2, bp.y - w / 2, w, w);
          ctx.fillStyle = "rgba(255, 255, 0, 0.25)";
          ctx.fill();
          ctx.stroke();

          // Diagonal cross
          ctx.beginPath();
          ctx.moveTo(bp.x - w / 2, bp.y - w / 2);
          ctx.lineTo(bp.x + w / 2, bp.y + w / 2);
          ctx.moveTo(bp.x + w / 2, bp.y - w / 2);
          ctx.lineTo(bp.x - w / 2, bp.y + w / 2);
          ctx.stroke();
        } else {
          // Generic Block Icon
          const s = 15;
          ctx.strokeRect(bp.x - s / 2, bp.y - s / 2, s, s);
          ctx.fillText(blk.blockName || "BLOCK", bp.x + s, bp.y + 4);
        }
        break;
      }

      case "TEXT":
      case "MTEXT": {
        const txt = entity as any;
        const tp = { x: txt.position.x * vt.scale + vt.offsetX, y: vt.offsetY - txt.position.y * vt.scale };
        const fontSize = Math.max(9, (txt.height || 250) * vt.scale);
        ctx.font = `${txt.hasField ? "bold " : ""}${fontSize}px sans-serif`;

        if (txt.hasField) {
          // Highlight Field with gray box background (AutoCAD standard)
          const metrics = ctx.measureText(txt.text);
          ctx.fillStyle = "rgba(128, 128, 128, 0.35)";
          ctx.fillRect(tp.x - 2, tp.y - fontSize + 2, metrics.width + 4, fontSize + 4);
        }

        ctx.fillStyle = color;
        ctx.fillText(txt.text, tp.x, tp.y);
        break;
      }

      case "DIMENSION": {
        const dim = entity as any;
        const dp1 = { x: dim.p1.x * vt.scale + vt.offsetX, y: vt.offsetY - dim.p1.y * vt.scale };
        const dp2 = { x: dim.p2.x * vt.scale + vt.offsetX, y: vt.offsetY - dim.p2.y * vt.scale };

        ctx.strokeStyle = color;
        ctx.lineWidth = 1;

        // Dimension Line
        ctx.beginPath();
        ctx.moveTo(dp1.x, dp1.y);
        ctx.lineTo(dp2.x, dp2.y);
        ctx.stroke();

        // Architectural Ticks (Slash at 45 deg)
        const tickSize = 4;
        [dp1, dp2].forEach((p) => {
          ctx.beginPath();
          ctx.moveTo(p.x - tickSize, p.y + tickSize);
          ctx.lineTo(p.x + tickSize, p.y - tickSize);
          ctx.lineWidth = 1.8;
          ctx.stroke();
        });

        // Dim Text
        const midX = (dp1.x + dp2.x) / 2;
        const midY = (dp1.y + dp2.y) / 2 - 4;
        ctx.font = `${Math.max(9, 180 * vt.scale)}px sans-serif`;
        ctx.fillStyle = color;
        ctx.textAlign = "center";
        ctx.fillText(String(dim.measurement || 0), midX, midY);
        ctx.textAlign = "start";
        break;
      }

      case "RECTANGLE": {
        const rect = entity as any;
        const rx = rect.x * vt.scale + vt.offsetX;
        const ry = vt.offsetY - (rect.y + rect.height) * vt.scale;
        const rw = rect.width * vt.scale;
        const rh = rect.height * vt.scale;
        ctx.strokeRect(rx, ry, rw, rh);
        break;
      }

      case "DETAIL_CALLOUT": {
        const det = entity as any;
        const b = det.box;
        const bx = b.x * vt.scale + vt.offsetX;
        const by = vt.offsetY - (b.y + b.height) * vt.scale;
        const bw = b.width * vt.scale;
        const bh = b.height * vt.scale;

        // 1. Detail Extraction Boundary Box (Rounded dashed cyan)
        ctx.strokeStyle = color || "#00E5FF";
        ctx.lineWidth = 1.5;
        ctx.setLineDash([6, 3]);
        ctx.strokeRect(bx, by, bw, bh);
        ctx.setLineDash([]);

        // 2. Leader & Callout Bubble
        const bpx = det.bubblePos.x * vt.scale + vt.offsetX;
        const bpy = vt.offsetY - det.bubblePos.y * vt.scale;
        const cx = (b.x + b.width) * vt.scale + vt.offsetX;
        const cy = vt.offsetY - (b.y + b.height / 2) * vt.scale;

        ctx.beginPath();
        ctx.moveTo(cx, cy);
        ctx.lineTo(bpx, bpy);
        ctx.stroke();

        // Callout Bubble Circle (split into top & bottom halves)
        const bubbleR = Math.max(14, 180 * vt.scale);
        ctx.beginPath();
        ctx.arc(bpx, bpy, bubbleR, 0, Math.PI * 2);
        ctx.fillStyle = "#1E1F22";
        ctx.fill();
        ctx.strokeStyle = color || "#00E5FF";
        ctx.lineWidth = 2;
        ctx.stroke();

        // Horizontal divider line in bubble
        ctx.beginPath();
        ctx.moveTo(bpx - bubbleR, bpy);
        ctx.lineTo(bpx + bubbleR, bpy);
        ctx.stroke();

        // Top number: Detail Number
        ctx.font = `bold ${Math.max(9, bubbleR * 0.75)}px sans-serif`;
        ctx.fillStyle = "#00E5FF";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(det.detailNumber || "01", bpx, bpy - bubbleR * 0.4);

        // Bottom number: Sheet Number
        ctx.font = `bold ${Math.max(8, bubbleR * 0.6)}px sans-serif`;
        ctx.fillStyle = "#FFFFFF";
        ctx.fillText(det.sheetNumber || "A-101", bpx, bpy + bubbleR * 0.4);

        // Title Tag next to bubble
        ctx.font = `${Math.max(8, 110 * vt.scale)}px sans-serif`;
        ctx.fillStyle = "#E0E0E0";
        ctx.textAlign = "left";
        ctx.fillText(`${det.title} (${det.scale || "1:10"})`, bpx + bubbleR + 6, bpy);
        ctx.textAlign = "start";
        ctx.textBaseline = "alphabetic";
        break;
      }

      case "SECTION_CALLOUT": {
        const sec = entity as any;
        const p1 = { x: sec.p1.x * vt.scale + vt.offsetX, y: vt.offsetY - sec.p1.y * vt.scale };
        const p2 = { x: sec.p2.x * vt.scale + vt.offsetX, y: vt.offsetY - sec.p2.y * vt.scale };

        // 1. Heavy Dash-Dot Section Cutting Line
        ctx.strokeStyle = color || "#FF9100";
        ctx.lineWidth = 2.5;
        ctx.setLineDash([12, 4, 3, 4]);
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
        ctx.setLineDash([]);

        // 2. Section Bubbles on both ends
        [p1, p2].forEach((pt) => {
          const bubbleR = Math.max(13, 160 * vt.scale);
          ctx.beginPath();
          ctx.arc(pt.x, pt.y, bubbleR, 0, Math.PI * 2);
          ctx.fillStyle = "#1E1F22";
          ctx.fill();
          ctx.strokeStyle = color || "#FF9100";
          ctx.lineWidth = 2;
          ctx.stroke();

          // Section Name & Arrow
          ctx.font = `bold ${Math.max(9, bubbleR * 0.8)}px sans-serif`;
          ctx.fillStyle = "#FF9100";
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          ctx.fillText(sec.sectionName || "A-A", pt.x, pt.y);
          ctx.textAlign = "start";
          ctx.textBaseline = "alphabetic";
        });
        break;
      }

      case "MLEADER": {
        const mld = entity as any;
        if (mld.leaderPoints && mld.leaderPoints.length > 1) {
          ctx.strokeStyle = color || "#00E5FF";
          ctx.lineWidth = isSelected ? 2.5 : 1.5;

          // 1. Draw Leader line segments
          ctx.beginPath();
          mld.leaderPoints.forEach((pt: Point2D, i: number) => {
            const sp = { x: pt.x * vt.scale + vt.offsetX, y: vt.offsetY - pt.y * vt.scale };
            if (i === 0) ctx.moveTo(sp.x, sp.y);
            else ctx.lineTo(sp.x, sp.y);
          });
          ctx.stroke();

          // 2. Draw Sharp Filled Arrowhead at the Origin (point 0)
          const p0 = mld.leaderPoints[0];
          const p1 = mld.leaderPoints[1];
          const sp0 = { x: p0.x * vt.scale + vt.offsetX, y: vt.offsetY - p0.y * vt.scale };
          const sp1 = { x: p1.x * vt.scale + vt.offsetX, y: vt.offsetY - p1.y * vt.scale };
          const angle = Math.atan2(sp0.y - sp1.y, sp0.x - sp1.x);
          const arrowLen = Math.max(7, Math.min(16, 120 * vt.scale));
          const arrowWidth = arrowLen * 0.35;

          ctx.fillStyle = color || "#00E5FF";
          ctx.beginPath();
          ctx.moveTo(sp0.x, sp0.y);
          ctx.lineTo(
            sp0.x - arrowLen * Math.cos(angle - 0.28),
            sp0.y - arrowLen * Math.sin(angle - 0.28)
          );
          ctx.lineTo(
            sp0.x - arrowLen * Math.cos(angle + 0.28),
            sp0.y - arrowLen * Math.sin(angle + 0.28)
          );
          ctx.closePath();
          ctx.fill();

          // 3. Landing Shoulder Bar & Multi-line Text
          const lastPt = mld.leaderPoints[mld.leaderPoints.length - 1];
          const spLast = { x: lastPt.x * vt.scale + vt.offsetX, y: vt.offsetY - lastPt.y * vt.scale };
          const landingLen = Math.max(20, (mld.landingDistance || 300) * vt.scale);
          const isGoingLeft = lastPt.x < p0.x;
          const landingEndX = isGoingLeft ? spLast.x - landingLen : spLast.x + landingLen;

          // Draw Horizontal Landing Bar
          ctx.beginPath();
          ctx.moveTo(spLast.x, spLast.y);
          ctx.lineTo(landingEndX, spLast.y);
          ctx.stroke();

          // Multiline Text Box
          const textStr = mld.text || "MLeader Note";
          const lines = textStr.split("\n");
          const fontSize = Math.max(9, Math.min(16, 140 * vt.scale));
          ctx.font = `500 ${fontSize}px sans-serif`;
          ctx.fillStyle = color || "#FFFFFF";
          ctx.textAlign = isGoingLeft ? "right" : "left";

          const textStartX = isGoingLeft ? spLast.x - 4 : spLast.x + 4;
          lines.forEach((line: string, lIdx: number) => {
            const lineY = spLast.y - 4 - (lines.length - 1 - lIdx) * (fontSize * 1.25);
            ctx.fillText(line, textStartX, lineY);
          });
          ctx.textAlign = "start";
        }
        break;
      }

      case "LINE": {
        const line = entity as any;
        const p1 = { x: line.start.x * vt.scale + vt.offsetX, y: vt.offsetY - line.start.y * vt.scale };
        const p2 = { x: line.end.x * vt.scale + vt.offsetX, y: vt.offsetY - line.end.y * vt.scale };
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
        break;
      }

      case "POLYLINE": {
        const poly = entity as any;
        if (poly.points && poly.points.length > 0) {
          ctx.beginPath();
          poly.points.forEach((pt: Point2D, i: number) => {
            const sp = { x: pt.x * vt.scale + vt.offsetX, y: vt.offsetY - pt.y * vt.scale };
            if (i === 0) ctx.moveTo(sp.x, sp.y);
            else ctx.lineTo(sp.x, sp.y);
          });
          if (poly.closed) ctx.closePath();
          ctx.stroke();
        }
        break;
      }

      case "CIRCLE": {
        const circ = entity as any;
        const cp = { x: circ.center.x * vt.scale + vt.offsetX, y: vt.offsetY - circ.center.y * vt.scale };
        const r = circ.radius * vt.scale;
        ctx.beginPath();
        ctx.arc(cp.x, cp.y, r, 0, Math.PI * 2);
        ctx.stroke();
        break;
      }
    }

    ctx.restore();
  }

  function drawToolPreview(
    ctx: CanvasRenderingContext2D,
    tool: string,
    points: Point2D[],
    currentPos: Point2D,
    vt: { scale: number; offsetX: number; offsetY: number }
  ) {
    ctx.save();
    ctx.strokeStyle = "#00E5FF";
    ctx.lineWidth = 2;
    ctx.setLineDash([4, 4]);

    const p1 = points[0];
    const s1 = { x: p1.x * vt.scale + vt.offsetX, y: vt.offsetY - p1.y * vt.scale };
    const s2 = { x: currentPos.x * vt.scale + vt.offsetX, y: vt.offsetY - currentPos.y * vt.scale };

    if (tool.startsWith("WALL")) {
      ctx.beginPath();
      ctx.moveTo(s1.x, s1.y);
      ctx.lineTo(s2.x, s2.y);
      ctx.stroke();

      // Wall thickness outline preview
      const thick = tool === "WALL_200" ? 200 : 100;
      const thickPx = thick * vt.scale;
      ctx.lineWidth = thickPx;
      ctx.strokeStyle = "rgba(0, 229, 255, 0.3)";
      ctx.stroke();
    } else if (tool === "RECTANGLE") {
      const rx = Math.min(s1.x, s2.x);
      const ry = Math.min(s1.y, s2.y);
      const rw = Math.abs(s2.x - s1.x);
      const rh = Math.abs(s2.y - s1.y);
      ctx.strokeRect(rx, ry, rw, rh);
    } else if (tool === "MLEADER" || tool === "MLEADER_ANNO" || tool === "MLEADER_MATERIAL") {
      // Arrowhead at origin s1
      ctx.beginPath();
      ctx.moveTo(s1.x, s1.y);
      ctx.lineTo(s2.x, s2.y);
      ctx.stroke();

      const landingLen = 35;
      const isLeft = s2.x < s1.x;
      const endX = isLeft ? s2.x - landingLen : s2.x + landingLen;
      ctx.beginPath();
      ctx.moveTo(s2.x, s2.y);
      ctx.lineTo(endX, s2.y);
      ctx.stroke();

      ctx.font = "11px sans-serif";
      ctx.fillStyle = "#00E5FF";
      ctx.fillText(
        tool === "MLEADER_MATERIAL" ? "Cấu tạo vách (cần xác nhận)" : "Chú thích MLeader...",
        isLeft ? s2.x - 40 : s2.x + 8,
        s2.y - 4
      );
    } else {
      ctx.beginPath();
      ctx.moveTo(s1.x, s1.y);
      ctx.lineTo(s2.x, s2.y);
      ctx.stroke();
    }

    ctx.restore();
  }

  // Mouse Interaction Handlers
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;
    const worldPos = screenToWorld(screenX, screenY);

    // Middle Click or Space Key = Pan
    if (e.button === 1 || e.buttons === 4 || e.shiftKey) {
      setIsPanning(true);
      setPanStart({ x: e.clientX, y: e.clientY });
      return;
    }

    // Determine final effective world coordinate (prefer precise Osnap lock if active)
    const effectivePos = activeSnap && osnapSettings.enabled ? activeSnap.point : worldPos;

    // Left Click in Active Drawing Tool mode
    if (currentTool !== "SELECT" && onAddEntity) {
      if (tempPoints.length === 0) {
        setTempPoints([effectivePos]);
      } else {
        // Complete current entity
        const p1 = tempPoints[0];
        const p2 = effectivePos;

        if (currentTool === "WALL_100" || currentTool === "WALL_200") {
          const thickness = currentTool === "WALL_200" ? 200 : 100;
          onAddEntity({
            id: `wall_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "WALL",
            layer: "KT_TUONG",
            color: thickness === 200 ? "#00E5FF" : "#FF9100",
            p1,
            p2,
            thickness,
            wallType: thickness === 200 ? "BRICK_200" : "BRICK_100",
          } as CadWall);
        } else if (currentTool === "LINE") {
          onAddEntity({
            id: `line_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "LINE",
            layer: "0",
            color: "#FFFFFF",
            start: p1,
            end: p2,
          } as any);
        } else if (currentTool === "RECTANGLE") {
          onAddEntity({
            id: `rect_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "RECTANGLE",
            layer: "0",
            color: "#00E5FF",
            x: Math.min(p1.x, p2.x),
            y: Math.min(p1.y, p2.y),
            width: Math.abs(p2.x - p1.x),
            height: Math.abs(p2.y - p1.y),
          } as any);
        } else if (currentTool === "CIRCLE") {
          const radius = Math.hypot(p2.x - p1.x, p2.y - p1.y);
          onAddEntity({
            id: `circle_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "CIRCLE",
            layer: "0",
            color: "#00E5FF",
            center: p1,
            radius,
          } as any);
        } else if (currentTool === "POLYLINE") {
          onAddEntity({
            id: `poly_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "POLYLINE",
            layer: "0",
            color: "#00E5FF",
            points: [p1, p2],
            closed: false,
            length: Math.hypot(p2.x - p1.x, p2.y - p1.y),
          } as any);
        } else if (currentTool === "MLEADER" || currentTool === "MLEADER_ANNO" || currentTool === "MLEADER_MATERIAL") {
          const isMaterial = currentTool === "MLEADER_MATERIAL";
          const defaultText = isMaterial
            ? "CẤU TẠO VÁCH – CẦN XÁC NHẬN\nBoard / Stud / Insulation theo Approved System\nKhông tự suy luận EI từ số lớp tấm"
            : "Ghi chú kỹ thuật MLeader\n(Đối chiếu Project Spec / Approved Submittal)";
          onAddEntity({
            id: `mleader_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "MLEADER",
            layer: "HNL_ANNO_MLEADER",
            color: "#00E5FF",
            leaderPoints: [p1, p2],
            text: defaultText,
            textPosition: p2,
            landingDistance: 350,
            category: isMaterial ? "FIRE_DRYWALL" : "GENERAL",
          } as any);
        }

        setTempPoints([]);
        if (onToolComplete) onToolComplete();
      }
      return;
    }

    // Left Click Selection
    if (e.button === 0) {
      // Find clicked entity
      const hitEntity = findEntityAtScreen(screenX, screenY);
      if (hitEntity) {
        onSelectEntity(hitEntity.id, e.ctrlKey);
      } else {
        // Start Box Selection
        setIsBoxSelecting(true);
        setSelectionBoxStart({ x: screenX, y: screenY });
        setSelectionBoxCurrent({ x: screenX, y: screenY });
        if (!e.ctrlKey) {
          onClearSelection();
        }
      }
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;

    if (isPanning) {
      const dx = e.clientX - panStart.x;
      const dy = e.clientY - panStart.y;
      setViewTransform((prev) => ({
        ...prev,
        offsetX: prev.offsetX + dx,
        offsetY: prev.offsetY + dy,
      }));
      setPanStart({ x: e.clientX, y: e.clientY });
      return;
    }

    if (isBoxSelecting) {
      setSelectionBoxCurrent({ x: screenX, y: screenY });
    }

    const rawWorldPos = screenToWorldRaw(screenX, screenY);

    // 1. Real-time Osnap Candidate Search
    if (osnapSettings.enabled && !isPanning && !isBoxSelecting) {
      const snap = findBestOsnapPoint({
        rawMousePos: rawWorldPos,
        entities,
        viewTransform,
        settings: osnapSettings,
        activeDrawingOrigin: tempPoints.length > 0 ? tempPoints[0] : null,
      });

      if (snap) {
        setActiveSnap(snap);
        setMousePosCad(snap.point);
        return;
      }
    }

    // If no snap detected or Osnap is off:
    setActiveSnap(null);

    let targetWorldPos = isGridSnap ? screenToWorld(screenX, screenY) : rawWorldPos;

    // Apply Ortho mode constraint if drawing in progress
    if (isOrtho && tempPoints.length > 0) {
      const origin = tempPoints[0];
      const dx = Math.abs(targetWorldPos.x - origin.x);
      const dy = Math.abs(targetWorldPos.y - origin.y);
      if (dx > dy) {
        targetWorldPos = { x: targetWorldPos.x, y: origin.y };
      } else {
        targetWorldPos = { x: origin.x, y: targetWorldPos.y };
      }
    }

    setMousePosCad(targetWorldPos);
  };

  const handleMouseUp = () => {
    if (isPanning) {
      setIsPanning(false);
    }
    if (isBoxSelecting) {
      setIsBoxSelecting(false);
      // Select all entities enclosed or crossing the box
      const minX = Math.min(selectionBoxStart.x, selectionBoxCurrent.x);
      const maxX = Math.max(selectionBoxStart.x, selectionBoxCurrent.x);
      const minY = Math.min(selectionBoxStart.y, selectionBoxCurrent.y);
      const maxY = Math.max(selectionBoxStart.y, selectionBoxCurrent.y);

      if (maxX - minX > 5 && maxY - minY > 5) {
        entities.forEach((ent) => {
          if (ent.type === "WALL") {
            const w = ent as CadWall;
            const s1 = worldToScreen(w.p1.x, w.p1.y);
            const s2 = worldToScreen(w.p2.x, w.p2.y);
            if (
              (s1.x >= minX && s1.x <= maxX && s1.y >= minY && s1.y <= maxY) ||
              (s2.x >= minX && s2.x <= maxX && s2.y >= minY && s2.y <= maxY)
            ) {
              onSelectEntity(ent.id, true);
            }
          }
        });
      }
    }
  };

  const handleWheel = (e: React.WheelEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;

    const zoomFactor = e.deltaY < 0 ? 1.15 : 0.87;
    const newScale = Math.max(0.005, Math.min(1.5, viewTransform.scale * zoomFactor));

    // Zoom towards mouse pointer
    const newOffsetX = mouseX - (mouseX - viewTransform.offsetX) * (newScale / viewTransform.scale);
    const newOffsetY = mouseY - (mouseY - viewTransform.offsetY) * (newScale / viewTransform.scale);

    setViewTransform({
      scale: newScale,
      offsetX: newOffsetX,
      offsetY: newOffsetY,
    });
  };

  function findEntityAtScreen(sx: number, sy: number): CadEntity | null {
    const pickTolerancePx = 8;
    for (let i = entities.length - 1; i >= 0; i--) {
      const ent = entities[i];
      if (ent.type === "WALL") {
        const wall = ent as CadWall;
        const s1 = worldToScreen(wall.p1.x, wall.p1.y);
        const s2 = worldToScreen(wall.p2.x, wall.p2.y);
        const dist = distToSegment({ x: sx, y: sy }, s1, s2);
        if (dist <= pickTolerancePx + (wall.thickness * viewTransform.scale) / 2) {
          return ent;
        }
      } else if (ent.type === "BLOCK_REF" || ent.type === "TEXT" || ent.type === "MTEXT") {
        const p = (ent as any).position;
        const sp = worldToScreen(p.x, p.y);
        const d = Math.hypot(sx - sp.x, sy - sp.y);
        if (d <= 20) return ent;
      }
    }
    return null;
  }

  function distToSegment(p: Point2D, v: Point2D, w: Point2D) {
    const l2 = Math.pow(v.x - w.x, 2) + Math.pow(v.y - w.y, 2);
    if (l2 === 0) return Math.hypot(p.x - v.x, p.y - v.y);
    let t = ((p.x - v.x) * (w.x - v.x) + (p.y - v.y) * (w.y - v.y)) / l2;
    t = Math.max(0, Math.min(1, t));
    return Math.hypot(p.x - (v.x + t * (w.x - v.x)), p.y - (v.y + t * (w.y - v.y)));
  }

  return (
    <div ref={containerRef} className="relative w-full h-full bg-[#1A1B1E] select-none overflow-hidden flex flex-col">
      {/* Top Left Canvas HUD */}
      <div className="absolute top-3 left-3 z-10 flex items-center space-x-2 bg-[#25272C]/90 backdrop-blur-md px-3 py-1.5 rounded-lg border border-neutral-700/60 shadow-lg text-xs text-neutral-300">
        <span className="font-semibold text-cyan-400">
          {activeLayout ? `[Layout] ${activeLayout.name} (${activeLayout.paperSize})` : "[Model Space] 2D Wireframe"}
        </span>
        <span className="text-neutral-500">|</span>
        <span>
          Scale: 1:{(1 / (viewTransform.scale * 10)).toFixed(0)}
        </span>
        {currentTool !== "SELECT" && (
          <span className="bg-cyan-500/20 text-cyan-300 px-2 py-0.5 rounded text-[11px] font-medium animate-pulse border border-cyan-500/40">
            LỆNH ĐANG CHẠY: {currentTool}
          </span>
        )}
      </div>

      {/* Top Right Floating Toolbar (Zoom / Grid / Snap / View Extents) */}
      <div className="absolute top-3 right-3 z-10 flex items-center space-x-1.5 bg-[#25272C]/90 backdrop-blur-md p-1 rounded-lg border border-neutral-700/60 shadow-lg text-neutral-300">
        <button
          onClick={handleZoomExtents}
          title="Zoom Extents (Z + E)"
          className="p-1.5 hover:bg-neutral-700 hover:text-white rounded transition"
        >
          <Maximize2 className="w-4 h-4 text-cyan-400" />
        </button>
        <button
          onClick={() =>
            setViewTransform((prev) => ({
              ...prev,
              scale: prev.scale * 1.25,
            }))
          }
          title="Zoom In (+)"
          className="p-1.5 hover:bg-neutral-700 hover:text-white rounded transition"
        >
          <ZoomIn className="w-4 h-4" />
        </button>
        <button
          onClick={() =>
            setViewTransform((prev) => ({
              ...prev,
              scale: Math.max(0.005, prev.scale * 0.8),
            }))
          }
          title="Zoom Out (-)"
          className="p-1.5 hover:bg-neutral-700 hover:text-white rounded transition"
        >
          <ZoomOut className="w-4 h-4" />
        </button>
        <div className="w-[1px] h-4 bg-neutral-700 mx-1" />
        <button
          onClick={() => setIsGridSnap(!isGridSnap)}
          title={`Snap Grid [F9]: ${isGridSnap ? "ON (100mm)" : "OFF"}`}
          className={`p-1.5 rounded transition ${
            isGridSnap ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40" : "hover:bg-neutral-700"
          }`}
        >
          <GridIcon className="w-4 h-4" />
        </button>
        <button
          onClick={() => setIsOrtho(!isOrtho)}
          title={`Ortho Mode [F8]: ${isOrtho ? "ON" : "OFF"}`}
          className={`px-2 py-1 text-xs font-mono rounded transition ${
            isOrtho ? "bg-cyan-500/20 text-cyan-400 font-bold border border-cyan-500/40" : "hover:bg-neutral-700"
          }`}
        >
          ORTHO
        </button>
        <div className="w-[1px] h-4 bg-neutral-700 mx-1" />
        {/* Quick Osnap Control */}
        <OsnapIndicator
          currentSnap={activeSnap}
          settings={osnapSettings}
          onUpdateSettings={setOsnapSettings}
          activeOriginPoint={tempPoints.length > 0 ? tempPoints[0] : null}
        />
      </div>

      {/* Dynamic Input Floating HUD */}
      {currentTool !== "SELECT" && dynInput.enabled && (
        <DynamicInputHUD
          mouseScreenPos={mouseScreenPos}
          worldPos={mousePosCad}
          startPoint={tempPoints.length > 0 ? tempPoints[0] : null}
          state={dynInput}
          onChangeLength={(val) => setDynInput((prev) => ({ ...prev, lengthInput: val }))}
          onChangeAngle={(val) => setDynInput((prev) => ({ ...prev, angleInput: val }))}
          onToggleField={(field) => setDynInput((prev) => ({ ...prev, activeField: field }))}
          onCommit={(targetPos) => {
            // Trigger drawing entity completion
            if (tempPoints.length === 0) {
              setTempPoints([targetPos]);
            } else if (onAddEntity) {
              const p1 = tempPoints[0];
              const p2 = targetPos;
              if (currentTool === "WALL_100" || currentTool === "WALL_200") {
                const thickness = currentTool === "WALL_200" ? 200 : 100;
                onAddEntity({
                  id: `wall_${Date.now()}`,
                  handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                  type: "WALL",
                  layer: "KT_TUONG",
                  color: thickness === 200 ? "#00E5FF" : "#FF9100",
                  p1,
                  p2,
                  thickness,
                  wallType: thickness === 200 ? "BRICK_200" : "BRICK_100",
                } as CadWall);
              } else if (currentTool === "LINE") {
                onAddEntity({
                  id: `line_${Date.now()}`,
                  handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                  type: "LINE",
                  layer: "0",
                  color: "#FFFFFF",
                  start: p1,
                  end: p2,
                } as any);
              } else if (currentTool === "RECTANGLE") {
                onAddEntity({
                  id: `rect_${Date.now()}`,
                  handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                  type: "RECTANGLE",
                  layer: "0",
                  color: "#00E5FF",
                  x: Math.min(p1.x, p2.x),
                  y: Math.min(p1.y, p2.y),
                  width: Math.abs(p2.x - p1.x),
                  height: Math.abs(p2.y - p1.y),
                } as any);
              }
              setTempPoints([]);
              setDynInput((prev) => ({ ...prev, lengthInput: "", angleInput: "" }));
              if (onToolComplete) onToolComplete();
            }
          }}
        />
      )}

      {/* Main CAD Interactive Canvas */}
      <canvas
        ref={canvasRef}
        onMouseDown={handleMouseDown}
        onMouseMove={(e) => {
          const rect = canvasRef.current?.getBoundingClientRect();
          if (rect) {
            setMouseScreenPos({ x: e.clientX - rect.left, y: e.clientY - rect.top });
          }
          handleMouseMove(e);
        }}
        onMouseUp={handleMouseUp}
        onWheel={handleWheel}
        className="w-full h-full cursor-crosshair"
      />

      {/* Bottom Status Bar / Coordinate Readout */}
      <div className="h-7 bg-[#1E1F22] border-t border-neutral-800 px-4 flex items-center justify-between text-xs text-neutral-400 select-none">
        <div className="flex items-center space-x-4 font-mono">
          <span className="flex items-center space-x-1">
            <span className="text-neutral-500">X:</span>
            <span className="text-neutral-200">{mousePosCad.x.toFixed(0)}</span>
          </span>
          <span className="flex items-center space-x-1">
            <span className="text-neutral-500">Y:</span>
            <span className="text-neutral-200">{mousePosCad.y.toFixed(0)}</span>
          </span>
          <span className="flex items-center space-x-1">
            <span className="text-neutral-500">Z:</span>
            <span className="text-neutral-200">0.00</span>
          </span>
          {activeSnap && (
            <span className="flex items-center space-x-1 text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/30 text-[11px] font-sans">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping" />
              <span className="font-bold font-mono">SNAP:</span>
              <span>{activeSnap.mode}</span>
            </span>
          )}
        </div>

        <div className="flex items-center space-x-3 text-[11px]">
          <div className="flex items-center space-x-1.5">
            <button
              onClick={() => setOsnapSettings((prev) => ({ ...prev, enabled: !prev.enabled }))}
              title="Bật/Tắt Object Snap [F3]"
              className={`px-1.5 py-0.5 rounded font-mono text-[10px] font-bold transition ${
                osnapSettings.enabled
                  ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40"
                  : "bg-neutral-800 text-neutral-500 hover:text-neutral-300"
              }`}
            >
              OSNAP [F3]
            </button>
            <button
              onClick={() => setOsnapSettings((prev) => ({ ...prev, trackingEnabled: !prev.trackingEnabled }))}
              title="Bật/Tắt Object Snap Tracking [F11]"
              className={`px-1.5 py-0.5 rounded font-mono text-[10px] font-bold transition ${
                osnapSettings.trackingEnabled
                  ? "bg-sky-500/20 text-sky-300 border border-sky-500/40"
                  : "bg-neutral-800 text-neutral-500 hover:text-neutral-300"
              }`}
            >
              OTRACK [F11]
            </button>
            <button
              onClick={() => setIsOrtho((prev) => !prev)}
              title="Bật/Tắt Ortho [F8]"
              className={`px-1.5 py-0.5 rounded font-mono text-[10px] font-bold transition ${
                isOrtho
                  ? "bg-cyan-500/20 text-cyan-300 border border-cyan-500/40"
                  : "bg-neutral-800 text-neutral-500 hover:text-neutral-300"
              }`}
            >
              ORTHO [F8]
            </button>
            <button
              onClick={() => setDynInput((prev) => ({ ...prev, enabled: !prev.enabled }))}
              title="Bật/Tắt Dynamic Input [F12]"
              className={`px-1.5 py-0.5 rounded font-mono text-[10px] font-bold transition ${
                dynInput.enabled
                  ? "bg-amber-500/20 text-amber-300 border border-amber-500/40"
                  : "bg-neutral-800 text-neutral-500 hover:text-neutral-300"
              }`}
            >
              DYN [F12]
            </button>
          </div>
          <span className="text-neutral-600">|</span>
          <span className="text-neutral-400">
            Selected: <strong className="text-cyan-400">{selectedEntityIds.length}</strong> objects
          </span>
          <span className="text-neutral-600">|</span>
          <span className="text-neutral-400">
            Total Entities: <strong className="text-neutral-200">{entities.length}</strong>
          </span>
          <span className="text-neutral-600">|</span>
          <span className="text-amber-400 flex items-center space-x-1">
            <span className="w-2 h-2 rounded-full bg-amber-400" />
            <span>Standalone Mode • AutoCAD bridge chưa kết nối</span>
          </span>
        </div>
      </div>
    </div>
  );
};
