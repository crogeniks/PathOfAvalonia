using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeDomain;

public sealed partial class PassiveSpec
{
    private static readonly ClusterJewelSize[] LargeSocketAllowedSizes = [ClusterJewelSize.Large, ClusterJewelSize.Medium, ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] MediumSocketAllowedSizes = [ClusterJewelSize.Medium, ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] SmallSocketAllowedSizes = [ClusterJewelSize.Small];
    private static readonly ClusterJewelSize[] NoClusterSizes = [];

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
