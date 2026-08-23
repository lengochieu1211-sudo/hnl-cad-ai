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

export type CadDraftingMode = "SNAP" | "OSNAP" | "OTRACK" | "ORTHO" | "GRID" | "DYN";

export interface CadDraftingStatus {
  snap: boolean;
  osnap: boolean;
  otrack: boolean;
  ortho: boolean;
  grid: boolean;
  dyn: boolean;
}

export interface CadDraftingAction {
  id: number;
  mode: CadDraftingMode;
  enabled: boolean;
}

export interface CadPointerStatus {
  x: number;
  y: number;
  activeSnapMode?: string | null;
}

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
  draftingAction?: CadDraftingAction | null;
  onDraftingStatusChange?: (status: CadDraftingStatus) => void;
  onPointerStatusChange?: (status: CadPointerStatus) => void;
  hideInternalStatusBar?: boolean;
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
  draftingAction = null,
  onDraftingStatusChange,
  onPointerStatusChange,
  hideInternalStatusBar = false,
}) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [viewportSize, setViewportSize] = useState({ width: 1000, height: 700 });

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
  const [hoveredEntityId, setHoveredEntityId] = useState<string | null>(null);
  const [isGridSnap, setIsGridSnap] = useState(false); // SNAPMODE / F9
  const [isGridVisible, setIsGridVisible] = useState(true); // GRIDMODE / F7
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

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const update = () => setViewportSize({
      width: Math.max(1, el.clientWidth),
      height: Math.max(1, el.clientHeight),
    });
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!draftingAction) return;
    switch (draftingAction.mode) {
      case "SNAP": setIsGridSnap(draftingAction.enabled); break;
      case "GRID": setIsGridVisible(draftingAction.enabled); break;
      case "ORTHO": setIsOrtho(draftingAction.enabled); break;
      case "OSNAP":
        setOsnapSettings((prev) => ({ ...prev, enabled: draftingAction.enabled }));
        break;
      case "OTRACK":
        setOsnapSettings((prev) => ({ ...prev, trackingEnabled: draftingAction.enabled }));
        break;
      case "DYN":
        setDynInput((prev) => ({ ...prev, enabled: draftingAction.enabled }));
        break;
    }
  }, [draftingAction?.id]);

  useEffect(() => {
    onDraftingStatusChange?.({
      snap: isGridSnap,
      osnap: osnapSettings.enabled,
      otrack: osnapSettings.trackingEnabled,
      ortho: isOrtho,
      grid: isGridVisible,
      dyn: dynInput.enabled,
    });
  }, [
    isGridSnap,
    isGridVisible,
    isOrtho,
    osnapSettings.enabled,
    osnapSettings.trackingEnabled,
    dynInput.enabled,
    onDraftingStatusChange,
  ]);

  useEffect(() => {
    onPointerStatusChange?.({
      x: mousePosCad.x,
      y: mousePosCad.y,
      activeSnapMode: activeSnap?.mode || null,
    });
  }, [mousePosCad.x, mousePosCad.y, activeSnap?.mode, onPointerStatusChange]);

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

  const finishPolyline = useCallback((closed = false) => {
    if (currentTool !== "POLYLINE" || !onAddEntity) return false;
    if (tempPoints.length < 2) {
      setTempPoints([]);
      return false;
    }
    const points = [...tempPoints];
    let length = 0;
    for (let i = 1; i < points.length; i++) {
      length += Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y);
    }
    if (closed && points.length >= 3) {
      length += Math.hypot(points[0].x - points[points.length - 1].x, points[0].y - points[points.length - 1].y);
    }
    onAddEntity({
      id: `poly_${Date.now()}`,
      handle: Math.random().toString(16).substring(2, 8).toUpperCase(),
      type: "POLYLINE",
      layer: "0",
      color: "#00E5FF",
      points,
      closed,
      length,
    } as any);
    setTempPoints([]);
    if (onToolComplete) onToolComplete();
    return true;
  }, [currentTool, onAddEntity, onToolComplete, tempPoints]);

  // Global Keyboard shortcuts for CAD functions + PLINE subcommands.
  useEffect(() => {
    const isTypingTarget = (target: EventTarget | null) => {
      const el = target as HTMLElement | null;
      return Boolean(el && (
        el instanceof HTMLInputElement ||
        el instanceof HTMLTextAreaElement ||
        el.isContentEditable ||
        el.closest?.('input, textarea, [contenteditable="true"]')
      ));
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "F3") {
        e.preventDefault();
        setOsnapSettings((prev) => ({ ...prev, enabled: !prev.enabled }));
        return;
      } else if (e.key === "F11") {
        e.preventDefault();
        setOsnapSettings((prev) => ({ ...prev, trackingEnabled: !prev.trackingEnabled }));
        return;
      } else if (e.key === "F7") {
        e.preventDefault();
        setIsGridVisible((prev) => !prev);
        return;
      } else if (e.key === "F8") {
        e.preventDefault();
        setIsOrtho((prev) => !prev);
        return;
      } else if (e.key === "F9") {
        e.preventDefault();
        setIsGridSnap((prev) => !prev);
        return;
      } else if (e.key === "F12") {
        e.preventDefault();
        setDynInput((prev) => ({ ...prev, enabled: !prev.enabled }));
        return;
      }

      // AutoCAD-like PLINE session:
      // click = next vertex, Enter/Space = finish, C = close, U = undo last vertex.
      if (currentTool === "POLYLINE" && !isTypingTarget(e.target)) {
        const key = e.key.toUpperCase();
        if (e.key === "Enter" || e.code === "Space") {
          e.preventDefault();
          e.stopPropagation();
          finishPolyline(false);
          return;
        }
        if (key === "C" && tempPoints.length >= 3) {
          e.preventDefault();
          e.stopPropagation();
          finishPolyline(true);
          return;
        }
        if (key === "U" && tempPoints.length > 0) {
          e.preventDefault();
          e.stopPropagation();
          setTempPoints((prev) => prev.slice(0, -1));
          return;
        }
      }

      if (e.key === "Escape") {
        setTempPoints([]);
        setIsBoxSelecting(false);
        setIsPanning(false);
        setActiveSnap(null);
        setHoveredEntityId(null);
        setDynInput((prev) => ({
          ...prev,
          lengthInput: "",
          angleInput: "",
          coordXInput: "",
          coordYInput: "",
          lockedLength: null,
          lockedAngle: null,
        }));
      }
    };
    window.addEventListener("keydown", handleKeyDown, true);
    return () => window.removeEventListener("keydown", handleKeyDown, true);
  }, [currentTool, tempPoints.length, finishPolyline]);

  // Clear local command state whenever App returns to SELECT (ESC / command complete).
  useEffect(() => {
    if (currentTool !== "SELECT") return;
    setTempPoints([]);
    setIsBoxSelecting(false);
    setIsPanning(false);
    setActiveSnap(null);
    setHoveredEntityId(null);
    setDynInput((prev) => ({
      ...prev,
      lengthInput: "",
      angleInput: "",
      coordXInput: "",
      coordYInput: "",
      lockedLength: null,
      lockedAngle: null,
    }));
  }, [currentTool]);

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

    // Draw Grid (GRIDMODE / F7)
    if (isGridVisible) {
      drawGrid(ctx, canvas.width, canvas.height, viewTransform, isPaperSpace);
    }

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
        const isPreselected = !isSelected && hoveredEntityId === entity.id;
        drawEntity(ctx, entity, viewTransform, isSelected || isPreselected, false);
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
    isGridVisible,
    isBoxSelecting,
    selectionBoxStart,
    selectionBoxCurrent,
    tempPoints,
    mousePosCad,
    currentTool,
    activeSnap,
    osnapSettings,
    hoveredEntityId,
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

    const layerStyle = layers.find((l) => l.name === entity.layer);
    const color = isGhost
      ? "#00E5FF"
      : isSelected
      ? "#00E5FF"
      : entity.color || layerStyle?.color || "#FFFFFF";

    ctx.strokeStyle = color;
    ctx.fillStyle = color;
    const effectiveLineweight = entity.lineweight ?? layerStyle?.lineweight ?? 0.25;
    ctx.lineWidth = isSelected ? 3 : effectiveLineweight * 10 * vt.scale + 1;

    const lt = String(layerStyle?.linetype || "Continuous").toUpperCase();
    if (!isGhost) {
      if (lt.includes("CENTER")) ctx.setLineDash([12, 4, 2, 4]);
      else if (lt.includes("HIDDEN") || lt.includes("DASH")) ctx.setLineDash([8, 5]);
    }

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

    if (tool === "POLYLINE") {
      ctx.beginPath();
      points.forEach((pt, index) => {
        const sp = { x: pt.x * vt.scale + vt.offsetX, y: vt.offsetY - pt.y * vt.scale };
        if (index === 0) ctx.moveTo(sp.x, sp.y);
        else ctx.lineTo(sp.x, sp.y);
      });
      const last = points[points.length - 1];
      if (last) {
        const sl = { x: last.x * vt.scale + vt.offsetX, y: vt.offsetY - last.y * vt.scale };
        ctx.moveTo(sl.x, sl.y);
        ctx.lineTo(s2.x, s2.y);
      }
      ctx.stroke();
    } else if (tool.startsWith("WALL")) {
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

  // CAD drafting constraint helpers. ORTHO must use the LAST vertex for multi-segment
  // PLINE, not the first vertex. This fixes the F8 diagonal-segment regression.
  const getDraftConstraintOrigin = (): Point2D | null => {
    if (tempPoints.length === 0) return null;
    if (currentTool === "POLYLINE") return tempPoints[tempPoints.length - 1];
    return tempPoints[0];
  };

  const applyOrthoConstraint = (point: Point2D): Point2D => {
    if (!isOrtho) return point;
    const origin = getDraftConstraintOrigin();
    if (!origin) return point;
    const dx = Math.abs(point.x - origin.x);
    const dy = Math.abs(point.y - origin.y);
    return dx >= dy ? { x: point.x, y: origin.y } : { x: origin.x, y: point.y };
  };

  // Mouse Interaction Handlers
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;
    const worldPos = screenToWorld(screenX, screenY);

    // Middle mouse = Pan. Shift is reserved for CAD-style selection add/remove.
    if (e.button === 1 || e.buttons === 4) {
      setIsPanning(true);
      setPanStart({ x: e.clientX, y: e.clientY });
      return;
    }

    // Only left mouse creates/selects geometry. Right mouse is reserved for command completion/context.
    if (e.button !== 0) return;

    // Determine final coordinate. ORTHO is the final geometric constraint; OSNAP is
    // accepted only through the constrained cursor state computed by mouse-move.
    const clickCandidate = activeSnap && osnapSettings.enabled ? activeSnap.point : worldPos;
    const effectivePos = applyOrthoConstraint(clickCandidate);

    // Left Click in Active Drawing Tool mode
    if (currentTool !== "SELECT" && onAddEntity) {
      if (currentTool === "MTEXT") {
        const text = window.prompt("MTEXT — Nội dung:", "");
        if (text && text.trim()) {
          onAddEntity({
            id: `mtext_${Date.now()}`,
            handle: Math.random().toString(16).substring(2, 8).toUpperCase(),
            type: "MTEXT",
            layer: "0",
            color: "#FFFFFF",
            position: effectivePos,
            text: text.trim(),
            height: 250,
          } as any);
        }
        setTempPoints([]);
        if (onToolComplete) onToolComplete();
        return;
      }

      if (currentTool === "POLYLINE") {
        // Keep the command active and append vertices until Enter/Space/C/ESC.
        setTempPoints((prev) => {
          const last = prev[prev.length - 1];
          if (last && Math.hypot(last.x - effectivePos.x, last.y - effectivePos.y) < 1e-6) return prev;
          return [...prev, effectivePos];
        });
        canvasRef.current?.focus();
        return;
      }

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
        // PICKADD-like behavior: subsequent clicks add to the selection.
        // Ctrl/Shift clicking a selected object toggles/removes it.
        const additive = e.ctrlKey || e.shiftKey || selectedEntityIds.length > 0;
        onSelectEntity(hitEntity.id, additive);
      } else {
        // Start AutoCAD-style Window/Crossing selection.
        setIsBoxSelecting(true);
        setSelectionBoxStart({ x: screenX, y: screenY });
        setSelectionBoxCurrent({ x: screenX, y: screenY });
        if (!e.ctrlKey && !e.shiftKey) onClearSelection();
      }
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;
    const screenX = e.clientX - rect.left;
    const screenY = e.clientY - rect.top;

    if (currentTool === "SELECT" && !isPanning && !isBoxSelecting) {
      setHoveredEntityId(findEntityAtScreen(screenX, screenY)?.id || null);
    } else if (currentTool !== "SELECT") {
      setHoveredEntityId(null);
    }

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

    // 1. Real-time OSNAP candidate search. Do NOT return early: F8/ORTHO must
    // remain authoritative. In v2.6.3 an early return let OSNAP bypass ORTHO and
    // PLINE used the first vertex as origin, producing the diagonal segment seen
    // in the user video.
    let snapCandidate: any = null;
    if (osnapSettings.enabled && !isPanning && !isBoxSelecting) {
      snapCandidate = findBestOsnapPoint({
        rawMousePos: rawWorldPos,
        entities,
        viewTransform,
        settings: osnapSettings,
        activeDrawingOrigin: getDraftConstraintOrigin(),
      });
    }

    let targetWorldPos = snapCandidate?.point ?? (isGridSnap ? screenToWorld(screenX, screenY) : rawWorldPos);
    const constrained = applyOrthoConstraint(targetWorldPos);

    // Keep the OSNAP marker only when the exact snap point is compatible with the
    // active ORTHO axis. Otherwise show the constrained cursor without a false snap.
    if (snapCandidate && (!isOrtho || Math.hypot(snapCandidate.point.x - constrained.x, snapCandidate.point.y - constrained.y) < 1e-6)) {
      setActiveSnap(snapCandidate);
    } else {
      setActiveSnap(null);
    }
    setMousePosCad(constrained);
  };

  const handleMouseUp = () => {
    if (isPanning) setIsPanning(false);
    if (!isBoxSelecting) return;

    setIsBoxSelecting(false);
    const leftToRight = selectionBoxCurrent.x >= selectionBoxStart.x;
    const minX = Math.min(selectionBoxStart.x, selectionBoxCurrent.x);
    const maxX = Math.max(selectionBoxStart.x, selectionBoxCurrent.x);
    const minY = Math.min(selectionBoxStart.y, selectionBoxCurrent.y);
    const maxY = Math.max(selectionBoxStart.y, selectionBoxCurrent.y);

    if (maxX - minX <= 5 || maxY - minY <= 5) return;

    const box={minX,minY,maxX,maxY};
    const matched=entities.filter(ent=>{
      const b=getEntityScreenBounds(ent);
      if(!b)return false;
      if(leftToRight){
        // Blue Window: entity must be fully contained.
        return b.minX>=box.minX && b.maxX<=box.maxX && b.minY>=box.minY && b.maxY<=box.maxY;
      }
      // Green Crossing: any bounding overlap is enough.
      return !(b.maxX<box.minX || b.minX>box.maxX || b.maxY<box.minY || b.minY>box.maxY);
    });

    matched.forEach(ent=>onSelectEntity(ent.id,true));
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

  function getEntityWorldPoints(ent: CadEntity): Point2D[] {
    const e:any=ent;
    if(e.type==="WALL" && e.p1 && e.p2) return [e.p1,e.p2];
    if(e.type==="LINE" && e.start && e.end) return [e.start,e.end];
    if(e.type==="POLYLINE" && Array.isArray(e.points)) return e.points;
    if(e.type==="RECTANGLE" && Number.isFinite(e.x) && Number.isFinite(e.y)){
      return [{x:e.x,y:e.y},{x:e.x+e.width,y:e.y},{x:e.x+e.width,y:e.y+e.height},{x:e.x,y:e.y+e.height}];
    }
    if(e.type==="CIRCLE" && e.center && Number.isFinite(e.radius)){
      return [
        {x:e.center.x-e.radius,y:e.center.y-e.radius},
        {x:e.center.x+e.radius,y:e.center.y+e.radius},
      ];
    }
    if(Array.isArray(e.leaderPoints)) return e.leaderPoints;
    if(Array.isArray(e.boundary)) return e.boundary;
    if(e.p1 && e.p2) return [e.p1,e.p2];
    if(e.start && e.end) return [e.start,e.end];
    if(e.position) return [e.position];
    if(e.textPosition) return [e.textPosition];
    if(Number.isFinite(e.x)&&Number.isFinite(e.y)) return [{x:e.x,y:e.y}];
    return [];
  }

  function getEntityScreenBounds(ent:CadEntity){
    const e:any=ent;
    if(e.type==="CIRCLE" && e.center && Number.isFinite(e.radius)){
      const c=worldToScreen(e.center.x,e.center.y);
      const r=Math.abs(e.radius*viewTransform.scale);
      return{minX:c.x-r,maxX:c.x+r,minY:c.y-r,maxY:c.y+r};
    }
    const pts=getEntityWorldPoints(ent).map(p=>worldToScreen(p.x,p.y));
    if(!pts.length)return null;
    let minX=Math.min(...pts.map(p=>p.x)),maxX=Math.max(...pts.map(p=>p.x));
    let minY=Math.min(...pts.map(p=>p.y)),maxY=Math.max(...pts.map(p=>p.y));
    if(["TEXT","MTEXT","BLOCK_REF"].includes((ent as any).type)){
      minX-=14;maxX+=80;minY-=18;maxY+=18;
    }
    return{minX,maxX,minY,maxY};
  }

  function findEntityAtScreen(sx: number, sy: number): CadEntity | null {
    const pickTolerancePx = 9;
    for (let i = entities.length - 1; i >= 0; i--) {
      const ent:any = entities[i];
      const layerObj=layers.find(l=>l.name===ent.layer);
      if(layerObj && !layerObj.isVisible)continue;

      if (ent.type === "WALL" && ent.p1 && ent.p2) {
        const s1 = worldToScreen(ent.p1.x, ent.p1.y);
        const s2 = worldToScreen(ent.p2.x, ent.p2.y);
        if (distToSegment({x:sx,y:sy},s1,s2) <= pickTolerancePx + Math.abs((ent.thickness||0)*viewTransform.scale)/2) return ent;
      } else if (ent.type === "LINE" && ent.start && ent.end) {
        if(distToSegment({x:sx,y:sy},worldToScreen(ent.start.x,ent.start.y),worldToScreen(ent.end.x,ent.end.y))<=pickTolerancePx)return ent;
      } else if (ent.type === "POLYLINE" && Array.isArray(ent.points)) {
        for(let j=1;j<ent.points.length;j++){
          if(distToSegment({x:sx,y:sy},worldToScreen(ent.points[j-1].x,ent.points[j-1].y),worldToScreen(ent.points[j].x,ent.points[j].y))<=pickTolerancePx)return ent;
        }
        if(ent.closed && ent.points.length>2 && distToSegment({x:sx,y:sy},worldToScreen(ent.points[ent.points.length-1].x,ent.points[ent.points.length-1].y),worldToScreen(ent.points[0].x,ent.points[0].y))<=pickTolerancePx)return ent;
      } else if (ent.type === "RECTANGLE" && Number.isFinite(ent.x)) {
        const ps=getEntityWorldPoints(ent).map(p=>worldToScreen(p.x,p.y));
        for(let j=0;j<4;j++)if(distToSegment({x:sx,y:sy},ps[j],ps[(j+1)%4])<=pickTolerancePx)return ent;
      } else if (ent.type === "CIRCLE" && ent.center && Number.isFinite(ent.radius)) {
        const c=worldToScreen(ent.center.x,ent.center.y);
        const d=Math.hypot(sx-c.x,sy-c.y);
        if(Math.abs(d-Math.abs(ent.radius*viewTransform.scale))<=pickTolerancePx)return ent;
      } else if (ent.type === "MLEADER" && Array.isArray(ent.leaderPoints)) {
        const ps=ent.leaderPoints.map((p:Point2D)=>worldToScreen(p.x,p.y));
        for(let j=1;j<ps.length;j++)if(distToSegment({x:sx,y:sy},ps[j-1],ps[j])<=pickTolerancePx)return ent;
        if(ent.textPosition){const sp=worldToScreen(ent.textPosition.x,ent.textPosition.y);if(Math.hypot(sx-sp.x,sy-sp.y)<=24)return ent;}
      } else if (ent.type === "CEILING_GRID" && Array.isArray(ent.boundary)) {
        const ps=ent.boundary.map((p:Point2D)=>worldToScreen(p.x,p.y));
        for(let j=0;j<ps.length;j++)if(distToSegment({x:sx,y:sy},ps[j],ps[(j+1)%ps.length])<=pickTolerancePx)return ent;
      } else if (ent.type === "DIMENSION" && ent.p1 && ent.p2) {
        if(distToSegment({x:sx,y:sy},worldToScreen(ent.p1.x,ent.p1.y),worldToScreen(ent.p2.x,ent.p2.y))<=pickTolerancePx+4)return ent;
      } else if (ent.type === "BLOCK_REF" || ent.type === "TEXT" || ent.type === "MTEXT") {
        const p=ent.position;
        if(p){const sp=worldToScreen(p.x,p.y);if(Math.hypot(sx-sp.x,sy-sp.y)<=24)return ent;}
      } else {
        const b=getEntityScreenBounds(ent);
        if(b && sx>=b.minX-pickTolerancePx && sx<=b.maxX+pickTolerancePx && sy>=b.minY-pickTolerancePx && sy<=b.maxY+pickTolerancePx)return ent;
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
      <div className="absolute top-3 left-3 z-10 max-w-[48%] overflow-hidden flex items-center space-x-2 bg-[#25272C]/90 backdrop-blur-md px-3 py-1.5 rounded-lg border border-neutral-700/60 shadow-lg text-xs text-neutral-300">
        <span className="font-semibold text-cyan-400">
          {activeLayout ? `[Layout] ${activeLayout.name} (${activeLayout.paperSize})` : "[Model Space] 2D Wireframe"}
        </span>
        <span className="text-neutral-500">|</span>
        <span>
          Scale: 1:{Math.max(1, Math.round(1 / Math.max(0.000001, viewTransform.scale * 10)))}
        </span>
        {currentTool !== "SELECT" && (
          <span className="min-w-0 truncate bg-cyan-500/20 text-cyan-300 px-2 py-0.5 rounded text-[11px] font-medium animate-pulse border border-cyan-500/40">
            LỆNH ĐANG CHẠY: {currentTool}
            {currentTool === "POLYLINE" ? ` • Đỉnh: ${tempPoints.length} • Enter=Kết thúc • C=Đóng kín • U=Lùi` : ""}
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
          onClick={() => setIsGridVisible((prev) => !prev)}
          title={`Grid Display [F7]: ${isGridVisible ? "ON" : "OFF"}`}
          className={`p-1.5 rounded transition ${
            isGridVisible ? "bg-cyan-500/20 text-cyan-400 border border-cyan-500/40" : "hover:bg-neutral-700"
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
          activeOriginPoint={getDraftConstraintOrigin()}
          viewportWidth={viewportSize.width}
          viewportHeight={viewportSize.height}
        />
      </div>

      {/* Dynamic Input Floating HUD */}
      {currentTool !== "SELECT" && dynInput.enabled && (
        <DynamicInputHUD
          mouseScreenPos={mouseScreenPos}
          worldPos={mousePosCad}
          startPoint={getDraftConstraintOrigin()}
          state={dynInput}
          onChangeLength={(val) => setDynInput((prev) => ({ ...prev, lengthInput: val }))}
          onChangeAngle={(val) => setDynInput((prev) => ({ ...prev, angleInput: val }))}
          onToggleField={(field) => setDynInput((prev) => ({ ...prev, activeField: field }))}
          viewportWidth={viewportSize.width}
          viewportHeight={viewportSize.height}
          onCommit={(targetPos) => {
            const committedPos = applyOrthoConstraint(targetPos);
            // PLINE keeps accumulating vertices; Dynamic Input must not end it after 2 points.
            if (currentTool === "POLYLINE") {
              setTempPoints((prev) => {
                const last = prev[prev.length - 1];
                if (last && Math.hypot(last.x - committedPos.x, last.y - committedPos.y) < 1e-6) return prev;
                return [...prev, committedPos];
              });
              return;
            }
            // Trigger two-point drawing entity completion for other tools.
            if (tempPoints.length === 0) {
              setTempPoints([targetPos]);
            } else if (onAddEntity) {
              const p1 = tempPoints[0];
              const p2 = committedPos;
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
        onContextMenu={(e) => {
          if (currentTool === "POLYLINE") {
            e.preventDefault();
            finishPolyline(false);
          }
        }}
        tabIndex={0}
        className={`w-full flex-1 min-h-0 outline-none ${currentTool==="SELECT" ? (hoveredEntityId ? "cursor-pointer" : "cursor-crosshair") : "cursor-crosshair"}`}
      />

      {!hideInternalStatusBar && (
        <>
      {/* Bottom CAD Status — real states, not decorative labels */}
      <div className="h-7 shrink-0 bg-[#1E1F22] border-t border-neutral-800 px-2 flex items-center gap-2 text-[10px] text-neutral-400 select-none overflow-x-auto scrollbar-none">
        <div className="flex items-center gap-2 font-mono shrink-0">
          <span><span className="text-neutral-600">X:</span> <b className="text-neutral-200">{mousePosCad.x.toFixed(0)}</b></span>
          <span><span className="text-neutral-600">Y:</span> <b className="text-neutral-200">{mousePosCad.y.toFixed(0)}</b></span>
          {activeSnap && (
            <span className="flex items-center gap-1 text-emerald-300 bg-emerald-500/10 px-1.5 py-0.5 rounded border border-emerald-500/30">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
              {activeSnap.mode}
            </span>
          )}
        </div>

        <span className="text-neutral-700 shrink-0">|</span>

        <div className="flex items-center gap-1 shrink-0">
          <button
            onClick={() => setIsGridSnap((prev) => !prev)}
            title="Grid Snap [F9] — khóa con trỏ theo bước lưới 100mm"
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              isGridSnap ? "bg-cyan-500/20 text-cyan-300 border-cyan-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            SNAP F9
          </button>
          <button
            onClick={() => setOsnapSettings((prev) => ({ ...prev, enabled: !prev.enabled }))}
            title="Object Snap [F3] — bắt Endpoint/Midpoint/Center/..."
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              osnapSettings.enabled ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            OSNAP F3
          </button>
          <button
            onClick={() => setOsnapSettings((prev) => ({ ...prev, trackingEnabled: !prev.trackingEnabled }))}
            title="Object Snap Tracking [F11]"
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              osnapSettings.trackingEnabled ? "bg-sky-500/20 text-sky-300 border-sky-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            OTRACK F11
          </button>
          <button
            onClick={() => setIsGridVisible((prev) => !prev)}
            title="Grid Display [F7]"
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              isGridVisible ? "bg-cyan-500/20 text-cyan-300 border-cyan-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            GRID F7
          </button>
          <button
            onClick={() => setIsOrtho((prev) => !prev)}
            title="Ortho [F8]"
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              isOrtho ? "bg-cyan-500/20 text-cyan-300 border-cyan-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            ORTHO F8
          </button>
          <button
            onClick={() => setDynInput((prev) => ({ ...prev, enabled: !prev.enabled }))}
            title="Dynamic Input [F12]"
            className={`px-1.5 py-0.5 rounded font-mono font-bold transition border ${
              dynInput.enabled ? "bg-amber-500/20 text-amber-300 border-amber-500/40" : "bg-neutral-800 text-neutral-500 border-neutral-800 hover:text-neutral-300"
            }`}
          >
            DYN F12
          </button>
        </div>

        <span className="ml-auto shrink-0 text-neutral-600">
          Sel <b className="text-cyan-400">{selectedEntityIds.length}</b> / {entities.length}
        </span>
      </div>        </>
      )}

    </div>
  );
};
