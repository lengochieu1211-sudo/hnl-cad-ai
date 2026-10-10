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
    /// - Max spacing and Max edge are HARD and are never relaxed;
    /// - Min spacing is SOFT through the existing dense lattice fallback;
    /// - Min edge is SOFT: strict Min is tried first, then configured Min-edge tolerance,
    ///   then a final lattice fallback only when no preferred solution exists.
    /// Every spacing remains an exact multiple of the configured increment.
    /// Preview and Create use this same engine.
    /// </summary>
    public static class SmartLayout1D
    {
        private const double Eps = 1e-8;
        private const double Tol = 0.1;

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
            bool obstacleGreedy = false,
            double minEdgeTolerance = 25.0,
            bool preferOneSideTailFallback = false)
        {
            if (!BasicInputValid(length, maxSpacing, minSpacing, maxEdge, minEdge, increment))
                return null;

            var explicitOneSide = mode == MainLayoutMode.OneSide;
            var oneSided = obstacleGreedy || explicitOneSide;

            // Explicit OneSide contract:
            // 1) minimize member count first (HARD Max spacing + HARD Max edge);
            // 2) with that same count, search the largest uniform lattice spacing that keeps
            //    both edges inside the normal Min/Max range;
            // 3) only after strict Min-spacing candidates fail, use the existing Dense fallback;
            // 4) only after strict-edge candidates fail, relax Min edge through its SOFT passes.
            //
            // This prevents "Max at any cost". Example: L=3442, Max=1000, Min=700,
            // edge=300..400, step=50 has the same member count at 1000/950/900.
            // 1000 would force a short far edge, but 900 gives 350|900|900|900|392,
            // so the strict 900 solution must win before SOFT edge is considered.
            if (explicitOneSide)
            {
                var chase = CalculateOneSideChase(
                    length, maxSpacing, minSpacing, minSpacing,
                    maxEdge, minEdge, increment, minEdge,
                    reverse, usedSoftEdge: false);
                if (chase != null) return chase;

                // Existing Dense contract: Min spacing is SOFT, but keep the same minimum
                // member count and strict edges before relaxing Min edge.
                chase = CalculateOneSideChase(
                    length, maxSpacing, increment, minSpacing,
                    maxEdge, minEdge, increment, minEdge,
                    reverse, usedSoftEdge: false);
                if (chase != null) return chase;

                var toleratedMinEdgeForChase = Math.Max(
                    0.0, minEdge - Math.Max(0.0, minEdgeTolerance));
                if (toleratedMinEdgeForChase < minEdge - Eps)
                {
                    chase = CalculateOneSideChase(
                        length, maxSpacing, minSpacing, minSpacing,
                        maxEdge, minEdge, increment, toleratedMinEdgeForChase,
                        reverse, usedSoftEdge: true);
                    if (chase != null) return chase;

                    chase = CalculateOneSideChase(
                        length, maxSpacing, increment, minSpacing,
                        maxEdge, minEdge, increment, toleratedMinEdgeForChase,
                        reverse, usedSoftEdge: true);
                    if (chase != null) return chase;
                }

                // Final Min-edge fallback remains last. HARD Max spacing/edge and the minimum
                // achievable member count are never relaxed.
                if (toleratedMinEdgeForChase > Eps)
                {
                    chase = CalculateOneSideChase(
                        length, maxSpacing, minSpacing, minSpacing,
                        maxEdge, minEdge, increment, 0.0,
                        reverse, usedSoftEdge: true);
                    if (chase != null) return chase;

                    chase = CalculateOneSideChase(
                        length, maxSpacing, increment, minSpacing,
                        maxEdge, minEdge, increment, 0.0,
                        reverse, usedSoftEdge: true);
                    if (chase != null) return chase;
                }

                // Construction-friendly Ty-only opt-in:
                // If no uniform OneSide lattice exists, keep Max spacing continuously from
                // the selected start side and absorb the dimensional remainder into ONE final
                // lattice gap. This avoids alternating 950/1000/... rows in the field.
                //
                // HARD Max spacing / Max edge remain absolute. Min spacing and Min edge keep
                // their existing SOFT contract. If even this one-tail solution cannot satisfy
                // HARD limits, fall through to the certified legacy balanced/dense fallback.
                if (preferOneSideTailFallback)
                {
                    var tail = CalculateOneSideTailFallback(
                        length, maxSpacing, minSpacing,
                        maxEdge, minEdge, increment,
                        minEdgeTolerance, reverse);
                    if (tail != null) return tail;
                }
            }

            // 1) Preferred solution: both Min spacing and Min edge are respected.
            var normal = CalculateStrict(
                length, maxSpacing, minSpacing, maxEdge, minEdge,
                increment, oneSided, reverse, dense: false, usedSoftEdge: false);
            if (normal != null) return normal;

            // 2) Existing Dense contract: Min spacing may fall below configured Min,
            // but Max spacing/Max edge and Min edge are still respected.
            var dense = CalculateDense(
                length, maxSpacing, maxEdge, minEdge,
                increment, oneSided, reverse, usedSoftEdge: false);
            if (dense != null) return dense;

            // 3) Min-edge tolerance is SOFT only on the minimum side. Max edge is never expanded.
            var toleratedMinEdge = Math.Max(0.0, minEdge - Math.Max(0.0, minEdgeTolerance));
            if (toleratedMinEdge < minEdge - Eps)
            {
                var softEdge = CalculateStrict(
                    length, maxSpacing, minSpacing, maxEdge, toleratedMinEdge,
                    increment, oneSided, reverse, dense: false, usedSoftEdge: true);
                if (softEdge != null) return softEdge;

                dense = CalculateDense(
                    length, maxSpacing, maxEdge, toleratedMinEdge,
                    increment, oneSided, reverse, usedSoftEdge: true);
                if (dense != null) return dense;
            }

            // 4) Final Min-edge fallback. This keeps Min edge genuinely SOFT for very short runs,
            // while Max edge/Max spacing remain HARD and every spacing remains on the lattice.
            if (toleratedMinEdge > Eps)
            {
                var fallback = CalculateStrict(
                    length, maxSpacing, minSpacing, maxEdge, 0.0,
                    increment, oneSided, reverse, dense: false, usedSoftEdge: true);
                if (fallback != null) return fallback;

                dense = CalculateDense(
                    length, maxSpacing, maxEdge, 0.0,
                    increment, oneSided, reverse, usedSoftEdge: true);
                if (dense != null) return dense;
            }

            return null;
        }

        private static Result CalculateOneSideChase(
            double length,
            double maxSpacing,
            double minCandidateSpacing,
            double configuredMinSpacing,
            double maxEdge,
            double startEdge,
            double increment,
            double minAcceptedFarEdge,
            bool reverse,
            bool usedSoftEdge)
        {
            var maxDiscrete = FloorMultiple(maxSpacing, increment);
            var minDiscrete = Math.Max(increment, CeilMultiple(minCandidateSpacing, increment));
            if (maxDiscrete < increment - Eps || minDiscrete > maxDiscrete + Tol) return null;
            if (startEdge < -Tol || startEdge > maxEdge + Tol) return null;

            // First determine the minimum number of internal gaps required by HARD constraints
            // at the configured Max spacing. The count is then frozen while candidate spacing is
            // searched downward. Therefore reducing 1000 -> 900 never adds an XC when k is unchanged.
            var hardInteriorNeed = Math.Max(0.0, length - 2.0 * maxEdge);
            var k = hardInteriorNeed <= Tol
                ? 0
                : LispFix(hardInteriorNeed / maxDiscrete + 0.999999);

            if (k == 0)
            {
                var edgeSum = length;
                if (edgeSum < startEdge + minAcceptedFarEdge - Tol ||
                    edgeSum > 2.0 * maxEdge + Tol)
                    return null;

                var shift0 = Math.Max(0.0, edgeSum - startEdge - maxEdge);
                if (shift0 > Tol) shift0 = CeilMultiple(shift0, increment);
                var start0 = startEdge + shift0;
                var end0 = edgeSum - start0;
                if (start0 > maxEdge + Tol ||
                    end0 < minAcceptedFarEdge - Tol ||
                    end0 > maxEdge + Tol)
                    return null;

                return MaybeReverse(
                    new Result(
                        start0,
                        Array.Empty<double>(),
                        end0,
                        isDense: false,
                        usedSoftEdge: usedSoftEdge || end0 < startEdge - Tol),
                    reverse);
            }

            // For the fixed minimum count, take the largest uniform lattice spacing that
            // satisfies the current edge pass. This is the exact "same number of XC -> choose
            // the largest spacing that gives better two-edge distribution" rule.
            for (var spacing = maxDiscrete; spacing >= minDiscrete - Tol; spacing -= increment)
            {
                var edgeSum = length - k * spacing;

                // Both edges are HARD <= Max. Current pass also enforces its accepted Min edge.
                if (edgeSum > 2.0 * maxEdge + Tol) break;
                if (edgeSum < startEdge + minAcceptedFarEdge - Tol) continue;

                // Keep the selected start edge at Min when possible. If the far edge would exceed
                // HARD Max, shift the entire grid only by the minimum lattice increment needed.
                var shift = Math.Max(0.0, edgeSum - startEdge - maxEdge);
                if (shift > Tol) shift = CeilMultiple(shift, increment);

                var shiftedStartEdge = startEdge + shift;
                var farEdge = edgeSum - shiftedStartEdge;

                if (shiftedStartEdge < startEdge - Tol ||
                    shiftedStartEdge > maxEdge + Tol ||
                    farEdge <= 2.0 + Tol ||
                    farEdge < minAcceptedFarEdge - Tol ||
                    farEdge > maxEdge + Tol)
                    continue;

                var steps = Enumerable.Repeat(spacing, k).ToArray();
                var result = new Result(
                    shiftedStartEdge,
                    steps,
                    farEdge,
                    isDense: spacing < configuredMinSpacing - Tol,
                    usedSoftEdge: usedSoftEdge || farEdge < startEdge - Tol);
                return MaybeReverse(result, reverse);
            }

            return null;
        }

        private static Result CalculateOneSideTailFallback(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            double minEdgeTolerance,
            bool reverse)
        {
            var maxDiscrete = FloorMultiple(maxSpacing, increment);
            if (maxDiscrete < increment - Eps) return null;
            if (minEdge < -Tol || minEdge > maxEdge + Tol) return null;

            // Freeze the same minimum number of internal gaps required by HARD Max.
            var hardInteriorNeed = Math.Max(0.0, length - 2.0 * maxEdge);
            var k = hardInteriorNeed <= Tol
                ? 0
                : LispFix(hardInteriorNeed / maxDiscrete + 0.999999);
            if (k <= 0) return null;

            // The selected start side stays anchored at Min edge. All gaps except the last
            // are Max; only the final gap may absorb the remainder.
            var startEdge = minEdge;
            var fixedSpan = (k - 1) * maxDiscrete;
            var tailTotal = length - startEdge - fixedSpan;
            if (tailTotal <= increment - Tol) return null;

            // Prefer keeping the far edge inside the configured SOFT tolerance. This is why
            // e.g. 17090.15 becomes 300 | 1000x16 | 500 | 290.15 instead of a 450-mm tail:
            // a 10-mm edge shortfall is preferred to another 50-mm spacing shortfall.
            var toleratedMinEdge = Math.Max(
                0.0, minEdge - Math.Max(0.0, minEdgeTolerance));
            var farEdgePasses = toleratedMinEdge > Eps
                ? new[] { toleratedMinEdge, 0.0 }
                : new[] { 0.0 };

            foreach (var minAcceptedFarEdge in farEdgePasses)
            {
                var maxTailByFarEdge = tailTotal - minAcceptedFarEdge;
                var tailGap = FloorMultiple(
                    Math.Min(maxDiscrete, maxTailByFarEdge),
                    increment);
                if (tailGap < increment - Tol || tailGap > maxDiscrete + Tol)
                    continue;

                var farEdge = tailTotal - tailGap;
                if (farEdge <= 2.0 + Tol ||
                    farEdge < minAcceptedFarEdge - Tol ||
                    farEdge > maxEdge + Tol)
                    continue;

                var steps = Enumerable.Repeat(maxDiscrete, k).ToArray();
                steps[k - 1] = tailGap;

                var result = new Result(
                    startEdge,
                    steps,
                    farEdge,
                    isDense: tailGap < minSpacing - Tol,
                    usedSoftEdge: farEdge < minEdge - Tol);
                return MaybeReverse(result, reverse);
            }

            return null;
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
            double increment,
            double minEdgeTolerance = 25.0)
        {
            var ideal = (idealCoordinates ?? Enumerable.Empty<double>()).OrderBy(v => v).ToArray();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();
            if (ideal.Length == 0 || obstacles.Count == 0) return ideal;
            if (increment <= Eps) increment = 1.0;

            // Preserve an already-dense/soft baseline. The MEP pass must never "repair" a valid
            // soft-Min base grid back into a different phase before it even considers obstacles.
            var preferredMinSpacing = minSpacing;
            if (ideal.Length > 1)
            {
                var actualMin = double.MaxValue;
                for (var i = 1; i < ideal.Length; i++)
                    actualMin = Math.Min(actualMin, ideal[i] - ideal[i - 1]);
                if (actualMin < preferredMinSpacing - Tol)
                    preferredMinSpacing = Math.Max(0.0, actualMin);
            }

            var preferredMinEdge = minEdge;
            var actualEdge = Math.Min(ideal[0] - minLimit, maxLimit - ideal[ideal.Length - 1]);
            if (actualEdge < preferredMinEdge - Tol)
                preferredMinEdge = Math.Max(0.0, actualEdge);

            var denseMinSpacing = Math.Min(preferredMinSpacing, Math.Max(increment, Eps));
            var toleratedMinEdge = Math.Max(
                0.0,
                Math.Min(preferredMinEdge, minEdge - Math.Max(0.0, minEdgeTolerance)));

            var passes = new[]
            {
                Tuple.Create(preferredMinSpacing, preferredMinEdge),
                Tuple.Create(denseMinSpacing, preferredMinEdge),
                Tuple.Create(preferredMinSpacing, toleratedMinEdge),
                Tuple.Create(denseMinSpacing, toleratedMinEdge),
                Tuple.Create(preferredMinSpacing, 0.0),
                Tuple.Create(denseMinSpacing, 0.0)
            };

            foreach (var pass in passes)
            {
                var adjusted = AdjustGridPass(
                    ideal, obstacles, minLimit, maxLimit,
                    pass.Item1, maxSpacing, pass.Item2, maxEdge, increment);
                if (adjusted.Count > 0) return adjusted;
            }

            return Array.Empty<double>();
        }

        private static IReadOnlyList<double> AdjustGridPass(
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
            var x = (ideal ?? Array.Empty<double>()).OrderBy(v => v).ToList();
            if (x.Count == 0) return x;

            // Local MEP repair stays on the phase of the normal XC/Ty grid.
            var gridOrigin = ideal[0];
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
                        var left = FloorMultipleOnPhase(collision.Item1, gridOrigin, increment);
                        var right = CeilMultipleOnPhase(collision.Item2, gridOrigin, increment);
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
                            x[i] = CeilMultipleOnPhase(minLimit + minEdge, gridOrigin, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var previous = x[i - 1];
                        if (value - previous < minSpacing - Tol)
                        {
                            value = CeilMultipleOnPhase(previous + minSpacing, gridOrigin, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (value - previous > maxSpacing + Tol)
                        {
                            value = FloorMultipleOnPhase(previous + maxSpacing, gridOrigin, increment);
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
                            x[i] = FloorMultipleOnPhase(maxLimit - minEdge, gridOrigin, increment);
                            changed = true;
                        }
                    }
                    else
                    {
                        var next = x[i + 1];
                        if (next - value < minSpacing - Tol)
                        {
                            value = FloorMultipleOnPhase(next - minSpacing, gridOrigin, increment);
                            x[i] = value;
                            changed = true;
                        }
                        if (next - value > maxSpacing + Tol)
                        {
                            value = CeilMultipleOnPhase(next - maxSpacing, gridOrigin, increment);
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
                        minSpacing, maxSpacing, minEdge, maxEdge, increment, gridOrigin);
                    if (!double.IsNaN(safe) && Math.Abs(safe - x[i]) > Eps)
                    {
                        x[i] = safe;
                        anyMoved = true;
                    }
                }
                if (!anyMoved || x.All(v => !Collides(v, obstacles))) break;
            }

            x.Sort();
            if (!GridLayoutValid(x, minLimit, maxLimit, minSpacing, maxSpacing, minEdge, maxEdge))
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
            double increment,
            double gridOrigin)
        {
            var candidates = new HashSet<double>();
            foreach (var box in obstacles)
            {
                var left = FloorMultipleOnPhase(box.Item1, gridOrigin, increment);
                var right = CeilMultipleOnPhase(box.Item2, gridOrigin, increment);
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

        private static double FloorMultipleOnPhase(double value, double origin, double increment)
            => origin + FloorMultiple(value - origin, increment);

        private static double CeilMultipleOnPhase(double value, double origin, double increment)
            => origin + CeilMultiple(value - origin, increment);
    }
}
