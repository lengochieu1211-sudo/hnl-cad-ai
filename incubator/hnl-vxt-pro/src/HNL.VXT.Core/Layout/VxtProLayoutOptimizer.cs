using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// HNL VXT Pro multi-candidate 1D optimizer.
    ///
    /// IMPORTANT:
    /// - SmartLayout1D remains the certified Legacy V6.7.x parity solver.
    /// - This class never changes SmartLayout1D behavior.
    /// - Pro modes enumerate legal Legacy-derived candidates, then score them.
    /// - Legacy mode returns the original SmartLayout1D result unchanged.
    /// </summary>
    public static class VxtProLayoutOptimizer
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;

        public sealed class QualityReport
        {
            internal QualityReport(
                int hardViolationCount,
                int collisionCount,
                int pointCount,
                double minGap,
                double maxGap,
                double averageGap,
                double spacingSpread,
                double startEdge,
                double endEdge,
                double edgeImbalance,
                double score)
            {
                HardViolationCount = hardViolationCount;
                CollisionCount = collisionCount;
                PointCount = pointCount;
                MinGap = minGap;
                MaxGap = maxGap;
                AverageGap = averageGap;
                SpacingSpread = spacingSpread;
                StartEdge = startEdge;
                EndEdge = endEdge;
                EdgeImbalance = edgeImbalance;
                Score = score;
            }

            public int HardViolationCount { get; }
            public int CollisionCount { get; }
            public int PointCount { get; }
            public double MinGap { get; }
            public double MaxGap { get; }
            public double AverageGap { get; }
            public double SpacingSpread { get; }
            public double StartEdge { get; }
            public double EndEdge { get; }
            public double EdgeImbalance { get; }
            public double Score { get; }
            public bool IsValid => HardViolationCount == 0;
            public bool IsClear => CollisionCount == 0;
            public int QualityScore100
            {
                get
                {
                    if (!IsValid) return 0;
                    var penalty = Math.Min(70.0, CollisionCount * 20.0)
                                + Math.Min(15.0, EdgeImbalance / 20.0)
                                + Math.Min(15.0, SpacingSpread / 20.0);
                    return Math.Max(0, Math.Min(100, (int)Math.Round(100.0 - penalty)));
                }
            }
        }

        public sealed class OptimizedResult
        {
            internal OptimizedResult(SmartLayout1D.Result layout, QualityReport quality, VxtOptimizationMode mode)
            {
                Layout = layout;
                Quality = quality;
                Mode = mode;
            }

            public SmartLayout1D.Result Layout { get; }
            public QualityReport Quality { get; }
            public VxtOptimizationMode Mode { get; }
        }

        private sealed class Candidate
        {
            public SmartLayout1D.Result Layout;
            public QualityReport Quality;
        }

        public static OptimizedResult Calculate(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            MainLayoutMode layoutMode,
            VxtOptimizationMode optimizationMode,
            IEnumerable<Tuple<double, double>> obstacleIntervals = null,
            double edgeTolerance = 25.0)
        {
            var mode = layoutMode == MainLayoutMode.Auto
                ? MainLayoutMode.BalancedTwoEnds
                : layoutMode;

            var obstacles = NormalizeObstacles(obstacleIntervals, length);

            if (optimizationMode == VxtOptimizationMode.Legacy)
            {
                var legacy = SmartLayout1D.Calculate(
                    length, maxSpacing, minSpacing, maxEdge, minEdge, increment, mode);
                return legacy == null
                    ? new OptimizedResult(null, null, optimizationMode)
                    : new OptimizedResult(
                        legacy,
                        Evaluate(legacy, length, minSpacing, maxSpacing, minEdge, maxEdge, obstacles, optimizationMode, edgeTolerance),
                        optimizationMode);
            }

            var candidates = new List<Candidate>();
            var allowEdgeShift = mode != MainLayoutMode.OneSide;
            foreach (var candidateMax in CandidateMaxSpacings(maxSpacing, minSpacing, increment, optimizationMode))
            {
                AddLayoutFamily(
                    candidates,
                    SmartLayout1D.Calculate(length, candidateMax, minSpacing, maxEdge, minEdge, increment, mode, reverse: false),
                    length, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                    obstacles, optimizationMode, edgeTolerance, allowEdgeShift);

                // OneSide has directional meaning. Never silently flip the user's requested side.
                if (mode != MainLayoutMode.OneSide)
                {
                    AddLayoutFamily(
                        candidates,
                        SmartLayout1D.Calculate(length, candidateMax, minSpacing, maxEdge, minEdge, increment, mode, reverse: true),
                        length, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                        obstacles, optimizationMode, edgeTolerance, allowEdgeShift);
                }
            }

            if (candidates.Count == 0)
                return new OptimizedResult(null, null, optimizationMode);

            var best = candidates
                .Where(c => c.Quality != null && c.Quality.IsValid)
                .OrderBy(c => c.Quality.Score)
                .ThenBy(c => c.Quality.CollisionCount)
                .ThenBy(c => c.Quality.PointCount)
                .FirstOrDefault();

            if (best == null)
                best = candidates.OrderBy(c => c.Quality?.HardViolationCount ?? int.MaxValue)
                                 .ThenBy(c => c.Quality?.Score ?? double.MaxValue)
                                 .First();

            return new OptimizedResult(best.Layout, best.Quality, optimizationMode);
        }

        public static QualityReport Evaluate(
            SmartLayout1D.Result layout,
            double length,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            VxtOptimizationMode optimizationMode,
            double edgeTolerance = 25.0)
        {
            if (layout == null) return null;

            var obstacles = NormalizeObstacles(obstacleIntervals, length);
            var hard = 0;
            var allowedMaxEdge = maxEdge + Math.Max(0.0, edgeTolerance);

            if (layout.StartOffset < minEdge - Tol || layout.StartOffset > allowedMaxEdge + Tol) hard++;
            if (layout.EndOffset < minEdge - Tol || layout.EndOffset > allowedMaxEdge + Tol) hard++;

            foreach (var gap in layout.Steps)
                if (gap < minSpacing - Tol || gap > maxSpacing + Tol) hard++;

            var positions = layout.Positions(0.0);
            var collisions = 0;
            foreach (var value in positions)
            {
                if (obstacles.Any(o => value > o.Item1 + Tol && value < o.Item2 - Tol))
                    collisions++;
            }

            var minGap = layout.Steps.Count == 0 ? 0.0 : layout.Steps.Min();
            var maxGap = layout.Steps.Count == 0 ? 0.0 : layout.Steps.Max();
            var averageGap = layout.Steps.Count == 0 ? 0.0 : layout.Steps.Average();
            var spread = maxGap - minGap;
            var edgeImbalance = Math.Abs(layout.StartOffset - layout.EndOffset);

            var reportWithoutScore = new QualityReport(
                hard, collisions, layout.PointCount,
                minGap, maxGap, averageGap, spread,
                layout.StartOffset, layout.EndOffset, edgeImbalance, 0.0);
            var score = CalculateScore(reportWithoutScore, minSpacing, maxSpacing, optimizationMode);

            return new QualityReport(
                hard, collisions, layout.PointCount,
                minGap, maxGap, averageGap, spread,
                layout.StartOffset, layout.EndOffset, edgeImbalance, score);
        }

        private static void AddLayoutFamily(
            IList<Candidate> candidates,
            SmartLayout1D.Result baseLayout,
            double length,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment,
            IReadOnlyList<Tuple<double, double>> obstacles,
            VxtOptimizationMode optimizationMode,
            double edgeTolerance,
            bool allowEdgeShift)
        {
            if (baseLayout == null) return;

            var edgeSum = length - baseLayout.Span;
            var allowedMax = maxEdge + Math.Max(0.0, edgeTolerance);
            var startMin = Math.Max(minEdge, edgeSum - allowedMax);
            var startMax = Math.Min(allowedMax, edgeSum - minEdge);
            if (startMin > startMax + Tol) return;

            var starts = new HashSet<double> { baseLayout.StartOffset };
            if (allowEdgeShift)
            {
                starts.Add(edgeSum / 2.0);
                if (increment > Eps)
                {
                    var first = Math.Ceiling((startMin - Eps) / increment) * increment;
                    for (var value = first; value <= startMax + Eps; value += increment)
                        starts.Add(value);
                }
            }

            foreach (var start in starts.OrderBy(x => x))
            {
                var end = edgeSum - start;
                if (start < startMin - Tol || start > startMax + Tol ||
                    end < minEdge - Tol || end > allowedMax + Tol)
                    continue;

                var layout = new SmartLayout1D.Result(start, baseLayout.Steps.ToArray(), end);
                var quality = Evaluate(
                    layout, length, minSpacing, maxSpacing, minEdge, maxEdge,
                    obstacles, optimizationMode, edgeTolerance);

                if (candidates.Any(c => SameLayout(c.Layout, layout))) continue;
                candidates.Add(new Candidate { Layout = layout, Quality = quality });
            }
        }

        private static IEnumerable<double> CandidateMaxSpacings(
            double maxSpacing,
            double minSpacing,
            double increment,
            VxtOptimizationMode mode)
        {
            yield return maxSpacing;
            if (mode != VxtOptimizationMode.ProConservative || increment <= Eps) yield break;

            // Conservative mode deliberately explores denser legal grids. It never exceeds
            // the user's original Max and never goes below Min.
            var target = Math.Max(minSpacing, maxSpacing * 0.82);
            var value = maxSpacing - increment;
            var emitted = 0;
            while (value >= target - Tol && value >= minSpacing - Tol && emitted < 8)
            {
                yield return value;
                value -= increment;
                emitted++;
            }
        }

        private static double CalculateScore(
            QualityReport q,
            double minSpacing,
            double maxSpacing,
            VxtOptimizationMode mode)
        {
            if (q == null) return double.MaxValue;

            var score = q.HardViolationCount * 1_000_000_000.0
                      + q.CollisionCount * 10_000_000.0;

            switch (mode)
            {
                case VxtOptimizationMode.ProEconomy:
                    score += q.PointCount * 100_000.0;
                    score += Math.Max(0.0, maxSpacing - q.AverageGap) * 50.0;
                    score += q.EdgeImbalance * 5.0;
                    score += q.SpacingSpread * 2.0;
                    break;

                case VxtOptimizationMode.ProConservative:
                    score += q.MaxGap * 10_000.0;
                    score += q.PointCount * 1_000.0;
                    score += q.EdgeImbalance * 5.0;
                    score += q.SpacingSpread * 2.0;
                    break;

                default: // ProBalanced
                    var target = (minSpacing + maxSpacing) * 0.5;
                    score += q.PointCount * 100_000.0;
                    score += q.EdgeImbalance * 100.0;
                    score += q.SpacingSpread * 20.0;
                    score += Math.Abs(q.AverageGap - target) * 2.0;
                    break;
            }

            return score;
        }

        private static IReadOnlyList<Tuple<double, double>> NormalizeObstacles(
            IEnumerable<Tuple<double, double>> intervals,
            double length)
        {
            return (intervals ?? Enumerable.Empty<Tuple<double, double>>())
                .Where(x => x != null)
                .Select(x => Tuple.Create(
                    Math.Max(0.0, Math.Min(x.Item1, x.Item2)),
                    Math.Min(length, Math.Max(x.Item1, x.Item2))))
                .Where(x => x.Item2 > x.Item1 + Eps)
                .OrderBy(x => x.Item1)
                .ToArray();
        }

        private static bool SameLayout(SmartLayout1D.Result a, SmartLayout1D.Result b)
        {
            if (a == null || b == null) return false;
            if (Math.Abs(a.StartOffset - b.StartOffset) > Tol ||
                Math.Abs(a.EndOffset - b.EndOffset) > Tol ||
                a.Steps.Count != b.Steps.Count)
                return false;

            for (var i = 0; i < a.Steps.Count; i++)
                if (Math.Abs(a.Steps[i] - b.Steps[i]) > Tol) return false;
            return true;
        }
    }
}
