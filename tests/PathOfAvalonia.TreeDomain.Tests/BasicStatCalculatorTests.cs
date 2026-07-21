using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Calculations;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class BasicStatCalculatorTests
{
    [Fact]
    public void CalculatesPoe1BasicsFromLevelPassivesAndEquippedItemText()
    {
        var spec = CreateSpec(
            [
                "+10 to Strength",
                "20% increased maximum Life",
                "20% increased Armour and Evasion Rating",
                "+15% to all Elemental Resistances",
                "+5% to Fire and Cold Resistances",
                "+1% to maximum Fire Resistance",
                "+5% Chance to Block Attack Damage",
                "+10% chance to Suppress Spell Damage",
                "Regenerate 1% of Life per second",
                "12% increased Movement Speed",
                "10% increased maximum Life while on Full Life",
            ]);
        var items = new[]
        {
            RawItemParser.Parse("Helmet", """
                Rarity: Rare
                Stalwart Shell
                Iron Hat
                --------
                Armour: 100 (augmented)
                --------
                100% increased Armour
                +50 to maximum Life
                +20 to Intelligence
                +30% to Fire Resistance
                """),
            RawItemParser.Parse("Ring 1", """
                Rarity: Rare
                Balanced Circle
                Gold Ring
                --------
                +10 to all Attributes
                """),
            RawItemParser.Parse("Weapon 2", """
                Rarity: Rare
                Guard Emblem
                Iron Shield
                --------
                Chance to Block: 25%
                """),
        };

        var result = BasicStatCalculator.Calculate(spec, items, level: 10, resistancePenalty: -60);

        Assert.Equal(40, result.Strength);
        Assert.Equal(30, result.Dexterity);
        Assert.Equal(50, result.Intelligence);
        Assert.Equal(274, result.Life);
        Assert.Equal(119, result.Mana);
        Assert.Equal(120, result.Armour);
        Assert.Equal(19, result.Evasion);
        Assert.Equal(30, result.BlockChance);
        Assert.Equal(10, result.SpellSuppressionChance);
        Assert.Equal(12, result.MovementSpeedModifier);
        Assert.Equal(-10, result.FireResistance.Uncapped);
        Assert.Equal(76, result.FireResistance.Maximum);
        Assert.Equal(-40, result.ColdResistance.Uncapped);
        Assert.Equal(2.74, result.LifeRegeneration, precision: 2);
        Assert.True(result.Coverage.IsPartial);
        Assert.Contains(result.Coverage.UnsupportedExamples, line => line.Contains("Full Life", StringComparison.Ordinal));
    }

    [Fact]
    public void UsesPoe2BasePoolsAndAttributeBonuses()
    {
        var classes = new ClassCatalog
        {
            Classes =
            [
                new CharacterClassInfo(0, 0, "Witch", [new AscendancyInfo(0, "None", string.Empty, null)])
                {
                    BaseStrength = 7,
                    BaseDexterity = 7,
                    BaseIntelligence = 15,
                },
            ],
        };
        var spec = CreateSpec([], GameId.PathOfExile2, classes);

        var result = BasicStatCalculator.Calculate(spec, [], level: 10, resistancePenalty: -60);

        Assert.Equal(150, result.Life);
        Assert.Equal(100, result.Mana);
        Assert.Equal(7, result.Evasion);
        Assert.Equal(-60, result.FireResistance.Uncapped);
        Assert.Equal(0, result.ChaosResistance.Uncapped);
        Assert.Equal(4, result.ManaRegeneration);
    }

    [Fact]
    public void SavedPobArmourWithoutFinalPropertiesIsNotTreatedAsGlobalDefence()
    {
        var spec = CreateSpec([]);
        var helmet = RawItemParser.Parse("Helmet", """
            Rarity: RARE
            Veiled Crown
            Hubris Circlet
            Item Level: 86
            Quality: 20
            120% increased Energy Shield
            +50 to maximum Energy Shield
            +80 to maximum Life
            """);

        var result = BasicStatCalculator.Calculate(spec, [helmet], level: 1);

        Assert.Equal(38 + 12 + 10 + 80, result.Life);
        Assert.Equal(0, result.EnergyShield);
        Assert.True(result.Coverage.HasIncompleteItemDefences);
        Assert.True(result.Coverage.IsPartial);
    }

    [Fact]
    public void EquipmentViewModelRecalculatesWhenLevelChanges()
    {
        var spec = CreateSpec(["10% increased maximum Life"]);
        var viewModel = new EquipmentViewModel(spec);
        var levelOneLife = viewModel.CalculatedStats!.Values.Life;

        viewModel.CharacterLevel = 10;

        Assert.True(viewModel.HasCalculatedStats);
        Assert.True(viewModel.HasContent);
        Assert.True(viewModel.CalculatedStats!.Values.Life > levelOneLife);
        Assert.Equal(10, viewModel.CalculatedStats.Values.Level);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void HoverPreviewProjectsQueuedPassiveWithoutMutatingTheSpec()
    {
        var spec = CreateSpec(["+10 to Strength"], allocatePassive: false);
        var equipment = new EquipmentViewModel(spec);
        var tree = new PassiveTreeViewModel(spec);
        _ = new BuildWorkspaceState(
            GameRegistry.CreatePoe1(),
            spec,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            tree,
            equipment);
        var baselineStrength = equipment.CalculatedStats!.Values.Strength;

        tree.SetHover(2);

        Assert.DoesNotContain(2, spec.AllocatedNodes);
        Assert.Equal(PassiveAllocationPreviewKind.Allocate, tree.AllocationPreview.Kind);
        Assert.Equal(baselineStrength + 10, equipment.TreeCalculatedStats!.Values.Strength);
        Assert.Contains(
            equipment.PassivePreview!.Changes,
            change => change.Label == "Strength" && change.DeltaText == "(+10)");
        Assert.Same(equipment.PassivePreview, tree.BasicStatPreview);

        tree.SetHover(null);

        Assert.Null(equipment.PassivePreview);
        Assert.Equal(baselineStrength, equipment.TreeCalculatedStats!.Values.Strength);
    }

    [Fact]
    public void HoverPreviewProjectsPassiveRefundWithoutMutatingTheSpec()
    {
        var spec = CreateSpec(["+10 to Strength"]);
        var equipment = new EquipmentViewModel(spec);
        var tree = new PassiveTreeViewModel(spec);
        _ = new BuildWorkspaceState(
            GameRegistry.CreatePoe1(),
            spec,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            tree,
            equipment);
        var baselineStrength = equipment.CalculatedStats!.Values.Strength;

        tree.SetHover(2);

        Assert.Contains(2, spec.AllocatedNodes);
        Assert.Equal(PassiveAllocationPreviewKind.Deallocate, tree.AllocationPreview.Kind);
        Assert.Equal(baselineStrength - 10, equipment.TreeCalculatedStats!.Values.Strength);
        Assert.Contains(
            equipment.PassivePreview!.Changes,
            change => change.Label == "Strength" && change.DeltaText == "(-10)");
    }

    [Fact]
    public void AllocationPreviewIncludesQueuedPathAndRefundDependents()
    {
        var spec = CreateChainSpec();

        var allocation = spec.PreviewAllocationChange(3);

        Assert.Equal(PassiveAllocationPreviewKind.Allocate, allocation.Kind);
        Assert.Equal([2, 3], allocation.NodeIds.Order());
        Assert.DoesNotContain(2, spec.AllocatedNodes);
        Assert.DoesNotContain(3, spec.AllocatedNodes);

        spec.AllocateMany([2, 3]);
        var refund = spec.PreviewAllocationChange(2);

        Assert.Equal(PassiveAllocationPreviewKind.Deallocate, refund.Kind);
        Assert.Equal([2, 3], refund.NodeIds.Order());
        Assert.Contains(2, spec.AllocatedNodes);
        Assert.Contains(3, spec.AllocatedNodes);
    }

    private static PassiveSpec CreateSpec(
        IReadOnlyList<string> stats,
        GameId gameId = GameId.PathOfExile1,
        ClassCatalog? classes = null,
        bool allocatePassive = true)
    {
        classes ??= ClassCatalog.CreatePoe1();
        var start = Node(1, NodeType.ClassStart, []);
        var passive = Node(2, NodeType.Normal, stats);
        start.LinkedNodes.Add(passive);
        passive.LinkedNodes.Add(start);
        var tree = new TreeModel
        {
            GameId = gameId,
            Version = "test",
            Classes = classes,
            Nodes = new Dictionary<int, Node> { [1] = start, [2] = passive },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [],
            Bounds = new TreeBounds(-1, -1, 1, 1),
            Groups = new Dictionary<int, GroupPosition> { [1] = new(0, 0) },
            SkillsPerOrbit = [1],
            OrbitRadii = [0],
            OrbitAngles = [new[] { 0d }],
        };
        var spec = new PassiveSpec(tree, classes, gameId == GameId.PathOfExile2
            ? GameFeatureFlags.Poe2Milestone2
            : GameFeatureFlags.Poe1);
        spec.ApplyImport(new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: allocatePassive ? [2] : [],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "test",
            Source: "test"));
        return spec;
    }

    private static PassiveSpec CreateChainSpec()
    {
        var classes = ClassCatalog.CreatePoe1();
        var start = Node(1, NodeType.ClassStart, []);
        var first = Node(2, NodeType.Normal, ["+10 to Strength"]);
        var second = Node(3, NodeType.Normal, ["+10 to Dexterity"]);
        start.LinkedNodes.Add(first);
        first.LinkedNodes.Add(start);
        first.LinkedNodes.Add(second);
        second.LinkedNodes.Add(first);
        var tree = new TreeModel
        {
            GameId = GameId.PathOfExile1,
            Version = "test",
            Classes = classes,
            Nodes = new Dictionary<int, Node> { [1] = start, [2] = first, [3] = second },
            ClusterNodeTemplates = new Dictionary<string, Node>(),
            Connectors = [],
            Bounds = new TreeBounds(-1, -1, 1, 1),
            Groups = new Dictionary<int, GroupPosition> { [1] = new(0, 0) },
            SkillsPerOrbit = [1],
            OrbitRadii = [0],
            OrbitAngles = [new[] { 0d }],
        };
        return new PassiveSpec(tree, classes, GameFeatureFlags.Poe1);
    }

    private static Node Node(int id, NodeType type, IReadOnlyList<string> stats) => new()
    {
        Id = id,
        Name = $"Node {id}",
        Type = type,
        X = id,
        Y = 0,
        Stats = stats,
        ClassStartIndex = type == NodeType.ClassStart ? 0 : null,
        GroupId = 1,
        Orbit = 0,
        OrbitIndex = 0,
    };
}
