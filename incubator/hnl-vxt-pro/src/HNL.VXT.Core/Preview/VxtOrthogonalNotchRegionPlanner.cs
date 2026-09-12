using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Decomposes an orthogonal concave ceiling, already transformed to the XC local axis,
    /// into maximal vertical rectangular regions. Each region receives a normal SmartLayout1D
    /// XC layout; collinear rows are merged afterwards. XP is intentionally outside this
    /// planner and therefore keeps one global chase direction. Ty is rebuilt later from the
    /// final XC segments so every short/long XC receives its own valid hanger distribution.
    /// </summary>
    internal static class VxtOrthogonalNotchRegionPlanner
    {
        private const double Tol = 0.5;
        private const double MinDrawLength = 5.0;

        public static bool TryBuild(
  IReadOnlyList<Point2> polygon,
  Box2 domain,
  VxtSettings settings,
  IReadOnlyList<Box2> obstacles,
  out List<Segment2> mainSegments)
        {
  mainSegments = null;
  if (polygon == null || polygon.Count < 4 || settings == null) return false;
  if (!IsOrthogonal(polygon)) return false;

  var regions = BuildMaximalRegions(polygon, domain);
  if (regions.Count <= 1) return false;

  var segments = new List<Segment2>();
  foreach (var region in regions)
  {
      List<double> grid;
      if (!TryBuildRegionGrid(region, settings, obstacles, out grid)) return false;
      foreach (var y in grid)
      {
          if (region.X2 - region.X1 <= MinDrawLength) continue;
          segments.Add(new Segment2(new Point2(region.X1, y), new Point2(region.X2, y)));
      }
  }

  segments = MergeCollinear(segments);
  if (segments.Count == 0) return false;
  mainSegments = segments;
  return true;
        }

        private static bool IsOrthogonal(IReadOnlyList<Point2> polygon)
        {
  for (var i = 0; i < polygon.Count; i++)
  {
      var a = polygon[i];
      var b = polygon[(i + 1) % polygon.Count];
      var dx = Math.Abs(b.X - a.X);
      var dy = Math.Abs(b.Y - a.Y);
      if (dx <= Tol && dy <= Tol) continue;
      if (dx > Tol && dy > Tol) return false;
  }
  return true;
        }

        private static List<RectRegion> BuildMaximalRegions(IReadOnlyList<Point2> polygon, Box2 domain)
        {
  var xs = UniqueSort(polygon.Select(p => p.X)
      .Concat(new[] { domain.MinX, domain.MaxX }));
  var cells = new List<RectRegion>();

  for (var i = 0; i + 1 < xs.Count; i++)
  {
      var x1 = Math.Max(domain.MinX, xs[i]);
      var x2 = Math.Min(domain.MaxX, xs[i + 1]);
      if (x2 - x1 <= MinDrawLength) continue;
      var x = (x1 + x2) * 0.5;
      foreach (var raw in PolygonScanline.ClipVertical(polygon, x))
      {
          var y1 = Math.Max(domain.MinY, Math.Min(raw.A.Y, raw.B.Y));
          var y2 = Math.Min(domain.MaxY, Math.Max(raw.A.Y, raw.B.Y));
          if (y2 - y1 <= MinDrawLength) continue;
          cells.Add(new RectRegion(x1, x2, y1, y2));
      }
  }

  var result = new List<RectRegion>();
  foreach (var verticalGroup in cells
      .GroupBy(c => SpanKey(c.Y1, c.Y2), StringComparer.Ordinal))
  {
      RectRegion current = null;
      foreach (var cell in verticalGroup.OrderBy(c => c.X1).ThenBy(c => c.X2))
      {
          if (current == null)
          {
              current = cell;
              continue;
          }
          if (cell.X1 <= current.X2 + Tol)
          {
              current = new RectRegion(
                  current.X1,
                  Math.Max(current.X2, cell.X2),
                  current.Y1,
                  current.Y2);
          }
          else
          {
              result.Add(current);
              current = cell;
          }
      }
      if (current != null) result.Add(current);
  }

  return result
      .OrderBy(r => r.X1)
      .ThenBy(r => r.Y1)
      .ToList();
        }

        private static bool TryBuildRegionGrid(
  RectRegion region,
  VxtSettings settings,
  IReadOnlyList<Box2> obstacles,
  out List<double> grid)
        {
  grid = null;
  var mode = settings.MainLayout == MainLayoutMode.Auto
      ? MainLayoutMode.BalancedTwoEnds
      : settings.MainLayout;
  var layout = SmartLayout1D.Calculate(
      region.Height,
      settings.MainMaxSpacing,
      settings.MainMinSpacing,
      settings.MainMaxEdgeOffset,
      settings.MainMinEdgeOffset,
      settings.MainBalanceStep,
      mode);
  if (layout == null) return false;

  IReadOnlyList<double> coordinates = layout.Positions(region.Y1);
  if (settings.UseAvoidance && obstacles != null && obstacles.Count > 0)
  {
      var intervals = obstacles
          .Where(b => b.MaxX >= region.X1 + Tol && b.MinX <= region.X2 - Tol &&
                      b.MaxY >= region.Y1 + Tol && b.MinY <= region.Y2 - Tol)
          .Select(b => Tuple.Create(
              Math.Max(region.Y1, b.MinY),
              Math.Min(region.Y2, b.MaxY)))
          .Where(x => x.Item2 - x.Item1 > Tol)
          .ToList();
      if (intervals.Count > 0)
      {
          coordinates = SmartLayout1D.AdjustGrid(
              coordinates,
              intervals,
              region.Y1,
              region.Y2,
              settings.MainMinSpacing,
              settings.MainMaxSpacing,
              settings.MainMinEdgeOffset,
              settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance),
              settings.MainBalanceStep);
      }
  }

  var values = UniqueSort(coordinates);
  if (!GridValid(values, region.Y1, region.Y2, settings)) return false;
  grid = values;
  return true;
        }

        private static bool GridValid(
  IReadOnlyList<double> grid,
  double min,
  double max,
  VxtSettings settings)
        {
  if (grid == null || grid.Count == 0) return false;
  var maxEdge = settings.MainMaxEdgeOffset + Math.Max(0.0, settings.MainEdgeTolerance);
  if (grid[0] - min < settings.MainMinEdgeOffset - Tol || grid[0] - min > maxEdge + Tol)
      return false;
  if (max - grid[grid.Count - 1] < settings.MainMinEdgeOffset - Tol ||
      max - grid[grid.Count - 1] > maxEdge + Tol)
      return false;
  for (var i = 0; i + 1 < grid.Count; i++)
  {
      var d = grid[i + 1] - grid[i];
      if (d < settings.MainMinSpacing - Tol || d > settings.MainMaxSpacing + Tol)
          return false;
  }
  return true;
        }

        private static List<Segment2> MergeCollinear(IEnumerable<Segment2> source)
        {
  var groups = new List<List<Segment2>>();
  foreach (var segment in source
      .OrderBy(s => (s.A.Y + s.B.Y) * 0.5)
      .ThenBy(s => Math.Min(s.A.X, s.B.X)))
  {
      var y = (segment.A.Y + segment.B.Y) * 0.5;
      var group = groups.FirstOrDefault(g =>
          Math.Abs(((g[0].A.Y + g[0].B.Y) * 0.5) - y) <= Tol);
      if (group == null)
      {
          group = new List<Segment2>();
          groups.Add(group);
      }
      group.Add(segment);
  }

  var output = new List<Segment2>();
  foreach (var group in groups)
  {
      var y = group.Average(s => (s.A.Y + s.B.Y) * 0.5);
      double? x1 = null;
      var x2 = 0.0;
      foreach (var segment in group.OrderBy(s => Math.Min(s.A.X, s.B.X)))
      {
          var a = Math.Min(segment.A.X, segment.B.X);
          var b = Math.Max(segment.A.X, segment.B.X);
          if (!x1.HasValue)
          {
              x1 = a;
              x2 = b;
          }
          else if (a <= x2 + Tol)
          {
              x2 = Math.Max(x2, b);
          }
          else
          {
              output.Add(new Segment2(new Point2(x1.Value, y), new Point2(x2, y)));
              x1 = a;
              x2 = b;
          }
      }
      if (x1.HasValue)
          output.Add(new Segment2(new Point2(x1.Value, y), new Point2(x2, y)));
  }
  return output;
        }

        private static List<double> UniqueSort(IEnumerable<double> values)
        {
  var result = new List<double>();
  foreach (var value in (values ?? Enumerable.Empty<double>()).OrderBy(x => x))
  {
      if (result.Count == 0 || Math.Abs(result[result.Count - 1] - value) > Tol)
          result.Add(value);
  }
  return result;
        }

        private static string SpanKey(double a, double b)
  => Math.Round(a, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "|" +
     Math.Round(b, 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private sealed class RectRegion
        {
  public RectRegion(double x1, double x2, double y1, double y2)
  {
      X1 = Math.Min(x1, x2);
      X2 = Math.Max(x1, x2);
      Y1 = Math.Min(y1, y2);
      Y2 = Math.Max(y1, y2);
  }
  public double X1 { get; }
  public double X2 { get; }
  public double Y1 { get; }
  public double Y2 { get; }
  public double Height => Y2 - Y1;
        }
    }
}
