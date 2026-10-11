#!/usr/bin/env python3
"""HNL Tool: FAIL-CLOSED physical stock/kerf gate for nominal HCE candidate.

Read-only research validation. A pass proves guillotine grouping feasibility
for supplied physical finished XY sizes plus kerf, NOT vendor manufacturing
Golden, original Hatch quantities or global minimum purchase cost.

Usage: python verify_physical_cut_plan.py --plan candidate.json --spec spec.json
Spec fields:
  unit: mm
  physical_stock_mm: {"S": [605,605], "D": [605,1210]}
  kerf_mm: strictly positive numeric value
  finished_xy_mm_by_code: {"G1-A": [width_mm,height_mm], ...}
Every G code requires its OWN supplier-approved finished XY size. Dimensions
are in placed XY orientation (no inferred automatic rotation/allowance).
"""
import argparse
from functools import lru_cache
import json
import math
from pathlib import Path

EPS = 1e-8

def number(value, label):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(label + ': positive numeric input required')
    value = float(value)
    if not math.isfinite(value) or value <= 0:
        raise ValueError(label + ': positive finite input required')
    return value

def read_spec(spec, required_codes):
    if spec.get('unit') != 'mm':
        raise ValueError('Physical spec must declare unit=mm')
    kerf = number(spec.get('kerf_mm'), 'kerf_mm')
    raw_stock = spec.get('physical_stock_mm')
    if not isinstance(raw_stock, dict) or set(raw_stock) != {'S', 'D'}:
        raise ValueError('Specify physical_stock_mm for both S and D')
    stock = {}
    for code, dimensions in raw_stock.items():
        if not isinstance(dimensions, list) or len(dimensions) != 2:
            raise ValueError(code + ': stock dimensions must be [width,height]')
        stock[code] = tuple(number(x, code + ' stock') for x in dimensions)
    raw_parts = spec.get('finished_xy_mm_by_code')
    if not isinstance(raw_parts, dict) or set(raw_parts) != required_codes:
        missing = sorted(required_codes - set(raw_parts or {})) if isinstance(raw_parts, dict) else sorted(required_codes)
        extra = sorted(set(raw_parts or {}) - required_codes) if isinstance(raw_parts, dict) else []
        raise ValueError('Missing/extra exact finished G codes: ' + str((missing[:5], extra[:5])))
    sizes = {}
    for code, dimensions in raw_parts.items():
        if not isinstance(dimensions, list) or len(dimensions) != 2:
            raise ValueError(code + ': finished XY must be [width,height]')
        sizes[code] = tuple(number(x, code) for x in dimensions)
    return kerf, stock, sizes

def physical_slicing_frontier(parts, sizes, kerf):
    """All Pareto-minimal physical bounding dimensions of straight-cut trees."""
    if not 1 <= len(parts) <= 24:
        raise ValueError('Expected 1..24 pieces per stock')
    for i, p in enumerate(parts):
        for key in ('x', 'y', 'w', 'h'):
            x = p.get(key)
            if isinstance(x, bool) or not isinstance(x, (int, float)) or not math.isfinite(x):
                raise ValueError('Invalid nominal rectangle coordinate')
        if p['x'] < 0 or p['y'] < 0 or p['w'] <= 0 or p['h'] <= 0:
            raise ValueError('Invalid nominal rectangle geometry')
        for q in parts[:i]:
            if (p['x'] < q['x']+q['w'] and q['x'] < p['x']+p['w'] and
                p['y'] < q['y']+q['h'] and q['y'] < p['y']+p['h']):
                raise ValueError('Nominal overlapping pieces')
    def pareto(candidates):
        result = []
        for w, h in sorted(set(candidates)):
            if not any(a <= w+EPS and b <= h+EPS for a, b in result):
                result = [(a,b) for a,b in result if not (w <= a+EPS and h <= b+EPS)]
                result.append((w,h))
        if len(result) > 10000:
            raise ValueError('Too many cut layouts: fail closed')
        return tuple(result)

    @lru_cache(maxsize=None)
    def recurse(indices):
        if len(indices) == 1:
            return (sizes[parts[indices[0]]['code']],)
        subset = [parts[i] for i in indices]
        result = []
        for axis, length in (('x','w'), ('y','h')):
            minimum = min(p[axis] for p in subset)
            maximum = max(p[axis]+p[length] for p in subset)
            cuts = {p[axis] for p in subset} | {p[axis]+p[length] for p in subset}
            for cut in cuts:
                if cut <= minimum+EPS or cut >= maximum-EPS:
                    continue
                left, right = [], []
                for i in indices:
                    p = parts[i]
                    if p[axis]+p[length] <= cut+EPS:
                        left.append(i)
                    elif p[axis] >= cut-EPS:
                        right.append(i)
                    else:
                        break
                else:
                    if not left or not right:
                        continue
                    for lw, lh in recurse(tuple(left)):
                        for rw, rh in recurse(tuple(right)):
                            result.append((lw+kerf+rw, max(lh,rh)) if axis=='x'
                                          else (max(lw,rw), lh+kerf+rh))
        return pareto(result)
    return recurse(tuple(range(len(parts))))

def verify_physical(plan, spec):
    if plan.get('status') != 'NOMINAL_ZERO_KERF_CANDIDATE_NOT_MANUFACTURING_APPROVED':
        raise ValueError('Uncertified source layout status')
    boards = plan.get('boards')
    if not isinstance(boards, list) or not boards:
        raise ValueError('No candidate stocks')
    pieces = [p for b in boards for p in b.get('pieces', [])]
    codes = [p.get('code') for p in pieces]
    if not all(isinstance(c,str) and c for c in codes) or len(set(codes)) != len(codes):
        raise ValueError('Duplicate/invalid part codes')
    if plan.get('statistics',{}).get('pieces') != len(codes):
        raise ValueError('Cut-piece count mismatch')
    kerf, stocks, sizes = read_spec(spec, set(codes))
    impossible = []
    for board in boards:
        stock = board.get('stock')
        if stock not in stocks:
            raise ValueError('Unknown stock type')
        items = board['pieces']
        for p in items:
            if p['x']+p['w'] > board['width']+EPS or p['y']+p['h'] > board['height']+EPS:
                raise ValueError('Nominal overhang at board '+str(board.get('index')))
        physical_w, physical_h = stocks[stock]
        frontier = physical_slicing_frontier(items, sizes, kerf)
        if not any(w <= physical_w+EPS and h <= physical_h+EPS for w,h in frontier):
            impossible.append(board.get('index'))
    if impossible:
        raise ValueError('NOT PHYSICALLY CUTTABLE with positive kerf; board indexes '+
                         str(impossible[:25])+(' ...' if len(impossible)>25 else ''))
    return {'boards':len(boards), 'pieces':len(codes),'kerf_mm':kerf,
            'cuttable_same_grouping':True,'manufacturing_golden':False}

def risk_screen(plan):
    """Conservative nominal warning only; no inferred manufacturing approval."""
    full = 0
    double_full = []
    for board in plan['boards']:
        ps = board['pieces']
        if sum(p['w']*p['h'] for p in ps) == board['width']*board['height']:
            full += 1
        if board['stock']=='D' and sum(p['w']==610 and p['h']==610 for p in ps)>=2:
            double_full.append(board['index'])
    return {'total':len(plan['boards']), 'fully_used_nominal':full,
            'two_610x610_on_one_610x1220':double_full}

def main():
    arg = argparse.ArgumentParser()
    arg.add_argument('--plan',type=Path,required=True)
    arg.add_argument('--spec',type=Path)
    opts=arg.parse_args()
    plan=json.loads(opts.plan.read_text(encoding='utf-8'))
    if opts.spec is None:
        print(json.dumps({'status':'NOMINAL ONLY; NOT MANUFACTURING APPROVED',
                          'risks':risk_screen(plan)}))
    else:
        spec=json.loads(opts.spec.read_text(encoding='utf-8'))
        print(json.dumps(verify_physical(plan,spec)))

if __name__ == '__main__':
    main()
