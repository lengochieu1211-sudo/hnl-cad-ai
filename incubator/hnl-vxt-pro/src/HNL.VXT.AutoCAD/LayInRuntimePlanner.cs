using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    // Model-space Hatch sources are DWG-owned. One record per ceiling boundary.
    internal sealed class LayInExistingHatchInfo
    {
        public ObjectId HatchId { get; set; }
        public LayInGridSystem System { get; set; }
        public Point2 Origin { get; set; }
        public double AngleRadians { get; set; }
        public string PatternName { get; set; }
        public int LoopCount { get; set; }
        public double HatchArea { get; set; }
    }

    internal sealed class LayInBoundaryRuntimePlan
    {
        public int BoundaryIndex { get; set; }
        public Boundary2 Boundary { get; set; }
        public IReadOnlyList<Boundary2> Holes { get; set; }
        public LayInCeilingPlan Plan { get; set; }
    }

    internal static class LayInRuntimePlanner
    {
        public static IReadOnlyList<LayInBoundaryRuntimePlan> Build(
            VxtSession session,
            LayInCeilingSettings settings)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!session.HasBoundary)
                throw new InvalidOperationException("Chưa chọn biên trần cho Lay-in Ceiling.");

            var result = new List<LayInBoundaryRuntimePlan>();
            var existing = session.LayInExistingHatches;
            if (!settings.DrawHatch)
            {
                if (existing.Count == 0 ||
                    existing.Count != session.Boundaries.Count ||
                    session.BoundaryIds.Count != existing.Count)
                    throw new InvalidOperationException(
                        "HNL Tool - Trần nổi: Tắt Vẽ Hatch cần chọn Hatch có sẵn làm nguồn lưới.");
                ValidateExistingHatchSources(session);
            }
            else if (existing.Count > 0)
            {
                throw new InvalidOperationException(
                    "HNL Tool - Trần nổi: Đã chọn Hatch có sẵn. Chọn Polyline trước khi bật Vẽ Hatch.");
            }

            for (var i = 0; i < session.Boundaries.Count; i++)
            {
                var boundary = session.Boundaries[i];
                if (boundary == null) continue;

                var holes = i < session.BoundaryHoleGroups.Count &&
                            session.BoundaryHoleGroups[i] != null
                    ? session.BoundaryHoleGroups[i].Where(h => h != null).ToList()
                    : new List<Boundary2>();

                // Preview and Create share the same source snapshot. Existing
                // Hatches have independent systems, rotations and origins.
                var perRegionSettings = settings.Clone();
                perRegionSettings.ExistingHatchOrigin = null;
                perRegionSettings.ExistingHatchAngleRadians = null;
                if (!settings.DrawHatch)
                {
                    var hatch = existing[i];
                    if (hatch.HatchId.IsNull ||
                        hatch.HatchId != session.BoundaryIds[i])
                        throw new InvalidOperationException(
                            "HNL Tool - Trần nổi: Hatch nguồn đã thay đổi; hãy chọn lại Hatch.");
                    perRegionSettings.GridSystem = hatch.System;
                    perRegionSettings.ExistingHatchOrigin = hatch.Origin;
                    perRegionSettings.ExistingHatchAngleRadians = hatch.AngleRadians;
                }

                result.Add(new LayInBoundaryRuntimePlan
                {
                    BoundaryIndex = i,
                    Boundary = boundary,
                    Holes = holes,
                    Plan = LayInCeilingPlanner.Build(boundary, holes, perRegionSettings)
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Không có biên trần Lay-in hợp lệ.");

            return result;
        }

        private static void ValidateExistingHatchSources(VxtSession session)
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("HNL Tool - Trần nổi: Không có DWG đang mở.");

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                for (var i = 0; i < session.LayInExistingHatches.Count; i++)
                {
                    var source = session.LayInExistingHatches[i];
                    if (source.HatchId.IsNull ||
                        source.HatchId != session.BoundaryIds[i])
                        throw new InvalidOperationException(
                            "HNL Tool - Trần nổi: Hatch nguồn đã thay đổi; hãy chọn lại.");
                    var hatch = tr.GetObject(source.HatchId, OpenMode.ForRead, false) as Hatch;
                    if (hatch == null ||
                        !string.Equals(hatch.PatternName, source.PatternName,
                            StringComparison.OrdinalIgnoreCase) ||
                        hatch.NumberOfLoops != source.LoopCount ||
                        Math.Abs(hatch.PatternScale - 1.0) > 0.000001 ||
                        Math.Abs(hatch.PatternAngle - source.AngleRadians) > 0.000001 ||
                        Math.Abs(hatch.Origin.X - source.Origin.X) > 0.01 ||
                        Math.Abs(hatch.Origin.Y - source.Origin.Y) > 0.01 ||
                        Math.Abs(hatch.Area - source.HatchArea) >
                            Math.Max(0.1, Math.Abs(source.HatchArea) * 0.000000001))
                    {
                        throw new InvalidOperationException(
                            "HNL Tool - Trần nổi: Hatch nguồn đã bị sửa; hãy chọn lại Hatch trước khi tạo Ty/DIM.");
                    }
                }
                tr.Commit();
            }
        }

        public static void Summarize(
            IEnumerable<LayInBoundaryRuntimePlan> plans,
            out int mainStocks,
            out int longCross,
            out int shortCross,
            out int hangers,
            out double waste)
        {
            var list = (plans ?? Enumerable.Empty<LayInBoundaryRuntimePlan>())
                .Where(x => x?.Plan != null)
                .Select(x => x.Plan)
                .ToList();

            mainStocks = list.Sum(x => x.MainTeeStockCount);
            longCross = list.Sum(x => x.LongCrossTeeCount);
            shortCross = list.Sum(x => x.ShortCrossTeeCount);
            hangers = list.Sum(x => x.HangerPoints.Count);
            waste = list.Sum(x => x.WasteLength);
        }
    }
}
