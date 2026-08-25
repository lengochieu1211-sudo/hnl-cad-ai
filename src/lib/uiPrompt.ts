export type HnlPromptOptions = {
  title: string;
  label?: string;
  defaultValue?: string;
  placeholder?: string;
  multiline?: boolean;
  readOnly?: boolean;
  okText?: string;
  cancelText?: string;
};

let activePromptCleanup: (() => void) | null = null;

export function requestHnlInput(options: HnlPromptOptions): Promise<string | null> {
  return new Promise((resolve) => {
    if (typeof document === "undefined") { resolve(null); return; }
    activePromptCleanup?.();

    const overlay = document.createElement("div");
    overlay.setAttribute("data-hnl-native-prompt", "1");
    Object.assign(overlay.style, {
      position: "fixed", inset: "0", zIndex: "100000", background: "rgba(0,0,0,.68)",
      display: "flex", alignItems: "center", justifyContent: "center", padding: "16px"
    });

    const card = document.createElement("div");
    Object.assign(card.style, {
      width: "min(560px, calc(100vw - 32px))", background: "#17191d", color: "#e5e7eb",
      border: "1px solid #3f3f46", borderRadius: "12px", boxShadow: "0 24px 80px rgba(0,0,0,.55)",
      fontFamily: "Segoe UI, Arial, sans-serif", overflow: "hidden"
    });

    const header = document.createElement("div");
    header.textContent = options.title;
    Object.assign(header.style, { padding: "14px 16px", fontWeight: "700", borderBottom: "1px solid #27272a", background: "#111317" });

    const body = document.createElement("div");
    Object.assign(body.style, { padding: "16px" });
    if (options.label) {
      const label = document.createElement("div");
      label.textContent = options.label;
      Object.assign(label.style, { fontSize: "12px", color: "#a3a3a3", marginBottom: "8px" });
      body.appendChild(label);
    }

    const input = options.multiline ? document.createElement("textarea") : document.createElement("input");
    if (input instanceof HTMLInputElement) input.type = "text";
    input.value = options.defaultValue ?? "";
    input.placeholder = options.placeholder ?? "";
    input.readOnly = Boolean(options.readOnly);
    Object.assign(input.style, {
      width: "100%", boxSizing: "border-box", minHeight: options.multiline ? "190px" : "38px",
      resize: options.multiline ? "vertical" : "none", padding: "9px 10px", borderRadius: "7px",
      border: "1px solid #52525b", background: "#24262b", color: "#f4f4f5", outline: "none",
      font: options.multiline ? "12px Consolas, monospace" : "13px Segoe UI, Arial, sans-serif"
    });
    body.appendChild(input);

    const footer = document.createElement("div");
    Object.assign(footer.style, { display: "flex", justifyContent: "flex-end", gap: "8px", padding: "12px 16px", borderTop: "1px solid #27272a" });
    const cancel = document.createElement("button");
    cancel.textContent = options.cancelText ?? "Hủy";
    const ok = document.createElement("button");
    ok.textContent = options.okText ?? (options.readOnly ? "Đóng" : "OK");
    for (const b of [cancel, ok]) Object.assign(b.style, { padding: "8px 14px", borderRadius: "7px", border: "1px solid #52525b", cursor: "pointer" });
    Object.assign(cancel.style, { background: "#27272a", color: "#d4d4d8" });
    Object.assign(ok.style, { background: "#0891b2", borderColor: "#0891b2", color: "white", fontWeight: "700" });
    if (!options.readOnly) footer.appendChild(cancel);
    footer.appendChild(ok);

    card.append(header, body, footer);
    overlay.appendChild(card);
    document.body.appendChild(overlay);

    let finished = false;
    const finish = (value: string | null) => {
      if (finished) return;
      finished = true;
      document.removeEventListener("keydown", keydown, true);
      overlay.remove();
      if (activePromptCleanup === cleanup) activePromptCleanup = null;
      resolve(value);
    };
    const cleanup = () => finish(null);
    const keydown = (e: KeyboardEvent) => {
      if (e.key === "Escape") { e.preventDefault(); finish(null); }
      if (!options.multiline && e.key === "Enter") { e.preventDefault(); finish(input.value); }
    };
    activePromptCleanup = cleanup;
    document.addEventListener("keydown", keydown, true);
    cancel.onclick = () => finish(null);
    ok.onclick = () => finish(options.readOnly ? null : input.value);
    overlay.onclick = (e) => { if (e.target === overlay && !options.readOnly) finish(null); };
    window.setTimeout(() => { input.focus(); if (!options.readOnly && input instanceof HTMLInputElement) input.select(); }, 0);
  });
}
