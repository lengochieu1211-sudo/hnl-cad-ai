using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HNL.VXT.Core.Preview
{
    public enum VxtConstraintSeverity
    {
        Warning = 0,
        Error = 1
    }

    public enum VxtConstraintTarget
    {
        Main = 0,
        Hanger = 1
    }

    public enum VxtConstraintKind
    {
        MinSpacingSoft = 0,
        MinEdgeSoft = 1,
        MaxSpacingHard = 2,
        MaxEdgeHard = 3,
        SpacingStepHard = 4,
        MissingCoverageHard = 5
    }

    public sealed class VxtConstraintDiagnostic
    {
        public VxtConstraintDiagnostic(
            int boundaryIndex,
            VxtConstraintTarget target,
            VxtConstraintKind kind,
            VxtConstraintSeverity severity,
            double actualValue,
            double limitValue)
        {
            BoundaryIndex = Math.Max(0, boundaryIndex);
            Target = target;
            Kind = kind;
            Severity = severity;
            ActualValue = actualValue;
            LimitValue = limitValue;
        }

        public int BoundaryIndex { get; }
        public VxtConstraintTarget Target { get; }
        public VxtConstraintKind Kind { get; }
        public VxtConstraintSeverity Severity { get; }
        public double ActualValue { get; }
        public double LimitValue { get; }

        public string BoundaryCode => "M" + (BoundaryIndex + 1).ToString("00", CultureInfo.InvariantCulture);
        public bool IsHard => Severity == VxtConstraintSeverity.Error;

        public double Difference
        {
            get
            {
                switch (Kind)
                {
                    case VxtConstraintKind.MinSpacingSoft:
                    case VxtConstraintKind.MinEdgeSoft:
                        return Math.Max(0.0, LimitValue - ActualValue);
                    case VxtConstraintKind.MaxSpacingHard:
                    case VxtConstraintKind.MaxEdgeHard:
                        return Math.Max(0.0, ActualValue - LimitValue);
                    case VxtConstraintKind.SpacingStepHard:
                        if (LimitValue <= 0.0) return 0.0;
                        return Math.Abs(ActualValue - Math.Round(ActualValue / LimitValue) * LimitValue);
                    default:
                        return 0.0;
                }
            }
        }

        public string DisplayText
        {
            get
            {
                var system = Target == VxtConstraintTarget.Main ? "XC" : "Ty";
                switch (Kind)
                {
                    case VxtConstraintKind.MinSpacingSoft:
                        return BoundaryCode + " • " + system + " Min spacing: " +
                               Num(ActualValue) + " mm < " + Num(LimitValue) +
                               " mm, giảm " + Num(Difference) + " mm";
                    case VxtConstraintKind.MinEdgeSoft:
                        return BoundaryCode + " • " + system + " Min biên: " +
                               Num(ActualValue) + " mm < " + Num(LimitValue) +
                               " mm, giảm " + Num(Difference) + " mm";
                    case VxtConstraintKind.MaxSpacingHard:
                        return BoundaryCode + " • " + system + " Max spacing HARD: " +
                               Num(ActualValue) + " mm > " + Num(LimitValue) +
                               " mm, vượt " + Num(Difference) + " mm";
                    case VxtConstraintKind.MaxEdgeHard:
                        return BoundaryCode + " • " + system + " Max biên HARD: " +
                               Num(ActualValue) + " mm > " + Num(LimitValue) +
                               " mm, vượt " + Num(Difference) + " mm";
                    case VxtConstraintKind.SpacingStepHard:
                        return BoundaryCode + " • " + system + " bội số HARD: " +
                               Num(ActualValue) + " mm không đúng bước " + Num(LimitValue) +
                               " mm, lệch " + Num(Difference) + " mm";
                    case VxtConstraintKind.MissingCoverageHard:
                        return BoundaryCode + " • " + system +
                               " HARD: không đủ phần tử để thỏa điều kiện Max";
                    default:
                        return BoundaryCode + " • " + system + " constraint";
                }
            }
        }

        private static string Num(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static class VxtConstraintReport
    {
        public static string Format(IEnumerable<VxtConstraintDiagnostic> diagnostics)
        {
            var list = (diagnostics ?? Enumerable.Empty<VxtConstraintDiagnostic>())
                .Where(x => x != null)
                .OrderBy(x => x.BoundaryIndex)
                .ThenByDescending(x => x.Severity)
                .ThenBy(x => x.Target)
                .ThenBy(x => x.Kind)
                .ToList();
            return list.Count == 0 ? string.Empty : string.Join(" | ", list.Select(x => x.DisplayText));
        }
    }
}
