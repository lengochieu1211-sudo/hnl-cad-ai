import React, { useState } from "react";
import { Code2, Sparkles, Play, Copy, Download, Check, X, AlertCircle } from "lucide-react";
import { LispScriptItem } from "../../types/cad";

interface LispBuilderModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSaveLisp: (lisp: LispScriptItem) => void;
  onRunLisp: (lisp: LispScriptItem) => void;
}

export const LispBuilderModal: React.FC<LispBuilderModalProps> = ({
  isOpen,
  onClose,
  onSaveLisp,
  onRunLisp,
}) => {
  const [prompt, setPrompt] = useState("");
  const [isGenerating, setIsGenerating] = useState(false);
  const [generatedLisp, setGeneratedLisp] = useState<{
    commandName: string;
    description: string;
    code: string;
    category: string;
  } | null>(null);
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const handleGenerateLisp = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt.trim() || isGenerating) return;

    setIsGenerating(true);
    try {
      const res = await fetch("/api/gemini/lisp", {
        method: "POST",
        headers: { "Content-Type": "application/json", ...(((window as any).electronNative?.sessionToken) ? { "x-hnl-token": (window as any).electronNative.sessionToken } : {}) },
        body: JSON.stringify({ prompt }),
      });
      const data = await res.json();
      if(!res.ok) throw new Error(data?.error || "AI Lisp Builder không phản hồi.");
      const generated = data?.lisp || (data?.code ? data : null);
      if(generated) setGeneratedLisp(generated);
      else throw new Error("AI không trả về mã Lisp hợp lệ.");
    } catch (err: any) {
      alert("Lỗi tạo Lisp: " + err.message);
    } finally {
      setIsGenerating(false);
    }
  };

  const handleCopy = () => {
    if (!generatedLisp) return;
    navigator.clipboard.writeText(generatedLisp.code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleDownload = () => {
    if (!generatedLisp) return;
    const blob = new Blob([generatedLisp.code], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${generatedLisp.commandName.replace("C:", "").toLowerCase()}.lsp`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleSaveAndRun = () => {
    if (!generatedLisp) return;
    const item: LispScriptItem = {
      id: `lsp_${Date.now()}`,
      commandName: generatedLisp.commandName,
      description: generatedLisp.description,
      code: generatedLisp.code,
      category: generatedLisp.category as any,
      autoload: true,
      lastModified: new Date().toLocaleDateString("vi-VN"),
    };
    onSaveLisp(item);
    onRunLisp(item);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="w-full max-w-4xl h-[80vh] bg-[#1E1F22] rounded-xl border border-neutral-700 shadow-2xl overflow-hidden flex flex-col">
        {/* Header */}
        <div className="h-14 px-6 bg-[#141517] border-b border-neutral-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <div className="w-8 h-8 rounded bg-gradient-to-r from-cyan-600 to-blue-500 flex items-center justify-center text-white">
              <Code2 className="w-5 h-5" />
            </div>
            <div>
              <h2 className="font-bold text-white text-sm">AI AUTOLISP BUILDER</h2>
              <p className="text-xs text-neutral-400">
                Tự động viết mã AutoLISP chuẩn AutoCAD từ mô tả ngôn ngữ tự nhiên
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg hover:bg-neutral-800 text-neutral-400 hover:text-white transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form and Prompt Input */}
        <div className="p-4 bg-[#18191C] border-b border-neutral-800">
          <form onSubmit={handleGenerateLisp} className="flex items-center space-x-3">
            <input
              type="text"
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="Mô tả tác vụ (vd: 'Viết Lisp vẽ tim trục tự động theo 2 điểm và gắn vòng tròn số hiệu A, B, C')..."
              className="flex-1 bg-[#25272C] text-neutral-100 placeholder-neutral-500 px-4 py-2.5 rounded-lg border border-neutral-700 text-xs focus:outline-none focus:border-cyan-500"
            />
            <button
              type="submit"
              disabled={isGenerating || !prompt.trim()}
              className="px-5 py-2.5 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 disabled:opacity-50 text-white font-semibold rounded-lg text-xs flex items-center space-x-2 shadow transition"
            >
              <Sparkles className="w-4 h-4" />
              <span>{isGenerating ? "AI đang viết mã..." : "Tạo Lisp"}</span>
            </button>
          </form>

          {/* Quick Examples */}
          <div className="flex items-center space-x-2 mt-2 text-[11px] text-neutral-400">
            <span className="text-neutral-500">Mẫu gợi ý:</span>
            {[
              "Vẽ đường kính ống MEP và gắn mũi tên hướng dòng",
              "Tính diện tích hatch và ghi ra giữa hình",
              "Đổi toàn bộ text trong bản vẽ sang font Arial 2.5",
            ].map((eg, i) => (
              <button
                key={i}
                type="button"
                onClick={() => setPrompt(eg)}
                className="hover:text-cyan-400 underline underline-offset-2 truncate max-w-xs"
              >
                {eg}
              </button>
            ))}
          </div>
        </div>

        {/* Code Result & Actions */}
        <div className="flex-1 flex flex-col bg-[#141517] overflow-hidden">
          {generatedLisp ? (
            <>
              <div className="h-10 px-4 bg-[#1C1D20] border-b border-neutral-800 flex items-center justify-between text-xs">
                <div className="flex items-center space-x-3 font-mono">
                  <span className="font-bold text-cyan-400">{generatedLisp.commandName}</span>
                  <span className="text-neutral-500">|</span>
                  <span className="text-neutral-300">{generatedLisp.description}</span>
                </div>

                <div className="flex items-center space-x-2">
                  <button
                    onClick={handleCopy}
                    className="flex items-center space-x-1.5 px-3 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition text-xs"
                  >
                    {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                    <span>{copied ? "Đã chép" : "Sao chép"}</span>
                  </button>
                  <button
                    onClick={handleDownload}
                    className="flex items-center space-x-1.5 px-3 py-1 rounded bg-neutral-800 hover:bg-neutral-700 text-neutral-200 transition text-xs"
                  >
                    <Download className="w-3.5 h-3.5" />
                    <span>Tải .lsp</span>
                  </button>
                  <button
                    onClick={handleSaveAndRun}
                    className="flex items-center space-x-1.5 px-4 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-semibold transition text-xs shadow"
                  >
                    <Play className="w-3.5 h-3.5 fill-current" />
                    <span>Nạp & Chạy ngay</span>
                  </button>
                </div>
              </div>

              <div className="flex-1 p-4 overflow-auto font-mono text-xs text-neutral-200 leading-relaxed bg-[#101113]">
                <pre className="whitespace-pre">{generatedLisp.code}</pre>
              </div>
            </>
          ) : (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-8 text-neutral-500 text-xs space-y-2">
              <Code2 className="w-12 h-12 text-neutral-600 stroke-1" />
              <p className="text-neutral-300 font-semibold">Chưa có mã AutoLISP nào được tạo</p>
              <p className="max-w-md text-neutral-400">
                Hãy nhập yêu cầu của bạn ở ô phía trên (bằng tiếng Việt hoặc tiếng Anh) để AI sinh mã AutoLISP tối ưu, sạch lỗi và có bẫy lỗi (*error* handler).
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
