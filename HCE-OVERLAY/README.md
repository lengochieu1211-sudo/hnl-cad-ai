# HCE AutoCAD Runtime Bridge Overlay - RC2

Base: HCE RC1 2cb4aeed191be8f6309c3610defbecf586e6415f
Core source and Golden fixtures in HCE-PAYLOAD are FROZEN, unchanged.

RC2 safe additions:
- HCEQA diagnostic command inspects Hatch pattern/type, spacing, double flag,
  origin, angle, area, loops, and adapter eligibility, read-only.
- User-defined Hatch patterns require true double-grid and 610mm spacing.
- Predefined and custom patterns have unverified effective spacing; HCE requires
  explicit Use610 acknowledgement (default Cancel) before calculating.
- Explicit unit assumption displayed; no automatic drawing scale conversion.
- Duplicate terminal polyline vertex excluded to avoid false degenerate closing edge.
- Result table title carries the "610mm grid assumed" label.
- HCE/DTC/DEMTC and Golden 610x610/packing Core unchanged.

Limitations:
- No CAD runtime evidence yet; HCEQA is meant to collect it.
- Curves, multiple loop holes, and non-+Z Hatch remain safely rejected.
- Visual geometry Preview/Create parity is pending; current Preview is numbers.
- No unsupported geometry is silently approximated into a different area.
