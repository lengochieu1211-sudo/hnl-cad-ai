const STORAGE_KEY = 'hnl-cad-ai-project-v2';
const LEGACY_STORAGE_KEY = 'hnl-cad-ai-project-v1';

export type HnlProjectSnapshot = {
  schemaVersion: 2;
  savedAt: string;
  entities: unknown[];
  layers: unknown[];
  layouts: unknown[];
  viewports: unknown[];
  smartObjects: unknown[];
  spreadsheetParameters: unknown[];
  translationMemory: unknown[];
  blockLibrary: unknown[];
  activeLayoutId: string | null;
  currentFileName: string;
  dependencyEdges: unknown[];
  modules: unknown[];
  selectedWorkbench: string;
};

export function saveProjectSnapshot(snapshot: Omit<HnlProjectSnapshot, 'schemaVersion' | 'savedAt'>) {
  const payload: HnlProjectSnapshot = {
    schemaVersion: 2,
    savedAt: new Date().toISOString(),
    ...snapshot,
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  return payload.savedAt;
}

export function loadProjectSnapshot(): HnlProjectSnapshot | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY) || localStorage.getItem(LEGACY_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed.entities)) return null;
    if (parsed?.schemaVersion === 1) {
      return { ...parsed, schemaVersion: 2, translationMemory: parsed.translationMemory || [], blockLibrary: parsed.blockLibrary || [], activeLayoutId: parsed.activeLayoutId || null, currentFileName: parsed.currentFileName || "Recovered.hnl.json", dependencyEdges: parsed.dependencyEdges || [], modules: parsed.modules || [], selectedWorkbench: parsed.selectedWorkbench || "HNL_CAD" } as HnlProjectSnapshot;
    }
    if (parsed?.schemaVersion !== 2) return null;
    return parsed as HnlProjectSnapshot;
  } catch {
    return null;
  }
}

export function clearProjectSnapshot() {
  localStorage.removeItem(STORAGE_KEY);
  localStorage.removeItem(LEGACY_STORAGE_KEY);
}
