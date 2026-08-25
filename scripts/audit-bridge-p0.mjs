import fs from "node:fs";
const read=p=>fs.readFileSync(p,"utf8");
const server=read("server.ts");
const client=read("src/lib/autoCadBridge.ts");
const bridge=read("autocad-plugin/Hnl.CadBridge/BridgeCommands.cs");
const diag=read("src/components/Dialogs/DiagnosticsModal.tsx");
const checks=[
 ["SERVER_GOLDEN_SMOKE",server.includes('/api/autocad/golden-smoke')&&server.includes('BRIDGE_GOLDEN_READ_ONLY')],
 ["CAPABILITY_GUARD",server.includes('AUTOCAD_ACTION_NOT_SUPPORTED')],
 ["QUEUE_DEADLINE",server.includes('expiresAt:createdAt+timeoutMs')&&bridge.includes('AUTOCAD_ACTION_EXPIRED_BEFORE_EXECUTION')],
 ["CLIENT_TIMEOUT_PROPAGATION",client.includes('timeoutMs:ACTION_TIMEOUT_MS')],
 ["CLIENT_TIMEOUT_CANCEL",client.includes('/cancel')],
 ["POLL_OVERLAP_GUARD",bridge.includes('Interlocked.Exchange(ref _pollBusy, 1)')],
 ["HTTP_TIMEOUT",bridge.includes('Timeout = TimeSpan.FromSeconds(5)')],
 ["BRIDGE_INSTANCE_EVIDENCE",bridge.includes('BridgeInstanceId')&&server.includes('bridgeInstanceId')],
 ["DIAGNOSTIC_GOLDEN_BUTTON",diag.includes('Golden Test Bridge')&&diag.includes('BRIDGE GOLDEN PASS')],
];
let fail=0;for(const [id,ok] of checks){console.log(`${ok?'PASS':'FAIL'} ${id}`);if(!ok)fail++;}
console.log(`HNL BRIDGE P0 STATIC: ${checks.length-fail}/${checks.length} PASS`);
if(fail)process.exit(1);
