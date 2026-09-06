using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

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

            if (!settings.DrawMain && !settings.DrawFurring && !settings.DrawHangers && !settings.AutoDimension)
            {
                ed.WriteMessage("\nHNL Tool - VXT Pro: Không có tính năng nào được chọn.");
                return;
            }

            if (settings.MainDirection == MainDirectionMode.Auto &&
                (settings.DrawMain || settings.DrawFurring || settings.AutoDimension))
            {
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
            }

            // Preserve the existing Ask-each workflow that used to live in VxtCommands.Create.
            // Manual rectangle mode already stores the XP start side per HCN when the HCN is made.
            if (session.HasBoundary && settings.AskDirectionEachRegion &&
                settings.MainDirection != MainDirectionMode.RectangleRegions)
            {
                if (!PromptFurringStartSides(ed, session, settings)) return;
                VxtTransientPreview.Instance.Refresh();
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

        private static bool PromptFurringStartSides(Editor ed, VxtSession session, VxtSettings settings)
        {
            if (session.Regions.Count == 0)
            {
                bool far;
                if (!PromptFurringStartSide(ed, ResolveCurrentMainAngle(settings), "biên trần", out far)) return false;
                session.GlobalFurringFromFarEdge = far;
                return true;
            }

            for (var i = 0; i < session.Regions.Count; i++)
            {
                bool far;
                var region = session.Regions[i];
                if (!PromptFurringStartSide(ed, region.MainAngleDegrees, "vùng " + (i + 1), out far)) return false;
                region.FurringFromFarEdge = far;
            }
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

        private static double ResolveCurrentMainAngle(VxtSettings settings)
        {
            if (settings.MainDirection == MainDirectionMode.Vertical) return 90.0;
            if (settings.MainDirection == MainDirectionMode.TwoPoints || settings.MainDirection == MainDirectionMode.RectangleRegions)
                return settings.DirectionDegrees;
            return 0.0;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            return angle < 0.0 ? angle + 180.0 : angle;
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
