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
            double increment)
            => AdjustGridCore(
                idealCoordinates, obstacleIntervals,
                minLimit, maxLimit, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                preserveIdealGridPhase: true);

        /// <summary>
        /// Exact V6.7.2 adjust-grid snapping semantics: floor/ceil are taken against absolute
        /// WCS zero, not the initial ceiling-grid phase. XP Legacy uses this path so its
        /// avoidance behavior matches the original Lisp exactly. XC intentionally keeps the
        /// field-proven phase-preserving correction through AdjustGrid().
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
            double increment)
            => AdjustGridCore(
                idealCoordinates, obstacleIntervals,
                minLimit, maxLimit, minSpacing, maxSpacing, minEdge, maxEdge, increment,
                preserveIdealGridPhase: false);

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
            bool preserveIdealGridPhase)
        {
            var x = (idealCoordinates ?? Enumerable.Empty<double>()).ToList();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();

            if (x.Count == 0 || obstacles.Count == 0)
                return x;

            if (increment <= 0.0)
                increment = 1.0;

            // XC/Ty can preserve the generated grid phase. XP Legacy can explicitly request
            // the original Lisp behavior, where math-floor-mult/math-ceil-mult snap to WCS zero.
            var gridOrigin = preserveIdealGridPhase ? x[0] : 0.0;

            var changed = true;
            var iteration = 0;

            while (changed && iteration < MaxIterations)
            {
                changed = false;

                // Lisp: foreach xi X, keep the last matching collision box, then snap to
                // the nearer multiple-aligned side of that box. A tie chooses the left side.
                // gridOrigin=0 reproduces V6.7.2 exactly; gridOrigin=x[0] is the XC phase fix.
                for (var i = 0; i < x.Count; i++)
                {
                    var value = x[i];
                    Tuple<double, double> collision = null;
                    foreach (var box in obstacles)
                    {
                        if (value > box.Item1 + Tol && value < box.Item2 - Tol)
                            collision = box;
                    }

                    if (collision == null)
                        continue;

                    var left = LatticeFloor(collision.Item1, gridOrigin, increment);
                    var right = LatticeCeil(collision.Item2, gridOrigin, increment);
                    x[i] = value - left <= right - value ? left : right;
                    changed = true;
                }

                // Forward pass: first-edge minimum, then Min/Max spacing from the previous item.
                for (var i = 0; i < x.Count; i++)
                {
                    var value = x[i];
                    if (i == 0)
                    {
                        if (value - minLimit < minEdge - Tol)
                        {
                            x[i] = LatticeCeil(minLimit + minEdge, gridOrigin, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var previous = x[i - 1];
                    if (value - previous < minSpacing - Tol)
                    {
                        value = LatticeCeil(previous + minSpacing, gridOrigin, increment);
                        x[i] = value;
                        changed = true;
                    }

                    if (value - previous > maxSpacing + Tol)
                    {
                        value = LatticeFloor(previous + maxSpacing, gridOrigin, increment);
                        x[i] = value;
                        changed = true;
                    }
                }

                // Backward pass: last-edge minimum, then Min/Max spacing from the next item.
                for (var i = x.Count - 1; i >= 0; i--)
                {
                    var value = x[i];
                    if (i == x.Count - 1)
                    {
                        if (maxLimit - value < minEdge - Tol)
                        {
                            x[i] = LatticeFloor(maxLimit - minEdge, gridOrigin, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var next = x[i + 1];
                    if (next - value < minSpacing - Tol)
                    {
                        value = LatticeFloor(next - minSpacing, gridOrigin, increment);
                        x[i] = value;
                        changed = true;
                    }

                    if (next - value > maxSpacing + Tol)
                    {
                        value = LatticeCeil(next - maxSpacing, gridOrigin, increment);
                        x[i] = value;
                        changed = true;
                    }
                }

                iteration++;
            }

            // V6.7.2 adjust-grid returns X unconditionally. Do not fail closed here.
            return x;
        }

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
