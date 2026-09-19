using System;

namespace HNL.VXT.Core.Geometry
{
    /// <summary>
    /// Shared HNL VXT boundary-input tolerance rules.
    /// The AutoCAD adapter uses these values to normalize harmless drafting noise
    /// without modifying the source DWG entities.
    /// </summary>
    public static class BoundaryInputTolerancePolicy
    {
        public const double AutoCloseGapTolerance = 1.0;
        public const double ZTolerance = 0.01;

        public static bool AcceptOpenGap(double gap)
        {
            return !double.IsNaN(gap) &&
                   !double.IsInfinity(gap) &&
                   gap >= 0.0 &&
                   gap <= AutoCloseGapTolerance;
        }

        public static bool AcceptZ(double maxAbsZ)
        {
            return !double.IsNaN(maxAbsZ) &&
                   !double.IsInfinity(maxAbsZ) &&
                   maxAbsZ >= 0.0 &&
                   maxAbsZ <= ZTolerance;
        }

        public static bool IsTinyNonZeroZ(double maxAbsZ)
        {
            return AcceptZ(maxAbsZ) && maxAbsZ > 1e-12;
        }
    }
}
