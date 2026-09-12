using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Bridges AutoCAD PickFirst/implied selection into the VXT session.
    /// This prevents a user who already selected one or more closed ceiling polylines
    /// from being asked to select the same boundaries a second time.
    /// </summary>
    internal static class VxtBoundarySelectionAdapter
    {
        public static bool TryAdoptImpliedSelection(bool refreshPreview, bool writeMessage)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var ed = doc.Editor;
            PromptSelectionResult implied;
            try
            {
                implied = ed.SelectImplied();
            }
            catch
            {
                return false;
            }

            if (implied == null || implied.Status != PromptStatus.OK || implied.Value == null)
                return false;

            var sourceIds = implied.Value.GetObjectIds();
            if (sourceIds == null || sourceIds.Length == 0)
                return false;

            var accepted = new List<Boundary2>();
            var acceptedIds = new List<ObjectId>();
            var skipped = 0;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var id in sourceIds)
                {
                    Entity entity = null;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { }

                    if (entity is Polyline pl)
                    {
                        if (!pl.Closed) { skipped++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline(pl));
                        acceptedIds.Add(id);
                    }
                    else if (entity is Polyline2d pl2)
                    {
                        if (!pl2.Closed) { skipped++; continue; }
                        accepted.Add(BoundarySampler.FromPolyline2d(pl2, tr));
                        acceptedIds.Add(id);
                    }
                    else
                    {
                        skipped++;
                    }
                }

                if (accepted.Count == 0)
                    return false;

                var session = VxtSession.Current;
                session.Boundaries.Clear();
                session.Boundaries.AddRange(accepted);
                session.BoundaryIds.Clear();
                session.BoundaryIds.AddRange(acceptedIds);
                session.Regions.Clear();
                session.BoundaryRegionGroups.Clear();
                session.BoundaryFurringFromFarEdges.Clear();
                session.GlobalFurringFromFarEdge = false;

                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã nhận " + accepted.Count + " Polyline kín từ tập chọn sẵn" +
                    (skipped > 0 ? " • Bỏ qua " + skipped + " đối tượng không hợp lệ" : string.Empty),
                    true);

                tr.Commit();
            }

            // Consume PickFirst after successful adoption so the same ceiling boundary cannot leak
            // into a later manual-existing / MEP selection prompt in the same workflow.
            try { ed.SetImpliedSelection(Array.Empty<ObjectId>()); }
            catch { }

            if (writeMessage)
            {
                ed.WriteMessage(
                    "\nHNL Tool - VXT Pro: Đã dùng " + accepted.Count +
                    " Polyline kín đang chọn làm biên trần" +
                    (skipped > 0 ? "; bỏ qua " + skipped + " đối tượng không hợp lệ." : "."));
            }

            if (refreshPreview)
            {
                var mode = VxtSession.Current.Settings.MainDirection;
                if (mode == MainDirectionMode.TwoPoints || mode == MainDirectionMode.RectangleRegions)
                    VxtTransientPreview.Instance.Clear();
                else
                    VxtTransientPreview.Instance.Refresh();
            }

            return true;
        }
    }
}