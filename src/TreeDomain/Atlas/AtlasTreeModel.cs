namespace PathOfAvalonia.TreeDomain.Atlas;

public enum AtlasNodeType
{
    Normal,
    Notable,
    Keystone,
    Start,
    ClusterIcon,
}

public sealed class AtlasNode
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required AtlasNodeType Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public string? Icon { get; init; }
    public bool IsGateway { get; init; }
    public IReadOnlyList<string> Stats { get; init; } = [];
    public IReadOnlyList<string> ReminderText { get; init; } = [];
    public IReadOnlyList<string> FlavourText { get; init; } = [];
    public required int GroupId { get; init; }
    public required int Orbit { get; init; }
    public required int OrbitIndex { get; init; }

    // Populated only while the immutable loaded graph is being constructed.
    public List<AtlasNode> LinkedNodes { get; } = [];
}

public sealed record AtlasGroupVisual(
    int GroupId,
    double X,
    double Y,
    string AtlasKey,
    string SpriteKey);

public sealed class AtlasTreeModel
{
    public required GameId GameId { get; init; }
    public required string Version { get; init; }
    public required int StartNodeId { get; init; }
    public required int PointLimit { get; init; }
    public required IReadOnlyDictionary<int, AtlasNode> Nodes { get; init; }
    public required IReadOnlyList<Connector> Connectors { get; init; }
    public required TreeBounds Bounds { get; init; }
    public required IReadOnlyDictionary<int, GroupPosition> Groups { get; init; }
    public required IReadOnlyList<int> SkillsPerOrbit { get; init; }
    public required IReadOnlyList<double> OrbitRadii { get; init; }
    public required IReadOnlyList<IReadOnlyList<double>> OrbitAngles { get; init; }
    public required IReadOnlyList<AtlasGroupVisual> GroupVisuals { get; init; }
}

public enum AtlasNodeDiffKind
{
    Added,
    Changed,
    Removed,
}

public sealed record AtlasNodeDiff(AtlasNodeDiffKind Kind, AtlasNode Node);

public sealed class AtlasTreeDiff
{
    public static readonly AtlasTreeDiff Empty = new(new Dictionary<int, AtlasNodeDiff>(), []);

    private AtlasTreeDiff(
        IReadOnlyDictionary<int, AtlasNodeDiff> currentNodeDiffs,
        IReadOnlyList<AtlasNodeDiff> removedNodes)
    {
        CurrentNodeDiffs = currentNodeDiffs;
        RemovedNodes = removedNodes;
    }

    public IReadOnlyDictionary<int, AtlasNodeDiff> CurrentNodeDiffs { get; }
    public IReadOnlyList<AtlasNodeDiff> RemovedNodes { get; }
    public int AddedCount => CurrentNodeDiffs.Values.Count(diff => diff.Kind == AtlasNodeDiffKind.Added);
    public int ChangedCount => CurrentNodeDiffs.Values.Count(diff => diff.Kind == AtlasNodeDiffKind.Changed);
    public int RemovedCount => RemovedNodes.Count;
    public bool HasChanges => CurrentNodeDiffs.Count > 0 || RemovedNodes.Count > 0;

    public static AtlasTreeDiff Compare(AtlasTreeModel current, AtlasTreeModel baseline)
    {
        var currentDiffs = new Dictionary<int, AtlasNodeDiff>();
        foreach (var (id, currentNode) in current.Nodes)
        {
            if (!baseline.Nodes.TryGetValue(id, out var baselineNode))
            {
                currentDiffs[id] = new AtlasNodeDiff(AtlasNodeDiffKind.Added, currentNode);
            }
            else if (NodeChanged(currentNode, baselineNode))
            {
                currentDiffs[id] = new AtlasNodeDiff(AtlasNodeDiffKind.Changed, currentNode);
            }
        }

        var removed = baseline.Nodes
            .Where(pair => !current.Nodes.ContainsKey(pair.Key))
            .Select(pair => new AtlasNodeDiff(AtlasNodeDiffKind.Removed, pair.Value))
            .ToArray();
        return new AtlasTreeDiff(currentDiffs, removed);
    }

    private static bool NodeChanged(AtlasNode current, AtlasNode baseline) =>
        current.Name != baseline.Name
        || current.Type != baseline.Type
        || current.Icon != baseline.Icon
        || current.IsGateway != baseline.IsGateway
        || current.X != baseline.X
        || current.Y != baseline.Y
        || !current.Stats.SequenceEqual(baseline.Stats);
}
