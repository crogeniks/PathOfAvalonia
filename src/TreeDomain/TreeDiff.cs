namespace PathOfAvalonia.TreeDomain;

public enum TreeNodeDiffKind
{
    Added,
    Changed,
    Removed,
}

public sealed record TreeNodeDiff(TreeNodeDiffKind Kind, Node Node);

public sealed class TreeDiff
{
    public static readonly TreeDiff Empty = new(
        new Dictionary<int, TreeNodeDiff>(),
        new Dictionary<int, Node>(),
        Array.Empty<TreeNodeDiff>(),
        null,
        null);

    public IReadOnlyDictionary<int, TreeNodeDiff> CurrentNodeDiffs { get; }
    public IReadOnlyDictionary<int, Node> BaselineNodes { get; }
    public IReadOnlyList<TreeNodeDiff> RemovedNodes { get; }
    public string? CurrentVersion { get; }
    public string? BaselineVersion { get; }

    public int AddedCount => CurrentNodeDiffs.Values.Count(diff => diff.Kind == TreeNodeDiffKind.Added);
    public int ChangedCount => CurrentNodeDiffs.Values.Count(diff => diff.Kind == TreeNodeDiffKind.Changed);
    public int RemovedCount => RemovedNodes.Count;
    public bool HasChanges => CurrentNodeDiffs.Count > 0 || RemovedNodes.Count > 0;

    private TreeDiff(
        IReadOnlyDictionary<int, TreeNodeDiff> currentNodeDiffs,
        IReadOnlyDictionary<int, Node> baselineNodes,
        IReadOnlyList<TreeNodeDiff> removedNodes,
        string? currentVersion,
        string? baselineVersion)
    {
        CurrentNodeDiffs = currentNodeDiffs;
        BaselineNodes = baselineNodes;
        RemovedNodes = removedNodes;
        CurrentVersion = currentVersion;
        BaselineVersion = baselineVersion;
    }

    public static TreeDiff Compare(TreeModel current, TreeModel baseline)
    {
        var currentDiffs = new Dictionary<int, TreeNodeDiff>();
        foreach (var (id, currentNode) in current.Nodes)
        {
            if (!baseline.Nodes.TryGetValue(id, out var baselineNode))
            {
                currentDiffs[id] = new TreeNodeDiff(TreeNodeDiffKind.Added, currentNode);
                continue;
            }

            if (NodeChanged(currentNode, baselineNode))
            {
                currentDiffs[id] = new TreeNodeDiff(TreeNodeDiffKind.Changed, currentNode);
            }
        }

        var removed = new List<TreeNodeDiff>();
        foreach (var (id, baselineNode) in baseline.Nodes)
        {
            if (!current.Nodes.ContainsKey(id))
            {
                removed.Add(new TreeNodeDiff(TreeNodeDiffKind.Removed, baselineNode));
            }
        }

        return new TreeDiff(currentDiffs, baseline.Nodes, removed, current.Version, baseline.Version);
    }

    private static bool NodeChanged(Node current, Node baseline)
    {
        return current.Name != baseline.Name
            || current.Type != baseline.Type
            || current.AscendancyName != baseline.AscendancyName
            || current.ClassStartIndexes.Count != baseline.ClassStartIndexes.Count
            || !current.ClassStartIndexes.SequenceEqual(baseline.ClassStartIndexes)
            || current.Stats.Count != baseline.Stats.Count
            || !current.Stats.SequenceEqual(baseline.Stats);
    }
}
