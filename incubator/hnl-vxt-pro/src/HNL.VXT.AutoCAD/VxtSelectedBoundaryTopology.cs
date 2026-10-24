using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;

namespace HNL.VXT.AutoCAD
{
    /// <summary>
    /// Converts explicitly selected nested closed curves into drawable islands:
    /// even nesting levels are outer regions; odd levels are holes of the
    /// immediately enclosing even level. Disjoint boundaries stay independent.
    /// Only boundaries on the SAME layer nest, avoiding unrelated annotations.
    /// This shared adapter is used by explicit Select and PickFirst paths.
    /// </summary>
    internal static class VxtSelectedBoundaryTopology
    {
        internal static void Arrange(
            IList<Boundary2> source, IList<ObjectId> ids, IList<string> layers,
            out List<Boundary2> outers, out List<ObjectId> outerIds,
            out List<List<Boundary2>> holeGroups)
        {
            outers = new List<Boundary2>();
            outerIds = new List<ObjectId>();
            holeGroups = new List<List<Boundary2>>();
            if (source == null || ids == null || layers == null ||
                source.Count != ids.Count || source.Count != layers.Count)
                throw new ArgumentException("Invalid selected boundary inputs.");

            var areas = source.Select(x => Math.Abs(Area(x))).ToArray();
            var parents = new int[source.Count];
            var depths = new int[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                parents[i] = -1;
                var bestArea = double.PositiveInfinity;
                for (var j = 0; j < source.Count; j++)
                {
                    if (j == i || areas[j] <= areas[i] + 1e-3 ||
                        areas[j] >= bestArea ||
                        !string.Equals(layers[i], layers[j], StringComparison.OrdinalIgnoreCase) ||
                        source[i].Vertices.Count == 0) continue;
                    if (!Contains(source[j], source[i].Vertices[0])) continue;
                    parents[i] = j;
                    bestArea = areas[j];
                }
            }

            for (var i = 0; i < source.Count; i++)
            {
                var depth = 0;
                var cursor = parents[i];
                while (cursor >= 0 && depth <= source.Count)
                {
                    depth++;
                    cursor = parents[cursor];
                }
                if (depth > source.Count)
                    throw new InvalidOperationException("Nested boundary parent cycle.");
                depths[i] = depth;
            }

            var selectedToOuter = new Dictionary<int, int>();
            for (var i = 0; i < source.Count; i++)
            {
                if (depths[i] % 2 != 0) continue;
                selectedToOuter[i] = outers.Count;
                outers.Add(source[i]);
                outerIds.Add(ids[i]);
                holeGroups.Add(new List<Boundary2>());
            }
            for (var i = 0; i < source.Count; i++)
            {
                if (depths[i] % 2 == 0) continue;
                var parent = parents[i];
                if (parent >= 0 && selectedToOuter.TryGetValue(parent, out var groupIndex))
                    holeGroups[groupIndex].Add(source[i]);
            }
        }

        private static double Area(Boundary2 boundary)
        {
            if (boundary == null || boundary.Vertices.Count < 3) return 0;
            double twiceArea = 0;
            for (var i = 0; i < boundary.Vertices.Count; i++)
            {
                var a = boundary.Vertices[i];
                var b = boundary.Vertices[(i + 1) % boundary.Vertices.Count];
                twiceArea += a.X * b.Y - b.X * a.Y;
            }
            return twiceArea * 0.5;
        }

        private static bool Contains(Boundary2 loop, Point2 p)
        {
            if (loop == null || loop.Vertices.Count < 3) return false;
            var inside = false;
            for (var i = 0; i < loop.Vertices.Count; i++)
            {
                var a = loop.Vertices[i];
                var b = loop.Vertices[(i + 1) % loop.Vertices.Count];
                if ((a.Y > p.Y) == (b.Y > p.Y)) continue;
                var x = a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (x > p.X) inside = !inside;
            }
            return inside;
        }
    }
}
