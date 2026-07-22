using Avalonia.Media;
using Avalonia.Media.Imaging;
using Moq;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Atlas;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class AtlasTreeViewModelTests
{
    [Fact]
    public void CounterTurnsRedAtLimitAndRemainsInformationalAboveIt()
    {
        var tree = LoadTree();
        var viewModel = new AtlasTreeViewModel(
            GameRegistry.CreatePoe1(),
            tree,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new NullImageResolver(),
            Mock.Of<IGameAssetService>(),
            Mock.Of<IGameAssetLayoutRegistry>());
        var connectedNodeIds = ConnectedAllocatableNodes(tree).ToArray();

        viewModel.Spec.RestoreConnectedAllocations(connectedNodeIds.Take(138));
        Assert.Equal(137, viewModel.AllocatedPointCount);
        Assert.False(viewModel.IsPointLimitReached);

        viewModel.Spec.RestoreConnectedAllocations(connectedNodeIds.Take(139));
        Assert.Equal(138, viewModel.AllocatedPointCount);
        Assert.True(viewModel.IsPointLimitReached);
        Assert.Equal(Color.FromRgb(0xEF, 0x58, 0x58), Assert.IsType<SolidColorBrush>(viewModel.PointUsageBrush).Color);

        viewModel.Spec.RestoreConnectedAllocations(connectedNodeIds.Take(140));
        Assert.Equal(139, viewModel.AllocatedPointCount);
        Assert.True(viewModel.IsPointLimitReached);
    }

    [Fact]
    public void SearchAndClusterCategoryHighlightUseAtlasSpecificNodes()
    {
        var tree = LoadTree();
        var viewModel = new AtlasTreeViewModel(
            GameRegistry.CreatePoe1(),
            tree,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new NullImageResolver(),
            Mock.Of<IGameAssetService>(),
            Mock.Of<IGameAssetLayoutRegistry>());

        viewModel.SearchText = "Ritual Altars";
        Assert.NotEmpty(viewModel.SearchResultNodeIds);
        Assert.All(viewModel.SearchResultNodeIds, id => Assert.Contains(
            tree.Nodes[id].Stats,
            stat => stat.Contains("Ritual", StringComparison.OrdinalIgnoreCase)));

        var category = tree.Nodes.Values.First(node =>
            node.Type == AtlasNodeType.ClusterIcon
            && tree.Nodes.Values.Count(other => other.Type == AtlasNodeType.ClusterIcon && other.Icon == node.Icon) > 1);
        Assert.True(viewModel.HighlightSimilarClusters(category.Id));
        Assert.True(viewModel.HighlightedClusterIconNodeIds.Count > 1);
    }

    [Fact]
    public void SearchDimsNonMatchingAggregatedModifiers()
    {
        var tree = LoadTree();
        var viewModel = new AtlasTreeViewModel(
            GameRegistry.CreatePoe1(),
            tree,
            new SpriteMap { Atlases = new Dictionary<string, SpriteAtlas>() },
            new NullImageResolver(),
            Mock.Of<IGameAssetService>(),
            Mock.Of<IGameAssetLayoutRegistry>());
        viewModel.Spec.RestoreConnectedAllocations(ConnectedAllocatableNodes(tree));

        Assert.All(
            viewModel.AggregatedStatGroups.SelectMany(group => group.Stats),
            stat => Assert.Equal(1, stat.Opacity));

        viewModel.SearchText = "Corrupted Gaze";

        var stats = viewModel.AggregatedStatGroups.SelectMany(group => group.Stats).ToArray();
        Assert.Equal(
            1,
            Assert.Single(stats, stat => stat.Text.StartsWith("Abyss Jewels found", StringComparison.Ordinal)).Opacity);
        Assert.Contains(stats, stat => stat.Opacity == 0.25);
    }

    private static IEnumerable<int> ConnectedAllocatableNodes(AtlasTreeModel tree)
    {
        var visited = new HashSet<int> { tree.StartNodeId };
        var queue = new Queue<AtlasNode>();
        queue.Enqueue(tree.Nodes[tree.StartNodeId]);
        yield return tree.StartNodeId;
        while (queue.TryDequeue(out var node))
        {
            foreach (var linked in node.LinkedNodes)
            {
                if (linked.Type == AtlasNodeType.ClusterIcon || !visited.Add(linked.Id))
                {
                    continue;
                }
                queue.Enqueue(linked);
                yield return linked.Id;
            }
        }
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

    private sealed class NullImageResolver : ITreeImageAssetResolver
    {
        public Bitmap? LoadBitmap(string relativePath) => null;
        public Bitmap? LoadBackground(string treeVersion) => null;
    }
}
