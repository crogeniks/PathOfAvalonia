namespace PathOfAvalonia.TreeDomain.Jewels;

public sealed record TimelessNodeEffect(
    string EffectiveName,
    string? EffectiveIcon,
    IReadOnlyList<string> EffectiveStats,
    bool ReplacesNode);
