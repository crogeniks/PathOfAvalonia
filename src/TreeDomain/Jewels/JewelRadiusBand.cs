namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record JewelRadiusBand(
    int Index,
    string Label,
    double Inner,
    double Outer,
    string ColorHex)
{
    public bool IsAnnulus => Inner > 0;
}
