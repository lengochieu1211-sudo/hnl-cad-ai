using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Preview;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// Geometry-only DIM chain packer for the Pro engine.
    /// Removes duplicate segments and separates only chains whose projected spans overlap.
    /// Extension points remain untouched; only DimensionLinePoint is moved to another row.
    /// </summary>
    public static class VxtProDimensionPacker
    {
        private const double Tol = 0.1;

        public sealed class Result
        {
            internal Result(IReadOnlyList<PreviewDimension> dimensions, int duplicateCount, int shiftedChainCount)
            {
                Dimensions = dimensions ?? Array.Empty<PreviewDimension>();
                RemovedDuplicateCount = duplicateCount;
                ShiftedChainCount = shiftedChainCount;
            }

            public IReadOnlyList<PreviewDimension> Dimensions { get; }
            public int RemovedDuplicateCount { get; }
            public int ShiftedChainCount { get; }
        }

        private sealed class IndexedDimension
        {
            public int Index;
            public PreviewDimension Dimension;
        }

        private sealed class Chain
        {
            public double Rotation;
            public double NormalCoordinate;
            public double TangentMin;
            public double TangentMax;
            public readonly List<IndexedDimension> Items = new List<IndexedDimension>();
        }

        private sealed class PlacedChain
        {
            public Chain Chain;
            public double NormalCoordinate;
        }

        public static Result Pack(IEnumerable<PreviewDimension> source, double minimumRowSpacing)
        {
            var input = (source ?? Enumerable.Empty<PreviewDimension>()).ToList();
            if (input.Count == 0)
                return new Result(Array.Empty<PreviewDimension>(), 0, 0);

            var unique = new List<IndexedDimension>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < input.Count; i++)
            {
                var key = SegmentKey(input[i]);
                if (!seen.Add(key)) continue;
                unique.Add(new IndexedDimension { Index = i, Dimension = input[i] });
            }

            var removed = input.Count - unique.Count;
            if (minimumRowSpacing <= Tol)
                return new Result(unique.OrderBy(x => x.Index).Select(x => x.Dimension).ToArray(), removed, 0);

            var chains = BuildChains(unique);
            var output = new Dictionary<int, PreviewDimension>();
            var shiftedChains = 0;

            foreach (var orientationGroup in chains.GroupBy(c => RotationBucket(c.Rotation)))
            {
                var placed = new List<PlacedChain>();
                foreach (var chain in orientationGroup.OrderBy(c => c.NormalCoordinate).ThenBy(c => c.TangentMin))
                {
                    var targetNormal = chain.NormalCoordinate;
                    var guard = 0;
                    bool moved;
                    do
                    {
                        moved = false;
                        foreach (var previous in placed)
                        {
                            if (!IntervalsOverlap(chain.TangentMin, chain.TangentMax,
                                                  previous.Chain.TangentMin, previous.Chain.TangentMax))
                                continue;
                            if (Math.Abs(targetNormal - previous.NormalCoordinate) >= minimumRowSpacing - Tol)
                                continue;

                            targetNormal = previous.NormalCoordinate + minimumRowSpacing;
                            moved = true;
                        }
                        guard++;
                    }
                    while (moved && guard < 1000);

                    var delta = targetNormal - chain.NormalCoordinate;
                    if (Math.Abs(delta) > Tol) shiftedChains++;
                    var nx = -Math.Sin(chain.Rotation);
                    var ny = Math.Cos(chain.Rotation);

                    foreach (var item in chain.Items)
                    {
                        var d = item.Dimension;
                        var linePoint = new Point2(
                            d.DimensionLinePoint.X + nx * delta,
                            d.DimensionLinePoint.Y + ny * delta);
                        output[item.Index] = new PreviewDimension(
                            d.ExtensionPoint1,
                            d.ExtensionPoint2,
                            linePoint,
                            d.RotationRadians,
                            d.Target);
                    }

                    placed.Add(new PlacedChain { Chain = chain, NormalCoordinate = targetNormal });
                }
            }

            return new Result(
                output.OrderBy(x => x.Key).Select(x => x.Value).ToArray(),
                removed,
                shiftedChains);
        }

        private static List<Chain> BuildChains(IEnumerable<IndexedDimension> dimensions)
        {
            var result = new List<Chain>();
            foreach (var orientation in dimensions.GroupBy(x => RotationBucket(x.Dimension.RotationRadians)))
            {
                var rotation = NormalizeHalfTurn(orientation.First().Dimension.RotationRadians);
                var ux = Math.Cos(rotation);
                var uy = Math.Sin(rotation);
                var nx = -uy;
                var ny = ux;

                foreach (var row in orientation.GroupBy(x => RoundRow(Project(x.Dimension.DimensionLinePoint, nx, ny))))
                {
                    var chain = new Chain
                    {
                        Rotation = rotation,
                        NormalCoordinate = row.Average(x => Project(x.Dimension.DimensionLinePoint, nx, ny)),
                        TangentMin = double.MaxValue,
                        TangentMax = double.MinValue
                    };

                    foreach (var item in row)
                    {
                        var a = Project(item.Dimension.ExtensionPoint1, ux, uy);
                        var b = Project(item.Dimension.ExtensionPoint2, ux, uy);
                        chain.TangentMin = Math.Min(chain.TangentMin, Math.Min(a, b));
                        chain.TangentMax = Math.Max(chain.TangentMax, Math.Max(a, b));
                        chain.Items.Add(item);
                    }
                    result.Add(chain);
                }
            }
            return result;
        }

        private static string SegmentKey(PreviewDimension d)
        {
            var a = PointKey(d.ExtensionPoint1);
            var b = PointKey(d.ExtensionPoint2);
            if (string.CompareOrdinal(a, b) > 0)
            {
                var temp = a;
                a = b;
                b = temp;
            }
            return d.Target + ":" + RotationBucket(d.RotationRadians) + ":" + a + ":" + b + ":" + PointKey(d.DimensionLinePoint);
        }

        private static string PointKey(Point2 p)
            => Math.Round(p.X, 2).ToString("0.00", CultureInfo.InvariantCulture) + "," +
               Math.Round(p.Y, 2).ToString("0.00", CultureInfo.InvariantCulture);

        private static bool IntervalsOverlap(double a0, double a1, double b0, double b1)
            => Math.Min(a1, b1) > Math.Max(a0, b0) + Tol;

        private static double Project(Point2 p, double x, double y) => p.X * x + p.Y * y;

        private static double RoundRow(double value) => Math.Round(value, 1);

        private static int RotationBucket(double radians)
            => (int)Math.Round(NormalizeHalfTurn(radians) * 10000.0);

        private static double NormalizeHalfTurn(double radians)
        {
            var value = radians % Math.PI;
            if (value < 0.0) value += Math.PI;
            return value;
        }
    }
}
