using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class PassiveSpecTests
{
    [Fact]
    public void ApplyImportStoresWeaponSetAllocationForAppliedNodes()
    {
        var spec = LoadPoe2Spec();
        var nodes = spec.Tree.Nodes.Values
            .Where(node => node.Type == NodeType.Normal)
            .Take(2)
            .Select(node => node.Id)
            .ToArray();
        Assert.Equal(2, nodes.Length);
        var build = Build(nodes)
            with
            {
                AllocationSets = new Dictionary<int, PassiveAllocationSet>
                {
                    [nodes[0]] = PassiveAllocationSet.WeaponSet1,
                    [nodes[1]] = PassiveAllocationSet.WeaponSet2,
                },
            };

        spec.ApplyImport(build);

        Assert.Contains(nodes[0], spec.AllocatedNodes);
        Assert.Contains(nodes[1], spec.AllocatedNodes);
        Assert.Equal(PassiveAllocationSet.WeaponSet1, spec.AllocationSetOf(nodes[0]));
        Assert.Equal(PassiveAllocationSet.WeaponSet2, spec.AllocationSetOf(nodes[1]));
    }

    [Fact]
    public void ApplyImportSkipsWeaponSetMetadataForSkippedNodes()
    {
        var spec = LoadPoe2Spec();
        var invalidNode = -123456;
        var build = Build([invalidNode])
            with
            {
                AllocationSets = new Dictionary<int, PassiveAllocationSet>
                {
                    [invalidNode] = PassiveAllocationSet.WeaponSet1,
                },
            };

        spec.ApplyImport(build);

        Assert.Empty(spec.AllocationSets);
    }

    [Fact]
    public void ClearRemovesWeaponSetAllocationMetadata()
    {
        var spec = LoadPoe2Spec();
        var node = spec.Tree.Nodes.Values.First(n => n.Type == NodeType.Normal).Id;
        var build = Build([node])
            with
            {
                AllocationSets = new Dictionary<int, PassiveAllocationSet>
                {
                    [node] = PassiveAllocationSet.WeaponSet1,
                },
            };
        spec.ApplyImport(build);

        spec.Clear();

        Assert.Empty(spec.AllocationSets);
    }

    [Fact]
    public void SelectedAscendancyStartIsAnAllocationRoot()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var ascendancyStart = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.Type == NodeType.AscendancyStart &&
            node.AscendancyName == "Juggernaut"));
        var passives = ascendancyStart.LinkedNodes
            .Where(node => node.Type is NodeType.Normal or NodeType.Notable)
            .Take(2)
            .ToArray();

        Assert.Equal(2, passives.Length);
        Assert.Contains(ascendancyStart.Id, spec.AllocatedNodes);

        spec.AllocateMany(passives.Select(node => node.Id));
        spec.Toggle(passives[0].Id);

        Assert.DoesNotContain(passives[0].Id, spec.AllocatedNodes);
        Assert.Contains(passives[1].Id, spec.AllocatedNodes);
    }

    [Fact]
    public void ScionCanSelectScavengerAscendancy()
    {
        var spec = LoadSpec();
        spec.SetClass(0);
        spec.SetAscendancy(2);

        var scavengerStart = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.Type == NodeType.AscendancyStart &&
            node.Name == "Scavenger" &&
            node.AscendancyName == "Reliquarian"));

        Assert.Equal(new[] { "None", "Ascendant", "Scavenger" }, spec.Classes.AscendancyNames(0));
        Assert.Contains(scavengerStart.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void CannotAllocateUnselectedAscendancyNodes()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var berserkerNode = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.AscendancyName == "Berserker" &&
            node.Type == NodeType.Notable &&
            node.Name == "Aspect of Carnage"));

        spec.Toggle(berserkerNode.Id);
        spec.AllocateMany(new[] { berserkerNode.Id });

        Assert.DoesNotContain(berserkerNode.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void HoverPathRejectsUnselectedAscendancyNodes()
    {
        var spec = LoadSpec();
        spec.SetClass(1);
        spec.SetAscendancy(1);

        var berserkerNode = Assert.Single(spec.Tree.Nodes.Values.Where(node =>
            node.AscendancyName == "Berserker" &&
            node.Type == NodeType.Notable &&
            node.Name == "Aspect of Carnage"));

        var path = spec.HoverPathTo(berserkerNode.Id);

        Assert.True(path.IsEmpty);
    }

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static PassiveSpec LoadPoe2Spec() => new(LoadPoe2Tree());

    private static ImportedBuild Build(IReadOnlyList<int> nodeIds) =>
        new(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: nodeIds,
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0_4",
            Source: "test");

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", "3_28_0", "data.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28.0");
    }

    private static TreeModel LoadPoe2Tree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "0_5_0", "data.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadPoe2FromJson(stream, "0.5.0");
    }
}
