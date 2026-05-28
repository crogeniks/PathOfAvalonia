using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
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
}

public sealed record ImportResult(int Applied, int Skipped, int ClusterSkipped, ImportedBuild Build)
{
    public int UnsupportedClusterJewels { get; init; }
    public int UnsupportedAttributeOverrides { get; init; }
    public int UnsupportedSocketedJewels { get; init; }
    public int WeaponSet1Allocations { get; init; }
    public int WeaponSet2Allocations { get; init; }
}
