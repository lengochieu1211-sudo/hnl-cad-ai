import React, { useRef, useEffect } from "react";
import { Point2D, DynamicInputState } from "../../types/cad";

interface DynamicInputHUDProps {
  mouseScreenPos: Point2D;
  worldPos: Point2D;
  startPoint: Point2D | null;
  state: DynamicInputState;
  onCommit: (targetWorldPos: Point2D) => void;
  onChangeLength: (val: string) => void;
  onChangeAngle: (val: string) => void;
  onToggleField: (field: "LENGTH" | "ANGLE" | "COORDS") => void;
  viewportWidth?: number;
  viewportHeight?: number;
}

export const DynamicInputHUD: React.FC<DynamicInputHUDProps> = ({
  mouseScreenPos,
  worldPos,
  startPoint,
  state,
  onCommit,
  onChangeLength,
  onChangeAngle,
  onToggleField,
  viewportWidth = 1000,
  viewportHeight = 700,
}) => {
  const lengthInputRef = useRef<HTMLInputElement>(null);
  const angleInputRef = useRef<HTMLInputElement>(null);

  // Compute live relative distance and angle if starting point exists
  let liveDistance = 0;
  let liveAngle = 0;
  if (startPoint) {
    const dx = worldPos.x - startPoint.x;
    const dy = worldPos.y - startPoint.y;
    liveDistance = Math.round(Math.hypot(dx, dy));
    let angleRad = Math.atan2(dy, dx);
    liveAngle = Math.round((angleRad * 180) / Math.PI);
    if (liveAngle < 0) liveAngle += 360;
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Tab") {
      e.preventDefault();
      onToggleField(state.activeField === "LENGTH" ? "ANGLE" : "LENGTH");
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (!startPoint) {
        onCommit(worldPos);
        return;
      }

      const parsedLen = parseFloat(state.lengthInput) || liveDistance;
      const parsedAng = parseFloat(state.angleInput) !== undefined && !isNaN(parseFloat(state.angleInput))
        ? parseFloat(state.angleInput)
        : liveAngle;

      const rad = (parsedAng * Math.PI) / 180;
      const targetPoint: Point2D = {
        x: Math.round(startPoint.x + parsedLen * Math.cos(rad)),
        y: Math.round(startPoint.y + parsedLen * Math.sin(rad)),
      };
      onCommit(targetPoint);
    }
  };

  if (!state.enabled) return null;

  const hudWidth = startPoint ? 330 : 180;
  const hudHeight = 40;
  const hudLeft = Math.max(8, Math.min(mouseScreenPos.x + 22, Math.max(8, viewportWidth - hudWidth - 8)));
  const hudTop = Math.max(8, Math.min(mouseScreenPos.y + 16, Math.max(8, viewportHeight - hudHeight - 8)));

  return (
    <div
      className="pointer-events-auto absolute z-40 flex items-center space-x-1 p-1 rounded-md bg-[#16181D]/95 border border-cyan-500/50 shadow-2xl backdrop-blur-md text-[11px] text-white select-none animate-in fade-in zoom-in-95"
      style={{
        left: `${hudLeft}px`,
        top: `${hudTop}px`,
      }}
    >
      {startPoint ? (
        <>
          {/* Dynamic Distance / Length Box */}
          <div className="flex items-center space-x-1 bg-black/50 px-1.5 py-0.5 rounded border border-neutral-700 focus-within:border-cyan-400">
            <span className="text-[10px] text-neutral-400 font-mono">L:</span>
            <input
              ref={lengthInputRef}
              type="text"
              placeholder={`${liveDistance}`}
              value={state.lengthInput}
              onChange={(e) => onChangeLength(e.target.value)}
              onKeyDown={handleKeyDown}
              className="w-16 bg-transparent text-cyan-300 font-mono font-bold outline-none text-right placeholder-cyan-500/60"
              autoFocus={state.activeField === "LENGTH"}
            />
            <span className="text-[9px] text-neutral-500">mm</span>
          </div>

          {/* Dynamic Angle Box */}
          <div className="flex items-center space-x-1 bg-black/50 px-1.5 py-0.5 rounded border border-neutral-700 focus-within:border-cyan-400">
            <span className="text-[10px] text-neutral-400 font-mono">∠:</span>
            <input
              ref={angleInputRef}
              type="text"
              placeholder={`${liveAngle}`}
              value={state.angleInput}
              onChange={(e) => onChangeAngle(e.target.value)}
              onKeyDown={handleKeyDown}
              className="w-10 bg-transparent text-amber-300 font-mono font-bold outline-none text-right placeholder-amber-500/60"
              autoFocus={state.activeField === "ANGLE"}
            />
            <span className="text-[9px] text-neutral-500">°</span>
          </div>

          <span className="text-[9px] text-neutral-400 font-mono px-1">
            [Tab] Chuyển | [Enter] Khóa
          </span>
        </>
      ) : (
        /* Dynamic Cursor Coordinates when picking origin */
        <div className="flex items-center space-x-2 px-1.5 py-0.5">
          <span className="font-mono text-cyan-400">X: {worldPos.x}</span>
          <span className="font-mono text-emerald-400">Y: {worldPos.y}</span>
        </div>
      )}
    </div>
  );
};
