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
        // HCE opens the same dockable palette as HCEUI, like HNL Ceiling Framing Pro.
        // The unchanged calculation path remains callable as HCECALC.
        [CommandMethod("HCE", CommandFlags.Modal)]
        public void Hce() { new HcePaletteCommands().Open(); }

        [CommandMethod("HCECALC", CommandFlags.Modal)]
        public void HceCalculate() { RunCeilingEstimator(); }

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
                        if (hatch.IsGradient)
                        {
                            editor.WriteMessage("\nHCEQA handle=" + hatch.Handle +
                                " layer=" + hatch.Layer +
                                " rejected: gradient fill is not a ceiling tile grid.");
                            continue;
                        }
                        var userPattern = hatch.PatternType == HatchPatternType.UserDefined;
                        var scaleText = userPattern ? "n/a (user-defined)" : Fmt(hatch.PatternScale);
                        var spacingText = userPattern ? Fmt(hatch.PatternSpace) : "n/a";
                        var doubleText = userPattern ? hatch.PatternDouble.ToString() : "n/a";
                        editor.WriteMessage("\nHCEQA handle=" + hatch.Handle +
                            " layer=" + hatch.Layer +
                            " pattern=" + hatch.PatternName +
                            " type=" + hatch.PatternType +
                            " scale=" + scaleText +
                            " space=" + spacingText +
                            " double=" + doubleText +
                            " origin=" + Fmt(hatch.Origin.X) + "," + Fmt(hatch.Origin.Y) +
                            " patternBase43_44=" + PatternBaseText(hatch) +
                            " angleRad=" + Fmt(hatch.PatternAngle) +
                            " area=" + SafeAreaText(hatch) +
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
                            editor.WriteMessage("\n  bridge=accepted areaSource=" +
                                (extracted.AreaFromBoundary ? "boundary-fallback" : "native") +
                                " phasePolicy=Hatch.Origin" +
                                " originVs43_44=" + PatternBaseDeltaText(hatch));
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

        // Read-only complete current-space audit. Intended for AutoCAD 2023
        // Runtime proof; printed counts must still be compared with real DXF Golden.
        [CommandMethod("HCEGOLDEN", CommandFlags.Modal)]
        public void GoldenAudit()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            var database = document.Database;
            var inputs = new List<HatchInput>();
            var total = 0;
            var rejected = 0;
            editor.WriteMessage("\nHNL Tool - HCEGOLDEN read-only current-space Hatch audit.");
            editor.WriteMessage("\nHNL Tool - Drawing units=" + database.Insunits +
                "; assumed tile pitch=610mm; verify actual Hatch definition.");
            try
            {
                using (var transaction = database.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)transaction.GetObject(
                        database.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        if (id.IsNull) continue;
                        var hatch = transaction.GetObject(id, OpenMode.ForRead, false) as Hatch;
                        if (hatch == null) continue;
                        total++;
                        try
                        {
                            HatchInput input;
                            string reason;
                            if (TryExtractHatch(hatch, out input, out reason))
                            {
                                inputs.Add(input);
                                editor.WriteMessage("\nHCEGOLDEN INPUT handle=" + input.Handle +
                                    " pattern=" + input.PatternName +
                                    " scale=" + Fmt(input.PatternScale) +
                                    " area=" + Fmt(input.Area) +
                                    " segments=" + input.Segments.Count +
                                    " origin=" + Fmt(input.Frame.Origin.X) + "," +
                                    Fmt(input.Frame.Origin.Y) +
                                    " patternBase43_44=" + PatternBaseText(hatch) +
                                    " originVs43_44=" + PatternBaseDeltaText(hatch) +
                                    " areaSource=" + (input.AreaFromBoundary ? "boundary-fallback" : "native") +
                                    " angleRad=" + Fmt(input.Frame.AngleRadians) +
                                    " gridVerified=" + input.UserGridVerified +
                                    " nearClosedRecovered=" + input.RecoveredNearClosed +
                                    " joinGapMm=" + Fmt(input.NearClosedGapMm));
                            }
                            else
                            {
                                rejected++;
                                editor.WriteMessage("\nHCEGOLDEN REJECT handle=" +
                                    hatch.Handle + " reason=" + reason);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            rejected++;
                            editor.WriteMessage("\nHCEGOLDEN REJECT handle=" +
                                hatch.Handle + " exception=" + ex.Message);
                        }
                    }
                }

                editor.WriteMessage("\nHCEGOLDEN SCAN total=" + total +
                    " accepted=" + inputs.Count + " rejected=" + rejected);
                if (inputs.Count == 0)
                {
                    editor.WriteMessage("\nHCEGOLDEN STATUS=NO_SUPPORTED_HATCH; no DWG changes.");
                    return;
                }
                var report = CalculateOnce(inputs, editor, ref rejected, true);
                WritePreview(editor, report, rejected);
                editor.WriteMessage("\nHCEGOLDEN STATUS=" +
                    (rejected == 0 && report.Groups.Count > 0 ? "COMPLETE" : "INCOMPLETE") +
                    "; calculated using assumed 610mm policy.");
                editor.WriteMessage("\nHCEGOLDEN Runtime Golden certification is PENDING real AutoCAD evidence.");
                editor.WriteMessage("\nHNL Tool - Audit finished. No DWG changes.");
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHCEGOLDEN ADAPTER ERROR=" + ex.Message +
                    "; no DWG changes.");
            }
        }

        private static string Fmt(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static string SafeAreaText(Hatch hatch)
        {
            try { return Fmt(hatch.Area); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                return "n/a (" + ex.ErrorStatus + ")";
            }
        }

        // RC5.4.1 diagnostic only. Legacy DEMTC RC18.5B.4C reads the first
        // pattern-definition base point (DXF 43/44) before falling back to Origin.
        // Do not feed this into Core until real-DWG phase parity is proven.
        private static bool TryGetFirstPatternBase(Hatch hatch, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            try
            {
                if (hatch.NumberOfPatternDefinitions < 1) return false;
                object definition = hatch.GetPatternDefinitionAt(0);
                var type = definition.GetType();
                var px = type.GetProperty("BaseX");
                var py = type.GetProperty("BaseY");
                object? vx = px == null ? null : px.GetValue(definition, null);
                object? vy = py == null ? null : py.GetValue(definition, null);
                if (vx == null)
                {
                    var fx = type.GetField("BaseX");
                    vx = fx == null ? null : fx.GetValue(definition);
                }
                if (vy == null)
                {
                    var fy = type.GetField("BaseY");
                    vy = fy == null ? null : fy.GetValue(definition);
                }
                if (vx == null || vy == null) return false;
                x = Convert.ToDouble(vx, CultureInfo.InvariantCulture);
                y = Convert.ToDouble(vy, CultureInfo.InvariantCulture);
                return !(double.IsNaN(x) || double.IsInfinity(x) ||
                         double.IsNaN(y) || double.IsInfinity(y));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static string PatternBaseText(Hatch hatch)
        {
            double x, y;
            return TryGetFirstPatternBase(hatch, out x, out y)
                ? Fmt(x) + "," + Fmt(y)
                : "n/a";
        }

        private static string PatternBaseDeltaText(Hatch hatch)
        {
            double x, y;
            if (!TryGetFirstPatternBase(hatch, out x, out y)) return "n/a";
            var dx = hatch.Origin.X - x;
            var dy = hatch.Origin.Y - y;
            return Fmt(Math.Sqrt(dx * dx + dy * dy));
        }

        private static void RunCeilingEstimator()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null) return;
            var editor = document.Editor;
            var profile = HceLegacyProfiles.For(document.Database).Clone();
            var options = profile.ToOptions();
            editor.WriteMessage("\nHNL Tool - Ceiling Estimator Pro RC5.4.1 (Area/phase diagnostic; Runtime Candidate).");
            editor.WriteMessage("\nHNL Tool - " + profile.Summary());

            Point2? chosenGridOrigin = null;
            double chosenGridAngle = 0.0;
            if (profile.GridMode == "2")
            {
                var origin = editor.GetPoint("\nHNL Tool - Pick grid origin: ");
                if (origin.Status != PromptStatus.OK) return;
                var next = new PromptPointOptions("\nHNL Tool - Pick grid X direction: ")
                    { UseBasePoint = true, BasePoint = origin.Value };
                var direction = editor.GetPoint(next);
                if (direction.Status != PromptStatus.OK) return;
                var ddx = direction.Value.X - origin.Value.X;
                var ddy = direction.Value.Y - origin.Value.Y;
                if (ddx * ddx + ddy * ddy <= 1e-12)
                {
                    editor.WriteMessage("\nHNL Tool - Grid direction too short; no changes.");
                    return;
                }
                chosenGridOrigin = new Point2(origin.Value.X, origin.Value.Y);
                chosenGridAngle = Math.Atan2(ddy, ddx);
            }

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
                        try
                        {
                            var hatch = transaction.GetObject(selected.ObjectId, OpenMode.ForRead, false) as Hatch;
                            if (hatch == null) continue;
                            HatchInput input;
                            string reason;
                            if (TryExtractHatch(hatch, out input, out reason, profile.Family))
                            {
                                if (profile.GridMode == "1")
                                    input.Frame = new GridFrame(new Point2(0, 0), 0);
                                else if (profile.GridMode == "2" && chosenGridOrigin.HasValue)
                                    input.Frame = new GridFrame(chosenGridOrigin.Value, chosenGridAngle);
                                inputs.Add(input);
                                if (input.RecoveredNearClosed)
                                    editor.WriteMessage("\nHNL Tool - Hatch handle=" + input.Handle +
                                        " NotClosed gap=" + Fmt(input.NearClosedGapMm) +
                                        " mm; near-closed loop included after topology checks. " +
                                        "Original DWG Hatch unchanged.");
                                if (input.AreaFromBoundary)
                                    editor.WriteMessage("\nHNL Tool - Hatch handle=" + input.Handle +
                                        " native Area=NotApplicable; certified simple-loop boundary area=" +
                                        Fmt(input.Area) + " mm2 (strict topology gate).");
                            }
                            else
                            {
                                rejected++;
                                editor.WriteMessage("\nHNL Tool - Rejected Hatch handle=" +
                                    hatch.Handle + " id=" + selected.ObjectId + ": " + reason);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            rejected++;
                            editor.WriteMessage("\nHNL Tool - Rejected Hatch id=" +
                                selected.ObjectId + " at opening/reading: " +
                                ex.GetType().Name + " " + ex.Message);
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
                editor.WriteMessage("\nHNL Tool - Module policy=" + options.GridWidth + "x" +
                    options.GridHeight + " mm; pitch family=" + profile.Family + ".");
                foreach (var input in inputs)
                {
                    if (!input.UserGridVerified)
                        editor.WriteMessage("\nHNL Tool - Hatch " + input.Handle +
                            " pattern=" + input.PatternName +
                            " scale=" + Fmt(input.PatternScale) +
                            " has no certified " + profile.Family +
                            "mm spacing; verify before proceeding.");
                }
                var acceptKey = profile.Family == 610 ? "Use610" : "Use600";
                // Do not ask Use600/Use610 again after the user has configured
                // the module in the Palette and USER Hatch pitch was verified.
                // If even one Hatch was rejected, output diagnostics only; never
                // request meaningless confirmation before blocking the Table.
                var needsConfirmation = rejected == 0 &&
                    (document.Database.Insunits != UnitsValue.Millimeters ||
                     inputs.Exists(input => !input.UserGridVerified));
                if (needsConfirmation)
                {
                    var acknowledge = new PromptKeywordOptions(
                        "\nHNL Tool - Grid pitch or DWG unit is not verified. [Proceed/Cancel] <Cancel>: ")
                    {
                        AllowNone = true
                    };
                    acknowledge.Keywords.Add("Proceed");
                    acknowledge.Keywords.Add("Cancel");
                    var consent = editor.GetKeywords(acknowledge);
                    if (consent.Status != PromptStatus.OK ||
                        !string.Equals(consent.StringResult, "Proceed", StringComparison.OrdinalIgnoreCase))
                    {
                        editor.WriteMessage("\nHNL Tool - Unverified grid cancelled. No drawing changes.");
                        return;
                    }
                }

                var report = CalculateOnce(inputs, editor, ref rejected, false, options);
                if (report.Groups.Count == 0)
                {
                    editor.WriteMessage("\nHNL Tool - No successful calculation. No drawing changes.");
                    return;
                }

                // Preview remains available for diagnosing accepted Hatches.
                // Never create a seemingly complete quantity table from a partial selection.
                if (rejected > 0)
                {
                    WritePreview(editor, report, rejected);
                    editor.WriteMessage("\nHNL Tool - INCOMPLETE SELECTION. " +
                        "Table is blocked; resolve rejected Hatch handles and rerun.");
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

                // Like the original LISP, detailed cut lists can be slow on large Hatches.
                // Confirm before allocating thousands of AutoCAD Table rows.
                if (profile.CutListEnabled && profile.CutListMode == "D")
                {
                    var detailedCount = 0;
                    foreach (var material in report.Groups) detailedCount += material.Cuts.Count;
                    if (detailedCount > 2000)
                    {
                        var verifyDetail = new PromptKeywordOptions(
                            "\nHNL Tool - Detailed cut table has " + detailedCount +
                            " rows and may be slow. [Proceed/Cancel] <Cancel>: ")
                        {
                            AllowNone = true
                        };
                        verifyDetail.Keywords.Add("Proceed");
                        verifyDetail.Keywords.Add("Cancel");
                        var consent = editor.GetKeywords(verifyDetail);
                        if (consent.Status != PromptStatus.OK ||
                            !string.Equals(consent.StringResult, "Proceed", StringComparison.OrdinalIgnoreCase))
                        {
                            editor.WriteMessage("\nHNL Tool - Detailed table cancelled; DWG unchanged.");
                            return;
                        }
                    }
                }

                var location = editor.GetPoint("\nHNL Tool - Specify result table insertion point: ");
                if (location.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nHNL Tool - Table cancelled. No drawing changes.");
                    return;
                }
                var labelCount = CountLabelCandidates(report);
                if (labelCount > 2000)
                {
                    var verifyLabels = new PromptKeywordOptions(
                        "\nHNL Tool - Create " + labelCount +
                        " N/G/L labels on the DWG? [Yes/No] <Yes>: ") { AllowNone = true };
                    verifyLabels.Keywords.Add("Yes");
                    verifyLabels.Keywords.Add("No");
                    var decision = editor.GetKeywords(verifyLabels);
                    if (decision.Status == PromptStatus.Cancel) return;
                    if (decision.Status == PromptStatus.OK &&
                        string.Equals(decision.StringResult, "No", StringComparison.OrdinalIgnoreCase))
                    {
                        InsertTable(document.Database, report, location.Value, profile, false);
                        editor.WriteMessage("\nHNL Tool - Table created; large text label set was skipped.");
                        return;
                    }
                }
                InsertTable(document.Database, report, location.Value, profile, true);
                editor.WriteMessage("\nHNL Tool - Table created from the same calculation report.");
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nHNL Tool - Runtime adapter error: " + ex.Message);
                // Do not partially create geometry on calculation errors.
            }
        }

        private static bool TryExtractHatch(Hatch hatch, out HatchInput input, out string reason,
            double expectedPitch = 610.0)
        {
            input = new HatchInput();
            reason = string.Empty;
            var stage = "Hatch.IsGradient";
            try
            {
            // Gradients have no line-grid pattern, and some pattern APIs throw
            // eNotApplicable on gradients. Reject before accessing their properties.
            if (hatch.IsGradient)
            {
                reason = "gradient fill is not a ceiling tile grid";
                return false;
            }
            stage = "Hatch.Normal";
            // HatchLoop points are in OCS; the XY WCS Core contract is valid only
            // for +Z hatches. Other normals need an explicit OCS->WCS transform gate.
            if (Math.Abs(hatch.Normal.X) > 1e-8 ||
                Math.Abs(hatch.Normal.Y) > 1e-8 ||
                hatch.Normal.Z < 1.0 - 1e-8)
            {
                reason = "unsupported OCS normal; +Z WCS Hatch required";
                return false;
            }
            stage = "Hatch.IsSolidFill";
            if (hatch.IsSolidFill)
            {
                reason = "solid fill has no reliable 610 mm pattern grid";
                return false;
            }
            stage = "Hatch.PatternType";
            var userDefined = hatch.PatternType == HatchPatternType.UserDefined;
            stage = userDefined ? "Hatch.PatternSpace / PatternDouble" : "Hatch.PatternScale";
            // Autodesk documents PatternScale for predefined/custom patterns,
            // and PatternSpace/PatternDouble for user-defined patterns only.
            // Do not query a property that does not apply to the pattern kind.
            var patternScale = userDefined ? 1.0 : hatch.PatternScale;
            if (double.IsNaN(patternScale) ||
                double.IsInfinity(patternScale) || !(patternScale > 0))
            {
                reason = "invalid Hatch.PatternScale";
                return false;
            }
            var userGridVerified = false;
            if (userDefined)
            {
                var doublePattern = hatch.PatternDouble;
                var spacing = hatch.PatternSpace;
                if (!doublePattern || double.IsNaN(spacing) ||
                    Math.Abs(spacing - expectedPitch) > 0.01)
                {
                    reason = "user-defined grid is not " + Fmt(expectedPitch) +
                        "x" + Fmt(expectedPitch) + " double pattern (space=" +
                        Fmt(spacing) + ", double=" + doublePattern + ")";
                    return false;
                }
                userGridVerified = true;
            }
            stage = "Hatch.NumberOfLoops";
            if (hatch.NumberOfLoops != 1)
            {
                reason = "loopCount=" + hatch.NumberOfLoops +
                    "; multi-loop/hole geometry pending parity validation";
                return false;
            }

            stage = "Hatch.GetLoopAt(0)";
            var boundary = hatch.GetLoopAt(0);
            // NotClosed is a DXF flag, not proof that a tiny seam means the
            // entire visible Hatch must be omitted. Validate the actual edges
            // and recover only a single near-zero seam in memory (never edit DWG).
            var flaggedNotClosed = (boundary.LoopType & HatchLoopTypes.NotClosed) != 0;
            if ((boundary.LoopType &
                (HatchLoopTypes.SelfIntersecting | HatchLoopTypes.Duplicate)) != 0)
            {
                reason = "loop=0 self-intersecting/duplicate Hatch boundary";
                return false;
            }
            stage = "HatchLoop edges";
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
                {
                    if (Math.Abs(polyline[vertexCount - 1].Bulge) > 1e-10)
                    {
                        reason = "loop=0 duplicated terminal vertex has arc bulge";
                        return false;
                    }
                    vertexCount--;
                }
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
            var nearClosedGapMm = 0.0;
            if (flaggedNotClosed)
            {
                // A recorded NotClosed flag may be caused by a tiny seam.
                // Accept only a single terminal gap <=0.01mm, without internal
                // join gaps or crossings. This includes DEMTAM handle 88 (0.002121mm).
                if (segments.Count > 512 || HasNonAdjacentIntersections(segments))
                {
                    reason = "NotClosed boundary topology cannot be recovered";
                    return false;
                }
                for (var i = 0; i < segments.Count - 1; i++)
                {
                    var a = segments[i];
                    var b = segments[i + 1];
                    var gap = Math.Sqrt((a.X2 - b.X1) * (a.X2 - b.X1) +
                        (a.Y2 - b.Y1) * (a.Y2 - b.Y1));
                    if (gap > 1e-6)
                    {
                        reason = "NotClosed internal edge gap: " + Fmt(gap) + "mm";
                        return false;
                    }
                }
                var last = segments[segments.Count - 1];
                var first = segments[0];
                nearClosedGapMm = Math.Sqrt((last.X2 - first.X1) *
                    (last.X2 - first.X1) + (last.Y2 - first.Y1) *
                    (last.Y2 - first.Y1));
                if (nearClosedGapMm > 0.01)
                {
                    reason = "NotClosed terminal gap too large: " +
                        Fmt(nearClosedGapMm) + "mm";
                    return false;
                }
                // Close the seam only in the temporary input segment array.
                // The real DWG Hatch is left unchanged.
                segments[segments.Count - 1] = new Segment2(
                    last.X1, last.Y1, first.X1, first.Y1);
            }

            // Compute a numerically stable signed area from the certified
            // linear one-loop boundary before accessing the native Hatch.Area.
            // Some valid non-associative USER Hatches throw eNotApplicable at Area.
            var refX = segments[0].X1;
            var refY = segments[0].Y1;
            var twiceArea = 0.0;
            foreach (var segment in segments)
            {
                twiceArea += (segment.X1 - refX) * (segment.Y2 - refY)
                    - (segment.X2 - refX) * (segment.Y1 - refY);
            }
            var boundaryArea = Math.Abs(twiceArea) * 0.5;
            if (!(boundaryArea > 0) || double.IsNaN(boundaryArea) ||
                double.IsInfinity(boundaryArea))
            {
                reason = "invalid closed-loop polygon area";
                return false;
            }

            stage = "Hatch.Area";
            var area = boundaryArea;
            var usedBoundaryArea = false;
            try
            {
                area = hatch.Area;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                if (ex.ErrorStatus.ToString() != "NotApplicable") throw;
                // The topology gate is deliberately narrower than the general
                // Core: never infer Area for holes, arcs, intersecting loops or
                // unchecked giant paths. Do not silently under-count material.
                if (segments.Count > 512 || HasNonAdjacentIntersections(segments))
                {
                    reason = "native Hatch.Area=NotApplicable, boundary topology not " +
                        "certified for mathematical area fallback";
                    return false;
                }
                usedBoundaryArea = true;
                area = boundaryArea;
            }

            if (!(area > 0) || double.IsNaN(area) || double.IsInfinity(area))
            {
                reason = "invalid Hatch.Area";
                return false;
            }
            var areaTolerance = Math.Max(0.01, area * 0.00001);
            if (Math.Abs(boundaryArea - area) > areaTolerance)
            {
                reason = "boundary/Hatch.Area mismatch: boundary=" + Fmt(boundaryArea) +
                    " hatch=" + Fmt(area) + " tolerance=" + Fmt(areaTolerance);
                return false;
            }
            stage = "Hatch.Origin / PatternAngle";
            if (double.IsNaN(hatch.Origin.X) || double.IsNaN(hatch.Origin.Y) ||
                double.IsNaN(hatch.PatternAngle) || double.IsInfinity(hatch.PatternAngle))
            {
                reason = "non-finite pattern origin/angle";
                return false;
            }

            stage = "Hatch.Layer / Color / PatternName";
            input.Handle = hatch.Handle.ToString();
            input.GroupKey = hatch.Layer + " | ACI=" +
                hatch.Color.ColorIndex.ToString(CultureInfo.InvariantCulture) +
                " | " + hatch.PatternName;
            input.Frame = new GridFrame(
                new Point2(hatch.Origin.X, hatch.Origin.Y), hatch.PatternAngle);
            input.Segments = segments;
            input.Area = area;
            input.AreaFromBoundary = usedBoundaryArea;
            input.RecoveredNearClosed = flaggedNotClosed;
            input.NearClosedGapMm = nearClosedGapMm;
            input.PatternScale = patternScale;
            input.PatternName = hatch.PatternName;
            input.UserGridVerified = userGridVerified;
            return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                reason = "AutoCAD " + ex.ErrorStatus + " at " + stage;
                return false;
            }
            catch (System.Exception ex)
            {
                reason = ex.GetType().Name + " at " + stage + ": " + ex.Message;
                return false;
            }
        }

        // Fail closed on complex/self-crossing boundaries when native
        // Hatch.Area is unavailable. Adjacent edges share a vertex by design.
        private static bool HasNonAdjacentIntersections(List<Segment2> segments)
        {
            const double eps = 1e-8;
            for (var i = 0; i < segments.Count; i++)
            {
                var a = segments[i];
                for (var j = i + 2; j < segments.Count; j++)
                {
                    if (i == 0 && j == segments.Count - 1) continue;
                    var b = segments[j];
                    if (Math.Max(a.X1, a.X2) < Math.Min(b.X1, b.X2) - eps ||
                        Math.Max(b.X1, b.X2) < Math.Min(a.X1, a.X2) - eps ||
                        Math.Max(a.Y1, a.Y2) < Math.Min(b.Y1, b.Y2) - eps ||
                        Math.Max(b.Y1, b.Y2) < Math.Min(a.Y1, a.Y2) - eps)
                        continue;
                    var c1 = (a.X2 - a.X1) * (b.Y1 - a.Y1) -
                             (a.Y2 - a.Y1) * (b.X1 - a.X1);
                    var c2 = (a.X2 - a.X1) * (b.Y2 - a.Y1) -
                             (a.Y2 - a.Y1) * (b.X2 - a.X1);
                    var c3 = (b.X2 - b.X1) * (a.Y1 - b.Y1) -
                             (b.Y2 - b.Y1) * (a.X1 - b.X1);
                    var c4 = (b.X2 - b.X1) * (a.Y2 - b.Y1) -
                             (b.Y2 - b.Y1) * (a.X2 - b.X1);
                    if ((c1 <= eps && c2 >= -eps || c2 <= eps && c1 >= -eps) &&
                        (c3 <= eps && c4 >= -eps || c4 <= eps && c3 >= -eps))
                        return true;
                }
            }
            return false;
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
            List<HatchInput> inputs, Editor editor, ref int rejected,
            bool tracePerHatch = false, DemtcOptions? selectedOptions = null)
        {
            var engine = new DemtcEngine();
            // HCEGOLDEN always uses unchanged defaults. UI is a separate snapshot.
            var options = selectedOptions ?? new DemtcOptions();
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

                MaterialGroup? group;
                if (!groups.TryGetValue(input.GroupKey, out group) || group == null)
                {
                    group = new MaterialGroup { Name = input.GroupKey };
                    groups.Add(input.GroupKey, group);
                    report.Groups.Add(group);
                }

                if (tracePerHatch)
                {
                    editor.WriteMessage("\nHCEGOLDEN RESULT handle=" + input.Handle +
                        " path=" + calc.Path +
                        " full=" + calc.PureResult.TotalFullCount +
                        " boundary=" + calc.PureResult.BoundaryCandidateCount +
                        " cutPieces=" + calc.PureResult.CutPieces.Count +
                        " slivers=" + calc.PureResult.SliverCount +
                        " areaResidual=" + Fmt(calc.PureResult.AreaResidual) +
                        " group=" + input.GroupKey);
                }
                group.HatchCount++;
                group.Full += calc.PureResult.TotalFullCount;
                group.BoundaryCells += calc.PureResult.BoundaryCandidateCount;
                group.Slivers += calc.PureResult.SliverCount;
                group.Cuts.AddRange(calc.PureResult.CutPieces);
                group.BoundaryFullPieces.AddRange(calc.PureResult.BoundaryFullPieces);
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
                    " | Slivers=" + group.Slivers +
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

        // CAD Table and N/G/L labels use only the already calculated immutable report.
        // No secondary geometry/packing pass is allowed here.
        private static int CountLabelCandidates(CalculationReport report)
        {
            var count = 0;
            foreach (var group in report.Groups)
            {
                count += group.BoundaryFullPieces.Count;
                foreach (var bin in group.Packing.Bins)
                    count += bin.PiecesReversed.Count;
            }
            return count;
        }

        // LISP-compatible A..Z, AA..AZ... sequence.
        private static string AlphaLabel(int number)
        {
            var label = string.Empty;
            while (number > 0)
            {
                number--;
                label = (char)('A' + number % 26) + label;
                number /= 26;
            }
            return label;
        }

        private sealed class LabelSpec
        {
            public Piece Piece = new Piece();
            public string Code = string.Empty;
            public short ColorIndex;
            public string SourceDisplay = string.Empty;
        }

        private static List<LabelSpec> BuildLabelSpecs(CalculationReport report,
            DemtcOptions options, out int groupedPieces, out int groupedBins, out int lonePieces)
        {
            var labels = new List<LabelSpec>();
            groupedPieces = 0;
            groupedBins = 0;
            lonePieces = 0;
            var nCode = 1;
            var gCode = 1;
            var lCode = 1;
            foreach (var group in report.Groups)
            {
                // Pure interior full cells have no coordinates. Like the LISP fast path,
                // label only the boundary full cells with known real WCS positions.
                foreach (var piece in group.BoundaryFullPieces)
                    labels.Add(new LabelSpec { Piece = piece, Code = "N-" + nCode++,
                        ColorIndex = 3 });
                foreach (var bin in group.Packing.Bins)
                {
                    var sourceName = options.MixedMode && bin.Code == "D"
                        ? options.LargeStock.DisplayName : options.SmallStock.DisplayName;
                    var pieces = new List<Piece>(bin.PiecesInLispOrder());
                    if (pieces.Count > 1)
                    {
                        for (var j = 0; j < pieces.Count; j++)
                            labels.Add(new LabelSpec { Piece = pieces[j],
                                Code = "G" + gCode + "-" + AlphaLabel(j + 1), ColorIndex = 2,
                                SourceDisplay = sourceName });
                        groupedPieces += pieces.Count;
                        groupedBins++;
                        gCode++;
                    }
                    else if (pieces.Count == 1)
                    {
                        labels.Add(new LabelSpec { Piece = pieces[0],
                            Code = "L-" + lCode++, ColorIndex = 1,
                            SourceDisplay = sourceName });
                        lonePieces++;
                    }
                }
            }
            return labels;
        }

        private static void AddCadLabel(BlockTableRecord space, Transaction transaction,
            LabelSpec spec, ObjectId styleId, double labelHeight)
        {
            var p = spec.Piece.LabelPointWcs;
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) ||
                double.IsInfinity(p.X) || double.IsInfinity(p.Y))
                throw new InvalidOperationException("HCE label has invalid WCS coordinates: " + spec.Code);
            var pt = new Point3d(p.X, p.Y, 0);
            var label = new DBText
            {
                TextString = spec.Code,
                Position = pt,
                Height = labelHeight,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = pt,
                ColorIndex = spec.ColorIndex
            };
            if (!styleId.IsNull) label.TextStyleId = styleId;
            space.AppendEntity(label);
            transaction.AddNewlyCreatedDBObject(label, true);
        }

        private sealed class CutSizeRow
        {
            public string Code = string.Empty;
            public int WidthMm;
            public int HeightMm;
            public string Source = string.Empty;
            public int Count = 1;
        }

        private static int RoundCutDimension(double dimension)
        {
            if (dimension < 0 || dimension > 10000000 ||
                double.IsNaN(dimension) || double.IsInfinity(dimension))
                throw new InvalidOperationException("Invalid cut piece dimension");
            return checked((int)Math.Round(dimension, 0, MidpointRounding.AwayFromZero));
        }

        // Exact LISP RC13 summary key: rounded W(mm) + rounded H(mm) + source.
        // Preserve source and orientation; do not merge rotated dimensions.
        private static List<CutSizeRow> BuildCutSizeRows(CalculationReport report,
            List<LabelSpec> labels, bool detailed)
        {
            var details = new List<CutSizeRow>();
            foreach (var label in labels)
            {
                if (label.Code.StartsWith("N-", StringComparison.Ordinal)) continue;
                details.Add(new CutSizeRow
                {
                    Code = label.Code,
                    WidthMm = RoundCutDimension(label.Piece.Width),
                    HeightMm = RoundCutDimension(label.Piece.Height),
                    Source = label.SourceDisplay
                });
            }
            var oversizeIndex = 1;
            foreach (var group in report.Groups)
                foreach (var piece in group.Packing.Oversize)
                    details.Add(new CutSizeRow
                    {
                        Code = "V-" + oversizeIndex++,
                        WidthMm = RoundCutDimension(piece.Width),
                        HeightMm = RoundCutDimension(piece.Height),
                        Source = "Vượt module"
                    });

            var expected = 0;
            foreach (var group in report.Groups) expected += group.Cuts.Count;
            if (details.Count != expected)
                throw new InvalidOperationException("HCE cut-list parity: " +
                    details.Count + " / " + expected + " pieces");
            if (detailed) return details;

            var totals = new Dictionary<string, CutSizeRow>(StringComparer.Ordinal);
            foreach (var row in details)
            {
                var key = row.WidthMm.ToString(CultureInfo.InvariantCulture) + "|" +
                    row.HeightMm.ToString(CultureInfo.InvariantCulture) + "|" + row.Source;
                if (totals.TryGetValue(key, out var item))
                    item.Count++;
                else
                    totals.Add(key, new CutSizeRow
                    {
                        Code = "Mảnh biên", WidthMm = row.WidthMm,
                        HeightMm = row.HeightMm, Source = row.Source, Count = 1
                    });
            }
            var summary = new List<CutSizeRow>(totals.Values);
            summary.Sort((a, b) =>
            {
                var c = a.WidthMm.CompareTo(b.WidthMm);
                if (c != 0) return c;
                c = a.HeightMm.CompareTo(b.HeightMm);
                return c != 0 ? c : string.CompareOrdinal(a.Source, b.Source);
            });
            return summary;
        }

        private static void InsertTable(Database database, CalculationReport report, Point3d point,
            HceLegacyProfile profile, bool drawLabels)
        {
            var options = profile.ToOptions();
            var labelHeight = profile.LabelTextHeight;
            if (labelHeight <= 0 || labelHeight > 10000 ||
                double.IsNaN(labelHeight) || double.IsInfinity(labelHeight))
                throw new InvalidOperationException("Invalid HCE label text height");
            var labelSpecs = BuildLabelSpecs(report, options,
                out var groupedPieces, out var groupedBins, out var lonePieces);
            var full = 0;
            var boundary = 0;
            var cuts = 0;
            var hatches = 0;
            var smallBins = 0;
            var longBins = 0;
            var oversize = 0;
            foreach (var group in report.Groups)
            {
                hatches += group.HatchCount;
                full += group.Full;
                boundary += group.BoundaryCells;
                cuts += group.Cuts.Count;
                smallBins += BinCount(group, "S");
                longBins += BinCount(group, "D");
                oversize += group.Packing.Oversize.Count;
            }

            // Build the legacy 4-column Vietnamese quantity breakdown.
            var mixed = options.MixedMode;
            var baseRows = mixed ? 11 : 9;
            var groupHeadingRow = baseRows;
            var cutDetailsEnabled = profile.CutListEnabled;
            var cutListIsDetailed = profile.CutListMode == "D";
            var cutRows = cutDetailsEnabled
                ? BuildCutSizeRows(report, labelSpecs, cutListIsDetailed)
                : new List<CutSizeRow>();
            var cutHeadingRow = groupHeadingRow + 1 + report.Groups.Count;
            var rows = cutHeadingRow + (cutDetailsEnabled ? 2 + Math.Max(1, cutRows.Count) : 0);
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)transaction.GetObject(
                    database.CurrentSpaceId, OpenMode.ForWrite);
                using (var table = new Table())
                {
                    table.SetSize(rows, 4);
                    var textHeight = profile.TableTextHeight;
                    if (textHeight < 0 || textHeight > 10000 ||
                        double.IsNaN(textHeight) || double.IsInfinity(textHeight))
                        throw new InvalidOperationException("Invalid HCE Table text height");
                    table.SetRowHeight(Math.Max(55, textHeight * 1.6));
                    table.SetColumnWidth(Math.Max(450, textHeight * 14));
                    var styleId = ObjectId.Null;
                    if (!string.IsNullOrEmpty(profile.TableTextStyle))
                    {
                        var styles = (TextStyleTable)transaction.GetObject(
                            database.TextStyleTableId, OpenMode.ForRead);
                        if (!styles.Has(profile.TableTextStyle))
                            throw new InvalidOperationException("HCE Table Text Style is missing from DWG: " +
                                profile.TableTextStyle);
                        styleId = styles[profile.TableTextStyle];
                    }
                    table.Position = point;
                    table.Cells[0, 0].TextString =
                        "BẢNG BÓC TÁCH KHỐI LƯỢNG TẤM TRẦN - HNL TOOL";
                    table.MergeCells(CellRange.Create(table, 0, 0, 0, 3));
                    string[] headers = { "Hạng mục", "Ký hiệu / Loại", "Số lượng", "Ghi chú" };
                    for (var i = 0; i < 4; i++) table.Cells[1, i].TextString = headers[i];

                    table.Cells[2, 0].TextString = "Tấm nguyên";
                    table.Cells[2, 1].TextString = "N-...";
                    table.Cells[2, 2].TextString = full + " tấm";
                    table.Cells[2, 3].TextString = options.GridWidth + " x " +
                        options.GridHeight + " mm (module)";

                    table.Cells[3, 0].TextString = "Ô biên kiểm tra";
                    table.Cells[3, 1].TextString = "Biên";
                    table.Cells[3, 2].TextString = boundary + " ô";
                    table.Cells[3, 3].TextString = "Không phải số mảnh cắt";

                    table.Cells[4, 0].TextString = "Mảnh ghép theo nhóm";
                    table.Cells[4, 1].TextString = "G1-A, G1-B...";
                    table.Cells[4, 2].TextString = groupedPieces + " mảnh";
                    table.Cells[4, 3].TextString = groupedBins + " nguồn";

                    table.Cells[5, 0].TextString = "Mảnh lẻ độc lập";
                    table.Cells[5, 1].TextString = "L-...";
                    table.Cells[5, 2].TextString = lonePieces + " mảnh";
                    table.Cells[5, 3].TextString = "1 mảnh / nguồn";

                    table.Cells[6, 0].TextString = "Mảnh vượt module";
                    table.Cells[6, 1].TextString = "V-...";
                    table.Cells[6, 2].TextString = oversize + " mảnh";
                    table.Cells[6, 3].TextString = oversize > 0 ? "Cần xử lý riêng" : "-";

                    if (mixed)
                    {
                        table.Cells[7, 0].TextString = "Nguồn cắt - tấm nhỏ";
                        table.Cells[7, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[7, 2].TextString = smallBins + " tấm";
                        table.Cells[7, 3].TextString = "Từ mảnh cắt";

                        table.Cells[8, 0].TextString = "Nguồn cắt - tấm dài";
                        table.Cells[8, 1].TextString = options.LargeStock.DisplayName + " mm";
                        table.Cells[8, 2].TextString = longBins + " tấm";
                        table.Cells[8, 3].TextString = "Từ mảnh cắt";

                        table.Cells[9, 0].TextString = "TỔNG MUA - TẤM NHỎ";
                        table.Cells[9, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[9, 2].TextString =
                            (smallBins + (options.MixedPrimary ==
                                MixedPrimaryMode.SmallMain ? full : 0)) + " tấm";
                        table.Cells[9, 3].TextString = "Tấm nguyên + nguồn cắt";

                        table.Cells[10, 0].TextString = "TỔNG MUA - TẤM DÀI";
                        table.Cells[10, 1].TextString = options.LargeStock.DisplayName + " mm";
                        table.Cells[10, 2].TextString =
                            (longBins + (options.MixedPrimary ==
                                MixedPrimaryMode.LargeMain ? full : 0)) + " tấm";
                        table.Cells[10, 3].TextString = "Tấm nguyên + nguồn cắt";
                    }
                    else
                    {
                        table.Cells[7, 0].TextString = "Nguồn cắt - cùng hệ";
                        table.Cells[7, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[7, 2].TextString = (smallBins + longBins) + " tấm";
                        table.Cells[7, 3].TextString = "Từ mảnh cắt";

                        table.Cells[8, 0].TextString = "TỔNG MUA";
                        table.Cells[8, 1].TextString = options.SmallStock.DisplayName + " mm";
                        table.Cells[8, 2].TextString = (full + smallBins + longBins) + " tấm";
                        table.Cells[8, 3].TextString = "Tấm nguyên + nguồn cắt";
                    }

                    table.Cells[groupHeadingRow, 0].TextString = "THEO NHÓM HATCH: " + hatches + " Hatch, " + cuts + " mảnh cắt";
                    table.MergeCells(CellRange.Create(table, groupHeadingRow, 0, groupHeadingRow, 3));
                    for (var i = 0; i < report.Groups.Count; i++)
                    {
                        var group = report.Groups[i];
                        var row = groupHeadingRow + 1 + i;
                        table.Cells[row, 0].TextString = group.Name;
                        table.Cells[row, 1].TextString = group.HatchCount + " Hatch";
                        table.Cells[row, 2].TextString = group.Full + " tấm nguyên";
                        table.Cells[row, 3].TextString = group.Cuts.Count + " mảnh cắt";
                    }

                    if (cutDetailsEnabled)
                    {
                        table.Cells[cutHeadingRow, 0].TextString =
                            "KÍCH THƯỚC MẢNH BIÊN - THEO MODULE";
                        table.MergeCells(CellRange.Create(table, cutHeadingRow, 0, cutHeadingRow, 3));
                        var cutHeaderRow = cutHeadingRow + 1;
                        table.Cells[cutHeaderRow, 0].TextString = cutListIsDetailed ? "Mã" : "Loại";
                        table.Cells[cutHeaderRow, 1].TextString = "Kích thước module (mm)";
                        table.Cells[cutHeaderRow, 2].TextString = "Số lượng";
                        table.Cells[cutHeaderRow, 3].TextString = "Nguồn cắt";
                        if (cutRows.Count == 0)
                        {
                            var emptyRow = cutHeaderRow + 1;
                            table.Cells[emptyRow, 0].TextString = "Không có mảnh biên";
                            table.MergeCells(CellRange.Create(table, emptyRow, 0, emptyRow, 3));
                        }
                        else
                        {
                            for (var i = 0; i < cutRows.Count; i++)
                            {
                                var cut = cutRows[i];
                                var row = cutHeaderRow + 1 + i;
                                table.Cells[row, 0].TextString = cut.Code;
                                table.Cells[row, 1].TextString = cut.WidthMm + " x " + cut.HeightMm;
                                table.Cells[row, 2].TextString = cut.Count.ToString(CultureInfo.InvariantCulture);
                                table.Cells[row, 3].TextString = cut.Source;
                            }
                        }
                    }

                    if (textHeight > 0 || !styleId.IsNull)
                    {
                        for (var row = 0; row < rows; row++)
                            for (var col = 0; col < 4; col++)
                            {
                                // Avoid styling merged child cells (only the first cell owns content).
                                if ((row == 0 || row == groupHeadingRow ||
                                    (cutDetailsEnabled && (row == cutHeadingRow ||
                                    (cutRows.Count == 0 && row == cutHeadingRow + 2)))) && col > 0)
                                    continue;
                                var cell = table.Cells[row, col];
                                if (textHeight > 0) cell.TextHeight = textHeight;
                                if (!styleId.IsNull) cell.TextStyleId = styleId;
                            }
                    }
                    table.GenerateLayout();
                    currentSpace.AppendEntity(table);
                    transaction.AddNewlyCreatedDBObject(table, true);
                    if (drawLabels)
                        foreach (var spec in labelSpecs)
                            AddCadLabel(currentSpace, transaction, spec, styleId, labelHeight);
                    // Both table and labels commit in one AutoCAD transaction.
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
            public bool AreaFromBoundary;
            public bool RecoveredNearClosed;
            public double NearClosedGapMm;
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
            public List<Piece> BoundaryFullPieces = new List<Piece>();
            public List<Piece> Cuts = new List<Piece>();
            public PackResult Packing = new PackResult();
        }
    }
}
