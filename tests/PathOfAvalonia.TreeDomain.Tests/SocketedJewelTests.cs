using PathOfAvalonia.TreeDomain.Import;
using PathOfAvalonia.TreeDomain.ClusterJewels;
using PathOfAvalonia.TreeApp.ViewModels;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class SocketedJewelTests
{
    [Fact]
    public void ApplyImportPreservesNonClusterSocketedJewel()
    {
        var spec = LoadSpec();
        var item = JewelItem(1, "Cobalt Jewel");

        spec.ApplyImport(new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: new[] { 55190 },
            ClusterNodeHashes: Array.Empty<int>(),
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "3.28",
            Source: "test")
        {
            ItemsById = new Dictionary<int, ImportedItem> { [1] = item },
            SocketedJewels = new[] { new ImportedSocketedJewel(55190, 1) },
        });

        Assert.True(spec.TryGetSocketedJewel(55190, out var socketed));
        Assert.Equal(item, socketed);
        Assert.Empty(spec.ActiveSubgraphs);
    }

    [Fact]
    public void SocketedJewelVisualClassifierMapsCoreJewelBases()
    {
        AssertOverlay("Crimson Jewel", "JewelSocketActiveRed", "JewelSocketActiveRedAlt");
        AssertOverlay("Viridian Jewel", "JewelSocketActiveGreen", "JewelSocketActiveGreenAlt");
        AssertOverlay("Cobalt Jewel", "JewelSocketActiveBlue", "JewelSocketActiveBlueAlt");
        AssertOverlay("Prismatic Jewel", "JewelSocketActivePrismatic", "JewelSocketActivePrismaticAlt");
    }

    [Fact]
    public void SocketedJewelVisualClassifierMapsClusterBases()
    {
        Assert.Equal("JewelSocketActiveAltPurple", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Large Cluster Jewel"), isExpansionSocket: true));
        Assert.Equal("JewelSocketActiveAltBlue", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Medium Cluster Jewel"), isExpansionSocket: true));
        Assert.Equal("JewelSocketActiveAltRed", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Small Cluster Jewel"), isExpansionSocket: true));
    }

    [Fact]
    public void SocketedJewelVisualClassifierMapsAbyssTimelessAndCharms()
    {
        AssertOverlay("Ghastly Eye Jewel", "JewelSocketActiveAbyss", "JewelSocketActiveAbyssAlt");
        AssertOverlay("Searching Eye Jewel", "JewelSocketActiveAbyss", "JewelSocketActiveAbyssAlt");
        AssertOverlay("Murderous Eye Jewel", "JewelSocketActiveAbyss", "JewelSocketActiveAbyssAlt");
        AssertOverlay("Hypnotic Eye Jewel", "JewelSocketActiveAbyss", "JewelSocketActiveAbyssAlt");
        AssertOverlay("Timeless Jewel", "JewelSocketActiveLegion", "JewelSocketActiveLegionAlt");

        Assert.Equal("CharmSocketActiveStr", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Ursine Charm"), isExpansionSocket: false));
        Assert.Equal("CharmSocketActiveInt", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Corvine Charm"), isExpansionSocket: false));
        Assert.Equal("CharmSocketActiveDex", SocketedJewelVisualClassifier.OverlayKey(JewelItem(1, "Lupine Charm"), isExpansionSocket: false));
    }

    [Fact]
    public void ForbiddenJewelsKeepTheirSocketOverlay()
    {
        Assert.Equal("JewelSocketActiveBlue", SocketedJewelVisualClassifier.OverlayKey(
            ForbiddenJewel(1, "Forbidden Flesh", "Vile Bastion"), isExpansionSocket: false));
        Assert.Equal("JewelSocketActiveRed", SocketedJewelVisualClassifier.OverlayKey(
            ForbiddenJewel(2, "Forbidden Flame", "Vile Bastion"), isExpansionSocket: false));
    }

    [Fact]
    public void ForbiddenJewelsUseTheirOwnBaseAssetsEvenWhenImportedWithTheWrongBase()
    {
        Assert.Equal("JewelSocketActiveBlue", SocketedJewelVisualClassifier.OverlayKey(
            ForbiddenJewel(1, "Forbidden Flesh", "Vile Bastion", "Viridian Jewel"), isExpansionSocket: false));
        Assert.Equal("JewelSocketActiveRed", SocketedJewelVisualClassifier.OverlayKey(
            ForbiddenJewel(2, "Forbidden Flame", "Vile Bastion", "Viridian Jewel"), isExpansionSocket: false));
    }

    [Fact]
    public void MatchingForbiddenJewelsAllocateTheirAscendancyPassive()
    {
        var spec = LoadSpec();
        var sockets = spec.Tree.Nodes.Values.Where(node => node.Type == NodeType.JewelSocket).Take(2).ToArray();
        Assert.Equal(2, sockets.Length);
        var target = spec.Tree.Nodes.Values.First(node => node.AscendancyName is not null && node.Type == NodeType.Notable);
        var flesh = ForbiddenJewel(1, "Forbidden Flesh", target.Name);
        var flame = ForbiddenJewel(2, "Forbidden Flame", target.Name);

        spec.ApplyImport(BuildWithJewels(sockets, flesh, flame));

        Assert.All(sockets, socket => Assert.True(spec.IsAllocated(socket.Id)));
        Assert.All(sockets, socket => Assert.True(spec.TryGetSocketedJewel(socket.Id, out _)));
        Assert.True(spec.IsAllocated(target.Id));
        Assert.Contains(target.Id, spec.AllocatedNodes);
    }

    [Fact]
    public void OnlyOneMatchingForbiddenJewelVariantIsActive()
    {
        var spec = LoadSpec();
        var sockets = spec.Tree.Nodes.Values.Where(node => node.Type == NodeType.JewelSocket).Take(4).ToArray();
        Assert.Equal(4, sockets.Length);
        var targets = spec.Tree.Nodes.Values
            .Where(node => node.AscendancyName is not null && node.Type == NodeType.Notable)
            .Take(2)
            .ToArray();

        spec.ApplyImport(BuildWithJewels(
            sockets,
            ForbiddenJewel(1, "Forbidden Flesh", targets[0].Name),
            ForbiddenJewel(2, "Forbidden Flame", targets[0].Name),
            ForbiddenJewel(3, "Forbidden Flesh", targets[1].Name),
            ForbiddenJewel(4, "Forbidden Flame", targets[1].Name)));

        Assert.True(spec.IsAllocated(targets[0].Id));
        Assert.False(spec.IsAllocated(targets[1].Id));
    }

    [Fact]
    public void ForbiddenJewelsUseOnlyTheirSelectedPobVariant()
    {
        var spec = LoadSpec();
        var sockets = spec.Tree.Nodes.Values.Where(node => node.Type == NodeType.JewelSocket).Take(2).ToArray();
        var targets = spec.Tree.Nodes.Values
            .Where(node => node.AscendancyName is not null && node.Type == NodeType.Notable)
            .Take(2)
            .ToArray();
        var flesh = ForbiddenVariantJewel(1, "Forbidden Flesh", targets[0].Name, targets[1].Name, selectedVariant: 2);
        var flame = ForbiddenVariantJewel(2, "Forbidden Flame", targets[0].Name, targets[1].Name, selectedVariant: 2);

        spec.ApplyImport(BuildWithJewels(sockets, flesh, flame));

        Assert.False(spec.IsAllocated(targets[0].Id));
        Assert.True(spec.IsAllocated(targets[1].Id));
        Assert.Equal(2, flesh.SelectedVariant);
        Assert.DoesNotContain(ItemViewModel.FromImported(flesh).Body, line => line.Text.Contains(targets[0].Name));
        Assert.Contains(ItemViewModel.FromImported(flesh).Body, line => line.Text.Contains(targets[1].Name));
    }

    [Fact]
    public void SpriteMapContainsSocketedJewelOverlayAssets()
    {
        using var stream = File.OpenRead(AssetPath("3_28_0", "data.json"));
        var sprites = SpriteMap.LoadPoe1FromGggTree(stream, "3_28_0/assets");

        Assert.True(sprites.Atlases.ContainsKey("jewel"));
        foreach (var key in new[]
        {
            "JewelSocketActiveRed",
            "JewelSocketActiveGreen",
            "JewelSocketActiveBlue",
            "JewelSocketActivePrismatic",
            "JewelSocketActiveAbyss",
            "JewelSocketActiveLegion",
            "JewelSocketActiveRedAlt",
            "JewelSocketActiveAbyssAlt",
            "JewelSocketActiveLegionAlt",
            "JewelSocketActiveAltPurple",
            "JewelSocketActiveAltBlue",
            "JewelSocketActiveAltRed",
        })
        {
            Assert.NotNull(sprites.Lookup("jewel", key));
        }

        Assert.True(sprites.Atlases.ContainsKey("azmeriBloodline"));
        foreach (var key in new[]
        {
            "CharmSocketActiveStr",
            "CharmSocketActiveInt",
            "CharmSocketActiveDex",
        })
        {
            Assert.NotNull(sprites.Lookup("azmeriBloodline", key));
        }
    }

    [Fact]
    public void ViewModelUsesClusterOverlayForManuallyInsertedJewel()
    {
        var spec = LoadSpec();
        var vm = new PassiveTreeViewModel(spec);

        vm.InsertCluster(55190, ClusterJewelSize.Large);

        Assert.Equal("JewelSocketActiveAltPurple", vm.SocketedJewelOverlayAt(spec.Tree.Nodes[55190]));
    }

    private static void AssertOverlay(string baseType, string normal, string expansion)
    {
        var item = JewelItem(1, baseType);
        Assert.Equal(normal, SocketedJewelVisualClassifier.OverlayKey(item, isExpansionSocket: false));
        Assert.Equal(expansion, SocketedJewelVisualClassifier.OverlayKey(item, isExpansionSocket: true));
    }

    private static ImportedItem JewelItem(int id, string baseType)
    {
        var raw = string.Join('\n', new[]
        {
            "Rarity: RARE",
            "New Item",
            baseType,
            "--------",
            "5% increased maximum Life",
        });
        return new ImportedItem(string.Empty, "RARE", "New Item", baseType, raw) { Id = id };
    }

    private static ImportedItem ForbiddenJewel(int id, string name, string passiveName, string? baseType = null)
    {
        var matchingJewel = name == "Forbidden Flesh" ? "Forbidden Flame" : "Forbidden Flesh";
        baseType ??= name == "Forbidden Flesh" ? "Cobalt Jewel" : "Crimson Jewel";
        var raw = $"""
            Rarity: Unique
            {name}
            {baseType}
            --------
            Allocates {passiveName} if you have the matching modifier on {matchingJewel}
            --------
            Corrupted
            """;
        return new ImportedItem(string.Empty, "UNIQUE", name, baseType, raw) { Id = id };
    }

    private static ImportedItem ForbiddenVariantJewel(int id, string name, string firstPassive, string secondPassive, int selectedVariant)
    {
        var matchingJewel = name == "Forbidden Flesh" ? "Forbidden Flame" : "Forbidden Flesh";
        var baseType = name == "Forbidden Flesh" ? "Cobalt Jewel" : "Crimson Jewel";
        var raw = $$"""
            Rarity: Unique
            {{name}}
            {{baseType}}
            --------
            {variant:1}Allocates {{firstPassive}} if you have the matching modifier on {{matchingJewel}}
            {variant:2}Allocates {{secondPassive}} if you have the matching modifier on {{matchingJewel}}
            Variant: {{firstPassive}}
            Variant: {{secondPassive}}
            Selected Variant: {{selectedVariant}}
            """;
        return RawItemParser.Parse(string.Empty, raw) with { Id = id };
    }

    private static ImportedBuild BuildWithJewels(IReadOnlyList<Node> sockets, params ImportedItem[] items) =>
        new(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: sockets.Select(socket => socket.Id).ToArray(),
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "3.28",
            Source: "test")
        {
            ItemsById = items.ToDictionary(item => item.Id),
            SocketedJewels = sockets.Zip(items, (socket, item) => new ImportedSocketedJewel(socket.Id, item.Id)).ToArray(),
        };

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static TreeModel LoadTree()
    {
        using var stream = File.OpenRead(AssetPath("3_28_0", "data.json"));
        return TreeLoader.LoadFromJson(stream, "3.28.0");
    }

    private static string AssetPath(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", .. parts]));
}
