using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Pro-only Auto direction search. Legacy Auto remains untouched.
    /// Candidate angles come from dominant polygon edges, their perpendiculars, and 0/90.
    /// Each candidate is solved by the normal Pro builder and scored after concave post-process.
    /// </summary>
    public static class VxtProAutoDirectionPlanBuilder
    {
        private const double Eps = 1e-8;
        private const double AngleTolerance = 1.5;
        private const int MaxCandidates = 16;

        private sealed class Candidate
        {
            public double Angle;
            public VxtPreviewPlan Plan;
            public VxtPlanQuality Quality;
        }

        public static VxtPreviewPlan Build(
            Boundary2 boundary,
            VxtSettings settings,
            VxtLayoutContext context)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            context = context ?? new VxtLayoutContext();

            if (settings.OptimizationMode == VxtOptimizationMode.Legacy ||
                settings.MainDirection != MainDirectionMode.Auto)
                return new VxtProPreviewPlanBuilder().Build(boundary, settings, context);

            var legacyAngle = ResolveLegacyAutoAngle(boundary, settings.AutoShadowline);
            var angles = BuildCandidateAngles(boundary, legacyAngle);
            var candidates = new List<Candidate>();

            foreach (var angle in angles)
            {
                try
                {
                    var candidateSettings = settings.Clone();
                    candidateSettings.MainDirection = MainDirectionMode.TwoPoints;
                    candidateSettings.DirectionDegrees = angle;

                    var plan = new VxtProPreviewPlanBuilder().Build(boundary, candidateSettings, context);
                    VxtConcaveMainPostProcessor.Apply(boundary, candidateSettings, context, plan);
                    var quality = VxtProPlanQualityEvaluator.Evaluate(
                        plan, candidateSettings, context, angle, angles.Count);
                    plan.Quality = quality;
                    candidates.Add(new Candidate { Angle = angle, Plan = plan, Quality = quality });
                }
                catch
                {
                    // One pathological angle must not abort the whole Auto search.
                }
            }

            if (candidates.Count == 0)
            {
                var fallbackSettings = settings.Clone();
                fallbackSettings.MainDirection = MainDirectionMode.TwoPoints;
                fallbackSettings.DirectionDegrees = legacyAngle;
                var fallback = new VxtProPreviewPlanBuilder().Build(boundary, fallbackSettings, context);
                VxtConcaveMainPostProcessor.Apply(boundary, fallbackSettings, context, fallback);
                fallback.Quality = VxtProPlanQualityEvaluator.Evaluate(
                    fallback, fallbackSettings, context, legacyAngle, 1);
                VxtProPlanQualityEvaluator.AttachCompactPreviewLabel(boundary, fallback);
                return fallback;
            }

            var best = candidates
                .OrderBy(x => x.Quality.HardViolationCount)
                .ThenBy(x => x.Quality.CollisionCount)
                .ThenBy(x => x.Quality.SortScore)
                .ThenBy(x => AngularDistance180(x.Angle, legacyAngle))
                .First();

            best.Quality.AutoDirectionCandidateCount = candidates.Count;
            best.Plan.Quality = best.Quality;
            VxtProPlanQualityEvaluator.AttachCompactPreviewLabel(boundary, best.Plan);
            return best.Plan;
        }

        public static IReadOnlyList<double> BuildCandidateAngles(Boundary2 boundary, double legacyAngle)
        {
            var weighted = new List<Tuple<double, double>>();
            var vertices = boundary.Vertices;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= Eps) continue;
                var angle = Normalize180(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                weighted.Add(Tuple.Create(angle, length));
                weighted.Add(Tuple.Create(Normalize180(angle + 90.0), length));
            }

            // Always include certified legacy Auto and global orthogonal directions.
            weighted.Add(Tuple.Create(Normalize180(legacyAngle), double.MaxValue));
            weighted.Add(Tuple.Create(0.0, 1.0));
            weighted.Add(Tuple.Create(90.0, 1.0));

            var result = new List<double>();
            foreach (var item in weighted.OrderByDescending(x => x.Item2))
            {
                var angle = Normalize180(item.Item1);
                if (result.Any(x => AngularDistance180(x, angle) <= AngleTolerance)) continue;
                result.Add(angle);
                if (result.Count >= MaxCandidates) break;
            }

            return result;
        }

        private static double ResolveLegacyAutoAngle(Boundary2 boundary, bool shadowline)
        {
            var bounds = boundary.GetBounds();
            var width = bounds.Max.X - bounds.Min.X;
            var height = bounds.Max.Y - bounds.Min.Y;
            var wide = width > height;
            return shadowline
                ? (wide ? 0.0 : 90.0)
                : (wide ? 90.0 : 0.0);
        }

        private static double AngularDistance180(double a, double b)
        {
            var d = Math.Abs(Normalize180(a) - Normalize180(b));
            return Math.Min(d, 180.0 - d);
        }

        private static double Normalize180(double degrees)
        {
            degrees %= 180.0;
            if (degrees < 0.0) degrees += 180.0;
            return degrees;
        }
    }
}
