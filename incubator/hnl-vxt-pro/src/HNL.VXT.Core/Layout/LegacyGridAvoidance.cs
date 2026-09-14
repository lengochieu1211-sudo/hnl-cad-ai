using System;
using System.Collections.Generic;
using System.Linq;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Exact best-effort port of the V6.7.2 Lisp adjust-grid routine.
    ///
    /// Important: unlike the Pro repair path, the legacy Lisp never discards the whole grid
    /// when the iterative repair cannot produce a perfectly valid obstacle-free solution.
    /// It performs at most 100 forward/backward balancing passes and then returns the current
    /// coordinates. Keep this helper isolated to the Legacy builder so Pro hard-validation
    /// semantics remain unchanged.
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
        {
            var x = (idealCoordinates ?? Enumerable.Empty<double>()).ToList();
            var obstacles = (obstacleIntervals ?? Enumerable.Empty<Tuple<double, double>>()).ToList();

            if (x.Count == 0 || obstacles.Count == 0)
                return x;

            if (increment <= 0.0)
                increment = 1.0;

            var changed = true;
            var iteration = 0;

            while (changed && iteration < MaxIterations)
            {
                changed = false;

                // Lisp: foreach xi X, keep the last matching collision box, then snap to
                // the nearer multiple-aligned side of that box. A tie chooses the left side.
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

                    var left = LispFloorMultiple(collision.Item1, increment);
                    var right = LispCeilMultiple(collision.Item2, increment);
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
                            x[i] = LispCeilMultiple(minLimit + minEdge, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var previous = x[i - 1];
                    if (value - previous < minSpacing - Tol)
                    {
                        value = LispCeilMultiple(previous + minSpacing, increment);
                        x[i] = value;
                        changed = true;
                    }

                    if (value - previous > maxSpacing + Tol)
                    {
                        value = LispFloorMultiple(previous + maxSpacing, increment);
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
                            x[i] = LispFloorMultiple(maxLimit - minEdge, increment);
                            changed = true;
                        }
                        continue;
                    }

                    var next = x[i + 1];
                    if (next - value < minSpacing - Tol)
                    {
                        value = LispFloorMultiple(next - minSpacing, increment);
                        x[i] = value;
                        changed = true;
                    }

                    if (next - value > maxSpacing + Tol)
                    {
                        value = LispCeilMultiple(next - maxSpacing, increment);
                        x[i] = value;
                        changed = true;
                    }
                }

                iteration++;
            }

            // V6.7.2 adjust-grid returns X unconditionally. Do not fail closed here.
            return x;
        }

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
