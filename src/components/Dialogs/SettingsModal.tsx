import React, { useEffect, useState } from "react";
import { Settings, Shield, Cpu, Key, Database, X, Check, Globe } from "lucide-react";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
  safeMode: boolean;
  onSafeModeChange: (value: boolean) => void;
}

export const SettingsModal: React.FC<SettingsModalProps> = ({ isOpen, onClose, safeMode, onSafeModeChange }) => {
  const [aiProvider, setAiProvider] = useState<"GEMINI" | "LOCAL" | "CUSTOM">("GEMINI");
  const [apiKey, setApiKey] = useState("");
  const [apiKeyStatus, setApiKeyStatus] = useState("Chưa kiểm tra");
  const [wallThickDefault, setWallThickDefault] = useState(100);
  const [ceilingMainSpacing, setCeilingMainSpacing] = useState(800);
  const [ceilingCrossSpacing, setCeilingCrossSpacing] = useState(400);
  const [savedMessage, setSavedMessage] = useState("");

  useEffect(() => {
    if (!isOpen) return;
    try {
      const raw = localStorage.getItem("hnl.settings.v1");
      const cfg = raw ? JSON.parse(raw) : {};
      if (cfg.aiProvider) setAiProvider(cfg.aiProvider);
      if (Number.isFinite(cfg.wallThickDefault)) setWallThickDefault(cfg.wallThickDefault);
      if (Number.isFinite(cfg.ceilingMainSpacing)) setCeilingMainSpacing(cfg.ceilingMainSpacing);
      if (Number.isFinite(cfg.ceilingCrossSpacing)) setCeilingCrossSpacing(cfg.ceilingCrossSpacing);
    } catch {}
    const nativeApi = (window as any).electronNative;
    nativeApi?.getAIKeyStatus?.().then((r: any) => setApiKeyStatus(r?.configured ? "Đã cấu hình an toàn" : "Chưa cấu hình"));
  }, [isOpen]);

  if (!isOpen) return null;

  const handleSave = () => {
    localStorage.setItem("hnl.settings.v1", JSON.stringify({ aiProvider, safeMode, wallThickDefault, ceilingMainSpacing, ceilingCrossSpacing }));
    onSafeModeChange(safeMode);
    const nativeApi = (window as any).electronNative;
    if (apiKey.trim() && nativeApi?.saveAIKey) {
      nativeApi.saveAIKey(apiKey.trim()).then((r: any) => setApiKeyStatus(r?.success ? "Đã lưu mã hóa" : `Lỗi: ${r?.error || "không lưu được"}`));
      setApiKey("");
    }
    setSavedMessage("Đã lưu cấu hình. Safe Mode có hiệu lực ngay.");
    setTimeout(() => onClose(), 450);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-2xl bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-neutral-800 border border-neutral-700 flex items-center justify-center text-cyan-400">
              <Settings className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">CÀI ĐẶT HỆ THỐNG HNL CAD AI</h2>
              <p className="text-xs text-neutral-400">Cấu hình AI Provider, Cơ chế an toàn và Tiêu chuẩn bản vẽ</p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-5 text-xs overflow-y-auto max-h-[70vh]">
          {/* AI Provider Config */}
          <div className="space-y-2">
            <label className="text-neutral-300 font-semibold flex items-center space-x-1.5">
              <Cpu className="w-4 h-4 text-cyan-400" />
              <span>Nhà cung cấp Trí tuệ nhân tạo (AI Engine):</span>
            </label>

            <div className="grid grid-cols-3 gap-2">
              <div
                onClick={() => setAiProvider("GEMINI")}
                className={`p-3 rounded-lg border cursor-pointer transition ${
                  aiProvider === "GEMINI"
                    ? "bg-cyan-500/20 border-cyan-500 text-white font-semibold"
                    : "bg-[#25272C] border-neutral-700 text-neutral-400 hover:text-white"
                }`}
              >
                <div className="font-bold text-cyan-400">Google Gemini</div>
                <div className="text-[10px] text-neutral-400 mt-1">Online Gemini (cần GEMINI_API_KEY ở runtime/server)</div>
              </div>

              <div
                onClick={() => setAiProvider("LOCAL")}
                className={`p-3 rounded-lg border cursor-pointer transition ${
                  aiProvider === "LOCAL"
                    ? "bg-cyan-500/20 border-cyan-500 text-white font-semibold"
                    : "bg-[#25272C] border-neutral-700 text-neutral-400 hover:text-white"
                }`}
              >
                <div className="font-bold text-emerald-400">Local Rule-Based</div>
                <div className="text-[10px] text-neutral-400 mt-1">Chạy cục bộ; dữ liệu AI online chỉ gửi khi người dùng bật nhà cung cấp tương ứng</div>
              </div>

              <div
                onClick={() => setAiProvider("CUSTOM")}
                className={`p-3 rounded-lg border cursor-pointer transition ${
                  aiProvider === "CUSTOM"
                    ? "bg-cyan-500/20 border-cyan-500 text-white font-semibold"
                    : "bg-[#25272C] border-neutral-700 text-neutral-400 hover:text-white"
                }`}
              >
                <div className="font-bold text-purple-400">Custom / Ollama</div>
                <div className="text-[10px] text-neutral-400 mt-1">Tùy chọn dự kiến; endpoint Custom/Ollama chưa nối trong bản này</div>
              </div>
            </div>
          </div>

          {/* Safe Mode Toggle */}
          <div className="p-3 bg-[#18191C] rounded-lg border border-neutral-800 space-y-2">
            <div className="flex items-center justify-between">
              <div className="space-y-0.5">
                <div className="font-semibold text-neutral-200 flex items-center space-x-1.5">
                  <Shield className="w-4 h-4 text-emerald-400" />
                  <span>Chế độ An toàn (Safe Execution Mode)</span>
                </div>
                <div className="text-[11px] text-neutral-400">
                  Luôn yêu cầu người dùng xem trước (Preview) và xác nhận trước khi thực thi các lệnh xoá hoặc sửa đổi hàng loạt.
                </div>
              </div>
              <input
                type="checkbox"
                checked={safeMode}
                onChange={(e) => onSafeModeChange(e.target.checked)}
                className="w-4 h-4 rounded text-cyan-500 focus:ring-0 cursor-pointer"
              />
            </div>
          </div>


          <div className="p-4 bg-[#18191C] rounded-lg border border-neutral-800 space-y-3">
            <div className="flex items-center justify-between">
              <div><div className="font-semibold text-neutral-200 flex items-center gap-2"><Key className="w-4 h-4 text-amber-400"/>Gemini API Key</div><div className="text-[11px] text-neutral-500 mt-1">EXE lưu bằng Electron safeStorage/Windows DPAPI; không ghi API key dạng text trong project.</div></div>
              <span className="px-2 py-1 rounded bg-neutral-800 border border-neutral-700 text-[10px] text-emerald-300">{apiKeyStatus}</span>
            </div>
            <input type="password" value={apiKey} onChange={(e)=>setApiKey(e.target.value)} placeholder="Nhập API key mới (để trống nếu không đổi)" className="w-full bg-[#25272C] text-neutral-200 px-3 py-2.5 rounded-lg border border-neutral-700 focus:border-cyan-500 outline-none" />
          </div>

          {/* CAD Drawing Defaults */}
          <div className="space-y-3">
            <label className="text-neutral-300 font-semibold">Thông số mặc định công cụ vẽ nhanh:</label>
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1">
                <span className="text-neutral-400 text-[11px]">Độ dày tường mặc định:</span>
                <select
                  value={wallThickDefault}
                  onChange={(e) => setWallThickDefault(Number(e.target.value))}
                  className="w-full bg-[#25272C] text-neutral-200 px-3 py-2 rounded-lg border border-neutral-700"
                >
                  <option value={100}>100 mm (Tường ngăn)</option>
                  <option value={200}>200 mm (Tường chịu lực)</option>
                  <option value={150}>150 mm</option>
                </select>
              </div>

              <div className="space-y-1">
                <span className="text-neutral-400 text-[11px]">Khoảng cách xương chính:</span>
                <input
                  type="number"
                  value={ceilingMainSpacing}
                  onChange={(e) => setCeilingMainSpacing(Number(e.target.value))}
                  className="w-full bg-[#25272C] text-neutral-200 px-3 py-2 rounded-lg border border-neutral-700"
                />
              </div>

              <div className="space-y-1">
                <span className="text-neutral-400 text-[11px]">Khoảng cách xương phụ:</span>
                <input
                  type="number"
                  value={ceilingCrossSpacing}
                  onChange={(e) => setCeilingCrossSpacing(Number(e.target.value))}
                  className="w-full bg-[#25272C] text-neutral-200 px-3 py-2 rounded-lg border border-neutral-700"
                />
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="h-14 px-6 bg-[#141517] border-t border-neutral-800 flex items-center justify-between">
          <div className="text-[11px] text-emerald-400 min-w-[180px]">{savedMessage}</div>
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs transition"
          >
            Đóng
          </button>
          <button
            onClick={handleSave}
            className="px-5 py-2 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white font-semibold text-xs flex items-center space-x-2 shadow transition"
          >
            <Check className="w-4 h-4" />
            <span>Lưu cấu hình</span>
          </button>
        </div>
      </div>
    </div>
  );
};
