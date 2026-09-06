using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Pure workflow gate shared by UI/tests. V6.7.2 allows one important no-boundary
    /// entry path: XC off + XP off + Ty on + Auto DIM off. Pressing Create then asks the
    /// user to select existing XC and lays Ty on those members.
    /// </summary>
    public static class VxtWorkflowEligibility
    {
        public static bool HasAnyTask(VxtSettings settings)
        {
            return settings != null &&
                   (settings.DrawMain || settings.DrawFurring || settings.DrawHangers || settings.AutoDimension);
        }

        public static bool IsManualHangerOnlyStart(VxtSettings settings)
        {
            return settings != null &&
                   !settings.DrawMain &&
                   !settings.DrawFurring &&
                   settings.DrawHangers &&
                   !settings.AutoDimension;
        }

        public static bool CanStartCreate(bool hasBoundary, VxtSettings settings)
        {
            if (settings == null || !HasAnyTask(settings)) return false;
            string error;
            if (!settings.IsValid(out error)) return false;
            return hasBoundary || IsManualHangerOnlyStart(settings);
        }
    }
}
