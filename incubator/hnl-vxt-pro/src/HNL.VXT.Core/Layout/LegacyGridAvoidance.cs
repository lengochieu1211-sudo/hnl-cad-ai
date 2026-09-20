using System;
using System.Collections.Generic;
using System.Linq;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Best-effort port of the V6.7.2 Lisp adjust-grid routine, with one field-proven
    /// correction: snapping is performed on the phase of the original layout grid instead
    /// of the absolute WCS zero lattice.
    ///
    /// The original Lisp uses math-floor-mult/math-ceil-mult directly on absolute coordinates.
    /// In drawings whose ceiling origin is not itself an exact multiple of the configured
    /// increment, a collision repair can therefore mix two grid phases and create odd XC
    /// spacings such as 823.113 / 876.887 even when the configured main increment is 50 mm.
    ///
    /// HNL VXT keeps the original best-effort semantics (maximum 100 passes and never discards
    /// the entire Legacy grid), but every repair stays congruent with the first ideal grid
    /// coordinate. This preserves the configured spacing increment between final members and
    /// also preserves XP/Ty one-side phases when this helper is reused by those Legacy paths.
    /// Pro hard-validation semantics remain isolated in SmartLayout1D and are unchanged.
    /// </summary>
    internal static class LegacyGridAvoidance
    {
        private const double Tol = 0.1;
        private const int MaxIterations = 100;

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
            => AdjustGridCore(
                idealCoordinates, obstacleIntervals,
                minLimit, maxLimit, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                preserveIdealGridPhase: true, minEdgeTolerance: minEdgeTolerance);

        /// <summary>
        /// Exact V6.7.2 XP snapping remains available. Soft-Min fallback still keeps
        /// Max spacing/Max edge HARD; only the lattice origin differs for this XP path.
        /// </summary>
        public static IReadOnlyList<double> AdjustGridAbsoluteLisp(
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
            => AdjustGridCore(
                idealCoordinates, obstacleIntervals,
                minLimit, maxLimit, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                preserveIdealGridPhase: false, minEdgeTolerance: minEdgeTolerance);

        private static IReadOnlyList<double> AdjustGridCore(
            IEnumerable<double> idealCoordinates,
            IEnumerable<Tuple<double, double>> obstacleIntervals,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment,
            bool preserveIdealGridPhase,
            double minEdgeTolerance)
        {
            var ideal = (idealCoordinates ?? Enumerable.Empty<double>()).OrderBy(x => x).ToArray();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();
            if (ideal.Length == 0 || obstacles.Count == 0) return ideal;
            if (increment <= 0.0) increment = 1.0;

            var preferredMinSpacing = minSpacing;
            if (ideal.Length > 1)
            {
                var actualMin = double.MaxValue;
                for (var index = 1; index < ideal.Length; index++)
                    actualMin = Math.Min(actualMin, ideal[index] - ideal[index - 1]);
                if (actualMin < preferredMinSpacing - Tol)
                    preferredMinSpacing = Math.Max(0.0, actualMin);
            }

            var preferredMinEdge = minEdge;
            var actualEdge = Math.Min(ideal[0] - minLimit, maxLimit - ideal[ideal.Length - 1]);
            if (actualEdge < preferredMinEdge - Tol)
                preferredMinEdge = Math.Max(0.0, actualEdge);

            var denseMinSpacing = Math.Min(preferredMinSpacing, Math.Max(increment, Tol));
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
                var candidate = AdjustPass(
                    ideal, obstacles, minLimit, maxLimit,
                    pass.Item1, maxSpacing, pass.Item2, maxEdge, increment,
                    preserveIdealGridPhase);

                if (GridHardValid(candidate, minLimit, maxLimit, maxSpacing, maxEdge) &&
                    candidate.All(value => !Collides(value, obstacles)))
                    return candidate;
            }

            // MEP avoidance is lower priority than structural Max limits. If no clear repair exists,
            // preserve the original hard-valid lattice instead of returning a Max violation.
            return GridHardValid(ideal, minLimit, maxLimit, maxSpacing, maxEdge)
                ? ideal
                : Array.Empty<double>();
        }

        private static IReadOnlyList<double> AdjustPass(
            IEnumerable<double> idealCoordinates,
            IReadOnlyList<Tuple<double, double>> obstacles,
            double minLimit,
            double maxLimit,
            double minSpacing,
            double maxSpacing,
            double minEdge,
            double maxEdge,
            double increment,
            bool preserveIdealGridPhase)
        {
            var x = (idealCoordinates ?? Enumerable.Empty<double>()).ToList();
            if (x.Count == 0) return x;

            var gridOrigin = preserveIdealGridPhase ? x[0] : 0.0;
            var changed = true;
            var iteration = 0;

            while (changed && iteration < MaxIterations)
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

                    if (collision == null) continue;

                    var left = LatticeFloor(collision.Item1, gridOrigin, increment);
                    var right = LatticeCeil(collision.Item2, gridOrigin, increment);
                    x[index] = value - left <= right - value ? left : right;
                    changed = true;
                }

                for (var index = 0; index < x.Count; index++)
                {
                    var value = x[index];
                    if (index == 0)
                    {
                        if (value - minLimit < minEdge - Tol)
                        {
                            x[index] = LatticeCeil(minLimit + minEdge, gridOrigin, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var previous = x[index - 1];
                    if (value - previous < minSpacing - Tol)
                    {
                        value = LatticeCeil(previous + minSpacing, gridOrigin, increment);
                        x[index] = value;
                        changed = true;
                    }

                    if (value - previous > maxSpacing + Tol)
                    {
                        value = LatticeFloor(previous + maxSpacing, gridOrigin, increment);
                        x[index] = value;
                        changed = true;
                    }
                }

                for (var index = x.Count - 1; index >= 0; index--)
                {
                    var value = x[index];
                    if (index == x.Count - 1)
                    {
                        if (maxLimit - value < minEdge - Tol)
                        {
                            x[index] = LatticeFloor(maxLimit - minEdge, gridOrigin, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var next = x[index + 1];
                    if (next - value < minSpacing - Tol)
                    {
                        value = LatticeFloor(next - minSpacing, gridOrigin, increment);
                        x[index] = value;
                        changed = true;
                    }

                    if (next - value > maxSpacing + Tol)
                    {
                        value = LatticeCeil(next - maxSpacing, gridOrigin, increment);
                        x[index] = value;
                        changed = true;
                    }
                }

                iteration++;
            }

            x.Sort();
            return x;
        }

        private static bool GridHardValid(
            IEnumerable<double> values,
            double minLimit,
            double maxLimit,
            double maxSpacing,
            double maxEdge)
        {
            var ordered = (values ?? Enumerable.Empty<double>()).OrderBy(x => x).ToArray();
            if (ordered.Length == 0) return false;

            var firstEdge = ordered[0] - minLimit;
            var lastEdge = maxLimit - ordered[ordered.Length - 1];
            if (firstEdge < -Tol || firstEdge > maxEdge + Tol ||
                lastEdge < -Tol || lastEdge > maxEdge + Tol)
                return false;

            for (var index = 1; index < ordered.Length; index++)
            {
                var gap = ordered[index] - ordered[index - 1];
                if (gap <= Tol || gap > maxSpacing + Tol) return false;
            }
            return true;
        }

        private static bool Collides(double value, IEnumerable<Tuple<double, double>> obstacles)
            => (obstacles ?? Enumerable.Empty<Tuple<double, double>>())
                .Any(box => value > box.Item1 + Tol && value < box.Item2 - Tol);

        private static double LatticeFloor(double value, double origin, double increment)
            => origin + LispFloorMultiple(value - origin, increment);

        private static double LatticeCeil(double value, double origin, double increment)
            => origin + LispCeilMultiple(value - origin, increment);

        private static double LispFloorMultiple(double value, double increment)
        {
            var quotient = value / increment;
            var units = value < 0.0
                ? Math.Truncate(quotient - 0.9999)
                : Math.Truncate(quotient);
            return units * increment;
        }

        private static double LispCeilMultiple(double value, double increment)
        {
            var quotient = value / increment;
            var units = value < 0.0
                ? Math.Truncate(quotient)
                : Math.Truncate(quotient + 0.9999);
            return units * increment;
        }
    }
}
