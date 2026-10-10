namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Manual Create override is deliberately limited to final-plan layout constraints that can
    /// be corrected in CAD after materialization. Runtime/resource/validation/parity failures are
    /// not VxtConstraintDiagnostic items and remain fatal in VxtCreateEngine.
    /// </summary>
    public static class VxtConstraintOverridePolicy
    {
        public static bool IsManualOverrideAllowed(VxtConstraintDiagnostic diagnostic)
        {
            if (diagnostic == null || !diagnostic.IsHard) return false;

            switch (diagnostic.Kind)
            {
                case VxtConstraintKind.MaxSpacingHard:
                case VxtConstraintKind.MaxEdgeHard:
                case VxtConstraintKind.SpacingStepHard:
                case VxtConstraintKind.MissingCoverageHard:
                    return true;
                default:
                    return false;
            }
        }
    }
}
