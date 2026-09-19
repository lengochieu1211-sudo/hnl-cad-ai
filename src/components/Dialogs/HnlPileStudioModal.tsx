import React, { useMemo, useState } from 'react';
import { X, CircleDot, ShieldCheck, AlertTriangle, Table2 } from 'lucide-react';
import { generatePilePlan, PileInput, PileScheduleRow, PileType } from '../../lib/pileEngine';
import { CadEntity } from '../../types/cad';

type Props = { isOpen: boolean; onClose: () => void; onApply: (entities: CadEntity[], schedule: PileScheduleRow[]) => void };

export const HnlPileStudioModal: React.FC<Props> = ({ isOpen, onClose, onApply }) => {
  const [type, setType] = useState<PileType>('PHC');
  const [size, setSize] = useState(600);
  const [length, setLength] = useState(18000);
  const [rows, setRows] = useState(2);
  const [cols, setCols] = useState(2);
  const [spacingX, setSpacingX] = useState(1800);
  const [spacingY, setSpacingY] = useState(1800);
  const [manufacturer, setManufacturer] = useState('Phan Vu / Custom');
  const [standard, setStandard] = useState('Theo Project Spec / Approved Material');
  const [verified, setVerified] = useState(false);

  const input: PileInput = useMemo(() => ({ type, diameterOrWidth: size, length, rows, cols, spacingX, spacingY, startX: 0, startY: 0, manufacturer, standard, verified }), [type,size,length,rows,cols,spacingX,spacingY,manufacturer,standard,verified]);
  const preview = useMemo(() => generatePilePlan(input), [input]);
  if (!isOpen) return null;

  const field = (label: string, value: number, set: (v:number)=>void) => <label className="text-xs text-neutral-300"><span className="block mb-1 text-neutral-500">{label}</span><input type="number" value={value} onChange={e=>set(Number(e.target.value))} className="w-full bg-neutral-900 border border-neutral-700 rounded px-2 py-1.5" /></label>;
  return <div className="fixed inset-0 z-[120] bg-black/70 flex items-center justify-center p-4">
    <div className="w-[min(980px,96vw)] max-h-[92vh] overflow-hidden rounded-xl border border-neutral-700 bg-[#191b1f] shadow-2xl flex flex-col">
      <div className="flex items-center justify-between px-5 py-3 border-b border-neutral-800 bg-[#121417]"><div><h2 className="font-bold text-cyan-300 flex items-center gap-2"><CircleDot className="w-5 h-5"/>HNL Pile Studio</h2><p className="text-xs text-neutral-500 mt-1">Mặt bằng cọc tham số • Schedule • kiểm soát nguồn kỹ thuật</p></div><button onClick={onClose} className="p-2 hover:bg-neutral-800 rounded"><X className="w-4 h-4"/></button></div>
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_1.2fr] gap-4 p-5 overflow-auto">
        <div className="space-y-3">
          <label className="text-xs text-neutral-300"><span className="block mb-1 text-neutral-500">Loại cọc</span><select value={type} onChange={e=>setType(e.target.value as PileType)} className="w-full bg-neutral-900 border border-neutral-700 rounded px-2 py-2"><option value="PHC">PHC - cọc ly tâm dự ứng lực</option><option value="PC">PC - cọc ly tâm</option><option value="SQUARE_RC">Cọc vuông BTCT</option><option value="CUSTOM">Custom</option></select></label>
          <div className="grid grid-cols-2 gap-3">{field(type==='SQUARE_RC'?'Cạnh (mm)':'Đường kính (mm)',size,setSize)}{field('Chiều dài (mm)',length,setLength)}{field('Số hàng',rows,setRows)}{field('Số cột',cols,setCols)}{field('Khoảng X (mm)',spacingX,setSpacingX)}{field('Khoảng Y (mm)',spacingY,setSpacingY)}</div>
          <label className="text-xs"><span className="block mb-1 text-neutral-500">Nhà sản xuất</span><input value={manufacturer} onChange={e=>setManufacturer(e.target.value)} className="w-full bg-neutral-900 border border-neutral-700 rounded px-2 py-1.5"/></label>
          <label className="text-xs"><span className="block mb-1 text-neutral-500">Nguồn / tiêu chuẩn dự án</span><input value={standard} onChange={e=>setStandard(e.target.value)} className="w-full bg-neutral-900 border border-neutral-700 rounded px-2 py-1.5"/></label>
          <label className="flex items-start gap-2 p-3 rounded border border-amber-700/40 bg-amber-950/20 text-xs"><input type="checkbox" checked={verified} onChange={e=>setVerified(e.target.checked)} className="mt-0.5"/><span><b className="text-amber-300">Đã đối chiếu hồ sơ dự án</b><br/><span className="text-neutral-400">Chỉ bật khi cấu tạo/sản phẩm đã được xác nhận bằng Project Spec, Approved Material hoặc bản vẽ được duyệt.</span></span></label>
        </div>
        <div className="space-y-3">
          <div className={`rounded-lg border p-3 text-xs flex gap-2 ${verified?'border-emerald-700/50 bg-emerald-950/20':'border-amber-700/50 bg-amber-950/20'}`}>{verified?<ShieldCheck className="w-5 h-5 text-emerald-400"/>:<AlertTriangle className="w-5 h-5 text-amber-400"/>}<div><b>{verified?'VERIFIED BY USER':'NEEDS CONFIRMATION'}</b><div className="text-neutral-400 mt-1">HNL không suy luận sức chịu tải địa chất từ catalog nhà sản xuất và không xem capacity cấu kiện = allowable pile load.</div></div></div>
          <div className="rounded-lg border border-neutral-800 bg-neutral-950/50 p-3"><div className="flex items-center gap-2 text-sm font-semibold mb-2"><Table2 className="w-4 h-4 text-cyan-400"/>Preview Schedule ({preview.schedule.length} cọc)</div><div className="max-h-64 overflow-auto"><table className="w-full text-xs"><thead className="sticky top-0 bg-neutral-900"><tr><th>ID</th><th>X</th><th>Y</th><th>Type</th><th>Size</th><th>L</th></tr></thead><tbody>{preview.schedule.map(r=><tr key={r.id} className="border-t border-neutral-800 text-center"><td className="py-1 text-cyan-300">{r.id}</td><td>{r.x}</td><td>{r.y}</td><td>{r.type}</td><td>{r.size}</td><td>{r.length}</td></tr>)}</tbody></table></div></div>
          <div className="text-xs text-neutral-500">Giai đoạn này tạo Smart Pile plan 2D + tag + schedule nội bộ. Detail đầu cọc/mối nối, as-built deviation và tính địa kỹ thuật phải dùng dữ liệu dự án được kiểm chứng.</div>
        </div>
      </div>
      <div className="px-5 py-3 border-t border-neutral-800 flex justify-end gap-2"><button onClick={onClose} className="px-4 py-2 text-xs rounded bg-neutral-800">Hủy</button><button onClick={()=>{onApply(preview.entities,preview.schedule);onClose();}} className="px-4 py-2 text-xs font-bold rounded bg-cyan-600 hover:bg-cyan-500 text-white">Tạo mặt bằng cọc</button></div>
    </div>
  </div>;
};
