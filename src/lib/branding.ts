/**
 * HNL CAD AI STUDIO - BRANDING & COLOR PALETTE DEFINITION
 * Professional Architectural & Engineering Theme
 */

export interface BrandColorToken {
  name: string;
  hex: string;
  rgb: string;
  usage: string;
}

export const HNL_BRAND = {
  name: "HNL CAD AI TOOL",
  shortName: "HNL CAD",
  code: "HNL",
  edition: "Professional Shopdrawing & AI Edition",
  version: "v2.6.0",
  tagline: "Bộ công cụ CAD AI, Shopdrawing & Tự động hoá Bản vẽ Chuyên nghiệp",
  author: "HNL Architecture & Construction Solutions",
  authorShort: "HNL Architecture",
  website: "https://hnlcad.vn",
  supportEmail: "support@hnlcad.vn",
  copyright: "© 2026 HNL Architecture & CAD AI Studio. All rights reserved.",
  standardCode: "TCVN 9377:2012 / HNL CAD Standard Specification",
  titleBlockDefault: "HNL_TITLE_A3",
  
  // Official HNL Palette Definition: HNL Blue & Professional Slate
  colors: {
    // Primary HNL Blue Scale
    primary: {
      50: "#f0f9ff",
      100: "#e0f2fe",
      200: "#bae6fd",
      300: "#7dd3fc",
      400: "#38bdf8",
      500: "#00A3FF", // Core HNL Electric CAD Blue
      600: "#0284c7", // Precision Active Blue
      700: "#0369a1",
      800: "#075985",
      900: "#0c4a6e",
      950: "#082f49",
      accent: "#00E5FF", // Neon Cyan Highlight
    },
    // Professional Slate Scale (Dark Canvas & Engineering Surfaces)
    slate: {
      50: "#f8fafc",
      100: "#f1f5f9",
      200: "#e2e8f0",
      300: "#cbd5e1",
      400: "#94a3b8",
      500: "#64748b",
      600: "#475569",
      700: "#334155",
      800: "#1e293b", // Modal / Panel Background
      850: "#172033", // Deep Panel Base
      900: "#0f172a", // Application Workspace Background
      950: "#0a0f1d", // Ultimate Dark Ingress
    },
    // Engineering Accent Colors
    accents: {
      gold: "#F59E0B",     // Drywall & Fire Resistance (EI60/EI120)
      amber: "#D97706",
      emerald: "#10B981",  // Success, Dimension Snap, Audit OK
      rose: "#F43F5E",     // Cad Warning, Clash Detection
      violet: "#8B5CF6",   // AI Generative & Layout Optimization
      cyan: "#06B6D4",     // MLeader & Precision Vector Lines
    }
  }
};

export const HNL_PALETTE_TOKENS: BrandColorToken[] = [
  {
    name: "HNL Core Blue",
    hex: "#00A3FF",
    rgb: "0, 163, 255",
    usage: "Primary brand color, key action buttons, and active ribbon tabs",
  },
  {
    name: "HNL Neon Cyan",
    hex: "#00E5FF",
    rgb: "0, 229, 255",
    usage: "Multileader annotations, snap indicators, vector cursors, and AI highlights",
  },
  {
    name: "Professional Slate 900",
    hex: "#0F172A",
    rgb: "15, 23, 42",
    usage: "Main CAD drawing viewport background & dark ribbon surface",
  },
  {
    name: "Professional Slate 800",
    hex: "#1E293B",
    rgb: "30, 41, 59",
    usage: "Docked tool palette, modal window headers, and property inspector backgrounds",
  },
  {
    name: "Technical Gold",
    hex: "#F59E0B",
    rgb: "245, 158, 11",
    usage: "Thạch cao PCCC EI30-EI120 indicators, BOQ material badges, and preset highlights",
  },
  {
    name: "Precision Emerald",
    hex: "#10B981",
    rgb: "16, 185, 129",
    usage: "CAD audit pass status, Win 10/11 compatibility indicators, and verified dimensions",
  }
];
