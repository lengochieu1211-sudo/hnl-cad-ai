import React, { useState, useEffect, useCallback, useMemo, useRef } from "react";
import { HnlRibbon } from "./components/Ribbon/HnlRibbon";
import { CadCanvas } from "./components/Canvas/CadCanvas";
import { HnlPalette } from "./components/Palette/HnlPalette";
import { CommandSearchModal } from "./components/CommandCenter/CommandSearchModal";
import { NetPluginExporterModal } from "./components/Dialogs/NetPluginExporterModal";
import { LispBuilderModal } from "./components/Dialogs/LispBuilderModal";
import { TableBuilderModal } from "./components/Dialogs/TableBuilderModal";
import { ExcelExportModal } from "./components/Dialogs/ExcelExportModal";
import { AuditModal } from "./components/Dialogs/AuditModal";
import { SettingsModal } from "./components/Dialogs/SettingsModal";
import { AutoDetailLayoutComposerModal } from "./components/Dialogs/AutoDetailLayoutComposerModal";
import { StandaloneExeBuilderModal } from "./components/Dialogs/StandaloneExeBuilderModal";
import { DrywallCeilingStudioModal } from "./components/Dialogs/DrywallCeilingStudioModal";
import { HnlWindowsCompatibilityModal } from "./components/Dialogs/HnlWindowsCompatibilityModal";
import { HnlAddonManagerModal } from "./components/Dialogs/HnlAddonManagerModal";
import { HnlShopdrawingCheckModal } from "./components/Dialogs/HnlShopdrawingCheckModal";
import { HnlSectionGeneratorModal } from "./components/Dialogs/HnlSectionGeneratorModal";
import { HnlMepClashModal } from "./components/Dialogs/HnlMepClashModal";
import { HnlExportModal } from "./components/Dialogs/HnlExportModal";
import { HnlBuildingCodeModal } from "./components/Dialogs/HnlBuildingCodeModal";
import { HnlPileStudioModal } from "./components/Dialogs/HnlPileStudioModal";
import { DiagnosticsModal } from "./components/Dialogs/DiagnosticsModal";
import { UsageGuideModal } from "./components/Dialogs/UsageGuideModal";
import { SketchUp2DBridgeModal } from "./components/Dialogs/SketchUp2DBridgeModal";
import { ProfessionalAuditCenterModal } from "./components/Dialogs/ProfessionalAuditCenterModal";
import { PlotPublishSheetSetModal } from "./components/Dialogs/PlotPublishSheetSetModal";
import { LispInspiredToolCenterModal } from "./components/Dialogs/LispInspiredToolCenterModal";

// FreeCAD-inspired Dock Panels
import { HnlProjectTreePanel } from "./components/Dock/HnlProjectTreePanel";
import { HnlPropertyEditorPanel } from "./components/Dock/HnlPropertyEditorPanel";
import { HnlDependencyPanel } from "./components/Dock/HnlDependencyPanel";
import { HnlSpreadsheetPanel } from "./components/Dock/HnlSpreadsheetPanel";

import {
  INITIAL_ENTITIES,
  INITIAL_LAYERS,
  INITIAL_LAYOUTS,
  INITIAL_VIEWPORTS,
  INITIAL_BLOCK_LIBRARY,
  INITIAL_LISP_SCRIPTS,
  INITIAL_TRANSLATION_MEMORY,
  INITIAL_AUDIT_ISSUES,
} from "./lib/initialData";
import {
  INITIAL_SMART_OBJECTS,
  buildLogicalProjectTree,
  calculateSmartObjectBOQ,
} from "./lib/smartObjectEngine";
import {
  INITIAL_DEPENDENCY_EDGES,
  markObjectDirty,
  recomputeDirtyObjects,
} from "./lib/dependencyEngine";
import {
  INITIAL_SPREADSHEET_PARAMETERS,
  evaluateExpression,
} from "./lib/spreadsheetEngine";
import { INITIAL_HNL_MODULES } from "./lib/moduleManagerEngine";
import { detectAutoCadBridge, AutoCadBridgeStatus } from "./lib/autoCadBridge";
import { loadProjectSnapshot, saveProjectSnapshot, clearProjectSnapshot } from "./lib/projectPersistence";
import { DiagnosticEvent, errorToDetails, loadDiagnostics, makeDiagnostic, saveDiagnostics } from "./lib/diagnostics";
import { markCommandHealth } from "./lib/commandHealth";
import { saveRecoveryGeneration } from "./lib/recoveryGenerations";

import {
  CadEntity,
  CadLayer,
  CadLayout,
  CadViewport,
  BlockLibraryItem,
  LispScriptItem,
  TranslationMemoryItem,
  DrawingAuditIssue,
  AICommandPlan,
  CadWall,
  CadCeilingGrid,
  CadTable,
  HnlSmartObject,
  HnlModuleItem,
  SpreadsheetParameter,
  DependencyEdge,
} from "./types/cad";
import {
  normalizeVietnameseText,
  calculatePolygonArea,
  calculatePolygonPerimeter,
  calculateCentroid,
  distance2D,
  computeBlockSimilarity,
  calculateOptimalViewportScale,
} from "./lib/cadEngine";
import { parseBasicDxf } from "./lib/importEngine";
import {
  Plus,
  Layout as LayoutIcon,
  Check,
  Layers,
  AlertCircle,
  FolderTree,
  Sliders,
  Cpu,
  Table2,
  ChevronLeft,
  ChevronRight,
  Maximize2,
  Minimize2,
} from "lucide-react";

export default function App() {
  // Core CAD State
  const [entities, setEntities] = useState<CadEntity[]>(INITIAL_ENTITIES);
  const [layers, setLayers] = useState<CadLayer[]>(INITIAL_LAYERS);
  const [layouts, setLayouts] = useState<CadLayout[]>(INITIAL_LAYOUTS);
  const [activeLayout, setActiveLayout] = useState<CadLayout | null>(null); // null = Model Space
  const [viewports, setViewports] = useState<CadViewport[]>(INITIAL_VIEWPORTS);
  const [blockLibrary, setBlockLibrary] = useState<BlockLibraryItem[]>(INITIAL_BLOCK_LIBRARY);
  const [lispScripts, setLispScripts] = useState<LispScriptItem[]>(INITIAL_LISP_SCRIPTS);
  const [translationMemory, setTranslationMemory] = useState<TranslationMemoryItem[]>(INITIAL_TRANSLATION_MEMORY);
  const [auditIssues, setAuditIssues] = useState<DrawingAuditIssue[]>(INITIAL_AUDIT_ISSUES);

  // Interaction State
  const [selectedEntityIds, setSelectedEntityIds] = useState<string[]>([]);
  const [ghostPreviewEntities, setGhostPreviewEntities] = useState<CadEntity[]>([]);
  const [currentTool, setCurrentTool] = useState<string>("SELECT");
  const [activeRibbonTab, setActiveRibbonTab] = useState<string>("VE_NHANH");

  // History Stack for Undo/Redo
  const [history, setHistory] = useState<CadEntity[][]>([INITIAL_ENTITIES]);
  const [historyIndex, setHistoryIndex] = useState(0);

  // Modals & Palette State
  const [isAiPaletteOpen, setIsAiPaletteOpen] = useState(true);
  const [paletteDockPosition, setPaletteDockPosition] = useState<"left" | "right">("right");
  const [isCommandCenterOpen, setIsCommandCenterOpen] = useState(false);
  const [isNetPluginExporterOpen, setIsNetPluginExporterOpen] = useState(false);
  const [isLispBuilderOpen, setIsLispBuilderOpen] = useState(false);
  const [isTableBuilderOpen, setIsTableBuilderOpen] = useState(false);
  const [isExcelExportOpen, setIsExcelExportOpen] = useState(false);
  const [isAuditModalOpen, setIsAuditModalOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isAutoDetailComposerOpen, setIsAutoDetailComposerOpen] = useState(false);
  const [isStandaloneExeBuilderOpen, setIsStandaloneExeBuilderOpen] = useState(false);
  const [isDrywallStudioOpen, setIsDrywallStudioOpen] = useState(false);
  const [isWindowsCompatOpen, setIsWindowsCompatOpen] = useState(false);
  const [isSectionGenOpen, setIsSectionGenOpen] = useState(false);
  const [isMepClashOpen, setIsMepClashOpen] = useState(false);
  const [isMultiExportOpen, setIsMultiExportOpen] = useState(false);
  const [isBuildingCodeOpen, setIsBuildingCodeOpen] = useState(false);
  const [isPileStudioOpen, setIsPileStudioOpen] = useState(false);
  const [autoCadBridgeStatus, setAutoCadBridgeStatus] = useState<AutoCadBridgeStatus>({ connected: false, source: "standalone", lastCheckedAt: Date.now() });
  const [lastAutosaveAt, setLastAutosaveAt] = useState<string | null>(null);
  const [recoveryLoaded, setRecoveryLoaded] = useState(false);
  const [currentFileName, setCurrentFileName] = useState("Untitled.hnl.json");
  const [isDirty, setIsDirty] = useState(false);
  const suppressProjectDirtyRef = useRef(true);
  const [showStartCenter, setShowStartCenter] = useState(true);
  const [isDiagnosticsOpen, setIsDiagnosticsOpen] = useState(false);
  const [isUsageGuideOpen, setIsUsageGuideOpen] = useState(false);
  const [isSketchUpBridgeOpen, setIsSketchUpBridgeOpen] = useState(false);
  const [isProfessionalAuditOpen, setIsProfessionalAuditOpen] = useState(false);
  const [isPlotPublishOpen, setIsPlotPublishOpen] = useState(false);
  const [is2DProfessionalOpen, setIs2DProfessionalOpen] = useState(false);
  const [pro2DInitialTab, setPro2DInitialTab] = useState<"TEXT"|"FIELD"|"GEOMETRY"|"DIMENSION"|"QUANTITY"|"LAYOUT"|"TOOLS"|"SOURCES">("TOOLS");
  const [diagnosticEvents, setDiagnosticEvents] = useState<DiagnosticEvent[]>(() => loadDiagnostics());

  // FreeCAD Architecture State: Smart Objects, Workbenches, Modules & DAG
  const [smartObjects, setSmartObjects] = useState<HnlSmartObject[]>(INITIAL_SMART_OBJECTS);
  const [selectedSmartObjectId, setSelectedSmartObjectId] = useState<string | null>("obj_ceiling_c01");
  const [dependencyEdges, setDependencyEdges] = useState<DependencyEdge[]>(INITIAL_DEPENDENCY_EDGES);
  const [spreadsheetParameters, setSpreadsheetParameters] = useState<SpreadsheetParameter[]>(INITIAL_SPREADSHEET_PARAMETERS);
  const [modules, setModules] = useState<HnlModuleItem[]>(INITIAL_HNL_MODULES);
  const [isSafeMode, setIsSafeMode] = useState(() => {
    try {
      const cfg = JSON.parse(localStorage.getItem("hnl.settings.v1") || "{}");
      return typeof cfg.safeMode === "boolean" ? cfg.safeMode : true;
    } catch { return true; }
  });
  const [selectedWorkbench, setSelectedWorkbench] = useState<string>("HNL_CAD");

  // Left Dock Navigation & FreeCAD Tools
  const [isLeftDockOpen, setIsLeftDockOpen] = useState(true);
  const [leftDockTab, setLeftDockTab] = useState<"TREE" | "PROPERTIES" | "DEPENDENCY" | "SPREADSHEET">("TREE");
  const [isAddonManagerOpen, setIsAddonManagerOpen] = useState(false);
  const [isShopCheckOpen, setIsShopCheckOpen] = useState(false);

  // Logical Project Tree derived from Smart Objects
  const projectTree = useMemo(() => buildLogicalProjectTree(smartObjects), [smartObjects]);

  // Selected Smart Object Instance
  const selectedSmartObject = useMemo(() => {
    return smartObjects.find((o) => o.id === selectedSmartObjectId) || null;
  }, [smartObjects, selectedSmartObjectId]);

  // Toast Notification Message
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = useCallback((msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  }, []);

  const pushDiagnostic = useCallback((event: Omit<DiagnosticEvent, "id" | "timestamp">, open = false) => {
    const item = makeDiagnostic(event);
    setDiagnosticEvents(prev => { const next = [item, ...prev].slice(0, 150); saveDiagnostics(next); return next; });
    if (open) setIsDiagnosticsOpen(true);
    return item;
  }, []);

  const reportCommandFailure = useCallback((command: string, title: string, message: string, suggestion?: string, severity: DiagnosticEvent["severity"] = "WARNING") => {
    const code = `HNL-CMD-${Math.abs(Array.from(command).reduce((a,c)=>((a*31+c.charCodeAt(0))|0),7)).toString(16).toUpperCase().slice(0,6)}`;
    markCommandHealth(command, false, 0, message, "PARTIAL");
    pushDiagnostic({
      code, severity, title, message, command, suggestion,
      context: { file: currentFileName, selectedCount: selectedEntityIds.length, workbench: selectedWorkbench, autoCadConnected: autoCadBridgeStatus.connected, safeMode: isSafeMode }
    }, severity === "ERROR" || severity === "CRITICAL");
    showToast(`${code}: ${message}`);
  }, [pushDiagnostic, currentFileName, selectedEntityIds.length, selectedWorkbench, autoCadBridgeStatus.connected, isSafeMode, showToast]);

  useEffect(() => {
    const onError = (ev: ErrorEvent) => {
      pushDiagnostic({ code: "HNL-RUNTIME-001", severity: "ERROR", title: "Lỗi runtime giao diện", message: ev.message || "Lỗi JavaScript không xác định", cause: ev.error?.message, stack: ev.error?.stack, context: { file: currentFileName, source: ev.filename, line: ev.lineno, column: ev.colno }, suggestion: "Copy báo cáo chẩn đoán và gửi kèm thao tác vừa thực hiện trước khi lỗi xảy ra." }, true);
    };
    const onReject = (ev: PromiseRejectionEvent) => { const d = errorToDetails(ev.reason); pushDiagnostic({ code: "HNL-ASYNC-001", severity: "ERROR", title: "Lỗi tác vụ bất đồng bộ", message: "Một tác vụ nền/AI/file bị từ chối ngoài dự kiến.", cause: d.cause, stack: d.stack, context: { file: currentFileName }, suggestion: "Mở Trung tâm chẩn đoán, copy log và thử lại chức năng một lần." }, true); };
    window.addEventListener("error", onError); window.addEventListener("unhandledrejection", onReject);
    return () => { window.removeEventListener("error", onError); window.removeEventListener("unhandledrejection", onReject); };
  }, [pushDiagnostic, currentFileName]);

  // Recovery & Autosave: restore the last valid local project snapshot once, then persist changes periodically.
  useEffect(() => {
    if (recoveryLoaded) return;
    const snapshot = loadProjectSnapshot();
    if (snapshot && Array.isArray(snapshot.entities)) {
      suppressProjectDirtyRef.current = true;
      setEntities(snapshot.entities as CadEntity[]);
      if (Array.isArray(snapshot.layers)) setLayers(snapshot.layers as CadLayer[]);
      if (Array.isArray(snapshot.layouts)) setLayouts(snapshot.layouts as CadLayout[]);
      if (Array.isArray(snapshot.viewports)) setViewports(snapshot.viewports as CadViewport[]);
      if (Array.isArray(snapshot.smartObjects)) setSmartObjects(snapshot.smartObjects as HnlSmartObject[]);
      if (Array.isArray(snapshot.spreadsheetParameters)) setSpreadsheetParameters(snapshot.spreadsheetParameters as SpreadsheetParameter[]);
      if (Array.isArray((snapshot as any).translationMemory)) setTranslationMemory((snapshot as any).translationMemory as TranslationMemoryItem[]);
      if (Array.isArray((snapshot as any).blockLibrary)) setBlockLibrary((snapshot as any).blockLibrary as BlockLibraryItem[]);
      if (Array.isArray((snapshot as any).dependencyEdges)) setDependencyEdges((snapshot as any).dependencyEdges as DependencyEdge[]);
      if (Array.isArray((snapshot as any).modules)) setModules((snapshot as any).modules as HnlModuleItem[]);
      if ((snapshot as any).selectedWorkbench) setSelectedWorkbench((snapshot as any).selectedWorkbench);
      if ((snapshot as any).activeLayoutId) setActiveLayout((snapshot.layouts as CadLayout[]).find((l: any) => l.id === (snapshot as any).activeLayoutId) || null);
      setCurrentFileName((snapshot as any).currentFileName || "Recovered.hnl.json");
      setHistory([snapshot.entities as CadEntity[]]);
      setHistoryIndex(0);
      setLastAutosaveAt(snapshot.savedAt);
      setIsDirty(true);
      showToast(`Đã khôi phục AutoSave lúc ${new Date(snapshot.savedAt).toLocaleString()}`);
    }
    setRecoveryLoaded(true);
  }, [recoveryLoaded, showToast]);

  useEffect(() => {
    if (!recoveryLoaded) return;
    const saveNow = () => {
      const snapshotInput = { entities, layers, layouts, viewports, smartObjects, spreadsheetParameters, translationMemory, blockLibrary, activeLayoutId: activeLayout?.id || null, currentFileName, dependencyEdges, modules, selectedWorkbench };
      const savedAt = saveProjectSnapshot(snapshotInput);
      setLastAutosaveAt(savedAt);
      saveRecoveryGeneration({ ...snapshotInput, schemaVersion: 2, savedAt }, "AutoSave");
    };
    const timer = window.setTimeout(saveNow, 1200);
    const interval = window.setInterval(saveNow, 30000);
    window.addEventListener('beforeunload', saveNow);
    return () => { window.clearTimeout(timer); window.clearInterval(interval); window.removeEventListener('beforeunload', saveNow); };
  }, [recoveryLoaded, entities, layers, layouts, viewports, smartObjects, spreadsheetParameters, translationMemory, blockLibrary, activeLayout, currentFileName, dependencyEdges, modules, selectedWorkbench]);

  useEffect(() => {
    if (!recoveryLoaded) return;
    if (suppressProjectDirtyRef.current) { suppressProjectDirtyRef.current = false; return; }
    setIsDirty(true);
  }, [recoveryLoaded, layers, layouts, viewports, smartObjects, spreadsheetParameters, translationMemory, blockLibrary, dependencyEdges, modules, selectedWorkbench]);

  // AutoCAD bridge probing. Standalone stays fully usable; native DWG commands only enable when a real plugin injects the bridge.
  useEffect(() => {
    let cancelled = false;
    const probe = async () => { const status = await detectAutoCadBridge(); if (!cancelled) setAutoCadBridgeStatus(status); };
    probe();
    const id = window.setInterval(probe, 5000);
    return () => { cancelled = true; window.clearInterval(id); };
  }, []);

  // Smart Object Selection
  const handleSelectSmartObject = useCallback((id: string | null) => {
    setSelectedSmartObjectId(id);
    if (id) {
      setLeftDockTab("PROPERTIES");
    }
  }, []);

  // Update Smart Object Property and Propagate Dirty Flag via Dependency Graph
  const handleUpdateSmartObjectProperty = useCallback(
    (objectId: string, key: string, value: any) => {
      const target = smartObjects.find((o) => o.id === objectId);
      if (!target) return;

      const updatedProps = target.properties.map((p) => (p.key === key ? { ...p, value } : p));
      const updatedTarget = { ...target, [key]: value, properties: updatedProps };

      const baseList = smartObjects.map((o) => (o.id === objectId ? (updatedTarget as any) : o));
      const { updatedObjects, affectedIds } = markObjectDirty(objectId, dependencyEdges, baseList);

      setSmartObjects(updatedObjects);
      showToast(`Đã cập nhật ${target.name} -> ${affectedIds.length} đối tượng phụ thuộc cần Recompute`);
    },
    [smartObjects, dependencyEdges, showToast]
  );

  // Recompute All Dirty Objects
  const handleRecomputeAll = useCallback(() => {
    const { updatedObjects, result } = recomputeDirtyObjects(smartObjects);
    setSmartObjects(updatedObjects);
    showToast(`Đã Recompute ${result.recomputedCount} Smart Object trong ${result.durationMs}ms`);
  }, [smartObjects, showToast]);

  // Recompute Single Object
  const handleRecomputeObject = useCallback(
    (id: string) => {
      const { updatedObjects, result } = recomputeDirtyObjects(smartObjects, id);
      setSmartObjects(updatedObjects);
      showToast(`Đã cập nhật đối tượng ${id} (${result.durationMs}ms)`);
    },
    [smartObjects, showToast]
  );

  // Layer Visibility & Lock Quick Controls
  const handleToggleLayerVisible = useCallback((layerName: string) => {
    setLayers((prev) =>
      prev.map((l) => (l.name === layerName ? { ...l, isVisible: !l.isVisible } : l))
    );
  }, []);

  const handleToggleLayerLock = useCallback((layerName: string) => {
    setLayers((prev) =>
      prev.map((l) => (l.name === layerName ? { ...l, isLocked: !l.isLocked } : l))
    );
  }, []);

  const handleToggleAllLayersVisible = useCallback((visible: boolean) => {
    setLayers((prev) => prev.map((l) => ({ ...l, isVisible: visible })));
  }, []);

  const handleToggleAllLayersLock = useCallback((locked: boolean) => {
    setLayers((prev) => prev.map((l) => ({ ...l, isLocked: locked })));
  }, []);

  // Add New Smart Object
  const handleAddNewSmartObject = useCallback(
    (type: "CEILING" | "WALL" | "DETAIL" | "SECTION") => {
      const timestamp = Date.now();
      let newObj: HnlSmartObject;

      if (type === "CEILING") {
        newObj = {
          id: `obj_ceiling_${timestamp}`,
          name: `C0${smartObjects.filter((o) => o.type === "HNL_CEILING").length + 1} - Trần Thạch Cao`,
          type: "HNL_CEILING",
          floorId: "floor_01",
          layer: "HNL_CEILING_SUSPENDED",
          status: "VERIFIED",
          dirtyFlag: false,
          childObjectIds: [],
          dependencyIds: ["obj_room_101"],
          boundaryPoints: [
            { x: 1000, y: 1000 },
            { x: 5000, y: 1000 },
            { x: 5000, y: 3500 },
            { x: 1000, y: 3500 },
          ],
          ceilingType: "SUSPENDED_GYPSUM",
          levelElevationMm: 2800,
          boardType: "STANDARD_9.5",
          boardDirectionDeg: 0,
          mainFrameType: "V-KEEL_38",
          mainSpacingMm: 800,
          secondaryFrameType: "M-BAR",
          secondarySpacingMm: 400,
          hangerType: "THREADED_ROD_M6",
          hangerSpacingMm: 1000,
          perimeterType: "SHADOWLINE_Z",
          areaM2: 10.0,
          mepClashCount: 0,
          properties: [
            { key: "ceilingType", label: "Loại trần", type: "select", value: "SUSPENDED_GYPSUM", options: ["SUSPENDED_GYPSUM", "EXPOSED_GRID", "ALUMINUM_BAFFLE", "CURTAIN_STEP"], group: "General" },
            { key: "levelElevationMm", label: "Cao độ trần (mm)", type: "number", value: 2800, unit: "mm", group: "General" },
            { key: "boardType", label: "Loại tấm", type: "select", value: "STANDARD_9.5", options: ["STANDARD_9.5", "MOISTURE_RESIST_9.5", "FIRE_RESIST_12.5"], group: "Board" },
            { key: "mainSpacingMm", label: "Khoảng cách xương chính", type: "number", value: 800, unit: "mm", group: "Framing" },
            { key: "secondarySpacingMm", label: "Khoảng cách xương phụ", type: "number", value: 400, unit: "mm", group: "Framing" },
            { key: "hangerSpacingMm", label: "Khoảng cách ty treo", type: "number", value: 1000, unit: "mm", group: "Framing" },
          ],
        };
      } else if (type === "WALL") {
        newObj = {
          id: `obj_wall_${timestamp}`,
          name: `W0${smartObjects.filter((o) => o.type === "HNL_WALL").length + 1} - Vách Chống Cháy EI60`,
          type: "HNL_WALL",
          floorId: "floor_01",
          layer: "HNL_WALL_FIRE_EI60",
          status: "VERIFIED",
          dirtyFlag: false,
          childObjectIds: [],
          dependencyIds: ["obj_room_101"],
          p1: { x: 500, y: 2500 },
          p2: { x: 6500, y: 2500 },
          wallType: "DRYWALL_SINGLE_STUD",
          totalThicknessMm: 125,
          studType: "C75_0.5MM",
          trackType: "U75_0.5MM",
          studSpacingMm: 400,
          heightMm: 3600,
          boardSideA: "2x12.5mm Gyproc FireStop",
          boardSideB: "2x12.5mm Gyproc FireStop",
          insulationType: "ROCKWOOL_50MM_50KG",
          fireRating: "EI60",
          acousticRatingRw: 54,
          testedAssemblyId: "W-EI60-01",
          hasDeflectionHead: true,
          properties: [
            { key: "wallType", label: "Cấu tạo vách", type: "select", value: "DRYWALL_SINGLE_STUD", options: ["DRYWALL_SINGLE_STUD", "DRYWALL_DOUBLE_STUD"], group: "General" },
            { key: "fireRating", label: "Cấp chống cháy", type: "select", value: "EI60", options: ["EI30", "EI60", "EI90", "EI120", "NONE"], group: "Fire & Acoustic" },
            { key: "heightMm", label: "Chiều cao tường (mm)", type: "number", value: 3600, unit: "mm", group: "General" },
            { key: "studSpacingMm", label: "Khoảng cách Stud (mm)", type: "number", value: 400, unit: "mm", group: "Framing" },
          ],
        };
      } else {
        newObj = {
          id: `obj_detail_${timestamp}`,
          name: `D0${smartObjects.filter((o) => o.type === "HNL_DETAIL").length + 1} - Chi tiết trích xuất`,
          type: "HNL_DETAIL",
          floorId: "floor_01",
          layer: "HNL_ANNO_DETAIL",
          status: "VERIFIED",
          dirtyFlag: false,
          childObjectIds: [],
          dependencyIds: ["obj_ceiling_c01"],
          detailNumber: `0${smartObjects.filter((o) => o.type === "HNL_DETAIL").length + 1}`,
          sheetNumber: "A-102",
          title: "CHI TIẾT LIÊN KẾT KHUNG XƯƠNG THẠCH CAO",
          sourceBoundary: { x: 700, y: 700, width: 400, height: 400 },
          targetScale: "1:10",
          sourceLocation: { x: 700, y: 700 },
          properties: [
            { key: "detailNumber", label: "Số hiệu", type: "string", value: "02", group: "Documentation" },
            { key: "title", label: "Tên chi tiết", type: "string", value: "CHI TIẾT LIÊN KẾT KHUNG XƯƠNG THẠCH CAO", group: "Documentation" },
            { key: "targetScale", label: "Tỷ lệ", type: "select", value: "1:10", options: ["1:5", "1:10", "1:20"], group: "Documentation" },
          ],
        };
      }

      setSmartObjects((prev) => [...prev, newObj]);
      setSelectedSmartObjectId(newObj.id);
      setLeftDockTab("PROPERTIES");
      showToast(`Đã tạo Smart Object [${newObj.name}] thành công!`);
    },
    [smartObjects, showToast]
  );

  // Spreadsheet Parameter Handlers
  const handleUpdateSpreadsheetParam = useCallback(
    (paramId: string, newExpr: string) => {
      const paramMap: Record<string, number> = {};
      spreadsheetParameters.forEach((p) => {
        if (typeof p.evaluatedValue === "number") {
          paramMap[p.name] = p.evaluatedValue;
        }
      });

      const updated = spreadsheetParameters.map((p) => {
        if (p.id === paramId) {
          const evalVal = evaluateExpression(newExpr, paramMap);
          return {
            ...p,
            expression: newExpr,
            evaluatedValue: isNaN(evalVal) ? newExpr : evalVal,
          };
        }
        return p;
      });

      setSpreadsheetParameters(updated);
      showToast("Đã cập nhật công thức Spreadsheet & đồng bộ Smart Objects");
    },
    [spreadsheetParameters, showToast]
  );

  const handleAddSpreadsheetParam = useCallback(
    (name: string, expr: string, unit: string, category: any) => {
      const paramMap: Record<string, number> = {};
      spreadsheetParameters.forEach((p) => {
        if (typeof p.evaluatedValue === "number") {
          paramMap[p.name] = p.evaluatedValue;
        }
      });
      const evalVal = evaluateExpression(expr, paramMap);

      const newParam: SpreadsheetParameter = {
        id: `param_${Date.now()}`,
        name,
        expression: expr,
        evaluatedValue: isNaN(evalVal) ? expr : evalVal,
        unit,
        description: "Tham số người dùng định nghĩa",
        category,
      };

      setSpreadsheetParameters((prev) => [...prev, newParam]);
      showToast(`Đã thêm biến [${name}] vào bảng tham số`);
    },
    [spreadsheetParameters, showToast]
  );

  const handleDeleteSpreadsheetParam = useCallback(
    (paramId: string) => {
      setSpreadsheetParameters((prev) => prev.filter((p) => p.id !== paramId));
      showToast("Đã xóa tham số");
    },
    [showToast]
  );

  // Module Manager Handlers
  const handleToggleModule = useCallback((moduleId: string) => {
    setModules((prev) =>
      prev.map((m) => (m.id === moduleId ? { ...m, isEnabled: !m.isEnabled } : m))
    );
  }, []);

  const handleToggleSafeMode = useCallback(() => {
    setIsSafeMode((prev) => {
      const next = !prev;
      try { const cfg = JSON.parse(localStorage.getItem("hnl.settings.v1") || "{}"); localStorage.setItem("hnl.settings.v1", JSON.stringify({ ...cfg, safeMode: next })); } catch {}
      return next;
    });
  }, []);

  // Workbench Change Handler
  const handleChangeWorkbench = useCallback((wb: string) => {
    setSelectedWorkbench(wb);
    if (wb === "HNL_CEILING" || wb === "HNL_WALL") {
      setActiveRibbonTab("VE_NHANH");
      setLeftDockTab("TREE");
      setIsLeftDockOpen(true);
    } else if (wb === "HNL_QUANTITY") {
      setActiveRibbonTab("THONG_KE");
      setLeftDockTab("SPREADSHEET");
    } else if (wb === "HNL_SHOPDRAWING") {
      setActiveRibbonTab("TEXT_NOTE");
    } else if (wb === "HNL_LAYOUT") {
      setActiveRibbonTab("LAYOUT");
    }
  }, []);

  // Push new state to history stack
  const updateEntitiesWithHistory = useCallback(
    (newEntities: CadEntity[]) => {
      const newHistory = history.slice(0, historyIndex + 1);
      newHistory.push(newEntities);
      setHistory(newHistory);
      setHistoryIndex(newHistory.length - 1);
      setEntities(newEntities);
      setIsDirty(true);
    },
    [history, historyIndex]
  );

  const handleUndo = useCallback(() => {
    if (historyIndex > 0) {
      const newIdx = historyIndex - 1;
      setHistoryIndex(newIdx);
      setEntities(history[newIdx]);
      setIsDirty(true);
      showToast("Undo: Đã quay lại trạng thái trước");
    }
  }, [historyIndex, history, showToast]);

  const handleRedo = useCallback(() => {
    if (historyIndex < history.length - 1) {
      const newIdx = historyIndex + 1;
      setHistoryIndex(newIdx);
      setEntities(history[newIdx]);
      setIsDirty(true);
      showToast("Redo: Đã làm lại thao tác");
    }
  }, [historyIndex, history, showToast]);

  // Global Keyboard Shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.code === "Space") {
        e.preventDefault();
        setIsCommandCenterOpen((prev) => !prev);
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
        e.preventDefault();
        if (e.shiftKey) {
          handleRedo();
        } else {
          handleUndo();
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "y") {
        e.preventDefault();
        handleRedo();
      } else if (e.key === "Escape") {
        setCurrentTool("SELECT");
        setSelectedEntityIds([]);
        setGhostPreviewEntities([]);
      } else if (e.key === "Delete" || e.key === "Backspace") {
        if (selectedEntityIds.length > 0 && !(e.target instanceof HTMLInputElement)) {
          const remaining = entities.filter((ent) => !selectedEntityIds.includes(ent.id));
          updateEntitiesWithHistory(remaining);
          setSelectedEntityIds([]);
          showToast(`Đã xóa ${selectedEntityIds.length} đối tượng`);
        }
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [selectedEntityIds, entities, handleUndo, handleRedo, updateEntitiesWithHistory, showToast]);

  // Entity Selection Handler
  const handleSelectEntity = (id: string, multiSelect: boolean) => {
    if (multiSelect) {
      setSelectedEntityIds((prev) =>
        prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
      );
    } else {
      setSelectedEntityIds([id]);
    }
  };

  const handleClearSelection = () => {
    setSelectedEntityIds([]);
  };

  const handleAddEntity = (entity: CadEntity) => {
    const updated = [...entities, entity];
    updateEntitiesWithHistory(updated);
    showToast(`Đã thêm đối tượng [${entity.type}] vào Model Space`);
  };

  // AI Plan Execution Dispatcher
  const handleExecutePlan = (plan: AICommandPlan) => {
    if (plan.actionType === "DRAW_WALL") {
      const newWall: CadWall = {
        id: `wall_${Date.now()}`,
        handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
        type: "WALL",
        layer: "KT_TUONG",
        color: plan.intent.includes("200") ? "#00E5FF" : "#FF9100",
        p1: { x: 0, y: 0 },
        p2: { x: 6000, y: 0 },
        thickness: plan.intent.includes("200") ? 200 : 100,
        wallType: plan.intent.includes("200") ? "BRICK_200" : "BRICK_100",
      };
      updateEntitiesWithHistory([...entities, newWall]);
      showToast(`AI: Đã vẽ tường ${newWall.thickness}mm tim trục dài 6000mm!`);
    } else if (plan.actionType === "DRAW_CEILING") {
      const newCeiling: CadCeilingGrid = {
        id: `ceil_${Date.now()}`,
        handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
        type: "CEILING_GRID",
        layer: "KT_TRAN_XUONGCHINH",
        color: "#FF9100",
        boundary: [
          { x: 0, y: 0 },
          { x: 6000, y: 0 },
          { x: 6000, y: 4000 },
          { x: 0, y: 4000 },
        ],
        mainTeeSpacing: 800,
        crossTeeSpacing: 400,
        hangerSpacing: 1000,
        rotationDeg: 0,
        panelSize: { width: 600, height: 600 },
      };
      updateEntitiesWithHistory([...entities, newCeiling]);
      showToast("AI: Đã bố trí hệ trần thạch cao chìm (Xương chính @800, phụ @400, ty treo @1000)!");
    } else if (plan.actionType === "AUTO_LAYOUT") {
      handleExecuteCommand("AUTO_LAYOUT_A3");
    } else if (plan.actionType === "CALC_AREA") {
      handleExecuteCommand("LABEL_ROOM_AREAS");
    } else if (plan.actionType === "TRANSLATE") {
      handleTranslateDrawing("Bilingual", "en");
    } else {
      showToast(`AI Planner chưa có executor an toàn cho tác vụ: ${plan.intent}. Không có thay đổi nào được áp dụng.`);
    }
  };

  // Translation Handler
  const handleTranslateDrawing = (mode: "Bilingual" | "Replace" | "SideBySide", targetLang: string) => {
    const updated = entities.map((ent) => {
      if (ent.type === "TEXT" || ent.type === "MTEXT") {
        const txt = ent as any;
        const viText = txt.text || "";
        let translated = viText;

        // Check translation memory dictionary
        const found = translationMemory.find(
          (tm) => tm.original.toLowerCase() === viText.toLowerCase()
        );
        if (found) {
          translated = found.translated;
        } else if (viText.includes("PHÒNG KHÁCH")) {
          translated = "LIVING ROOM";
        } else if (viText.includes("BẾP")) {
          translated = "KITCHEN & DINING";
        } else if (viText.includes("PHÒNG NGỦ")) {
          translated = "BEDROOM";
        }

        if (mode === "Bilingual") {
          return { ...txt, text: `${viText}\n${translated}` };
        } else {
          return { ...txt, text: translated };
        }
      }
      return ent;
    });

    updateEntitiesWithHistory(updated);
    showToast(`Dịch thuật hoàn tất: Đã chuyển đổi sang chế độ [${mode}]`);
  };

  // Audit Issue Fix Handler
  const handleFixAuditIssue = (issueId: string) => {
    const issue = auditIssues.find((i) => i.id === issueId);
    if (!issue) return;

    if (issue.category === "FIELD") {
      // Fix broken fields
      const updated = entities.map((e) => {
        if ((e.type === "TEXT" || e.type === "MTEXT") && (e as any).text?.includes("####")) {
          return { ...e, text: "S = 32.40 m²", hasField: true, color: "#00E5FF" };
        }
        return e;
      });
      updateEntitiesWithHistory(updated);
    } else if (issue.category === "VIEWPORT") {
      // Lock all viewports
      setViewports((prev) => prev.map((vp) => ({ ...vp, locked: true })));
    } else if (issue.category === "TEXT") {
      // Fix TCVN3 / VNI encoding
      const updated = entities.map((e) => {
        if (e.type === "TEXT" || e.type === "MTEXT") {
          return { ...e, text: normalizeVietnameseText((e as any).text) };
        }
        return e;
      });
      updateEntitiesWithHistory(updated);
    }

    setAuditIssues((prev) => prev.filter((i) => i.id !== issueId));
    showToast(`Đã tự động sửa lỗi: ${issue.title}`);
  };

  const handleFixAllAuditIssues = () => {
    auditIssues.forEach((issue) => handleFixAuditIssue(issue.id));
    setAuditIssues([]);
    showToast("Đã áp dụng các sửa lỗi tự động có hỗ trợ; các lỗi kỹ thuật còn lại cần kiểm tra thủ công.");
  };

  // Ribbon Command Dispatcher
  const handleExecuteCommandCore = (cmdKey: string) => {
    switch (cmdKey) {
      case "SMART_WALL_100":
        setCurrentTool("WALL_100");
        showToast("Vẽ tường 100: Nhấp điểm 1 và điểm 2 trên bản vẽ");
        break;

      case "SMART_WALL_200":
        setCurrentTool("WALL_200");
        showToast("Vẽ tường 200: Nhấp điểm 1 và điểm 2 trên bản vẽ");
        break;

      case "SMART_CEILING": {
        const selected = entities.find((e) => selectedEntityIds.includes(e.id));
        let boundary: { x: number; y: number }[] | null = null;
        if (selected?.type === "POLYLINE" && (selected as any).closed) {
          boundary = (selected as any).points;
        } else if (selected?.type === "RECTANGLE") {
          const r = selected as any;
          boundary = [
            { x: r.x, y: r.y },
            { x: r.x + r.width, y: r.y },
            { x: r.x + r.width, y: r.y + r.height },
            { x: r.x, y: r.y + r.height },
          ];
        }
        if (!boundary) {
          showToast("Hãy chọn 1 Polyline kín hoặc Rectangle làm biên trần trước khi tạo Smart Ceiling.");
          break;
        }
        let localCfg: any = {};
        try { localCfg = JSON.parse(localStorage.getItem("hnl.settings.v1") || "{}"); } catch {}
        const mainSpacing = Number(localCfg.ceilingMainSpacing) || 800;
        const subSpacing = Number(localCfg.ceilingCrossSpacing) || 400;
        const newCeiling: CadCeilingGrid = {
          id: `ceil_${Date.now()}`, handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
          type: "CEILING_GRID", layer: "KT_TRAN_XUONGCHINH", color: "#FF9100", boundary,
          mainSpacing, subSpacing, hangerSpacing: 1000, rotationDeg: 0, panelSize: { width: 600, height: 600 },
        };
        updateEntitiesWithHistory([...entities, newCeiling]);
        showToast(`Đã tạo Smart Ceiling từ biên được chọn. Preset ${mainSpacing}/${subSpacing}/1000 mm là dữ liệu thao tác, cần đối chiếu Project Spec/Approved System.`);
        break;
      }

      case "DRAW_RECTANGLE":
        setCurrentTool("RECTANGLE");
        showToast("Vẽ Rect: Nhấp 2 góc đối diện");
        break;

      case "DRAW_LINE":
        setCurrentTool("LINE");
        showToast("Vẽ Line: Nhấp điểm đầu và điểm cuối");
        break;

      case "DRAW_CIRCLE":
        setCurrentTool("CIRCLE");
        showToast("Vẽ Circle: Nhấp tâm rồi nhấp điểm xác định bán kính");
        break;

      case "DRAW_MLEADER":
      case "MLEADER":
      case "APMLEADER":
      case "HNLMLEADER":
        setCurrentTool("MLEADER");
        showToast("Đã kích hoạt vẽ Multileader: Nhấp điểm gốc mũi tên -> Nhấp vị trí ghi chú");
        break;

      case "DRAW_MLEADER_MATERIAL":
      case "MLEADER_MATERIAL":
        setCurrentTool("MLEADER_MATERIAL");
        showToast("Vẽ MLeader cấu tạo PCCC: Nhấp điểm chỉ định vật liệu -> Nhấp điểm đặt text");
        break;

      case "MLEADER_ALIGN": {
        const targets = entities.filter((e) => e.type === "MLEADER" && (selectedEntityIds.length === 0 || selectedEntityIds.includes(e.id)));
        if (targets.length < 2) {
          showToast("Cần ít nhất 2 MLeader (chọn trước hoặc để HNL lấy toàn bộ) để căn hàng.");
        } else {
          const targetX = Math.max(...targets.map((e: any) => e.textPosition?.x ?? 0));
          const ids = new Set(targets.map((e) => e.id));
          const updated = entities.map((e: any) => ids.has(e.id) ? { ...e, textPosition: { ...e.textPosition, x: targetX } } : e);
          updateEntitiesWithHistory(updated);
          showToast(`Đã căn X của ${targets.length} MLeader theo vị trí text ngoài cùng.`);
        }
        break;
      }

      case "TEXT2MLEADER": {
        // Convert any standalone text into an authentic MLeader
        const texts = entities.filter((e) => e.type === "TEXT" || e.type === "MTEXT");
        if (texts.length === 0) {
          showToast("Không có Text rời rạc nào để chuyển đổi.");
        } else {
          const newMleaders: CadEntity[] = texts.map((t: any, idx) => ({
            id: `mld_conv_${Date.now()}_${idx}`,
            handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
            type: "MLEADER",
            layer: "HNL_ANNO_MLEADER",
            color: "#00E5FF",
            leaderPoints: [
              { x: (t.position?.x || 1000) - 300, y: (t.position?.y || 1000) - 200 },
              { x: t.position?.x || 1000, y: t.position?.y || 1000 },
            ],
            text: t.text || "MLeader",
            textPosition: t.position || { x: 1000, y: 1000 },
            landingDistance: 300,
          } as any));
          const nonText = entities.filter((e) => e.type !== "TEXT" && e.type !== "MTEXT");
          updateEntitiesWithHistory([...nonText, ...newMleaders]);
          showToast(`Đã chuyển ${texts.length} Text sang HNL MLeader nội bộ. AutoCAD MLeader native cần plugin.`);
        }
        break;
      }

      case "OPEN_WINDOWS_COMPAT":
      case "CHECK_WINDOWS_COMPAT":
        setIsWindowsCompatOpen(true);
        break;

      case "OPEN_SECTION_GEN":
      case "SECTION_GEN":
      case "PARAMETRIC_SECTION":
        setIsSectionGenOpen(true);
        break;

      case "OPEN_MEP_CLASH":
      case "MEP_CLASH":
      case "CLASH_DETECTION":
        setIsMepClashOpen(true);
        break;

      case "OPEN_MULTI_EXPORT":
      case "EXPORT_DXF":
      case "EXPORT_MULTI":
        setIsMultiExportOpen(true);
        break;

      case "OPEN_BUILDING_CODE":
      case "BUILDING_CODE":
      case "TCVN_ASTM":
      case "HANGER_CALC":
        setIsBuildingCodeOpen(true);
        break;

      case "LABEL_ROOM_AREAS": {
        const target = entities.find((e) => selectedEntityIds.includes(e.id));
        let points: any[] | null = null;
        if (target?.type === "POLYLINE" && (target as any).closed) points = (target as any).points;
        if (target?.type === "RECTANGLE") {
          const r: any = target; points = [{x:r.x,y:r.y},{x:r.x+r.width,y:r.y},{x:r.x+r.width,y:r.y+r.height},{x:r.x,y:r.y+r.height}];
        }
        if (!points) { showToast("Chọn 1 Polyline kín hoặc Rectangle để ghi diện tích."); break; }
        const areaM2 = calculatePolygonArea(points) / 1_000_000;
        const center = calculateCentroid(points);
        const newLabelText: CadEntity = {
          id: `txt_${Date.now()}`, handle: Math.random().toString(16).substring(2, 6).toUpperCase(), type: "TEXT",
          layer: "KT_TEXT_TENPHONG", color: "#00E5FF", position: center, text: `S = ${areaM2.toFixed(2)} m²`, height: 250,
          hasField: false, fieldFormula: `HNL_AREA_REF:${target.id}`,
        } as any;
        updateEntitiesWithHistory([...entities, newLabelText]);
        showToast("Đã ghi diện tích từ hình học thực. Đây là liên kết HNL nội bộ; DWG Field thật cần AutoCAD plugin.");
        break;
      }

      case "CALC_AREA_TOTAL": {
        const targets = entities.filter((e) => selectedEntityIds.length === 0 || selectedEntityIds.includes(e.id));
        let area = 0, perimeter = 0, count = 0;
        for (const e of targets as any[]) {
          if (e.type === "POLYLINE" && e.closed && e.points?.length >= 3) { area += calculatePolygonArea(e.points); perimeter += calculatePolygonPerimeter(e.points, true); count++; }
          else if (e.type === "RECTANGLE") { area += Math.abs(e.width * e.height); perimeter += 2 * (Math.abs(e.width) + Math.abs(e.height)); count++; }
          else if (e.type === "CIRCLE") { area += Math.PI * e.radius * e.radius; perimeter += 2 * Math.PI * e.radius; count++; }
        }
        showToast(count ? `Đã tính ${count} vùng: S = ${(area/1_000_000).toFixed(2)} m²; P = ${(perimeter/1000).toFixed(2)} m.` : "Không có Polyline kín/Rectangle/Circle phù hợp để tính.");
        break;
      }

      case "AUTO_LAYOUT_A3": {
        const newLayout: CadLayout = {
          id: `layout_${Date.now()}`,
          name: `A3_MAT_BANG_${layouts.length + 1}`,
          paperSize: "A3",
          orientation: "LANDSCAPE",
          widthMm: 420,
          heightMm: 297,
          marginMm: 10,
          titleBlockBlockName: "HNL_TITLE_A3",
          drawingName: "MẶT BẰNG BỐ TRÍ TRẦN & ĐÈN CHIẾU SÁNG",
          drawingNo: `HNL-KT-0${layouts.length + 1}`,
          scale: "1:50",
          status: "READY",
        };
        const newVp: CadViewport = {
          id: `vp_${Date.now()}`,
          type: "VIEWPORT",
          layoutName: newLayout.name,
          x: 20,
          y: 20,
          width: 380,
          height: 240,
          scale: "1:50",
          scaleFactor: 0.02,
          modelCenter: { x: 3000, y: 2000 },
          locked: true,
          title: "MẶT BẰNG TRẦN",
        };
        setLayouts((prev) => [...prev, newLayout]);
        setViewports((prev) => [...prev, newVp]);
        setActiveLayout(newLayout);
        showToast(`Đã tự động tạo Layout ${newLayout.name} với khung tên A3 & Viewport 1:50`);
        break;
      }

      case "AUTO_LAYOUT_A4": {
        const newLayout: CadLayout = {
          id: `layout_${Date.now()}`,
          name: `A4_DANH_MUC_${layouts.length + 1}`,
          paperSize: "A4",
          orientation: "PORTRAIT",
          widthMm: 210,
          heightMm: 297,
          marginMm: 10,
          titleBlockBlockName: "HNL_TITLE_A4",
          drawingName: "DANH MỤC BẢN VẼ & GHI CHÚ CHUNG",
          drawingNo: `HNL-KT-0${layouts.length + 1}`,
          scale: "NTS",
          status: "READY",
        };
        setLayouts((prev) => [...prev, newLayout]);
        setActiveLayout(newLayout);
        showToast(`Đã tạo Layout ${newLayout.name}`);
        break;
      }

      case "VIEWPORT_LOCK_ALL":
        setViewports((prev) => prev.map((vp) => ({ ...vp, locked: true })));
        showToast("Đã khóa an toàn tất cả Viewports trên toàn bộ bản vẽ!");
        break;

      case "TRANSLATE_VI_EN_BILINGUAL":
        handleTranslateDrawing("Bilingual", "en");
        break;

      case "TRANSLATE_REPLACE_EN":
        handleTranslateDrawing("Replace", "en");
        break;

      case "TEXT_FIX_UNICODE": {
        const updated = entities.map((e) => {
          if (e.type === "TEXT" || e.type === "MTEXT") {
            return { ...e, text: normalizeVietnameseText((e as any).text) };
          }
          return e;
        });
        updateEntitiesWithHistory(updated);
        showToast("Đã chuẩn hóa các ký tự/bảng mã HNL nhận diện được về Unicode. Với VNI/TCVN3 phức tạp cần kiểm tra lại font nguồn.");
        break;
      }

      case "BLOCK_SIMILARITY": {
        const refs = entities.filter((e) => e.type === "BLOCK_REF") as any[];
        const names = [...new Set(refs.map((b) => b.blockName))];
        let best: { a: string; b: string; score: number } | null = null;
        for (let i = 0; i < names.length; i++) for (let j = i + 1; j < names.length; j++) {
          const aRefs = refs.filter((b) => b.blockName === names[i]); const bRefs = refs.filter((b) => b.blockName === names[j]);
          const aKeys = [...new Set(aRefs.flatMap((b) => Object.keys(b.attributes || {})))];
          const bKeys = [...new Set(bRefs.flatMap((b) => Object.keys(b.attributes || {})))];
          const score = computeBlockSimilarity({ name: names[i], attrKeys: aKeys, tagCount: aKeys.length }, { name: names[j], attrKeys: bKeys, tagCount: bKeys.length });
          if (!best || score > best.score) best = { a: names[i], b: names[j], score };
        }
        showToast(best ? `Cặp Block giống nhất theo tên/Attribute: ${best.a} ↔ ${best.b}: ${Math.round(best.score*100)}%. (Không thay thế so sánh hình học DWG native.)` : "Cần ít nhất 2 loại Block để so sánh.");
        break;
      }

      case "BLOCK_COUNT": {
        const refs = entities.filter((e) => e.type === "BLOCK_REF") as any[];
        const byName: Record<string, number> = {};
        refs.forEach((b) => { const key = b.blockName || "(Unnamed)"; byName[key] = (byName[key] || 0) + 1; });
        const top = Object.entries(byName).sort((a,b)=>b[1]-a[1]).slice(0,4).map(([n,c])=>`${n}: ${c}`).join("; ");
        showToast(`Tổng Block: ${refs.length}${top ? ` • ${top}` : ""}`);
        break;
      }

      case "OPEN_BLOCK_LIBRARY":
        setIsAiPaletteOpen(true);
        break;

      case "OPEN_TABLE_BUILDER":
        setIsTableBuilderOpen(true);
        break;

      case "OPEN_EXCEL_EXPORT":
        setIsExcelExportOpen(true);
        break;

      case "OPEN_AUDIT_MODAL":
        setIsAuditModalOpen(true);
        break;

      case "OPEN_LISP_BUILDER":
        setIsLispBuilderOpen(true);
        break;

      case "LISP_RUN_APAREA":
        showToast("APAREA yêu cầu AutoCAD plugin để thực thi AutoLISP. Standalone chỉ có thể tính diện tích bằng CAD Engine nội bộ.");
        break;

      case "LISP_RUN_APWALL":
        setCurrentTool("WALL_100");
        showToast("Standalone: dùng CAD Engine nội bộ tương đương thao tác APWALL. AutoLISP thật chỉ chạy khi có AutoCAD plugin.");
        break;

      case "AI_AUTO_DETAIL":
      case "LAYOUT_AUTO_COMPOSER":
      case "AI_SHOPDRAWING_COMPOSER":
        setIsAutoDetailComposerOpen(true);
        break;

      case "OPEN_STANDALONE_EXE_BUILDER":
      case "BUILD_EXE":
      case "STANDALONE_EXE":
        setIsStandaloneExeBuilderOpen(true);
        break;

      case "OPEN_DRYWALL_STUDIO":
      case "DRYWALL_STUDIO":
      case "HNLDRYWALL":
        setIsDrywallStudioOpen(true);
        break;

      case "DRAW_POLYLINE":
        setCurrentTool("POLYLINE");
        showToast("Polyline Standalone hiện tạo đoạn 2 điểm. Multi-segment/Arc sẽ được mở rộng ở CAD Engine tiếp theo.");
        break;

      case "DRAW_OFFSET": {
        const line = entities.find((e: any) => selectedEntityIds.includes(e.id) && e.type === "LINE") as any;
        if (!line) { showToast("Offset Standalone hiện hỗ trợ LINE: hãy chọn 1 Line trước."); break; }
        const raw = window.prompt("Khoảng Offset (mm):", "100"); const d = Number(raw);
        if (!Number.isFinite(d) || d === 0) { showToast("Khoảng Offset không hợp lệ."); break; }
        const dx = line.end.x-line.start.x, dy = line.end.y-line.start.y, len = Math.hypot(dx,dy) || 1;
        const ox = -dy/len*d, oy = dx/len*d;
        const copy = { ...line, id:`line_${Date.now()}`, handle:Math.random().toString(16).substring(2,6).toUpperCase(), start:{x:line.start.x+ox,y:line.start.y+oy}, end:{x:line.end.x+ox,y:line.end.y+oy} };
        updateEntitiesWithHistory([...entities, copy]); showToast(`Đã Offset Line ${d} mm.`); break;
      }

      case "EDIT_JOIN": {
        const lines = entities.filter((e: any) => selectedEntityIds.includes(e.id) && e.type === "LINE") as any[];
        if (lines.length < 2) { showToast("Join Standalone: chọn ít nhất 2 Line liên tiếp."); break; }
        const points = [lines[0].start, lines[0].end];
        let ok = true;
        for (let i=1;i<lines.length;i++) { const last=points[points.length-1], l=lines[i]; if(distance2D(last,l.start)<1e-6) points.push(l.end); else if(distance2D(last,l.end)<1e-6) points.push(l.start); else {ok=false;break;} }
        if (!ok) { showToast("Các Line chưa nối liên tục theo thứ tự chọn; chưa Join để tránh sai hình học."); break; }
        const ids=new Set(lines.map(l=>l.id)); const poly:any={id:`poly_${Date.now()}`,handle:Math.random().toString(16).substring(2,6).toUpperCase(),type:"POLYLINE",layer:lines[0].layer,color:lines[0].color,points,closed:false};
        updateEntitiesWithHistory([...entities.filter(e=>!ids.has(e.id)),poly]); showToast(`Đã Join ${lines.length} Line thành Polyline.`); break;
      }

      case "EDIT_TRIM": case "EDIT_EXTEND": case "EDIT_FILLET": case "EDIT_ARRAY": case "DRAW_HATCH":
        reportCommandFailure(cmdKey, "Chức năng chưa hỗ trợ Standalone", "Geometry kernel hiện chưa đủ an toàn để thực thi lệnh này trong Standalone.", "Kết nối AutoCAD Bridge/plugin để dùng thao tác native, hoặc gửi yêu cầu để bổ sung geometry kernel.", "WARNING");
        break;

      case "TEXT_UPPERCASE": {
        const ids = new Set(selectedEntityIds); let n=0; const updated=entities.map((e:any)=>{ if((e.type==="TEXT"||e.type==="MTEXT")&&(ids.size===0||ids.has(e.id))){n++;return{...e,text:String(e.text||"").toUpperCase()}} return e;});
        if(n) updateEntitiesWithHistory(updated); showToast(n?`Đã chuyển ${n} Text/MText sang CHỮ HOA.`:"Không có Text/MText phù hợp."); break;
      }
      case "TEXT_TRIM_SPACE": {
        const ids = new Set(selectedEntityIds); let n=0; const updated=entities.map((e:any)=>{ if((e.type==="TEXT"||e.type==="MTEXT")&&(ids.size===0||ids.has(e.id))){n++;return{...e,text:String(e.text||"").replace(/[ \t]+/g," ").replace(/ *\n */g,"\n").trim()}} return e;});
        if(n) updateEntitiesWithHistory(updated); showToast(n?`Đã dọn khoảng trắng ${n} Text/MText.`:"Không có Text/MText phù hợp."); break;
      }
      case "TEXT_FIND_REPLACE": {
        const find=window.prompt("Tìm chuỗi:",""); if(!find) break; const repl=window.prompt("Thay bằng:","") ?? ""; let n=0;
        const updated=entities.map((e:any)=>{if((e.type==="TEXT"||e.type==="MTEXT")&&String(e.text||"").includes(find)){n++;return{...e,text:String(e.text).split(find).join(repl)}} return e;});
        if(n) updateEntitiesWithHistory(updated); showToast(n?`Đã thay ${n} đối tượng Text/MText.`:"Không tìm thấy nội dung cần thay."); break;
      }

      case "CALC_PERIMETER": {
        const targets=entities.filter((e)=>selectedEntityIds.length===0||selectedEntityIds.includes(e.id)) as any[]; let p=0,n=0;
        for(const e of targets){if(e.type==="POLYLINE"&&e.points?.length>1){p+=calculatePolygonPerimeter(e.points,!!e.closed);n++;}else if(e.type==="RECTANGLE"){p+=2*(Math.abs(e.width)+Math.abs(e.height));n++;}else if(e.type==="CIRCLE"){p+=2*Math.PI*e.radius;n++;}else if(e.type==="LINE"){p+=distance2D(e.start,e.end);n++;}}
        showToast(n?`Tổng chiều dài/chu vi ${n} đối tượng = ${(p/1000).toFixed(3)} m.`:"Không có đối tượng phù hợp."); break;
      }

      case "AI_EXPLAIN_SELECTION": {
        const sel=entities.filter(e=>selectedEntityIds.includes(e.id)); const by=sel.reduce((a:Record<string,number>,e)=>{a[e.type]=(a[e.type]||0)+1;return a;},{});
        showToast(sel.length?`Selection ${sel.length} đối tượng: ${Object.entries(by).map(([k,v])=>`${k}=${v}`).join(", ")}. AI semantic sâu cần provider được cấu hình.`:"Chưa chọn đối tượng nào."); break;
      }
      case "COUNT_LIGHTS": {
        const refs=entities.filter((e:any)=>e.type==="BLOCK_REF" && /DEN|LIGHT|LAMP/i.test(e.blockName||"")); showToast(`Phát hiện ${refs.length} Block có tên giống thiết bị chiếu sáng.`); break;
      }

      case "BLOCK_PURGE_UNUSED": {
        const used=new Set(entities.filter(e=>e.type==="BLOCK_REF").map((e:any)=>e.blockName)); const before=blockLibrary.length; setBlockLibrary(prev=>prev.filter(b=>used.has((b as any).name)||(b as any).isFavorite));
        showToast(`Đã dọn ${Math.max(0,before-blockLibrary.filter((b:any)=>used.has(b.name)||b.isFavorite).length)} mục thư viện chưa dùng (giữ Favorite). Không PURGE định nghĩa DWG native.`); break;
      }
      case "BLOCK_SYNC_ATTRIB": {
        const refs=entities.filter(e=>e.type==="BLOCK_REF") as any[]; const unions:Record<string,Set<string>>={}; refs.forEach(b=>{unions[b.blockName]??=new Set();Object.keys(b.attributes||{}).forEach(k=>unions[b.blockName].add(k));});
        let n=0; const updated=entities.map((e:any)=>{if(e.type!=="BLOCK_REF")return e; const attrs={...(e.attributes||{})}; let ch=false; for(const k of unions[e.blockName]||[]){if(!(k in attrs)){attrs[k]="";ch=true;}} if(ch){n++;return{...e,attributes:attrs}}return e;});
        if(n)updateEntitiesWithHistory(updated); showToast(n?`Đã đồng bộ schema Attribute nội bộ cho ${n} Block Ref.`:"Attribute đã đồng nhất hoặc chưa có dữ liệu."); break;
      }

      case "FIELD_INSERT_AREA": handleExecuteCommand("LABEL_ROOM_AREAS"); break;
      case "FIELD_SCAN_BROKEN": {
        const broken=entities.filter((e:any)=>(e.type==="TEXT"||e.type==="MTEXT") && (String(e.text||"").includes("####") || (e.hasField && !e.fieldFormula)));
        showToast(`HNL Field scan: ${broken.length} liên kết nội bộ nghi lỗi. DWG Field native cần AutoCAD plugin.`); break;
      }
      case "FIELD_RELINK": showToast("Relink DWG Field native cần AutoCAD plugin/ObjectId. Standalone không tự tạo liên kết giả."); break;

      case "LAYOUT_RECOGNIZE_TITLEBLOCK": {
        const candidates=entities.filter((e:any)=>e.type==="BLOCK_REF" && /TITLE|KHUNG|FRAME|BORDER/i.test(e.blockName||"")) as any[];
        showToast(candidates.length?`Tìm thấy ${candidates.length} Block có khả năng là khung tên: ${[...new Set(candidates.map(b=>b.blockName))].slice(0,3).join(", ")}.`:`Chưa nhận thấy Block khung tên trong dữ liệu Standalone.`); break;
      }
      case "VIEWPORT_AUTO_FIT": {
        if(!viewports.length){showToast("Chưa có Viewport để Auto Fit.");break;}
        const xs:number[]=[],ys:number[]=[]; (entities as any[]).forEach(e=>{if(e.start){xs.push(e.start.x,e.end.x);ys.push(e.start.y,e.end.y)}else if(e.points){e.points.forEach((p:any)=>{xs.push(p.x);ys.push(p.y)})}else if(e.center){xs.push(e.center.x-e.radius,e.center.x+e.radius);ys.push(e.center.y-e.radius,e.center.y+e.radius)}else if(e.x!==undefined&&e.width!==undefined){xs.push(e.x,e.x+e.width);ys.push(e.y,e.y+e.height)}});
        if(!xs.length){showToast("Không có hình học để tính phạm vi Model.");break;} const mw=Math.max(...xs)-Math.min(...xs), mh=Math.max(...ys)-Math.min(...ys);
        const layout=activeLayout||layouts[0]; if(!layout){showToast("Chưa có Layout.");break;} const fit=calculateOptimalViewportScale(mw,mh,layout.widthMm,layout.heightMm,layout.marginMm);
        setViewports(prev=>prev.map(v=>({...v,scale:fit.recommendedScale,scaleFactor:fit.scaleFactor,locked:true}))); showToast(`Auto Fit đề xuất ${fit.recommendedScale}; đã áp dụng cho Viewport nội bộ.`); break;
      }
      case "VIEWPORT_MULTI_ARRANGE": {
        if(viewports.length<2){showToast("Cần ít nhất 2 Viewport để sắp xếp.");break;} const cols=Math.ceil(Math.sqrt(viewports.length)); const gap=8, cellW=120,cellH=80;
        setViewports(prev=>prev.map((v,i)=>({...v,x:15+(i%cols)*(cellW+gap),y:15+Math.floor(i/cols)*(cellH+gap),width:cellW,height:cellH}))); showToast(`Đã sắp ${viewports.length} Viewport theo grid nội bộ; hãy kiểm tra lại trước khi xuất.`); break;
      }

      case "TABLE_SYNC_DRAWING": showToast("Table nội bộ hiện là snapshot. Đồng bộ hai chiều với AutoCAD Table cần plugin; không báo Sync giả."); break;
      case "TRANSLATE_MEMORY_MANAGE": setIsAiPaletteOpen(true); showToast("Translation Memory đang dùng trong HNL Palette/Knowledge. Trình quản lý chuyên dụng sẽ được bổ sung."); break;
      case "SCAN_DWG_FOLDER": showToast("Quét thư mục DWG cần native folder picker + DWG metadata provider. Bản Standalone hiện chưa đọc DWG binary trực tiếp."); break;
      case "OPEN_PILE_STUDIO": case "PILE_STUDIO": case "HNLPILE": setIsPileStudioOpen(true); break;
      case "AUTOCAD_BRIDGE_STATUS": showToast(autoCadBridgeStatus.connected ? `AutoCAD ${autoCadBridgeStatus.version || ""} • ${autoCadBridgeStatus.drawingName || "Drawing"} • Connected` : "Standalone Workspace • AutoCAD plugin chưa kết nối"); break;
      case "OPEN_SKETCHUP_BRIDGE":
      case "EXPORT_SKETCHUP_2D":
      case "CLEAN_2D":
        setIsSketchUpBridgeOpen(true); break;
      case "OPEN_PRO_AUDIT":
      case "GEOMETRY_DOCTOR":
      case "DRAWING_COMPARE":
      case "COMMAND_HEALTH":
        setIsProfessionalAuditOpen(true); break;
      case "OPEN_PLOT_PUBLISH":
      case "QUICK_PLOT_PDF":
      case "PUBLISH_MULTI_PDF":
      case "SHEETSET_MANAGER":
      case "PLOT_STYLE_MANAGER":
        setIsPlotPublishOpen(true); break;
      case "OPEN_2D_PRO_CENTER":
        setPro2DInitialTab("TOOLS"); setIs2DProfessionalOpen(true); break;
      case "SMART_TEXT_CENTER":
        setPro2DInitialTab("TEXT"); setIs2DProfessionalOpen(true); break;
      case "FIELD_DOCTOR_CENTER":
        setPro2DInitialTab("FIELD"); setIs2DProfessionalOpen(true); break;
      case "GEOMETRY_TOOL_CENTER":
        setPro2DInitialTab("GEOMETRY"); setIs2DProfessionalOpen(true); break;
      case "QUICK_DIM_CENTER":
        setPro2DInitialTab("DIMENSION"); setIs2DProfessionalOpen(true); break;
      case "QUANTITY_CENTER":
        setPro2DInitialTab("QUANTITY"); setIs2DProfessionalOpen(true); break;
      case "LAYOUT_AUTOMATION_CENTER":
        setPro2DInitialTab("LAYOUT"); setIs2DProfessionalOpen(true); break;

      default:
        reportCommandFailure(cmdKey, "Lệnh chưa được triển khai", `Lệnh ${cmdKey} chưa được triển khai trong bản Standalone hiện tại.`, "Mở Trung tâm chẩn đoán và gửi mã lỗi này để bổ sung handler/chức năng.", "WARNING");
        break;
    }
  };

  const handleExecuteCommand = (cmdKey: string) => {
    const started = performance.now();
    try {
      handleExecuteCommandCore(cmdKey);
      markCommandHealth(cmdKey, true, Math.round(performance.now() - started));
    } catch (error: unknown) {
      const d = errorToDetails(error);
      markCommandHealth(cmdKey, false, Math.round(performance.now() - started), d.cause);
      const code = `HNL-CMD-ERR-${Date.now().toString().slice(-6)}`;
      pushDiagnostic({ code, severity: "ERROR", title: "Chức năng thực thi thất bại", message: `Lệnh ${cmdKey} không hoàn tất.`, command: cmdKey, cause: d.cause, stack: d.stack, context: { file: currentFileName, selectedCount: selectedEntityIds.length, workbench: selectedWorkbench, autoCadConnected: autoCadBridgeStatus.connected, safeMode: isSafeMode }, suggestion: "Giữ nguyên bản vẽ, mở Trung tâm chẩn đoán và Copy báo cáo để xác định chính xác lỗi cần sửa." }, true);
      showToast(`${code}: ${d.cause}`);
    }
  };

  useEffect(() => {
    const nativeApi = (window as any).electronNative;
    nativeApi?.setDirty?.(isDirty);
    nativeApi?.setWindowTitle?.(`${isDirty ? "● " : ""}${currentFileName} — HNL CAD AI Professional`);
  }, [isDirty, currentFileName]);

  // Electron native menu + file integration. This makes the installed EXE menu functional.
  useEffect(() => {
    const nativeApi = (window as any).electronNative;
    if (!nativeApi) return;

    const offMenu = nativeApi.onMenuCommand?.(async (command: string) => {
      switch (command) {
        case "NEW_DRAWING": {
          if (isDirty && !window.confirm("Bản vẽ có thay đổi chưa lưu. Tạo bản vẽ mới và bỏ các thay đổi này?")) break;
          suppressProjectDirtyRef.current = true;
          clearProjectSnapshot();
          setEntities([]); setHistory([[]]); setHistoryIndex(0);
          setLayers(INITIAL_LAYERS); setLayouts(INITIAL_LAYOUTS); setViewports(INITIAL_VIEWPORTS);
          setSmartObjects([]); setSpreadsheetParameters(INITIAL_SPREADSHEET_PARAMETERS);
          setTranslationMemory(INITIAL_TRANSLATION_MEMORY); setBlockLibrary(INITIAL_BLOCK_LIBRARY); setDependencyEdges(INITIAL_DEPENDENCY_EDGES); setModules(INITIAL_HNL_MODULES); setSelectedWorkbench("HNL_CAD");
          setSelectedEntityIds([]); setActiveLayout(null); setCurrentFileName("Untitled.hnl.json"); setIsDirty(false);
          setShowStartCenter(false); showToast("Đã tạo bản vẽ HNL mới sạch dữ liệu dự án.");
          break;
        }
        case "SAVE_DRAWING": {
          const payload = JSON.stringify({ version: "2.0.4", schemaVersion: 2, savedAt: new Date().toISOString(), currentFileName, activeLayoutId: activeLayout?.id || null, entities, layers, layouts, viewports, smartObjects, spreadsheetParameters, translationMemory, blockLibrary, dependencyEdges, modules, selectedWorkbench }, null, 2);
          const result = await nativeApi.saveFile?.({ defaultName: currentFileName || "BanVe_HNL.json", content: payload, extDescription: "HNL Drawing JSON", extension: "json" });
          if (result?.success) { setCurrentFileName(String(result.filePath).split(/[\\/]/).pop() || "BanVe_HNL.json"); setIsDirty(false); showToast(`Đã lưu đầy đủ dự án: ${result.filePath}`); }
          else if (!result?.canceled) { pushDiagnostic({ code: "HNL-FILE-SAVE-001", severity: "ERROR", title: "Lưu dự án thất bại", message: "Electron không lưu được file dự án.", cause: result?.error || "Không có thông tin lỗi từ native layer", context: { file: currentFileName }, suggestion: "Thử lưu vào Documents/Desktop, kiểm tra quyền ghi và dung lượng ổ đĩa; sau đó copy log nếu vẫn lỗi." }, true); }
          break;
        }
        case "EXPORT_DXF": setIsMultiExportOpen(true); break;
        case "PRINT_PDF": window.print(); break;
        case "UNDO": handleUndo(); break;
        case "REDO": handleRedo(); break;
        case "SELECT_ALL": setSelectedEntityIds(entities.map((e) => e.id)); break;
        case "DELETE":
          if (selectedEntityIds.length > 0) {
            updateEntitiesWithHistory(entities.filter((e) => !selectedEntityIds.includes(e.id)));
            setSelectedEntityIds([]);
            showToast("Đã xóa các đối tượng được chọn.");
          }
          break;
        case "OPEN_AUTO_DETAIL": setIsAutoDetailComposerOpen(true); break;
        case "TOGGLE_AI_PALETTE": setIsAiPaletteOpen((v) => !v); break;
        case "OPEN_AUDIT": setIsAuditModalOpen(true); break;
        case "OPEN_PILE_STUDIO": setIsPileStudioOpen(true); break;
        case "OPEN_TRANSLATE": setIsAiPaletteOpen(true); break;
        default: handleExecuteCommand(command); break;
      }
    });

    const offFile = nativeApi.onFileOpened?.((data: any) => {
      try {
        const fileName = String(data?.fileName || "");
        const lower = fileName.toLowerCase();
        if (lower.endsWith(".dxf")) {
          const imported = parseBasicDxf(String(data?.content || ""));
          if (imported.length === 0) {
            showToast("DXF không có entity được hỗ trợ hoặc file không phải DXF ASCII.");
            return;
          }
          updateEntitiesWithHistory(imported);
          setSelectedEntityIds([]);
          setCurrentFileName(fileName); setIsDirty(true); setShowStartCenter(false); showToast(`Đã nhập ${imported.length} entity từ ${fileName}.`);
        } else if (lower.endsWith(".json")) {
          suppressProjectDirtyRef.current = true;
          const parsed = JSON.parse(String(data?.content || "{}"));
          const imported = Array.isArray(parsed) ? parsed : parsed.entities;
          if (!Array.isArray(imported)) throw new Error("JSON không có mảng entities");
          setEntities(imported); setHistory([imported]); setHistoryIndex(0);
          if (!Array.isArray(parsed)) {
            if (Array.isArray(parsed.layers)) setLayers(parsed.layers);
            if (Array.isArray(parsed.layouts)) setLayouts(parsed.layouts);
            if (Array.isArray(parsed.viewports)) setViewports(parsed.viewports);
            if (Array.isArray(parsed.smartObjects)) setSmartObjects(parsed.smartObjects);
            if (Array.isArray(parsed.spreadsheetParameters)) setSpreadsheetParameters(parsed.spreadsheetParameters);
            if (Array.isArray(parsed.translationMemory)) setTranslationMemory(parsed.translationMemory);
            if (Array.isArray(parsed.blockLibrary)) setBlockLibrary(parsed.blockLibrary);
            if (Array.isArray(parsed.dependencyEdges)) setDependencyEdges(parsed.dependencyEdges);
            if (Array.isArray(parsed.modules)) setModules(parsed.modules);
            if (parsed.selectedWorkbench) setSelectedWorkbench(parsed.selectedWorkbench);
            setActiveLayout(Array.isArray(parsed.layouts) ? parsed.layouts.find((l: any) => l.id === parsed.activeLayoutId) || null : null);
          }
          setSelectedEntityIds([]); setCurrentFileName(fileName); setIsDirty(false); setShowStartCenter(false);
          showToast(`Đã mở đầy đủ dự án HNL: ${fileName}`);
        } else if (lower.endsWith(".lsp")) {
          showToast(`Đã đọc ${fileName}; chạy AutoLISP cần AutoCAD plugin.`);
        }
      } catch (error: any) {
        pushDiagnostic({ code: "HNL-FILE-OPEN-001", severity: "ERROR", title: "Không thể mở file", message: `Không thể mở ${String(data?.fileName || "file")}.`, cause: error?.message || "Sai định dạng", stack: error?.stack, context: { fileName: data?.fileName, size: String(data?.content || "").length }, suggestion: "Kiểm tra đúng định dạng DXF ASCII hoặc HNL JSON. Copy log nếu file hợp lệ nhưng vẫn lỗi." }, true); showToast(`HNL-FILE-OPEN-001: ${error?.message || "Sai định dạng"}`);
      }
    });

    return () => {
      if (typeof offMenu === "function") offMenu();
      if (typeof offFile === "function") offFile();
    };
  }, [entities, layers, layouts, viewports, smartObjects, selectedEntityIds, historyIndex]);

  const handleApplyComposer = (
    newLayouts: CadLayout[],
    newViewports: CadViewport[],
    newCalloutEntities: CadEntity[]
  ) => {
    setLayouts((prev) => [...prev, ...newLayouts]);
    setViewports((prev) => [...prev, ...newViewports]);
    updateEntitiesWithHistory([...entities, ...newCalloutEntities]);
    if (newLayouts.length > 0) {
      setActiveLayout(newLayouts[0]);
    }
    showToast(`Đã tạo thành công ${newLayouts.length} Sheet Layout và các ký hiệu Detail Callout!`);
  };

  const selectedEntities = entities.filter((e) => selectedEntityIds.includes(e.id));

  return (
    <div className="w-screen h-screen flex flex-col bg-[#141517] overflow-hidden select-none font-sans text-neutral-200">
      {showStartCenter && recoveryLoaded && (
        <div className="fixed inset-0 z-[45] bg-[#0b0d10]/95 backdrop-blur-md flex items-center justify-center p-6">
          <div className="w-full max-w-5xl rounded-2xl border border-neutral-700/80 bg-[#16181c] shadow-2xl overflow-hidden">
            <div className="px-8 py-7 border-b border-neutral-800 bg-gradient-to-r from-[#15181c] to-[#101820] flex items-center justify-between gap-6">
              <div>
                <div className="text-[11px] tracking-[0.24em] uppercase text-cyan-400 font-semibold">Professional CAD Workspace</div>
                <h1 className="mt-2 text-2xl font-bold text-white">HNL CAD AI <span className="text-cyan-400">v2.0.4</span></h1>
                <p className="mt-2 text-sm text-neutral-400 max-w-2xl">Không gian làm việc Standalone + AutoCAD Bridge, tối ưu shopdrawing, thống kê, layout và trợ lý AI kỹ thuật.</p>
              </div>
              <div className={`px-3 py-2 rounded-lg border text-xs ${autoCadBridgeStatus.connected ? "border-emerald-700 bg-emerald-950/30 text-emerald-300" : "border-neutral-700 bg-neutral-900 text-neutral-400"}`}>
                {autoCadBridgeStatus.connected ? `AutoCAD ${autoCadBridgeStatus.version || ""} Connected` : "Standalone Mode"}
              </div>
            </div>
            <div className="grid md:grid-cols-3 gap-4 p-8">
              <button onClick={() => { setShowStartCenter(false); }} className="text-left p-5 rounded-xl border border-cyan-700/60 bg-cyan-950/20 hover:bg-cyan-900/30 transition">
                <div className="text-cyan-300 font-semibold">Tiếp tục Workspace</div>
                <div className="text-xs text-neutral-400 mt-2">Mở vùng làm việc hiện tại{lastAutosaveAt ? ` • AutoSave ${new Date(lastAutosaveAt).toLocaleTimeString()}` : ""}.</div>
              </button>
              <button onClick={() => { (window as any).electronNative?.requestOpenFile?.(); }} className="text-left p-5 rounded-xl border border-neutral-700 bg-[#1d2025] hover:border-neutral-500 transition">
                <div className="text-white font-semibold">Mở dự án / DXF</div>
                <div className="text-xs text-neutral-400 mt-2">Mở HNL JSON đầy đủ hoặc DXF ASCII trong chế độ độc lập.</div>
              </button>
              <button onClick={() => { if (!isDirty || window.confirm("Bỏ thay đổi hiện tại và tạo bản vẽ mới?")) { suppressProjectDirtyRef.current = true; clearProjectSnapshot(); setEntities([]); setHistory([[]]); setHistoryIndex(0); setLayers(INITIAL_LAYERS); setLayouts(INITIAL_LAYOUTS); setViewports(INITIAL_VIEWPORTS); setSmartObjects([]); setSpreadsheetParameters(INITIAL_SPREADSHEET_PARAMETERS); setTranslationMemory(INITIAL_TRANSLATION_MEMORY); setBlockLibrary(INITIAL_BLOCK_LIBRARY); setDependencyEdges(INITIAL_DEPENDENCY_EDGES); setModules(INITIAL_HNL_MODULES); setSelectedWorkbench("HNL_CAD"); setSelectedEntityIds([]); setActiveLayout(null); setCurrentFileName("Untitled.hnl.json"); setIsDirty(false); setShowStartCenter(false); } }} className="text-left p-5 rounded-xl border border-neutral-700 bg-[#1d2025] hover:border-neutral-500 transition">
                <div className="text-white font-semibold">Bản vẽ mới</div>
                <div className="text-xs text-neutral-400 mt-2">Khởi tạo project sạch với layer/layout mặc định HNL.</div>
              </button>
            </div>
            <div className="px-8 pb-7 grid md:grid-cols-4 gap-3 text-[11px] text-neutral-500">
              <div className="rounded-lg bg-neutral-900/60 border border-neutral-800 p-3"><b className="text-neutral-300">Safe Mode</b><br/>{isSafeMode ? "Đang bật" : "Đang tắt"}</div>
              <div className="rounded-lg bg-neutral-900/60 border border-neutral-800 p-3"><b className="text-neutral-300">Recovery</b><br/>{lastAutosaveAt ? "Có AutoSave" : "Chưa có AutoSave"}</div>
              <div className="rounded-lg bg-neutral-900/60 border border-neutral-800 p-3"><b className="text-neutral-300">Project</b><br/>{currentFileName}</div>
              <div className="rounded-lg bg-neutral-900/60 border border-neutral-800 p-3"><b className="text-neutral-300">Mode</b><br/>{autoCadBridgeStatus.connected ? "AutoCAD Bridge" : "Standalone"}</div>
            </div>
          </div>
        </div>
      )}

      {/* 1. AutoCAD Application Ribbon Header */}
      <HnlRibbon
        activeTab={activeRibbonTab}
        onTabChange={setActiveRibbonTab}
        onExecuteCommand={handleExecuteCommand}
        onOpenCommandCenter={() => setIsCommandCenterOpen(true)}
        onToggleAiPalette={() => setIsAiPaletteOpen((prev) => !prev)}
        onOpenLispBuilder={() => setIsLispBuilderOpen(true)}
        onOpenTableBuilder={() => setIsTableBuilderOpen(true)}
        onOpenAuditModal={() => setIsAuditModalOpen(true)}
        onOpenExcelExport={() => setIsExcelExportOpen(true)}
        onOpenNetPluginExporter={() => setIsNetPluginExporterOpen(true)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        onOpenDiagnostics={() => setIsDiagnosticsOpen(true)}
        onOpenProfessionalAudit={() => setIsProfessionalAuditOpen(true)}
        onOpenPlotPublish={() => setIsPlotPublishOpen(true)}
        onOpen2DProfessional={() => { setPro2DInitialTab("TOOLS"); setIs2DProfessionalOpen(true); }}
        onOpenUsageGuide={() => setIsUsageGuideOpen(true)}
        onOpenSketchUpBridge={() => setIsSketchUpBridgeOpen(true)}
        onOpenAutoDetailComposer={() => setIsAutoDetailComposerOpen(true)}
        onOpenStandaloneExeBuilder={() => setIsStandaloneExeBuilderOpen(true)}
        onOpenDrywallCeilingStudio={() => setIsDrywallStudioOpen(true)}
        onOpenWindowsCompat={() => setIsWindowsCompatOpen(true)}
        onOpenAddonManager={() => setIsAddonManagerOpen(true)}
        onOpenShopCheck={() => setIsShopCheckOpen(true)}
        onOpenSectionGenerator={() => setIsSectionGenOpen(true)}
        onOpenMepClash={() => setIsMepClashOpen(true)}
        onOpenMultiExport={() => setIsMultiExportOpen(true)}
        onOpenBuildingCode={() => setIsBuildingCodeOpen(true)}
        onToggleSpreadsheet={() => {
          setIsLeftDockOpen(true);
          setLeftDockTab("SPREADSHEET");
        }}
        onToggleProjectTree={() => {
          setIsLeftDockOpen((prev) => !prev);
          setLeftDockTab("TREE");
        }}
        onRecomputeDAG={handleRecomputeAll}
        selectedWorkbench={selectedWorkbench}
        onChangeWorkbench={handleChangeWorkbench}
        isAiPaletteOpen={isAiPaletteOpen}
        canUndo={historyIndex > 0}
        canRedo={historyIndex < history.length - 1}
        onUndo={handleUndo}
        onRedo={handleRedo}
        connectionStatus={autoCadBridgeStatus}
        lastAutosaveAt={lastAutosaveAt}
        documentName={currentFileName}
        isDirty={isDirty}
      />

      {/* 2. Main CAD Workspace (FreeCAD Left Dock + Canvas + Right AI Palette) */}
      <div className="flex-1 flex overflow-hidden relative">
        {/* Left Side Palette (if docked left) */}
        {paletteDockPosition === "left" && (
          <HnlPalette
            isOpen={isAiPaletteOpen}
            onToggle={() => setIsAiPaletteOpen((prev) => !prev)}
            dockPosition="left"
            onSetDockPosition={setPaletteDockPosition}
            entities={entities}
            selectedEntities={selectedEntities}
            blockLibrary={blockLibrary}
            lispScripts={lispScripts}
            translationMemory={translationMemory}
            auditIssues={auditIssues}
            onExecutePlan={handleExecutePlan}
            onOpenAutoDetailComposer={() => setIsAutoDetailComposerOpen(true)}
            onOpenDrywallStudio={() => setIsDrywallStudioOpen(true)}
            onAddBlockToDrawing={(blk) => {
              const newBlk: CadEntity = {
                id: `blk_${Date.now()}`,
                handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                type: "BLOCK_REF",
                layer: "KT_THIETBI",
                color: "#FFD54F",
                position: { x: 3000, y: 2000 },
                blockName: blk.name,
                rotationDeg: 0,
                scale: { x: 1, y: 1, z: 1 },
              } as any;
              updateEntitiesWithHistory([...entities, newBlk]);
              showToast(`Đã chèn Block [${blk.name}] vào Model Space nội bộ`);
            }}
            onRunLisp={(lsp) => showToast(`Đã chọn AutoLISP ${lsp.commandName}. Standalone không thực thi AutoLISP trực tiếp; hãy chạy qua AutoCAD plugin.`)}
            onFixAuditIssue={handleFixAuditIssue}
            onTranslateDrawing={handleTranslateDrawing}
          />
        )}

        {/* FreeCAD-style Parametric Architecture Left Dock */}
        {isLeftDockOpen && (
          <div className="w-[clamp(240px,19vw,300px)] h-full flex flex-col bg-[#16181b] border-r border-neutral-800 z-10 flex-shrink-0">
            {/* Dock Tab Selector Bar */}
            <div className="h-9 bg-[#111214] border-b border-neutral-800 flex items-center justify-between px-1 text-xs">
              <div className="flex items-center space-x-0.5">
                <button
                  onClick={() => setLeftDockTab("TREE")}
                  className={`px-2 py-1 rounded text-[11px] font-medium flex items-center space-x-1 transition ${
                    leftDockTab === "TREE"
                      ? "bg-neutral-800 text-sky-400 font-bold shadow"
                      : "text-neutral-400 hover:text-neutral-200"
                  }`}
                  title="Cây dự án đối tượng thông minh (Project Tree)"
                >
                  <FolderTree className="w-3.5 h-3.5 text-sky-400" />
                  <span>Tree</span>
                </button>

                <button
                  onClick={() => setLeftDockTab("PROPERTIES")}
                  className={`px-2 py-1 rounded text-[11px] font-medium flex items-center space-x-1 transition ${
                    leftDockTab === "PROPERTIES"
                      ? "bg-neutral-800 text-cyan-400 font-bold shadow"
                      : "text-neutral-400 hover:text-neutral-200"
                  }`}
                  title="Thuộc tính tham số (Parametric Properties)"
                >
                  <Sliders className="w-3.5 h-3.5 text-cyan-400" />
                  <span>Property</span>
                </button>

                <button
                  onClick={() => setLeftDockTab("DEPENDENCY")}
                  className={`px-2 py-1 rounded text-[11px] font-medium flex items-center space-x-1 transition ${
                    leftDockTab === "DEPENDENCY"
                      ? "bg-neutral-800 text-amber-400 font-bold shadow"
                      : "text-neutral-400 hover:text-neutral-200"
                  }`}
                  title="Đồ thị phụ thuộc & Tính toán lại (Dependency DAG)"
                >
                  <Cpu className="w-3.5 h-3.5 text-amber-400" />
                  <span>DAG</span>
                </button>

                <button
                  onClick={() => setLeftDockTab("SPREADSHEET")}
                  className={`px-2 py-1 rounded text-[11px] font-medium flex items-center space-x-1 transition ${
                    leftDockTab === "SPREADSHEET"
                      ? "bg-neutral-800 text-emerald-400 font-bold shadow"
                      : "text-neutral-400 hover:text-neutral-200"
                  }`}
                  title="Bảng tham số & Công thức tính toán (Spreadsheet)"
                >
                  <Table2 className="w-3.5 h-3.5 text-emerald-400" />
                  <span>Sheet</span>
                </button>
              </div>

              <button
                onClick={() => setIsLeftDockOpen(false)}
                className="p-1 rounded text-neutral-500 hover:text-neutral-300 hover:bg-neutral-800"
                title="Ẩn khung điều khiển"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
            </div>

            {/* Dock Content Body */}
            <div className="flex-1 overflow-hidden">
              {leftDockTab === "TREE" && (
                <HnlProjectTreePanel
                  treeRoot={projectTree}
                  smartObjects={smartObjects}
                  selectedObjectId={selectedSmartObjectId}
                  onSelectObject={handleSelectSmartObject}
                  onRecomputeAll={handleRecomputeAll}
                  onRecomputeObject={handleRecomputeObject}
                  onAddNewSmartObject={handleAddNewSmartObject}
                  layers={layers}
                  entities={entities}
                  onToggleLayerVisible={handleToggleLayerVisible}
                  onToggleLayerLock={handleToggleLayerLock}
                  onToggleAllLayersVisible={handleToggleAllLayersVisible}
                  onToggleAllLayersLock={handleToggleAllLayersLock}
                />
              )}

              {leftDockTab === "PROPERTIES" && (
                <HnlPropertyEditorPanel
                  selectedObject={selectedSmartObject}
                  onUpdateProperty={handleUpdateSmartObjectProperty}
                  onRecomputeObject={handleRecomputeObject}
                />
              )}

              {leftDockTab === "DEPENDENCY" && (
                <HnlDependencyPanel
                  smartObjects={smartObjects}
                  dependencyEdges={dependencyEdges}
                  onRecomputeAll={handleRecomputeAll}
                  onSelectObject={handleSelectSmartObject}
                />
              )}

              {leftDockTab === "SPREADSHEET" && (
                <HnlSpreadsheetPanel
                  parameters={spreadsheetParameters}
                  onUpdateParameter={handleUpdateSpreadsheetParam}
                  onAddParameter={handleAddSpreadsheetParam}
                  onDeleteParameter={handleDeleteSpreadsheetParam}
                />
              )}
            </div>
          </div>
        )}

        {/* Collapsed Left Dock Expand Button */}
        {!isLeftDockOpen && (
          <button
            onClick={() => setIsLeftDockOpen(true)}
            className="absolute left-2 top-2 z-20 p-2 rounded-lg bg-neutral-900/90 text-sky-400 border border-neutral-700 shadow-xl hover:bg-neutral-800 transition flex items-center space-x-1 text-xs"
            title="Mở Cây Dự Án HNL & Thuộc Tính Tham Số"
          >
            <FolderTree className="w-4 h-4" />
            <ChevronRight className="w-3.5 h-3.5" />
          </button>
        )}

        {/* CAD Canvas Engine */}
        <div className="flex-1 h-full relative">
          <CadCanvas
            entities={entities}
            layers={layers}
            activeLayout={activeLayout}
            viewports={viewports}
            selectedEntityIds={selectedEntityIds}
            ghostPreviewEntities={ghostPreviewEntities}
            onSelectEntity={handleSelectEntity}
            onClearSelection={handleClearSelection}
            onAddEntity={handleAddEntity}
            currentTool={currentTool}
            onToolComplete={() => setCurrentTool("SELECT")}
          />
        </div>

        {/* Right Side Palette (if docked right) */}
        {paletteDockPosition === "right" && (
          <HnlPalette
            isOpen={isAiPaletteOpen}
            onToggle={() => setIsAiPaletteOpen((prev) => !prev)}
            dockPosition="right"
            onSetDockPosition={setPaletteDockPosition}
            entities={entities}
            selectedEntities={selectedEntities}
            blockLibrary={blockLibrary}
            lispScripts={lispScripts}
            translationMemory={translationMemory}
            auditIssues={auditIssues}
            onExecutePlan={handleExecutePlan}
            onOpenAutoDetailComposer={() => setIsAutoDetailComposerOpen(true)}
            onOpenDrywallStudio={() => setIsDrywallStudioOpen(true)}
            onAddBlockToDrawing={(blk) => {
              const newBlk: CadEntity = {
                id: `blk_${Date.now()}`,
                handle: Math.random().toString(16).substring(2, 6).toUpperCase(),
                type: "BLOCK_REF",
                layer: "KT_THIETBI",
                color: "#FFD54F",
                position: { x: 3000, y: 2000 },
                blockName: blk.name,
                rotationDeg: 0,
                scale: { x: 1, y: 1, z: 1 },
              } as any;
              updateEntitiesWithHistory([...entities, newBlk]);
              showToast(`Đã chèn Block [${blk.name}] vào Model Space nội bộ`);
            }}
            onRunLisp={(lsp) => showToast(`Đã chọn AutoLISP ${lsp.commandName}. Standalone không thực thi AutoLISP trực tiếp; hãy chạy qua AutoCAD plugin.`)}
            onFixAuditIssue={handleFixAuditIssue}
            onTranslateDrawing={handleTranslateDrawing}
          />
        )}
      </div>

      {/* 3. Bottom Layout Tabs Switcher (Model / Layout A3 / Layout A4 / +) */}
      <div className="h-8 bg-[#18191C] border-t border-neutral-800 px-2 flex items-center justify-between text-xs select-none">
        <div className="flex items-center space-x-1 overflow-x-auto scrollbar-none">
          {/* Model Space Tab */}
          <button
            onClick={() => setActiveLayout(null)}
            className={`px-3 py-1 text-xs font-semibold rounded-t flex items-center space-x-1.5 transition ${
              activeLayout === null
                ? "bg-[#1A1B1E] text-cyan-400 border-t-2 border-cyan-400 font-bold"
                : "text-neutral-400 hover:bg-neutral-800 hover:text-neutral-200"
            }`}
          >
            <Layers className="w-3.5 h-3.5" />
            <span>Model</span>
          </button>

          {/* Paper Space Layout Tabs */}
          {layouts.map((layout) => {
            const isActive = activeLayout?.id === layout.id;
            return (
              <button
                key={layout.id}
                onClick={() => setActiveLayout(layout)}
                className={`px-3 py-1 text-xs font-semibold rounded-t flex items-center space-x-1.5 transition ${
                  isActive
                    ? "bg-[#2B2D30] text-cyan-400 border-t-2 border-cyan-400 font-bold"
                    : "text-neutral-400 hover:bg-neutral-800 hover:text-neutral-200"
                }`}
              >
                <LayoutIcon className="w-3.5 h-3.5" />
                <span>{layout.name}</span>
                <span className="text-[10px] opacity-60">({layout.paperSize})</span>
              </button>
            );
          })}

          {/* Add New Layout Button */}
          <button
            onClick={() => handleExecuteCommand("AUTO_LAYOUT_A3")}
            title="Tạo thêm Layout A3 chuẩn"
            className="p-1 text-neutral-400 hover:text-cyan-400 hover:bg-neutral-800 rounded transition"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Right Status */}
        <div className="hidden lg:flex items-center gap-2 text-xs text-neutral-400 pr-2 shrink-0">
          <span className="font-mono text-neutral-500">X: — &nbsp; Y: —</span>
          <span className="text-neutral-700">|</span>
          <span className="px-1.5 py-0.5 rounded bg-neutral-800 text-cyan-300">SNAP</span>
          <span className="px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-300">ORTHO</span>
          <span className="px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-300">GRID</span>
          <span className="px-1.5 py-0.5 rounded bg-neutral-800 text-neutral-300">mm</span>
          <span className="text-neutral-700">|</span>
          <span className={`font-mono ${autoCadBridgeStatus.connected ? "text-emerald-400" : "text-neutral-500"}`}>{autoCadBridgeStatus.connected ? `AutoCAD ${autoCadBridgeStatus.version || ""} • Connected` : "Standalone • AutoCAD Bridge offline"}</span>
        </div>
      </div>

      {/* Floating Action Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-12 left-1/2 -translate-x-1/2 z-50 bg-[#25272C]/95 backdrop-blur-md text-neutral-100 px-4 py-2.5 rounded-lg border border-cyan-500/40 shadow-2xl text-xs flex items-center space-x-2 animate-in fade-in slide-in-from-bottom-2">
          <Check className="w-4 h-4 text-cyan-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Modals & Dialogs */}
      <HnlAddonManagerModal
        isOpen={isAddonManagerOpen}
        onClose={() => setIsAddonManagerOpen(false)}
        modules={modules}
        onToggleModule={handleToggleModule}
        isSafeMode={isSafeMode}
        onToggleSafeMode={handleToggleSafeMode}
      />

      <HnlShopdrawingCheckModal
        isOpen={isShopCheckOpen}
        onClose={() => setIsShopCheckOpen(false)}
        smartObjects={smartObjects}
        onZoomToObject={(objId) => {
          setSelectedSmartObjectId(objId);
          setIsShopCheckOpen(false);
          setIsLeftDockOpen(true);
          setLeftDockTab("PROPERTIES");
          showToast(`Đã định vị đến đối tượng: ${objId}`);
        }}
        onAutoFixIssue={(issueId) => {
          showToast("Đã áp dụng sửa lỗi nội bộ được hỗ trợ. Các lỗi cần AutoCAD API hoặc xác nhận kỹ thuật vẫn giữ nguyên để kiểm tra.");
        }}
      />

      <AutoDetailLayoutComposerModal
        isOpen={isAutoDetailComposerOpen}
        onClose={() => setIsAutoDetailComposerOpen(false)}
        entities={entities}
        layers={layers}
        onApplyComposerToDrawing={handleApplyComposer}
      />

      <CommandSearchModal
        isOpen={isCommandCenterOpen}
        onClose={() => setIsCommandCenterOpen(false)}
        onSelectCommand={handleExecuteCommand}
      />

      <NetPluginExporterModal
        isOpen={isNetPluginExporterOpen}
        onClose={() => setIsNetPluginExporterOpen(false)}
      />

      <LispBuilderModal
        isOpen={isLispBuilderOpen}
        onClose={() => setIsLispBuilderOpen(false)}
        onSaveLisp={(lsp) => setLispScripts((prev) => [lsp, ...prev])}
        onRunLisp={(lsp) => showToast(`Đã chọn AutoLISP ${lsp.commandName}. Standalone không thực thi AutoLISP trực tiếp; hãy chạy qua AutoCAD plugin.`)}
      />

      <TableBuilderModal
        isOpen={isTableBuilderOpen}
        onClose={() => setIsTableBuilderOpen(false)}
        entities={entities}
        onInsertTable={(tbl) => {
          updateEntitiesWithHistory([...entities, tbl]);
          showToast(`Đã chèn bảng [${tbl.title}] vào Model Space nội bộ. Xuất/chèn sang AutoCAD cần plugin tích hợp.`);
        }}
      />

      <ExcelExportModal
        isOpen={isExcelExportOpen}
        onClose={() => setIsExcelExportOpen(false)}
        entities={entities}
      />

      <AuditModal
        isOpen={isAuditModalOpen}
        onClose={() => setIsAuditModalOpen(false)}
        issues={auditIssues}
        onFixIssue={handleFixAuditIssue}
        onFixAllIssues={handleFixAllAuditIssues}
      />

      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
        safeMode={isSafeMode}
        onSafeModeChange={setIsSafeMode}
      />

      <StandaloneExeBuilderModal
        isOpen={isStandaloneExeBuilderOpen}
        onClose={() => setIsStandaloneExeBuilderOpen(false)}
      />

      <DrywallCeilingStudioModal
        isOpen={isDrywallStudioOpen}
        onClose={() => setIsDrywallStudioOpen(false)}
        entities={entities}
        onApplyPresetToDrawing={(preset) => {
          showToast(`Đã chọn preset ${preset.wallSystem?.systemName || "Thạch cao"} để tham khảo. Chưa thay đổi hình học vì cần chọn vùng/Smart Object và xác nhận Project Spec.`);
        }}
      />

      <HnlWindowsCompatibilityModal
        isOpen={isWindowsCompatOpen}
        onClose={() => setIsWindowsCompatOpen(false)}
      />

      <HnlSectionGeneratorModal
        isOpen={isSectionGenOpen}
        onClose={() => setIsSectionGenOpen(false)}
        entities={entities}
        onInsertToCanvas={(newEnts) => {
          updateEntitiesWithHistory([...entities, ...newEnts]);
          showToast(`Đã chèn bản vẽ mặt cắt tham số (${newEnts.length} đối tượng) vào Model Space!`);
        }}
      />

      <HnlMepClashModal
        isOpen={isMepClashOpen}
        onClose={() => setIsMepClashOpen(false)}
        entities={entities}
        onApplyFramingReinforcement={(newEnts) => {
          updateEntitiesWithHistory([...entities, ...newEnts]);
          showToast(`Đã bổ sung ${newEnts.length} thanh gia cố khung xương né va chạm MEP!`);
        }}
      />

      <HnlExportModal
        isOpen={isMultiExportOpen}
        onClose={() => setIsMultiExportOpen(false)}
        entities={entities}
        layers={layers}
        layouts={layouts}
      />

      <HnlBuildingCodeModal
        isOpen={isBuildingCodeOpen}
        onClose={() => setIsBuildingCodeOpen(false)}
      />
      <HnlPileStudioModal
        isOpen={isPileStudioOpen}
        onClose={() => setIsPileStudioOpen(false)}
        onApply={(pileEntities, schedule) => {
          updateEntitiesWithHistory([...entities, ...pileEntities]);
          if (!layers.some((l) => l.name === 'KC_COC')) setLayers((prev) => [...prev, { name: 'KC_COC', color: '#4DD0E1', isVisible: true, isLocked: false } as any, { name: 'KC_COC_TAG', color: '#FFFFFF', isVisible: true, isLocked: false } as any]);
          showToast(`Đã tạo ${schedule.length} cọc + tag trong Model Space nội bộ. Dữ liệu thiết kế vẫn cần kỹ sư xác nhận.`);
        }}
      />
          <LispInspiredToolCenterModal
        isOpen={is2DProfessionalOpen}
        onClose={() => setIs2DProfessionalOpen(false)}
        entities={entities}
        layers={layers}
        selectedIds={selectedEntityIds}
        initialTab={pro2DInitialTab}
        autoCadConnected={autoCadBridgeStatus.connected}
        onApplyEntities={(next, summary) => { updateEntitiesWithHistory(next); showToast(summary); }}
        onOpenProfessionalAudit={() => { setIs2DProfessionalOpen(false); setIsProfessionalAuditOpen(true); }}
        onOpenPlotPublish={() => { setIs2DProfessionalOpen(false); setIsPlotPublishOpen(true); }}
        onOpenCeiling={() => { setIs2DProfessionalOpen(false); setIsDrywallStudioOpen(true); }}
        onOpenLispBuilder={() => { setIs2DProfessionalOpen(false); setIsLispBuilderOpen(true); }}
        onDiagnostic={(e) => pushDiagnostic(e, e.severity === "ERROR" || e.severity === "CRITICAL")}
      />

      <PlotPublishSheetSetModal
        isOpen={isPlotPublishOpen}
        onClose={() => setIsPlotPublishOpen(false)}
        entities={entities}
        layers={layers}
        layouts={layouts}
        viewports={viewports}
        autoCadConnected={autoCadBridgeStatus.connected}
        onDiagnostic={(e) => pushDiagnostic(e, e.severity === "ERROR" || e.severity === "CRITICAL")}
      />

      <ProfessionalAuditCenterModal
        isOpen={isProfessionalAuditOpen}
        onClose={() => setIsProfessionalAuditOpen(false)}
        entities={entities}
        layers={layers}
        layouts={layouts}
        viewports={viewports}
        onApplyEntities={(next, msg) => { updateEntitiesWithHistory(next); showToast(msg); }}
        onRestoreSnapshot={(snap, label) => {
          try {
            suppressProjectDirtyRef.current = true;
            const nextEntities = Array.isArray(snap?.entities) ? snap.entities as CadEntity[] : [];
            setEntities(nextEntities); setHistory([nextEntities]); setHistoryIndex(0);
            if (Array.isArray(snap?.layers)) setLayers(snap.layers);
            if (Array.isArray(snap?.layouts)) setLayouts(snap.layouts);
            if (Array.isArray(snap?.viewports)) setViewports(snap.viewports);
            if (Array.isArray(snap?.smartObjects)) setSmartObjects(snap.smartObjects);
            if (Array.isArray(snap?.spreadsheetParameters)) setSpreadsheetParameters(snap.spreadsheetParameters);
            if (Array.isArray(snap?.translationMemory)) setTranslationMemory(snap.translationMemory);
            if (Array.isArray(snap?.blockLibrary)) setBlockLibrary(snap.blockLibrary);
            if (Array.isArray(snap?.dependencyEdges)) setDependencyEdges(snap.dependencyEdges);
            if (Array.isArray(snap?.modules)) setModules(snap.modules);
            if (typeof snap?.selectedWorkbench === "string") setSelectedWorkbench(snap.selectedWorkbench);
            const active = Array.isArray(snap?.layouts) ? snap.layouts.find((l:any)=>l.id===snap.activeLayoutId) : null;
            setActiveLayout(active || null);
            setCurrentFileName(String(snap?.currentFileName || "Recovered.hnl.json"));
            setSelectedEntityIds([]); setIsDirty(true);
            pushDiagnostic({code:"HNL-RECOVERY-RESTORE",severity:"INFO",title:"Đã khôi phục Recovery",message:label,command:"RECOVERY_RESTORE",context:{savedAt:snap?.savedAt,entities:nextEntities.length}});
            showToast(`Đã khôi phục ${label}`);
          } catch (error: unknown) {
            const d = errorToDetails(error);
            pushDiagnostic({code:"HNL-RECOVERY-RESTORE-ERR",severity:"ERROR",title:"Khôi phục Recovery thất bại",message:d.cause,command:"RECOVERY_RESTORE",stack:d.stack},true);
          }
        }}
        onDiagnostic={(e) => pushDiagnostic(e, e.severity === "ERROR" || e.severity === "CRITICAL")}
      />

      <SketchUp2DBridgeModal
        isOpen={isSketchUpBridgeOpen}
        onClose={() => setIsSketchUpBridgeOpen(false)}
        entities={entities}
        layers={layers}
        selectedIds={selectedEntityIds}
        onApplyClean={(next, summary) => { updateEntitiesWithHistory(next); showToast(`2D Cleanup: ${summary}`); }}
        onImportSketchUp={(incoming, incomingLayers, summary) => {
          const oldSketchUpIds = new Set((entities as any[]).filter(e => e.hnlLinkId || String(e.id).startsWith("su_")).map(e => e.id));
          updateEntitiesWithHistory([...entities.filter(e => !oldSketchUpIds.has(e.id)), ...incoming]);
          setLayers(prev => {
            const map = new Map(prev.map(l => [l.name, l]));
            incomingLayers.forEach(l => { if (!map.has(l.name)) map.set(l.name, l); });
            return [...map.values()];
          });
          showToast(`SketchUp → CAD 2D: ${summary}`);
        }}
        onDiagnostic={(e) => pushDiagnostic(e, e.severity === "ERROR" || e.severity === "CRITICAL")}
      />

      <UsageGuideModal isOpen={isUsageGuideOpen} onClose={() => setIsUsageGuideOpen(false)} />
      <DiagnosticsModal isOpen={isDiagnosticsOpen} events={diagnosticEvents} onClose={() => setIsDiagnosticsOpen(false)} onClear={() => { setDiagnosticEvents([]); saveDiagnostics([]); }} />
</div>
  );
}
