import { HNL_APP_VERSION } from "./version";

export type DiagnosticSeverity = "INFO" | "WARNING" | "ERROR" | "CRITICAL";

export interface DiagnosticEvent {
  id: string;
  code: string;
  severity: DiagnosticSeverity;
  title: string;
  message: string;
  command?: string;
  timestamp: string;
  context?: Record<string, unknown>;
  cause?: string;
  stack?: string;
  suggestion?: string;
}

const STORAGE_KEY = "hnl.diagnostics.v1";
const MAX_EVENTS = 150;

export function loadDiagnostics(): DiagnosticEvent[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const data = raw ? JSON.parse(raw) : [];
    return Array.isArray(data) ? data.slice(0, MAX_EVENTS) : [];
  } catch {
    return [];
  }
}

export function saveDiagnostics(events: DiagnosticEvent[]) {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(events.slice(0, MAX_EVENTS))); } catch {}
}

export function makeDiagnostic(input: Omit<DiagnosticEvent, "id" | "timestamp">): DiagnosticEvent {
  const timestamp = new Date().toISOString();
  return {
    ...input,
    id: `diag_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`,
    timestamp,
  };
}

export function errorToDetails(error: unknown): { cause: string; stack?: string } {
  if (error instanceof Error) return { cause: error.message || error.name, stack: error.stack };
  if (typeof error === "string") return { cause: error };
  try { return { cause: JSON.stringify(error) }; } catch { return { cause: String(error) }; }
}

export function diagnosticsToText(events: DiagnosticEvent[], appVersion = HNL_APP_VERSION) {
  const lines = [
    "HNL CAD AI - DIAGNOSTIC REPORT",
    `App version: ${appVersion}`,
    `Generated: ${new Date().toISOString()}`,
    `Events: ${events.length}`,
    "",
  ];
  for (const e of events) {
    lines.push(`[${e.severity}] ${e.code} - ${e.title}`);
    lines.push(`Time: ${e.timestamp}`);
    if (e.command) lines.push(`Command: ${e.command}`);
    lines.push(`Message: ${e.message}`);
    if (e.cause) lines.push(`Cause: ${e.cause}`);
    if (e.suggestion) lines.push(`Suggestion: ${e.suggestion}`);
    if (e.context) lines.push(`Context: ${JSON.stringify(e.context, null, 2)}`);
    if (e.stack) lines.push(`Stack: ${e.stack}`);
    lines.push("---");
  }
  return lines.join("\n");
}
