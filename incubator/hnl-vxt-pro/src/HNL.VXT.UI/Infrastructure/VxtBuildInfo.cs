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
        private const string MetadataKey = "HNLBuildDateTime";
        private static readonly string _buildText = ResolveBuildText();

        public static string BuildText => _buildText;
        public static string VersionLabel => Version + " • Cập nhật " + BuildText;
        // AutoCAD's own PaletteSet title is the only product heading.
        // Show the exact embedded build time, without duplicating the version
        // or adding a second logo/title panel inside the palette.
        public static string PaletteTitle => "HNL Ceiling Framing Pro • " + BuildText;

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

                // Directory.Build.props stamps dd/MM/yyyy HH:mm in HNL local time (UTC+7).
                // Avoid treating that value as UTC or accidentally showing the workstation
                // clock: the title must identify the installed binary build exactly.
                DateTime localBuild;
                if (DateTime.TryParseExact(
                    metadata.Value, "dd/MM/yyyy HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out localBuild))
                    return localBuild.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

                return metadata.Value;
            }

            return "--/--/---- --:--";
        }
    }
}
