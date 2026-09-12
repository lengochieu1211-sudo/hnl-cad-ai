using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;

namespace HNL.VXT.Core.Preview
{
    public readonly struct PreviewLine
    {
        public PreviewLine(Point2 a, Point2 b, PreviewLineKind kind)
        {
            A = a;
            B = b;
            Kind = kind;
        }

        public Point2 A { get; }
        public Point2 B { get; }
        public PreviewLineKind Kind { get; }
    }

    public readonly struct PreviewText
    {
        public PreviewText(Point2 position, string text, PreviewLineKind kind, double rotationRadians = 0.0)
        {
            Position = position;
            Text = text;
            Kind = kind;
            RotationRadians = rotationRadians;
        }

        public Point2 Position { get; }
        public string Text { get; }
        public PreviewLineKind Kind { get; }
        public double RotationRadians { get; }
    }

    /// <summary>
    /// Geometry needed to render a real AutoCAD RotatedDimension as a transient drawable.
    /// The Core project stays AutoCAD-independent; the bridge resolves DimStyle/Layer at runtime.
    /// </summary>
    public readonly struct PreviewDimension
    {
        public PreviewDimension(
            Point2 extensionPoint1,
            Point2 extensionPoint2,
            Point2 dimensionLinePoint,
            double rotationRadians,
            DimensionTarget target)
        {
            ExtensionPoint1 = extensionPoint1;
            ExtensionPoint2 = extensionPoint2;
            DimensionLinePoint = dimensionLinePoint;
            RotationRadians = rotationRadians;
            Target = target;
        }

        public Point2 ExtensionPoint1 { get; }
        public Point2 ExtensionPoint2 { get; }
        public Point2 DimensionLinePoint { get; }
        public double RotationRadians { get; }
        public DimensionTarget Target { get; }
    }
}
