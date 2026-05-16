using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeDomain.ClusterJewels;

public static class ClusterJewelResolver
{
    private static readonly IReadOnlyDictionary<int, int> TemplateToOrbit16 = new Dictionary<int, int>
    {
        [0] = 0, [1] = 1, [2] = 3, [3] = 4, [4] = 5, [5] = 7,
        [6] = 8, [7] = 9, [8] = 11, [9] = 12, [10] = 13, [11] = 15,
    };

    public static ClusterSubgraph Resolve(TreeModel tree, Node socketNode, ClusterJewelSpec spec, int lineageIdBase, int clusterNodeIdBase)
    {
        if (socketNode.ExpansionSocket is not { } expansionSocket)
        {
            throw new InvalidOperationException($"Socket {socketNode.Id} is not a cluster socket");
        }

        var definition = ClusterJewelData.GetDefinition(spec.Size);
        var passiveCount = Math.Clamp(spec.PassiveCount, definition.MinNodes, definition.MaxNodes);
        var socketCount = Math.Clamp(spec.SocketCount, 0, definition.SocketIndices.Count);

        if (!tree.Nodes.TryGetValue(expansionSocket.ProxyNodeId, out var proxyNode))
        {
            throw new KeyNotFoundException($"Cluster proxy node {expansionSocket.ProxyNodeId} was not found");
        }
        if (!tree.Groups.TryGetValue(proxyNode.GroupId, out var proxyGroup))
        {
            throw new KeyNotFoundException($"Cluster proxy group {proxyNode.GroupId} was not found");
        }

        var orbit = definition.SizeIndex + 1;
        var skillsPerOrbit = tree.SkillsPerOrbit[orbit];
        var orbitRadius = tree.OrbitRadii[orbit];
        var startOrbitIndex = ClusterJewelData.GetOrbitOffset(proxyNode.Id, definition.SizeIndex);
        var orbitIndicesByTemplateIndex = new Dictionary<int, int>();

        var realSocketsByIndex = FindRealSockets(tree, proxyNode.GroupId);
        var nodesByTemplateIndex = new Dictionary<int, Node>();
        var nodesById = new Dictionary<int, Node>();
        var notableNames = spec.NotableNames
            .OrderBy(name => ClusterJewelData.NotableSortOrder.TryGetValue(name, out var order) ? order : int.MaxValue)
            .ToList();

        var getJewels = new[] { 0, 2, 1 };
        if (spec.Size == ClusterJewelSize.Large && socketCount == 1)
        {
            AddSocketNode(definition.SocketIndices[2], 1);
        }
        else
        {
            for (var i = 0; i < socketCount; i++)
            {
                AddSocketNode(definition.SocketIndices[i], getJewels[i]);
            }
        }

        var notableCount = notableNames.Count;
        var smallCount = passiveCount - socketCount - notableCount;
        var notableTemplateIndices = BuildNotableTemplateIndices(definition, passiveCount, socketCount, notableCount, nodesByTemplateIndex);
        for (var i = 0; i < notableNames.Count && i < notableTemplateIndices.Count; i++)
        {
            AddNotableNode(notableTemplateIndices[i], notableNames[i]);
        }

        var smallTemplateIndices = BuildSmallTemplateIndices(definition, passiveCount, smallCount, nodesByTemplateIndex);
        for (var i = 0; i < smallCount && i < smallTemplateIndices.Count; i++)
        {
            AddSmallNode(smallTemplateIndices[i]);
        }

        if (!nodesByTemplateIndex.TryGetValue(0, out var entranceNode))
        {
            throw new InvalidOperationException($"Cluster jewel at socket {socketNode.Id} has no entrance node");
        }

        var occupiedTemplateIndices = nodesByTemplateIndex.Keys.OrderBy(v => v).ToList();
        var connectors = new List<Connector>();
        for (var i = 0; i < occupiedTemplateIndices.Count; i++)
        {
            var current = occupiedTemplateIndices[i];
            var nextIndex = i + 1;
            if (nextIndex >= occupiedTemplateIndices.Count)
            {
                if (spec.Size == ClusterJewelSize.Small)
                {
                    break;
                }
                nextIndex = 0;
            }

            var next = occupiedTemplateIndices[nextIndex];
            var from = nodesByTemplateIndex[current];
            var to = nodesByTemplateIndex[next];
            from.LinkedNodes.Add(to);
            to.LinkedNodes.Add(from);
            connectors.Add(BuildArcConnector(
                tree,
                proxyGroup,
                orbitRadius,
                orbit,
                from.Id,
                orbitIndicesByTemplateIndex[current],
                to.Id,
                orbitIndicesByTemplateIndex[next]));
        }

        return new ClusterSubgraph
        {
            SocketNodeId = socketNode.Id,
            EntranceNodeId = entranceNode.Id,
            LineageIdBase = lineageIdBase,
            Size = spec.Size,
            CircleRadius = orbitRadius,
            ClusterCenterX = proxyGroup.X,
            ClusterCenterY = proxyGroup.Y,
            Nodes = nodesByTemplateIndex.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray(),
            NodesById = nodesById,
            Connectors = connectors,
        };

        void AddSocketNode(int templateIndex, int jewelIndex)
        {
            if (!realSocketsByIndex.TryGetValue(jewelIndex, out var realSocket))
            {
                throw new KeyNotFoundException($"Cluster proxy group {proxyNode.GroupId} has no socket for index {jewelIndex}");
            }
            nodesByTemplateIndex[templateIndex] = AddNode(
                templateIndex,
                realSocket.Id,
                realSocket.Name,
                NodeType.JewelSocket,
                realSocket.Icon,
                realSocket.ActiveIcon,
                realSocket.InactiveIcon,
                realSocket.ExpansionSocket);
        }

        void AddNotableNode(int templateIndex, string notableName)
        {
            var baseNode = tree.Nodes.Values.FirstOrDefault(node => node.Type == NodeType.Notable && node.Name == notableName);
            nodesByTemplateIndex[templateIndex] = AddNode(
                templateIndex,
                clusterNodeIdBase + templateIndex,
                notableName,
                NodeType.Notable,
                baseNode?.Icon,
                baseNode?.ActiveIcon,
                baseNode?.InactiveIcon,
                null);
        }

        void AddSmallNode(int templateIndex)
        {
            nodesByTemplateIndex[templateIndex] = AddNode(
                templateIndex,
                clusterNodeIdBase + templateIndex,
                "Cluster Passive",
                NodeType.Normal,
                null,
                null,
                null,
                null);
        }

        Node AddNode(
            int templateIndex,
            int nodeId,
            string name,
            NodeType type,
            string? icon,
            string? activeIcon,
            string? inactiveIcon,
            ExpansionSocketInfo? nodeExpansionSocket)
        {
            var orbitIndex = MapOrbitIndex(templateIndex, startOrbitIndex, definition.TotalIndices, skillsPerOrbit);
            orbitIndicesByTemplateIndex[templateIndex] = orbitIndex;
            if (!tree.TryGetPosition(proxyNode.GroupId, orbit, orbitIndex, out var x, out var y))
            {
                throw new InvalidOperationException($"Failed to resolve cluster position for group {proxyNode.GroupId}, orbit {orbit}, index {orbitIndex}");
            }

            var node = new Node
            {
                Id = nodeId,
                Name = name,
                Type = type,
                X = x,
                Y = y,
                Icon = icon,
                ActiveIcon = activeIcon,
                InactiveIcon = inactiveIcon,
                AscendancyName = socketNode.AscendancyName,
                ClassStartIndex = null,
                GroupId = proxyNode.GroupId,
                Orbit = orbit,
                OrbitIndex = orbitIndex,
                ExpansionSocket = nodeExpansionSocket,
                MasteryEffects = null,
            };
            nodesById[node.Id] = node;
            return node;
        }
    }

    private static Dictionary<int, Node> FindRealSockets(TreeModel tree, int groupId)
    {
        var socketsByIndex = new Dictionary<int, Node>();
        foreach (var node in tree.Nodes.Values)
        {
            if (node.Type != NodeType.JewelSocket || node.GroupId != groupId || node.ExpansionSocket is not { } expansionSocket)
            {
                continue;
            }
            socketsByIndex[expansionSocket.Index] = node;
        }
        return socketsByIndex;
    }

    private static List<int> BuildNotableTemplateIndices(
        ClusterJewelDefinition definition,
        int nodeCount,
        int socketCount,
        int notableCount,
        IReadOnlyDictionary<int, Node> occupied)
    {
        var result = new List<int>(notableCount);
        foreach (var rawIndex in definition.NotableIndices)
        {
            if (result.Count == notableCount)
            {
                break;
            }
            var nodeIndex = rawIndex;
            if (definition.Size == ClusterJewelSize.Medium)
            {
                if (socketCount == 0 && notableCount == 2)
                {
                    if (nodeIndex == 6)
                    {
                        nodeIndex = 4;
                    }
                    else if (nodeIndex == 10)
                    {
                        nodeIndex = 8;
                    }
                }
                else if (nodeCount == 4)
                {
                    if (nodeIndex == 10)
                    {
                        nodeIndex = 9;
                    }
                    else if (nodeIndex == 2)
                    {
                        nodeIndex = 3;
                    }
                }
            }
            if (!occupied.ContainsKey(nodeIndex))
            {
                result.Add(nodeIndex);
            }
        }
        result.Sort();
        return result;
    }

    private static List<int> BuildSmallTemplateIndices(
        ClusterJewelDefinition definition,
        int nodeCount,
        int smallCount,
        IReadOnlyDictionary<int, Node> occupied)
    {
        var result = new List<int>(smallCount);
        foreach (var rawIndex in definition.SmallIndices)
        {
            if (result.Count == smallCount)
            {
                break;
            }
            var nodeIndex = rawIndex;
            if (definition.Size == ClusterJewelSize.Medium)
            {
                if (nodeCount == 5 && nodeIndex == 4)
                {
                    nodeIndex = 3;
                }
                else if (nodeCount == 4)
                {
                    if (nodeIndex == 8)
                    {
                        nodeIndex = 9;
                    }
                    else if (nodeIndex == 4)
                    {
                        nodeIndex = 3;
                    }
                }
            }
            if (!occupied.ContainsKey(nodeIndex))
            {
                result.Add(nodeIndex);
            }
        }
        return result;
    }

    private static int MapOrbitIndex(int templateIndex, int startOrbitIndex, int templateSlots, int skillsPerOrbit)
    {
        var corrected = (templateIndex + startOrbitIndex) % templateSlots;
        return TranslateClusterOrbitIndex(corrected, templateSlots, skillsPerOrbit);
    }

    private static int TranslateClusterOrbitIndex(int sourceOrbitIndex, int sourceNodesPerOrbit, int destinationNodesPerOrbit)
    {
        if (sourceNodesPerOrbit == destinationNodesPerOrbit)
        {
            return sourceOrbitIndex;
        }
        if (sourceNodesPerOrbit == 12 && destinationNodesPerOrbit == 16)
        {
            return TemplateToOrbit16[sourceOrbitIndex];
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
        return (int)Math.Floor(sourceOrbitIndex * destinationNodesPerOrbit / (double)sourceNodesPerOrbit);
    }

    private static ArcConnector BuildArcConnector(
        TreeModel tree,
        GroupPosition group,
        double orbitRadius,
        int orbit,
        int fromNodeId,
        int fromOrbitIndex,
        int toNodeId,
        int toOrbitIndex)
    {
        var angleList = tree.OrbitAngles[orbit];
        var fromAngle = angleList[fromOrbitIndex];
        var toAngle = angleList[toOrbitIndex];
        var sweep = toAngle - fromAngle;
        while (sweep <= 0)
        {
            sweep += Math.Tau;
        }

        return new ArcConnector(
            fromNodeId,
            toNodeId,
            group.X,
            group.Y,
            orbitRadius,
            fromAngle,
            sweep);
    }
}
