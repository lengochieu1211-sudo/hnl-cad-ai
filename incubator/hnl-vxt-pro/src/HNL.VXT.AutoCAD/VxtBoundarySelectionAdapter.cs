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
            var autoClosed = 0;
            var maxAutoCloseGap = 0.0;
            var tinyZNormalized = 0;
            var skippedOpen = 0;
            var skippedZ = 0;
            var skippedUnsupported = 0;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                foreach (var id in sourceIds)
                {
                    Entity entity = null;
                    try { entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
                    catch { }

                    Boundary2 boundary;
                    BoundarySampleInfo info;
                    var acceptedBoundary = false;

                    if (entity is Polyline pl)
                        acceptedBoundary = BoundarySampler.TryFromPolyline(pl, out boundary, out info);
                    else if (entity is Polyline2d pl2)
                        acceptedBoundary = BoundarySampler.TryFromPolyline2d(pl2, tr, out boundary, out info);
                    else
                    {
                        skippedUnsupported++;
                        continue;
                    }

                    if (!acceptedBoundary)
                    {
                        if (string.Equals(info.RejectionReason, "OpenGap", StringComparison.Ordinal))
                            skippedOpen++;
                        else if (string.Equals(info.RejectionReason, "Z", StringComparison.Ordinal))
                            skippedZ++;
                        else
                            skippedUnsupported++;
                        continue;
                    }

                    accepted.Add(boundary);
                    acceptedIds.Add(id);
                    if (info.AutoClosed)
                    {
                        autoClosed++;
                        maxAutoCloseGap = Math.Max(maxAutoCloseGap, info.ClosureGap);
                    }
                    if (info.TinyZNormalized)
                        tinyZNormalized++;
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

                var skipped = skippedOpen + skippedZ + skippedUnsupported;
                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã nhận " + accepted.Count + " biên từ tập chọn sẵn" +
                    (autoClosed > 0 ? " • Tự khép " + autoClosed : string.Empty) +
                    (tinyZNormalized > 0 ? " • Chuẩn Z≈0 " + tinyZNormalized : string.Empty) +
                    (skipped > 0 ? " • Bỏ qua " + skipped : string.Empty),
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
                    "\nHNL Tool - VXT Pro: Đã dùng " + accepted.Count + " biên trần từ tập chọn sẵn");
                if (autoClosed > 0)
                    ed.WriteMessage("; tự khép " + autoClosed + " biên, khe lớn nhất " +
                        maxAutoCloseGap.ToString("0.###") + " mm");
                if (tinyZNormalized > 0)
                    ed.WriteMessage("; chuẩn hóa Z≈0 cho " + tinyZNormalized + " biên");
                if (skippedOpen > 0)
                    ed.WriteMessage("; bỏ " + skippedOpen + " biên hở > 1 mm");
                if (skippedZ > 0)
                    ed.WriteMessage("; bỏ " + skippedZ + " biên có |Z| > 0.01 mm");
                if (skippedUnsupported > 0)
                    ed.WriteMessage("; bỏ " + skippedUnsupported + " đối tượng không hỗ trợ");
                ed.WriteMessage(".");
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