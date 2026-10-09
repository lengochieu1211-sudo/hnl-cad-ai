import fs from "node:fs";
const read=p=>fs.readFileSync(p,"utf8");
const server=read("server.ts");
const client=read("src/lib/autoCadBridge.ts");
const bridge=read("autocad-plugin/Hnl.CadBridge/BridgeCommands.cs");
const diag=read("src/components/Dialogs/DiagnosticsModal.tsx");
const workflow=read(".github/workflows/build-windows.yml");
const electron=read("electron/main.cjs");
const checks=[
 ["SERVER_GOLDEN_SMOKE",server.includes('/api/autocad/golden-smoke')&&server.includes('BRIDGE_GOLDEN_READ_ONLY')],
 ["CAPABILITY_GUARD",server.includes('AUTOCAD_ACTION_NOT_SUPPORTED')&&server.includes('if(!caps.includes(action))')],
 ["QUEUE_DEADLINE",server.includes('expiresAt:createdAt+timeoutMs')&&bridge.includes('AUTOCAD_ACTION_EXPIRED_BEFORE_EXECUTION')],
 ["CLIENT_ABSOLUTE_DEADLINE",client.includes("server's absolute deadline")&&client.includes('const expiresAt=Number(x.expiresAt)')],
 ["CLIENT_TIMEOUT_CANCEL",client.includes('/cancel')],
 ["CLIENT_FETCH_ABORT",client.includes('AbortController')&&client.includes('AUTOCAD_BRIDGE_HTTP_TIMEOUT')],
 ["POLL_OVERLAP_GUARD",bridge.includes('Interlocked.Exchange(ref _pollBusy, 1)')],
 ["HTTP_TIMEOUT",bridge.includes('Timeout = TimeSpan.FromSeconds(5)')],
 ["BRIDGE_INSTANCE_EVIDENCE",bridge.includes('BridgeInstanceId')&&server.includes('bridgeInstanceId')],
 ["BRIDGE_OWNER_LOCK",server.includes('AUTOCAD_BRIDGE_ALREADY_OWNED')&&server.includes('AUTOCAD_BRIDGE_OWNER_MISMATCH')&&bridge.includes('x-hnl-bridge-instance')],
 ["VERSION_MISMATCH_GUARD",server.includes('AUTOCAD_BRIDGE_VERSION_MISMATCH')&&server.includes('pluginVersion!==HNL_APP_VERSION')],
 ["INFLIGHT_RESULT_GUARD",server.includes('autoCadInFlightActions')&&server.includes('AUTOCAD_ACTION_UNKNOWN_OR_EXPIRED')],
 ["GOLDEN_TIMEOUT_TOMBSTONE",server.includes('AUTOCAD_BRIDGE_GOLDEN_TIMEOUT')&&server.includes('else if(autoCadInFlightActions.has(id))')&&server.includes('autoCadCancelledActions.set(id,Date.now())')],
 ["OUTSTANDING_QUEUE_CAP",server.includes('autoCadActionQueue.length+autoCadInFlightActions.size')],
 ["DISPATCHED_CANCEL_PROPAGATION",server.includes('cancelledActionIds:Array.from(autoCadCancelledActions.keys())')&&bridge.includes('CancelledActionIds.TryRemove')],
 ["AUTOCAD_THREAD_AFFINITY",bridge.includes('_activeDrawingName')&&bridge.includes('All AutoCAD API reads/writes stay on AutoCAD\'s thread.')],
 ["AUTOCAD_BUSY_GUARD",bridge.includes('IsAutoCadBusy()')&&bridge.includes('CMDNAMES')],
 ["PAIRING_SWITCH_DROPS_STALE_QUEUE",bridge.includes('while (UiActions.TryDequeue(out _))')],
 ["ACTIVE_DOCUMENT_GUARD",bridge.includes('AUTOCAD_ACTIVE_DOCUMENT_CHANGED')&&server.includes('targetDrawingName')],
 ["NULLABLE_HANDLE_GUARD",bridge.includes('ObjectIdFromHandle(Database db, string? handleText)')&&bridge.includes('string.IsNullOrWhiteSpace(handleText)')],
 ["SINGLE_HNL_PROCESS",electron.includes('requestSingleInstanceLock')&&electron.includes("second-instance")],
 ["DIAGNOSTIC_GOLDEN_BUTTON",diag.includes('Golden Test Bridge')&&diag.includes('BRIDGE GOLDEN PASS')],
 ["LISP_ON_DEMAND_BUILD_LABEL",workflow.includes('Lisp On-Demand pack PASS')&&!workflow.includes('Lisp AutoLoad pack PASS')],
];
let fail=0;for(const [id,ok] of checks){console.log(`${ok?'PASS':'FAIL'} ${id}`);if(!ok)fail++;}
console.log(`HNL BRIDGE P0 STATIC: ${checks.length-fail}/${checks.length} PASS`);
if(fail)process.exit(1);
