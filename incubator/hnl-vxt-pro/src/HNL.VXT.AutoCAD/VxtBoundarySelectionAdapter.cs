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
            var acceptedLayers = new List<string>();
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
                    var acceptedBoundary = BoundarySampler.TryFromEntity(entity, tr, out boundary, out info);

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
                    acceptedLayers.Add(entity.Layer);
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

                VxtSelectedBoundaryTopology.Arrange(accepted, acceptedIds, acceptedLayers,
                    out var outerBoundaries, out var outerIds, out var holeGroups);
                var session = VxtSession.Current;
                session.Boundaries.Clear();
                session.Boundaries.AddRange(outerBoundaries);
                session.BoundaryIds.Clear();
                session.BoundaryIds.AddRange(outerIds);
                session.LayInExistingHatches.Clear();
                session.ViewModel?.LayIn?.SetPolylineSourceSelected();
                session.Regions.Clear();
                session.BoundaryRegionGroups.Clear();
                session.BoundaryHoleGroups.Clear();
                session.BoundaryHoleGroups.AddRange(holeGroups);
                session.BoundaryFurringFromFarEdges.Clear();
                session.GlobalFurringFromFarEdge = false;

                var skipped = skippedOpen + skippedZ + skippedUnsupported;
                session.ViewModel?.SetBoundaryStatus(
                    "✓ Đã nhận " + outerBoundaries.Count + " mảng, " + holeGroups.Sum(g => g.Count) + " lỗ từ tập chọn sẵn" +
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
                    "\nHNL Tool - HCF: Đã dùng " + outerBoundaries.Count + " mảng trần và " +
                    holeGroups.Sum(g => g.Count) + " lỗ từ tập chọn sẵn");
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
                VxtCommands.RefreshBoundaryPreviewForActiveTab(doc);
            }

            return true;
        }
    }
}