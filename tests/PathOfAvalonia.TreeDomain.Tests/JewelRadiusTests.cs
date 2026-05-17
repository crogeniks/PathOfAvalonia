using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.Jewels;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class JewelRadiusTests
{
    [Fact]
    public void RadiusTablesMatchPobValues()
    {
        var poe1 = JewelRadiusTable.For(GameId.PathOfExile1, "3.28");
        Assert.Equal((0, 960), Bounds(poe1[1]));
        Assert.Equal((0, 1440), Bounds(poe1[2]));
        Assert.Equal((0, 1800), Bounds(poe1[3]));
        Assert.Equal((0, 2400), Bounds(poe1[4]));
        Assert.Equal((0, 2880), Bounds(poe1[5]));
        Assert.Equal((960, 1320), Bounds(poe1[6]));
        Assert.Equal((2400, 2880), Bounds(poe1[10]));

        var poe2 = JewelRadiusTable.For(GameId.PathOfExile2, "0.4");
        Assert.Equal((0, 1200), Bounds(poe2[1]));
        Assert.Equal((0, 1380), Bounds(poe2[2]));
        Assert.Equal((0, 1560), Bounds(poe2[3]));
        Assert.Equal((0, 1800), Bounds(poe2[4]));
        Assert.Equal((780, 1140), Bounds(poe2[5]));
        Assert.Equal((2160, 2520), Bounds(poe2[12]));
    }

    [Fact]
    public void RadiusMembershipUsesAnnulusAndExcludesSourceProxyMasteryAndKeystoneSockets()
    {
        var tree = LoadPoe1Tree();
        var table = JewelRadiusTable.For(tree.GameId, tree.Version);
        var socket = tree.Nodes[55190];
        var sockets = RadiusMembership.BuildForSockets(tree, table);
        var keystones = RadiusMembership.BuildForKeystones(tree, table);

        Assert.DoesNotContain(socket.Id, sockets[socket.Id].NodesByRadiusIndex[1]);
        Assert.DoesNotContain(tree.Nodes.Values.Where(n => n.Type == NodeType.Proxy).Select(n => n.Id), sockets[socket.Id].NodesByRadiusIndex[1].Contains);
        Assert.DoesNotContain(tree.Nodes.Values.Where(n => n.Type == NodeType.Mastery).Select(n => n.Id), sockets[socket.Id].NodesByRadiusIndex[1].Contains);

        var annulus = table[6];
        var member = tree.Nodes.Values.First(n =>
            n.Id != socket.Id &&
            n.Type is not NodeType.Proxy and not NodeType.Mastery &&
            DistanceSquared(n, socket) >= annulus.Inner * annulus.Inner &&
            DistanceSquared(n, socket) <= annulus.Outer * annulus.Outer);
        Assert.Contains(member.Id, sockets[socket.Id].NodesByRadiusIndex[6]);

        var insideInner = tree.Nodes.Values.FirstOrDefault(n =>
            n.Id != socket.Id &&
            DistanceSquared(n, socket) < annulus.Inner * annulus.Inner &&
            DistanceSquared(n, socket) <= annulus.Outer * annulus.Outer);
        if (insideInner is not null)
        {
            Assert.DoesNotContain(insideInner.Id, sockets[socket.Id].NodesByRadiusIndex[6]);
        }

        var keystone = tree.Nodes.Values.First(n => n.Type == NodeType.Keystone);
        Assert.DoesNotContain(tree.Nodes.Values.Where(n => n.Type == NodeType.JewelSocket).Select(n => n.Id), keystones[keystone.Id].NodesByRadiusIndex[1].Contains);
    }

    [Fact]
    public void SocketedUnallocatedRadiusJewelHasOverlayButNoActiveRadius()
    {
        var spec = LoadPoe1Spec();
        var item = RadiusItem(1, "Cobalt Jewel", "Radius: Medium");
        spec.ApplyImport(BuildImport([], [new ImportedSocketedJewel(55190, 1)], item));
        var vm = new PassiveTreeViewModel(spec);

        Assert.True(spec.TryGetSocketedJewel(55190, out _));
        Assert.Equal("JewelSocketActiveBlueAlt", vm.SocketedJewelOverlayAt(spec.Tree.Nodes[55190]));
        Assert.Empty(spec.ActiveJewelRadii);
    }

    [Fact]
    public void AllocatedRadiusJewelCreatesOneVisual()
    {
        var spec = LoadPoe1Spec();
        var item = RadiusItem(1, "Cobalt Jewel", "Radius: Medium");
        spec.ApplyImport(BuildImport([55190], [new ImportedSocketedJewel(55190, 1)], item));

        var visual = Assert.Single(spec.ActiveJewelRadii);
        Assert.Equal(55190, visual.SourceNodeId);
        Assert.Equal(1440, visual.OuterRadius);
        Assert.Equal(JewelRadiusVisualStyle.Normal, visual.Style);
    }

    [Fact]
    public void ThreadOfHopeAnnulusAllowsUnconnectedAllocationOnlyInRing()
    {
        var spec = LoadPoe1Spec();
        var item = RadiusItem(1, "Crimson Jewel", """
            Radius: Variable
            Only affects Passives in Small Ring
            Passives in Radius can be Allocated without being connected to your tree
            """, name: "Thread of Hope");
        spec.ApplyImport(BuildImport([55190], [new ImportedSocketedJewel(55190, 1)], item));

        var visual = Assert.Single(spec.ActiveJewelRadii);
        Assert.Equal(JewelRadiusVisualStyle.Annulus, visual.Style);

        var target = NodeInBand(spec.Tree, spec.Tree.Nodes[55190], visual.InnerRadius, visual.OuterRadius, spec.AllocatedNodes);
        Assert.True(spec.IsAllocatedByRadiusJewel(target.Id));
        spec.Toggle(target.Id);
        Assert.Contains(target.Id, spec.AllocatedNodes);

        var outside = spec.Tree.Nodes.Values.First(n =>
            n.Type == NodeType.Normal &&
            DistanceSquared(n, spec.Tree.Nodes[55190]) > visual.OuterRadius * visual.OuterRadius &&
            n.LinkedNodes.All(l => !spec.AllocatedNodes.Contains(l.Id)));
        spec.Toggle(outside.Id);
        Assert.DoesNotContain(outside.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void ImpossibleEscapeCentersVisualOnNamedKeystone()
    {
        var spec = LoadPoe1Spec();
        var keystone = spec.Tree.Nodes.Values.First(n => n.Type == NodeType.Keystone);
        var item = RadiusItem(1, "Viridian Jewel", $"""
            Radius: Small
            Passives in Radius of {keystone.Name} can be Allocated without being connected to your tree
            """, name: "Impossible Escape");
        spec.ApplyImport(BuildImport([55190], [new ImportedSocketedJewel(55190, 1)], item));

        var visual = Assert.Single(spec.ActiveJewelRadii);
        Assert.Equal(JewelRadiusVisualStyle.KeystoneCentered, visual.Style);
        Assert.Equal(keystone.X, visual.X);
        Assert.Equal(keystone.Y, visual.Y);
    }

    [Fact]
    public void EffectiveNodeStatsAreOverlayedWithoutMutatingBaseNode()
    {
        var tree = LoadPoe1Tree();
        var socket = tree.Nodes.Values.First(n => n.Type == NodeType.JewelSocket);
        var target = tree.Nodes.Values.First(n =>
            n.Type == NodeType.Normal &&
            n.Stats.Any(s => s.Contains("Strength", StringComparison.OrdinalIgnoreCase)) &&
            DistanceSquared(n, socket) <= 960 * 960);
        var spec = new PassiveSpec(tree);
        var item = RadiusItem(1, "Cobalt Jewel", """
            Radius: Small
            Strength from Passives in Radius is Transformed to Dexterity
            """);
        spec.ApplyImport(BuildImport([socket.Id], [new ImportedSocketedJewel(socket.Id, 1)], item));

        var effective = spec.EffectiveNode(target.Id);

        Assert.Contains(effective.EffectiveStats, s => s.Contains("Dexterity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tree.Nodes[target.Id].Stats, s => s.Contains("Dexterity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tree.Nodes[target.Id].Stats, s => s.Contains("Strength", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeallocatingSocketRemovesRadiusOnlyAllocation()
    {
        var spec = LoadPoe1Spec();
        var item = RadiusItem(1, "Viridian Jewel", """
            Radius: Small
            Passives in Radius can be Allocated without being connected to your tree
            """, name: "Intuitive Leap");
        spec.ApplyImport(BuildImport([55190], [new ImportedSocketedJewel(55190, 1)], item));
        var visual = Assert.Single(spec.ActiveJewelRadii);
        var target = NodeInBand(spec.Tree, spec.Tree.Nodes[55190], visual.InnerRadius, visual.OuterRadius);

        spec.Toggle(target.Id);
        Assert.Contains(target.Id, spec.AllocatedNodes);
        spec.Toggle(55190);

        Assert.DoesNotContain(target.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void TimelessParsesConquerorAndVisualStyle()
    {
        var spec = LoadPoe1Spec();
        var item = RadiusItem(1, "Timeless Jewel", """
            Radius: Large
            Passives in radius are Conquered by the Karui
            """, name: "Lethal Pride");
        spec.ApplyImport(BuildImport([55190], [new ImportedSocketedJewel(55190, 1)], item));

        var visual = Assert.Single(spec.ActiveJewelRadii);
        Assert.Equal(TimelessConqueror.Karui, visual.Conqueror);
        Assert.Equal(JewelRadiusVisualStyle.Timeless, visual.Style);
    }

    [Fact]
    public void ViewModelExposesActiveRadiiAndEffectiveStats()
    {
        var tree = LoadPoe1Tree();
        var socket = tree.Nodes.Values.First(n => n.Type == NodeType.JewelSocket);
        var target = tree.Nodes.Values.First(n =>
            n.Type == NodeType.Normal &&
            n.Stats.Any(s => s.Contains("Strength", StringComparison.OrdinalIgnoreCase)) &&
            DistanceSquared(n, socket) <= 960 * 960);
        var spec = new PassiveSpec(tree);
        var item = RadiusItem(1, "Cobalt Jewel", """
            Radius: Small
            Strength from Passives in Radius is Transformed to Dexterity
            """);
        spec.ApplyImport(BuildImport([socket.Id], [new ImportedSocketedJewel(socket.Id, 1)], item));
        var vm = new PassiveTreeViewModel(spec);

        Assert.Single(vm.ActiveJewelRadii);
        Assert.Contains(vm.PassiveEffectLines(target), s => s.Contains("Dexterity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RequiredJewelRadiusAssetsExist()
    {
        foreach (var file in new[] { "ring.png", "small_ring.png", "ShadedOuterRing.png", "ShadedInnerRing.png" })
        {
            Assert.True(File.Exists(SharedAsset(file)), file);
        }

        Assert.True(File.Exists(Poe1JewelAsset("PassiveSkillScreenKaruiJewelCircle1.png")));
        Assert.True(File.Exists(Poe1JewelAsset("PassiveSkillScreenKaruiJewelCircle2.png")));
    }

    private static (double Inner, double Outer) Bounds(JewelRadiusBand band) => (band.Inner, band.Outer);

    private static Node NodeInBand(TreeModel tree, Node center, double inner, double outer, IReadOnlySet<int>? allocated = null) =>
        tree.Nodes.Values.First(n =>
            n.Id != center.Id &&
            n.Type is NodeType.Normal or NodeType.Notable &&
            (allocated is null || n.LinkedNodes.All(l => !allocated.Contains(l.Id))) &&
            DistanceSquared(n, center) >= inner * inner &&
            DistanceSquared(n, center) <= outer * outer);

    private static double DistanceSquared(Node a, Node b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static ImportedBuild BuildImport(IReadOnlyList<int> allocated, IReadOnlyList<ImportedSocketedJewel> jewels, ImportedItem item) =>
        new(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: allocated,
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "3.28",
            Source: "test")
        {
            ItemsById = new Dictionary<int, ImportedItem> { [item.Id] = item },
            SocketedJewels = jewels,
        };

    private static ImportedItem RadiusItem(int id, string baseType, string body, string name = "Radius Jewel")
    {
        var raw = string.Join('\n', "Rarity: Unique", name, baseType, "--------", body);
        return new ImportedItem(string.Empty, "Unique", name, baseType, raw) { Id = id };
    }

    private static PassiveSpec LoadPoe1Spec() => new(LoadPoe1Tree());

    private static TreeModel LoadPoe1Tree()
    {
        using var stream = File.OpenRead(Poe1Asset("tree_3_28.json"));
        return TreeLoader.LoadFromJson(stream, "3.28");
    }

    private static string Poe1Asset(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", fileName));

    private static string Poe1JewelAsset(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", "JewelRadius", fileName));

    private static string SharedAsset(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "Shared", "JewelRadius", fileName));
}
