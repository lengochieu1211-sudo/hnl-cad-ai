import React, { useState } from "react";
import {
  OsnapMode,
  OsnapPoint,
  OsnapSettings,
  Point2D,
} from "../../types/cad";
import {
  Magnet,
  Square,
  Triangle,
  Circle,
  X,
  CornerDownRight,
  Compass,
  Hourglass,
  Crosshair,
  Sliders,
  ChevronDown,
  Check,
  Eye,
  Activity,
} from "lucide-react";

interface OsnapIndicatorProps {
  currentSnap: OsnapPoint | null;
  settings: OsnapSettings;
  onUpdateSettings: (newSettings: OsnapSettings) => void;
  activeOriginPoint?: Point2D | null;
}

export const OsnapIndicator: React.FC<OsnapIndicatorProps> = ({
  currentSnap,
  settings,
  onUpdateSettings,
  activeOriginPoint,
}) => {
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const toggleMode = (mode: OsnapMode) => {
    onUpdateSettings({
      ...settings,
      modes: {
        ...settings.modes,
        [mode]: !settings.modes[mode],
      },
    });
  };

  const getModeIcon = (mode: OsnapMode) => {
    switch (mode) {
      case "ENDPOINT":
        return <Square className="w-3.5 h-3.5 text-emerald-400" />;
      case "MIDPOINT":
        return <Triangle className="w-3.5 h-3.5 text-emerald-400" />;
      case "CENTER":
        return <Circle className="w-3.5 h-3.5 text-emerald-400" />;
      case "INTERSECTION":
        return <X className="w-3.5 h-3.5 text-emerald-400" />;
      case "PERPENDICULAR":
        return <CornerDownRight className="w-3.5 h-3.5 text-emerald-400" />;
      case "QUADRANT":
        return <Compass className="w-3.5 h-3.5 text-emerald-400" />;
      case "NEAREST":
        return <Hourglass className="w-3.5 h-3.5 text-emerald-400" />;
      case "EXTENSION":
        return <Crosshair className="w-3.5 h-3.5 text-emerald-400" />;
      default:
        return <Magnet className="w-3.5 h-3.5 text-emerald-400" />;
    }
  };

  const getModeLabel = (mode: OsnapMode) => {
    switch (mode) {
      case "ENDPOINT":
        return "Endpoint (Điểm mút/Góc)";
      case "MIDPOINT":
        return "Midpoint (Trung điểm)";
      case "CENTER":
        return "Center (Tâm)";
      case "INTERSECTION":
        return "Intersection (Giao điểm)";
      case "PERPENDICULAR":
        return "Perpendicular (Vuông góc)";
      case "QUADRANT":
        return "Quadrant (Góc phần tư 90°)";
      case "EXTENSION":
        return "Extension / OTrack (Gióng trục)";
      case "NEAREST":
        return "Nearest (Điểm gần nhất)";
      case "NODE":
        return "Node / Origin (Gốc)";
      default:
        return mode;
    }
  };

  const osnapModesList: OsnapMode[] = [
    "ENDPOINT",
    "MIDPOINT",
    "CENTER",
    "INTERSECTION",
    "PERPENDICULAR",
    "QUADRANT",
    "EXTENSION",
    "NEAREST",
  ];

  const activeModesCount = Object.values(settings.modes).filter(Boolean).length;

  return (
    <>
      {/* 1. Real-time Floating Magnetic Snap HUD Indicator (top center or near cursor) */}
      {currentSnap && settings.enabled && (
        <div
          className="pointer-events-none absolute z-30 flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-[#141619]/95 border border-emerald-500/70 shadow-2xl backdrop-blur-md transition-all duration-75 text-xs text-white animate-in fade-in zoom-in-95"
          style={{
            left: `${currentSnap.screenPos.x + 18}px`,
            top: `${currentSnap.screenPos.y - 36}px`,
          }}
        >
          <div className="flex items-center justify-center w-5 h-5 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/40">
            {getModeIcon(currentSnap.mode)}
          </div>
          <div>
            <div className="flex items-center space-x-1.5">
              <span className="font-bold text-emerald-400 tracking-wide">
                {currentSnap.mode}
              </span>
              <span className="text-[10px] text-neutral-400 font-mono">
                X:{currentSnap.point.x} Y:{currentSnap.point.y}
              </span>
            </div>
            <div className="text-[10px] text-neutral-300 truncate max-w-[220px]">
              {currentSnap.sourceDescription}
            </div>
          </div>
        </div>
      )}

      {/* 2. Floating Osnap Quick Switcher & Popover Settings in Canvas Top/Bottom Area */}
      <div className="relative inline-block">
        <button
          onClick={() => setIsMenuOpen((prev) => !prev)}
          className={`flex items-center space-x-1.5 px-2.5 py-1 rounded text-xs font-medium transition ${
            settings.enabled
              ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 shadow-sm"
              : "bg-neutral-800 text-neutral-400 hover:text-neutral-200 border border-neutral-700"
          }`}
          title="Bật/Tắt Object Snap [F3] & Cấu hình điểm bắt"
        >
          <Magnet className={`w-3.5 h-3.5 ${settings.enabled ? "text-emerald-400" : "text-neutral-500"}`} />
          <span className="font-mono font-bold">OSNAP</span>
          <span className="text-[10px] px-1 py-0.2 bg-black/40 rounded text-emerald-300">
            {settings.enabled ? `${activeModesCount}` : "OFF"}
          </span>
          <ChevronDown className="w-3 h-3 text-neutral-400" />
        </button>

        {/* Osnap Settings Menu Popover */}
        {isMenuOpen && (
          <div className="absolute right-0 bottom-full mb-2 w-72 bg-[#181A1E] border border-neutral-700 rounded-xl shadow-2xl z-50 p-3 text-xs text-neutral-200 backdrop-blur-xl">
            {/* Header */}
            <div className="flex items-center justify-between pb-2 border-b border-neutral-800 mb-2">
              <div className="flex items-center space-x-2">
                <Magnet className="w-4 h-4 text-emerald-400" />
                <span className="font-bold text-white">Chế Độ Bắt Điểm (Osnap F3)</span>
              </div>
              <button
                onClick={() =>
                  onUpdateSettings({
                    ...settings,
                    enabled: !settings.enabled,
                  })
                }
                className={`px-2 py-0.5 rounded text-[11px] font-bold ${
                  settings.enabled
                    ? "bg-emerald-500 text-black"
                    : "bg-neutral-700 text-neutral-300"
                }`}
              >
                {settings.enabled ? "BẬT" : "TẮT"}
              </button>
            </div>

            {/* Tracking (OTrack F11) Quick Toggle */}
            <div className="flex items-center justify-between px-2 py-1.5 mb-2 rounded bg-neutral-900/60 border border-neutral-800">
              <div className="flex items-center space-x-2">
                <Crosshair className="w-3.5 h-3.5 text-sky-400" />
                <span className="text-neutral-300">OTrack Gióng Hướng [F11]</span>
              </div>
              <input
                type="checkbox"
                checked={settings.trackingEnabled}
                onChange={(e) =>
                  onUpdateSettings({
                    ...settings,
                    trackingEnabled: e.target.checked,
                  })
                }
                className="accent-emerald-500 rounded cursor-pointer"
              />
            </div>

            {/* List of Osnap Modes */}
            <div className="space-y-1 max-h-56 overflow-y-auto pr-1">
              {osnapModesList.map((mode) => {
                const isChecked = settings.modes[mode];
                return (
                  <button
                    key={mode}
                    onClick={() => toggleMode(mode)}
                    className={`w-full flex items-center justify-between px-2 py-1.5 rounded transition text-left ${
                      isChecked
                        ? "bg-emerald-500/10 text-emerald-200 border border-emerald-500/20"
                        : "hover:bg-neutral-800/80 text-neutral-400"
                    }`}
                  >
                    <div className="flex items-center space-x-2">
                      {getModeIcon(mode)}
                      <span className="text-[11px]">{getModeLabel(mode)}</span>
                    </div>
                    {isChecked ? (
                      <Check className="w-3.5 h-3.5 text-emerald-400" />
                    ) : (
                      <span className="w-3.5 h-3.5" />
                    )}
                  </button>
                );
              })}
            </div>

            {/* Aperture Size Slider */}
            <div className="mt-3 pt-2 border-t border-neutral-800 space-y-1">
              <div className="flex justify-between text-[11px] text-neutral-400">
                <span>Độ nhạy bắt điểm (Aperture)</span>
                <span className="font-mono text-emerald-400">{settings.apertureSizePx}px</span>
              </div>
              <input
                type="range"
                min={8}
                max={32}
                step={2}
                value={settings.apertureSizePx}
                onChange={(e) =>
                  onUpdateSettings({
                    ...settings,
                    apertureSizePx: parseInt(e.target.value, 10),
                  })
                }
                className="w-full accent-emerald-500 h-1 bg-neutral-700 rounded-lg cursor-pointer"
              />
            </div>

            {/* Action Bar */}
            <div className="mt-2.5 flex items-center justify-between text-[10px] text-neutral-500">
              <button
                onClick={() => {
                  const allTrue: Record<OsnapMode, boolean> = {
                    ENDPOINT: true,
                    MIDPOINT: true,
                    CENTER: true,
                    INTERSECTION: true,
                    PERPENDICULAR: true,
                    NEAREST: true,
                    QUADRANT: true,
                    EXTENSION: true,
                    NODE: true,
                  };
                  onUpdateSettings({ ...settings, modes: allTrue });
                }}
                className="hover:text-emerald-400 underline"
              >
                Chọn tất cả
              </button>
              <button
                onClick={() => {
                  const standard: Record<OsnapMode, boolean> = {
                    ENDPOINT: true,
                    MIDPOINT: true,
                    CENTER: true,
                    INTERSECTION: true,
                    PERPENDICULAR: true,
                    NEAREST: false,
                    QUADRANT: true,
                    EXTENSION: true,
                    NODE: true,
                  };
                  onUpdateSettings({ ...settings, modes: standard });
                }}
                className="hover:text-neutral-300 underline"
              >
                Mặc định CAD
              </button>
              <button
                onClick={() => setIsMenuOpen(false)}
                className="px-2 py-1 bg-neutral-800 hover:bg-neutral-700 text-white rounded font-medium"
              >
                Đóng
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
};
