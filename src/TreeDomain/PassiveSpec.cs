using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed class PassiveSpec
{
    public TreeModel Tree { get; }
    private readonly HashSet<int> _allocated = new();
    private readonly Dictionary<int, int> _masterySelections = new();
    private readonly Dictionary<int, int> _classStartNodeByIndex;
    private int _selectedClassIndex;

    public PassiveSpec(TreeModel tree)
    {
        Tree = tree;
        _classStartNodeByIndex = new Dictionary<int, int>();
        foreach (var n in tree.Nodes.Values)
        {
            if (n.Type == NodeType.ClassStart && n.ClassStartIndex is int idx)
            {
                _classStartNodeByIndex[idx] = n.Id;
            }
        }
        _selectedClassIndex = 0;
        if (_classStartNodeByIndex.TryGetValue(0, out var startId))
        {
            _allocated.Add(startId);
        }
    }

    public IReadOnlySet<int> AllocatedNodes => _allocated;
    public bool IsAllocated(int id) => _allocated.Contains(id);

    public int SelectedClassIndex => _selectedClassIndex;

    // Switch to a different starting class. Resets the allocation to just that class-start
    // node — paths valid under the old class don't carry over.
    public void SetClass(int classIndex)
    {
        if (_selectedClassIndex == classIndex)
        {
            return;
        }
        if (!_classStartNodeByIndex.ContainsKey(classIndex))
        {
            return;
        }
        _allocated.Clear();
        _masterySelections.Clear();
        _selectedClassIndex = classIndex;
        _allocated.Add(_classStartNodeByIndex[classIndex]);
        SpecChanged?.Invoke();
    }

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
        if (_allocated.Contains(id))
        {
            DeallocateWithDependents(id);
            return;
        }
        _allocated.Add(id);
        SpecChanged?.Invoke();
    }

    // Removes `id` and any allocated node whose only path to the class-start went
    // through it. BFS over the allocated subgraph from the start, pretending `id`
    // is gone — anything unvisited is a dependent.
    private void DeallocateWithDependents(int id)
    {
        var roots = DependencyRoots(id).ToList();
        if (roots.Count == 0)
        {
            _allocated.Remove(id);
            _masterySelections.Remove(id);
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
        var orphans = _allocated.Where(a => !reachable.Contains(a)).ToList();
        foreach (var o in orphans)
        {
            _allocated.Remove(o);
            _masterySelections.Remove(o);
        }
        SpecChanged?.Invoke();
    }

    private IEnumerable<Node> DependencyRoots(int excludeId)
    {
        if (_classStartNodeByIndex.TryGetValue(_selectedClassIndex, out var startId)
            && startId != excludeId
            && _allocated.Contains(startId))
        {
            return new[] { Tree.Nodes[startId] };
        }
        return Array.Empty<Node>();
    }

    public void AllocateMany(IEnumerable<int> ids)
    {
        var changed = false;
        foreach (var id in ids)
        {
            if (Tree.Nodes.ContainsKey(id) && _allocated.Add(id))
            {
                changed = true;
            }
        }
        if (changed)
        {
            SpecChanged?.Invoke();
        }
    }

    // Shortest path from the allocated subgraph to the given target (BFS over node
    // links). Path rules mirror PoB (PassiveSpec.lua:911): can't leave a mastery,
    // can't step into a class/ascendancy start. Ascendancy-boundary edges are
    // already filtered out at load time (TreeLoader.cs:110).
    public HoverPath HoverPathTo(int targetId)
    {
        if (!Tree.Nodes.TryGetValue(targetId, out var target)
            || _allocated.Contains(targetId)
            || target.Type is NodeType.Proxy or NodeType.ClassStart or NodeType.AscendancyStart)
        {
            return HoverPath.Empty;
        }

        var roots = _allocated.Select(id => Tree.Nodes[id]);

        var parent = new Dictionary<int, int>();
        var visited = new HashSet<int>();
        var queue = new Queue<Node>();
        foreach (var r in roots)
        {
            if (visited.Add(r.Id))
            {
                queue.Enqueue(r);
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
        if (_allocated.Count == 0 && _masterySelections.Count == 0)
        {
            return;
        }
        _allocated.Clear();
        _masterySelections.Clear();
        if (_classStartNodeByIndex.TryGetValue(_selectedClassIndex, out var nodeId))
        {
            _allocated.Add(nodeId);
        }
        SpecChanged?.Invoke();
    }

    // Replace allocation with the set derived from an imported build. Hashes not present
    // in the current tree are silently skipped (cluster-jewel subgraph ids ≥ 65536 aren't
    // part of the base tree data), and the caller gets the applied/skipped counts back.
    public ImportResult ApplyImport(ImportedBuild build)
    {
        _allocated.Clear();
        _masterySelections.Clear();
        if (_classStartNodeByIndex.ContainsKey(build.ClassId))
        {
            _selectedClassIndex = build.ClassId;
        }
        _allocated.Add(_classStartNodeByIndex[_selectedClassIndex]);
        var applied = 0;
        var skipped = 0;
        foreach (var id in build.NodeHashes)
        {
            if (Tree.Nodes.ContainsKey(id))
            {
                _allocated.Add(id);
                applied++;
            }
            else
            {
                skipped++;
            }
        }
        foreach (var (nodeId, effectId) in build.MasterySelections)
        {
            if (_allocated.Contains(nodeId))
            {
                _masterySelections[nodeId] = effectId;
            }
        }
        foreach (var id in build.ClusterNodeHashes)
        {
            // Cluster jewel subgraph nodes aren't in the base tree — record as skipped but
            // don't treat as an error. Rendering them requires the cluster-jewel resolver.
            skipped++;
        }
        SpecChanged?.Invoke();
        return new ImportResult(applied, skipped, build.ClusterNodeHashes.Count, build);
    }

    public event Action? SpecChanged;
}

public sealed record ImportResult(int Applied, int Skipped, int ClusterSkipped, ImportedBuild Build);

public sealed record HoverPath(IReadOnlyList<int> Nodes, IReadOnlySet<(int Min, int Max)> Edges)
{
    public static readonly HoverPath Empty = new(Array.Empty<int>(), new HashSet<(int, int)>());
    public bool IsEmpty => Nodes.Count == 0;
}

// Classes are indexed by the tree's classStartIndex. The 3.28 tree still uses the
// original 7-class lineup; newer POE2 trees would need a different table.
public static class CharacterClasses
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Scion", "Marauder", "Ranger", "Witch", "Duelist", "Templar", "Shadow",
    };
}
