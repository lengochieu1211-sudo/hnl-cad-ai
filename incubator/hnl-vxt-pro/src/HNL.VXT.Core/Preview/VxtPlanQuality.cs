namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Compact quality telemetry attached to a Pro preview/create plan.
    /// It is informational only; geometry legality is still enforced by the solvers.
    /// </summary>
    public sealed class VxtPlanQuality
    {
        public int QualityScore100 { get; set; }
        public int HardViolationCount { get; set; }
        public int CollisionCount { get; set; }
        public int MainCollisionCount { get; set; }
        public int FurringCollisionCount { get; set; }
        public int HangerCollisionCount { get; set; }
        public int AutoDirectionCandidateCount { get; set; }
        public double SelectedDirectionDegrees { get; set; }
        public double MainLength { get; set; }
        public double FurringLength { get; set; }
        public double MaterialIndex { get; set; }
        public double SortScore { get; set; }

        // Multi-boundary telemetry. A single-boundary Pro plan reports 1/1/100.
        public int BoundaryCount { get; set; } = 1;
        public int DistinctDirectionCount { get; set; } = 1;
        public int AlignmentScore100 { get; set; } = 100;
        public bool UsesSharedDirection { get; set; }

        public bool IsValid => HardViolationCount == 0;
        public bool IsClear => CollisionCount == 0;
    }
}
