using System;
using System.Globalization;
using System.Reflection;

namespace HNL.VXT.UI.Infrastructure
{
    /// <summary>
    /// Single visible version/build stamp for the palette header, PaletteSet title and
    /// AutoCAD load message. The timestamp is stamped into HNL.VXT.UI at build time and
    /// converted to the workstation local time when displayed.
    /// </summary>
    public static class VxtBuildInfo
    {
        public const string Version = "v7.0.0-beta.1";
        private const string MetadataKey = "HnlVxtBuildUtc";
        private static readonly string _buildText = ResolveBuildText();

        public static string BuildText => _buildText;
        public static string VersionLabel => "VXT Pro " + Version + " • Build " + BuildText;
        public static string PaletteTitle => "HNL Tool - " + VersionLabel;

        private static string ResolveBuildText()
        {
            var assembly = typeof(VxtBuildInfo).Assembly;
            foreach (var attribute in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
            {
                var metadata = attribute as AssemblyMetadataAttribute;
                if (metadata == null ||
                    !string.Equals(metadata.Key, MetadataKey, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(metadata.Value))
                    continue;

                DateTimeOffset utc;
                if (DateTimeOffset.TryParseExact(
                    metadata.Value,
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out utc))
                {
                    return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                }

                return metadata.Value;
            }

            return "--/--/---- --:--";
        }
    }
}
