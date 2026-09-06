using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// AutoCAD bridge for the V6.7.2 manual-source branch: existing XC can receive new Ty,
    /// and existing XC/XP/Ty can feed automatic DIM when that system is not redrawn.
    /// Geometry math remains deterministic and follows the same local-axis convention as
    /// VxtPreviewPlanBuilder (main member is horizontal in local coordinates).
    /// </summary>
    internal static class VxtManualExistingEngine
    {
        internal sealed class Counts
        {
            public int Hangers;
            public int Dimensions;
        }

        public static Counts Append(
            Database db,
            Transaction tr,
            BlockTableRecord modelSpace,
            BlockTable blockTable,
            LayerTable layerTable,
            DimStyleTable dimStyleTable,
            VxtSession session,
            VxtSettings settings)
        {
            var counts = new Counts();

            if (!settings.DrawMain && settings.DrawHangers && session.ManualMainIds.Length > 0)
                counts.Hangers += AppendManualHangers(db, tr, modelSpace, blockTable, layerTable, session, settings);

            if (settings.AutoDimension && session.HasBoundary)
                counts.Dimensions += AppendManualDimensions(
                    db, tr, modelSpace, layerTable, dimStyleTable, session, settings);

            return counts;
        }

        private static int AppendManualHangers(
            Database db,
            Transaction tr,
            BlockTableRecord modelSpace,
            BlockTable blockTable,
            LayerTable layerTable,
            VxtSession session,
            VxtSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.HangerBlockName) || !blockTable.Has(settings.HangerBlockName))
                throw new InvalidOperationException("Không tìm thấy Block Ty treo '" + settings.HangerBlockName + "'. Hãy chọn Block Ty trước khi tạo.");

            var hangerBlockId = blockTable[settings.HangerBlockName];
            var hangerLayerId = layerTable[settings.HangerLayer];
            var obstacles = ReadBoxes(
                session.GeneralEquipmentIds.Concat(session.MainEquipmentIds).Distinct(), tr);
            var count = 0;

            foreach (var id in session.ManualMainIds)
            {
                var entity = TryGetEntity(id, tr);
                if (entity == null) continue;
                Box2 box;
                try { box = ToBox(entity.GeometricExtents); }
                catch { continue; }

                var axis = ExistingMemberLayout.FromBounds(box);
                var reverse = axis.IsHorizontal
                    ? session.ManualHangerReverseHorizontal
                    : session.ManualHangerReverseVertical;
                var points = ExistingMemberLayout.HangerPoints(axis, settings, reverse, obstacles);
                foreach (var point in points)
                {
                    var br = new BlockReference(new Point3d(point.X, point.Y, 0.0), hangerBlockId)
                    {
                        Rotation = axis.IsHorizontal ? 0.0 : Math.PI * 0.5
                    };
                    br.SetDatabaseDefaults(db);
                    VxtCadResources.ApplyByLayer(br, hangerLayerId);
                    modelSpace.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                    count++;
                }
            }
            return count;
        }

        private static int AppendManualDimensions(
            Database db,
            Transaction tr,
            BlockTableRecord modelSpace,
            LayerTable layerTable,
            DimStyleTable dimStyleTable,
            VxtSession session,
            VxtSettings settings)
        {
            var needMain = settings.DimMain && !settings.DrawMain && session.ManualMainIds.Length > 0;
            var needFurring = settings.DimFurring && !settings.DrawFurring && session.ManualFurringIds.Length > 0;
            var needHanger = settings.DimHanger && !settings.DrawHangers && session.ManualHangerIds.Length > 0;
            if (!needMain && !needFurring && !needHanger) return 0;

            var mainCenters = needMain ? ReadCenters(session.ManualMainIds, tr) : new List<Point2>();
            var furringCenters = needFurring ? ReadCenters(session.ManualFurringIds, tr) : new List<Point2>();
            var hangerCenters = needHanger ? ReadCenters(session.ManualHangerIds, tr) : new List<Point2>();
            var dimStyleId = VxtTransientPreview.ResolveDimStyle(settings.DimensionStyle, db, dimStyleTable);
            var dimLayerId = layerTable[settings.DimensionLayer];
            var count = 0;

            for (var boundaryIndex = 0; boundaryIndex < session.Boundaries.Count; boundaryIndex++)
            {
                var boundary = session.Boundaries[boundaryIndex];
                foreach (var domain in ResolveDomains(session, settings, boundary, boundaryIndex))
                {
                    var radians = domain.AngleDegrees * Math.PI / 180.0;
                    var localBox = ToLocalBox(domain.WorldBounds, radians);
                    var localMain = FilterLocal(mainCenters, localBox, radians);
                    var localFurring = FilterLocal(furringCenters, localBox, radians);
                    var localHangers = FilterLocal(hangerCenters, localBox, radians);
                    var dimensions = new List<PreviewDimension>();
                    var stack = new Dictionary<string, int>(StringComparer.Ordinal);

                    if (needMain)
                    {
                        var ys = localMain.Select(p => p.Y).ToList();
                        AddVerticalChain(dimensions, WithBounds(ys, localBox.MinY, localBox.MaxY),
                            settings.MainDimPosition, DimensionTarget.Main, radians, localBox,
                            settings.DimensionDistance, settings.DimensionSpacing, stack);
                    }

                    if (needFurring)
                    {
                        var xs = localFurring.Select(p => p.X).ToList();
                        AddHorizontalChain(dimensions, WithBounds(xs, localBox.MinX, localBox.MaxX),
                            settings.FurringDimPosition, DimensionTarget.Furring, radians, localBox,
                            settings.DimensionDistance, settings.DimensionSpacing, stack);
                    }

                    if (needHanger)
                    {
                        var rows = GroupHangerRows(localHangers);
                        var uniquePatterns = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var row in rows)
                        {
                            var xs = row.Value.Select(p => p.X).Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
                            if (xs.Count == 0) continue;
                            var key = string.Join("|", xs.Select(v => Math.Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture)));
                            if (!uniquePatterns.Add(key)) continue;
                            AddHorizontalChain(dimensions, WithBounds(xs, localBox.MinX, localBox.MaxX),
                                settings.HangerDimPosition, DimensionTarget.Hanger, radians, localBox,
                                settings.DimensionDistance, settings.DimensionSpacing, stack, row.Key);
                        }
                    }

                    foreach (var item in dimensions)
                    {
                        var dim = new RotatedDimension(
                            item.RotationRadians,
                            ToPoint3d(item.ExtensionPoint1),
                            ToPoint3d(item.ExtensionPoint2),
                            ToPoint3d(item.DimensionLinePoint),
                            string.Empty,
                            dimStyleId);
                        dim.SetDatabaseDefaults(db);
                        VxtCadResources.ApplyByLayer(dim, dimLayerId);
                        modelSpace.AppendEntity(dim);
                        tr.AddNewlyCreatedDBObject(dim, true);
                        count++;
                    }
                }
            }
            return count;
        }

        private sealed class ManualDomain
        {
            public ManualDomain(Box2 worldBounds, double angleDegrees)
            {
                WorldBounds = worldBounds;
                AngleDegrees = angleDegrees;
            }
            public Box2 WorldBounds { get; }
            public double AngleDegrees { get; }
        }

        private static IEnumerable<ManualDomain> ResolveDomains(
            VxtSession session,
            VxtSettings settings,
            Boundary2 boundary,
            int boundaryIndex)
        {
            if (settings.MainDirection == MainDirectionMode.RectangleRegions &&
                boundaryIndex < session.BoundaryRegionGroups.Count &&
                session.BoundaryRegionGroups[boundaryIndex].Count > 0)
            {
                foreach (var region in session.BoundaryRegionGroups[boundaryIndex])
                    yield return new ManualDomain(region.WorldBounds, region.MainAngleDegrees);
                yield break;
            }

            var points = boundary.Vertices;
            var worldBox = Box2.FromPoints(points);
            yield return new ManualDomain(worldBox, ResolveMainAngle(settings, worldBox));
        }

        private static double ResolveMainAngle(VxtSettings settings, Box2 worldBox)
        {
            switch (settings.MainDirection)
            {
                case MainDirectionMode.Vertical: return 90.0;
                case MainDirectionMode.TwoPoints: return NormalizeAngle(settings.DirectionDegrees);
                case MainDirectionMode.Auto:
                    var longHorizontal = worldBox.Width >= worldBox.Height;
                    if (!settings.AutoShadowline) longHorizontal = !longHorizontal;
                    return longHorizontal ? 0.0 : 90.0;
                default: return 0.0;
            }
        }

        private static Box2 ToLocalBox(Box2 box, double radians)
        {
            var corners = new[]
            {
                new Point2(box.MinX, box.MinY), new Point2(box.MinX, box.MaxY),
                new Point2(box.MaxX, box.MinY), new Point2(box.MaxX, box.MaxY)
            };
            return Box2.FromPoints(corners.Select(p => Transform2.ToLocal(p, radians)));
        }

        private static List<Point2> FilterLocal(IEnumerable<Point2> worldPoints, Box2 localBox, double radians)
        {
            return worldPoints
                .Select(p => Transform2.ToLocal(p, radians))
                .Where(p => p.X >= localBox.MinX - 5.0 && p.X <= localBox.MaxX + 5.0 &&
                            p.Y >= localBox.MinY - 5.0 && p.Y <= localBox.MaxY + 5.0)
                .ToList();
        }

        private static List<KeyValuePair<double, List<Point2>>> GroupHangerRows(IEnumerable<Point2> points)
        {
            var rows = new List<KeyValuePair<double, List<Point2>>>();
            foreach (var point in points)
            {
                var index = rows.FindIndex(x => Math.Abs(x.Key - point.Y) <= 0.001);
                if (index < 0)
                {
                    rows.Add(new KeyValuePair<double, List<Point2>>(point.Y, new List<Point2> { point }));
                }
                else
                {
                    rows[index].Value.Add(point);
                }
            }
            return rows;
        }

        private static List<Point2> ReadCenters(IEnumerable<ObjectId> ids, Transaction tr)
        {
            var result = new List<Point2>();
            foreach (var id in ids)
            {
                var entity = TryGetEntity(id, tr);
                if (entity == null) continue;
                try
                {
                    var ext = entity.GeometricExtents;
                    result.Add(new Point2(
                        (ext.MinPoint.X + ext.MaxPoint.X) * 0.5,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5));
                }
                catch { }
            }
            return result;
        }

        private static List<Box2> ReadBoxes(IEnumerable<ObjectId> ids, Transaction tr)
        {
            var result = new List<Box2>();
            foreach (var id in ids)
            {
                var entity = TryGetEntity(id, tr);
                if (entity == null) continue;
                try { result.Add(ToBox(entity.GeometricExtents)); }
                catch { }
            }
            return result;
        }

        private static Entity TryGetEntity(ObjectId id, Transaction tr)
        {
            if (id.IsNull) return null;
            try { return tr.GetObject(id, OpenMode.ForRead, false) as Entity; }
            catch { return null; }
        }

        private static Box2 ToBox(Extents3d ext)
            => new Box2(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);

        private static Point3d ToPoint3d(Point2 p) => new Point3d(p.X, p.Y, 0.0);

        private static IEnumerable<double> WithBounds(IEnumerable<double> values, double min, double max)
        {
            yield return min;
            foreach (var value in values) yield return value;
            yield return max;
        }

        private static void AddVerticalChain(
            ICollection<PreviewDimension> output,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget target,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var ys = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
            if (ys.Count < 2) return;

            double baseX;
            double textX;
            if (position == DimensionPosition.Auto)
            {
                var index = GetAndIncrement(stack, "C-V");
                baseX = (domain.MinX + domain.MaxX) * 0.5;
                textX = baseX + index * spacing;
            }
            else if (position == DimensionPosition.Left || position == DimensionPosition.Bottom)
            {
                var index = GetAndIncrement(stack, "L");
                baseX = domain.MinX;
                textX = domain.MinX - distance - index * spacing;
            }
            else
            {
                var index = GetAndIncrement(stack, "R");
                baseX = domain.MaxX;
                textX = domain.MaxX + distance + index * spacing;
            }
            if (sourceBaseCoordinate.HasValue) baseX = sourceBaseCoordinate.Value;

            for (var i = 0; i + 1 < ys.Count; i++)
            {
                if (Math.Abs(ys[i + 1] - ys[i]) <= 1.0) continue;
                output.Add(new PreviewDimension(
                    Transform2.ToWorld(new Point2(baseX, ys[i]), radians),
                    Transform2.ToWorld(new Point2(baseX, ys[i + 1]), radians),
                    Transform2.ToWorld(new Point2(textX, (ys[i] + ys[i + 1]) * 0.5), radians),
                    radians + Math.PI * 0.5,
                    target));
            }
        }

        private static void AddHorizontalChain(
            ICollection<PreviewDimension> output,
            IEnumerable<double> values,
            DimensionPosition position,
            DimensionTarget target,
            double radians,
            Box2 domain,
            double distance,
            double spacing,
            IDictionary<string, int> stack,
            double? sourceBaseCoordinate = null)
        {
            var xs = values.Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
            if (xs.Count < 2) return;

            double baseY;
            double textY;
            if (position == DimensionPosition.Auto)
            {
                var index = GetAndIncrement(stack, "C-H");
                baseY = (domain.MinY + domain.MaxY) * 0.5;
                textY = baseY + index * spacing;
            }
            else if (position == DimensionPosition.Top || position == DimensionPosition.Left)
            {
                var index = GetAndIncrement(stack, "T");
                baseY = domain.MaxY;
                textY = domain.MaxY + distance + index * spacing;
            }
            else
            {
                var index = GetAndIncrement(stack, "B");
                baseY = domain.MinY;
                textY = domain.MinY - distance - index * spacing;
            }
            if (sourceBaseCoordinate.HasValue) baseY = sourceBaseCoordinate.Value;

            for (var i = 0; i + 1 < xs.Count; i++)
            {
                if (Math.Abs(xs[i + 1] - xs[i]) <= 1.0) continue;
                output.Add(new PreviewDimension(
                    Transform2.ToWorld(new Point2(xs[i], baseY), radians),
                    Transform2.ToWorld(new Point2(xs[i + 1], baseY), radians),
                    Transform2.ToWorld(new Point2((xs[i] + xs[i + 1]) * 0.5, textY), radians),
                    radians,
                    target));
            }
        }

        private static int GetAndIncrement(IDictionary<string, int> stack, string key)
        {
            int value;
            if (!stack.TryGetValue(key, out value)) value = 0;
            stack[key] = value + 1;
            return value;
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 180.0;
            return angle < 0.0 ? angle + 180.0 : angle;
        }

        private sealed class DoubleToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) <= 0.001;
            public int GetHashCode(double obj) => Math.Round(obj, 3).GetHashCode();
        }
    }
}
