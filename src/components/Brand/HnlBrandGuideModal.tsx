import React, { useState } from "react";
import { X, Check, Copy, Sparkles, Palette, ShieldCheck, Download, Code } from "lucide-react";
import { HNL_BRAND, HNL_PALETTE_TOKENS } from "../../lib/branding";
import { HnlLogo } from "./HnlLogo";

interface HnlBrandGuideModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const HnlBrandGuideModal: React.FC<HnlBrandGuideModalProps> = ({ isOpen, onClose }) => {
  const [copiedHex, setCopiedHex] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedHex(text);
    setTimeout(() => setCopiedHex(null), 2000);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 backdrop-blur-sm p-4 animate-in fade-in duration-200">
      <div className="w-full max-w-3xl bg-[#0F172A] border border-slate-700 rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="h-14 px-6 bg-slate-900 border-b border-slate-800 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <HnlLogo size="sm" showSubtitle={false} />
            <div className="h-4 w-px bg-slate-700" />
            <span className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
              <Palette className="w-4 h-4 text-sky-400" />
              Bộ Nhận Diện Thương Hiệu & Bảng Màu Chuẩn (HNL Blue & Slate)
            </span>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6 text-sm text-slate-200">
          {/* Brand Info Banner */}
          <div className="p-4 rounded-xl bg-gradient-to-r from-sky-950/60 via-slate-900 to-slate-950 border border-sky-500/30 flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
            <div>
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                <span>{HNL_BRAND.name}</span>
                <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-sky-500/20 text-sky-300 border border-sky-500/30">
                  {HNL_BRAND.version}
                </span>
              </h3>
              <p className="text-xs text-slate-400 mt-1">{HNL_BRAND.tagline}</p>
              <p className="text-[11px] text-slate-500 font-mono mt-0.5">Tiêu chuẩn: {HNL_BRAND.standardCode}</p>
            </div>
            <div className="flex items-center gap-2">
              <div className="px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700 text-xs font-mono text-emerald-400 flex items-center gap-1.5">
                <ShieldCheck className="w-4 h-4" />
                <span>Thương hiệu HNL đã thống nhất</span>
              </div>
            </div>
          </div>

          {/* Logo Variations Showcase */}
          <div>
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3 flex items-center gap-1.5">
              <Sparkles className="w-3.5 h-3.5 text-sky-400" />
              Các Biến Thể Vector Logo HNL (SVG Formats)
            </h4>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              {/* Variant 1: Monogram */}
              <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col items-center justify-center text-center space-y-3 group hover:border-sky-500/50 transition">
                <div className="p-2 bg-slate-900 rounded-lg border border-slate-800">
                  <HnlLogo size="lg" variant="monogram" showText={false} />
                </div>
                <div>
                  <div className="font-bold text-white text-xs">Laser Monogram</div>
                  <div className="text-[10px] text-slate-400">Biểu tượng Laser CAD & Snap Points</div>
                </div>
              </div>

              {/* Variant 2: Blueprint Isometric */}
              <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col items-center justify-center text-center space-y-3 group hover:border-amber-500/50 transition">
                <div className="p-2 bg-slate-900 rounded-lg border border-slate-800">
                  <HnlLogo size="lg" variant="blueprint" showText={false} />
                </div>
                <div>
                  <div className="font-bold text-white text-xs">Architectural Blueprint</div>
                  <div className="text-[10px] text-slate-400">Compa kỹ thuật & Lưới toạ độ Isometric</div>
                </div>
              </div>

              {/* Variant 3: Full Brand Header */}
              <div className="p-4 rounded-xl bg-slate-800/50 border border-slate-700/80 flex flex-col items-center justify-center text-center space-y-3 group hover:border-emerald-500/50 transition">
                <div className="p-2.5 bg-slate-900 rounded-lg border border-slate-800 w-full flex justify-center">
                  <HnlLogo size="sm" variant="horizontal" />
                </div>
                <div>
                  <div className="font-bold text-white text-xs">Full Horizontal Lockup</div>
                  <div className="text-[10px] text-slate-400">Sử dụng trên Header & Ribbon Toolbar</div>
                </div>
              </div>
            </div>
          </div>

          {/* Color Palette Definition: HNL Blue & Professional Slate */}
          <div>
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3 flex items-center gap-1.5">
              <Palette className="w-3.5 h-3.5 text-sky-400" />
              Bảng Màu Chuẩn (HNL Blue & Professional Slate)
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
              {HNL_PALETTE_TOKENS.map((token) => (
                <div
                  key={token.name}
                  className="p-3 rounded-xl bg-slate-800/60 border border-slate-700/80 flex flex-col justify-between hover:bg-slate-800 transition"
                >
                  <div className="flex items-center space-x-3">
                    <div
                      className="w-10 h-10 rounded-lg border border-white/20 shadow-md flex-shrink-0"
                      style={{ backgroundColor: token.hex }}
                    />
                    <div className="flex-1 min-w-0">
                      <div className="font-bold text-white text-xs truncate">{token.name}</div>
                      <div className="text-[11px] font-mono text-slate-400 flex items-center gap-1">
                        <span>{token.hex}</span>
                        <button
                          onClick={() => handleCopy(token.hex)}
                          className="text-slate-500 hover:text-sky-400 transition"
                          title="Sao chép mã màu HEX"
                        >
                          {copiedHex === token.hex ? (
                            <Check className="w-3 h-3 text-emerald-400" />
                          ) : (
                            <Copy className="w-3 h-3" />
                          )}
                        </button>
                      </div>
                    </div>
                  </div>
                  <div className="mt-2 text-[10px] text-slate-400 line-clamp-2 border-t border-slate-700/50 pt-1.5">
                    {token.usage}
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Copyright & Organization */}
          <div className="p-3.5 rounded-lg bg-slate-900/90 border border-slate-800 text-[11px] text-slate-400 flex flex-col sm:flex-row items-center justify-between gap-2">
            <span>{HNL_BRAND.copyright}</span>
            <span className="text-slate-500 font-mono">Author: {HNL_BRAND.author}</span>
          </div>
        </div>

        {/* Footer */}
        <div className="h-12 px-6 bg-slate-900 border-t border-slate-800 flex items-center justify-end">
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold transition"
          >
            Đóng bảng hướng dẫn
          </button>
        </div>
      </div>
    </div>
  );
};
