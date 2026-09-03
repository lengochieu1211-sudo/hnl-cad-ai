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
}
