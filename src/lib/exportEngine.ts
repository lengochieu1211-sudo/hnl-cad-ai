import { CadEntity, CadWall, CadCeilingGrid, CadLine, CadPolyline, CadCircle, CadText, CadDimension, CadMLeader, CadTable } from "../types/cad";

/**
 * Generates an AutoCAD-compliant DXF ASCII file string
 */
export function generateAutoCadDxf(entities: CadEntity[], drawingName: string = "HNL_CAD_DRAWING"): string {
  const lines: string[] = [];

  // DXF HEADER
  lines.push("0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015"); // AutoCAD 2000 format
  lines.push("9", "$INSUNITS", "70", "4"); // 4 = Millimeters
  lines.push("0", "ENDSEC");

  // DXF TABLES (LAYERS)
  lines.push("0", "SECTION", "2", "TABLES");
  lines.push("0", "TABLE", "2", "LAYER", "70", "10");

  const standardLayers = [
    { name: "0", color: 7 },
    { name: "TUONG_220", color: 1 }, // Red
    { name: "TUONG_110", color: 2 }, // Yellow
    { name: "TRAN_THACH_CAO", color: 3 }, // Green
    { name: "KHUNG_XUONG_CHINH", color: 4 }, // Cyan
    { name: "KHUNG_XUONG_PHU", color: 6 }, // Magenta
    { name: "TY_TREO_M8", color: 5 }, // Blue
    { name: "KICHTHUOC_DIM", color: 8 }, // Gray
    { name: "GHICHU_TEXT", color: 7 }, // White
    { name: "MEP_THIETBI", color: 30 }, // Orange
  ];

  standardLayers.forEach((lay) => {
    lines.push("0", "LAYER", "2", lay.name, "70", "0", "62", `${lay.color}`, "6", "CONTINUOUS");
  });

  lines.push("0", "ENDTAB");
  lines.push("0", "ENDSEC");

  // DXF BLOCKS
  lines.push("0", "SECTION", "2", "BLOCKS", "0", "ENDSEC");

  // DXF ENTITIES
  lines.push("0", "SECTION", "2", "ENTITIES");

  entities.forEach((ent) => {
    switch (ent.type) {
      case "WALL": {
        const wall = ent as CadWall;
        const layer = wall.thickness >= 200 ? "TUONG_220" : "TUONG_110";
        lines.push("0", "LINE", "8", layer);
        lines.push("10", `${wall.p1.x}`, "20", `${wall.p1.y}`, "30", "0.0");
        lines.push("11", `${wall.p2.x}`, "21", `${wall.p2.y}`, "31", "0.0");
        break;
      }
      case "LINE": {
        const line = ent as any;
        if (line.start && line.end) {
          lines.push("0", "LINE", "8", ent.layer || "0");
          lines.push("10", `${line.start.x}`, "20", `${line.start.y}`, "30", "0.0");
          lines.push("11", `${line.end.x}`, "21", `${line.end.y}`, "31", "0.0");
        }
        break;
      }
      case "CIRCLE": {
        const circ = ent as any;
        if (circ.center) {
          lines.push("0", "CIRCLE", "8", ent.layer || "0");
          lines.push("10", `${circ.center.x}`, "20", `${circ.center.y}`, "30", "0.0");
          lines.push("40", `${circ.radius || 100}`);
        }
        break;
      }
      case "RECTANGLE": {
        const rect = ent as any;
        lines.push("0", "LWPOLYLINE", "8", ent.layer || "0", "90", "4", "70", "1");
        lines.push("10", `${rect.x}`, "20", `${rect.y}`);
        lines.push("10", `${rect.x + rect.width}`, "20", `${rect.y}`);
        lines.push("10", `${rect.x + rect.width}`, "20", `${rect.y + rect.height}`);
        lines.push("10", `${rect.x}`, "20", `${rect.y + rect.height}`);
        break;
      }
      case "POLYLINE": {
        const poly = ent as any;
        if (poly.points && poly.points.length > 0) {
          lines.push("0", "LWPOLYLINE", "8", ent.layer || "0");
          lines.push("90", `${poly.points.length}`);
          lines.push("70", poly.closed ? "1" : "0");
          poly.points.forEach((pt: any) => {
            lines.push("10", `${pt.x}`, "20", `${pt.y}`);
          });
        }
        break;
      }
      case "CEILING_GRID": {
        const clg = ent as CadCeilingGrid;
        // Boundary
        const minX = clg.x ?? (clg.boundary && clg.boundary.length > 0 ? Math.min(...clg.boundary.map((p) => p.x)) : 0);
        const minY = clg.y ?? (clg.boundary && clg.boundary.length > 0 ? Math.min(...clg.boundary.map((p) => p.y)) : 0);
        const width = clg.width ?? (clg.boundary && clg.boundary.length > 0 ? Math.max(...clg.boundary.map((p) => p.x)) - minX : 4000);
        const height = clg.height ?? (clg.boundary && clg.boundary.length > 0 ? Math.max(...clg.boundary.map((p) => p.y)) - minY : 3000);

        lines.push("0", "LWPOLYLINE", "8", "TRAN_THACH_CAO", "90", "4", "70", "1");
        lines.push("10", `${minX}`, "20", `${minY}`);
        lines.push("10", `${minX + width}`, "20", `${minY}`);
        lines.push("10", `${minX + width}`, "20", `${minY + height}`);
        lines.push("10", `${minX}`, "20", `${minY + height}`);
        break;
      }
      case "TEXT": {
        const txt = ent as any;
        if (txt.position) {
          lines.push("0", "TEXT", "8", "GHICHU_TEXT");
          lines.push("10", `${txt.position.x}`, "20", `${txt.position.y}`, "30", "0.0");
          lines.push("40", `${txt.height || 250}`);
          lines.push("1", `${txt.text || ""}`);
        }
        break;
      }
    }
  });

  lines.push("0", "ENDSEC");
  lines.push("0", "EOF");

  return lines.join("\n");
}

/**
 * Generates structured CSV for Excel BOQ (Bill of Quantities)
 */
export function generateExcelBoqCsv(entities: CadEntity[]): string {
  const walls = entities.filter((e) => e.type === "WALL") as CadWall[];
  const ceilings = entities.filter((e) => e.type === "CEILING_GRID") as CadCeilingGrid[];

  let totalWallAreaM2 = 0;
  walls.forEach((w) => {
    const len = Math.hypot(w.p2.x - w.p1.x, w.p2.y - w.p1.y) / 1000;
    const height = 3.6; // 3.6m
    totalWallAreaM2 += len * height;
  });

  let totalCeilingAreaM2 = 0;
  ceilings.forEach((c) => {
    const w = c.width ?? (c.boundary && c.boundary.length > 0 ? Math.max(...c.boundary.map((p) => p.x)) - Math.min(...c.boundary.map((p) => p.x)) : 4000);
    const h = c.height ?? (c.boundary && c.boundary.length > 0 ? Math.max(...c.boundary.map((p) => p.y)) - Math.min(...c.boundary.map((p) => p.y)) : 3000);
    totalCeilingAreaM2 += (Math.abs(w) * Math.abs(h)) / 1000000;
  });

  const rows = [
    ["HNL CAD AI - BẢNG DỰ TOÁN KHỐI LƯỢNG VẬT TƯ THI CÔNG TRẦN VÁCH THẠCH CAO"],
    ["Dự án", "DỰ ÁN SHOPDRAWING & NỘI THẤT CAO CẤP HNL CAD"],
    ["Tiêu chuẩn áp dụng", "TCVN 8256:2009 / QCVN 06:2022/BXD / Saint-Gobain Gyproc Specs"],
    [],
    [
      "STT",
      "Mã vật tư",
      "Mô tả quy cách vật liệu",
      "Đơn vị",
      "Khối lượng",
      "Hệ số hao hụt (%)",
      "KL nghiệm thu",
      "Đơn giá (VNĐ)",
      "Thành tiền (VNĐ)",
      "Ghi chú",
    ],
    [
      "1",
      "GYP-STD-12.5",
      "Tấm thạch cao tiêu chuẩn Gyproc Saint-Gobain 1200x2400x12.5mm",
      "m²",
      (totalCeilingAreaM2 * 1.05).toFixed(2),
      "5%",
      (totalCeilingAreaM2 * 1.05 * 1.05).toFixed(2),
      "165000",
      `=G6*H6`,
      "Trần chìm phòng khách/ngủ",
    ],
    [
      "2",
      "GYP-FIRE-12.5",
      "Tấm thạch cao chống cháy Gyproc FireBloc 12.5mm (Chống cháy EI60)",
      "m²",
      (totalWallAreaM2 * 2).toFixed(2),
      "8%",
      (totalWallAreaM2 * 2 * 1.08).toFixed(2),
      "245000",
      `=G7*H7`,
      "Vách ngăn 2 mặt 2 lớp",
    ],
    [
      "3",
      "KEEL-VT-SERRA",
      "Xương chính C-Channel C38 Vĩnh Tường Serra mạ hợp kim nhôm kẽm",
      "mét",
      ((totalCeilingAreaM2 / 0.8) * 1.1).toFixed(2),
      "10%",
      ((totalCeilingAreaM2 / 0.8) * 1.1).toFixed(2),
      "38500",
      `=G8*H8`,
      "Bước xương @800mm",
    ],
    [
      "4",
      "KEEL-VT-TRIP",
      "Xương phụ M-Bar Omega Vĩnh Tường Tripflex",
      "mét",
      ((totalCeilingAreaM2 / 0.4) * 1.1).toFixed(2),
      "10%",
      ((totalCeilingAreaM2 / 0.4) * 1.1).toFixed(2),
      "29000",
      `=G9*H9`,
      "Bước xương @400mm",
    ],
    [
      "5",
      "STUD-VT-C75",
      "Thanh đứng vách C75 dày 0.5mm V-Wall Vĩnh Tường",
      "mét",
      ((totalWallAreaM2 / 0.4) * 3.6).toFixed(2),
      "5%",
      ((totalWallAreaM2 / 0.4) * 3.6).toFixed(2),
      "52000",
      `=G10*H10`,
      "Khoảng cách @400mm",
    ],
    [
      "6",
      "INSUL-RW-50",
      "Bông khoáng cách âm chống cháy Rockwool 50mm (Tỷ trọng 60kg/m³)",
      "m²",
      totalWallAreaM2.toFixed(2),
      "5%",
      (totalWallAreaM2 * 1.05).toFixed(2),
      "115000",
      `=G11*H11`,
      "Nhét trong vách thạch cao",
    ],
    [
      "7",
      "ROD-M8-ANCHOR",
      "Ty ren M8 mạ kẽm + Nở đạn đính sàn BTCT + Pát treo 2 lỗ",
      "bộ",
      Math.ceil(totalCeilingAreaM2 * 1.4).toString(),
      "5%",
      Math.ceil(totalCeilingAreaM2 * 1.4).toString(),
      "18500",
      `=G12*H12`,
      "Khoảng cách ty @800mm",
    ],
  ];

  return rows.map((r) => r.map((cell) => `"${(cell || "").replace(/"/g, '""')}"`).join(",")).join("\n");
}

/**
 * Triggers instant browser download of a generated text/blob file
 */
export function downloadFile(filename: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
