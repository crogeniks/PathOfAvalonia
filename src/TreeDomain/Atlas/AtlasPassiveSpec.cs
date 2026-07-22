namespace PathOfAvalonia.TreeDomain.Atlas;

public sealed record AtlasHoverPath(
    IReadOnlyList<int> Nodes,
    IReadOnlySet<(int Min, int Max)> Edges)
{
    public static readonly AtlasHoverPath Empty = new([], new HashSet<(int, int)>());
    public IReadOnlySet<int> NodeIds { get; } = Nodes.ToHashSet();
    public bool IsEmpty => Nodes.Count == 0;
}

public sealed class AtlasPassiveSpec
{
    private readonly HashSet<int> _allocated = [];

    public AtlasPassiveSpec(AtlasTreeModel tree)
    {
        Tree = tree;
        _allocated.Add(tree.StartNodeId);
    }

    public AtlasTreeModel Tree { get; }
    public IReadOnlySet<int> AllocatedNodes => _allocated;
    public int AllocatedPointCount => Math.Max(0, _allocated.Count - 1);
    public event Action? SpecChanged;

    public bool IsAllocated(int nodeId) => _allocated.Contains(nodeId);

    public void Toggle(int nodeId)
    {
        if (!Tree.Nodes.TryGetValue(nodeId, out var node)
            || node.Type is AtlasNodeType.Start or AtlasNodeType.ClusterIcon)
        {
            return;
        }

        if (_allocated.Contains(nodeId))
        {
            DeallocateWithDependents(nodeId);
            return;
        }

        if (!node.LinkedNodes.Any(linked => _allocated.Contains(linked.Id)))
        {
            return;
        }

        _allocated.Add(nodeId);
        SpecChanged?.Invoke();
    }

    public AtlasHoverPath HoverPathTo(int targetNodeId)
    {
        if (_allocated.Contains(targetNodeId)
            || !Tree.Nodes.TryGetValue(targetNodeId, out var target)
            || target.Type is AtlasNodeType.Start or AtlasNodeType.ClusterIcon)
        {
            return AtlasHoverPath.Empty;
        }

        var parents = new Dictionary<int, int>();
        var visited = new HashSet<int>(_allocated);
        var queue = new Queue<AtlasNode>(_allocated.Select(id => Tree.Nodes[id]));
        var found = false;
        while (queue.TryDequeue(out var node) && !found)
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (linked.Type is AtlasNodeType.Start or AtlasNodeType.ClusterIcon || !visited.Add(linked.Id))
                {
                    continue;
                }

                parents[linked.Id] = node.Id;
                if (linked.Id == targetNodeId)
                {
                    found = true;
                    break;
                }
                queue.Enqueue(linked);
            }
        }

        if (!found)
        {
            return AtlasHoverPath.Empty;
        }

        var nodes = new List<int>();
        var edges = new HashSet<(int, int)>();
        var current = targetNodeId;
        while (parents.TryGetValue(current, out var parent))
        {
            if (!_allocated.Contains(current))
            {
                nodes.Add(current);
            }
            edges.Add((Math.Min(current, parent), Math.Max(current, parent)));
            current = parent;
        }
        nodes.Reverse();
        return new AtlasHoverPath(nodes, edges);
    }

    public void AllocatePath(AtlasHoverPath path)
    {
        var changed = false;
        foreach (var nodeId in path.Nodes)
        {
            if (Tree.Nodes.TryGetValue(nodeId, out var node)
                && node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
            {
                changed |= _allocated.Add(nodeId);
            }
        }
        if (changed)
        {
            SpecChanged?.Invoke();
        }
    }

    public void RestoreConnectedAllocations(IEnumerable<int> nodeIds)
    {
        var requested = nodeIds.Where(Tree.Nodes.ContainsKey).ToHashSet();
        requested.Add(Tree.StartNodeId);
        var restored = new HashSet<int> { Tree.StartNodeId };
        var queue = new Queue<AtlasNode>();
        queue.Enqueue(Tree.Nodes[Tree.StartNodeId]);
        while (queue.TryDequeue(out var node))
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (linked.Type == AtlasNodeType.ClusterIcon
                    || !requested.Contains(linked.Id)
                    || !restored.Add(linked.Id))
                {
                    continue;
                }
                queue.Enqueue(linked);
            }
        }

        if (_allocated.SetEquals(restored))
        {
            return;
        }
        _allocated.Clear();
        _allocated.UnionWith(restored);
        SpecChanged?.Invoke();
    }

    public void Clear()
    {
        if (_allocated.Count == 1 && _allocated.Contains(Tree.StartNodeId))
        {
            return;
        }
        _allocated.Clear();
        _allocated.Add(Tree.StartNodeId);
        SpecChanged?.Invoke();
    }

    private void DeallocateWithDependents(int excludedNodeId)
    {
        var reachable = new HashSet<int> { Tree.StartNodeId };
        var queue = new Queue<AtlasNode>();
        queue.Enqueue(Tree.Nodes[Tree.StartNodeId]);
        while (queue.TryDequeue(out var node))
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (linked.Id == excludedNodeId
                    || !_allocated.Contains(linked.Id)
                    || !reachable.Add(linked.Id))
                {
                    continue;
                }
                queue.Enqueue(linked);
            }
        }

        _allocated.IntersectWith(reachable);
        SpecChanged?.Invoke();
    }
}
