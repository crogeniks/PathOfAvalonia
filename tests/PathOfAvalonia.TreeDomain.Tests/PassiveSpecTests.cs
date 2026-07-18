using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class PassiveSpecTests
{
    [Fact]
    public void MasteryAllocationRequiresValidUniqueEffectAndSupportsReplacementAndDeallocation()
    {
        var spec = new PassiveSpec(CreateMasteryTestTree());

        spec.Toggle(2);
        spec.Toggle(3);
        Assert.DoesNotContain(3, spec.AllocatedNodes);

        Assert.False(spec.AllocateMastery(3, 999));
        Assert.DoesNotContain(3, spec.AllocatedNodes);

        Assert.True(spec.AllocateMastery(3, 101));
        Assert.Contains(3, spec.AllocatedNodes);
        Assert.Equal(101, spec.SelectedMasteryEffect(3)?.Id);

        Assert.False(spec.AllocateMastery(4, 101));
        Assert.DoesNotContain(4, spec.AllocatedNodes);
        Assert.True(spec.AllocateMastery(3, 102));
        Assert.Equal(102, spec.SelectedMasteryEffect(3)?.Id);
        Assert.True(spec.AllocateMastery(4, 101));

        spec.Toggle(3);
        Assert.DoesNotContain(3, spec.AllocatedNodes);
        Assert.Null(spec.SelectedMasteryEffect(3));
        Assert.Contains(4, spec.AllocatedNodes);
    }

    [Fact]
    public void ImportSkipsMasteriesWithoutValidUniqueEffectsAndReportsInvalidSelections()
    {
        var spec = new PassiveSpec(CreateMasteryTestTree());
        var build = Build([2, 3, 4]) with
        {
            MasterySelections = new Dictionary<int, int>
            {
                [3] = 101,
                [4] = 101,
            },
        };

        var result = spec.ApplyImport(build);

        Assert.Contains(3, spec.AllocatedNodes);
        Assert.DoesNotContain(4, spec.AllocatedNodes);
        Assert.Equal(101, spec.SelectedMasteryEffect(3)?.Id);
        Assert.Equal(1, result.InvalidMasterySelections);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void ImportReportsAnEffectThatDoesNotBelongToItsMastery()
    {
        var spec = new PassiveSpec(CreateMasteryTestTree());
        var build = Build([2, 3]) with
        {
            MasterySelections = new Dictionary<int, int> { [3] = 999 },
        };

        var result = spec.ApplyImport(build);

        Assert.DoesNotContain(3, spec.AllocatedNodes);
        Assert.Equal(1, result.InvalidMasterySelections);
        Assert.Equal(1, result.Skipped);
    }
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

    [Fact]
    public void Poe2OracleUnseenPathNodesRequireTheUnseenPath()
    {
        var spec = LoadPoe2Spec();
        spec.SetClass(4);
        spec.SetAscendancy(1);

        spec.Toggle(47190);
        Assert.DoesNotContain(47190, spec.AllocatedNodes);
        Assert.True(spec.HoverPathTo(47190).IsEmpty);

        spec.AllocateMany([11335, 5571]);
        spec.Toggle(47190);

        Assert.Contains(5571, spec.AllocatedNodes);
        Assert.Contains(47190, spec.AllocatedNodes);
    }

    [Fact]
    public void Poe2ImportPrunesOracleUnseenPathNodesWithoutRequirement()
    {
        var spec = LoadPoe2Spec();
        var build = Build([47190, 32905])
            with
            {
                ClassInternalId = "Druid",
                AscendancyInternalId = "Druid1",
            };

        var result = spec.ApplyImport(build);

        Assert.DoesNotContain(47190, spec.AllocatedNodes);
        Assert.DoesNotContain(32905, spec.AllocatedNodes);
        Assert.Equal(0, result.Applied);
        Assert.Equal(2, result.Skipped);
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

    private static TreeModel CreateMasteryTestTree()
    {
        var start = TestNode(1, NodeType.ClassStart, classStartIndex: 0);
        var passive = TestNode(2, NodeType.Normal);
        var firstMastery = TestNode(3, NodeType.Mastery, [new MasteryEffect(101, ["First"]), new MasteryEffect(102, ["Second"])]);
        var secondMastery = TestNode(4, NodeType.Mastery, [new MasteryEffect(101, ["First"])]);
        Link(start, passive);
        Link(passive, firstMastery);
        Link(passive, secondMastery);
        return new TreeModel
        {
            GameId = GameId.PathOfExile1,
            Version = "test",
            Classes = ClassCatalog.CreatePoe1(),
            Nodes = new Dictionary<int, Node> { [1] = start, [2] = passive, [3] = firstMastery, [4] = secondMastery },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [],
            Bounds = new TreeBounds(0, 0, 1, 1),
            Groups = new Dictionary<int, GroupPosition>(),
            SkillsPerOrbit = [],
            OrbitRadii = [],
            OrbitAngles = [],
        };
    }

    private static Node TestNode(int id, NodeType type, IReadOnlyList<MasteryEffect>? effects = null, int? classStartIndex = null) => new()
    {
        Id = id,
        Name = $"Node {id}",
        Type = type,
        X = 0,
        Y = 0,
        GroupId = 0,
        Orbit = 0,
        OrbitIndex = 0,
        ClassStartIndex = classStartIndex,
        MasteryEffects = effects,
    };

    private static void Link(Node first, Node second)
    {
        first.LinkedNodes.Add(second);
        second.LinkedNodes.Add(first);
    }
}
