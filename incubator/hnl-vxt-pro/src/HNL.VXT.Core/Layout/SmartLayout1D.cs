using System;
using System.Collections.Generic;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Layout
{
    /// <summary>
    /// AutoCAD-independent port of the V6.7.4 calc-smart-layout intent.
    /// It enforces rounded spacing, edge limits and balanced/one-side placement.
    /// </summary>
    public static class SmartLayout1D
    {
        public sealed class Result
        {
            public Result(double startOffset, IReadOnlyList<double> steps, double endOffset)
            {
                StartOffset = startOffset;
                Steps = steps ?? Array.Empty<double>();
                EndOffset = endOffset;
            }

            public double StartOffset { get; }
            public IReadOnlyList<double> Steps { get; }
            public double EndOffset { get; }
            public int PointCount => Steps.Count + 1;
            public double Span => Steps.Sum();

            public IReadOnlyList<double> Positions(double minCoordinate)
            {
                var result = new List<double>(PointCount);
                var value = minCoordinate + StartOffset;
                result.Add(value);
                for (var i = 0; i < Steps.Count; i++)
                {
                    value += Steps[i];
                    result.Add(value);
                }
                return result;
            }
        }

        public static Result Calculate(
            double length,
            double maxSpacing,
            double minSpacing,
            double maxEdge,
            double minEdge,
            double increment,
            MainLayoutMode mode,
            bool reverse = false)
        {
            if (length <= 1e-9) return null;
            increment = Math.Max(1e-6, Math.Abs(increment));
            minEdge = Math.Max(0.0, minEdge);
            maxEdge = Math.Max(minEdge, maxEdge);

            var maxRounded = FloorMultiple(maxSpacing, increment);
            var minRounded = CeilMultiple(minSpacing, increment);
            if (maxRounded < minRounded) maxRounded = minRounded;
            if (maxRounded <= 0.0) maxRounded = increment;
            if (minRounded <= 0.0) minRounded = increment;

            var candidates = new List<Candidate>();

            // A single member is valid when both edge distances can satisfy the limits.
            if (length >= 2.0 * minEdge && length <= 2.0 * maxEdge + 1e-8)
            {
                AddCandidate(candidates, length, Array.Empty<double>(), minEdge, maxEdge, increment, mode, reverse, maxRounded);
            }

            var maxIntervals = Math.Max(1, (int)Math.Floor(Math.Max(0.0, length - 2.0 * minEdge) / minRounded) + 1);
            // Hard guard is only defensive against pathological drawings/units.
            maxIntervals = Math.Min(maxIntervals, 10000);

            for (var n = 1; n <= maxIntervals; n++)
            {
                var minSum = Math.Max(n * minRounded, length - 2.0 * maxEdge);
                var maxSum = Math.Min(n * maxRounded, length - 2.0 * minEdge);
                if (minSum > maxSum + 1e-8) continue;

                // V6.7.4 biases toward the largest legal rounded interval sum.
                var roundedSum = FloorMultiple(maxSum, increment);
                if (roundedSum < minSum - 1e-8)
                    roundedSum = CeilMultiple(minSum, increment);
                if (roundedSum < minSum - 1e-8 || roundedSum > maxSum + 1e-8) continue;

                var steps = BuildSteps(n, roundedSum, minRounded, maxRounded, increment, mode == MainLayoutMode.OneSide);
                if (steps == null) continue;
                AddCandidate(candidates, length, steps, minEdge, maxEdge, increment, mode, reverse, maxRounded);
            }

            if (candidates.Count == 0)
            {
                // Very short region: preserve non-destructive preview/create semantics with
                // a centered member rather than inventing invalid negative spacing.
                var centered = Math.Max(0.0, length * 0.5);
                return new Result(centered, Array.Empty<double>(), length - centered);
            }

            return candidates
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Steps.Count) // prefer fewer members at equivalent quality
                .Select(x => new Result(x.Start, x.Steps, x.End))
                .First();
        }

        private static void AddCandidate(
            ICollection<Candidate> candidates,
            double length,
            IReadOnlyList<double> steps,
            double minEdge,
            double maxEdge,
            double increment,
            MainLayoutMode mode,
            bool reverse,
            double preferredSpacing)
        {
            var sum = steps.Sum();
            var edgeSum = length - sum;
            if (edgeSum < 2.0 * minEdge - 1e-8 || edgeSum > 2.0 * maxEdge + 1e-8) return;

            double start;
            double end;
            if (mode == MainLayoutMode.OneSide)
            {
                start = FloorMultiple(Math.Min(maxEdge, edgeSum - minEdge), increment);
                start = Clamp(start, minEdge, maxEdge);
                end = edgeSum - start;
                if (end < minEdge - 1e-8 || end > maxEdge + 1e-8)
                {
                    end = Clamp(end, minEdge, maxEdge);
                    start = edgeSum - end;
                }
                if (reverse)
                {
                    var t = start;
                    start = end;
                    end = t;
                }
            }
            else
            {
                var ideal = edgeSum * 0.5;
                start = NearestMultiple(ideal, increment);
                start = Clamp(start, minEdge, maxEdge);
                end = edgeSum - start;
                if (end < minEdge - 1e-8 || end > maxEdge + 1e-8)
                {
                    end = Clamp(end, minEdge, maxEdge);
                    start = edgeSum - end;
                }
            }

            if (start < minEdge - 1e-7 || start > maxEdge + 1e-7 ||
                end < minEdge - 1e-7 || end > maxEdge + 1e-7) return;

            var edgePenalty = Math.Abs(start - end);
            var spacingPenalty = 0.0;
            foreach (var s in steps)
                spacingPenalty += Math.Abs(preferredSpacing - s);

            // Auto uses the balanced score; OneSide prioritizes its loaded edge while still
            // preferring the largest legal spacing values, matching Golden intent.
            var score = spacingPenalty + (mode == MainLayoutMode.OneSide ? 0.05 * edgePenalty : edgePenalty);
            candidates.Add(new Candidate(start, steps.ToArray(), end, score));
        }

        private static IReadOnlyList<double> BuildSteps(
            int count,
            double sum,
            double minSpacing,
            double maxSpacing,
            double increment,
            bool greedy)
        {
            if (count <= 0) return Array.Empty<double>();
            var steps = Enumerable.Repeat(minSpacing, count).ToArray();
            var remaining = sum - count * minSpacing;
            if (remaining < -1e-7) return null;

            var increments = Math.Max(0, (int)Math.Round(remaining / increment));
            var maxExtra = Math.Max(0, (int)Math.Round((maxSpacing - minSpacing) / increment));

            if (greedy)
            {
                for (var i = 0; i < count && increments > 0; i++)
                {
                    var add = Math.Min(maxExtra, increments);
                    steps[i] += add * increment;
                    increments -= add;
                }
            }
            else
            {
                var cursor = 0;
                while (increments > 0)
                {
                    if (steps[cursor] + increment <= maxSpacing + 1e-8)
                    {
                        steps[cursor] += increment;
                        increments--;
                    }
                    cursor++;
                    if (cursor >= count) cursor = 0;
                    if (steps.All(x => x + increment > maxSpacing + 1e-8) && increments > 0) return null;
                }
            }

            return steps;
        }

        public static double OptimizeOffset(
            Result layout,
            double minCoordinate,
            double minEdge,
            double maxEdge,
            double increment,
            Func<double, bool> isCoordinateClear)
        {
            if (layout == null || isCoordinateClear == null) return layout?.StartOffset ?? 0.0;
            if (IsLayoutClear(layout, minCoordinate, layout.StartOffset, isCoordinateClear)) return layout.StartOffset;

            for (var k = 1; k < 10000; k++)
            {
                var delta = k * increment;
                var plus = layout.StartOffset + delta;
                var plusEnd = layout.EndOffset - delta;
                if (plus <= maxEdge + 1e-8 && plusEnd >= minEdge - 1e-8 &&
                    IsLayoutClear(layout, minCoordinate, plus, isCoordinateClear)) return plus;

                var minus = layout.StartOffset - delta;
                var minusEnd = layout.EndOffset + delta;
                if (minus >= minEdge - 1e-8 && minusEnd <= maxEdge + 1e-8 &&
                    IsLayoutClear(layout, minCoordinate, minus, isCoordinateClear)) return minus;

                if (plus > maxEdge + 1e-8 && minus < minEdge - 1e-8) break;
            }
            return double.NaN;
        }

        private static bool IsLayoutClear(Result layout, double minCoordinate, double offset, Func<double, bool> clear)
        {
            var value = minCoordinate + offset;
            if (!clear(value)) return false;
            for (var i = 0; i < layout.Steps.Count; i++)
            {
                value += layout.Steps[i];
                if (!clear(value)) return false;
            }
            return true;
        }

        private static double FloorMultiple(double value, double increment)
            => Math.Floor((value + 1e-10) / increment) * increment;

        private static double CeilMultiple(double value, double increment)
            => Math.Ceiling((value - 1e-10) / increment) * increment;

        private static double NearestMultiple(double value, double increment)
            => Math.Round(value / increment, MidpointRounding.AwayFromZero) * increment;

        private static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));

        private sealed class Candidate
        {
            public Candidate(double start, IReadOnlyList<double> steps, double end, double score)
            {
                Start = start;
                Steps = steps;
                End = end;
                Score = score;
            }
            public double Start { get; }
            public IReadOnlyList<double> Steps { get; }
            public double End { get; }
            public double Score { get; }
        }
    }
}
