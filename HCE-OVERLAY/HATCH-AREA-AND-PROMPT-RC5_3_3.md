RC5.3.3 Hatch Area and redundant prompt patch
Evidence: user selected seven USER Hatches; one handle=2AD46 returned AutoCAD NotApplicable at Hatch.Area, six accepted, no table due partial selection; UI already fixed family 610 and mm.
Root cause: API Hatch.Area may throw eNotApplicable even for valid flat single-loop boundaries; adapter previously returned false without a mathematical fallback. Separately, adapter always requested Use610 despite UI + grid verification.
Small safe changes to Commands.cs only:
- one-loop linear closed edges -> calculate stable shoelace area. Native Hatch.Area remains priority; on NotApplicable alone use boundary area with <=512 edge and nonadjacent-intersection guard. Reject holes/arcs/open paths/complex boundaries, no partial Table.
- HCEQA area getter reports n/a (ErrorStatus), allowing other diagnostics to continue.
- do not request a second family acknowledgement when DWG units=Millimeters and USER pattern spacing matches selected family; if pattern or units unverified, require explicit Proceed. If any Hatch is rejected, show partial read-only Preview without interrupting with a confirmation that cannot create Table.
- Preserve calculated report/Preview/Table parity, engines and all Golden fixtures.
CI / AutoCAD native runtime still required, not Golden yet.
