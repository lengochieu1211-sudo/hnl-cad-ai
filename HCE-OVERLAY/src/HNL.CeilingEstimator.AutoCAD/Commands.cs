using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HNL.CeilingEstimator.Core;
using HNL.CeilingEstimator.Core.Models;

namespace HNL.CeilingEstimator.AutoCAD
{
    // This is the AutoCAD adapter only. Golden geometry and packing are owned by DemtcEngine.
    // The same immutable-per-command report feeds Preview and Table, without recalculation.
    public sealed class Commands
    {
        [CommandMethod("HCE", CommandFlags.Modal)]
        public void Hce() { RunCeilingEstimator(); }

        [CommandMethod("DTC", CommandFlags.Modal)]
        public void DtcCompatibilityAlias() { RunCeilingEstimator(); }

        [CommandMethod("DEMTC", CommandFlags.Modal)]
        public void DemtcCompatibilityAlias() { RunCeilingEstimator(); }

        [CommandMethod("HCEQA", CommandFlags.Modal)]
        public void HatchDiagnostic()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            var selection = editor.GetSelection(
                new PromptSelectionOptions
                {
                    MessageForAdding = "\nHNL Tool - HCEQA select Hatches to inspect: "
                },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") }));
            if (selection.Status != PromptStatus.OK || selection.Value == null)
            {
                editor.WriteMessage("\nHNL Tool - HCEQA cancelled; read-only.");
                return;
            }
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                editor.WriteMessage("\nHNL Tool - HCEQA drawing units=" +
                    document.Database.Insunits.ToString());
                foreach (SelectedObject selected in selection.Value)
                {
                    if (selected == null || selected.ObjectId.IsNull) continue;
                    try
                    {
                        var hatch = tr.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Hatch;
                        if (hatch == null) continue;
                        editor.WriteMessage("\nHCEQA handle=" + hatch.Handle.ToString() +
                            " layer=" + hatch.Layer +
                            " pattern=" + hatch.PatternName +
                            " type=" + hatch.PatternType.ToString() +
                            " scale=" + Fmt(hatch.PatternScale) +
                            " space=" + Fmt(hatch.PatternSpace) +
                            " double=" + hatch.PatternDouble.ToString() +
                            " origin=" + Fmt(hatch.Origin.X) + "," + Fmt(hatch.Origin.Y) +
                            " angleRad=" + Fmt(hatch.PatternAngle) +
                            " area=" + Fmt(hatch.Area) +
                            " normalZ=" + Fmt(hatch.Normal.Z) +
                            " loops=" + hatch.NumberOfLoops +
                            " definitions=" + hatch.NumberOfPatternDefinitions);
                        for (var i = 0; i < hatch.NumberOfLoops; i++)
                        {
                            var loop = hatch.GetLoopAt(i);
                            var totalEdges = loop.IsPolyline ? loop.Polyline.Count : loop.Curves.Count;
                            editor.WriteMessage("\n  loop=" + i +
                                " kind=" + (loop.IsPolyline ? "polyline" : "curves") +
                                " type=" + loop.LoopType.ToString() +
                                " edges=" + totalEdges);
                        }
                        HatchInput extracted;
                        string failure;
                        if (TryExtractHatch(hatch, out extracted, out failure))
                            editor.WriteMessage("\n  bridge=accepted (assumes 610x610 for unverified patterns)");
                        else
                            editor.WriteMessage("\n  bridge=rejected: " + failure);
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\nHCEQA id=" + selected.ObjectId.ToString() +
                            " read failure: " + ex.Message);
                    }
                }
            }
            editor.WriteMessage("\nHNL Tool - HCEQA finished; no drawing changes.");
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static void RunCeilingEstimator()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            editor.WriteMessage("\nHNL Tool - Ceiling Estimator Pro v0.3.0 (AutoCAD bridge candidate).");

            var selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - Select one or more Hatch entities: "
            };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "HATCH") });
            var picked = editor.GetSelection(selectionOptions, filter);
            if (picked.Status != PromptStatus.OK || picked.Value == null)
            {
                editor.WriteMessage("\nHNL Tool - Cancelled. No drawing changes.");
                return;
            }

            try
            {
                var inputs = new List<HatchInput>();
                var rejected = 0;
                using (var transaction = document.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selected in picked.Value)
                    {
                        if (selected == null || selected.ObjectId.IsNull) continue;
                        var hatch = transaction.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Hatch;
                        if (hatch == null) continue;
                        HatchInput input;
                        string reason;
                        if (TryExtractHatch(hatch, out input, out reason))
                        {
                            inputs.Add(input);
                        }
                        else
                        {
                            rejected++;
                            editor.WriteMessage("\nHNL Tool - Rejected Hatch handle=" +
                                hatch.Handle.ToString() + " id=" + selected.ObjectId.ToString() +
                                ": " + reason);
                        }
                    }
                }

                if (inputs.Count == 0)
                {
                    editor.WriteMessage("\nHNL Tool - No supported Hatch boundary found. No drawing changes.");
                    return;
                }

                // Reject implicit assumptions. Non-user-defined PAT spacing cannot be
                // proven from PatternScale alone, so require explicit user acceptance.
                editor.WriteMessage("\nHNL Tool - Unit mode=" +
                    document.Database.Insunits.ToString() +
                    ". Golden input coordinates are millimeters.");
                editor.WriteMessage("\nHNL Tool - Grid policy fixed at 610x610 mm.");
                foreach (var input in inputs)
                {
                    if (!input.UserGridVerified)
                        editor.WriteMessage("\nHNL Tool - Hatch " + input.Handle +
                            " pattern=" + input.PatternName +
                            " scale=" + Fmt(input.PatternScale) +
                            " has no certified 610mm spacing; verify before proceeding.");
                }
                var acknowledge = new PromptKeywordOptions(
                    "\nHNL Tool - Acknowledge 610mm grid and drawing units [Use610/Cancel] <Cancel>: ")
                {
                    AllowNone = true
                };
                acknowledge.Keywords.Add("Use610");
                acknowledge.Keywords.Add("Cancel");
                var consent = editor.GetKeywords(acknowledge);
                if (consent.Status != PromptStatus.OK ||
                    !string.Equals(consent.StringResult, "Use610", StringComparison.OrdinalIgnoreCase))
                {
                    editor.WriteMessage("\nHNL Tool - Unverified grid rejected by operator. No drawing changes.");
                    return;
                }

                var report = CalculateOnce(inputs, editor, ref rejected);
                if (report.Groups.Count == 0)
                {
                    editor.WriteMessage("\nHNL Tool - No successful calculation. No drawing changes.");
                    return;
                }

                // Preview currently means read-only numeric output. Visual tile geometry
                // is deliberately NOT fabricated: Core currently returns aggregate inner
                // full counts, not per-cell positions. That needs a separate Golden gate.
                WritePreview(editor, report, rejected);

                var choiceOptions = new PromptKeywordOptions(
                    "\nHNL Tool - Result [Table/Exit] <Exit>: ")
                {
                    AllowNone = true
                };
                choiceOptions.Keywords.Add("Table");
                choiceOptions.Keywords.Add("Exit");
                var choice = editor.GetKeywords(choiceOptions);
                if (choice.Status != PromptStatus.OK ||
                    !string.Equals(choice.StringResult, "Table", StringComparison.OrdinalIgnoreCase))
                {
                    editor.WriteMessage("\nHNL Tool - Preview only. No drawing changes.");
                    return;
                }

                var location = editor.GetPoint("\nHNL Tool - Specify result table insertion point: ");
                if (location.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nHNL Tool - Table cancelled. No drawing changes.");
                    return;
                }
                InsertTable(document.Database, report, location.Value);
                editor.WriteMessage("\nHNL Tool - Table created from the same calculation report.");
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHNL Tool - Runtime adapter error: " + ex.Message);
                // Do not partially create geometry on calculation errors.
            }
        }

        private static bool TryExtractHatch(Hatch hatch, out HatchInput input, out string reason)
        {
            input = new HatchInput();
            reason = string.Empty;
            // HatchLoop points are in OCS; the XY WCS Core contract is valid only
            // for +Z hatches. Other normals need an explicit OCS->WCS transform gate.
            if (Math.Abs(hatch.Normal.X) > 1e-8 ||
                Math.Abs(hatch.Normal.Y) > 1e-8 ||
                hatch.Normal.Z < 1.0 - 1e-8)
            {
                reason = "unsupported OCS normal; +Z WCS Hatch required";
                return false;
            }
            if (hatch.IsSolidFill)
            {
                reason = "solid fill has no reliable 610 mm pattern grid";
                return false;
            }
            if (double.IsNaN(hatch.PatternScale) ||
                double.IsInfinity(hatch.PatternScale) ||
                !(hatch.PatternScale > 0))
            {
                reason = "invalid Hatch.PatternScale";
                return false;
            }
            if (hatch.PatternType == HatchPatternType.UserDefined &&
                (!hatch.PatternDouble ||
                 double.IsNaN(hatch.PatternSpace) ||
                 Math.Abs(hatch.PatternSpace - 610.0) > 0.01))
            {
                reason = "user-defined grid is not 610x610 double pattern (space=" +
                    Fmt(hatch.PatternSpace) + ", double=" + hatch.PatternDouble + ")";
                return false;
            }
            if (hatch.NumberOfLoops != 1)
            {
                reason = "loopCount=" + hatch.NumberOfLoops +
                    "; multi-loop/hole geometry pending parity validation";
                return false;
            }

            var boundary = hatch.GetLoopAt(0);
            var segments = new List<Segment2>();
            if (boundary.IsPolyline)
            {
                var polyline = boundary.Polyline;
                if (polyline == null || polyline.Count < 3)
                {
                    reason = "loop=0 polyline has fewer than 3 vertices";
                    return false;
                }
                // Some Hatch polylines repeat the first vertex as the last.
                // Ignore that duplicate rather than creating a zero-length closing edge.
                var vertexCount = polyline.Count;
                var first = polyline[0].Vertex;
                var last = polyline[vertexCount - 1].Vertex;
                var dxx = first.X - last.X;
                var dyy = first.Y - last.Y;
                if (vertexCount > 3 && dxx * dxx + dyy * dyy <= 1e-12)
                    vertexCount--;
                for (var i = 0; i < vertexCount; i++)
                {
                    var vertex = polyline[i];
                    var next = polyline[(i + 1) % vertexCount];
                    if (Math.Abs(vertex.Bulge) > 1e-10)
                    {
                        reason = "loop=0 edge=" + i + " bulge/arc requires curve parity validation";
                        return false;
                    }
                    if (!AddLine(segments, vertex.Vertex, next.Vertex, out reason))
                    {
                        reason = "loop=0 edge=" + i + ": " + reason;
                        return false;
                    }
                }
            }
            else
            {
                var edge = 0;
                foreach (Curve2d curve in boundary.Curves)
                {
                    var line = curve as LineSegment2d;
                    if (line == null)
                    {
                        reason = "loop=0 edge=" + edge + " type=" +
                            curve.GetType().Name + " requires curve parity validation";
                        return false;
                    }
                    if (!AddLine(segments, line.StartPoint, line.EndPoint, out reason))
                    {
                        reason = "loop=0 edge=" + edge + ": " + reason;
                        return false;
                    }
                    edge++;
                }
            }

            if (segments.Count < 3)
            {
                reason = "loop=0 has too few non-degenerate edges";
                return false;
            }
            var maxGap = 0.0;
            for (var i = 0; i < segments.Count; i++)
            {
                var current = segments[i];
                var next = segments[(i + 1) % segments.Count];
                var dx = current.X2 - next.X1;
                var dy = current.Y2 - next.Y1;
                maxGap = Math.Max(maxGap, Math.Sqrt(dx * dx + dy * dy));
            }
            if (maxGap > 0.01)
            {
                reason = "loop=0 sequential join gap=" +
                    maxGap.ToString("F5", CultureInfo.InvariantCulture) + "mm";
                return false;
            }

            var area = hatch.Area;
            if (!(area > 0) || double.IsNaN(area) || double.IsInfinity(area))
            {
                reason = "invalid Hatch.Area";
                return false;
            }
            if (double.IsNaN(hatch.Origin.X) || double.IsNaN(hatch.Origin.Y) ||
                double.IsNaN(hatch.PatternAngle) || double.IsInfinity(hatch.PatternAngle))
            {
                reason = "non-finite pattern origin/angle";
                return false;
            }

            input.Handle = hatch.Handle.ToString();
            input.GroupKey = hatch.Layer + " | ACI=" +
                hatch.Color.ColorIndex.ToString(CultureInfo.InvariantCulture) +
                " | " + hatch.PatternName;
            input.Frame = new GridFrame(
                new Point2(hatch.Origin.X, hatch.Origin.Y), hatch.PatternAngle);
            input.Segments = segments;
            input.Area = area;
            input.PatternScale = hatch.PatternScale;
            input.PatternName = hatch.PatternName;
            input.UserGridVerified = hatch.PatternType == HatchPatternType.UserDefined &&
                hatch.PatternDouble && Math.Abs(hatch.PatternSpace - 610.0) <= 0.01;
            return true;
        }

        private static bool AddLine(
            List<Segment2> segments, Point2d start, Point2d end, out string reason)
        {
            reason = string.Empty;
            if (double.IsNaN(start.X) || double.IsNaN(start.Y) ||
                double.IsNaN(end.X) || double.IsNaN(end.Y))
            {
                reason = "non-finite edge coordinate";
                return false;
            }
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            if (dx * dx + dy * dy < 1e-12)
            {
                reason = "degenerate zero-length edge";
                return false;
            }
            segments.Add(new Segment2(start.X, start.Y, end.X, end.Y));
            return true;
        }

        private static CalculationReport CalculateOnce(
            List<HatchInput> inputs, Editor editor, ref int rejected)
        {
            var engine = new DemtcEngine();
            // Default options are the unchanged v0.3.0 Golden policy.
            var options = new DemtcOptions();
            var report = new CalculationReport();
            var groups = new Dictionary<string, MaterialGroup>(StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                HatchCalculationResult calc;
                try
                {
                    calc = engine.CalculateLineHatch(
                        input.Segments, input.Frame, input.Area, options);
                }
                catch (System.Exception ex)
                {
                    rejected++;
                    editor.WriteMessage("\nHNL Tool - Rejected Hatch " + input.Handle +
                        " engine exception: " + ex.Message);
                    continue;
                }
                if (!calc.Success || calc.PureResult == null)
                {
                    rejected++;
                    editor.WriteMessage("\nHNL Tool - Rejected Hatch " + input.Handle +
                        " calculation: " + calc.FailureReason);
                    continue;
                }

                MaterialGroup group;
                if (!groups.TryGetValue(input.GroupKey, out group))
                {
                    group = new MaterialGroup { Name = input.GroupKey };
                    groups.Add(input.GroupKey, group);
                    report.Groups.Add(group);
                }

                group.HatchCount++;
                group.Full += calc.PureResult.TotalFullCount;
                group.BoundaryCells += calc.PureResult.BoundaryCandidateCount;
                group.Slivers += calc.PureResult.SliverCount;
                group.Cuts.AddRange(calc.PureResult.CutPieces);
                if (calc.Path == "general-line-loop") group.GeneralFallbackCount++;
            }
            foreach (var group in report.Groups)
            {
                // Pack across all Hatches of one material group, not independently per Hatch.
                group.Packing = engine.PackCutPieces(group.Cuts, options);
            }
            return report;
        }

        private static void WritePreview(Editor editor, CalculationReport report, int rejected)
        {
            editor.WriteMessage("\nHNL Tool - READ-ONLY PREVIEW (counts only, no geometry created).");
            foreach (var group in report.Groups)
            {
                editor.WriteMessage("\n  " + group.Name +
                    " | Hatch=" + group.HatchCount +
                    " | Full=" + group.Full +
                    " | Boundary=" + group.BoundaryCells +
                    " | Cuts=" + group.Cuts.Count +
                    " | Small=" + BinCount(group, "S") +
                    " | Long=" + BinCount(group, "D") +
                    " | Oversize=" + group.Packing.Oversize.Count +
                    " | General=" + group.GeneralFallbackCount);
            }
            editor.WriteMessage("\nHNL Tool - Rejected Hatch count=" + rejected +
                ". Preview and Table share exactly one report.");
        }

        private static int BinCount(MaterialGroup group, string code)
        {
            var count = 0;
            foreach (var bin in group.Packing.Bins)
                if (string.Equals(bin.Code, code, StringComparison.Ordinal)) count++;
            return count;
        }

        private static void InsertTable(Database database, CalculationReport report, Point3d point)
        {
            // No transaction is committed until all table cells are ready. AutoCAD UNDO
            // therefore removes the single table creation in the usual manner.
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)transaction.GetObject(
                    database.CurrentSpaceId, OpenMode.ForWrite);
                using (var table = new Table())
                {
                    table.SetSize(report.Groups.Count + 3, 8);
                    table.SetRowHeight(55);
                    table.SetColumnWidth(230);
                    table.Position = point;
                    table.Cells[0, 0].TextString = "HNL Tool - Ceiling Estimator Pro (610mm grid assumed)";
                    string[] headers =
                    {
                        "Material / Hatch group", "Hatch", "Full", "Boundary",
                        "Cuts", "Small 610", "Long 1220", "Oversize"
                    };
                    for (var i = 0; i < headers.Length; i++)
                        table.Cells[1, i].TextString = headers[i];
                    var totalHatches = 0;
                    var totalFull = 0;
                    var totalBoundary = 0;
                    var totalCuts = 0;
                    var totalSmall = 0;
                    var totalLong = 0;
                    var totalOversize = 0;
                    for (var i = 0; i < report.Groups.Count; i++)
                    {
                        var group = report.Groups[i];
                        var r = i + 2;
                        var small = BinCount(group, "S");
                        var large = BinCount(group, "D");
                        table.Cells[r, 0].TextString = group.Name;
                        table.Cells[r, 1].TextString = group.HatchCount.ToString();
                        table.Cells[r, 2].TextString = group.Full.ToString();
                        table.Cells[r, 3].TextString = group.BoundaryCells.ToString();
                        table.Cells[r, 4].TextString = group.Cuts.Count.ToString();
                        table.Cells[r, 5].TextString = small.ToString();
                        table.Cells[r, 6].TextString = large.ToString();
                        table.Cells[r, 7].TextString = group.Packing.Oversize.Count.ToString();
                        totalHatches += group.HatchCount;
                        totalFull += group.Full;
                        totalBoundary += group.BoundaryCells;
                        totalCuts += group.Cuts.Count;
                        totalSmall += small;
                        totalLong += large;
                        totalOversize += group.Packing.Oversize.Count;
                    }
                    var footer = report.Groups.Count + 2;
                    string[] totals =
                    {
                        "TOTAL", totalHatches.ToString(), totalFull.ToString(),
                        totalBoundary.ToString(), totalCuts.ToString(),
                        totalSmall.ToString(), totalLong.ToString(),
                        totalOversize.ToString()
                    };
                    for (var i = 0; i < totals.Length; i++)
                        table.Cells[footer, i].TextString = totals[i];
                    table.GenerateLayout();
                    currentSpace.AppendEntity(table);
                    transaction.AddNewlyCreatedDBObject(table, true);
                    transaction.Commit();
                }
            }
        }

        private sealed class HatchInput
        {
            public string Handle = string.Empty;
            public string GroupKey = string.Empty;
            public GridFrame Frame = new GridFrame(new Point2(0, 0), 0);
            public List<Segment2> Segments = new List<Segment2>();
            public double Area;
            public double PatternScale;
            public string PatternName = string.Empty;
            public bool UserGridVerified;
        }

        private sealed class CalculationReport
        {
            public List<MaterialGroup> Groups = new List<MaterialGroup>();
        }

        private sealed class MaterialGroup
        {
            public string Name = string.Empty;
            public int HatchCount;
            public int Full;
            public int BoundaryCells;
            public int Slivers;
            public int GeneralFallbackCount;
            public List<Piece> Cuts = new List<Piece>();
            public PackResult Packing = new PackResult();
        }
    }
}
