namespace HNL.VXT.Core.Models
{
    public enum DimensionPosition
    {
        Auto = 0,
        Top = 1,
        Bottom = 2,
        Left = 3,
        Right = 4
    }

    public enum DimensionTarget
    {
        Main = 0,
        Furring = 1,
        Hanger = 2
    }

    public enum PreviewLineKind
    {
        Boundary,
        Main,
        Furring,
        Hanger,
        Dimension,
        DimensionExtension,
        Direction,
        Avoidance
    }
}
