namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record EffectiveNodeView(
    Node BaseNode,
    IReadOnlyList<string> EffectiveStats,
    bool IsConquered,
    TimelessConqueror? Conqueror,
    IReadOnlyList<int> AffectedBySocketNodeIds);
