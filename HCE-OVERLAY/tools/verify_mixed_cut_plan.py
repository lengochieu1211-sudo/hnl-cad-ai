#!/usr/bin/env python3
"""HNL Tool read-only validator for nominal mixed S/D cutting plans.

No Autodesk API or Core dependency. Fails closed on missing/duplicated parts,
changed totals, rectangle overlap, stock overhang or non-guillotine layouts.
Positive kerf and physical finished sizes are NOT certified.
"""
import argparse
from collections import Counter
from functools import lru_cache
import json
from pathlib import Path


def guillotine(parts):
    @lru_cache(maxsize=None)
    def cut(indices):
        if len(indices) <= 1:
            return True
        items = [parts[k] for k in indices]
        for coord, length in [('x', 'w'), ('y', 'h')]:
            low = min(item[coord] for item in items)
            high = max(item[coord] + item[length] for item in items)
            positions = {item[coord] for item in items} | {
                item[coord] + item[length] for item in items}
            for boundary in positions:
                if not low < boundary < high:
                    continue
                left, right = [], []
                for k in indices:
                    part = parts[k]
                    if part[coord] + part[length] <= boundary:
                        left.append(k)
                    elif part[coord] >= boundary:
                        right.append(k)
                    else:
                        break
                else:
                    if left and right and cut(tuple(left)) and cut(tuple(right)):
                        return True
        return False
    return cut(tuple(range(len(parts))))


def verify(layout):
    if layout.get('status') != 'NOMINAL_ZERO_KERF_CANDIDATE_NOT_MANUFACTURING_APPROVED':
        raise ValueError('Missing nominal zero-kerf safety marker')
    seen = set()
    area = 0
    stocks = Counter()
    for board in layout['boards']:
        stock = board['stock']
        if stock not in ('S', 'D'):
            raise ValueError('Invalid stock type')
        if board['width'] != 610 or board['height'] != (610 if stock == 'S' else 1220):
            raise ValueError('Unexpected nominal stock dimensions')
        pieces = board['pieces']
        if not pieces:
            raise ValueError('Empty stock')
        if not guillotine(pieces):
            raise ValueError('Non-guillotine layout at stock ' + str(board['index']))
        stocks[stock] += 1
        for i, p in enumerate(pieces):
            code = p['code']
            if code in seen:
                raise ValueError('Duplicate part ' + code)
            seen.add(code)
            if (p['x'] < 0 or p['y'] < 0 or p['w'] <= 0 or p['h'] <= 0 or
                    p['x'] + p['w'] > board['width'] or
                    p['y'] + p['h'] > board['height']):
                raise ValueError('Out-of-stock part ' + code)
            for other in pieces[:i]:
                if (p['x'] < other['x'] + other['w'] and
                        other['x'] < p['x'] + p['w'] and
                        p['y'] < other['y'] + other['h'] and
                        other['y'] < p['y'] + p['h']):
                    raise ValueError('Overlap at part ' + code)
            area += p['w'] * p['h']
    stats = layout['statistics']
    if (len(seen) != stats['pieces'] or area != stats['cut_area_module_mm2'] or
            stocks['S'] != stats['short_cut_boards'] or
            stocks['D'] != stats['long_cut_boards']):
        raise ValueError('Totals/stock inventory mismatch')
    equivalents = stocks['S'] + 2 * stocks['D']
    if (equivalents != stats['candidate_small_equivalents'] or
            stats['baseline_small_equivalents'] - equivalents !=
            stats['saved_small_equivalents']):
        raise ValueError('Material-equivalent arithmetic mismatch')
    if stats.get('physical_kerf_certified') is not False:
        raise ValueError('Do not claim kerf-certified without physical test')
    return stats


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('plan', type=Path)
    args = parser.parse_args()
    result = verify(json.loads(args.plan.read_text(encoding='utf-8')))
    print('HNL Tool NOMINAL MIXED-CUT VERIFY PASS:', result['pieces'], 'parts;',
          result['long_cut_boards'], 'long +', result['short_cut_boards'], 'short;',
          'saved small equivalents', result['saved_small_equivalents'])


if __name__ == '__main__':
    main()
