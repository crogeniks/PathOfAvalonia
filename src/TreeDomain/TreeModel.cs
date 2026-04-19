namespace PathOfAvalonia.TreeDomain;

public sealed record TreeBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double CenterX => (MinX + MaxX) * 0.5;
    public double CenterY => (MinY + MaxY) * 0.5;
}

public sealed class TreeModel
{
    public required string Version { get; init; }
    public required IReadOnlyDictionary<int, Node> Nodes { get; init; }
    public required IReadOnlyList<Connector> Connectors { get; init; }
    public required TreeBounds Bounds { get; init; }
}
