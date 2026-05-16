using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class ClusterJewelTests
{
    [Fact]
    public void LoaderPreservesExpansionSocketMetadata()
    {
        var tree = LoadTree();

        var largeSocket = tree.Nodes[55190];
        Assert.Equal("Large Jewel Socket", largeSocket.Name);
        Assert.Equal(2, largeSocket.ExpansionSocket?.Size);
        Assert.Equal(2, largeSocket.ExpansionSocket?.Index);
        Assert.Equal(30275, largeSocket.ExpansionSocket?.ProxyNodeId);

        var mediumSocket = tree.Nodes[33753];
        Assert.Equal("Medium Jewel Socket", mediumSocket.Name);
        Assert.Equal(1, mediumSocket.ExpansionSocket?.Size);
        Assert.Equal(2, mediumSocket.ExpansionSocket?.Index);
        Assert.Equal(50179, mediumSocket.ExpansionSocket?.ProxyNodeId);

        var smallSocket = tree.Nodes[22748];
        Assert.Equal("Small Jewel Socket", smallSocket.Name);
        Assert.Equal(0, smallSocket.ExpansionSocket?.Size);
        Assert.Equal(0, smallSocket.ExpansionSocket?.Index);
        Assert.Equal(56439, smallSocket.ExpansionSocket?.ProxyNodeId);
    }

    [Fact]
    public void ValidationRulesMatchSocketSizeRestrictions()
    {
        var spec = LoadSpec();

        Assert.Equal(
            new[] { ClusterJewelSize.Large, ClusterJewelSize.Medium, ClusterJewelSize.Small },
            spec.AllowedClusterSizes(55190));
        Assert.Equal(
            new[] { ClusterJewelSize.Medium, ClusterJewelSize.Small },
            spec.AllowedClusterSizes(33753));
        Assert.Equal(
            new[] { ClusterJewelSize.Small },
            spec.AllowedClusterSizes(22748));
        Assert.Empty(spec.AllowedClusterSizes(61419));
    }

    [Fact]
    public void LargeClusterUsesProxyGroupCenterAndRealNestedSocketPositions()
    {
        var spec = LoadSpec();
        spec.SetClusterJewel(55190, new ClusterJewelSpec(55190, ClusterJewelSize.Large, 8, 2, Array.Empty<string>()));

        var subgraph = spec.ActiveSubgraphs[55190];
        var proxyNode = spec.Tree.Nodes[30275];
        var proxyGroup = spec.Tree.Groups[proxyNode.GroupId];

        Assert.Equal(proxyGroup.X, subgraph.ClusterCenterX);
        Assert.Equal(proxyGroup.Y, subgraph.ClusterCenterY);

        var generatedSockets = subgraph.Nodes.Where(node => node.Type == NodeType.JewelSocket).OrderBy(node => node.ExpansionSocket?.Index).ToArray();
        Assert.Equal(2, generatedSockets.Length);
        AssertSocketMatchesRealNode(spec.Tree, generatedSockets[0], proxyNode.GroupId, 0);
        AssertSocketMatchesRealNode(spec.Tree, generatedSockets[1], proxyNode.GroupId, 2);
    }

    [Fact]
    public void MediumFourPassiveWithoutSocketUsesPobNotableAdjustment()
    {
        var tree = LoadTree();
        var subgraph = ClusterJewelResolver.Resolve(
            tree,
            tree.Nodes[33753],
            new ClusterJewelSpec(33753, ClusterJewelSize.Medium, 4, 0, new[] { "Eye to Eye", "Repeater" }),
            0x10000,
            0x10010);

        var notables = subgraph.Nodes.Where(node => node.Type == NodeType.Notable).OrderBy(node => node.OrbitIndex).ToArray();
        Assert.Equal(2, notables.Length);

        var expected = new[]
        {
            ResolveOrbitIndex(tree, 50179, ClusterJewelSize.Medium, 4),
            ResolveOrbitIndex(tree, 50179, ClusterJewelSize.Medium, 8),
        };
        Assert.Equal(expected, notables.Select(node => node.OrbitIndex).ToArray());
    }

    [Fact]
    public void MediumFourPassiveWithSocketUsesAdjustedNotableAndSmallPlacement()
    {
        var tree = LoadTree();
        var subgraph = ClusterJewelResolver.Resolve(
            tree,
            tree.Nodes[33753],
            new ClusterJewelSpec(33753, ClusterJewelSize.Medium, 4, 1, new[] { "Eye to Eye" }),
            0x11000,
            0x11010);

        var notable = Assert.Single(subgraph.Nodes.Where(node => node.Type == NodeType.Notable));
        Assert.Equal(ResolveOrbitIndex(tree, 50179, ClusterJewelSize.Medium, 9), notable.OrbitIndex);

        var smalls = subgraph.Nodes.Where(node => node.Type == NodeType.Normal).OrderBy(node => node.OrbitIndex).ToArray();
        Assert.Equal(
            new[]
            {
                ResolveOrbitIndex(tree, 50179, ClusterJewelSize.Medium, 0),
                ResolveOrbitIndex(tree, 50179, ClusterJewelSize.Medium, 3),
            }.OrderBy(value => value).ToArray(),
            smalls.Select(node => node.OrbitIndex).ToArray());
    }

    [Fact]
    public void LifecycleSupportsReplaceRemoveAndNestedRestrictions()
    {
        var spec = LoadSpec();
        spec.SetClusterJewel(55190, new ClusterJewelSpec(55190, ClusterJewelSize.Large, 8, 2, Array.Empty<string>()));

        var firstGeneratedPassiveIds = spec.ActiveSubgraphs[55190].Nodes
            .Where(node => node.Type != NodeType.JewelSocket)
            .Select(node => node.Id)
            .ToHashSet();
        var mediumSocket = spec.ActiveSubgraphs[55190].Nodes
            .Where(node => node.Type == NodeType.JewelSocket && node.Name == "Medium Jewel Socket")
            .OrderBy(node => node.ExpansionSocket?.Index)
            .First();

        Assert.False(spec.CanInsertCluster(mediumSocket.Id, ClusterJewelSize.Large));

        spec.SetClusterJewel(mediumSocket.Id, new ClusterJewelSpec(mediumSocket.Id, ClusterJewelSize.Medium, 4, 1, Array.Empty<string>()));
        Assert.Contains(mediumSocket.Id, spec.ActiveSubgraphs.Keys);

        spec.SetClusterJewel(55190, new ClusterJewelSpec(55190, ClusterJewelSize.Medium, 4, 1, Array.Empty<string>()));
        Assert.DoesNotContain(spec.ActiveSubgraphs[55190].Nodes, node => firstGeneratedPassiveIds.Contains(node.Id));
        Assert.DoesNotContain(mediumSocket.Id, spec.ActiveSubgraphs.Keys);

        spec.RemoveClusterJewel(55190);
        Assert.Empty(spec.ActiveSubgraphs);
    }

    [Fact]
    public void ViewModelManualInsertionSupportsPassiveAndNotableCounts()
    {
        var spec = LoadSpec();
        var vm = new PassiveTreeViewModel(spec);

        Assert.Equal(new[] { 8, 9, 10, 11, 12 }, vm.ManualPassiveCounts(ClusterJewelSize.Large));
        Assert.Equal(new[] { 0, 1, 2, 3 }, vm.ManualNotableCounts(ClusterJewelSize.Large, 8));

        vm.InsertCluster(55190, ClusterJewelSize.Large, 12, 3);

        var subgraph = Assert.Single(spec.ActiveSubgraphs.Values);
        Assert.Equal(12, subgraph.Nodes.Count);
        Assert.Equal(2, subgraph.Nodes.Count(node => node.Type == NodeType.JewelSocket));
        Assert.Equal(3, subgraph.Nodes.Count(node => node.Type == NodeType.Notable));
        Assert.Equal(7, subgraph.Nodes.Count(node => node.Type == NodeType.Normal));
    }

    [Fact]
    public void ImportedClusterJewelParserReadsStructureFromPobItemText()
    {
        var item = ClusterItem(
            1,
            "Large Cluster Jewel",
            "Cluster Jewel Node Count: 8",
            "{crafted}Adds 8 Passive Skills",
            "{crafted}2 Added Passive Skills are Jewel Sockets",
            "{crafted}Added Small Passive Skills grant: 12% increased Chaos Damage",
            "1 Added Passive Skill is Misery Everlasting",
            "1 Added Passive Skill is Unholy Grace",
            "1 Added Passive Skill is Wicked Pall");

        Assert.True(ImportedClusterJewelParser.TryParse(item, out var cluster));
        Assert.Equal(ClusterJewelSize.Large, cluster.Size);
        Assert.Equal(8, cluster.PassiveCount);
        Assert.Equal(2, cluster.SocketCount);
        Assert.Equal(
            new[] { "Misery Everlasting", "Unholy Grace", "Wicked Pall" },
            cluster.NotableNames);
    }

    [Fact]
    public void ApplyImportRestoresClusterSubgraphsAndAllocatesImportedClusterHashes()
    {
        var spec = LoadSpec();
        var clusterNodeId = ClusterNodeBase(spec.Tree, 55190, ClusterJewelSize.Large);
        var item = ClusterItem(
            1,
            "Large Cluster Jewel",
            "{crafted}Adds 8 Passive Skills",
            "{crafted}2 Added Passive Skills are Jewel Sockets",
            "1 Added Passive Skill is Misery Everlasting",
            "1 Added Passive Skill is Unholy Grace",
            "1 Added Passive Skill is Wicked Pall");

        var result = spec.ApplyImport(new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: new[] { 55190 },
            ClusterNodeHashes: new[] { clusterNodeId },
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "3.28",
            Source: "test")
        {
            ItemsById = new Dictionary<int, ImportedItem> { [1] = item },
            SocketedJewels = new[] { new ImportedSocketedJewel(55190, 1) },
        });

        Assert.Contains(55190, spec.ActiveSubgraphs.Keys);
        Assert.Contains(clusterNodeId, spec.AllocatedNodes);
        Assert.Equal(2, result.Applied);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void ApplyImportRestoresNestedClustersAfterTheirParent()
    {
        var temp = LoadSpec();
        temp.SetClusterJewel(55190, new ClusterJewelSpec(55190, ClusterJewelSize.Large, 8, 2, Array.Empty<string>()));
        var childSocket = temp.ActiveSubgraphs[55190].Nodes
            .Where(node => node.Type == NodeType.JewelSocket)
            .OrderBy(node => node.ExpansionSocket?.Index)
            .First();

        var spec = LoadSpec();
        var childNodeId = ClusterNodeBase(spec.Tree, 55190, ClusterJewelSize.Large, childSocket, ClusterJewelSize.Medium);
        var large = ClusterItem(
            1,
            "Large Cluster Jewel",
            "{crafted}Adds 8 Passive Skills",
            "{crafted}2 Added Passive Skills are Jewel Sockets");
        var medium = ClusterItem(
            2,
            "Medium Cluster Jewel",
            "{crafted}Adds 4 Passive Skills",
            "{crafted}1 Added Passive Skill is a Jewel Socket",
            "1 Added Passive Skill is Brush with Death",
            "1 Added Passive Skill is Exposure Therapy");

        spec.ApplyImport(new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: new[] { 55190, childSocket.Id },
            ClusterNodeHashes: new[] { childNodeId },
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "3.28",
            Source: "test")
        {
            ItemsById = new Dictionary<int, ImportedItem> { [1] = large, [2] = medium },
            SocketedJewels = new[]
            {
                new ImportedSocketedJewel(childSocket.Id, 2),
                new ImportedSocketedJewel(55190, 1),
            },
        });

        Assert.Contains(55190, spec.ActiveSubgraphs.Keys);
        Assert.Contains(childSocket.Id, spec.ActiveSubgraphs.Keys);
        Assert.Contains(childNodeId, spec.AllocatedNodes);
    }

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "tree_3_28.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28");
    }

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static ImportedItem ClusterItem(int id, string baseType, params string[] bodyLines)
    {
        var raw = string.Join('\n', new[]
        {
            "Rarity: RARE",
            "New Item",
            baseType,
        }.Concat(bodyLines));
        return new ImportedItem(string.Empty, "RARE", "New Item", baseType, raw) { Id = id };
    }

    private static int ClusterNodeBase(TreeModel tree, int socketId, ClusterJewelSize size)
    {
        var socket = tree.Nodes[socketId];
        var lineage = 0x10000 + (socket.ExpansionSocket!.Index << 6);
        return lineage + (ClusterJewelData.GetDefinition(size).SizeIndex << 4);
    }

    private static int ClusterNodeBase(TreeModel tree, int outerSocketId, ClusterJewelSize outerSize, Node childSocket, ClusterJewelSize childSize)
    {
        var outerSocket = tree.Nodes[outerSocketId];
        var lineage = 0x10000 + (outerSocket.ExpansionSocket!.Index << 6);
        lineage += childSocket.ExpansionSocket!.Size == 1 ? childSocket.ExpansionSocket.Index << 9 : 0;
        return lineage + (ClusterJewelData.GetDefinition(childSize).SizeIndex << 4);
    }

    private static void AssertSocketMatchesRealNode(TreeModel tree, Node generatedSocket, int groupId, int expansionIndex)
    {
        var realSocket = Assert.Single(tree.Nodes.Values.Where(node =>
            node.Type == NodeType.JewelSocket &&
            node.GroupId == groupId &&
            node.ExpansionSocket?.Index == expansionIndex));

        Assert.Equal(realSocket.Name, generatedSocket.Name);
        Assert.Equal(realSocket.ExpansionSocket?.Size, generatedSocket.ExpansionSocket?.Size);
        Assert.Equal(realSocket.ExpansionSocket?.ProxyNodeId, generatedSocket.ExpansionSocket?.ProxyNodeId);
        Assert.Equal(realSocket.X, generatedSocket.X, 6);
        Assert.Equal(realSocket.Y, generatedSocket.Y, 6);
    }

    private static int ResolveOrbitIndex(TreeModel tree, int proxyNodeId, ClusterJewelSize size, int templateIndex)
    {
        var definition = ClusterJewelData.GetDefinition(size);
        var orbit = definition.SizeIndex + 1;
        var templateSlots = definition.TotalIndices;
        var skillsPerOrbit = tree.SkillsPerOrbit[orbit];
        var corrected = (templateIndex + ClusterJewelData.GetOrbitOffset(proxyNodeId, definition.SizeIndex)) % templateSlots;

        return (templateSlots, skillsPerOrbit) switch
        {
            (12, 16) => corrected switch
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
                _ => throw new ArgumentOutOfRangeException(nameof(templateIndex)),
            },
            (6, 16) => corrected switch
            {
                0 => 0,
                1 => 3,
                2 => 5,
                3 => 8,
                4 => 11,
                5 => 13,
                _ => throw new ArgumentOutOfRangeException(nameof(templateIndex)),
            },
            _ => corrected,
        };
    }
}
