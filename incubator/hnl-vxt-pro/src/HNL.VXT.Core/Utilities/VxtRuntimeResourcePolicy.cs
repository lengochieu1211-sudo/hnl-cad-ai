using System;

namespace HNL.VXT.Core.Utilities
{
    /// <summary>
    /// Environment-independent resource preference used by the real AutoCAD Runtime Golden probe.
    /// The preferred ISO linetype is optional because AutoCAD support-file installations differ
    /// between machines/languages. Missing optional support files must not invalidate geometry QA.
    /// </summary>
    public static class VxtRuntimeResourcePolicy
    {
        public const string PreferredProbeLinetype = "ACAD_ISO10W100";
        public const string ContinuousLinetype = "Continuous";
        public const string ByLayerLinetype = "ByLayer";

        public static string SelectMlineProbeLinetype(Func<string, bool> exists)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            if (exists(PreferredProbeLinetype)) return PreferredProbeLinetype;
            if (exists(ContinuousLinetype)) return ContinuousLinetype;
            if (exists(ByLayerLinetype)) return ByLayerLinetype;
            return string.Empty;
        }
    }
}
