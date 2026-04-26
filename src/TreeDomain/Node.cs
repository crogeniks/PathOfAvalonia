namespace PathOfAvalonia.TreeDomain;

public sealed class Node
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required NodeType Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string? Icon { get; init; }
    // Masteries have separate sprite keys per state, in different atlases
    // (masteryActiveSelected / masteryConnected). Non-mastery nodes use Icon.
    public string? ActiveIcon { get; init; }
    public string? InactiveIcon { get; init; }
    public string? AscendancyName { get; init; }
    public int? ClassStartIndex { get; init; }

    // Effect options for mastery nodes. null for non-masteries and for masteries that
    // are static cluster decorations (Atlas Tree charm masteries have no selectable effect).
    public IReadOnlyList<MasteryEffect>? MasteryEffects { get; init; }

    // Wired after load; mutable only during construction.
    public List<Node> LinkedNodes { get; } = new();
}

public sealed record MasteryEffect(int Id, IReadOnlyList<string> Stats);
