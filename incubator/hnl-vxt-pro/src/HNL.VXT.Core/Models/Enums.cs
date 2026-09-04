namespace HNL.VXT.Core.Models
{
    public enum MainDirectionMode
    {
        Horizontal = 0,
        Vertical = 1,
        TwoPoints = 2,
        RectangleRegions = 3,
        Auto = 4
    }

    public enum MainLayoutMode
    {
        Auto = 0,
        BalancedTwoEnds = 1,
        OneSide = 2
    }

    public enum HangerLayoutMode
    {
        BalancedTwoEnds = 0,
        OneSideFollowFurring = 1
    }

    public enum BlockTarget
    {
        Main = 0,
        Furring = 1,
        Hanger = 2
    }

    public enum EquipmentTarget
    {
        General = 0,
        Main = 1,
        Furring = 2
    }

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
