namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record TimelessJewelSpec(
    TimelessJewelType Type,
    int Seed,
    TimelessConqueror Conqueror,
    string ConquerorId);
