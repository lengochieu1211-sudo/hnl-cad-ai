namespace HNL.VXT.Core.Geometry
{
    public readonly struct Segment2
    {
        public Segment2(Point2 a, Point2 b) { A = a; B = b; }
        public Point2 A { get; }
        public Point2 B { get; }
        public double Length => A.DistanceTo(B);
    }
}
