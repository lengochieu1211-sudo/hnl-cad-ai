using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    public sealed class VxtPreviewPlanBuilder
    {
        public VxtPreviewPlan Build(Boundary2 boundary, VxtSettings settings)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.IsValid(out var error)) throw new ArgumentException(error, nameof(settings));

            var plan = new VxtPreviewPlan();
            var radians = settings.DirectionDegrees * Math.PI / 180.0;
            var world = boundary.Vertices;
            var local = world.Select(p => Transform2.ToLocal(p, radians)).ToList();

            AddBoundary(plan, world);

            var minX = local.Min(p => p.X);
            var maxX = local.Max(p => p.X);
            var minY = local.Min(p => p.Y);
            var maxY = local.Max(p => p.Y);

            var mainLocalSegments = new List<Segment2>();
            var furringLocalSegments = new List<Segment2>();

            if (settings.DrawMain)
            {
                foreach (var x in SmartCenteredPositions(minX, maxX, settings.MainMinSpacing, settings.MainMaxSpacing))
                {
                    foreach (var seg in PolygonScanline.ClipVertical(local, x))
                    {
                        mainLocalSegments.Add(seg);
                        AddWorldLine(plan, seg, radians, PreviewLineKind.Main);
                        plan.MainSegmentCount++;
                    }
                }
            }

            if (settings.DrawFurring)
            {
                foreach (var y in FixedCenteredPositions(minY, maxY, settings.FurringSpacing))
                {
                    foreach (var seg in PolygonScanline.ClipHorizontal(local, y))
                    {
                        furringLocalSegments.Add(seg);
                        AddWorldLine(plan, seg, radians, PreviewLineKind.Furring);
                        plan.FurringSegmentCount++;
                    }
                }
            }

            var hangerLocalPoints = new List<Point2>();
            if (settings.DrawHangers && settings.DrawMain)
            {
                foreach (var mainSeg in mainLocalSegments)
                {
                    var length = mainSeg.Length;
                    if (length < 1e-6) continue;

                    foreach (var distance in SmartCenteredDistances(length, settings.HangerMinSpacing, settings.HangerMaxSpacing))
                    {
                        var t = distance / length;
                        var p = new Point2(
                            mainSeg.A.X + (mainSeg.B.X - mainSeg.A.X) * t,
                            mainSeg.A.Y + (mainSeg.B.Y - mainSeg.A.Y) * t
                        );
                        hangerLocalPoints.Add(p);
                        AddHangerMark(plan, p, radians, Math.Max(20.0, Math.Min(60.0, settings.FurringSpacing * 0.08)));
                        plan.HangerCount++;
                    }
                }
            }

            AddDimensions(
                plan, settings, radians,
                minX, maxX, minY, maxY,
                mainLocalSegments, furringLocalSegments, hangerLocalPoints);

            AddDirectionIndicator(plan, radians, minX, minY, maxX, maxY);
            return plan;
        }

        private static void AddBoundary(VxtPreviewPlan plan, IReadOnlyList<Point2> polygon)
        {
            for (var i = 0; i < polygon.Count; i++)
                plan.Lines.Add(new PreviewLine(polygon[i], polygon[(i + 1) % polygon.Count], PreviewLineKind.Boundary));
        }

        private static void AddWorldLine(VxtPreviewPlan plan, Segment2 local, double radians, PreviewLineKind kind)
        {
            plan.Lines.Add(new PreviewLine(
                Transform2.ToWorld(local.A, radians),
                Transform2.ToWorld(local.B, radians),
                kind));
        }

        private static IReadOnlyList<double> SmartCenteredPositions(double min, double max, double minSpacing, double maxSpacing)
        {
            var length = max - min;
            if (length <= 1e-6) return Array.Empty<double>();

            var internalCount = Math.Max(1, (int)Math.Ceiling(length / maxSpacing) - 1);
            var spacing = length / (internalCount + 1);

            if (spacing < minSpacing && internalCount > 1)
            {
                internalCount = Math.Max(1, (int)Math.Floor(length / minSpacing) - 1);
                spacing = length / (internalCount + 1);
            }

            var result = new List<double>();
            for (var i = 1; i <= internalCount; i++)
                result.Add(min + spacing * i);
            return result;
        }

        private static IReadOnlyList<double> FixedCenteredPositions(double min, double max, double spacing)
        {
            var length = max - min;
            if (length <= 1e-6 || spacing <= 0) return Array.Empty<double>();

            var count = Math.Max(1, (int)Math.Floor(length / spacing));
            var used = (count - 1) * spacing;
            var start = min + (length - used) / 2.0;
            var result = new List<double>();

            for (var i = 0; i < count; i++)
            {
                var p = start + i * spacing;
                if (p > min + 1e-6 && p < max - 1e-6) result.Add(p);
            }

            return result;
        }

        private static IReadOnlyList<double> SmartCenteredDistances(double length, double minSpacing, double maxSpacing)
        {
            if (length <= 1e-6) return Array.Empty<double>();
            var count = Math.Max(1, (int)Math.Ceiling(length / maxSpacing) - 1);
            var spacing = length / (count + 1);
            if (spacing < minSpacing && count > 1)
            {
                count = Math.Max(1, (int)Math.Floor(length / minSpacing) - 1);
                spacing = length / (count + 1);
            }

            var result = new List<double>();
            for (var i = 1; i <= count; i++) result.Add(spacing * i);
            return result;
        }

        private static void AddHangerMark(VxtPreviewPlan plan, Point2 pLocal, double radians, double halfSize)
        {
            var a1 = new Point2(pLocal.X - halfSize, pLocal.Y);
            var b1 = new Point2(pLocal.X + halfSize, pLocal.Y);
            var a2 = new Point2(pLocal.X, pLocal.Y - halfSize);
            var b2 = new Point2(pLocal.X, pLocal.Y + halfSize);
            AddWorldLine(plan, new Segment2(a1, b1), radians, PreviewLineKind.Hanger);
            AddWorldLine(plan, new Segment2(a2, b2), radians, PreviewLineKind.Hanger);
        }

        private static void AddDimensions(
            VxtPreviewPlan plan,
            VxtSettings s,
            double radians,
            double minX, double maxX, double minY, double maxY,
            IReadOnlyList<Segment2> mainSegments,
            IReadOnlyList<Segment2> furringSegments,
            IReadOnlyList<Point2> hangerPoints)
        {
            if (!s.AutoDimension) return;

            var mainXs = mainSegments.Select(seg => seg.A.X).Distinct(new DoubleToleranceComparer()).OrderBy(x => x).ToList();
            var furringYs = furringSegments.Select(seg => seg.A.Y).Distinct(new DoubleToleranceComparer()).OrderBy(y => y).ToList();

            if (s.DimMain && mainXs.Count >= 2)
            {
                var pos = Resolve(s.MainDimPosition, DimensionTarget.Main);
                AddAxisDimension(plan, mainXs, true, pos, s.DimensionDistance, 0, minX, maxX, minY, maxY, radians, "XC");
            }

            if (s.DimFurring && furringYs.Count >= 2)
            {
                var pos = Resolve(s.FurringDimPosition, DimensionTarget.Furring);
                AddAxisDimension(plan, furringYs, false, pos, s.DimensionDistance, 0, minX, maxX, minY, maxY, radians, "XP");
            }

            if (s.DimHanger && hangerPoints.Count >= 2)
            {
                var grouped = hangerPoints
                    .GroupBy(p => Math.Round(p.X, 4))
                    .Select(g => g.OrderBy(p => p.Y).ToList())
                    .FirstOrDefault(g => g.Count >= 2);

                if (grouped != null)
                {
                    var ys = grouped.Select(p => p.Y).ToList();
                    var pos = Resolve(s.HangerDimPosition, DimensionTarget.Hanger);
                    AddAxisDimension(plan, ys, false, pos, s.DimensionDistance + s.DimensionSpacing, 1,
                        minX, maxX, minY, maxY, radians, "TY");
                }
            }
        }

        private static DimensionPosition Resolve(DimensionPosition requested, DimensionTarget target)
        {
            if (requested != DimensionPosition.Auto) return requested;
            switch (target)
            {
                case DimensionTarget.Main: return DimensionPosition.Top;
                case DimensionTarget.Furring: return DimensionPosition.Left;
                default: return DimensionPosition.Right;
            }
        }

        private static void AddAxisDimension(
            VxtPreviewPlan plan,
            IReadOnlyList<double> values,
            bool valuesAreX,
            DimensionPosition position,
            double distance,
            int stackIndex,
            double minX, double maxX, double minY, double maxY,
            double radians,
            string prefix)
        {
            if (values.Count < 2) return;

            var extra = stackIndex * 0.0;
            if (valuesAreX)
            {
                var y = position == DimensionPosition.Bottom ? minY - distance - extra : maxY + distance + extra;
                for (var i = 0; i + 1 < values.Count; i++)
                {
                    var a = new Point2(values[i], y);
                    var b = new Point2(values[i + 1], y);
                    AddWorldLine(plan, new Segment2(a, b), radians, PreviewLineKind.Dimension);
                    AddWorldLine(plan, new Segment2(new Point2(values[i], y - 80), new Point2(values[i], y + 80)), radians, PreviewLineKind.DimensionExtension);
                    if (i == values.Count - 2)
                        AddWorldLine(plan, new Segment2(new Point2(values[i + 1], y - 80), new Point2(values[i + 1], y + 80)), radians, PreviewLineKind.DimensionExtension);

                    var text = (values[i + 1] - values[i]).ToString("0", CultureInfo.InvariantCulture);
                    var mid = new Point2((values[i] + values[i + 1]) / 2.0, y + 70);
                    plan.Texts.Add(new PreviewText(Transform2.ToWorld(mid, radians), $"{prefix} {text}", PreviewLineKind.Dimension, radians));
                    plan.DimensionSegmentCount++;
                }
            }
            else
            {
                var x = position == DimensionPosition.Right ? maxX + distance + extra : minX - distance - extra;
                for (var i = 0; i + 1 < values.Count; i++)
                {
                    var a = new Point2(x, values[i]);
                    var b = new Point2(x, values[i + 1]);
                    AddWorldLine(plan, new Segment2(a, b), radians, PreviewLineKind.Dimension);
                    AddWorldLine(plan, new Segment2(new Point2(x - 80, values[i]), new Point2(x + 80, values[i])), radians, PreviewLineKind.DimensionExtension);
                    if (i == values.Count - 2)
                        AddWorldLine(plan, new Segment2(new Point2(x - 80, values[i + 1]), new Point2(x + 80, values[i + 1])), radians, PreviewLineKind.DimensionExtension);

                    var text = (values[i + 1] - values[i]).ToString("0", CultureInfo.InvariantCulture);
                    var mid = new Point2(x + 70, (values[i] + values[i + 1]) / 2.0);
                    plan.Texts.Add(new PreviewText(Transform2.ToWorld(mid, radians), $"{prefix} {text}", PreviewLineKind.Dimension, radians + Math.PI / 2.0));
                    plan.DimensionSegmentCount++;
                }
            }
        }

        private static void AddDirectionIndicator(VxtPreviewPlan plan, double radians, double minX, double minY, double maxX, double maxY)
        {
            var length = Math.Max(maxX - minX, maxY - minY);
            if (length < 1e-6) return;

            var startLocal = new Point2(minX, minY - length * 0.08);
            var endLocal = new Point2(minX + length * 0.15, minY - length * 0.08);
            AddWorldLine(plan, new Segment2(startLocal, endLocal), radians, PreviewLineKind.Direction);
            plan.Texts.Add(new PreviewText(
                Transform2.ToWorld(new Point2(minX, minY - length * 0.11), radians),
                "Hướng XC",
                PreviewLineKind.Direction,
                radians));
        }

        private sealed class DoubleToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-4;
            public int GetHashCode(double obj) => Math.Round(obj, 4).GetHashCode();
        }
    }
}
