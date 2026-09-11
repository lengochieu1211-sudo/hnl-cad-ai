using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Restores the create-time questions that VXT V6.7.2 asked after its DCL closed.
    /// Keeping these prompts in the AutoCAD bridge lets Core stay deterministic while the
    /// workflow remains familiar to users of the original Lisp.
    /// </summary>
    internal static class VxtLegacyParityCoordinator
    {
        public static void ExecuteCreate()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var session = VxtSession.Current;
            var settings = session.ViewModel != null ? session.ViewModel.Snapshot() : session.Settings.Clone();
            session.Settings = settings;
            var ed = doc.Editor;

            if (!VxtWorkflowEligibility.HasAnyTask(settings))
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Không có tính năng nào được chọn.");
                return;
            }

            if (settings.MainDirection == MainDirectionMode.Auto &&
                (settings.DrawMain || settings.DrawFurring || settings.AutoDimension))
            {
                var previousShadowline = settings.AutoShadowline;
                var options = new PromptKeywordOptions("\nHNL Tool - VXT Pro: Trần có đi Shadowline không? [Yes/No] <Yes>: ")
                {
                    AllowNone = true
                };
                options.Keywords.Add("Yes");
                options.Keywords.Add("No");
                options.Keywords.Default = "Yes";
                var result = ed.GetKeywords(options);
                if (result.Status == PromptStatus.Cancel) return;
                settings.AutoShadowline = result.Status == PromptStatus.None ||
                                          string.Equals(result.StringResult, "Yes", StringComparison.OrdinalIgnoreCase);
                session.ViewModel?.SetAutoShadowlineFromHost(settings.AutoShadowline);
                session.Settings = settings;

                // WYSIWYG gate: changing Shadowline changes the Auto construction direction.
                // Never create geometry from a direction that the user has not yet seen in Preview.
                // If this create-time legacy question changes the setting, refresh synchronously
                // and require an explicit second Create after visual confirmation.
                if (settings.AutoShadowline != previousShadowline && session.HasBoundary)
                {
                    VxtTransientPreview.Instance.Refresh();
                    ed.WriteMessage(
                        "\nHNL Tool - VXT Pro: Shadowline đã làm thay đổi hướng Auto. " +
                        "Preview đã được cập nhật; hãy kiểm tra rồi bấm Tạo khung xương trần lần nữa.");
                    return;
                }
            }

            // V6.7.2 ask_each is per selected ceiling Polyline/region and only applies when XP
            // itself is drawn or XP DIM needs an existing/generated XP direction. Rectangle mode
            // already asks and stores the XP side immediately for each HCN, so never ask twice.
            if (session.HasBoundary && settings.AskDirectionEachRegion &&
                settings.MainDirection != MainDirectionMode.RectangleRegions &&
                (settings.DrawFurring || (settings.AutoDimension && settings.DimFurring)))
            {
                if (!PromptFurringStartSides(doc, session, settings)) return;
                VxtTransientPreview.Instance.Refresh();
            }
            else if (!settings.AskDirectionEachRegion || settings.MainDirection == MainDirectionMode.RectangleRegions)
            {
                // Prevent an earlier multi-boundary ask_each selection from leaking into a later run.
                session.BoundaryFurringFromFarEdges.Clear();
            }

            // The Lisp creates fresh selection sets every run. Clearing them here also prevents
            // a cancelled selection from silently reusing stale CAD objects from an earlier run.
            session.ManualMainIds = Array.Empty<ObjectId>();
            session.ManualFurringIds = Array.Empty<ObjectId>();
            session.ManualHangerIds = Array.Empty<ObjectId>();
            session.ManualHangerReverseHorizontal = false;
            session.ManualHangerReverseVertical = false;

            if (!settings.DrawMain && (settings.DrawHangers || (settings.AutoDimension && settings.DimMain)))
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã tắt Rải Xương Chính. Quét chọn Xương Chính có sẵn để rải Ty hoặc DIM.");
                session.ManualMainIds = SelectExisting(ed, "LINE,LWPOLYLINE,POLYLINE,INSERT");
            }

            if (!settings.DrawFurring && settings.AutoDimension && settings.DimFurring)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã tắt Rải Xương Phụ. Quét chọn Xương Phụ có sẵn để ghi kích thước.");
                session.ManualFurringIds = SelectExisting(ed, "LINE,LWPOLYLINE,POLYLINE,INSERT");
            }

            if (!settings.DrawHangers && settings.AutoDimension && settings.DimHanger)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Đã tắt Rải Ty. Quét chọn Ty treo có sẵn để ghi kích thước.");
                session.ManualHangerIds = SelectExisting(ed, "INSERT");
            }

            if (!settings.DrawMain && settings.DrawHangers &&
                settings.HangerLayout == HangerLayoutMode.OneSideFollowFurring && session.ManualMainIds.Length > 0)
            {
                PromptManualHangerDirections(doc, session);
            }

            // The original Lisp checks the Ty block globally before it starts any drawing.
            // If Ty is enabled and its block is missing, XC/XP must not be created partially.
            if (settings.DrawHangers && !HasBlock(doc.Database, settings.HangerBlockName))
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Không tìm thấy Block Ty treo '" +
                                settings.HangerBlockName + "'. Không tạo đối tượng nào.");
                return;
            }

            // In the special no-boundary legacy path, Enter/Cancel at the existing-XC selection
            // means there is simply no source member to process. Do not fall through to a misleading
            // 'chưa chọn biên trần' message from the create engine.
            if (!session.HasBoundary && VxtWorkflowEligibility.IsManualHangerOnlyStart(settings) &&
                session.ManualMainIds.Length == 0)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Chưa chọn Xương Chính có sẵn để rải Ty. Không tạo đối tượng nào.");
                return;
            }

            VxtCreateEngine.Execute();
        }

        private static ObjectId[] SelectExisting(Editor ed, string dxfNames)
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nHNL Tool - VXT Pro: Chọn đối tượng có sẵn <Enter = bỏ qua>: ",
                MessageForRemoval = "\nHNL Tool - VXT Pro: Bỏ đối tượng khỏi tập chọn: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, dxfNames)
            });
            var result = ed.GetSelection(options, filter);
            return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : Array.Empty<ObjectId>();
        }

        private static bool HasBlock(Database db, string blockName)
        {
            if (db == null || string.IsNullOrWhiteSpace(blockName)) return false;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var exists = bt != null && bt.Has(blockName);
                    tr.Commit();
                    return exists;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool PromptFurringStartSides(Document doc, VxtSession session, VxtSettings settings)
        {
            var ed = doc.Editor;
            session.BoundaryFurringFromFarEdges.Clear();

            for (var i = 0; i < session.Boundaries.Count; i++)
            {
                var boundary = session.Boundaries[i];
                var boundaryId = i < session.BoundaryIds.Count ? session.BoundaryIds[i] : ObjectId.Null;
                var angle = ResolveBoundaryMainAngle(settings, boundary);
                bool far;

                TryHighlight(doc, boundaryId, true);
                try
                {
                    if (!PromptFurringStartSide(ed, angle,
                        "mảng trần " + (i + 1) + "/" + session.Boundaries.Count, out far))
                    {
                        session.BoundaryFurringFromFarEdges.Clear();
                        return false;
                    }
                }
                finally
                {
                    TryHighlight(doc, boundaryId, false);
                }

                session.BoundaryFurringFromFarEdges.Add(far);
            }

            if (session.BoundaryFurringFromFarEdges.Count > 0)
                session.GlobalFurringFromFarEdge = session.BoundaryFurringFromFarEdges[0];
            return true;
        }

        private static bool PromptFurringStartSide(Editor ed, double mainAngleDegrees, string label, out bool fromFarEdge)
        {
            fromFarEdge = false;
            var radians = NormalizeAngle(mainAngleDegrees) * Math.PI / 180.0;
            var verticalLike = Math.Abs(Math.Sin(radians)) > Math.Abs(Math.Cos(radians));
            PromptKeywordOptions options;
            if (verticalLike)
            {
                options = new PromptKeywordOptions("\nHNL Tool - VXT Pro: Chọn hướng rải Xương phụ cho " + label + " [Duoi/Tren] <Duoi>: ");
                options.Keywords.Add("Duoi");
                options.Keywords.Add("Tren");
                options.Keywords.Default = "Duoi";
            }
            else
            {
                options = new PromptKeywordOptions("\nHNL Tool - VXT Pro: Chọn hướng rải Xương phụ cho " + label + " [Trai/Phai] <Trai>: ");
                options.Keywords.Add("Trai");
                options.Keywords.Add("Phai");
                options.Keywords.Default = "Trai";
            }

            var result = ed.GetKeywords(options);
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None) return false;
            var value = result.Status == PromptStatus.None ? options.Keywords.Default : result.StringResult;
            fromFarEdge = value == "Phai" || value == "Tren";
            return true;
        }

        private static double ResolveBoundaryMainAngle(VxtSettings settings, Boundary2 boundary)
        {
            if (settings.MainDirection == MainDirectionMode.Vertical) return 90.0;
            if (settings.MainDirection == MainDirectionMode.TwoPoints ||
                settings.MainDirection == MainDirectionMode.RectangleRegions)
                return settings.DirectionDegrees;
            if (settings.MainDirection == MainDirectionMode.Auto && boundary != null)
            {
                var bounds = boundary.GetBounds();
                var wide = bounds.Max.X - bounds.Min.X > bounds.Max.Y - bounds.Min.Y;
                var legacyAngle = settings.AutoShadowline
                    ? (wide ? 0.0 : 90.0)
                    : (wide ? 90.0 : 0.0);

                // Legacy must keep exact V6.7.x bbox semantics. Pro Auto, however, can follow a
                // rotated real polygon axis; use the same preferred-axis resolver as the Pro solver
                // so the XP side prompt is oriented consistently with the visible Preview.
                if (settings.OptimizationMode != VxtOptimizationMode.Legacy)
                    return VxtProAutoDirectionPlanBuilder.ResolvePreferredAutoAngle(
                        boundary, settings.AutoShadowline, legacyAngle);

                return legacyAngle;
            }
            return 0.0;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            return angle < 0.0 ? angle + 180.0 : angle;
        }

        private static void TryHighlight(Document doc, ObjectId id, bool highlight)
        {
            if (doc == null || id.IsNull || !id.IsValid || id.IsErased) return;
            try
            {
                using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity != null)
                    {
                        if (highlight) entity.Highlight();
                        else entity.Unhighlight();
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // Highlight is only visual guidance; it must never block layout.
            }
        }

        private static void PromptManualHangerDirections(Document doc, VxtSession session)
        {
            var hasHorizontal = false;
            var hasVertical = false;
            using (var tr = doc.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (var id in session.ManualMainIds)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    try
                    {
                        var ext = entity.GeometricExtents;
                        var axis = ExistingMemberLayout.FromBounds(new Box2(
                            ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y));
                        if (axis.IsHorizontal) hasHorizontal = true;
                        else hasVertical = true;
                    }
                    catch
                    {
                        // Match the Lisp's GetBoundingBox failure tolerance: skip that object.
                    }
                }
                tr.Commit();
            }

            if (hasHorizontal)
                session.ManualHangerReverseHorizontal = PromptReverse(
                    doc.Editor,
                    "\nHNL Tool - VXT Pro: Dồn Ty 1 phía (Xương ngang) - hướng dồn [Trai/Phai] <Trai>: ",
                    "Trai", "Phai");

            if (hasVertical)
                session.ManualHangerReverseVertical = PromptReverse(
                    doc.Editor,
                    "\nHNL Tool - VXT Pro: Dồn Ty 1 phía (Xương dọc) - hướng dồn [Duoi/Tren] <Duoi>: ",
                    "Duoi", "Tren");
        }

        private static bool PromptReverse(Editor ed, string message, string normalKeyword, string reverseKeyword)
        {
            var options = new PromptKeywordOptions(message) { AllowNone = true };
            options.Keywords.Add(normalKeyword);
            options.Keywords.Add(reverseKeyword);
            options.Keywords.Default = normalKeyword;
            var result = ed.GetKeywords(options);
            return result.Status == PromptStatus.OK &&
                   string.Equals(result.StringResult, reverseKeyword, StringComparison.OrdinalIgnoreCase);
        }
    }
}
