using System;
using System.Linq;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Metrics derived strictly from the final post-processed plan that Preview and Create share.
    /// These values intentionally ignore builder bookkeeping counters so concave split/merge,
    /// MEP avoidance and other finalizers are reflected exactly in the displayed quantities.
    /// </summary>
    public sealed class VxtFinalPlanMetrics
    {
        private VxtFinalPlanMetrics() { }

        public int MainCount { get; private set; }
        public int FurringCount { get; private set; }
        public int HangerCount { get; private set; }
        public int DimensionCount { get; private set; }
        public double MainLengthMm { get; private set; }
        public double FurringLengthMm { get; private set; }

        public double MainLengthM => MainLengthMm / 1000.0;
        public double FurringLengthM => FurringLengthMm / 1000.0;
        public double TotalFrameLengthM => (MainLengthMm + FurringLengthMm) / 1000.0;

        public static VxtFinalPlanMetrics FromPlan(VxtPreviewPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var main = plan.Lines.Where(x => x.Kind == PreviewLineKind.Main).ToList();
            var furring = plan.Lines.Where(x => x.Kind == PreviewLineKind.Furring).ToList();

            return new VxtFinalPlanMetrics
            {
                // Create iterates these exact final PreviewLine collections and produces one
                // XC/XP entity (block, polyline or Mline fallback) for each item.
                MainCount = main.Count,
                FurringCount = furring.Count,
                HangerCount = plan.HangerPoints.Count,
                DimensionCount = plan.Dimensions.Count,
                MainLengthMm = main.Sum(Length),
                FurringLengthMm = furring.Sum(Length)
            };
        }

        private static double Length(PreviewLine line)
            => line.A.DistanceTo(line.B);
    }
}
