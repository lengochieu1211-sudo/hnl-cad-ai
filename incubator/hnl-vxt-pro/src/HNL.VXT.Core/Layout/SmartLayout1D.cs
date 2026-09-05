using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Exact AutoCAD-independent port of the legacy VXT calc-smart-layout / adjust-grid rules.
    /// Preview and Create use this same implementation so the .NET engine follows the Lisp output.
    /// </summary>
    public static class SmartLayout1D
    {
        private const double Eps = 1e-8;

        public sealed class Result
        {
            public Result(double startOffset, IReadOnlyList<double> steps, double endOffset)
            {
                StartOffset = startOffset;
                Steps = steps ?? Array.Empty<double>();
                EndOffset = endOffset;
            }

            public double StartOffset { get; }
            public IReadOnlyList<double> Steps { get; }
            public double EndOffset { get; }
            public int PointCount => Steps.Count + 1;
            public double Span => Steps.Sum();

            public IReadOnlyList<double> Positions(double minCoordinate)
            {
                var result = new List<double>(PointCount);
                var value = minCoordinate + StartOffset;
                result.Add(value);
                for (var i = 0; i < Steps.Count; i++)
                {
                    value += Steps[i];
                    result.Add(value);
                }
                return result;
            }
        }

        /// <summary>
        /// Legacy calc-smart-layout.
        /// obstacleGreedy=true corresponds to Lisp is_don=T (used when XC has avoidance boxes).
        /// mode=OneSide corresponds to Lisp is_don=2. Other modes correspond to nil.
        /// </summary>
        public static Result Calculate(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            MainLayoutMode mode,
            bool reverse = false,
            bool obstacleGreedy = false)
        {
            if (length <= Eps) return null;
            increment = Math.Abs(increment);
            if (increment <= Eps) increment = 1.0;

            var maxRounded = LispFix(maxSpacing / increment) * increment;
            var minRounded = LispFix(minSpacing / increment + 0.9999) * increment;
            if (maxRounded < minRounded) maxRounded = minRounded;
            if (maxRounded <= 0.0) maxRounded = increment;
            if (minRounded <= 0.0) minRounded = increment;

            Result result = obstacleGreedy
                ? CalculateObstacleGreedy(length, maxRounded, minRounded, maxEdge, minEdge, increment)
                : CalculateBalanced(length, maxRounded, minRounded, maxEdge, minEdge, increment, mode == MainLayoutMode.OneSide);

            if (!reverse || result == null) return result;
            return new Result(result.EndOffset, result.Steps.Reverse().ToArray(), result.StartOffset);
        }

        private static Result CalculateObstacleGreedy(
            double length, double maxSpacing, double minSpacing, double maxEdge, double minEdge, double increment)
        {
            var plans = new List<LegacyCandidate>();
            if (length >= 2.0 * minEdge && length <= 2.0 * maxEdge)
            {
                var o1 = Math.Min(maxEdge, length - minEdge);
                o1 = LispFix(o1 / increment) * increment;
                if (o1 < minEdge) o1 = minEdge;
                var o2 = length - o1;
                plans.Insert(0, new LegacyCandidate(0.0, o1, Array.Empty<double>(), o2));
            }

            var n = 1;
            while (n * minSpacing <= length - 2.0 * minEdge + Eps)
            {
                var minSum = Math.Max(n * minSpacing, length - 2.0 * maxEdge);
                var maxSum = Math.Min(n * maxSpacing, length - 2.0 * minEdge);
                if (minSum <= maxSum + Eps)
                {
                    var spacingSum = LispFix(maxSum / increment) * increment;
                    if (spacingSum < minSum - Eps) spacingSum += increment;
                    if (spacingSum >= minSum - Eps && spacingSum <= maxSum + Eps)
                    {
                        var steps = new List<double>(n);
                        var remaining = spacingSum;
                        for (var i = 0; i < n; i++)
                        {
                            double take;
                            if (i == n - 1)
                            {
                                take = remaining;
                            }
                            else
                            {
                                take = maxSpacing;
                                var maxAllow = remaining - (n - 1 - i) * minSpacing;
                                var minAllow = remaining - (n - 1 - i) * maxSpacing;
                                if (take > maxAllow) take = maxAllow;
                                if (take < minAllow) take = minAllow;
                                if (take > maxSpacing) take = maxSpacing;
                                if (take < minSpacing) take = minSpacing;
                                remaining -= take;
                            }
                            steps.Add(take);
                        }

                        var o1 = Math.Min(maxEdge, length - spacingSum - minEdge);
                        o1 = LispFix(o1 / increment) * increment;
                        if (o1 < minEdge) o1 = minEdge;
                        var o2 = length - spacingSum - o1;

                        var penalty = 0.0;
                        if (o1 < minEdge) penalty += 10000.0 * (minEdge - o1);
                        if (o1 > maxEdge) penalty += 10000.0 * (o1 - maxEdge);
                        if (o2 < minEdge) penalty += 10000.0 * (minEdge - o2);
                        if (o2 > maxEdge) penalty += 10000.0 * (o2 - maxEdge);
                        penalty += 5.0 * (maxEdge - o1);

                        var score = -penalty - 10000.0 * n;
                        plans.Insert(0, new LegacyCandidate(score, o1, steps.ToArray(), o2));
                    }
                }
                n++;
            }

            if (plans.Count == 0)
                return new Result(length / 2.0, Array.Empty<double>(), length / 2.0);

            var best = plans[0];
            var bestScore = -9999999.0;
            foreach (var plan in plans)
            {
                if (plan.Score > bestScore)
                {
                    bestScore = plan.Score;
                    best = plan;
                }
            }
            return new Result(best.Start, best.Steps, best.End);
        }

        private static Result CalculateBalanced(
            double length, double maxSpacing, double minSpacing, double maxEdge, double minEdge, double increment, bool oneSide)
        {
            if (length >= 2.0 * minEdge && length <= 2.0 * maxEdge)
                return new Result(length / 2.0, Array.Empty<double>(), length / 2.0);

            var plans = new List<LegacyCandidate>();
            var spacing = maxSpacing;
            while (spacing >= minSpacing - Eps)
            {
                var kMin = LispFix((length - 2.0 * maxEdge) / spacing + 0.9999);
                var kMax = LispFix((length - 2.0 * minEdge) / spacing);
                var k = Math.Min(kMin, kMax);
                var kLimit = Math.Max(kMin, kMax);
                var loop = true;
                while (loop)
                {
                    if (k <= 0) k = 1;
                    var edge = (length - k * spacing) / 2.0;
                    var penalty = 0.0;
                    if (edge < minEdge) penalty += 10000.0 * (minEdge - edge);
                    if (edge > maxEdge) penalty += 10000.0 * (edge - maxEdge);
                    penalty += 10000.0 * k;
                    penalty += 2.0 * (maxSpacing - spacing);
                    penalty += maxEdge - edge;
                    plans.Insert(0, new LegacyCandidate(-penalty, edge, Repeat(k, spacing), edge));
                    if (k >= kLimit) loop = false;
                    else k++;
                }
                spacing -= increment;
            }

            if (plans.Count == 0)
                return new Result(length / 2.0, Array.Empty<double>(), length / 2.0);

            var best = plans[0];
            var bestScore = -9999999.0;
            foreach (var plan in plans)
            {
                if (plan.Score > bestScore)
                {
                    bestScore = plan.Score;
                    best = plan;
                }
            }

            if (!oneSide)
                return new Result(best.Start, best.Steps, best.End);

            var sumSpacing = best.Steps.Sum();
            var balancedEdge = best.Start;
            var o1 = LispFix(balancedEdge / increment) * increment;
            if (o1 < minEdge) o1 += increment;
            var o2 = length - sumSpacing - o1;
            if (o2 < minEdge - Eps || o2 > maxEdge + Eps)
            {
                o1 = LispFix(balancedEdge / increment + 0.9999) * increment;
                if (o1 > maxEdge) o1 -= increment;
                o2 = length - sumSpacing - o1;
                if (o2 < minEdge - Eps || o2 > maxEdge + Eps)
                {
                    o1 = balancedEdge;
                    o2 = balancedEdge;
                }
            }
            return new Result(o1, best.Steps, o2);
        }

        public static IReadOnlyList<double> AdjustGrid(
            IEnumerable<double> idealCoordinates,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment)
        {
            var x = (idealCoordinates ?? Enumerable.Empty<double>()).ToList();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();
            if (x.Count == 0 || obstacles.Count == 0) return x;
            if (increment <= Eps) increment = 1.0;

            var changed = true;
            var iteration = 0;
            while (changed && iteration < 100)
            {
                changed = false;
                for (var index = 0; index < x.Count; index++)
                {
                    var value = x[index];
                    Tuple<double, double> collision = null;
                    foreach (var box in obstacles)
                    {
                        if (value > box.Item1 + 0.1 && value < box.Item2 - 0.1)
                            collision = box;
                    }
                    if (collision != null)
                    {
                        var left = FloorMultiple(collision.Item1, increment);
                        var right = CeilMultiple(collision.Item2, increment);
                        value = value - left <= right - value ? left : right;
                        x[index] = value;
                        changed = true;
                    }
                }

                for (var i = 0; i < x.Count; i++)
                {
                    var value = x[i];
                    if (i == 0)
                    {
                        if (value - minLimit < minEdge - 0.1)
                        {
                            x[i] = CeilMultiple(minLimit + minEdge, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var previous = x[i - 1];
                        if (value - previous < minSpacing - 0.1)
                        {
                            value = CeilMultiple(previous + minSpacing, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (value - previous > maxSpacing + 0.1)
                        {
                            value = FloorMultiple(previous + maxSpacing, increment);
                            x[i] = value;
                            changed = true;
                        }
                    }
                }

                for (var i = x.Count - 1; i >= 0; i--)
                {
                    var value = x[i];
                    if (i == x.Count - 1)
                    {
                        if (maxLimit - value < minEdge - 0.1)
                        {
                            x[i] = FloorMultiple(maxLimit - minEdge, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var next = x[i + 1];
                        if (next - value < minSpacing - 0.1)
                        {
                            value = FloorMultiple(next - minSpacing, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (next - value > maxSpacing + 0.1)
                        {
                            value = CeilMultiple(next - maxSpacing, increment);
                            x[i] = value;
                            changed = true;
                        }
                    }
                }
                iteration++;
            }
            return x;
        }

        public static double OptimizeOffset(
            Result layout,
            double minCoordinate,
            double minEdge,
            double maxEdge,
            double increment,
            Func<double, bool> isCoordinateClear)
        {
            if (layout == null || isCoordinateClear == null) return layout?.StartOffset ?? 0.0;
            if (IsLayoutClear(layout, minCoordinate, layout.StartOffset, isCoordinateClear)) return layout.StartOffset;

            var length = layout.StartOffset + layout.Span + layout.EndOffset;
            var spacingSum = layout.Span;
            var k = 1;
            var limitReached = false;
            while (!limitReached && k < 100000)
            {
                var delta = k * increment;
                limitReached = true;
                var plus = layout.StartOffset + delta;
                var plusEnd = length - spacingSum - plus;
                if (plus <= maxEdge + Eps && plusEnd >= minEdge - Eps)
                {
                    limitReached = false;
                    if (IsLayoutClear(layout, minCoordinate, plus, isCoordinateClear)) return plus;
                }
                var minus = layout.StartOffset - delta;
                var minusEnd = length - spacingSum - minus;
                if (minus >= minEdge - Eps && minusEnd <= maxEdge + Eps)
                {
                    limitReached = false;
                    if (IsLayoutClear(layout, minCoordinate, minus, isCoordinateClear)) return minus;
                }
                k++;
            }
            return double.NaN;
        }

        private static bool IsLayoutClear(Result layout, double minCoordinate, double offset, Func<double, bool> clear)
        {
            var value = minCoordinate + offset;
            if (!clear(value)) return false;
            for (var i = 0; i < layout.Steps.Count; i++)
            {
                value += layout.Steps[i];
                if (!clear(value)) return false;
            }
            return true;
        }

        private static int LispFix(double value) => (int)value;

        private static IReadOnlyList<double> Repeat(int count, double value)
        {
            if (count <= 0) return Array.Empty<double>();
            var result = new double[count];
            for (var i = 0; i < count; i++) result[i] = value;
            return result;
        }

        private static double FloorMultiple(double value, double increment)
            => Math.Floor((value + 1e-10) / increment) * increment;

        private static double CeilMultiple(double value, double increment)
            => Math.Ceiling((value - 1e-10) / increment) * increment;

        private sealed class LegacyCandidate
        {
            public LegacyCandidate(double score, double start, IReadOnlyList<double> steps, double end)
            {
                Score = score;
                Start = start;
                Steps = steps ?? Array.Empty<double>();
                End = end;
            }
            public double Score { get; }
            public double Start { get; }
            public IReadOnlyList<double> Steps { get; }
            public double End { get; }
        }
    }
}
