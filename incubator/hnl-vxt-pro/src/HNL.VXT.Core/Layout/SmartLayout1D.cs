using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// HNL Tool VXT strict-multiple layout engine.
    /// Ported from HNL_VXT_V6.7.6.15_VXT_StrictMultiple_PostProcess:
    /// - minimum item count first;
    /// - every XC/Ty gap is an exact multiple of the configured balance step;
    /// - equal gaps are preferred, otherwise only adjacent unit sizes are distributed evenly;
    /// - dimensional remainder is absorbed by the two edge offsets;
    /// - Max spacing and Max edge remain hard limits; a 25 mm soft-edge pass is attempted before
    ///   dense fallback, where Min spacing may be lowered only when no normal solution exists.
    /// Preview and Create use this same engine.
    /// </summary>
    public static class SmartLayout1D
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;
        private const double DefaultEdgeTolerance = 25.0;

        public sealed class Result
        {
            public Result(double startOffset, IReadOnlyList<double> steps, double endOffset)
                : this(startOffset, steps, endOffset, false, false)
            {
            }

            internal Result(
                double startOffset,
                IReadOnlyList<double> steps,
                double endOffset,
                bool isDense,
                bool usedSoftEdge)
            {
                StartOffset = startOffset;
                Steps = steps ?? Array.Empty<double>();
                EndOffset = endOffset;
                IsDense = isDense;
                UsedSoftEdge = usedSoftEdge;
            }

            public double StartOffset { get; }
            public IReadOnlyList<double> Steps { get; }
            public double EndOffset { get; }
            public bool IsDense { get; }
            public bool UsedSoftEdge { get; }
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
        /// V6.7.6.15 solve-hnl-layout parity.
        /// obstacleGreedy=true corresponds to Lisp is_don=T.
        /// mode=OneSide corresponds to Lisp is_don=2.
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
            if (!BasicInputValid(length, maxSpacing, minSpacing, maxEdge, minEdge, increment))
                return null;

            var oneSided = obstacleGreedy || mode == MainLayoutMode.OneSide;

            var normal = CalculateStrict(
                length, maxSpacing, minSpacing, maxEdge, minEdge,
                increment, oneSided, reverse, dense: false, usedSoftEdge: false);
            if (normal != null) return normal;

            var softMaxEdge = maxEdge + DefaultEdgeTolerance;
            if (DefaultEdgeTolerance > Eps)
            {
                var soft = CalculateStrict(
                    length, maxSpacing, minSpacing, softMaxEdge, minEdge,
                    increment, oneSided, reverse, dense: false, usedSoftEdge: true);
                if (soft != null) return soft;
            }

            var dense = CalculateDense(
                length, maxSpacing, maxEdge, minEdge,
                increment, oneSided, reverse, usedSoftEdge: false);
            if (dense != null) return dense;

            if (DefaultEdgeTolerance > Eps)
            {
                dense = CalculateDense(
                    length, maxSpacing, softMaxEdge, minEdge,
                    increment, oneSided, reverse, usedSoftEdge: true);
            }
            return dense;
        }

        private static Result CalculateStrict(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            bool oneSided,
            bool reverse,
            bool dense,
            bool usedSoftEdge)
        {
            var minDiscrete = CeilMultiple(minSpacing, increment);
            var maxDiscrete = FloorMultiple(maxSpacing, increment);
            if (minDiscrete > maxDiscrete + Tol) return null;

            var minUnits = RoundUnitCount(minDiscrete, increment);
            var maxUnits = RoundUnitCount(maxDiscrete, increment);
            if (minUnits <= 0 || maxUnits < minUnits) return null;

            var maxK = minDiscrete > Eps
                ? Math.Max(0, LispFix((Math.Max(0.0, length - 2.0 * minEdge) + Tol) / minDiscrete))
                : 0;

            for (var k = 0; k <= maxK; k++)
            {
                if (k == 0)
                {
                    var zero = BuildEdges(length, Array.Empty<double>(), minEdge, maxEdge, oneSided, dense, usedSoftEdge);
                    if (zero != null) return MaybeReverse(zero, reverse);
                    continue;
                }

                var lower = Math.Max(k * minDiscrete, length - 2.0 * maxEdge);
                var upper = Math.Min(k * maxDiscrete, length - 2.0 * minEdge);
                var totalUnits = SelectTotalUnits(lower, upper, increment, k, minUnits, maxUnits);
                if (!totalUnits.HasValue) continue;

                var unitSteps = BuildBalancedUnits(k, totalUnits.Value, minUnits, maxUnits);
                if (unitSteps == null) continue;
                var steps = unitSteps.Select(u => u * increment).ToArray();
                var candidate = BuildEdges(length, steps, minEdge, maxEdge, oneSided, dense, usedSoftEdge);
                if (candidate != null) return MaybeReverse(candidate, reverse);
            }
            return null;
        }

        private static Result CalculateDense(
            double length,
            double maxSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            bool oneSided,
            bool reverse,
            bool usedSoftEdge)
        {
            if (length < 2.0 * minEdge - Tol) return null;

            var maxDiscrete = FloorMultiple(maxSpacing, increment);
            var maxUnits = RoundUnitCount(maxDiscrete, increment);
            if (maxDiscrete < increment - Eps || maxUnits < 1) return null;

            var need = Math.Max(0.0, length - 2.0 * maxEdge);
            var k = need <= Tol ? 0 : LispFix(need / maxDiscrete + 0.999999);
            var maxK = Math.Max(
                k,
                LispFix(Math.Max(0.0, length - 2.0 * minEdge) / increment));

            for (; k <= maxK; k++)
            {
                if (k == 0)
                {
                    var zero = BuildEdges(length, Array.Empty<double>(), minEdge, maxEdge, oneSided, true, usedSoftEdge);
                    if (zero != null) return MaybeReverse(zero, reverse);
                    continue;
                }

                var lower = Math.Max(k * increment, length - 2.0 * maxEdge);
                var upper = Math.Min(k * maxDiscrete, length - 2.0 * minEdge);
                var totalUnits = SelectTotalUnits(lower, upper, increment, k, 1, maxUnits);
                if (!totalUnits.HasValue) continue;

                var unitSteps = BuildBalancedUnits(k, totalUnits.Value, 1, maxUnits);
                if (unitSteps == null) continue;
                var steps = unitSteps.Select(u => u * increment).ToArray();
                var candidate = BuildEdges(length, steps, minEdge, maxEdge, oneSided, true, usedSoftEdge);
                if (candidate != null) return MaybeReverse(candidate, reverse);
            }
            return null;
        }

        private static Result BuildEdges(
            double length,
            IReadOnlyList<double> steps,
            double minEdge,
            double maxEdge,
            bool oneSided,
            bool dense,
            bool usedSoftEdge)
        {
            var spacingSum = (steps ?? Array.Empty<double>()).Sum();
            var edgeSum = length - spacingSum;
            double start;
            double end;

            if (oneSided)
            {
                start = Math.Min(maxEdge, edgeSum - minEdge);
                if (start < minEdge) start = minEdge;
                end = edgeSum - start;
            }
            else
            {
                start = edgeSum / 2.0;
                end = start;
            }

            if (start < minEdge - Tol || start > maxEdge + Tol ||
                end < minEdge - Tol || end > maxEdge + Tol)
                return null;

            return new Result(start, steps, end, dense, usedSoftEdge);
        }

        private static int? SelectTotalUnits(
            double lower,
            double upper,
            double increment,
            int k,
            int minUnits,
            int maxUnits)
        {
            if (increment <= 0.0 || k <= 0 || lower > upper + Tol) return null;

            var lo = LispFix(Math.Max(0.0, lower - Tol) / increment + 0.999999);
            var hi = LispFix((upper + Tol) / increment);
            lo = Math.Max(lo, k * minUnits);
            hi = Math.Min(hi, k * maxUnits);
            if (lo > hi) return null;

            var uniformLow = Math.Max(minUnits, LispFix((double)lo / k + 0.999999));
            var uniformHigh = Math.Min(maxUnits, LispFix((double)hi / k));
            return uniformHigh >= uniformLow ? k * uniformHigh : hi;
        }

        private static IReadOnlyList<int> BuildBalancedUnits(
            int k,
            int totalUnits,
            int minUnits,
            int maxUnits)
        {
            if (k <= 0 || totalUnits < k * minUnits || totalUnits > k * maxUnits)
                return null;

            var baseUnits = totalUnits / k;
            var extra = totalUnits - baseUnits * k;
            if (baseUnits < minUnits || baseUnits > maxUnits ||
                (extra > 0 && baseUnits >= maxUnits))
                return null;

            var result = Enumerable.Repeat(baseUnits, k).ToArray();
            if (extra <= 0) return result;

            for (var j = 0; j < extra; j++)
            {
                var index = LispFix(((j + 0.5) * k) / extra);
                if (index >= k) index = k - 1;
                result[index]++;
            }
            return result;
        }

        private static Result MaybeReverse(Result result, bool reverse)
        {
            if (!reverse || result == null) return result;
            return new Result(
                result.EndOffset,
                result.Steps.Reverse().ToArray(),
                result.StartOffset,
                result.IsDense,
                result.UsedSoftEdge);
        }

        private static bool BasicInputValid(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment)
            => length > Eps &&
               maxSpacing > Eps &&
               minSpacing > Eps &&
               maxSpacing + Eps >= minSpacing &&
               minEdge >= -Eps &&
               maxEdge + Eps >= minEdge &&
               increment > Eps;

        private static int RoundUnitCount(double value, double increment)
            => LispFix(value / increment + 0.5);

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
            var x = (idealCoordinates ?? Enumerable.Empty<double>()).OrderBy(v => v).ToList();
            var ideal = x.ToArray();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();
            if (x.Count == 0 || obstacles.Count == 0) return x;
            if (increment <= Eps) increment = 1.0;

            var effectiveMin = minSpacing;
            if (x.Count > 1)
            {
                var actualMin = double.MaxValue;
                for (var i = 1; i < x.Count; i++)
                    actualMin = Math.Min(actualMin, x[i] - x[i - 1]);
                if (actualMin < effectiveMin - Tol) effectiveMin = Math.Max(0.0, actualMin);
            }

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
                        if (value > box.Item1 + Tol && value < box.Item2 - Tol)
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
                        if (value - minLimit < minEdge - Tol)
                        {
                            x[i] = CeilMultiple(minLimit + minEdge, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var previous = x[i - 1];
                        if (value - previous < effectiveMin - Tol)
                        {
                            value = CeilMultiple(previous + effectiveMin, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (value - previous > maxSpacing + Tol)
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
                        if (maxLimit - value < minEdge - Tol)
                        {
                            x[i] = FloorMultiple(maxLimit - minEdge, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var next = x[i + 1];
                        if (next - value < effectiveMin - Tol)
                        {
                            value = FloorMultiple(next - effectiveMin, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (next - value > maxSpacing + Tol)
                        {
                            value = CeilMultiple(next - maxSpacing, increment);
                            x[i] = value;
                            changed = true;
                        }
                    }
                }
                iteration++;
            }

            for (var pass = 0; pass < 4; pass++)
            {
                var anyMoved = false;
                for (var i = 0; i < x.Count; i++)
                {
                    if (!Collides(x[i], obstacles)) continue;
                    var safe = FindSafetyCoordinate(
                        i, x, ideal, obstacles, minLimit, maxLimit,
                        effectiveMin, maxSpacing, minEdge, maxEdge, increment);
                    if (!double.IsNaN(safe) && Math.Abs(safe - x[i]) > Eps)
                    {
                        x[i] = safe;
                        anyMoved = true;
                    }
                }
                if (!anyMoved || x.All(v => !Collides(v, obstacles))) break;
            }

            x.Sort();
            if (!GridLayoutValid(x, minLimit, maxLimit, effectiveMin, maxSpacing, minEdge, maxEdge))
                return Array.Empty<double>();
            if (x.Any(v => Collides(v, obstacles)))
                return Array.Empty<double>();

            return x;
        }

        private static bool GridLayoutValid(
            IReadOnlyList<double> values,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge)
        {
            if (values == null || values.Count == 0) return false;
            var ordered = values.OrderBy(v => v).ToArray();
            var firstEdge = ordered[0] - minLimit;
            var lastEdge = maxLimit - ordered[ordered.Length - 1];
            if (firstEdge < minEdge - Tol || firstEdge > maxEdge + Tol) return false;
            if (lastEdge < minEdge - Tol || lastEdge > maxEdge + Tol) return false;

            for (var i = 1; i < ordered.Length; i++)
            {
                var gap = ordered[i] - ordered[i - 1];
                if (gap < minSpacing - Tol || gap > maxSpacing + Tol) return false;
            }
            return true;
        }

        private static double FindSafetyCoordinate(
            int index,
            IList<double> current,
            IReadOnlyList<double> ideal,
            IReadOnlyList<Tuple<double, double>> obstacles,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment)
        {
            var candidates = new HashSet<double>();
            foreach (var box in obstacles)
            {
                var left = FloorMultiple(box.Item1, increment);
                var right = CeilMultiple(box.Item2, increment);
                candidates.Add(left);
                candidates.Add(right);
                for (var k = 1; k <= 8; k++)
                {
                    candidates.Add(left - k * increment);
                    candidates.Add(right + k * increment);
                }
            }

            var best = double.NaN;
            var bestScore = double.MaxValue;
            foreach (var candidate in candidates)
            {
                if (candidate <= minLimit + Eps || candidate >= maxLimit - Eps) continue;
                if (Collides(candidate, obstacles)) continue;

                var score = 0.0;
                if (index == 0)
                    score += ConstraintPenalty(candidate - minLimit, minEdge, maxEdge);
                if (index == current.Count - 1)
                    score += ConstraintPenalty(maxLimit - candidate, minEdge, maxEdge);
                if (index > 0)
                    score += ConstraintPenalty(candidate - current[index - 1], minSpacing, maxSpacing);
                if (index + 1 < current.Count)
                    score += ConstraintPenalty(current[index + 1] - candidate, minSpacing, maxSpacing);

                score += Math.Abs(candidate - ideal[index]);
                if (score < bestScore - Eps ||
                    (Math.Abs(score - bestScore) <= Eps &&
                     Math.Abs(candidate - ideal[index]) < Math.Abs(best - ideal[index])))
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private static double ConstraintPenalty(double distance, double min, double max)
        {
            if (distance < min - Tol) return 100000.0 + 10000.0 * (min - distance);
            if (distance > max + Tol) return 100000.0 + 10000.0 * (distance - max);
            return 0.0;
        }

        private static bool Collides(double value, IEnumerable<Tuple<double, double>> obstacles)
            => obstacles.Any(box => value > box.Item1 + Tol && value < box.Item2 - Tol);

        public static double OptimizeOffset(
            Result layout,
            double minCoordinate,
            double minEdge,
            double maxEdge,
            double increment,
            Func<double, bool> isCoordinateClear)
        {
            if (layout == null || isCoordinateClear == null) return layout?.StartOffset ?? 0.0;
            if (IsLayoutClear(layout, minCoordinate, layout.StartOffset, isCoordinateClear))
                return layout.StartOffset;

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

        private static bool IsLayoutClear(
            Result layout,
            double minCoordinate,
            double offset,
            Func<double, bool> clear)
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

        private static double FloorMultiple(double value, double increment)
            => Math.Floor((value + 1e-10) / increment) * increment;

        private static double CeilMultiple(double value, double increment)
            => Math.Ceiling((value - 1e-10) / increment) * increment;
    }
}
