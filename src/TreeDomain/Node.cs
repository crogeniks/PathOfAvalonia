namespace PathOfAvalonia.TreeDomain;

public sealed class Node
{
    public required int Id { get; init; }
    public string? BuildPlannerId { get; init; }
    public required string Name { get; init; }
    public required NodeType Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string? Icon { get; init; }
    public NodeVisual? Visual { get; init; }
    public IReadOnlyList<string> Stats { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<TextSpan>> StatLinkSpans { get; init; } = Array.Empty<IReadOnlyList<TextSpan>>();
    public IReadOnlyList<string> ReminderText { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FlavourText { get; init; } = Array.Empty<string>();
    // Masteries have separate sprite keys per state, in different atlases
    // (masteryActiveSelected / masteryConnected). Non-mastery nodes use Icon.
    public string? ActiveIcon { get; init; }
    public string? InactiveIcon { get; init; }
    public string? AscendancyName { get; init; }
    public int? ClassStartIndex { get; init; }
    public IReadOnlyList<int> ClassStartIndexes { get; init; } = Array.Empty<int>();
    public required int GroupId { get; init; }
    public required int Orbit { get; init; }
    public required int OrbitIndex { get; init; }
    public ExpansionSocketInfo? ExpansionSocket { get; init; }

    // Effect options for mastery nodes. null for non-masteries and for masteries that
    // are static cluster decorations (Atlas Tree charm masteries have no selectable effect).
    public IReadOnlyList<MasteryEffect>? MasteryEffects { get; init; }

    // Wired after load; mutable only during construction.
    public List<Node> LinkedNodes { get; } = new();
}

public sealed record TextSpan(int Start, int Length);
public sealed record MasteryEffect(int Id, IReadOnlyList<string> Stats);
public sealed record ExpansionSocketInfo(int Size, int Index, int ProxyNodeId, int? ParentSocketId);
public sealed record NodeVisual(
    string? Icon,
    string? AllocatedFrame,
    string? HoverFrame,
    string? UnallocatedFrame,
    string? ConnectionArt,
    bool IconPathIsAssetPath);
