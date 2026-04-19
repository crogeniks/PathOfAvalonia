namespace PathOfAvalonia.TreeDomain;

public sealed class Node
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required NodeType Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string? AscendancyName { get; init; }

    // Wired after load; mutable only during construction.
    public List<Node> LinkedNodes { get; } = new();
}
