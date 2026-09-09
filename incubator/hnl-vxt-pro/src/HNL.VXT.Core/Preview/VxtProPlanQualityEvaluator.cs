using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Scores an already-built Pro plan without changing geometry. The score uses the same
    /// local-frame obstacle/clearance convention as the Pro solvers so QA never reports a
    /// collision against a different clearance geometry than the one used to place XC/XP/Ty.
    /// </summary>
    public static class VxtProPlanQualityEvaluator
    {
        private const double Eps = 1e-8;
        private const double CollisionTolerance = 0.1;

        public static VxtPlanQuality Evaluate(
            VxtPreviewPlan plan,
            VxtSettings settings,
            VxtLayoutContext context,
            double selectedDirectionDegrees,
            int autoDirectionCandidateCount = 1)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            context = context ?? new VxtLayoutContext();

            var mainLength = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Sum(LineLength);
            var furringLength = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Furring)
                .Sum(LineLength);

            var radians = Normalize180(selectedDirectionDegrees) * Math.PI / 180.0;
            var mainObstacles = TransformAndExpand(
                context.GeneralObstacles.Concat(context.MainObstacles), radians, settings.ClearanceDistance);
            var furringObstacles = TransformAndExpand(
                context.GeneralObstacles.Concat(context.FurringObstacles), radians, settings.ClearanceDistance);

            var mainCollisions = 0;
            var furringCollisions = 0;
            var hangerCollisions = 0;
            if (settings.UseAvoidance)
            {
                foreach (var line in plan.Lines)
                {
                    if (line.Kind != PreviewLineKind.Main && line.Kind != PreviewLineKind.Furring) continue;
                    var a = Transform2.ToLocal(line.A, radians);
                    var b = Transform2.ToLocal(line.B, radians);
                    if (line.Kind == PreviewLineKind.Main && mainObstacles.Any(box => SegmentIntersectsBoxInterior(a, b, box)))
                        mainCollisions++;
                    else if (line.Kind == PreviewLineKind.Furring && furringObstacles.Any(box => SegmentIntersectsBoxInterior(a, b, box)))
                        furringCollisions++;
                }

                foreach (var point in plan.HangerPoints)
                {
                    var local = Transform2.ToLocal(point, radians);
                    if (mainObstacles.Any(box => ContainsInterior(box, local))) hangerCollisions++;
                }
            }
            var collisions = mainCollisions + furringCollisions + hangerCollisions;

            var hard = 0;
            if (settings.DrawMain && plan.MainSegmentCount == 0) hard++;
            if (settings.DrawFurring && plan.FurringSegmentCount == 0) hard++;
            if (settings.DrawHangers && plan.MainSegmentCount > 0 && plan.HangerCount == 0) hard++;

            // Equivalent material index is deliberately unitless. It is NOT a bill of quantities.
            // It only gives the direction optimizer a deterministic way to compare candidates.
            var materialIndex = mainLength
                              + furringLength * 0.35
                              + plan.HangerCount * 180.0
                              + plan.DimensionSegmentCount * 12.0;

            var modeWeight = settings.OptimizationMode == VxtOptimizationMode.ProEconomy ? 1.00
                           : settings.OptimizationMode == VxtOptimizationMode.ProConservative ? 0.65
                           : 0.82;

            var sortScore = hard * 1_000_000_000_000.0
                          + collisions * 1_000_000_000.0
                          + materialIndex * modeWeight
                          + (plan.MainSegmentCount + plan.FurringSegmentCount) * 20.0;

            var qualityScore = Math.Max(0, 100 - hard * 40 - Math.Min(60, collisions * 15));

            return new VxtPlanQuality
            {
                QualityScore100 = qualityScore,
                HardViolationCount = hard,
                CollisionCount = collisions,
                MainCollisionCount = mainCollisions,
                FurringCollisionCount = furringCollisions,
                HangerCollisionCount = hangerCollisions,
                AutoDirectionCandidateCount = Math.Max(1, autoDirectionCandidateCount),
                SelectedDirectionDegrees = Normalize180(selectedDirectionDegrees),
                MainLength = mainLength,
                FurringLength = furringLength,
                MaterialIndex = materialIndex,
                SortScore = sortScore
            };
        }

        public static void AttachCompactPreviewLabel(Boundary2 boundary, VxtPreviewPlan plan)
        {
            if (boundary == null || plan?.Quality == null) return;
            var q = plan.Quality;
            var bounds = boundary.GetBounds();
            var position = new Point2(bounds.Min.X, bounds.Max.Y + 120.0);
            var text = "HNL Pro Q" + q.QualityScore100.ToString(CultureInfo.InvariantCulture) +
                       " | H\u01B0\u1EDBng " + q.SelectedDirectionDegrees.ToString("0.#", CultureInfo.InvariantCulture) + "\u00B0" +
                       " | VT " + (q.MaterialIndex / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) +
                       " | VC " + q.CollisionCount.ToString(CultureInfo.InvariantCulture);
            if (q.AutoDirectionCandidateCount > 1)
                text += " | " + q.AutoDirectionCandidateCount.ToString(CultureInfo.InvariantCulture) + " PA";
            plan.Texts.Add(new PreviewText(position, text, PreviewLineKind.Direction));
        }

        public static VxtPlanQuality Aggregate(IEnumerable<VxtPlanQuality> qualities)
        {
            var list = (qualities ?? Enumerable.Empty<VxtPlanQuality>()).Where(x => x != null).ToList();
            if (list.Count == 0) return null;
            return new VxtPlanQuality
            {
                QualityScore100 = list.Min(x => x.QualityScore100),
                HardViolationCount = list.Sum(x => x.HardViolationCount),
                CollisionCount = list.Sum(x => x.CollisionCount),
                MainCollisionCount = list.Sum(x => x.MainCollisionCount),
                FurringCollisionCount = list.Sum(x => x.FurringCollisionCount),
                HangerCollisionCount = list.Sum(x => x.HangerCollisionCount),
                AutoDirectionCandidateCount = list.Sum(x => x.AutoDirectionCandidateCount),
                SelectedDirectionDegrees = list.Count == 1 ? list[0].SelectedDirectionDegrees : 0.0,
                MainLength = list.Sum(x => x.MainLength),
                FurringLength = list.Sum(x => x.FurringLength),
                MaterialIndex = list.Sum(x => x.MaterialIndex),
                SortScore = list.Sum(x => x.SortScore),
                BoundaryCount = list.Sum(x => Math.Max(1, x.BoundaryCount)),
                DistinctDirectionCount = list.Count == 1 ? list[0].DistinctDirectionCount : Math.Max(1, list.Sum(x => Math.Max(1, x.DistinctDirectionCount))),
                AlignmentScore100 = list.Min(x => x.AlignmentScore100),
                UsesSharedDirection = list.All(x => x.UsesSharedDirection)
            };
        }

        private static List<Box2> TransformAndExpand(
            IEnumerable<Box2> boxes,
            double radians,
            double clearance)
        {
            var result = new List<Box2>();
            foreach (var box in boxes ?? Enumerable.Empty<Box2>())
                result.Add(TransformBox(box, radians).Expand(Math.Max(0.0, clearance)));
            return result;
        }

        private static Box2 TransformBox(Box2 box, double radians)
        {
            var points = new[]
            {
                new Point2(box.MinX, box.MinY), new Point2(box.MaxX, box.MinY),
                new Point2(box.MaxX, box.MaxY), new Point2(box.MinX, box.MaxY)
            }.Select(point => Transform2.ToLocal(point, radians));
            return Box2.FromPoints(points);
        }

        private static double LineLength(PreviewLine line)
        {
            var dx = line.B.X - line.A.X;
            var dy = line.B.Y - line.A.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool ContainsInterior(Box2 box, Point2 point)
            => point.X > box.MinX + CollisionTolerance && point.X < box.MaxX - CollisionTolerance &&
               point.Y > box.MinY + CollisionTolerance && point.Y < box.MaxY - CollisionTolerance;

        private static bool SegmentIntersectsBoxInterior(Point2 a, Point2 b, Box2 box)
        {
            var minX = box.MinX + CollisionTolerance;
            var minY = box.MinY + CollisionTolerance;
            var maxX = box.MaxX - CollisionTolerance;
            var maxY = box.MaxY - CollisionTolerance;
            if (maxX <= minX + Eps || maxY <= minY + Eps) return false;
            var inner = new Box2(minX, minY, maxX, maxY);
            if (inner.Contains(a) || inner.Contains(b)) return true;

            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var t0 = 0.0;
            var t1 = 1.0;

            return Clip(-dx, a.X - inner.MinX, ref t0, ref t1) &&
                   Clip( dx, inner.MaxX - a.X, ref t0, ref t1) &&
                   Clip(-dy, a.Y - inner.MinY, ref t0, ref t1) &&
                   Clip( dy, inner.MaxY - a.Y, ref t0, ref t1) &&
                   t1 >= t0 - Eps;
        }

        private static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) <= Eps) return q >= -Eps;
            var r = q / p;
            if (p < 0.0)
            {
                if (r > t1 + Eps) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0 - Eps) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        private static double Normalize180(double degrees)
        {
            degrees %= 180.0;
            if (degrees < 0.0) degrees += 180.0;
            return degrees;
        }
    }
}
