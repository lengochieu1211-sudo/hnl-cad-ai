using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Constraint repair for Ty treo after equipment avoidance.
    /// Finds a clear legal hanger chain with a DAG shortest path instead of silently
    /// accepting an empty/invalid row when greedy adjustment cannot satisfy all limits.
    /// </summary>
    public static class VxtProHangerRepair
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;

        public sealed class Result
        {
            internal Result(IReadOnlyList<double> positions, bool repaired, int movedOrAdded, double maxGap)
            {
                Positions = positions ?? Array.Empty<double>();
                Repaired = repaired;
                MovedOrAddedCount = movedOrAdded;
                MaxGap = maxGap;
            }

            public IReadOnlyList<double> Positions { get; }
            public bool Repaired { get; }
            public int MovedOrAddedCount { get; }
            public double MaxGap { get; }
            public bool Success => Positions.Count > 0;
        }

        private sealed class PathState
        {
            public double Cost = double.MaxValue;
            public int Previous = -1;
            public int Count = int.MaxValue;
        }

        public static Result Repair(
            IEnumerable<double> idealPositions,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment,
            VxtOptimizationMode optimizationMode,
            double edgeTolerance = 25.0)
        {
            var ideal = (idealPositions ?? Enumerable.Empty<double>())
                .Where(x => x > minLimit - Tol && x < maxLimit + Tol)
                .Distinct(new ToleranceComparer())
                .OrderBy(x => x)
                .ToArray();
            var obstacles = NormalizeObstacles(obstacleIntervals, minLimit, maxLimit);
            var allowedMaxEdge = maxEdge + Math.Max(0.0, edgeTolerance);

            if (IsValidChain(ideal, obstacles, minLimit, maxLimit, minSpacing, maxSpacing, minEdge, allowedMaxEdge))
                return new Result(ideal, repaired: false, movedOrAdded: 0, MaxInternalGap(ideal));

            // Preserve the legacy greedy answer when it is valid. Pro repair is a fail-safe,
            // not an excuse to churn coordinates that already satisfy every constraint.
            var greedy = SmartLayout1D.AdjustGrid(
                ideal,
                obstacles,
                minLimit,
                maxLimit,
                minSpacing,
                maxSpacing,
                minEdge,
                maxEdge,
                increment);
            if (IsValidChain(greedy, obstacles, minLimit, maxLimit, minSpacing, maxSpacing, minEdge, allowedMaxEdge))
                return new Result(greedy.ToArray(), repaired: true, movedOrAdded: DifferenceCount(ideal, greedy), MaxInternalGap(greedy));

            if (increment <= Eps || maxLimit - minLimit <= Eps)
                return new Result(Array.Empty<double>(), repaired: true, movedOrAdded: ideal.Length, 0.0);

            Result best = null;
            double bestCost = double.MaxValue;
            foreach (var anchor in BuildAnchors(ideal, obstacles, minLimit, minEdge, maxEdge, increment))
            {
                var path = SolveForAnchor(
                    anchor,
                    ideal,
                    obstacles,
                    minLimit,
                    maxLimit,
                    minSpacing,
                    maxSpacing,
                    minEdge,
                    allowedMaxEdge,
                    increment,
                    optimizationMode,
                    out var cost);
                if (path == null || path.Count == 0 || cost >= bestCost) continue;

                bestCost = cost;
                best = new Result(
                    path,
                    repaired: true,
                    movedOrAdded: DifferenceCount(ideal, path),
                    MaxInternalGap(path));
            }

            return best ?? new Result(Array.Empty<double>(), repaired: true, movedOrAdded: ideal.Length, 0.0);
        }

        public static bool IsValidChain(
            IEnumerable<double> positions,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge)
        {
            var values = (positions ?? Enumerable.Empty<double>()).OrderBy(x => x).ToArray();
            if (values.Length == 0) return false;
            var obstacles = NormalizeObstacles(obstacleIntervals, minLimit, maxLimit);

            var startEdge = values[0] - minLimit;
            var endEdge = maxLimit - values[values.Length - 1];
            if (startEdge < minEdge - Tol || startEdge > maxEdge + Tol ||
                endEdge < minEdge - Tol || endEdge > maxEdge + Tol)
                return false;

            for (var i = 0; i < values.Length; i++)
            {
                if (!IsClear(values[i], obstacles)) return false;
                if (i == 0) continue;
                var gap = values[i] - values[i - 1];
                if (gap < minSpacing - Tol || gap > maxSpacing + Tol) return false;
            }
            return true;
        }

        private static IReadOnlyList<double> SolveForAnchor(
            double anchor,
            IReadOnlyList<double> ideal,
            IReadOnlyList<Tuple<double, double>> obstacles,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment,
            VxtOptimizationMode mode,
            out double bestCost)
        {
            var candidates = new List<double>();
            var value = anchor;
            while (value > minLimit + minEdge + Tol) value -= increment;
            while (value < minLimit + minEdge - Tol) value += increment;

            for (; value <= maxLimit - minEdge + Tol; value += increment)
            {
                if (value < minLimit + minEdge - Tol) continue;
                if (value > maxLimit - minEdge + Tol) break;
                if (IsClear(value, obstacles)) candidates.Add(value);
            }

            candidates = candidates.Distinct(new ToleranceComparer()).OrderBy(x => x).ToList();
            if (candidates.Count == 0)
            {
                bestCost = double.MaxValue;
                return null;
            }

            var states = candidates.Select(x => new PathState()).ToArray();
            for (var i = 0; i < candidates.Count; i++)
            {
                var startEdge = candidates[i] - minLimit;
                if (startEdge < minEdge - Tol || startEdge > maxEdge + Tol) continue;
                states[i].Count = 1;
                states[i].Cost = NodeCost(candidates[i], ideal, mode, 1);
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (states[i].Count == int.MaxValue) continue;
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var gap = candidates[j] - candidates[i];
                    if (gap < minSpacing - Tol) continue;
                    if (gap > maxSpacing + Tol) break;

                    var nextCount = states[i].Count + 1;
                    var nextCost = states[i].Cost + NodeCost(candidates[j], ideal, mode, nextCount) + GapCost(gap, minSpacing, maxSpacing, mode);
                    if (nextCost + Eps >= states[j].Cost) continue;
                    states[j].Cost = nextCost;
                    states[j].Count = nextCount;
                    states[j].Previous = i;
                }
            }

            var endIndex = -1;
            bestCost = double.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (states[i].Count == int.MaxValue) continue;
                var endEdge = maxLimit - candidates[i];
                if (endEdge < minEdge - Tol || endEdge > maxEdge + Tol) continue;

                var finalCost = states[i].Cost + Math.Abs(endEdge - minEdge) * 0.05;
                if (finalCost >= bestCost) continue;
                bestCost = finalCost;
                endIndex = i;
            }

            if (endIndex < 0) return null;

            var path = new List<double>();
            var cursor = endIndex;
            while (cursor >= 0)
            {
                path.Add(candidates[cursor]);
                cursor = states[cursor].Previous;
            }
            path.Reverse();
            return path;
        }

        private static double NodeCost(double value, IReadOnlyList<double> ideal, VxtOptimizationMode mode, int count)
        {
            var movement = ideal.Count == 0 ? 0.0 : ideal.Min(x => Math.Abs(x - value));
            switch (mode)
            {
                case VxtOptimizationMode.ProConservative:
                    return 1_000.0 + movement * 0.05;
                case VxtOptimizationMode.ProBalanced:
                    return 100_000.0 + movement * 1.0;
                default: // Economy and any future Pro profile
                    return 1_000_000.0 + movement * 0.25;
            }
        }

        private static double GapCost(double gap, double minSpacing, double maxSpacing, VxtOptimizationMode mode)
        {
            switch (mode)
            {
                case VxtOptimizationMode.ProConservative:
                    return gap * gap * 0.05;
                case VxtOptimizationMode.ProBalanced:
                    var target = (minSpacing + maxSpacing) * 0.5;
                    return Math.Abs(gap - target) * 2.0;
                default:
                    return Math.Max(0.0, maxSpacing - gap) * 0.1;
            }
        }

        private static IEnumerable<double> BuildAnchors(
            IReadOnlyList<double> ideal,
            IReadOnlyList<Tuple<double, double>> obstacles,
            double minLimit,
            double minEdge,
            double maxEdge,
            double increment)
        {
            var anchors = new HashSet<double>(new ToleranceComparer());
            if (ideal.Count > 0) anchors.Add(ideal[0]);
            anchors.Add(minLimit + minEdge);
            anchors.Add(minLimit + maxEdge);
            anchors.Add(minLimit + (minEdge + maxEdge) * 0.5);

            foreach (var obstacle in obstacles)
            {
                anchors.Add(Snap(obstacle.Item1 - increment, minLimit, increment));
                anchors.Add(Snap(obstacle.Item2 + increment, minLimit, increment));
            }
            return anchors.OrderBy(x => x);
        }

        private static double Snap(double value, double origin, double increment)
            => origin + Math.Round((value - origin) / increment) * increment;

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
                .OrderBy(x => x.Item1)
                .ToArray();
        }

        private static bool IsClear(double value, IEnumerable<Tuple<double, double>> obstacles)
            => obstacles.All(x => value <= x.Item1 + Tol || value >= x.Item2 - Tol);

        private static int DifferenceCount(IEnumerable<double> ideal, IEnumerable<double> actual)
        {
            var a = (ideal ?? Enumerable.Empty<double>()).ToArray();
            var b = (actual ?? Enumerable.Empty<double>()).ToArray();
            var matched = b.Count(x => a.Any(y => Math.Abs(x - y) < Tol));
            return Math.Max(a.Length, b.Length) - matched;
        }

        private static double MaxInternalGap(IEnumerable<double> positions)
        {
            var values = (positions ?? Enumerable.Empty<double>()).OrderBy(x => x).ToArray();
            var max = 0.0;
            for (var i = 1; i < values.Length; i++) max = Math.Max(max, values[i] - values[i - 1]);
            return max;
        }

        private sealed class ToleranceComparer : IEqualityComparer<double>
        {
            public bool Equals(double x, double y) => Math.Abs(x - y) < Tol;
            public int GetHashCode(double obj) => Math.Round(obj, 1).GetHashCode();
        }
    }
}
