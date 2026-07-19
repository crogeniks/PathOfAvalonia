namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record EffectiveNodeView(
    Node BaseNode,
    string EffectiveName,
    string? EffectiveIcon,
    IReadOnlyList<string> EffectiveStats,
    bool ReplacesNode,
    bool IsConquered,
    TimelessConqueror? Conqueror,
    IReadOnlyList<int> AffectedBySocketNodeIds);
