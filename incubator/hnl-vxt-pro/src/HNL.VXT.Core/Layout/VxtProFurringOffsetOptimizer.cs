using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Pro optimizer for a fixed XP spacing. Spacing itself is never changed; only the grid
    /// offset is selected. This preserves 1220/3 (or any user-entered spacing) exactly.
    /// </summary>
    public static class VxtProFurringOffsetOptimizer
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;

        public sealed class Result
        {
            internal Result(
                double offset,
                IReadOnlyList<double> positions,
                int collisionCount,
                double startEdge,
                double endEdge,
                double score)
            {
                Offset = offset;
                Positions = positions ?? Array.Empty<double>();
                CollisionCount = collisionCount;
                StartEdge = startEdge;
                EndEdge = endEdge;
                EdgeImbalance = Math.Abs(startEdge - endEdge);
                MaxEdge = Math.Max(startEdge, endEdge);
                Score = score;
            }

            public double Offset { get; }
            public IReadOnlyList<double> Positions { get; }
            public int CollisionCount { get; }
            public double StartEdge { get; }
            public double EndEdge { get; }
            public double EdgeImbalance { get; }
            public double MaxEdge { get; }
            public double Score { get; }
            public bool IsClear => CollisionCount == 0;
        }

        public static Result Calculate(
            double minLimit,
            double maxLimit,
            double spacing,
            double preferredOffset,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            VxtOptimizationMode mode,
            double sampleStep = 10.0)
        {
            var length = maxLimit - minLimit;
            if (length <= Eps || spacing <= Eps) return null;

            preferredOffset = NormalizeOffset(preferredOffset, spacing);
            var obstacles = NormalizeObstacles(obstacleIntervals, minLimit, maxLimit);
            var offsets = CandidateOffsets(length, spacing, preferredOffset, sampleStep).ToArray();

            Result best = null;
            foreach (var offset in offsets)
            {
                var positions = BuildPositions(minLimit, maxLimit, spacing, offset);
                if (positions.Count == 0) continue;

                var startEdge = positions[0] - minLimit;
                var endEdge = maxLimit - positions[positions.Count - 1];
                var collisions = positions.Count(x => obstacles.Any(o => x > o.Item1 + Tol && x < o.Item2 - Tol));
                var score = Score(
                    mode,
                    positions.Count,
                    collisions,
                    startEdge,
                    endEdge,
                    CircularDistance(offset, preferredOffset, spacing));

                var candidate = new Result(offset, positions, collisions, startEdge, endEdge, score);
                if (best == null || candidate.Score < best.Score - Eps ||
                    (Math.Abs(candidate.Score - best.Score) <= Eps && candidate.Offset < best.Offset))
                    best = candidate;
            }

            return best;
        }

        private static IEnumerable<double> CandidateOffsets(
            double length,
            double spacing,
            double preferredOffset,
            double sampleStep)
        {
            var values = new HashSet<double>(new ToleranceComparer());
            Add(values, preferredOffset, spacing);
            Add(values, spacing, spacing);

            var remainder = PositiveRemainder(length, spacing);
            if (remainder > Eps)
            {
                // Same-count balanced offset. When preferred=spacing, offsets greater than the
                // remainder keep the legacy member count; their edge sum is remainder+spacing.
                Add(values, (remainder + spacing) * 0.5, spacing);
                // Denser symmetric alternative for Conservative profile.
                Add(values, remainder * 0.5, spacing);
                Add(values, remainder, spacing);
            }
            else
            {
                Add(values, spacing * 0.5, spacing);
            }

            var step = sampleStep > Eps ? sampleStep : Math.Max(1.0, spacing / 40.0);
            for (var offset = step; offset < spacing - Eps; offset += step)
                Add(values, offset, spacing);

            return values.Where(x => x > 2.0 + Eps && x <= spacing + Eps).OrderBy(x => x);
        }

        private static void Add(ISet<double> values, double value, double spacing)
        {
            var normalized = NormalizeOffset(value, spacing);
            if (normalized <= 2.0 + Eps) normalized = spacing;
            values.Add(normalized);
        }

        private static List<double> BuildPositions(double min, double max, double spacing, double offset)
        {
            var result = new List<double>();
            var value = min + offset;
            while (value < max - 2.0 + Eps)
            {
                if (value > min + 2.0) result.Add(value);
                value += spacing;
            }
            return result;
        }

        private static double Score(
            VxtOptimizationMode mode,
            int pointCount,
            int collisions,
            double startEdge,
            double endEdge,
            double preferredMovement)
        {
            var edgeImbalance = Math.Abs(startEdge - endEdge);
            var maxEdge = Math.Max(startEdge, endEdge);
            var score = collisions * 1_000_000_000.0;

            switch (mode)
            {
                case VxtOptimizationMode.ProConservative:
                    score += maxEdge * 100_000.0;
                    score += edgeImbalance * 1_000.0;
                    score += pointCount * 100.0;
                    score += preferredMovement;
                    break;

                case VxtOptimizationMode.ProBalanced:
                    score += pointCount * 1_000_000.0;
                    score += edgeImbalance * 1_000.0;
                    score += maxEdge * 10.0;
                    score += preferredMovement;
                    break;

                default: // Economy
                    score += pointCount * 10_000_000.0;
                    score += edgeImbalance * 100.0;
                    score += maxEdge * 10.0;
                    score += preferredMovement;
                    break;
            }

            return score;
        }

        private static IReadOnlyList<Tuple<double, double>> NormalizeObstacles(
            IEnumerable<Tuple<double, double>> intervals,
            double minLimit,
            double maxLimit)
        {
            return (intervals ?? Enumerable.Empty<Tuple<double, double>>())
                .Where(x => x != null)
                .Select(x => Tuple.Create(
                    Math.Max(minLimit, Math.Min(x.Item1, x.Item2)),
                    Math.Min(maxLimit, Math.Max(x.Item1, x.Item2))))
                .Where(x => x.Item2 > x.Item1 + Eps)
                .ToArray();
        }

        private static double NormalizeOffset(double value, double spacing)
        {
            var result = value % spacing;
            if (result < 0.0) result += spacing;
            if (Math.Abs(result) <= Eps) result = spacing;
            return result;
        }

        private static double PositiveRemainder(double value, double divisor)
        {
            var result = value % divisor;
            if (result < 0.0) result += divisor;
            return result;
        }

        private static double CircularDistance(double a, double b, double period)
        {
            var delta = Math.Abs(a - b);
            return Math.Min(delta, Math.Abs(period - delta));
        }

        private sealed class ToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;
            public int GetHashCode(double obj) => Math.Round(obj, 6).GetHashCode();
        }
    }
}
