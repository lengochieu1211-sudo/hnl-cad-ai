using System;
using System.Linq;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    /// <summary>
    /// Decides which transient overlays may be rendered without exceeding the AutoCAD
    /// preview drawable budget. Counts must mirror VxtTransientPreview exactly:
    /// XC/XP = one Line each, Ty = one Circle per hanger point, DIM = one RotatedDimension,
    /// guides = non-structural/non-hanger PreviewLine items plus PreviewText items.
    ///
    /// PreviewLineKind.Hanger is intentionally excluded because those two cross-lines per Ty
    /// are Core geometry helpers only; the AutoCAD renderer replaces them with one Circle.
    /// </summary>
    public static class VxtPreviewLoadSheddingPolicy
    {
        public const int DefaultDrawableLimit = 3000;

        public static VxtPreviewRenderDecision Evaluate(
            VxtPreviewPlan plan,
            VxtSettings settings,
            int drawableLimit = DefaultDrawableLimit)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (drawableLimit < 1) throw new ArgumentOutOfRangeException(nameof(drawableLimit));

            var structural = plan.Lines.Count(x =>
                x.Kind == PreviewLineKind.Main || x.Kind == PreviewLineKind.Furring);

            var guides = plan.Lines.Count(x =>
                x.Kind != PreviewLineKind.Main &&
                x.Kind != PreviewLineKind.Furring &&
                x.Kind != PreviewLineKind.Hanger) + plan.Texts.Count;

            var hangers = settings.DrawHangers ? plan.HangerPoints.Count : 0;
            var dimensions = settings.AutoDimension ? plan.Dimensions.Count : 0;

            var full = structural + hangers + dimensions + guides;
            if (full <= drawableLimit)
            {
                return new VxtPreviewRenderDecision(
                    structural, hangers, dimensions, guides,
                    renderHangers: settings.DrawHangers,
                    renderDimensions: settings.AutoDimension,
                    renderGuides: true);
            }

            // WYSIWYG priority after XC/XP: retain DIM whenever XC/XP + DIM still fit.
            // Ty markers and visual guides are expendable because their exact counts remain
            // visible in the palette and Create consumes the same final plan.
            var priority = structural + dimensions;
            if (priority <= drawableLimit)
            {
                return new VxtPreviewRenderDecision(
                    structural, hangers, dimensions, guides,
                    renderHangers: false,
                    renderDimensions: settings.AutoDimension,
                    renderGuides: false);
            }

            // Very large drawings keep the historical fail-safe: structural framing only.
            return new VxtPreviewRenderDecision(
                structural, hangers, dimensions, guides,
                renderHangers: false,
                renderDimensions: false,
                renderGuides: false);
        }
    }

    public sealed class VxtPreviewRenderDecision
    {
        internal VxtPreviewRenderDecision(
            int structuralDrawableCount,
            int hangerDrawableCount,
            int dimensionDrawableCount,
            int guideDrawableCount,
            bool renderHangers,
            bool renderDimensions,
            bool renderGuides)
        {
            StructuralDrawableCount = structuralDrawableCount;
            HangerDrawableCount = hangerDrawableCount;
            DimensionDrawableCount = dimensionDrawableCount;
            GuideDrawableCount = guideDrawableCount;
            RenderHangers = renderHangers;
            RenderDimensions = renderDimensions;
            RenderGuides = renderGuides;
        }

        public int StructuralDrawableCount { get; }
        public int HangerDrawableCount { get; }
        public int DimensionDrawableCount { get; }
        public int GuideDrawableCount { get; }
        public int FullDrawableCount =>
            StructuralDrawableCount + HangerDrawableCount + DimensionDrawableCount + GuideDrawableCount;

        public bool RenderHangers { get; }
        public bool RenderDimensions { get; }
        public bool RenderGuides { get; }
        public bool IsReduced => !RenderHangers || !RenderDimensions || !RenderGuides;
    }
}
