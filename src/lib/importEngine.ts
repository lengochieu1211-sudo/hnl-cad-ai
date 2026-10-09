import { CadEntity } from "../types/cad";

/**
 * Minimal ASCII DXF importer for Standalone mode.
 * Supports LINE, CIRCLE, TEXT, MTEXT and LWPOLYLINE.
 * It intentionally does not claim full DWG/DXF fidelity.
 */
export function parseBasicDxf(content: string): CadEntity[] {
  const raw = content.replace(/\r/g, "").split("\n");
  const pairs: Array<[string, string]> = [];
  for (let i = 0; i + 1 < raw.length; i += 2) {
    pairs.push([raw[i].trim(), raw[i + 1].trim()]);
  }

  const entities: CadEntity[] = [];
  let inEntities = false;
  let i = 0;
  const num = (v?: string) => Number.parseFloat(v || "0") || 0;
  const makeId = (type: string, idx: number) => `dxf_${type.toLowerCase()}_${Date.now()}_${idx}`;

  while (i < pairs.length) {
    const [code, value] = pairs[i];
    if (code === "0" && value === "SECTION" && pairs[i + 1]?.[0] === "2" && pairs[i + 1]?.[1] === "ENTITIES") {
      inEntities = true;
      i += 2;
      continue;
    }
    if (inEntities && code === "0" && value === "ENDSEC") break;
    if (!inEntities || code !== "0") {
      i++;
      continue;
    }

    const type = value.toUpperCase();
    const props: Record<string, string[]> = {};
    let j = i + 1;
    while (j < pairs.length && pairs[j][0] !== "0") {
      const [c, v] = pairs[j];
      (props[c] ||= []).push(v);
      j++;
    }
    const first = (c: string) => props[c]?.[0];
    const layer = first("8") || "0";
    const color = "#FFFFFF";
    const handle = first("5") || Math.random().toString(16).substring(2, 8).toUpperCase();

    if (type === "LINE") {
      entities.push({
        id: makeId(type, entities.length), handle, type: "LINE", layer, color,
        start: { x: num(first("10")), y: num(first("20")) },
        end: { x: num(first("11")), y: num(first("21")) },
      } as any);
    } else if (type === "CIRCLE") {
      entities.push({
        id: makeId(type, entities.length), handle, type: "CIRCLE", layer, color,
        center: { x: num(first("10")), y: num(first("20")) },
        radius: Math.abs(num(first("40"))),
      } as any);
    } else if (type === "TEXT" || type === "MTEXT") {
      const chunks = props["1"] || [];
      entities.push({
        id: makeId(type, entities.length), handle, type, layer, color,
        position: { x: num(first("10")), y: num(first("20")) },
        text: chunks.join(""), height: Math.abs(num(first("40"))) || 200,
        rotationDeg: num(first("50")),
      } as any);
    } else if (type === "LWPOLYLINE") {
      const xs = props["10"] || [];
      const ys = props["20"] || [];
      const vertices = xs.map((x, idx) => ({ x: num(x), y: num(ys[idx]) }));
      if (vertices.length >= 2) {
        entities.push({
          id: makeId(type, entities.length), handle, type: "POLYLINE", layer, color,
          points: vertices, closed: (Number(first("70") || "0") & 1) === 1,
        } as any);
      }
    }
    i = j;
  }
  return entities;
}
