using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Scores an already-built Pro plan without changing geometry. The score is intentionally
    /// conservative: hard failures and MEP collisions dominate; material index is used mainly
    /// to rank Auto-direction candidates inside the same ceiling boundary.
    /// </summary>
    public static class VxtProPlanQualityEvaluator
    {
        private const double Eps = 1e-8;

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

            var mainObstacles = Expand(context.GeneralObstacles.Concat(context.MainObstacles), settings.ClearanceDistance);
            var furringObstacles = Expand(context.GeneralObstacles.Concat(context.FurringObstacles), settings.ClearanceDistance);

            var collisions = 0;
            if (settings.UseAvoidance)
            {
                foreach (var line in plan.Lines)
                {
                    if (line.Kind == PreviewLineKind.Main && mainObstacles.Any(b => SegmentIntersectsBox(line.A, line.B, b)))
                        collisions++;
                    else if (line.Kind == PreviewLineKind.Furring && furringObstacles.Any(b => SegmentIntersectsBox(line.A, line.B, b)))
                        collisions++;
                }

                foreach (var point in plan.HangerPoints)
                    if (mainObstacles.Any(b => b.Contains(point, 0.1))) collisions++;
            }

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
                AutoDirectionCandidateCount = list.Sum(x => x.AutoDirectionCandidateCount),
                SelectedDirectionDegrees = list.Count == 1 ? list[0].SelectedDirectionDegrees : 0.0,
                MainLength = list.Sum(x => x.MainLength),
                FurringLength = list.Sum(x => x.FurringLength),
                MaterialIndex = list.Sum(x => x.MaterialIndex),
                SortScore = list.Sum(x => x.SortScore)
            };
        }

        private static List<Box2> Expand(IEnumerable<Box2> boxes, double clearance)
            => (boxes ?? Enumerable.Empty<Box2>()).Select(x => x.Expand(Math.Max(0.0, clearance))).ToList();

        private static double LineLength(PreviewLine line)
        {
            var dx = line.B.X - line.A.X;
            var dy = line.B.Y - line.A.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool SegmentIntersectsBox(Point2 a, Point2 b, Box2 box)
        {
            if (box.Contains(a, 0.1) || box.Contains(b, 0.1)) return true;

            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var t0 = 0.0;
            var t1 = 1.0;

            return Clip(-dx, a.X - box.MinX, ref t0, ref t1) &&
                   Clip( dx, box.MaxX - a.X, ref t0, ref t1) &&
                   Clip(-dy, a.Y - box.MinY, ref t0, ref t1) &&
                   Clip( dy, box.MaxY - a.Y, ref t0, ref t1) &&
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
