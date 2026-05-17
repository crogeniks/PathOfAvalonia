namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record JewelRadiusVisual(
    int SourceNodeId,
    double X,
    double Y,
    double InnerRadius,
    double OuterRadius,
    JewelRadiusVisualStyle Style,
    TimelessConqueror? Conqueror);
