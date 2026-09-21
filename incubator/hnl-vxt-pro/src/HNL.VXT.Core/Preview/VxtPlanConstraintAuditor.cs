using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Audits the exact final geometry shared by Preview/Create.
    /// Max spacing/Max edge and spacing-lattice rules are HARD.
    /// Min spacing/Min edge are SOFT and are reported per selected ceiling Mxx.
    /// </summary>
    public static class VxtPlanConstraintAuditor
    {
        private const double Tol = 0.1;
        private const double SampleTol = 0.5;

        public static void Attach(
            Boundary2 boundary,
            VxtPreviewPlan plan,
            VxtSettings settings,
            double angleDegrees,
            int boundaryIndex)
        {
            if (boundary == null || plan == null || settings == null) return;

            var collected = new Dictionary<string, VxtConstraintDiagnostic>(StringComparer.Ordinal);

            if (settings.DrawMain && settings.MainDirection != MainDirectionMode.RectangleRegions)
                AuditMain(boundary, plan, settings, angleDegrees, boundaryIndex, collected);

            if (settings.DrawHangers)
                AuditHangers(plan, settings, boundaryIndex, collected);

            foreach (var item in collected.Values
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Target)
                .ThenBy(x => x.Kind))
            {
                plan.Diagnostics.Add(item);
            }
        }

        private static void AuditMain(
            Boundary2 boundary,
            VxtPreviewPlan plan,
            VxtSettings settings,
            double angleDegrees,
            int boundaryIndex,
            IDictionary<string, VxtConstraintDiagnostic> output)
        {
            var radians = Normalize180(angleDegrees) * Math.PI / 180.0;
            var polygon = boundary.Vertices.Select(p => Transform2.ToLocal(p, radians)).ToList();
            if (polygon.Count < 3) return;

            var bounds = Box2.FromPoints(polygon);

            // The production builder intentionally omits XC for a whole ceiling region when
            // "Bỏ XC nếu ngắn hơn" (MainSkipLimit) applies. Auditing that intentional omission as
            // MissingCoverageHard contradicts the solver and can block Create for a valid setting.
            if (settings.MainSkipLimit > Tol &&
                (bounds.Width <= settings.MainSkipLimit + Tol ||
                 bounds.Height <= settings.MainSkipLimit + Tol))
                return;

            var mains = plan.Lines
                .Where(x => x.Kind == PreviewLineKind.Main)
                .Select(x =>
                {
                    var a = Transform2.ToLocal(x.A, radians);
                    var b = Transform2.ToLocal(x.B, radians);
                    return new
                    {
                        A = a,
                        B = b,
                        Y = (a.Y + b.Y) * 0.5,
                        X1 = Math.Min(a.X, b.X),
                        X2 = Math.Max(a.X, b.X),
                        Horizontal = Math.Abs(a.Y - b.Y) <= SampleTol
                    };
                })
                .Where(x => x.Horizontal)
                .ToList();

            if (mains.Count == 0)
            {
                AddWorst(output, new VxtConstraintDiagnostic(
                    boundaryIndex, VxtConstraintTarget.Main,
                    VxtConstraintKind.MissingCoverageHard, VxtConstraintSeverity.Error, 0.0, 0.0));
                return;
            }

            var xs = UniqueSort(polygon.Select(p => p.X)
                .Concat(new[] { bounds.MinX, bounds.MaxX }));

            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var x1 = Math.Max(bounds.MinX, xs[i]);
                var x2 = Math.Min(bounds.MaxX, xs[i + 1]);
                if (x2 - x1 <= 1.0) continue;

                // "Chiều dài XC tối thiểu" is HARD in the construction contract: a local notch
                // XC shorter than MinLocalMainLength must NOT be added. Therefore a real notch band
                // narrower than that limit cannot simultaneously be treated as a Create-blocking
                // Max-edge/MissingCoverage violation; doing so makes the two hard rules impossible
                // to satisfy together. The base/global grid is still audited everywhere else.
                if (IsIntentionallySkippedShortNotchBand(
                        polygon, bounds, x1, x2, settings))
                    continue;

                var samples = new[]
                {
                    x1 + 0.25 * (x2 - x1),
                    x1 + 0.50 * (x2 - x1),
                    x1 + 0.75 * (x2 - x1)
                };

                foreach (var x in samples)
                {
                    foreach (var interval in PolygonScanline.ClipVertical(polygon, x))
                    {
                        var a = Math.Min(interval.A.Y, interval.B.Y);
                        var b = Math.Max(interval.A.Y, interval.B.Y);
                        if (b - a <= 1.0) continue;

                        var ys = UniqueSort(mains
                            .Where(m => x >= m.X1 - SampleTol && x <= m.X2 + SampleTol &&
                                        m.Y >= a - SampleTol && m.Y <= b + SampleTol)
                            .Select(m => m.Y));

                        if (ys.Count == 0)
                        {
                            AddWorst(output, new VxtConstraintDiagnostic(
                                boundaryIndex, VxtConstraintTarget.Main,
                                VxtConstraintKind.MissingCoverageHard, VxtConstraintSeverity.Error, 0.0, 0.0));
                            continue;
                        }

                        AuditEdge(
                            ys[0] - a,
                            settings.MainMinEdgeOffset,
                            settings.MainMaxEdgeOffset,
                            boundaryIndex,
                            VxtConstraintTarget.Main,
                            output);
                        AuditEdge(
                            b - ys[ys.Count - 1],
                            settings.MainMinEdgeOffset,
                            settings.MainMaxEdgeOffset,
                            boundaryIndex,
                            VxtConstraintTarget.Main,
                            output);

                        for (var j = 0; j + 1 < ys.Count; j++)
                        {
                            var gap = ys[j + 1] - ys[j];
                            AuditSpacing(
                                gap,
                                settings.MainMinSpacing,
                                settings.MainMaxSpacing,
                                settings.MainBalanceStep,
                                boundaryIndex,
                                VxtConstraintTarget.Main,
                                output);
                        }
                    }
                }
            }
        }

        private static bool IsIntentionallySkippedShortNotchBand(
            IReadOnlyList<Point2> polygon,
            Box2 bounds,
            double x1,
            double x2,
            VxtSettings settings)
        {
            if (!settings.UseLocalMainAdd ||
                settings.MinLocalMainLength <= Tol ||
                x2 - x1 >= settings.MinLocalMainLength - Tol)
                return false;

            var samples = new[]
            {
                x1 + 0.25 * (x2 - x1),
                x1 + 0.50 * (x2 - x1),
                x1 + 0.75 * (x2 - x1)
            };

            var hasDrawable = false;
            var realNotch = false;
            foreach (var x in samples)
            {
                var intervals = PolygonScanline.ClipVertical(polygon, x).ToList();
                if (intervals.Count == 0) continue;

                hasDrawable = true;
                if (intervals.Count != 1)
                {
                    realNotch = true;
                    continue;
                }

                var a = Math.Min(intervals[0].A.Y, intervals[0].B.Y);
                var b = Math.Max(intervals[0].A.Y, intervals[0].B.Y);
                if (a > bounds.MinY + SampleTol || b < bounds.MaxY - SampleTol)
                    realNotch = true;
            }

            return hasDrawable && realNotch;
        }

        private static void AuditHangers(
            VxtPreviewPlan plan,
            VxtSettings settings,
            int boundaryIndex,
            IDictionary<string, VxtConstraintDiagnostic> output)
        {
            var mains = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();
            foreach (var main in mains)
            {
                var dx = main.B.X - main.A.X;
                var dy = main.B.Y - main.A.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length <= 1.0) continue;

                var ux = dx / length;
                var uy = dy / length;
                var values = new List<double>();

                foreach (var p in plan.HangerPoints)
                {
                    var vx = p.X - main.A.X;
                    var vy = p.Y - main.A.Y;
                    var along = vx * ux + vy * uy;
                    var perpendicular = Math.Abs(vx * uy - vy * ux);
                    if (perpendicular <= SampleTol &&
                        along >= -SampleTol && along <= length + SampleTol)
                        values.Add(Math.Max(0.0, Math.Min(length, along)));
                }

                values = UniqueSort(values);
                if (values.Count == 0)
                {
                    AddWorst(output, new VxtConstraintDiagnostic(
                        boundaryIndex, VxtConstraintTarget.Hanger,
                        VxtConstraintKind.MissingCoverageHard, VxtConstraintSeverity.Error, 0.0, 0.0));
                    continue;
                }

                AuditEdge(
                    values[0],
                    settings.HangerMinEdgeOffset,
                    settings.HangerMaxEdgeOffset,
                    boundaryIndex,
                    VxtConstraintTarget.Hanger,
                    output);
                AuditEdge(
                    length - values[values.Count - 1],
                    settings.HangerMinEdgeOffset,
                    settings.HangerMaxEdgeOffset,
                    boundaryIndex,
                    VxtConstraintTarget.Hanger,
                    output);

                for (var i = 0; i + 1 < values.Count; i++)
                {
                    AuditSpacing(
                        values[i + 1] - values[i],
                        settings.HangerMinSpacing,
                        settings.HangerMaxSpacing,
                        settings.HangerBalanceStep,
                        boundaryIndex,
                        VxtConstraintTarget.Hanger,
                        output);
                }
            }
        }

        private static void AuditEdge(
            double actual,
            double min,
            double max,
            int boundaryIndex,
            VxtConstraintTarget target,
            IDictionary<string, VxtConstraintDiagnostic> output)
        {
            if (actual > max + Tol)
            {
                AddWorst(output, new VxtConstraintDiagnostic(
                    boundaryIndex, target, VxtConstraintKind.MaxEdgeHard,
                    VxtConstraintSeverity.Error, actual, max));
                return;
            }

            if (actual < min - Tol)
            {
                AddWorst(output, new VxtConstraintDiagnostic(
                    boundaryIndex, target, VxtConstraintKind.MinEdgeSoft,
                    VxtConstraintSeverity.Warning, actual, min));
            }
        }

        private static void AuditSpacing(
            double actual,
            double min,
            double max,
            double step,
            int boundaryIndex,
            VxtConstraintTarget target,
            IDictionary<string, VxtConstraintDiagnostic> output)
        {
            if (actual > max + Tol)
            {
                AddWorst(output, new VxtConstraintDiagnostic(
                    boundaryIndex, target, VxtConstraintKind.MaxSpacingHard,
                    VxtConstraintSeverity.Error, actual, max));
            }
            else if (actual < min - Tol)
            {
                AddWorst(output, new VxtConstraintDiagnostic(
                    boundaryIndex, target, VxtConstraintKind.MinSpacingSoft,
                    VxtConstraintSeverity.Warning, actual, min));
            }

            if (step > Tol)
            {
                var nearest = Math.Round(actual / step) * step;
                if (Math.Abs(actual - nearest) > Tol)
                {
                    AddWorst(output, new VxtConstraintDiagnostic(
                        boundaryIndex, target, VxtConstraintKind.SpacingStepHard,
                        VxtConstraintSeverity.Error, actual, step));
                }
            }
        }

        private static void AddWorst(
            IDictionary<string, VxtConstraintDiagnostic> output,
            VxtConstraintDiagnostic candidate)
        {
            var key = ((int)candidate.Target).ToString() + "|" + ((int)candidate.Kind).ToString();
            VxtConstraintDiagnostic current;
            if (!output.TryGetValue(key, out current) ||
                candidate.Difference > current.Difference + Tol)
                output[key] = candidate;
        }

        private static List<double> UniqueSort(IEnumerable<double> values)
        {
            var result = new List<double>();
            foreach (var value in (values ?? Enumerable.Empty<double>()).OrderBy(x => x))
            {
                if (result.Count == 0 || Math.Abs(result[result.Count - 1] - value) > Tol)
                    result.Add(value);
            }
            return result;
        }

        private static double Normalize180(double value)
        {
            value %= 180.0;
            return value < 0.0 ? value + 180.0 : value;
        }
    }
}
