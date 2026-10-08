using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
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
            for (var i = 0; i < session.Boundaries.Count; i++)
            {
                var boundary = session.Boundaries[i];
                if (boundary == null) continue;

                var holes = i < session.BoundaryHoleGroups.Count &&
                            session.BoundaryHoleGroups[i] != null
                    ? session.BoundaryHoleGroups[i].Where(h => h != null).ToList()
                    : new List<Boundary2>();

                result.Add(new LayInBoundaryRuntimePlan
                {
                    BoundaryIndex = i,
                    Boundary = boundary,
                    Holes = holes,
                    Plan = LayInCeilingPlanner.Build(boundary, holes, settings)
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Không có biên trần Lay-in hợp lệ.");

            return result;
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
