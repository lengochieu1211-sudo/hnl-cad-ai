#!/usr/bin/env python3
"""HNL HCE PHYSICAL STOCK gate: 11 synthetic tests; no Core or AutoCAD."""
import copy
import importlib.util
from pathlib import Path

module_path = Path(__file__).resolve().parents[1] / 'tools' / 'verify_physical_cut_plan.py'
spec = importlib.util.spec_from_file_location('hce_physical', module_path)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

base = {
    'status':'NOMINAL_ZERO_KERF_CANDIDATE_NOT_MANUFACTURING_APPROVED',
    'boards':[{'index':1,'stock':'D','width':610,'height':1220,
               'pieces':[{'code':'G1','x':0,'y':0,'w':610,'h':610},
                         {'code':'G2','x':0,'y':610,'w':610,'h':610}]}],
    'statistics':{'pieces':2,'physical_kerf_certified':False}}
spec_data = {
    'unit':'mm', 'physical_stock_mm':{'S':[605,605],'D':[605,1210]},
    'kerf_mm':1,
    'finished_xy_mm_by_code':{'G1':[605,600],'G2':[605,600]}}
def fails(plan, settings, text):
    try:
        gate.verify_physical(plan,settings)
    except ValueError as exc:
        assert text in str(exc), (text,str(exc))
        return
    raise AssertionError('Unsafe physical plan accepted: '+text)

assert gate.verify_physical(base,spec_data)['boards'] == 1
assert gate.risk_screen(base)['fully_used_nominal'] == 1
assert gate.risk_screen(base)['two_610x610_on_one_610x1220'] == [1]

t=copy.deepcopy(spec_data)
t['finished_xy_mm_by_code']={'G1':[605,605],'G2':[605,605]}
fails(base,t,'NOT PHYSICALLY CUTTABLE')
t=copy.deepcopy(spec_data); t['kerf_mm']=0
fails(base,t,'kerf_mm')
t=copy.deepcopy(spec_data); t['kerf_mm']=-1
fails(base,t,'kerf_mm')
t=copy.deepcopy(spec_data); del t['finished_xy_mm_by_code']['G2']
fails(base,t,'Missing/extra')
t=copy.deepcopy(spec_data); t['physical_stock_mm']['D']=[605,1199]
fails(base,t,'NOT PHYSICALLY CUTTABLE')
t=copy.deepcopy(base); t['boards'][0]['pieces'][1]['code']='G1'
fails(t,spec_data,'Duplicate/invalid')
t=copy.deepcopy(base); t['boards'][0]['pieces'][1]['y']=609
fails(t,spec_data,'Nominal overlapping')
t=copy.deepcopy(spec_data); t['unit']='inch'
fails(base,t,'unit=mm')
t=copy.deepcopy(base); t['boards'][0]['pieces'][1]['y']=611
fails(t,spec_data,'Nominal overhang')
t=copy.deepcopy(spec_data); t['finished_xy_mm_by_code']['G2']=[604,605]
assert gate.verify_physical(base,t)['boards'] == 1
print('HNL HCE PHYSICAL CUT GATE 11/11 PASS: synthetic, no manufacturing Golden')
