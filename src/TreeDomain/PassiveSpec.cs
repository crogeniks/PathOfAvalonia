using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;

namespace PathOfAvalonia.TreeDomain;

public sealed class PassiveSpec
{
    public TreeModel Tree { get; }
    public ClassCatalog Classes { get; }
    public GameFeatureFlags Features { get; }
    private static readonly ClusterJewelSize[] LargeSocketAllowedSizes = [ClusterJewelSize.Large, ClusterJewelSize.Medium, ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] MediumSocketAllowedSizes = [ClusterJewelSize.Medium, ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] SmallSocketAllowedSizes = [ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] NoClusterSizes = [];

    private readonly HashSet<int> _allocated = new();
    private readonly Dictionary<int, int> _masterySelections = new();
    private readonly Dictionary<int, AttributeNodeOverride> _attributeOverrides = new();
    private readonly Dictionary<int, PassiveAllocationSet> _allocationSets = new();
    private readonly Dictionary<int, int> _classStartNodeByIndex;
    private readonly Dictionary<string, int> _ascendancyStartNodeByName;
    private int _selectedClassIndex;
    private int _selectedAscendancyIndex;

    private readonly Dictionary<int, ClusterSubgraph> _activeSubgraphs = new();
    // Flat lookup for all currently-active cluster nodes by ID.
    private readonly Dictionary<int, Node> _clusterNodes = new();
    private readonly Dictionary<int, ImportedItem> _socketedJewels = new();
    private readonly JewelRadiusTable _jewelRadiusTable;
    private readonly IReadOnlyDictionary<int, RadiusMembership> _socketRadiusMembership;
    private readonly IReadOnlyDictionary<int, RadiusMembership> _keystoneRadiusMembership;
    private readonly Dictionary<string, int> _keystoneNodeIdsByName;
    private readonly List<RadiusJewelEffect> _activeRadiusEffects = new();
    private readonly List<JewelRadiusVisual> _activeJewelRadii = new();

    public IReadOnlyDictionary<int, ClusterSubgraph> ActiveSubgraphs => _activeSubgraphs;
    public IReadOnlyDictionary<int, ImportedItem> SocketedJewels => _socketedJewels;
    public IReadOnlyDictionary<int, AttributeNodeOverride> AttributeOverrides => _attributeOverrides;
    public IReadOnlyDictionary<int, PassiveAllocationSet> AllocationSets => _allocationSets;
    public IReadOnlyList<JewelRadiusVisual> ActiveJewelRadii => _activeJewelRadii;

    public PassiveSpec(TreeModel tree)
        : this(tree, tree.Classes, tree.GameId == GameId.PathOfExile2 ? GameFeatureFlags.Poe2Milestone2 : GameFeatureFlags.Poe1)
    {
    }

    public PassiveSpec(TreeModel tree, ClassCatalog classes, GameFeatureFlags features)
    {
        Tree = tree;
        Classes = classes;
        Features = features;
        _jewelRadiusTable = JewelRadiusTable.For(tree.GameId, tree.Version);
        _socketRadiusMembership = RadiusMembership.BuildForSockets(tree, _jewelRadiusTable);
        _keystoneRadiusMembership = RadiusMembership.BuildForKeystones(tree, _jewelRadiusTable);
        _keystoneNodeIdsByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _classStartNodeByIndex = new Dictionary<int, int>();
        _ascendancyStartNodeByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in tree.Nodes.Values)
        {
            if (n is { Type: NodeType.ClassStart, ClassStartIndex: { } idx })
            {
                _classStartNodeByIndex[idx] = n.Id;
            }
            switch (n.Type)
            {
                case NodeType.ClassStart:
                {
                    foreach (var classStartIdx in n.ClassStartIndexes)
                    {
                        _classStartNodeByIndex[classStartIdx] = n.Id;
                    }

                    break;
                }
                case NodeType.AscendancyStart when n.AscendancyName is { } ascendancyName:
                    _ascendancyStartNodeByName[ascendancyName] = n.Id;
                    break;
                case NodeType.Keystone:
                    _keystoneNodeIdsByName[n.Name] = n.Id;
                    break;
            }
        }
        _selectedClassIndex = 0;
        _selectedAscendancyIndex = 0;
        if (_classStartNodeByIndex.TryGetValue(0, out var startId))
        {
            _allocated.Add(startId);
        }
        RebuildActiveRadiusEffects();
    }

    public IReadOnlySet<int> AllocatedNodes => _allocated;
    public bool IsAllocated(int id) => _allocated.Contains(id);
    public PassiveAllocationSet AllocationSetOf(int nodeId) =>
        _allocationSets.GetValueOrDefault(nodeId, PassiveAllocationSet.Normal);

    public bool TryGetSocketedJewel(int socketNodeId, out ImportedItem item) =>
        _socketedJewels.TryGetValue(socketNodeId, out item!);

    public EffectiveNodeView EffectiveNode(int nodeId)
    {
        if (!TryGetNode(nodeId, out var node) || node is null)
        {
            throw new KeyNotFoundException($"Node {nodeId} is not present in the passive tree.");
        }

        var stats = (IReadOnlyList<string>)node.Stats;
        var affectedBy = new List<int>();
        TimelessConqueror? conqueror = null;
        foreach (var effect in _activeRadiusEffects.Where(effect => EffectAffectsNode(effect, nodeId)))
        {
            affectedBy.Add(effect.SocketNodeId);
            if (effect.Conqueror is { } c)
            {
                conqueror = c;
            }
            foreach (var transform in effect.NodeTransforms)
            {
                stats = transform.Apply(node, stats);
            }
        }

        return new EffectiveNodeView(node, stats, conqueror is not null, conqueror, affectedBy);
    }

    public bool IsAllocatedByRadiusJewel(int nodeId) =>
        _activeRadiusEffects.Any(effect =>
            effect.AllowsUnconnectedAllocation
            && EffectAffectsNode(effect, nodeId));

    public int SelectedClassIndex => _selectedClassIndex;
    public int SelectedAscendancyIndex => _selectedAscendancyIndex;

    public IReadOnlyList<ClusterJewelSize> AllowedClusterSizes(int socketId)
    {
        if (!Features.SupportsClusterJewels
            || !TryGetNode(socketId, out var socket)
            || socket?.Type != NodeType.JewelSocket
            || socket.ExpansionSocket is not { } expansionSocket)
        {
            return NoClusterSizes;
        }

        return expansionSocket.Size switch
        {
            2 => LargeSocketAllowedSizes,
            1 => MediumSocketAllowedSizes,
            0 => SmallSocketAllowedSizes,
            _ => NoClusterSizes,
        };
    }

    public bool CanInsertCluster(int socketId, ClusterJewelSize size) =>
        AllowedClusterSizes(socketId).Contains(size);

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
        _attributeOverrides.Clear();
        _allocationSets.Clear();
        _selectedClassIndex = classIndex;
        _selectedAscendancyIndex = 0;
        _allocated.Add(_classStartNodeByIndex[classIndex]);
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    public void SetAscendancy(int ascendancyIndex)
    {
        var names = Classes.AscendancyNames(_selectedClassIndex);
        if (ascendancyIndex < 0 || ascendancyIndex >= names.Count || _selectedAscendancyIndex == ascendancyIndex)
        {
            return;
        }

        RemoveSelectedAscendancyAllocations();
        _selectedAscendancyIndex = ascendancyIndex;
        if (SelectedAscendancyName is { } ascendancyName
            && _ascendancyStartNodeByName.TryGetValue(ascendancyName, out var startId))
        {
            _allocated.Add(startId);
        }
        RebuildActiveRadiusEffects();
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

    // Inserts or removes a cluster jewel in a base-tree JewelSocket.
    // Passing null for spec removes any existing cluster at that socket.
    public void SetClusterJewel(int socketId, ClusterJewelSpec? spec)
    {
        RemoveClusterRecursive(socketId);

        if (spec is null || !TryGetNode(socketId, out var socket) || socket is null || !CanInsertCluster(socketId, spec.Size))
        {
            SpecChanged?.Invoke();
            return;
        }

        var lineageIdBase = ResolveClusterLineageIdBase(socket);
        var clusterNodeIdBase = lineageIdBase + (ClusterJewelData.GetDefinition(spec.Size).SizeIndex << 4);
        var subgraph = ClusterJewelResolver.Resolve(Tree, socket, spec, lineageIdBase, clusterNodeIdBase);
        _activeSubgraphs[socketId] = subgraph;
        foreach (var n in subgraph.Nodes)
        {
            _clusterNodes[n.Id] = n;
        }

        // Wire socket ↔ entry node bidirectionally so the cluster is reachable from the tree.
        var entranceNode = subgraph.NodesById[subgraph.EntranceNodeId];
        socket.LinkedNodes.Add(entranceNode);
        entranceNode.LinkedNodes.Add(socket);

        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
    }

    public void RemoveClusterJewel(int socketId)
    {
        var changed = RemoveClusterRecursive(socketId);
        changed |= _socketedJewels.Remove(socketId);
        if (changed)
        {
            RebuildActiveRadiusEffects();
            PruneInvalidRadiusOnlyAllocations();
            SpecChanged?.Invoke();
        }
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

    // Replace allocation with the set derived from an imported build. Hashes not present
    // in the current tree are silently skipped (cluster-jewel subgraph ids ≥ 65536 aren't
    // part of the base tree data), and the caller gets the applied/skipped counts back.
    public ImportResult ApplyImport(ImportedBuild build)
    {
        _allocated.Clear();
        _masterySelections.Clear();
        _attributeOverrides.Clear();
        _allocationSets.Clear();
        _socketedJewels.Clear();
        foreach (var socketId in _activeSubgraphs.Keys.ToArray())
        {
            RemoveClusterRecursive(socketId);
        }
        var importedClassIndex = Classes.ResolveClassIndex(build);
        if (_classStartNodeByIndex.ContainsKey(importedClassIndex))
        {
            _selectedClassIndex = importedClassIndex;
        }
        _allocated.Add(_classStartNodeByIndex[_selectedClassIndex]);
        _selectedAscendancyIndex = Classes.ResolveAscendancyIndex(_selectedClassIndex, build);
        if (SelectedAscendancyStartNodeId() is { } ascendancyStartId)
        {
            _allocated.Add(ascendancyStartId);
        }
        var applied = 0;
        var skipped = 0;
        var clusterSkipped = 0;
        var unsupportedClusterJewels = 0;
        var unsupportedAttributeOverrides = 0;
        var unsupportedSocketedJewels = 0;
        var pendingJewels = build.SocketedJewels.ToList();
        if (Features.SupportsClusterJewels)
        {
            var restoredAny = true;
            while (restoredAny && pendingJewels.Count > 0)
            {
                restoredAny = false;
                var socketIdMap = build.ClusterHashFormatVersion < 2
                    ? BuildLegacyClusterIdMap()
                    : null;
                for (var i = pendingJewels.Count - 1; i >= 0; i--)
                {
                    var socketedJewel = pendingJewels[i];
                    if (socketIdMap is not null && socketIdMap.TryGetValue(socketedJewel.SocketNodeId, out var mappedSocketId))
                    {
                        socketedJewel = socketedJewel with { SocketNodeId = mappedSocketId };
                    }
                    if (RestoreImportedCluster(socketedJewel, build))
                    {
                        pendingJewels.RemoveAt(i);
                        restoredAny = true;
                    }
                }
            }
        }
        else
        {
            unsupportedClusterJewels = build.ClusterNodeHashes.Count;
        }

        var legacyClusterIdMap = build.ClusterHashFormatVersion < 2
            ? BuildLegacyClusterIdMap()
            : null;

        foreach (var socketedJewel in build.SocketedJewels)
        {
            var socketNodeId = legacyClusterIdMap is not null && legacyClusterIdMap.TryGetValue(socketedJewel.SocketNodeId, out var mappedSocketId)
                ? mappedSocketId
                : socketedJewel.SocketNodeId;
            if (!Features.SupportsPassiveTreeJewels)
            {
                unsupportedSocketedJewels++;
            }
            else if (Tree.Nodes.ContainsKey(socketNodeId) && build.ItemsById.TryGetValue(socketedJewel.ItemId, out var item))
            {
                _socketedJewels[socketNodeId] = item;
            }
        }

        foreach (var id in build.NodeHashes)
        {
            var mappedId = legacyClusterIdMap is not null && legacyClusterIdMap.TryGetValue(id, out var legacyMappedId)
                ? legacyMappedId
                : id;
            if (Tree.Nodes.ContainsKey(mappedId) || _clusterNodes.ContainsKey(mappedId))
            {
                _allocated.Add(mappedId);
                StoreImportedAllocationSet(build, id, mappedId);
                applied++;
            }
            else
            {
                skipped++;
            }
        }
        foreach (var (nodeId, effectId) in build.MasterySelections)
        {
            if (Features.SupportsMasterySelections && _allocated.Contains(nodeId))
            {
                _masterySelections[nodeId] = effectId;
            }
        }
        foreach (var id in build.ClusterNodeHashes)
        {
            if (!Features.SupportsClusterJewels)
            {
                skipped++;
                clusterSkipped++;
                continue;
            }
            var mappedId = legacyClusterIdMap is not null && legacyClusterIdMap.TryGetValue(id, out var legacyMappedId)
                ? legacyMappedId
                : id;
            if (_clusterNodes.ContainsKey(mappedId))
            {
                _allocated.Add(mappedId);
                StoreImportedAllocationSet(build, id, mappedId);
                applied++;
            }
            else
            {
                skipped++;
                clusterSkipped++;
            }
        }
        var fallbackApplied = AllocateUniqueClusterFallbacks();
        applied += fallbackApplied;
        skipped = Math.Max(0, skipped - fallbackApplied);
        clusterSkipped = Math.Max(0, clusterSkipped - fallbackApplied);
        if (Features.SupportsAttributeOverrides)
        {
            foreach (var (nodeId, attribute) in build.AttributeOverrides)
            {
                _attributeOverrides[nodeId] = attribute;
            }
        }
        else
        {
            unsupportedAttributeOverrides = build.AttributeOverrides.Count;
        }
        RebuildActiveRadiusEffects();
        SpecChanged?.Invoke();
        return new ImportResult(applied, skipped, clusterSkipped, build)
        {
            UnsupportedClusterJewels = unsupportedClusterJewels,
            UnsupportedAttributeOverrides = unsupportedAttributeOverrides,
            UnsupportedSocketedJewels = unsupportedSocketedJewels,
            WeaponSet1Allocations = _allocationSets.Count(kv => kv.Value == PassiveAllocationSet.WeaponSet1),
            WeaponSet2Allocations = _allocationSets.Count(kv => kv.Value == PassiveAllocationSet.WeaponSet2),
        };
    }

    private void StoreImportedAllocationSet(ImportedBuild build, int importedId, int mappedId)
    {
        if ((build.AllocationSets.TryGetValue(mappedId, out var set)
                || build.AllocationSets.TryGetValue(importedId, out set))
            && set != PassiveAllocationSet.Normal)
        {
            _allocationSets[mappedId] = set;
        }
    }

    private int AllocateUniqueClusterFallbacks()
    {
        var applied = 0;
        foreach (var (socketId, subgraph) in _activeSubgraphs)
        {
            if (subgraph.Nodes.Any(node => node.Id >= 65536 && _allocated.Contains(node.Id)))
            {
                continue;
            }
            if (!TryGetSocketedClusterItem(socketId, out var item, out var cluster))
            {
                continue;
            }
            if (!IsUniqueClusterFallbackCandidate(item, cluster))
            {
                continue;
            }

            foreach (var node in subgraph.Nodes.Where(node => node.Type != NodeType.JewelSocket))
            {
                if (_allocated.Add(node.Id))
                {
                    applied++;
                }
            }
        }
        return applied;
    }

    private bool TryGetSocketedClusterItem(int socketId, out ImportedItem item, out ImportedClusterJewel cluster)
    {
        item = null!;
        cluster = null!;
        return _socketedJewels.TryGetValue(socketId, out item!)
            && ImportedClusterJewelParser.TryParse(item, out cluster);
    }

    private static bool IsUniqueClusterFallbackCandidate(ImportedItem item, ImportedClusterJewel cluster) =>
        item.Rarity.Equals("Unique", StringComparison.OrdinalIgnoreCase)
        && (cluster.KeystoneName is not null
            || item.Name.Equals("Megalomaniac", StringComparison.OrdinalIgnoreCase));

    private Dictionary<int, int> BuildLegacyClusterIdMap()
    {
        var map = new Dictionary<int, int>();
        foreach (var subgraph in _activeSubgraphs.Values)
        {
            if (!TryGetNode(subgraph.SocketNodeId, out var socketNode)
                || socketNode?.ExpansionSocket is not { } expansionSocket
                || !Tree.Nodes.TryGetValue(expansionSocket.ProxyNodeId, out var proxyNode))
            {
                continue;
            }

            var definition = ClusterJewelData.GetDefinition(subgraph.Size);
            var currentNodeBase = subgraph.LineageIdBase + (definition.SizeIndex << 4);
            var legacyProxyGroupId = BuildLegacyProxyGroup(proxyNode.GroupId, expansionSocket.Size, definition.SizeIndex);

            foreach (var currentSocket in subgraph.Nodes.Where(node => node.Type == NodeType.JewelSocket && node.ExpansionSocket is not null))
            {
                if (FindClusterSocket(legacyProxyGroupId, currentSocket.ExpansionSocket!.Index) is { } legacySocket)
                {
                    map[legacySocket.Id] = currentSocket.Id;
                }
            }

            if (subgraph.Nodes.Count == 1 && subgraph.Nodes[0].Type == NodeType.Keystone)
            {
                continue;
            }

            var currentSkillsPerOrbit = Tree.SkillsPerOrbit[definition.SizeIndex + 1];
            var legacySkillsPerOrbit = Tree.SkillsPerOrbit[proxyNode.Orbit];
            var legacyProxyNodeIndex = TranslateClusterOrbitIndex(
                proxyNode.OrbitIndex,
                legacySkillsPerOrbit,
                definition.TotalIndices);

            var legacyNodeIdsByOrbit = new Dictionary<int, int>();
            var currentNodeIdsByLegacyOrbit = new Dictionary<int, int>();
            foreach (var node in subgraph.Nodes.Where(node => node.Id >= 65536))
            {
                var nodeIndex = node.Id - currentNodeBase;
                if (nodeIndex < 0 || nodeIndex >= definition.TotalIndices)
                {
                    continue;
                }

                var legacyNodeIndex = (nodeIndex + legacyProxyNodeIndex) % definition.TotalIndices;
                var legacyOrbitIndex = TranslateClusterOrbitIndex(
                    legacyNodeIndex,
                    definition.TotalIndices,
                    legacySkillsPerOrbit);
                legacyNodeIdsByOrbit[legacyOrbitIndex] = node.Id;

                var currentNodeIndex = TranslateClusterOrbitIndex(
                    node.OrbitIndex,
                    currentSkillsPerOrbit,
                    definition.TotalIndices);
                var currentLegacyOrbitIndex = TranslateClusterOrbitIndex(
                    currentNodeIndex,
                    definition.TotalIndices,
                    legacySkillsPerOrbit);
                currentNodeIdsByLegacyOrbit[currentLegacyOrbitIndex] = node.Id;
            }

            foreach (var (orbitIndex, legacyNodeId) in legacyNodeIdsByOrbit)
            {
                if (currentNodeIdsByLegacyOrbit.TryGetValue(orbitIndex, out var currentNodeId))
                {
                    map[legacyNodeId] = currentNodeId;
                }
            }
        }
        return map;
    }

    // Looks up a node by ID in both the base tree and any active cluster subgraphs.
    private bool TryGetNode(int id, out Node? node)
    {
        if (_clusterNodes.TryGetValue(id, out node))
        {
            return true;
        }
        return Tree.Nodes.TryGetValue(id, out node);
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

    public event Action? SpecChanged;

    private bool RemoveClusterRecursive(int socketId)
    {
        if (!_activeSubgraphs.TryGetValue(socketId, out var old))
        {
            return false;
        }

        foreach (var childSocket in old.Nodes.Where(node => node.Type == NodeType.JewelSocket).Select(node => node.Id).ToArray())
        {
            RemoveClusterRecursive(childSocket);
            _socketedJewels.Remove(childSocket);
        }

        if (TryGetNode(socketId, out var oldSocket) && oldSocket is not null
            && old.NodesById.TryGetValue(old.EntranceNodeId, out var entranceNode))
        {
            oldSocket.LinkedNodes.Remove(entranceNode);
            entranceNode.LinkedNodes.Remove(oldSocket);
        }

        foreach (var n in old.Nodes)
        {
            _allocated.Remove(n.Id);
            _allocationSets.Remove(n.Id);
            _clusterNodes.Remove(n.Id);
        }

        _activeSubgraphs.Remove(socketId);
        _socketedJewels.Remove(socketId);
        RebuildActiveRadiusEffects();
        return true;
    }

    private void RebuildActiveRadiusEffects()
    {
        _activeRadiusEffects.Clear();
        _activeJewelRadii.Clear();
        if (!Features.SupportsPassiveTreeJewels)
        {
            return;
        }

        foreach (var (socketNodeId, item) in _socketedJewels)
        {
            if (!_allocated.Contains(socketNodeId))
            {
                continue;
            }
            if (!Tree.Nodes.TryGetValue(socketNodeId, out var socket))
            {
                continue;
            }
            var effect = RadiusJewelParser.Parse(item, socketNodeId, Tree, _jewelRadiusTable, _keystoneNodeIdsByName);
            if (effect is null || !_jewelRadiusTable.TryGet(effect.RadiusIndex, out var band))
            {
                continue;
            }

            _activeRadiusEffects.Add(effect);
            var center = effect.AlternateCenterNodeId is { } centerNodeId && Tree.Nodes.TryGetValue(centerNodeId, out var alternate)
                ? alternate
                : socket;
            _activeJewelRadii.Add(new JewelRadiusVisual(
                socketNodeId,
                center.X,
                center.Y,
                band.Inner,
                band.Outer,
                VisualStyle(effect, band),
                effect.Conqueror));
        }
    }

    private static JewelRadiusVisualStyle VisualStyle(RadiusJewelEffect effect, JewelRadiusBand band)
    {
        if (effect.Conqueror is not null)
        {
            return JewelRadiusVisualStyle.Timeless;
        }
        if (effect.AlternateCenterNodeId is not null)
        {
            return JewelRadiusVisualStyle.KeystoneCentered;
        }
        return band.IsAnnulus ? JewelRadiusVisualStyle.Annulus : JewelRadiusVisualStyle.Normal;
    }

    private bool EffectAffectsNode(RadiusJewelEffect effect, int nodeId)
    {
        var sourceId = effect.AlternateCenterNodeId ?? effect.SocketNodeId;
        var memberships = effect.AlternateCenterNodeId is not null ? _keystoneRadiusMembership : _socketRadiusMembership;
        return memberships.TryGetValue(sourceId, out var membership)
            && membership.Contains(effect.RadiusIndex, nodeId);
    }

    private void PruneInvalidRadiusOnlyAllocations()
    {
        var roots = DependencyRoots(excludeId: -1).Select(r => r.Id).ToArray();
        if (roots.Length == 0)
        {
            return;
        }

        var reachable = NormallyReachableAllocatedNodes(roots);
        var changed = false;
        foreach (var id in _allocated.ToArray())
        {
            if (reachable.Contains(id) || IsAllocatedByRadiusJewel(id))
            {
                continue;
            }
            _allocated.Remove(id);
            _masterySelections.Remove(id);
            _allocationSets.Remove(id);
            changed = true;
        }
        if (changed)
        {
            RebuildActiveRadiusEffects();
        }
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

    private bool RestoreImportedCluster(ImportedSocketedJewel socketedJewel, ImportedBuild build)
    {
        if (!build.ItemsById.TryGetValue(socketedJewel.ItemId, out var item)
            || !ImportedClusterJewelParser.TryParse(item, out var cluster))
        {
            return false;
        }

        if (!CanInsertCluster(socketedJewel.SocketNodeId, cluster.Size))
        {
            return false;
        }

        SetClusterJewel(
            socketedJewel.SocketNodeId,
            new ClusterJewelSpec(
                socketedJewel.SocketNodeId,
                cluster.Size,
                cluster.PassiveCount,
                cluster.SocketCount,
                cluster.NotableNames,
                cluster.KeystoneName));
        return true;
    }

    private string? SelectedAscendancyName => Classes.AscendancyTreeName(_selectedClassIndex, _selectedAscendancyIndex);

    private int? SelectedAscendancyStartNodeId()
    {
        if (SelectedAscendancyName is not { } ascendancyName)
        {
            return null;
        }
        return _ascendancyStartNodeByName.TryGetValue(ascendancyName, out var startId) ? startId : null;
    }

    private void RemoveSelectedAscendancyAllocations()
    {
        if (SelectedAscendancyName is not { } ascendancyName)
        {
            return;
        }
        foreach (var id in _allocated.ToArray())
        {
            if (Tree.Nodes.TryGetValue(id, out var node) && node.AscendancyName == ascendancyName)
            {
                _allocated.Remove(id);
                _masterySelections.Remove(id);
                _allocationSets.Remove(id);
            }
        }
    }

    private int BuildLegacyProxyGroup(int proxyGroupId, int expansionJewelSize, int clusterSizeIndex)
    {
        var legacyGroupId = proxyGroupId;
        var groupSize = expansionJewelSize;
        var guard = 0;
        while (clusterSizeIndex < groupSize && guard < 4)
        {
            var socket = FindClusterSocket(legacyGroupId, 1) ?? FindClusterSocket(legacyGroupId, 0);
            if (socket?.ExpansionSocket is not { } expansionSocket
                || !Tree.Nodes.TryGetValue(expansionSocket.ProxyNodeId, out var legacyProxyNode)
                || !Tree.Groups.ContainsKey(legacyProxyNode.GroupId))
            {
                break;
            }

            legacyGroupId = legacyProxyNode.GroupId;
            groupSize = expansionSocket.Size;
            guard++;
        }

        return legacyGroupId;
    }

    private Node? FindClusterSocket(int groupId, int index)
    {
        foreach (var node in Tree.Nodes.Values)
        {
            if (node.GroupId == groupId
                && node.Type == NodeType.JewelSocket
                && node.ExpansionSocket?.Index == index)
            {
                return node;
            }
        }
        return null;
    }

    private static int TranslateClusterOrbitIndex(int sourceOrbitIndex, int sourceNodesPerOrbit, int destinationNodesPerOrbit)
    {
        if (sourceNodesPerOrbit == destinationNodesPerOrbit)
        {
            return sourceOrbitIndex;
        }
        if (sourceNodesPerOrbit == 12 && destinationNodesPerOrbit == 16)
        {
            return sourceOrbitIndex switch
            {
                0 => 0,
                1 => 1,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 7,
                6 => 8,
                7 => 9,
                8 => 11,
                9 => 12,
                10 => 13,
                11 => 15,
                _ => throw new ArgumentOutOfRangeException(nameof(sourceOrbitIndex)),
            };
        }
        if (sourceNodesPerOrbit == 16 && destinationNodesPerOrbit == 12)
        {
            return sourceOrbitIndex switch
            {
                0 => 0,
                1 => 1,
                2 => 1,
                3 => 2,
                4 => 3,
                5 => 4,
                6 => 4,
                7 => 5,
                8 => 6,
                9 => 7,
                10 => 7,
                11 => 8,
                12 => 9,
                13 => 10,
                14 => 10,
                15 => 11,
                _ => throw new ArgumentOutOfRangeException(nameof(sourceOrbitIndex)),
            };
        }
        if (sourceNodesPerOrbit == 6 && destinationNodesPerOrbit == 16)
        {
            return sourceOrbitIndex switch
            {
                0 => 0,
                1 => 3,
                2 => 5,
                3 => 8,
                4 => 11,
                5 => 13,
                _ => throw new ArgumentOutOfRangeException(nameof(sourceOrbitIndex)),
            };
        }
        if (sourceNodesPerOrbit == 16 && destinationNodesPerOrbit == 6)
        {
            return sourceOrbitIndex switch
            {
                0 => 0,
                1 => 0,
                2 => 0,
                3 => 1,
                4 => 1,
                5 => 2,
                6 => 2,
                7 => 2,
                8 => 3,
                9 => 3,
                10 => 3,
                11 => 4,
                12 => 4,
                13 => 5,
                14 => 5,
                15 => 5,
                _ => throw new ArgumentOutOfRangeException(nameof(sourceOrbitIndex)),
            };
        }
        return (int)Math.Floor(sourceOrbitIndex * destinationNodesPerOrbit / (double)sourceNodesPerOrbit);
    }

    private int ResolveClusterLineageIdBase(Node socket)
    {
        var baseId = 0x10000;
        foreach (var subgraph in _activeSubgraphs.Values)
        {
            if (subgraph.NodesById.ContainsKey(socket.Id))
            {
                baseId = subgraph.LineageIdBase;
                break;
            }
        }

        if (socket.ExpansionSocket is not { } expansionSocket)
        {
            return baseId;
        }

        return expansionSocket.Size switch
        {
            2 => baseId + (expansionSocket.Index << 6),
            1 => baseId + (expansionSocket.Index << 9),
            _ => baseId,
        };
    }
}

public sealed record ImportResult(int Applied, int Skipped, int ClusterSkipped, ImportedBuild Build)
{
    public int UnsupportedClusterJewels { get; init; }
    public int UnsupportedAttributeOverrides { get; init; }
    public int UnsupportedSocketedJewels { get; init; }
    public int WeaponSet1Allocations { get; init; }
    public int WeaponSet2Allocations { get; init; }
}

public sealed record HoverPath(IReadOnlyList<int> Nodes, IReadOnlySet<(int Min, int Max)> Edges)
{
    public static readonly HoverPath Empty = new(Array.Empty<int>(), new HashSet<(int, int)>());
    public bool IsEmpty => Nodes.Count == 0;
}
