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

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static TreeModel LoadTree()
    {
        using var stream = File.OpenRead(AssetPath("3_28_0", "data.json"));
        return TreeLoader.LoadFromJson(stream, "3.28.0");
    }

    private static string AssetPath(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", .. parts]));
}
