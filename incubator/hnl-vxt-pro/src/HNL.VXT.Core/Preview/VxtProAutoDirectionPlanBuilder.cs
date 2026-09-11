using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Pro-only Auto direction search. Legacy Auto remains untouched.
    /// Candidate angles come from dominant polygon edges, their perpendiculars, and 0/90.
    /// Each candidate is solved by the normal Pro builder and scored after concave post-process.
    ///
    /// Pro optimization must not silently rotate the construction direction just to improve
    /// material score. When legal/clear candidates exist, Auto stays in the same orientation
    /// family as the natural polygon axis implied by the legacy Shadowline rule. A perpendicular
    /// family may still win when it is required to eliminate a hard violation or collision.
    /// </summary>
    public static class VxtProAutoDirectionPlanBuilder
    {
        private const double Eps = 1e-8;
        private const double AngleTolerance = 1.5;
        private const double OrientationFamilyLimit = 45.0;
        private const int MaxCandidates = 16;

        private sealed class Candidate
        {
            public double Angle;
            public double AxisMisalignment;
            public VxtPreviewPlan Plan;
            public VxtPlanQuality Quality;
        }

        private sealed class WeightedAxis
        {
            public double Angle;
            public double Weight;
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
            var preferredAngle = ResolvePreferredAutoAngle(boundary, settings.AutoShadowline, legacyAngle);
            var angles = BuildCandidateAngles(boundary, legacyAngle);
            var boundaryAxes = BuildBoundaryAxes(boundary);
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
                    candidates.Add(new Candidate
                    {
                        Angle = angle,
                        AxisMisalignment = boundaryAxes.Count == 0
                            ? 0.0
                            : boundaryAxes.Min(x => AngularDistance180(x, angle)),
                        Plan = plan,
                        Quality = quality
                    });
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

            // Safety always wins first. After hard violations/collisions and real-axis alignment
            // are equal, keep the natural construction orientation family before comparing
            // material score. This prevents Economy/Balanced/Conservative from flipping XC/XP
            // by 90 degrees merely because the perpendicular candidate is slightly cheaper.
            var best = candidates
                .OrderBy(x => x.Quality.HardViolationCount)
                .ThenBy(x => x.Quality.CollisionCount)
                .ThenBy(x => x.AxisMisalignment > AngleTolerance ? 1 : 0)
                .ThenBy(x => OrientationFamilyPenalty(x.Angle, preferredAngle))
                .ThenBy(x => x.Quality.SortScore)
                .ThenBy(x => AngularDistance180(x.Angle, preferredAngle))
                .ThenBy(x => x.AxisMisalignment)
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

            // Always include certified legacy Auto and global orthogonal directions as fallback.
            weighted.Add(Tuple.Create(Normalize180(legacyAngle), 0.5));
            weighted.Add(Tuple.Create(0.0, 0.25));
            weighted.Add(Tuple.Create(90.0, 0.25));

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

        public static double ResolvePreferredAutoAngle(Boundary2 boundary, bool shadowline, double legacyAngle)
        {
            if (boundary == null) return Normalize180(legacyAngle);

            // Group parallel polygon edges and use accumulated real edge length rather than
            // axis-aligned bounding-box size. This keeps a rotated ceiling aligned to its real
            // construction axis while preserving the legacy long-side/short-side Shadowline rule.
            var axes = new List<WeightedAxis>();
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
                var axis = axes.FirstOrDefault(x => AngularDistance180(x.Angle, angle) <= AngleTolerance);
                if (axis == null)
                    axes.Add(new WeightedAxis { Angle = angle, Weight = length });
                else
                    axis.Weight += length;
            }

            if (axes.Count == 0) return Normalize180(legacyAngle);
            var dominant = axes
                .OrderByDescending(x => x.Weight)
                .ThenBy(x => AngularDistance180(x.Angle, legacyAngle))
                .First()
                .Angle;

            return shadowline ? Normalize180(dominant) : Normalize180(dominant + 90.0);
        }

        private static int OrientationFamilyPenalty(double angle, double preferredAngle)
            => AngularDistance180(angle, preferredAngle) > OrientationFamilyLimit + AngleTolerance ? 1 : 0;

        private static List<double> BuildBoundaryAxes(Boundary2 boundary)
        {
            var result = new List<double>();
            var vertices = boundary.Vertices;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i];
                var b = vertices[(i + 1) % vertices.Count];
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= Eps) continue;
                var angle = Normalize180(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                if (!result.Any(x => AngularDistance180(x, angle) <= AngleTolerance)) result.Add(angle);
                var perpendicular = Normalize180(angle + 90.0);
                if (!result.Any(x => AngularDistance180(x, perpendicular) <= AngleTolerance)) result.Add(perpendicular);
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
