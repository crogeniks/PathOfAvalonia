using PathOfAvalonia.TreeDomain.Atlas;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class AtlasTreeTests
{
    [Fact]
    public void LoaderCreatesAtlasSpecificTreeWithOneStartAndCategoryIcons()
    {
        var tree = LoadTree();

        Assert.Equal("3.29.0", tree.Version);
        Assert.Equal(138, tree.PointLimit);
        Assert.Equal(1027, tree.Nodes.Count);
        Assert.Equal(tree.StartNodeId, Assert.Single(tree.Nodes.Values.Where(node => node.Type == AtlasNodeType.Start)).Id);
        Assert.Equal(126, tree.Nodes.Values.Count(node => node.Type == AtlasNodeType.ClusterIcon));
        Assert.NotEmpty(tree.GroupVisuals);
        Assert.Contains(tree.GroupVisuals, visual => visual.AtlasKey == "startNode");
    }

    [Fact]
    public void AllocationCanExceedDisplayPointLimit()
    {
        var tree = LoadTree();
        var spec = new AtlasPassiveSpec(tree);
        var queue = new Queue<AtlasNode>();
        var visited = new HashSet<int> { tree.StartNodeId };
        queue.Enqueue(tree.Nodes[tree.StartNodeId]);

        while (queue.TryDequeue(out var node) && spec.AllocatedPointCount <= tree.PointLimit)
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (linked.Type == AtlasNodeType.ClusterIcon || !visited.Add(linked.Id))
                {
                    continue;
                }
                spec.Toggle(linked.Id);
                queue.Enqueue(linked);
                if (spec.AllocatedPointCount > tree.PointLimit)
                {
                    break;
                }
            }
        }

        Assert.Equal(139, spec.AllocatedPointCount);
    }

    [Fact]
    public void GatewayPairsRemainLinkedWithoutDrawableConnectors()
    {
        var tree = LoadTree();
        var gateways = tree.Nodes.Values.Where(node => node.IsGateway).ToArray();

        Assert.Equal(6, gateways.Length);
        foreach (var gateway in gateways)
        {
            var pairedGateway = Assert.Single(gateway.LinkedNodes.Where(node => node.IsGateway));
            Assert.Contains(gateway, pairedGateway.LinkedNodes);
            Assert.DoesNotContain(tree.Connectors, connector =>
                (connector.FromId == gateway.Id && connector.ToId == pairedGateway.Id)
                || (connector.FromId == pairedGateway.Id && connector.ToId == gateway.Id));
            Assert.Contains(tree.Connectors, connector =>
                (connector.FromId == gateway.Id || connector.ToId == gateway.Id)
                && connector.FromId != pairedGateway.Id
                && connector.ToId != pairedGateway.Id);
        }
    }

    [Fact]
    public void AggregatorSumsRepeatedChanceModifiers()
    {
        var tree = LoadTree();
        var ritualNodes = tree.Nodes.Values
            .Where(node => node.Stats.Contains("Your Maps have +10% chance to contain Ritual Altars"))
            .Select(node => node.Id)
            .ToArray();

        var aggregated = AtlasPassiveStatAggregator.Aggregate(tree, ritualNodes);

        var ritual = Assert.Single(aggregated, stat => stat.Text.Contains("chance to contain Ritual Altars"));
        Assert.Equal("Your Maps have +70% chance to contain Ritual Altars", ritual.Text);
        Assert.Equal(7, ritual.SourceCount);
    }

    [Fact]
    public void AggregatorGroupsModifiersByAtlasMechanicType()
    {
        var tree = LoadTree();
        var ritualNodes = tree.Nodes.Values
            .Where(node => node.Stats.Any(stat =>
                stat.StartsWith("Your Maps have +", StringComparison.Ordinal)
                && stat.EndsWith("chance to contain Ritual Altars", StringComparison.Ordinal)))
            .Select(node => node.Id)
            .ToArray();

        var groups = AtlasPassiveStatAggregator.AggregateGroups(tree, ritualNodes);

        var ritualGroup = Assert.Single(groups);
        Assert.Equal("Ritual", ritualGroup.Type);
        Assert.Contains(
            ritualGroup.Stats,
            stat => stat.Text == "Your Maps have +100% chance to contain Ritual Altars");
    }

    [Fact]
    public void StandaloneAbyssNotablesAreInferredAsAbyssModifiers()
    {
        var tree = LoadTree();
        var expectedStats = new[]
        {
            "Abyss Pits in your Maps have 5% chance to spawn all Monsters as at least Magic for each prior Pit closed",
            "Abysses in your Maps have a 20% chance to contain 3 additional Pits",
            "Abysses in your Maps roll number of Pits twice, keeping the highest value",
        };
        var nodeIds = tree.Nodes.Values
            .Where(node => node.Stats.Any(expectedStats.Contains))
            .Select(node => node.Id)
            .ToArray();

        var groups = AtlasPassiveStatAggregator.AggregateGroups(tree, nodeIds);

        var abyss = Assert.Single(groups);
        Assert.Equal("Abyss", abyss.Type);
        Assert.Equal(3, abyss.Stats.Count);
    }

    [Fact]
    public void GenericScarabModifierIsGroupedByEffectRatherThanNodeCluster()
    {
        var tree = LoadTree();
        const string text = "3% increased Scarabs found in your Maps";
        var harvestGroups = tree.Nodes.Values
            .Where(node => node.Type == AtlasNodeType.ClusterIcon && node.Name == "Harvest")
            .Select(node => node.GroupId)
            .ToHashSet();
        var scarabNodes = tree.Nodes.Values.Where(node =>
            harvestGroups.Contains(node.GroupId)
            && node.Stats.Contains(text)).ToArray();
        Assert.NotEmpty(scarabNodes);

        var group = Assert.Single(AtlasPassiveStatAggregator.AggregateGroups(
            tree,
            scarabNodes.Select(node => node.Id)));

        Assert.Equal("Scarabs", group.Type);
        var aggregated = Assert.Single(group.Stats);
        Assert.Equal("9% increased Scarabs found in your Maps", aggregated.Text);
        Assert.Equal(3, aggregated.SourceCount);
    }

    [Fact]
    public void GroupingAccountsForEveryModifierExactlyOnce()
    {
        var tree = LoadTree();
        var nodes = tree.Nodes.Values
            .Where(node => !node.IsGateway
                && node.Type is not (AtlasNodeType.Start or AtlasNodeType.ClusterIcon))
            .ToArray();

        var groups = AtlasPassiveStatAggregator.AggregateGroups(tree, nodes.Select(node => node.Id));

        Assert.Equal(
            nodes.Sum(node => node.Stats.Count),
            groups.SelectMany(group => group.Stats).Sum(stat => stat.SourceCount));
    }

    [Theory]
    [InlineData("Sturdy Construction", "Ichor Pumps", "Blight")]
    [InlineData("Adaptive Reaction", "Flammable Burrows", "Breach")]
    [InlineData("Altered Prophecy", "Scrying Orb", "Divination Cards")]
    [InlineData("Prolific Essence", "Imprisoned Monster", "Essence")]
    [InlineData("Contested Development", "resident Architects", "Incursion")]
    [InlineData("Emblematic", "Timeless Splinters", "Legion")]
    [InlineData("Amber Infusion", "Ore Deposits", "Settlers of Kalguur")]
    public void ModifierTypeRecognisesDatasetMechanicVocabulary(
        string nodeName,
        string statFragment,
        string expectedType)
    {
        var tree = LoadTree();
        var node = Assert.Single(tree.Nodes.Values.Where(node => node.Name == nodeName));

        var group = Assert.Single(
            AtlasPassiveStatAggregator.AggregateGroups(tree, [node.Id]),
            group => group.Stats.Any(stat => stat.Text.Contains(statFragment, StringComparison.Ordinal)));

        Assert.Equal(expectedType, group.Type);
    }

    [Fact]
    public void AggregatorOmitsGatewayNavigationLines()
    {
        var tree = LoadTree();
        var gatewayIds = tree.Nodes.Values
            .Where(node => node.IsGateway)
            .Select(node => node.Id);

        Assert.Empty(AtlasPassiveStatAggregator.AggregateGroups(tree, gatewayIds));
        Assert.Empty(AtlasPassiveStatAggregator.Aggregate(tree, gatewayIds));
    }

    [Fact]
    public void CorruptedGazeRemainsOneAggregatedModifier()
    {
        var tree = LoadTree();
        var corruptedGaze = Assert.Single(tree.Nodes.Values.Where(node => node.Name == "Corrupted Gaze"));

        var sourceStat = Assert.Single(corruptedGaze.Stats);
        Assert.Equal(
            "Abyss Jewels found in your Maps have 20% chance to be Corrupted and have 5 or 6 random Modifiers",
            sourceStat);

        var aggregated = AtlasPassiveStatAggregator.Aggregate(tree, [corruptedGaze.Id]);
        Assert.Equal(sourceStat, Assert.Single(aggregated).Text);
    }

    [Fact]
    public void AggregatorDoesNotSumTierQualifiers()
    {
        var tree = LoadTree();
        const string source = "Tier 1-15 Maps found have 5% chance to become 1 tier higher";
        var nodes = tree.Nodes.Values
            .Where(node => node.Stats.Contains(source))
            .Take(2)
            .Select(node => node.Id)
            .ToArray();

        var aggregated = AtlasPassiveStatAggregator.Aggregate(tree, nodes);

        var result = Assert.Single(aggregated);
        Assert.Equal("Tier 1-15 Maps found have 10% chance to become 1 tier higher", result.Text);
    }

    private static AtlasTreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "assets", "PoE1", "3_29_0", "Atlas", "data.json"));
        using var stream = File.OpenRead(path);
        return new Poe1AtlasTreeLoader().Load(stream, "3.29.0", GameId.PathOfExile1);
    }
}
