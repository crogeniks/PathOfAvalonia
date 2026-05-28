namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    // Effect chosen on an allocated mastery node, if any. Null for non-masteries,
    // unallocated masteries, or masteries allocated without a picked effect.
    public MasteryEffect? SelectedMasteryEffect(int nodeId)
    {
        if (!_masterySelections.TryGetValue(nodeId, out var effectId))
        {
            return null;
        }
        if (!Tree.Nodes.TryGetValue(nodeId, out var node) || node.MasteryEffects is null)
        {
            return null;
        }
        foreach (var me in node.MasteryEffects)
        {
            if (me.Id == effectId)
            {
                return me;
            }
        }
        return null;
    }

    // PoC-level toggle. Full reachability / dependency rules (selection.md §2–4)
    // are deferred until the domain gets its own tests.
    public void Toggle(int id)
    {
        if (_classStartNodeByIndex.TryGetValue(_selectedClassIndex, out var startId) && startId == id)
        {
            return;
        }
        if (SelectedAscendancyStartNodeId() is { } ascendancyStartId && ascendancyStartId == id)
        {
            return;
        }
        if (_allocated.Contains(id))
        {
            DeallocateWithDependents(id);
            return;
        }
        // Reject IDs that don't belong to either the base tree or an active cluster subgraph.
        if (!CanAllocate(id))
        {
            return;
        }
        _allocated.Add(id);
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    // Removes `id` and any allocated node whose only path to the class-start went
    // through it. BFS over the allocated subgraph from the start, pretending `id`
    // is gone — anything unvisited is a dependent.
    private void DeallocateWithDependents(int id)
    {
        var wasActiveRadiusSocket = _activeRadiusEffects.Any(effect => effect.SocketNodeId == id);
        var affected = AllocatedComponentFrom(id);
        var roots = DependencyRoots(id).ToList();
        if (roots.Count == 0)
        {
            _allocated.Remove(id);
            _masterySelections.Remove(id);
            _allocationSets.Remove(id);
            RebuildActiveRadiusEffects();
            if (wasActiveRadiusSocket)
            {
                PruneInvalidRadiusOnlyAllocations();
            }
            SpecChanged?.Invoke();
            return;
        }

        var reachable = new HashSet<int>();
        var queue = new Queue<Node>();
        foreach (var r in roots)
        {
            if (reachable.Add(r.Id))
            {
                queue.Enqueue(r);
            }
        }
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Type == NodeType.Mastery)
            {
                continue;
            }
            foreach (var other in node.LinkedNodes)
            {
                if (other.Id == id || !_allocated.Contains(other.Id))
                {
                    continue;
                }
                if (reachable.Add(other.Id))
                {
                    queue.Enqueue(other);
                }
            }
        }

        _allocated.Remove(id);
        _masterySelections.Remove(id);
        _allocationSets.Remove(id);
        var orphans = _allocated.Where(a => affected.Contains(a) && !reachable.Contains(a)).ToList();
        foreach (var o in orphans)
        {
            _allocated.Remove(o);
            _masterySelections.Remove(o);
            _allocationSets.Remove(o);
        }
        RebuildActiveRadiusEffects();
        if (wasActiveRadiusSocket)
        {
            PruneInvalidRadiusOnlyAllocations();
        }
        SpecChanged?.Invoke();
    }

    private HashSet<int> AllocatedComponentFrom(int id)
    {
        var component = new HashSet<int>();
        if (!TryGetNode(id, out var start) || start is null || !_allocated.Contains(id))
        {
            return component;
        }

        var queue = new Queue<Node>();
        component.Add(id);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Type == NodeType.Mastery)
            {
                continue;
            }
            foreach (var other in node.LinkedNodes)
            {
                if (!_allocated.Contains(other.Id) || !component.Add(other.Id))
                {
                    continue;
                }
                queue.Enqueue(other);
            }
        }
        return component;
    }

    private IEnumerable<Node> DependencyRoots(int excludeId)
    {
        var roots = new List<Node>();
        if (_classStartNodeByIndex.TryGetValue(_selectedClassIndex, out var startId)
            && startId != excludeId
            && _allocated.Contains(startId))
        {
            roots.Add(Tree.Nodes[startId]);
        }
        if (SelectedAscendancyStartNodeId() is { } ascendancyStartId
            && ascendancyStartId != excludeId
            && Tree.Nodes.TryGetValue(ascendancyStartId, out var ascendancyStart))
        {
            roots.Add(ascendancyStart);
        }
        return roots;
    }

    public void AllocateMany(IEnumerable<int> ids)
    {
        var changed = false;
        foreach (var id in ids)
        {
            if (TryGetNode(id, out var node) && node is not null && CanAllocateNodeRules(node) && _allocated.Add(id))
            {
                changed = true;
            }
        }
        if (changed)
        {
            RebuildActiveRadiusEffects();
            SpecChanged?.Invoke();
        }
    }

    // Shortest path from the allocated subgraph to the given target (BFS over node
    // links). Path rules mirror PoB (PassiveSpec.lua:911): can't leave a mastery,
    // can't step into a class/ascendancy start. Ascendancy-boundary edges are
    // already filtered out at load time (TreeLoader.cs:110).
    public HoverPath HoverPathTo(int targetId)
    {
        if (!TryGetNode(targetId, out var target)
            || _allocated.Contains(targetId)
            || !CanAllocateNodeRules(target!)
            || target!.Type is NodeType.Proxy or NodeType.ClassStart or NodeType.AscendancyStart)
        {
            return HoverPath.Empty;
        }
        if (IsAllocatedByRadiusJewel(targetId))
        {
            return new HoverPath([targetId], new HashSet<(int, int)>());
        }

        // Seed the BFS from all currently-allocated nodes (including cluster nodes).
        var parent = new Dictionary<int, int>();
        var visited = new HashSet<int>();
        var queue = new Queue<Node>();
        foreach (var id in _allocated)
        {
            if (TryGetNode(id, out var root) && visited.Add(id))
            {
                queue.Enqueue(root!);
            }
        }
        if (queue.Count == 0)
        {
            return HoverPath.Empty;
        }

        var found = false;
        while (queue.Count > 0 && !found)
        {
            var node = queue.Dequeue();
            if (node.Type == NodeType.Mastery)
            {
                continue;
            }
            foreach (var other in node.LinkedNodes)
            {
                if (!visited.Add(other.Id))
                {
                    continue;
                }
                if (other.Type is NodeType.Proxy or NodeType.ClassStart or NodeType.AscendancyStart)
                {
                    continue;
                }
                if (!CanAllocateNodeRules(other))
                {
                    continue;
                }
                parent[other.Id] = node.Id;
                if (other.Id == targetId)
                {
                    found = true;
                    break;
                }
                queue.Enqueue(other);
            }
        }
        if (!found)
        {
            return HoverPath.Empty;
        }

        var nodes = new List<int>();
        var edges = new HashSet<(int, int)>();
        var cur = targetId;
        while (true)
        {
            if (!_allocated.Contains(cur))
            {
                nodes.Add(cur);
            }
            if (!parent.TryGetValue(cur, out var pid))
            {
                break;
            }
            edges.Add((Math.Min(cur, pid), Math.Max(cur, pid)));
            cur = pid;
        }
        nodes.Reverse();
        return new HoverPath(nodes, edges);
    }

    public void Clear()
    {
        if (_allocated.Count == 0 && _masterySelections.Count == 0 && _attributeOverrides.Count == 0 && _allocationSets.Count == 0 && _activeSubgraphs.Count == 0 && _socketedJewels.Count == 0)
        {
            return;
        }
        foreach (var socketId in _activeSubgraphs.Keys.ToArray())
        {
            RemoveClusterRecursive(socketId);
        }
        _allocated.Clear();
        _masterySelections.Clear();
        _attributeOverrides.Clear();
        _allocationSets.Clear();
        _socketedJewels.Clear();
        _selectedAscendancyIndex = 0;
        if (_classStartNodeByIndex.TryGetValue(_selectedClassIndex, out var nodeId))
        {
            _allocated.Add(nodeId);
        }
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    private bool CanAllocate(int id)
    {
        if (!TryGetNode(id, out var node) || node is null)
        {
            return false;
        }
        return CanAllocateNodeRules(node)
            && (IsNormallyReachableForAllocation(node) || IsAllocatedByRadiusJewel(node.Id));
    }

    private bool CanAllocateNodeRules(Node node)
    {
        if (Tree.GameId == GameId.PathOfExile2 && node.Type == NodeType.Mastery)
        {
            return false;
        }

        if (node.AscendancyName is not { } ascendancyName)
        {
            return true;
        }
        return SelectedAscendancyName == ascendancyName;
    }

    private bool IsNormallyReachableForAllocation(Node node)
    {
        if (_allocated.Contains(node.Id))
        {
            return true;
        }
        if (node.Type is NodeType.ClassStart or NodeType.AscendancyStart)
        {
            return false;
        }
        foreach (var linked in node.LinkedNodes)
        {
            if (_allocated.Contains(linked.Id))
            {
                return true;
            }
        }
        return false;
    }

    private HashSet<int> NormallyReachableAllocatedNodes(IEnumerable<int> rootIds)
    {
        var reachable = new HashSet<int>();
        var queue = new Queue<Node>();
        foreach (var rootId in rootIds)
        {
            if (TryGetNode(rootId, out var root) && root is not null && _allocated.Contains(rootId) && reachable.Add(rootId))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Type == NodeType.Mastery)
            {
                continue;
            }
            foreach (var other in node.LinkedNodes)
            {
                if (!_allocated.Contains(other.Id) || !reachable.Add(other.Id))
                {
                    continue;
                }
                queue.Enqueue(other);
            }
        }
        return reachable;
    }
}

public sealed record HoverPath(IReadOnlyList<int> Nodes, IReadOnlySet<(int Min, int Max)> Edges)
{
    public static readonly HoverPath Empty = new(Array.Empty<int>(), new HashSet<(int, int)>());
    public bool IsEmpty => Nodes.Count == 0;
}
