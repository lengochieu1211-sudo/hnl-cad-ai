using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HNL.VXT.Core.Layout;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Adapts a selected XC/XP block to a requested member length without blindly
    /// writing that length into every writable Double property. This specifically
    /// protects dynamic blocks that contain Array/Count/Spacing actions.
    /// </summary>
    internal static class VxtDynamicBlockAdapter
    {
        private static readonly Dictionary<string, string> PreferredProperties =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string InspectAndRemember(BlockReference br, string effectiveName)
        {
            if (br == null || string.IsNullOrWhiteSpace(effectiveName)) return string.Empty;
            var referenceLength = GetVisibleLength(br);
            var candidate = FindBestProperty(br, referenceLength);
            if (!string.IsNullOrWhiteSpace(candidate)) PreferredProperties[effectiveName] = candidate;
            else PreferredProperties.Remove(effectiveName);
            return candidate ?? string.Empty;
        }

        public static string ApplyMemberLength(BlockReference br, string effectiveName, double targetLength)
        {
            if (br == null || targetLength <= 1e-8) return "none";

            string preferred = null;
            if (!string.IsNullOrWhiteSpace(effectiveName))
                PreferredProperties.TryGetValue(effectiveName, out preferred);

            if (TrySetDynamicLength(br, targetLength, preferred, out var usedProperty))
            {
                if (!string.IsNullOrWhiteSpace(effectiveName) && !string.IsNullOrWhiteSpace(usedProperty))
                    PreferredProperties[effectiveName] = usedProperty;
                return "dynamic:" + usedProperty;
            }

            // Safe fallback for static/ambiguous blocks: scale only the block's local X axis.
            // It does not mutate Array Count/Spacing parameters and therefore cannot explode an
            // associative/dynamic array. Dynamic blocks with a recognized length property never
            // reach this fallback.
            var currentLength = GetVisibleLength(br);
            if (currentLength > 1e-8)
            {
                try
                {
                    var scale = br.ScaleFactors;
                    var factor = targetLength / currentLength;
                    br.ScaleFactors = new Scale3d(scale.X * factor, scale.Y, scale.Z);
                    return "xscale";
                }
                catch
                {
                    // Caller may fall back to plain geometry if desired.
                }
            }

            return "unchanged";
        }

        public static bool IsArraySensitive(BlockReference br)
        {
            if (br == null) return false;
            try
            {
                if (!br.IsDynamicBlock) return false;
                foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
                {
                    if (DynamicBlockPropertyPolicy.IsRisky(prop.PropertyName, SafeDescription(prop))) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TrySetDynamicLength(
            BlockReference br,
            double targetLength,
            string preferred,
            out string usedProperty)
        {
            usedProperty = string.Empty;
            try
            {
                if (!br.IsDynamicBlock) return false;
                var props = br.DynamicBlockReferencePropertyCollection
                    .Cast<DynamicBlockReferenceProperty>()
                    .Where(p => !p.ReadOnly && p.Value is double &&
                                !DynamicBlockPropertyPolicy.IsRisky(p.PropertyName, SafeDescription(p)))
                    .ToList();
                if (props.Count == 0) return false;

                DynamicBlockReferenceProperty chosen = null;
                if (!string.IsNullOrWhiteSpace(preferred))
                    chosen = props.FirstOrDefault(p =>
                        string.Equals(p.PropertyName, preferred, StringComparison.OrdinalIgnoreCase));

                if (chosen == null)
                {
                    var referenceLength = GetVisibleLength(br);
                    chosen = props
                        .Select(p => new
                        {
                            Property = p,
                            Score = DynamicBlockPropertyPolicy.ScoreLengthProperty(
                                p.PropertyName, SafeDescription(p), Convert.ToDouble(p.Value), referenceLength)
                        })
                        .Where(x => x.Score >= 45)
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => x.Property.PropertyName, StringComparer.OrdinalIgnoreCase)
                        .Select(x => x.Property)
                        .FirstOrDefault();
                }

                if (chosen == null) return false;
                chosen.Value = targetLength;
                usedProperty = chosen.PropertyName;
                try { br.RecordGraphicsModified(true); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FindBestProperty(BlockReference br, double referenceLength)
        {
            try
            {
                if (!br.IsDynamicBlock) return string.Empty;
                return br.DynamicBlockReferencePropertyCollection
                    .Cast<DynamicBlockReferenceProperty>()
                    .Where(p => !p.ReadOnly && p.Value is double)
                    .Select(p => new
                    {
                        Property = p,
                        Score = DynamicBlockPropertyPolicy.ScoreLengthProperty(
                            p.PropertyName, SafeDescription(p), Convert.ToDouble(p.Value), referenceLength)
                    })
                    .Where(x => x.Score >= 45)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Property.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.Property.PropertyName)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static double GetVisibleLength(BlockReference br)
        {
            try
            {
                var ext = br.GeometricExtents;
                var dir = new Vector2d(Math.Cos(br.Rotation), Math.Sin(br.Rotation));
                var corners = new[]
                {
                    new Point2d(ext.MinPoint.X, ext.MinPoint.Y),
                    new Point2d(ext.MinPoint.X, ext.MaxPoint.Y),
                    new Point2d(ext.MaxPoint.X, ext.MinPoint.Y),
                    new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y)
                };
                var values = corners.Select(p => p.X * dir.X + p.Y * dir.Y).ToArray();
                return values.Max() - values.Min();
            }
            catch
            {
                return 0.0;
            }
        }

        private static string SafeDescription(DynamicBlockReferenceProperty prop)
        {
            try { return prop.Description ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
