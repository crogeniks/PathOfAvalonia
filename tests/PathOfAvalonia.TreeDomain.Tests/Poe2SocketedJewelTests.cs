using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class Poe2SocketedJewelTests
{
    [Fact]
    public void ApplyImportAssociatesSocketedTreeJewel()
    {
        var tree = LoadTree();
        var socket = tree.Nodes.Values.First(n => n.Type == NodeType.JewelSocket);
        var item = RawItemParser.Parse("", """
            Rarity: Magic
            Ruby
            --------
            Adds fire damage
            """) with { Id = 12 };
        var build = new ImportedBuild(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: "0_4",
            Source: "test")
        {
            ItemsById = new Dictionary<int, ImportedItem> { [12] = item },
            SocketedJewels = [new ImportedSocketedJewel(socket.Id, 12)],
        };
        var spec = new PassiveSpec(tree, tree.Classes, GameFeatureFlags.Poe2Milestone2);

        spec.ApplyImport(build);

        Assert.True(spec.TryGetSocketedJewel(socket.Id, out var socketed));
        Assert.Equal("Ruby", socketed.BaseType);
    }

    [Theory]
    [InlineData("Ruby", SocketedJewelVisualKind.RubyJewel)]
    [InlineData("Emerald", SocketedJewelVisualKind.EmeraldJewel)]
    [InlineData("Sapphire", SocketedJewelVisualKind.SapphireJewel)]
    [InlineData("Time-Lost Diamond", SocketedJewelVisualKind.TimeLost)]
    [InlineData("Soul Core", SocketedJewelVisualKind.SoulCore)]
    [InlineData("Charm", SocketedJewelVisualKind.Charm)]
    public void ClassifiesPoe2JewelKinds(string baseType, SocketedJewelVisualKind expected)
    {
        Assert.Equal(expected, SocketedJewelVisualClassifier.Classify(baseType, "", ""));
    }

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE2", "tree_0_4.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadPoe2FromJson(stream, "0.4");
    }
}
