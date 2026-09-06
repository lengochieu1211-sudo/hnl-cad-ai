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
