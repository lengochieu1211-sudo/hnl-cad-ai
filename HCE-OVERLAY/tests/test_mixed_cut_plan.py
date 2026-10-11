#!/usr/bin/env python3
"""HNL HCE nominal mixed stock regression: positive and fail-closed cases."""
import importlib.util
import json
from pathlib import Path
source = Path(__file__).resolve().parents[1] / 'tools' / 'verify_mixed_cut_plan.py'
spec = importlib.util.spec_from_file_location('mixed_verify', source)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
base = {
    'status': 'NOMINAL_ZERO_KERF_CANDIDATE_NOT_MANUFACTURING_APPROVED',
    'boards': [{'index': 1, 'stock': 'S', 'width': 610, 'height': 610,
                'pieces': [{'code':'G1', 'x':0, 'y':0, 'w':610, 'h':300},
                           {'code':'G2', 'x':0, 'y':300, 'w':610, 'h':310}]}],
    'statistics': {'pieces': 2, 'cut_area_module_mm2': 372100,
                   'short_cut_boards': 1, 'long_cut_boards': 0,
                   'candidate_small_equivalents': 1,
                   'baseline_small_equivalents': 2,
                   'saved_small_equivalents': 1,
                   'physical_kerf_certified': False}}
assert mod.verify(base)['pieces'] == 2
for patch in ('overlap', 'duplicate', 'oversize', 'unsafe_kerf'):
    trial = json.loads(json.dumps(base))
    ps = trial['boards'][0]['pieces']
    if patch == 'overlap':
        ps[1]['y'] = 299
    elif patch == 'duplicate':
        ps[1]['code'] = 'G1'
    elif patch == 'oversize':
        ps[1]['h'] = 311
    elif patch == 'unsafe_kerf':
        trial['statistics']['physical_kerf_certified'] = True
    try:
        mod.verify(trial)
    except ValueError:
        continue
    raise AssertionError('Unsafe mixed stock plan was accepted: ' + patch)
print('HNL HCE MIXED NOMINAL SAFETY GATES PASS 5/5; protected Core not involved')
