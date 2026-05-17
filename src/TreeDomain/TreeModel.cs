namespace PathOfAvalonia.TreeDomain;

public sealed record TreeBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public double CenterX => (MinX + MaxX) * 0.5;
    public double CenterY => (MinY + MaxY) * 0.5;
}

public sealed record GroupPosition(double X, double Y);

public sealed class TreeModel
{
    public required GameId GameId { get; init; }
    public required string Version { get; init; }
    public required ClassCatalog Classes { get; init; }
    public required IReadOnlyDictionary<int, Node> Nodes { get; init; }
    public required IReadOnlyDictionary<string, Node> ClusterNodeTemplates { get; init; }
    public required IReadOnlyList<Connector> Connectors { get; init; }
    public required TreeBounds Bounds { get; init; }
    public required IReadOnlyDictionary<int, GroupPosition> Groups { get; init; }
    public required IReadOnlyList<int> SkillsPerOrbit { get; init; }
    public required IReadOnlyList<double> OrbitRadii { get; init; }
    public required IReadOnlyList<IReadOnlyList<double>> OrbitAngles { get; init; }

    public bool TryGetPosition(int groupId, int orbit, int orbitIndex, out double x, out double y)
    {
        x = 0;
        y = 0;
        if (!Groups.TryGetValue(groupId, out var group)
            || orbit < 0 || orbit >= OrbitRadii.Count || orbit >= OrbitAngles.Count)
        {
            return false;
        }
        var angles = OrbitAngles[orbit];
        if (orbitIndex < 0 || orbitIndex >= angles.Count)
        {
            return false;
        }
        var angle = angles[orbitIndex];
        var radius = OrbitRadii[orbit];
        x = group.X + Math.Sin(angle) * radius;
        y = group.Y - Math.Cos(angle) * radius;
        return true;
    }
}
