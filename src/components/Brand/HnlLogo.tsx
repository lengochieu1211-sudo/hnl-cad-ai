import React from "react";
import { HNL_BRAND } from "../../lib/branding";

export type HnlLogoVariant = "monogram" | "blueprint" | "minimal" | "full" | "horizontal" | "badge";
export type HnlColorScheme = "blue-slate" | "cyan-gold" | "monochrome" | "light";

interface HnlLogoProps {
  size?: "xs" | "sm" | "md" | "lg" | "xl";
  variant?: HnlLogoVariant;
  colorScheme?: HnlColorScheme;
  showText?: boolean;
  showSubtitle?: boolean;
  showBadge?: boolean;
  className?: string;
}

export const HnlLogo: React.FC<HnlLogoProps> = ({
  size = "md",
  variant = "horizontal",
  colorScheme = "blue-slate",
  showText = true,
  showSubtitle = true,
  showBadge = true,
  className = "",
}) => {
  const iconDimensions = {
    xs: "w-5 h-5",
    sm: "w-7 h-7",
    md: "w-9 h-9",
    lg: "w-12 h-12",
    xl: "w-16 h-16",
  };

  const titleSizes = {
    xs: "text-[11px]",
    sm: "text-xs",
    md: "text-sm",
    lg: "text-lg",
    xl: "text-2xl",
  };

  // Render SVG Symbol based on chosen variant
  const renderSvgSymbol = () => {
    if (variant === "blueprint") {
      return (
        <svg
          viewBox="0 0 100 100"
          className="w-full h-full drop-shadow-[0_2px_8px_rgba(0,163,255,0.45)]"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          <defs>
            <linearGradient id="hnlBlueGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#00E5FF" />
              <stop offset="50%" stopColor="#00A3FF" />
              <stop offset="100%" stopColor="#0284C7" />
            </linearGradient>
            <linearGradient id="hnlCompassGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#F59E0B" />
              <stop offset="100%" stopColor="#D97706" />
            </linearGradient>
          </defs>

          {/* Isometric Blueprint Frame */}
          <polygon
            points="50,6 92,28 92,72 50,94 8,72 8,28"
            stroke="url(#hnlBlueGrad)"
            strokeWidth="3.5"
            strokeDasharray="4 2"
            fill="#0F172A"
            fillOpacity="0.85"
          />

          {/* Internal Grid Lines */}
          <line x1="50" y1="6" x2="50" y2="94" stroke="#0284C7" strokeWidth="1.2" strokeOpacity="0.4" />
          <line x1="8" y1="28" x2="92" y2="72" stroke="#0284C7" strokeWidth="1.2" strokeOpacity="0.3" />
          <line x1="8" y1="72" x2="92" y2="28" stroke="#0284C7" strokeWidth="1.2" strokeOpacity="0.3" />

          {/* Drafting Compass Legs */}
          <path d="M 50 18 L 30 78 L 36 78 L 50 36 L 64 78 L 70 78 Z" fill="url(#hnlCompassGrad)" />
          {/* Compass Pivot Head */}
          <circle cx="50" cy="22" r="5" fill="#00E5FF" stroke="#FFFFFF" strokeWidth="1.5" />

          {/* Central Stylized Monogram letters */}
          <text
            x="50"
            y="66"
            textAnchor="middle"
            fill="#FFFFFF"
            fontSize="18"
            fontFamily="sans-serif"
            fontWeight="900"
            letterSpacing="1.5"
          >
            HNL
          </text>
        </svg>
      );
    }

    if (variant === "minimal") {
      return (
        <svg viewBox="0 0 100 100" className="w-full h-full" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect width="100" height="100" rx="22" fill="#0F172A" stroke="#00A3FF" strokeWidth="4" />
          <path d="M 22 28 L 34 28 L 34 46 L 48 46 L 48 28 L 60 28 L 60 72 L 48 72 L 48 56 L 34 56 L 34 72 L 22 72 Z" fill="#00E5FF" />
          <path d="M 62 28 L 72 28 L 78 54 L 78 28 L 86 28 L 86 72 L 76 72 L 70 46 L 70 72 L 62 72 Z" fill="#F59E0B" />
        </svg>
      );
    }

    // Default Monogram & Horizontal variant: Laser Architectural Monogram
    return (
      <svg
        viewBox="0 0 100 100"
        className="w-full h-full drop-shadow-[0_2px_8px_rgba(0,229,255,0.4)]"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <linearGradient id="hnlPrimaryCyan" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#00E5FF" />
            <stop offset="100%" stopColor="#0284C7" />
          </linearGradient>
          <linearGradient id="hnlAccentGold" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FBBF24" />
            <stop offset="100%" stopColor="#EA580C" />
          </linearGradient>
          <linearGradient id="hnlBaseWhite" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FFFFFF" />
            <stop offset="100%" stopColor="#94A3B8" />
          </linearGradient>
        </defs>

        {/* Letter 'H' - Left pillar & bridge */}
        <path
          d="M 16 22 L 26 22 L 26 44 L 42 44 L 42 22 L 52 22 L 52 78 L 42 78 L 42 54 L 26 54 L 26 78 L 16 78 Z"
          fill="url(#hnlPrimaryCyan)"
        />

        {/* Letter 'N' - Diagonal Laser Beam */}
        <path
          d="M 52 22 L 62 22 L 76 60 L 76 22 L 86 22 L 86 78 L 76 78 L 62 40 L 62 78 L 52 78 Z"
          fill="url(#hnlAccentGold)"
        />

        {/* Letter 'L' - Precision Bottom Base Line */}
        <path
          d="M 16 80 L 86 80 L 86 88 L 16 88 Z"
          fill="url(#hnlBaseWhite)"
        />

        {/* Cad Precision Snap Points */}
        <circle cx="21" cy="22" r="3" fill="#00E5FF" />
        <circle cx="81" cy="22" r="3" fill="#FBBF24" />
        <circle cx="81" cy="84" r="3" fill="#FFFFFF" />
      </svg>
    );
  };

  return (
    <div className={`flex items-center space-x-2.5 select-none ${className}`}>
      {/* Official HNL logo supplied by the user */}
      <div className={`relative ${iconDimensions[size]} flex-shrink-0 group`}>
        <div className="absolute -inset-0.5 rounded-xl bg-sky-500/25 blur-[3px] opacity-70 group-hover:opacity-100 transition" />
        <div className="relative w-full h-full rounded-xl overflow-hidden shadow-xl ring-1 ring-white/15 bg-white/95">
          <img
            src="/hnl-logo.png"
            alt="HNL"
            className="w-full h-full object-contain"
            draggable={false}
          />
        </div>
      </div>

      {/* Typography Brand Name */}
      {showText && (
        <div className="flex flex-col leading-tight">
          <div className="flex items-center space-x-1.5">
            <span className={`font-black tracking-wider text-white ${titleSizes[size]} flex items-center`}>
              HNL
              <span className="text-sky-400 font-extrabold ml-1">CAD AI</span>
            </span>
            {showBadge && (
              <span className="px-1.5 py-0.2 rounded text-[9px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30 font-mono">
                PRO
              </span>
            )}
          </div>
          {showSubtitle && (
            <span className="text-[10px] text-slate-400 font-medium tracking-normal">
              {HNL_BRAND.tagline.split(",")[0]}
            </span>
          )}
        </div>
      )}
    </div>
  );
};
